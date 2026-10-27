using System.IO.Compression;
using System.Text.RegularExpressions;

namespace CtrNfSave.Updates;

public static class UpdatePackage
{
    public static string ParseChecksum(string text, string name)
    {
        var match = Regex.Match(text.Trim(), @"^([0-9a-fA-F]{64})  ([^\r\n]+)$", RegexOptions.CultureInvariant);
        if (!match.Success || match.Groups[2].Value != name) throw new InvalidDataException("Invalid SHA-256 sidecar.");
        return match.Groups[1].Value;
    }
    public static string EntryPath(string root, string name)
    {
        string normalized = name.Replace('\\', '/');
        if (string.IsNullOrEmpty(normalized) || normalized.StartsWith('/') || normalized.Any(c => c < 32 || "<>:\"|?*".Contains(c)) || normalized.Split('/').Any(p => p is ".." or "." || p.EndsWith(' ') || p.EndsWith('.') || Regex.IsMatch(p, @"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)))
            throw new InvalidDataException("Unsafe archive entry.");
        string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string target = Path.GetFullPath(Path.Combine(fullRoot, normalized));
        if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe archive entry.");
        return target;
    }
    // The caller supplies a new staging directory, never the installation directory.
    public static void Extract(string archive, string staging, CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(staging)) throw new IOException("Staging directory already exists.");
        using var zip = ZipFile.OpenRead(archive);
        var entries = zip.Entries.Select(e => (Entry: e, Path: EntryPath(staging, e.FullName))).ToArray();
        if (!entries.Any(e => e.Entry.FullName == "ctrnf-save-gui.exe" && e.Entry.Length > 0)) throw new InvalidDataException("Missing root GUI executable.");
        if (entries.Any(e => ((e.Entry.ExternalAttributes >> 16) & 0xF000) == 0xA000 || (e.Entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)) throw new InvalidDataException("Archive links are not allowed.");
        if (entries.Length > 20000 || entries.Sum(e => (decimal)e.Entry.Length) > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Archive exceeds extraction limits.");
        if (entries.Any(e => Path.GetExtension(e.Path).Equals(".sav", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(e.Path).Equals(".bak", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Save files are forbidden in an update.");
        if (entries.Select(e => e.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length) throw new InvalidDataException("Duplicate archive entry.");
        Directory.CreateDirectory(staging);
        foreach (var (entry, path) in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) Directory.CreateDirectory(path);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var source = entry.Open();
                using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                byte[] buffer = new byte[81920];
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int count = source.Read(buffer); if (count == 0) break;
                    output.Write(buffer, 0, count);
                }
            }
        }
    }
}
