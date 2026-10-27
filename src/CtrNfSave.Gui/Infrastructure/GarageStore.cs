using CtrNfSave;

namespace CtrNfSave.Gui;

internal sealed record StoredKartPreset(string Path, KartPreset Preset)
{
    public override string ToString() => Preset.Name;
}

internal static class GarageStore
{
    private static string Folder => Path.Combine(UpdateService.DataDirectory, "garage");
    public static IReadOnlyList<StoredKartPreset> Read(out int invalid)
    {
        invalid = 0; var result = new List<StoredKartPreset>();
        if (!Directory.Exists(Folder)) return result;
        foreach (string file in Directory.EnumerateFiles(Folder, "*.json"))
        {
            try { result.Add(new(file, KartPreset.FromJson(File.ReadAllText(file)))); }
            catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException) { invalid++; }
        }
        return result.OrderBy(p => p.Preset.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static void Write(KartPreset preset, string? path = null)
    {
        string json = preset.ToJson(); Directory.CreateDirectory(Folder);
        path ??= Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".json");
        string temporary = Path.Combine(Folder, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream)) writer.Write(json);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static void Delete(StoredKartPreset preset) => File.Delete(preset.Path);
}
