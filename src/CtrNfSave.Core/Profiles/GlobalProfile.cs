using System.Numerics;
namespace CtrNfSave;

public sealed record GlobalTimeTrial(string Track, string Category, int Rank, float Seconds, string Driver, string Name);

/// <summary>Legacy readouts and fixed-size profile edits; see TimeAttack for the named editing API.</summary>
public static class GlobalProfile
{
    public const string CharacterWarning = "Legacy challenge-mask key; this is not a complete character roster. Karts and skins use separate conditions.";
    public static string CharacterKey(int bit) => bit == 69 ? "global.character.king_chicken" : $"global.character.bit_{bit}";
    public static string CharacterName(int bit, bool french = false)
    {
        var driver = GlobalUnlocks.Drivers.SingleOrDefault(d => d.ChallengeBit == bit);
        return driver is not null ? (french ? driver.NameFr : driver.NameEn) : (french ? $"défi inconnu #{bit}" : $"unknown challenge #{bit}");
    }
    public static string CharacterConfidence(int bit) => bit == 69 ? "anchored: controlled King Chicken capture" : GlobalUnlocks.Drivers.Any(d => d.ChallengeBit == bit) ? "native authored challenge condition" : "unknown challenge identity";
    // Observed all-Pit-Stop envelope. Preserve bit 5 and all bits above 73.
    public static Save CheckAllCharacters(Save save)
    {
        Require(save);
        var fields = save.Graph.Fields.Where(f => f.Type == "character-bit").ToArray();
        if (fields.Length != 512) throw new SaveFormatException("Unsupported character roster layout; editing refused.");
        byte[] bytes = save.Serialize();
        foreach (var f in fields)
        {
            int bit = CharacterBit(f);
            if (bit <= 73 && bit != 5) bytes[f.Offset] |= (byte)(1 << (bit % 8));
        }
        return Save.Parse(bytes);
    }
    private static int CharacterBit(GraphField f) => f.Key == "global.character.king_chicken" ? 69 : int.Parse(f.Key.Split('_')[^1], System.Globalization.CultureInfo.InvariantCulture);
    internal static void SetCharacter(byte[] bytes, GraphField f, string input)
    {
        bool value = input.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new SaveFormatException("Expected true/false or 1/0.")
        };
        int mask = 1 << (CharacterBit(f) % 8);
        bytes[f.Offset] = (byte)((bytes[f.Offset] & ~mask) | (value ? mask : 0));
    }
    public const string EggNote = "Golden Egg bits; 1-3 confirmed by the user's profile (N. Sanity Beach owned before a session that added Gem Stone Valley + The Lost Ruins = bits 2,3), 4 and 5 confirmed by live captures (Glacier Park 0x0E -> 0x1E, Citadel City 0x1E -> 0x3E)";
    /// <summary>Bit order follows the in-game/IGN egg numbering; all five confirmed in game.</summary>
    public static readonly string[] EggHubs = ["", "N. Sanity Beach", "Gem Stone Valley", "The Lost Ruins", "Glacier Park", "Citadel City"];
    public static void Require(Save save)
    {
        if (save.Kind != "global" || !save.Graph.Decoded || save.Graph.Warnings.Count != 0 ||
            save.Graph.Objects.Count(o => o.ClassName == "COctaneGlobalSaveData") != 1 ||
            !save.Graph.Fields.Any(f => f.Key == "global.wumpa_coins"))
            throw new SaveFormatException("Unsupported or incomplete global graph; editing refused.");
    }
    internal static void Interpret(Graph g, byte[] bytes)
    {
        if (!g.Objects.Any(o => o.ClassName == "COctaneGlobalSaveData")) return;
        var root = g.Objects.Single(o => o.ClassName == "COctaneGlobalSaveData");
        var roster = g.Objects.SingleOrDefault(o => root.Words.Length > 16 && o.Id == (int)root.Words[16] && o.ClassName == "igBitMask");
        if (roster is not null && roster.Words.Length == 4 && roster.Words[0] == 16 && roster.Words[1] == 16 && roster.Words[3] == 512 &&
            g.Memories.TryGetValue((int)roster.Words[2], out var rm) && rm.Count == 16 && rm.Stride == 4 && rm.Meta == "igUnsignedIntMetaField")
        {
            int count = 0;
            for (int bit = 0; bit < 512; bit++)
            {
                int at = rm.Offset + bit / 8;
                bool unlocked = (bytes[at] & (1 << (bit % 8))) != 0;
                if (unlocked) count++;
                g.Fields.Add(new(CharacterKey(bit), "character-bit", at, 1, unlocked,
                    $"{CharacterName(bit)}; bit {bit}; {CharacterConfidence(bit)}; {CharacterWarning}"));
            }
            g.Fields.Add(new("global.character.count", "readonly", rm.Offset, 0, count, "Set roster bits, including unidentified entries; not a count of verified playable characters"));
        }
        var blobs = g.Objects.Where(o => o.ClassName == "igCharList").ToArray();
        // The older patch blob has only 60 bytes; named-header version is 31 in every sample.
        if (blobs.Length == 1 && blobs[0].Words.Length == 3 && blobs[0].Words[0] == 3352 &&
            g.Memories.TryGetValue((int)blobs[0].Words[2], out var m) && m.Meta == "igCharMetaField" && m.Stride == 1 && m.Count == 3352)
        {
            int at = m.Offset + 3300;
            for (int bit = 1; bit <= 5; bit++)
                g.Fields.Add(new($"global.golden_egg.bit_{bit}", "egg-bit", at, 1,
                    (bytes[at] & (1 << bit)) != 0, $"{EggHubs[bit]} egg (confirmed)"));
            g.Fields.Add(new("global.unknown.patch_3300.bit_6", "readonly", at, 1, (bytes[at] & 0x40) != 0,
                "Sets alongside King Chicken roster bit in the capture (3E -> 7E); purpose unknown, preserved"));
            g.Fields.Add(new("global.golden_egg.count", "readonly", at, 1,
                BitOperations.PopCount((uint)(bytes[at] & 0x3E)), EggNote));
            g.Fields.Add(new("global.golden_egg.bitmask", "readonly", at, 1, bytes[at] & 0x3E, EggNote));
        }
        // A name proves usage/counter presence, never ownership.
        foreach (string s in g.Strings.Where(s => s.StartsWith("Driver", StringComparison.Ordinal) || s.StartsWith("Kart", StringComparison.Ordinal)))
            g.Fields.Add(new("global.observed." + s, "readonly", 0, 0, true,
                "Name present (usage/customization/counter); ownership unknown"));
        foreach (var row in TimeTrials(g, bytes))
        {
            string key = $"global.time_trial.{row.Track}.{row.Category}.{row.Rank}";
            g.Fields.Add(new(key, "readonly", 0, 0, row.Seconds,
                $"seconds; {row.Driver}; {row.Name}; legacy read-only alias; edit corresponding global.time_attack key"));
            g.Fields.Add(new(key + ".driver", "readonly", 0, 0, row.Driver));
            g.Fields.Add(new(key + ".name", "readonly", 0, 0, row.Name));
        }
    }
    public static IReadOnlyList<GlobalTimeTrial> TimeTrials(Save save)
    {
        Require(save); return TimeTrials(save.Graph, save.Serialize());
    }
    private static List<GlobalTimeTrial> TimeTrials(Graph g, byte[] bytes)
    {
        var objects = g.Objects.ToDictionary(o => o.Id);
        var result = new List<GlobalTimeTrial>();
        foreach (var table in g.Objects.Where(o => o.ClassName == "COctaneMapTimeTrialHashTable"))
        {
            if (table.Words.Length < 2 || !g.Memories.TryGetValue((int)table.Words[0], out var vals) ||
                !g.Memories.TryGetValue((int)table.Words[1], out var keys)) continue;
            for (int i = 0; i < Math.Min(vals.Count, keys.Count); i++)
            {
                uint k = Binary.U32(bytes, keys.Offset + i * 4);
                if (k == 0 || k >= g.Strings.Count) continue;
                if (!objects.TryGetValue((int)Binary.U32(bytes, vals.Offset + i * 4), out var map) ||
                    map.ClassName != "COctaneMapTimeTrialData" || map.Words.Length != 3)
                    throw new SaveFormatException("Invalid time trial map entry.");
                string track = g.Strings[(int)k].Split('/')[^1];
                for (int category = 0; category < 3; category++)
                {
                    if (!objects.TryGetValue((int)map.Words[category], out var o))
                        throw new SaveFormatException("Missing time trial record/list.");
                    if (category == 2) Add(o, "best_lap_candidate", 1);
                    else
                    {
                        if (o.ClassName != "COctaneTimeTrialList" || o.Words.Length != 3)
                            throw new SaveFormatException("Invalid time trial list.");
                        if (o.Words[0] == 0) continue;
                        if (!g.Memories.TryGetValue((int)o.Words[2], out var memory) || o.Words[0] > memory.Count)
                            throw new SaveFormatException("Invalid time trial list memory.");
                        for (int n = 0; n < o.Words[0]; n++)
                        {
                            if (!objects.TryGetValue((int)Binary.U32(bytes, memory.Offset + n * 4), out var record))
                                throw new SaveFormatException("Missing time trial record.");
                            Add(record, category == 0 ? "race" : "alternate", n + 1);
                        }
                    }
                }
                void Add(GraphObject record, string category, int rank)
                {
                    if (record.ClassName != "COctaneTimeTrialData" || record.Words.Length != 3 ||
                        record.Words[1] >= g.Strings.Count || record.Words[2] >= g.Strings.Count)
                        throw new SaveFormatException("Invalid time trial data.");
                    float seconds = BitConverter.UInt32BitsToSingle(record.Words[0]);
                    if (seconds == float.MaxValue) return;
                    if (!float.IsFinite(seconds) || seconds < 0) throw new SaveFormatException("Invalid time trial seconds.");
                    result.Add(new(track, category, rank, seconds, g.Strings[(int)record.Words[1]], g.Strings[(int)record.Words[2]]));
                }
            }
        }
        return result.OrderBy(r => r.Track, StringComparer.Ordinal).ThenBy(r => r.Category).ThenBy(r => r.Rank).ToList();
    }
    internal static void SetEgg(byte[] bytes, GraphField f, string input)
    {
        bool value = input.ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => throw new SaveFormatException("Expected true/false or 1/0.")
        };
        int mask = 1 << int.Parse(f.Key.Split('_')[^1], System.Globalization.CultureInfo.InvariantCulture);
        bytes[f.Offset] = (byte)((bytes[f.Offset] & ~mask) | (value ? mask : 0));
    }
}
