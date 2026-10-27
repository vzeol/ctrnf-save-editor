using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CtrNfSave;

public sealed record SkinEntry(string Driver, string DriverEn, string DriverFr, int Index, string Handle,
    string NameEn, string NameFr, string? Condition, int? ChallengeBit, int? Wins, int[] MarketplaceIds)
{
    public string Key => $"global.skin.{Driver}.{Index}";
}

public static class SkinUnlocks
{
    public static IReadOnlyList<SkinEntry> Skins { get; } = Load();
    private static IReadOnlyList<SkinEntry> Load()
    {
        using var stream = typeof(SkinUnlocks).Assembly.GetManifestResourceStream("CtrNfSave.Core.Data.skins.json")!;
        return Array.AsReadOnly(JsonSerializer.Deserialize<SkinEntry[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);
    }
    public static string WinsKey(string driver) => "global.driver_wins." + driver;
    private static (GraphObject Table, GraphMemory Values, GraphMemory Keys) Table(Graph graph)
    {
        var root = graph.Objects.Single(o => o.ClassName == "COctaneGlobalSaveData");
        var table = graph.Objects.Single(o => o.Id == (int)root.Words[18]);
        if (table.ClassName != "igStringIntHashTable" || table.Words.Length != 5 ||
            !graph.Memories.TryGetValue((int)table.Words[0], out var vals) ||
            !graph.Memories.TryGetValue((int)table.Words[1], out var keys) ||
            vals.Meta != "igIntMetaField" || keys.Meta != "igStringMetaField" || vals.Stride != 4 || keys.Stride != 4 ||
            vals.Count != keys.Count || keys.Count == 0 || graph.Strings[0] != "")
            throw new SaveFormatException("Unsupported driver-win table.");
        return (table, vals, keys);
    }
    private static Dictionary<string, (int Bucket, int Count)> Counters(Graph graph, byte[] bytes)
    {
        var (_, vals, keys) = Table(graph); var result = new Dictionary<string, (int, int)>();
        for (int i = 0; i < keys.Count; i++)
        {
            uint index = Binary.U32(bytes, keys.Offset + i * 4);
            if (index == 0) continue;
            if (index >= graph.Strings.Count) throw new SaveFormatException("Invalid driver-win string index.");
            if (!result.TryAdd(graph.Strings[(int)index], (i, unchecked((int)Binary.U32(bytes, vals.Offset + i * 4)))))
                throw new SaveFormatException("Duplicate driver-win key.");
        }
        return result;
    }
    internal static void Interpret(Graph graph, byte[] bytes)
    {
        if (!graph.Objects.Any(o => o.ClassName == "COctaneGlobalSaveData")) return;
        Dictionary<string, (int Bucket, int Count)> counters;
        try { counters = Counters(graph, bytes); }
        catch (SaveFormatException) { return; }
        var semantics = graph.Fields.ToDictionary(f => f.Key, f => f.Value);
        foreach (string driver in Skins.Where(s => s.Wins is not null).Select(s => s.Driver).Distinct())
            graph.Fields.Add(new(WinsKey(driver), "driver-wins", 0, 0, counters.GetValueOrDefault(driver).Count,
                "Driver race-win count. Shared by all skins with a win threshold; editing does not alter race records."));
        foreach (var skin in Skins)
        {
            bool unlocked = skin.Condition is null ? true :
                skin.ChallengeBit is int bit ? semantics.GetValueOrDefault(GlobalProfile.CharacterKey(bit)) is true :
                skin.Wins is int wins ? counters.GetValueOrDefault(skin.Driver).Count >= wins :
                skin.Condition == "COctaneBeatOxideTimesUnlockable" ? semantics.GetValueOrDefault("global.bool_7") is true :
                skin.MarketplaceIds.Any(id => semantics.GetValueOrDefault(GlobalUnlocks.ItemKey(id)) is true);
            graph.Fields.Add(new(skin.Key, "skin-unlock", 0, 0, unlocked,
                skin.NameEn + "; " + (skin.Wins is int n ? $"requires {n} driver wins; related skins share this counter" : skin.Condition ?? "default skin")));
        }
    }
    public static Save Set(Save save, string key, string input)
    {
        GlobalProfile.Require(save);
        var skin = Skins.SingleOrDefault(s => s.Key == key) ?? throw new SaveFormatException("Unknown skin.");
        bool enable = GlobalUnlocks.Boolean(input);
        if (skin.Condition is null)
        {
            if (!enable) throw new SaveFormatException("A default skin cannot be locked independently.");
            return save;
        }
        if (skin.ChallengeBit is int bit) return save.Set(GlobalProfile.CharacterKey(bit), input);
        if (skin.Condition == "COctaneBeatOxideTimesUnlockable")
        {
            var bytes = save.Serialize(); var root = save.Graph.Objects.Single(o => o.ClassName == "COctaneGlobalSaveData");
            bytes[root.WordOffset(7)] = enable ? (byte)1 : (byte)0; return Save.Parse(bytes);
        }
        if (skin.MarketplaceIds.Length == 1) return save.Set(GlobalUnlocks.ItemKey(skin.MarketplaceIds[0]), input);
        if (skin.Wins is int wins)
        {
            int current = Counters(save.Graph, save.Serialize()).GetValueOrDefault(skin.Driver).Count;
            int next = enable ? Math.Max(current, wins) : Math.Min(current, wins - 1);
            return SetWins(save, skin.Driver, next);
        }
        throw new SaveFormatException("Unsupported skin condition.");
    }
    public static Save SetWins(Save save, string driver, int wins)
    {
        GlobalProfile.Require(save);
        if (wins < 0 || !Skins.Any(s => s.Driver == driver && s.Wins is not null)) throw new SaveFormatException("Invalid driver-win count or identity.");
        var counters = Counters(save.Graph, save.Serialize());
        if (counters.TryGetValue(driver, out var current))
        {
            if (current.Count == wins) return save;
            var bytes = save.Serialize(); var (_, values, _) = Table(save.Graph);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(values.Offset + current.Bucket * 4, 4), wins);
            return Save.Parse(bytes);
        }
        if (wins == 0) return save;
        var (_, _, originalKeys) = Table(save.Graph);
        if (counters.Count + 1 > originalKeys.Count / 2) throw new SaveFormatException("Driver-win hash table lacks verified spare capacity; insertion refused.");
        Save prepared = save;
        int stringIndex = save.Graph.Strings.IndexOf(driver);
        if (stringIndex < 0)
        {
            // Append a string without renumbering any existing string/object/memory reference.
            byte[] old = save.Serialize(), text = Encoding.UTF8.GetBytes(driver + "\0");
            int growth = 4 + text.Length;
            var strings = save.Graph.Sections.Single(s => s.Name == "graph_strings");
            var memory = save.Graph.Sections.Single(s => s.Name == "graph_memory");
            int at = strings.Offset + strings.Size, end = memory.Offset + memory.Size;
            if (end + growth > old.Length || old.AsSpan(end).IndexOfAnyExcept((byte)0) >= 0)
                throw new SaveFormatException("Insufficient verified padding for driver name.");
            byte[] output = new byte[old.Length]; old.AsSpan(0, at).CopyTo(output);
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(at, 4), text.Length); text.CopyTo(output, at + 4);
            old.AsSpan(at, end - at).CopyTo(output.AsSpan(at + growth));
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(strings.Offset, 4), strings.Size + growth);
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(strings.Offset + 4, 4), strings.Count + 1);
            prepared = Save.Parse(output); GlobalProfile.Require(prepared); stringIndex = prepared.Graph.Strings.IndexOf(driver);
        }
        var (table, vals, keys) = Table(prepared.Graph); byte[] result = prepared.Serialize();
        // Native predicate 0xFA3710 uses case-sensitive FNV-1a; lookup 0x2339A0 uses linear probing.
        uint hash = 2166136261;
        foreach (byte c in Encoding.UTF8.GetBytes(driver)) hash = unchecked((hash ^ c) * 16777619);
        int bucket = (int)(hash % keys.Count), probes = 0;
        while (Binary.U32(result, keys.Offset + bucket * 4) != 0)
        { bucket = (bucket + 1) % keys.Count; if (++probes == keys.Count) throw new SaveFormatException("Driver-win table is full."); }
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(keys.Offset + bucket * 4, 4), stringIndex);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(vals.Offset + bucket * 4, 4), wins);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(table.WordOffset(2), 4), counters.Count + 1);
        var parsed = Save.Parse(result); GlobalProfile.Require(parsed);
        if (Counters(parsed.Graph, parsed.Serialize()).GetValueOrDefault(driver).Count != wins) throw new SaveFormatException("Driver-win insertion verification failed.");
        return parsed;
    }
}
