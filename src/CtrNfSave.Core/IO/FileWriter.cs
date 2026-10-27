using System.Globalization;

namespace CtrNfSave;

public static class FileWriter
{
    private static string PhysicalPath(string path)
    {
        string full = Path.GetFullPath(path), root = Path.GetPathRoot(full)!;
        string current = root;
        foreach (string part in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (part.Length == 0) continue;
            current = Path.Combine(current, part);
            FileSystemInfo item = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                current = item.ResolveLinkTarget(true)?.FullName ?? throw new IOException("Cannot resolve symbolic link: " + current);
        }
        return Path.GetFullPath(current);
    }
    public static void ProtectSamples(string path, string? explicitSamples = null)
    {
        string full = PhysicalPath(path);
        List<string> roots = [Path.Combine(Environment.CurrentDirectory, "samples"), Path.Combine(Environment.CurrentDirectory, "target")];
        // Also protect repository samples when run from dist/ or another working directory.
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && d is not null; i++, d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "CtrNfSave.slnx"))) { roots.Add(Path.Combine(d.FullName, "samples")); roots.Add(Path.Combine(d.FullName, "target")); }
        if (explicitSamples is not null) roots.Add(explicitSamples);
        foreach (string candidate in roots.Where(Directory.Exists))
        {
            string sampleRoot = PhysicalPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if ((full + Path.DirectorySeparatorChar).StartsWith(sampleRoot, StringComparison.OrdinalIgnoreCase))
                throw new SaveFormatException("The samples/ and target/ input directories are read-only. Choose another output directory.");
        }
    }
    private static void WriteFresh(string path, byte[] bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(flushToDisk: true);
    }
    public static void New(string path, byte[] bytes)
    {
        ProtectSamples(path);
        // CreateNew also prevents hard-link aliases from overwriting the input.
        WriteFresh(path, bytes);
    }
    /// <summary>A new global export keeps the opened source bytes beside the output first.</summary>
    public static void NewWithBackup(string path, byte[] original, byte[] edited)
    {
        ProtectSamples(path);
        if (File.Exists(path)) throw new IOException("Output already exists: " + path);
        string backup = path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + ".bak";
        ProtectSamples(backup); WriteFresh(backup, original); New(path, edited);
    }
    public static void InPlace(string path, byte[] expected, byte[] edited, bool timestamped = false)
    {
        string backup = timestamped ? path + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss-fffffff", CultureInfo.InvariantCulture) + ".bak" : path + ".bak";
        ProtectSamples(path); ProtectSamples(backup);
        string full = PhysicalPath(path);
        if (File.Exists(backup)) throw new IOException("Backup already exists; refusing to replace it: " + backup);
        if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(expected)) throw new IOException("Input changed while editing; no write performed.");
        string temporary = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteFresh(temporary, edited);
            WriteFresh(backup, expected);
            if (!File.ReadAllBytes(full).AsSpan().SequenceEqual(expected)) throw new IOException("Input changed while creating backup; no replacement performed.");
            File.Move(temporary, full, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
