using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using CtrNfSave.Updates;

namespace CtrNfSave.Gui;

internal sealed class UpdateService
{
    public const string Repository = "https://github.com/vzeol/ctrnf-save-editor";
    public static string Version => typeof(UpdateService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ctrnf-save-editor");
    private static string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public UpdateSettings Settings { get; private set; } = UpdateSettings.Read(SettingsPath);
    public void SetSettings(UpdateSettings settings)
    {
        EnsurePlainDirectory(DataDirectory);
        string temp = Path.Combine(DataDirectory, "settings-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(settings.ToJson());
            File.Move(temp, SettingsPath, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Settings = settings;
    }
    private static HttpClient Client(TimeSpan timeout)
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ctrnf-save-editor/" + Version);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json"); return client;
    }
    public async Task<UpdateRelease?> Check(CancellationToken token)
    {
        using var client = Client(TimeSpan.FromSeconds(8));
        using var response = await client.GetAsync("https://api.github.com/repos/vzeol/ctrnf-save-editor/releases/latest", token);
        response.EnsureSuccessStatusCode();
        return UpdateRelease.Parse(await response.Content.ReadAsStringAsync(token));
    }
    public static void OpenPage(string url, IWin32Window owner)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show(owner, L.T("browserFailed"), L.T("about"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
    private static void EnsurePlainDirectory(string path)
    {
        for (DirectoryInfo? directory = new(Path.GetFullPath(path)); directory is not null; directory = directory.Parent)
            if (directory.Exists && (directory.Attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked updater directory rejected.");
        Directory.CreateDirectory(path);
    }
    public static bool CanInstall()
    {
        string root = AppContext.BaseDirectory;
        string data = Path.TrimEndingDirectorySeparator(Path.GetFullPath(DataDirectory));
        string install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (data.Equals(install, StringComparison.OrdinalIgnoreCase) || data.StartsWith(install + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || install.StartsWith(data + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        if (!new[] { "ctrnf-save-gui.exe", "ctrnf-save.exe", "LICENSE.md", "CHANGELOG.md" }.All(n => File.Exists(Path.Combine(root, n)))) return false;
        try
        {
            for (DirectoryInfo? directory = new(root); directory is not null; directory = directory.Parent)
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return false;
            string probe = Path.Combine(root, ".update-probe-" + Guid.NewGuid().ToString("N"));
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }
    private static async Task Download(HttpClient client, ReleaseAsset asset, string path, IProgress<int> progress, CancellationToken token)
    {
        string url = asset.Url;
        for (int redirect = 0; redirect < 6; redirect++)
        {
            if (!UpdateRelease.AllowedDownload(url)) throw new InvalidDataException("Unsafe download URL.");
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                var location = response.Headers.Location ?? throw new InvalidDataException("Missing redirect URL.");
                url = new Uri(new Uri(url), location).AbsoluteUri; continue;
            }
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            byte[] buffer = new byte[81920]; long total = 0;
            while (true)
            {
                int read = await source.ReadAsync(buffer, token); if (read == 0) break;
                total += read;
                long limit = asset.Name.EndsWith(".sha256", StringComparison.Ordinal) ? 4096 : 1024L * 1024 * 1024;
                if (total > limit) throw new InvalidDataException("Download exceeds size limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
                if (response.Content.Headers.ContentLength is > 0 and var length) progress.Report((int)Math.Min(100, total * 100 / length));
            }
            progress.Report(100); return;
        }
        throw new InvalidDataException("Too many redirects.");
    }
    public static async Task<string> Prepare(UpdateRelease release, IProgress<int> progress, CancellationToken token)
    {
        if (!CanInstall()) throw new IOException("The release directory is not writable.");
        if (!UpdateVersion.TryParse(Version, out _)) throw new InvalidDataException("Invalid running version.");
        // A rollback copy of this version may survive an earlier update; never reuse it.
        string previous = Path.Combine(DataDirectory, "previous", Version);
        if (Directory.Exists(previous)) previous += "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var assets = release.SelectAssets() ?? throw new InvalidDataException("Release assets unavailable.");
        string root = Path.Combine(DataDirectory, "updates", release.Version.ToString());
        string work = Path.Combine(root, Guid.NewGuid().ToString("N")); EnsurePlainDirectory(work);
        using var client = Client(TimeSpan.FromMinutes(30));
        string zip = Path.Combine(work, assets.Zip.Name), checksum = Path.Combine(work, assets.Checksum.Name);
        await Download(client, assets.Checksum, checksum, progress, token);
        await Download(client, assets.Zip, zip, progress, token);
        string expected = UpdatePackage.ParseChecksum(await File.ReadAllTextAsync(checksum, token), assets.Zip.Name);
        await using (var stream = File.OpenRead(zip))
        {
            string actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 mismatch.");
        }
        string stage = Path.Combine(work, "staging");
        await Task.Run(() => UpdatePackage.Extract(zip, stage, token), token);
        token.ThrowIfCancellationRequested();
        string script = Path.Combine(work, "install.ps1");
        File.WriteAllText(script, InstallerScript(stage, work, previous), new System.Text.UTF8Encoding(true)); return script;
    }
    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    private static string InstallerScript(string stage, string work, string previous)
    {
        string install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        return $$"""
            $ErrorActionPreference = 'Stop'
            $install = {{Quote(install)}}
            $stage = {{Quote(stage)}}
            $previous = {{Quote(previous)}}
            $dataRoot = {{Quote(DataDirectory)}}
            $log = {{Quote(Path.Combine(work, "update.log"))}}
            $backedUp = $false
            function Assert-PlainTree([string]$root) {
                $directory = [IO.DirectoryInfo]::new($root)
                while ($null -ne $directory) {
                    if ($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked directory rejected.' }
                    $directory = $directory.Parent
                }
                if (Get-ChildItem -LiteralPath $root -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Linked entry rejected.' }
            }
            try {
                'Waiting for editor exit.' | Out-File -LiteralPath $log
                $editorProcess = Get-Process -Id {{Environment.ProcessId}} -ErrorAction SilentlyContinue
                if ($editorProcess) {
                    if (-not $editorProcess.WaitForExit(120000)) { throw 'Editor did not exit; installation cancelled.' }
                }
                $prefix = [IO.Path]::GetFullPath($dataRoot).TrimEnd('\') + '\'
                foreach ($path in @($stage, $previous, $log)) {
                    if (-not [IO.Path]::GetFullPath($path).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Path outside updater data directory.' }
                }
                Assert-PlainTree $install
                Assert-PlainTree $stage
                if (Test-Path -LiteralPath $previous) { throw 'Rollback directory already exists; preserve it before retrying.' }
                # Reject an existing linked parent before creating the backup directory.
                $parent = [IO.DirectoryInfo]::new([IO.Path]::GetDirectoryName($previous))
                while ($null -ne $parent) {
                    if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked backup directory rejected.' }
                    $parent = $parent.Parent
                }
                $stagedFiles = @(Get-ChildItem -LiteralPath $stage -File -Recurse -Force)
                if ($stagedFiles | Where-Object { $_.Extension -in '.sav', '.bak' }) { throw 'Save files are forbidden in an update.' }
                New-Item -ItemType Directory -Path $previous | Out-Null
                foreach ($file in Get-ChildItem -LiteralPath $install -File -Recurse -Force) {
                    if ($file.Extension -in '.sav', '.bak') { continue }
                    $relative = $file.FullName.Substring($install.Length).TrimStart('\')
                    $target = Join-Path $previous $relative
                    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
                    Copy-Item -LiteralPath $file.FullName -Destination $target
                }
                'Backup completed.' | Out-File -LiteralPath $log -Append
                $backedUp = $true
                foreach ($file in $stagedFiles) {
                    $relative = $file.FullName.Substring($stage.Length).TrimStart('\')
                    $target = Join-Path $install $relative
                    if (-not [IO.Path]::GetFullPath($target).StartsWith($install + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid install target.' }
                    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
                    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
                }
                'Update installed.' | Out-File -LiteralPath $log -Append
                Start-Process -FilePath (Join-Path $install 'ctrnf-save-gui.exe') -WorkingDirectory $install
            } catch {
                $_ | Out-File -LiteralPath $log -Append
                if ($backedUp) {
                    # A partial copy must not leave a mixed installation: restore the backup and relaunch it.
                    # Restore file by file so one locked file does not stop the others.
                    $failed = 0
                    foreach ($file in Get-ChildItem -LiteralPath $previous -File -Recurse -Force) {
                        try {
                            $target = Join-Path $install $file.FullName.Substring($previous.Length).TrimStart('\')
                            if ((Test-Path -LiteralPath $target -PathType Leaf) -and (Get-FileHash -LiteralPath $target).Hash -eq (Get-FileHash -LiteralPath $file.FullName).Hash) { continue }
                            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($target)) -Force | Out-Null
                            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
                        } catch { $failed++; $_ | Out-File -LiteralPath $log -Append }
                    }
                    if ($failed -eq 0) {
                        'Previous version restored.' | Out-File -LiteralPath $log -Append
                        Start-Process -FilePath (Join-Path $install 'ctrnf-save-gui.exe') -WorkingDirectory $install
                    } else { "Restore incomplete ($failed files); copy $previous over $install manually." | Out-File -LiteralPath $log -Append }
                }
                exit 1
            }
            """;
    }
    public static void LaunchInstaller(string script)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
        foreach (string argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start updater.");
    }
}
