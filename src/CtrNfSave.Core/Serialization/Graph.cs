using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace CtrNfSave;

public sealed record GraphClass(string Name, int Parent, (ushort Type, ushort Index, ushort Size)[] Fields);
public sealed record GraphObject(int Id, int Type, string ClassName, int Offset, uint[] Words)
{
    /// <summary>File offset of serialized word <paramref name="i"/> (words start after the 8-byte type/size prefix).</summary>
    public int WordOffset(int i) => Offset + 8 + i * 4;
}
public sealed record GraphMemory(int Id, string Meta, int Offset, int Count, int Stride);

/// <summary>A decoded, fixed-size value that can be edited in place without moving any other byte.</summary>
public sealed record GraphField(string Key, string Type, int Offset, int Size, object Value, string? Note = null);

/// <summary>
/// Alchemy-style serialized object graph that follows the named header.
/// The structural walk mirrors tools/graph_decode.py; see FORMAT.md.
/// </summary>
public sealed class Graph
{
    public List<Section> Sections { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Strings { get; } = [];
    public List<string> MetaFields { get; } = [];
    public List<GraphClass> Classes { get; } = [];
    public Dictionary<uint, string> Names { get; } = [];
    public List<GraphObject> Objects { get; } = [];
    public Dictionary<int, GraphMemory> Memories { get; } = [];
    public List<GraphField> Fields { get; } = [];
    public bool Decoded { get; private set; }
    public List<TrackState> Tracks { get; } = [];
    private byte[] bytes = [];
    internal byte[] SourceBytes => bytes;

    // COctaneSaveSlotData: native metadata confirms counter order, two Oxide booleans,
    // difficulty and collectible colours. Legacy mask/cursor names remain compatible.
    private static readonly string[] SlotDataNames =
    [
        "trophy_count", "boss_key_count",
        "gem_red", "gem_blue", "gem_purple", "gem_green", "gem_yellow",
        "relic_sapphire", "relic_gold", "relic_platinum",
        "token_red", "token_blue", "token_purple", "token_green", "token_yellow",
        "mask_15", "mask_16", "unknown_17",
        "bool_18", "bool_19", "enum_20", "enum_21",
    ];

    public static Graph Parse(byte[] b, int start, uint[] h)
    {
        var g = new Graph { bytes = b };
        try { g.Walk(b, start, h); g.Interpret(); g.Decoded = true; }
        catch (Exception e) when (e is SaveFormatException or IndexOutOfRangeException or ArgumentOutOfRangeException or KeyNotFoundException)
        {
            g.Warnings.Add("Object graph not decoded: " + e.Message);
        }
        return g;
    }

    private static uint U(byte[] b, int o) => Binary.U32(b, o);
    private static void Expect(bool ok, string what) { if (!ok) throw new SaveFormatException("Graph layout mismatch: " + what); }
    private static string Z(byte[] b, int o, int len)
    {
        var span = Binary.Slice(b, o, len);
        int n = span.IndexOf((byte)0);
        return Encoding.UTF8.GetString(n < 0 ? span : span[..n]);
    }

    private void Walk(byte[] b, int p, uint[] h)
    {
        // Strings: u32 size, u32 count, then (u32 len, bytes) records.
        int s0 = p, size = (int)U(b, p), n = (int)U(b, p + 4); p += 8;
        for (int i = 0; i < n; i++) { int z = (int)U(b, p); p += 4; Strings.Add(Z(b, p, z)); p += z; }
        Expect(p == s0 + size, "string table size");
        Sections.Add(new("graph_strings", s0, size, n));

        // Meta-field type names: h[9] 12-byte records (first u32 = name length), then names; total h[8].
        int m0 = p, names = p + (int)h[9] * 12;
        for (int i = 0; i < h[9]; i++) { int z = (int)U(b, p + i * 12); MetaFields.Add(Z(b, names, z)); names += z; }
        Expect(names == m0 + h[8], "meta table size"); p = names;
        Sections.Add(new("graph_meta_fields", m0, (int)h[8], (int)h[9]));

        // Classes: h[3] 24-byte records, then per class its name and field triples; total h[2].
        int c0 = p, cp = p + (int)h[3] * 24;
        for (int i = 0; i < h[3]; i++)
        {
            int r = p + i * 24;
            int nameLen = (int)U(b, r), fieldCount = (int)U(b, r + 12), parent = unchecked((int)U(b, r + 16));
            string name = Z(b, cp, nameLen); cp += nameLen;
            var fields = new (ushort, ushort, ushort)[fieldCount];
            for (int j = 0; j < fieldCount; j++)
                fields[j] = (Binary.U16(b, cp + j * 6), Binary.U16(b, cp + j * 6 + 2), Binary.U16(b, cp + j * 6 + 4));
            cp += fieldCount * 6;
            Classes.Add(new(name, parent, fields));
        }
        Expect(cp == c0 + h[2], "class table size"); p = cp;
        Sections.Add(new("graph_classes", c0, (int)h[2], (int)h[3]));

        // Name hashes: u32 size, u32 version, u32 count, hashes, then NUL-terminated strings.
        int n0 = p, nsize = (int)U(b, p), ncount = (int)U(b, p + 8);
        var hashes = Enumerable.Range(0, ncount).Select(i => U(b, p + 12 + i * 4)).ToArray();
        p += 12 + ncount * 4;
        foreach (uint hash in hashes)
        {
            int e = Array.IndexOf(b, (byte)0, p);
            Expect(e >= 0, "unterminated name");
            Names[hash] = Encoding.UTF8.GetString(b, p, e - p); p = e + 1;
        }
        Expect(p == n0 + nsize, "name table size");
        Sections.Add(new("graph_names", n0, nsize, ncount));

        int pool0 = p; p += (int)U(b, p);
        Sections.Add(new("graph_pools", pool0, p - pool0, 0));
        int pi0 = p; p += (int)U(b, p);
        Sections.Add(new("graph_pool_indexes", pi0, p - pi0, 0));

        // Directory entries: h[1] records of (u32 class, u32 size incl. prefix, words); total h[0].
        int d0 = p;
        var dirs = new List<(int Type, uint[] Words)>();
        for (int i = 0; i < h[1]; i++)
        {
            int t = (int)U(b, p), z = (int)U(b, p + 4);
            Expect(z >= 8 && t < Classes.Count, "directory entry");
            dirs.Add((t, ReadWords(b, p + 8, (z - 8) / 4))); p += z;
        }
        Expect(p == d0 + h[0], "directory size");
        Sections.Add(new("graph_directories", d0, (int)h[0], (int)h[1]));

        // Allocation order: u32 size, u32 count, u16 directory index per allocation.
        int a0 = p, asize = (int)U(b, p), acount = (int)U(b, p + 4);
        var alloc = Enumerable.Range(0, acount).Select(i => (int)Binary.U16(b, p + 8 + i * 2)).ToArray();
        p = a0 + asize + 4;
        Sections.Add(new("graph_allocations", a0, asize + 4, acount));

        // Objects: h[5] records like directories; total h[4].
        int o0 = p;
        var raw = new List<(int Type, int Offset, uint[] Words)>();
        for (int i = 0; i < h[5]; i++)
        {
            int t = (int)U(b, p), z = (int)U(b, p + 4);
            Expect(z >= 8 && t < Classes.Count, "object record");
            raw.Add((t, p, ReadWords(b, p + 8, (z - 8) / 4))); p += z;
        }
        Expect(p == o0 + h[4], "object section size");
        Sections.Add(new("graph_objects", o0, (int)h[4], (int)h[5]));

        // Memory blocks follow in allocation order; objects take their id from the same order.
        int mem0 = p, oi = 0;
        for (int ai = 0; ai < alloc.Length; ai++)
        {
            var d = dirs[alloc[ai]];
            if (Classes[d.Type].Name == "igMemoryDirEntry")
            {
                int count = (int)d.Words[1], mt = (int)d.Words[2], stride = (int)d.Words[4];
                int ser = mt is 0 or 6 ? 4 : stride; // object refs and strings are stored as 4-byte indexes
                Memories[ai] = new(ai, MetaFields[mt], p, count, ser);
                p += (count * ser + 3) / 4 * 4;
            }
            else
            {
                var o = raw[oi++];
                Expect(o.Type == d.Words[1], $"object {oi} type");
                Objects.Add(new(ai, o.Type, Classes[o.Type].Name, o.Offset, o.Words));
            }
        }
        Expect(oi == raw.Count, "object count");
        Expect(Memories.Count == h[7], "memory block count");
        Expect(mem0 + h[6] - p is >= 0 and < 4, "memory section size");
        Sections.Add(new("graph_memory", mem0, (int)h[6], (int)h[7]));
    }

    private static uint[] ReadWords(byte[] b, int o, int n)
    {
        var w = new uint[n];
        for (int i = 0; i < n; i++) w[i] = U(b, o + i * 4);
        return w;
    }

    private string Name(uint hash) => Names.TryGetValue(hash, out var s) ? s : $"#{hash:X8}";
    private GraphObject? First(string cls) => Objects.FirstOrDefault(o => o.ClassName == cls);
    private void AddInt(string key, GraphObject o, int word, string? note = null) =>
        Fields.Add(new(key, "i32", o.WordOffset(word), 4, unchecked((int)o.Words[word]), note));
    private void AddHex(string key, GraphObject o, int word, string? note = null) =>
        Fields.Add(new(key, "u32", o.WordOffset(word), 4, o.Words[word], note));
    // Bools are serialized in a 4-byte slot; only the low byte is meaningful (the rest is stale memory).
    private void AddBool(string key, GraphObject o, int word, string? note = null) =>
        Fields.Add(new(key, "bool", o.WordOffset(word), 1, (o.Words[word] & 0xFF) != 0, note));

    private void Interpret()
    {
        if (First("COctaneSaveSlotData") is { } slot && slot.Words.Length >= 22)
        {
            for (int i = 0; i < 18; i++)
            {
                if (SlotDataNames[i].StartsWith("mask_")) AddHex("slot." + SlotDataNames[i], slot, i, "bitmask, meaning unknown");
                else AddInt("slot." + SlotDataNames[i], slot, i, SlotDataNames[i].StartsWith("unknown") ? "meaning unknown" : null);
            }
            AddBool("slot.bool_18", slot, 18, "meaning unknown");
            AddBool("slot.bool_19", slot, 19, "meaning unknown");
            AddBool("slot.oxide_first", slot, 18, "_hasWonOxideBossFirst: native metadata");
            AddBool("slot.oxide_second", slot, 19, "_hasWonOxideBossSecond: native metadata and controlled rematch victory");
            AddInt("slot.adventure_difficulty", slot, 21, "Native EDifficultyLevel: easy=0, medium=1, hard=2");
            AddInt("slot.enum_20", slot, 20, "meaning unknown");
            AddInt("slot.enum_21", slot, 21, "meaning unknown");
        }

        InterpretTracks();
        GlobalProfile.Interpret(this, bytes);
        GlobalUnlocks.Interpret(this, bytes);

        foreach (var (o, i) in Objects.Where(o => o.ClassName == "COctaneCustomizationSaveData").Select((o, i) => (o, i)))
        {
            if (o.Words.Length < 6) continue;
            string p = $"customization[{i}].";
            Fields.Add(new(p + "driver", "name", o.WordOffset(0), 4, Name(o.Words[0]), "read-only (name hash)"));
            Fields.Add(new(p + "kart", "name", o.WordOffset(3), 4, Name(o.Words[3]), "read-only (name hash)"));
        }

        if (First("COctaneGlobalSaveData") is { } global && global.Words.Length > 10)
        {
            AddInt("global.wumpa_coins", global, 10, "Pit Stop currency");
            for (int i = 3; i <= 7; i++) AddBool($"global.bool_{i}", global, i, "meaning unknown");
            // Fields 8, 9 and 11..18 are references; resolve the ones that point at igBitMask objects.
            foreach (int f in new[] { 8, 9, 11, 12, 13, 14, 15, 16, 17, 18 })
            {
                if (f >= global.Words.Length) continue;
                var mask = Objects.FirstOrDefault(o => o.Id == (int)global.Words[f] && o.ClassName == "igBitMask");
                if (mask is null || mask.Words.Length < 4 || !Memories.TryGetValue((int)mask.Words[2], out var mem)) continue;
                int bits = (int)mask.Words[3];
                int words = Math.Min(mem.Count, (bits + 31) / 32);
                int count = 0;
                for (int w = 0; w < words; w++)
                {
                    uint v = U(bytes, mem.Offset + w * 4);
                    string key = words == 1 ? $"global.bitmask_{f}" : $"global.bitmask_{f}[{w}]";
                    int used = Math.Min(32, bits - w * 32);
                    count += System.Numerics.BitOperations.PopCount(used == 32 ? v : v & ((1u << used) - 1));
                    Fields.Add(new(key, "u32", mem.Offset + w * 4, 4, v,
                        $"{System.Numerics.BitOperations.PopCount(used == 32 ? v : v & ((1u << used) - 1))}/{used} bits set; meaning unknown"));
                }
                Fields.Add(new($"global.unknown_mask_{f}.count", "readonly", mem.Offset, 0, count,
                    $"{count}/{bits} bits set; " + (f == 16 ? "global challenge completion mask, not a complete character roster" : "unknown meaning; read-only")));
                Fields.Add(new($"global.unknown_mask_{f}.capacity", "readonly", mem.Offset, 0, bits, "Declared bit capacity"));
            }
        }

        SkinUnlocks.Interpret(this, bytes);
        TimeAttack.Interpret(this, bytes);
        TimeTrialRewards.Interpret(this, bytes);
        CustomizationUnlocks.Interpret(this);
        foreach (var o in Objects.Where(o => o.ClassName.StartsWith("CGameVariableValueSaved") && !o.ClassName.EndsWith("List")))
        {
            if (o.Words.Length < 2) continue;
            string key = "var." + Name(o.Words[1]);
            if (Fields.Any(f => f.Key == key)) continue;
            switch (o.ClassName)
            {
                case "CGameVariableValueSavedBool": AddBool(key, o, 0); break;
                case "CGameVariableValueSavedInt": AddInt(key, o, 0); break;
                case "CGameVariableValueSavedFloat":
                    Fields.Add(new(key, "f32", o.WordOffset(0), 4, BitConverter.UInt32BitsToSingle(o.Words[0]))); break;
            }
        }

        // String→int hash tables (e.g. per-driver counters in the global save). Read-only for now.
        foreach (var o in Objects.Where(o => o.ClassName == "igStringIntHashTable"))
        {
            if (o.Words.Length < 2 || !Memories.TryGetValue((int)o.Words[0], out var vals) || !Memories.TryGetValue((int)o.Words[1], out var keys)) continue;
            for (int i = 0; i < Math.Min(vals.Count, keys.Count); i++)
            {
                int k = (int)U(bytes, keys.Offset + i * 4);
                if (k <= 0 || k >= Strings.Count) continue;
                Fields.Add(new($"table.{Strings[k]}", "i32", vals.Offset + i * 4, 4, unchecked((int)U(bytes, vals.Offset + i * 4)), "igStringIntHashTable value"));
            }
        }
    }

    private void InterpretTracks()
    {
        var objects = Objects.ToDictionary(o => o.Id);
        var list = First("CZoneInfoSaveList");
        if (list is null) return;
        Expect(list.Words.Length == 3 && Memories.TryGetValue((int)list.Words[2], out _), "zone list");
        var memory = Memories[(int)list.Words[2]];
        Expect(memory.Stride == 4 && list.Words[0] <= memory.Count, "zone list memory");
        var zones = Enumerable.Range(0, (int)list.Words[0]).Select(i => objects[(int)U(bytes, memory.Offset + i * 4)]);
        foreach (var zone in zones)
        {
            Expect(zone.ClassName == "CZoneInfoSave" && zone.Words.Length >= 2 && zone.Words[0] < Strings.Count, "zone path");
            string path = Strings[(int)zone.Words[0]];
            string shortName = path.Split('/')[^1];
            var definition = AdventureTracks.All.FirstOrDefault(t => t.Id == shortName);
            if (definition is null) continue;
            Expect(objects.TryGetValue((int)zone.Words[1], out var session) &&
                session.ClassName == "CZoneInfoUserSession" && session.Words.Length == 15, "zone session");
            Expect(Tracks.All(t => t.Definition.Id != shortName), "duplicate Adventure track");
            uint flags = session!.Words[13];
            var state = new TrackState(definition, session.WordOffset(13), flags);
            Tracks.Add(state);
            string prefix = $"track.{shortName}.";
            if (definition.BossName is not null) Fields.Add(new(prefix + "boss", "bool", state.Offset, 4, state.Boss, "bit 2 (boss: sample correlation)"));
            if (definition.HasTrophy) Fields.Add(new(prefix + "trophy", "bool", state.Offset, 4, state.Trophy, "bit 1"));
            if (definition.HasRelic) Fields.Add(new(prefix + "relic", "relic", state.Offset, 4, state.Relic, "bits 3,4,5"));
            if (definition.TokenBit is int bit) Fields.Add(new(prefix + "token", "bool", state.Offset, 4, state.Token, $"bit {bit} ({definition.TokenColour})"));
            Fields.Add(new(prefix + "flags", "u32", state.Offset, 4, state.OtherFlags, "other flags only; known award bits excluded"));
        }
    }

    private void SetTrack(byte[] target, string key, string input)
    {
        var field = Fields.FirstOrDefault(f => f.Key == key) ?? throw new SaveFormatException("Unknown or unavailable track field: " + key);
        var track = Tracks.Single(t => t.Offset == field.Offset);
        uint old = U(target, track.Offset), mask, value;
        switch (key.Split('.')[^1])
        {
            case "boss": mask = 4; value = ParseBool(input) ? mask : 0; break;
            case "trophy": mask = 2; value = ParseBool(input) ? mask : 0; break;
            case "token": mask = 1u << track.Definition.TokenBit!.Value; value = ParseBool(input) ? mask : 0; break;
            case "relic":
                mask = AdventureTracks.RelicMask;
                value = input.ToLowerInvariant() switch
                {
                    "none" => 0,
                    "sapphire" => 0x10,
                    "gold" => 0x30,
                    "platinum" => 0x38,
                    _ => throw new SaveFormatException("Expected none, sapphire, gold or platinum.")
                };
                break;
            case "flags":
                bool ok = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? uint.TryParse(input[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
                    : uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                if (!ok || (value & track.Definition.AwardMask) != 0)
                    throw new SaveFormatException("Expected raw u32 other flags with award bits clear; edit awards through trophy/relic/token.");
                mask = ~track.Definition.AwardMask;
                break;
            default: throw new SaveFormatException("Unknown track field.");
        }
        BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(track.Offset, 4), (old & ~mask) | value);
    }

    private static bool ParseBool(string value) => value.ToLowerInvariant() switch
    {
        "true" or "1" => true,
        "false" or "0" => false,
        _ => throw new SaveFormatException("Expected true/false or 1/0.")
    };

    public IEnumerable<(string Key, object? Value)> Semantic() => Fields.Select(f => (f.Key, (object?)f.Value)).Concat(Progress());

    public IEnumerable<(string Key, object? Value)> Progress()
    {
        var mask = Fields.FirstOrDefault(f => f.Key == "slot.mask_15");
        if (mask is null) yield break;
        uint bits = Convert.ToUInt32(mask.Value, CultureInfo.InvariantCulture);
        for (int i = 0; i < 32; i++)
            yield return (i switch
            {
                1 => "progress.t111_crash_cove.trophy",
                2 => "progress.first_hub_capture_group_bit02",
                3 => "progress.first_hub_capture_group_bit03",
                _ => $"progress.unresolved_event_{i:D2}"
            }, (bits & (1u << i)) != 0);
        var doors = Fields.FirstOrDefault(f => f.Key == "slot.mask_16");
        if (doors is null) yield break;
        uint unlocks = Convert.ToUInt32(doors.Value, CultureInfo.InvariantCulture);
        for (int i = 0; i < 32; i++)
            yield return (i switch
            {
                1 => "unlock.ripper_roo_challenge",
                7 => "unlock.roo_reward_group_bit07",
                13 => "unlock.roo_reward_group_bit13",
                _ => $"unlock.unresolved_bit_{i:D2}"
            }, (unlocks & (1u << i)) != 0);
    }

    public void Set(byte[] target, string key, string input)
    {
        if (key.StartsWith("track.", StringComparison.Ordinal)) { SetTrack(target, key, input); return; }
        var f = Fields.FirstOrDefault(x => x.Key == key)
            ?? throw new SaveFormatException($"Unknown key '{key}'. Use 'dump' to list editable keys.");
        if (Objects.Any(o => o.ClassName == "COctaneGlobalSaveData") && f.Key != "global.wumpa_coins" && f.Type != "egg-bit" && f.Type != "character-bit" && f.Type != "unlock-bool")
            throw new SaveFormatException("Global field is read-only until its meaning is verified: " + key);
        if (f.Type == "character-bit") { GlobalProfile.SetCharacter(target, f, input); return; }
        if (f.Type == "egg-bit") { GlobalProfile.SetEgg(target, f, input); return; }
        if (f.Type == "unlock-bool") { target[f.Offset] = GlobalUnlocks.Boolean(input) ? (byte)1 : (byte)0; return; }
        var span = target.AsSpan(f.Offset, f.Size);
        switch (f.Type)
        {
            case "i32":
                if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                    throw new SaveFormatException("Expected a signed 32-bit decimal integer.");
                BinaryPrimitives.WriteInt32LittleEndian(span, i); break;
            case "u32":
                uint u;
                bool ok = input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? uint.TryParse(input[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u)
                    : uint.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out u);
                if (!ok) throw new SaveFormatException("Expected an unsigned 32-bit integer (decimal or 0x hex).");
                BinaryPrimitives.WriteUInt32LittleEndian(span, u); break;
            case "f32":
                if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || !float.IsFinite(v))
                    throw new SaveFormatException("Expected a finite float using '.' as the decimal separator.");
                BinaryPrimitives.WriteSingleLittleEndian(span, v); break;
            case "bool":
                span[0] = input.ToLowerInvariant() switch
                {
                    "1" or "true" => 1,
                    "0" or "false" => 0,
                    _ => throw new SaveFormatException("Expected true/false or 1/0."),
                };
                break;
            default: throw new SaveFormatException($"'{key}' is read-only ({f.Type}).");
        }
    }

    public object ToJson() => new
    {
        decoded = Decoded,
        progress = Progress().ToDictionary(p => p.Key, p => p.Value),
        counts = new { strings = Strings.Count, classes = Classes.Count, names = Names.Count, objects = Objects.Count, memories = Memories.Count },
        fields = Fields.ToDictionary(f => f.Key, f => new
        {
            type = f.Type,
            offset = f.Offset,
            size = f.Size,
            value = f.Value,
            note = f.Note,
            editable = First("COctaneGlobalSaveData") is not null ? f.Key == "global.wumpa_coins" || f.Type is "egg-bit" or "character-bit" or "unlock-bool" or "marketplace-item" or "skin-unlock" or "driver-wins" or "ghost-level" or "time-record" or "customization-unlock" or "reward-flag" : f.Type is "i32" or "u32" or "f32" or "bool" or "relic"
        }),
        classCounts = Objects.GroupBy(o => o.ClassName).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()),
    };

    public string DumpText()
    {
        var s = new StringBuilder();
        if (!Decoded) return s.ToString();
        s.AppendLine($"graph: {Objects.Count} objects, {Memories.Count} memory blocks, {Classes.Count} classes, {Strings.Count} strings");
        foreach (var f in Fields)
        {
            string v = f.Value switch
            {
                uint u => $"0x{u:X8}",
                float x => x.ToString("0.###", CultureInfo.InvariantCulture),
                bool x => x ? "true" : "false",
                _ => Convert.ToString(f.Value, CultureInfo.InvariantCulture) ?? "",
            };
            s.AppendLine($"{f.Key} = {v} [{f.Type} @ 0x{f.Offset:X}]" + (f.Note is null ? "" : "  # " + f.Note));
        }
        foreach (var (key, value) in Progress()) s.AppendLine($"{key} = {value}");
        return s.ToString();
    }
}
