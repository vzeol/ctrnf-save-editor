using System.IO.Compression;
using System.Text.Json;
using CtrNfSave.Updates;

namespace CtrNfSave.Tests;

internal static partial class Program
{
    private static void UpdateTests()
    {
        static UpdateVersion Version(string text) => UpdateVersion.TryParse(text, out var version) ? version : throw new InvalidOperationException(text);
        Test("update versions: numeric ordering and prereleases", () =>
        {
            Assert(Version("v1.0.1").CompareTo(Version("1.0.0")) > 0, "Patch ordering.");
            Assert(Version("1.10.0").CompareTo(Version("1.9.9")) > 0, "Numeric ordering.");
            Assert(Version("1.1.0-preview.1").CompareTo(Version("1.1.0")) < 0, "Prerelease ordering.");
            Assert(Version("1.1.0-preview.2").CompareTo(Version("1.1.0-preview.10")) < 0, "Numeric suffix ordering.");
            foreach (string text in new[] { "garbage", "1.0", "1.0.0-", "1.0.0/../../x", "9999999999999.0.0", "1.0.0\n" })
                Assert(!UpdateVersion.TryParse(text, out _), "Invalid version accepted: " + text);
        });
        static string Release(bool draft = false, bool prerelease = false, string tag = "v1.0.2") => JsonSerializer.Serialize(new
        {
            draft, prerelease, tag_name = tag, body = new string('x', 20000), assets = new[] {
                new { name = "ctrnf-save-editor-1.0.2-win-x64.zip", browser_download_url = "https://github.com/vzeol/ctrnf-save-editor/releases/download/v1.0.2/package.zip" },
                new { name = "ctrnf-save-editor-1.0.2-win-x64.zip.sha256", browser_download_url = "https://release-assets.githubusercontent.com/example/checksum" } }
        });
        Test("release JSON and exact asset selection", () =>
        {
            var release = UpdateRelease.Parse(Release())!;
            Assert(release.Version == Version("1.0.2") && release.Body.Length == 16000 && release.SelectAssets() is not null, "Release parsing.");
            Assert(UpdateRelease.Parse(Release(draft: true)) is null && UpdateRelease.Parse(Release(prerelease: true)) is null && UpdateRelease.Parse(Release(tag: "v1.0.2-preview.1")) is null && UpdateRelease.Parse(Release(tag: "garbage")) is null, "Unstable release accepted.");
            Assert((release with { Assets = release.Assets.Take(1).ToArray() }).SelectAssets() is null, "Missing checksum accepted.");
            Assert((release with { Assets = release.Assets.Append(release.Assets[0]).ToArray() }).SelectAssets() is null, "Duplicate accepted.");
            Assert((release with { Version = Version("1.0.3") }).SelectAssets() is null, "Wrong version asset accepted.");
            Assert((release with { Assets = release.Assets.Select(a => a with { Url = "https://example.com/a" }).ToArray() }).SelectAssets() is null, "Unsafe assets accepted.");
        });
        Test("download host allow-list", () =>
        {
            foreach (string url in new[] { "https://github.com/file", "https://objects.githubusercontent.com/file" }) Assert(UpdateRelease.AllowedDownload(url), "Allowed host rejected.");
            foreach (string url in new[] { "http://github.com/file", "https://github.com.evil.test/file", "https://evilgithubusercontent.com/file", "https://githubusercontent.com/file", "file:///a", "https://user@github.com/a", "https://github.com:444/a" }) Assert(!UpdateRelease.AllowedDownload(url), "Unsafe URL accepted.");
        });
        Test("checksum sidecar format and filename", () =>
        {
            string hash = new('a', 64);
            Assert(UpdatePackage.ParseChecksum(hash + "  package.zip\r\n", "package.zip") == hash, "Checksum parse failed.");
            Reject(() => UpdatePackage.ParseChecksum(hash + "  other.zip", "package.zip"));
            Reject(() => UpdatePackage.ParseChecksum("garbage  package.zip", "package.zip"));
            Reject(() => UpdatePackage.ParseChecksum(hash + "  package.zip\n" + hash + "  other.zip", "package.zip"));
        });
        Test("archive traversal, root executable and fresh staging", () =>
        {
            string root = Path.Combine(Environment.CurrentDirectory, "artifacts", "tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                foreach (string entry in new[] { "../escape", "a/../../escape", "/absolute", @"C:\absolute", @"a\..\escape", @"\\server\file", "a./file", "a /file", "CON", "a/NUL.txt", "a/*.txt", "a/stream:data" }) Reject(() => UpdatePackage.EntryPath(root, entry));
                string zip = Path.Combine(root, "package.zip");
                void Archive(params string[] names)
                {
                    using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
                    foreach (string name in names) { using var writer = new StreamWriter(archive.CreateEntry(name).Open()); writer.Write("synthetic"); }
                }
                Archive("ctrnf-save-gui.exe", "../escape");
                string staging = Path.Combine(root, "stage"); Reject(() => UpdatePackage.Extract(zip, staging));
                Assert(!Directory.Exists(staging) && !File.Exists(Path.Combine(root, "escape")), "Rejected archive wrote files.");
                File.Delete(zip); Archive("nested/ctrnf-save-gui.exe"); Reject(() => UpdatePackage.Extract(zip, staging));
                File.Delete(zip); Archive("ctrnf-save-gui.exe", "save.sav"); Reject(() => UpdatePackage.Extract(zip, staging));
                File.Delete(zip); Archive("ctrnf-save-gui.exe", "licenses/", "licenses/license.txt"); UpdatePackage.Extract(zip, staging);
                Assert(File.ReadAllText(Path.Combine(staging, "licenses", "license.txt")) == "synthetic", "Nested extraction failed.");
                Reject(() => UpdatePackage.Extract(zip, staging));
                string cancelled = Path.Combine(root, "cancelled");
                try { UpdatePackage.Extract(zip, cancelled, new CancellationToken(true)); throw new InvalidOperationException("Cancellation ignored."); } catch (OperationCanceledException) { }
            }
            finally { Directory.Delete(root, true); }
        });
        Test("update settings: missing, corrupt and round-trip", () =>
        {
            string root = Path.Combine(Environment.CurrentDirectory, "artifacts", "tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string file = Path.Combine(root, "settings.json"); Assert(UpdateSettings.Read(file).CheckUpdatesAtStartup, "Missing default.");
                foreach (string text in new[] { "broken", "null", "[]", "{\"checkUpdatesAtStartup\":\"wrong\"}" })
                { File.WriteAllText(file, text); Assert(UpdateSettings.Read(file).CheckUpdatesAtStartup, "Corrupt default."); }
                var settings = new UpdateSettings { CheckUpdatesAtStartup = false, SkippedVersion = "1.0.2" };
                File.WriteAllText(file, settings.ToJson()); Assert(UpdateSettings.Read(file) == settings, "Settings round-trip.");
                Assert(UpdateSettings.Parse("{}").CheckUpdatesAtStartup, "Missing property default.");
            }
            finally { Directory.Delete(root, true); }
        });
    }
}
