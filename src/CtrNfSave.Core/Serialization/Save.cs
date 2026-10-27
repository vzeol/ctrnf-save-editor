using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CtrNfSave;

public sealed class SaveFormatException(string message) : Exception(message);

public static class Binary
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public static ReadOnlySpan<byte> Slice(byte[] b, int offset, int size)
    {
        if (offset < 0 || size < 0 || (long)offset + size > b.Length)
            throw new SaveFormatException($"Range 0x{offset:X}+0x{size:X} is outside file (0x{b.Length:X} bytes).");
        return b.AsSpan(offset, size);
    }
    public static uint U32(byte[] b, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(Slice(b, offset, 4));
    public static ushort U16(byte[] b, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(Slice(b, offset, 2));
    public static int Count(uint n, int max, string label)
    {
        if (n > max) throw new SaveFormatException($"Invalid {label}: {n} (maximum {max}).");
        return (int)n;
    }
    public static string Text(byte[] b, int offset, int length)
    {
        var span = Slice(b, offset, length);
        if (span.Length > 0 && span[^1] == 0) span = span[..^1];
        if (span.Contains((byte)0)) throw new SaveFormatException($"Embedded NUL at 0x{offset:X}.");
        try { return Utf8.GetString(span); }
        catch (DecoderFallbackException) { throw new SaveFormatException($"Invalid UTF-8 at 0x{offset:X}."); }
    }
    public static (string Text, int Length) CString(byte[] b, int offset, int end)
    {
        var span = Slice(b, offset, end - offset);
        int n = span.IndexOf((byte)0);
        if (n < 0) throw new SaveFormatException($"Unterminated string at 0x{offset:X}.");
        return (Text(b, offset, n + 1), n + 1);
    }
}

public sealed record Section(string Name, int Offset, int Size, int Count);
public sealed record HeaderField(string Name, int Offset, int Size, uint Tag, object Value)
{
    public string Type => Tag switch { 0 => "i32", 1 => "f32", 2 => "string", _ => "unknown" };
}

public static class JsonOutput
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public static string Render(object? value) => JsonSerializer.Serialize(value, Options);
    public static bool Equal(object? a, object? b) => Render(a) == Render(b);
}

public sealed class Save
{
    private readonly byte[] bytes;
    public string Kind { get; }
    public uint[] Header { get; }
    public List<HeaderField> Fields { get; }
    public List<Section> Sections { get; }
    public Graph Graph { get; }
    public List<string> Warnings { get; }
    private Save(byte[] bytes, string kind, uint[] header, List<HeaderField> fields, List<Section> sections, Graph graph)
    {
        this.bytes = bytes; Kind = kind; Header = header; Fields = fields; Sections = sections; Graph = graph;
        Warnings = [..graph.Warnings,
            kind == "global" ? "Global profile: local unlock conditions and existing time-attack records; exports require in-game verification. Server data is separate." :
            "No checksum found. Trophy/relic/token removal confirmed in game; boss/dependency edits need verification.",
            "Header progress counters are summaries. Editing them does not complete races or grant items."];
    }
    public static Save Parse(byte[] input)
    {
        // Own the buffer so callers cannot change a parsed save behind the decoder.
        byte[] b = (byte[])input.Clone();
        if (b.Length != 153600 && b.Length != 7340032)
            throw new SaveFormatException($"Unsupported length {b.Length}; expected a decrypted 153600-byte slot or 7340032-byte global save.");
        uint[] header = Enumerable.Range(0, 13).Select(i => Binary.U32(b, i * 4)).ToArray();
        if (header[10] != 0xFADA || header[11] != 0xB400000C || header[12] != 3)
            throw new SaveFormatException("Unrecognized format signature at 0x28..0x34 (encrypted or unsupported file?).");
        int tableSize = Binary.Count(Binary.U32(b, 0x34), 12 * 4096, "header table size");
        if (tableSize == 0 || tableSize % 12 != 0) throw new SaveFormatException("Invalid header table size.");
        int count = tableSize / 12;
        int nameLengthAt = 0x38 + tableSize, nameBase = nameLengthAt + 4;
        int nameLen = Binary.Count(Binary.U32(b, nameLengthAt), b.Length - nameBase, "header name length");
        int valueLengthAt = nameBase + nameLen, valueBase = valueLengthAt + 4;
        int valueLen = Binary.Count(Binary.U32(b, valueLengthAt), b.Length - valueBase, "header value length");
        int valueEnd = valueBase + valueLen;
        List<HeaderField> fields = [];
        for (int i = 0; i < count; i++)
        {
            int o = 0x38 + i * 12;
            uint tag = Binary.U32(b, o);
            int no = Binary.Count(Binary.U32(b, o + 4), nameLen - 1, "name offset");
            int vo = Binary.Count(Binary.U32(b, o + 8), valueLen - 1, "value offset");
            string name = Binary.CString(b, nameBase + no, valueLengthAt).Text;
            if (fields.Any(f => f.Name == name)) throw new SaveFormatException($"Duplicate header key '{name}'.");
            int offset = valueBase + vo, size = 4;
            object value;
            switch (tag)
            {
                case 0: value = unchecked((int)Binary.U32(b, offset)); break;
                case 1: value = BitConverter.UInt32BitsToSingle(Binary.U32(b, offset)); break;
                case 2:
                    (string text, int length) = Binary.CString(b, offset, valueEnd);
                    value = text; size = length; break;
                default: throw new SaveFormatException($"Unknown header type {tag} for '{name}'.");
            }
            if ((long)offset + size > valueEnd) throw new SaveFormatException("Header value extends past blob.");
            if (fields.Any(f => offset < f.Offset + f.Size && f.Offset < offset + size))
                throw new SaveFormatException("Overlapping header values are unsupported.");
            fields.Add(new(name, offset, size, tag, value));
        }
        string kind = fields.Any(f => f.Name == "percent_complete") ? "slot" :
            fields.Any(f => f.Name == "most_recent_save_slot") ? "global" :
            throw new SaveFormatException("Cannot identify slot/global from named header keys.");
        if ((kind == "slot") != (b.Length == 153600)) throw new SaveFormatException("Save kind and physical length disagree.");
        List<Section> sections = [new("header_keys", 0x38, tableSize, count),
            new("header_names", nameBase, nameLen, count), new("header_values", valueBase, valueLen, count)];
        Graph graph = Graph.Parse(b, valueEnd, header);
        sections.AddRange(graph.Sections);
        return new Save(b, kind, header, fields, sections, graph);
    }
    public static Save Load(string file)
    {
        long n = new FileInfo(file).Length;
        if (n != 153600 && n != 7340032) throw new SaveFormatException($"Unsupported file length {n}.");
        return Parse(File.ReadAllBytes(file));
    }
    public byte[] Serialize() => (byte[])bytes.Clone();
    public HeaderField? Get(string key) => Fields.FirstOrDefault(f => f.Name == key || "header." + f.Name == key);
    public Save Set(string key, string input)
    {
        if (Kind == "global") GlobalProfile.Require(this);
        if (key == "global.unlock_all") return UnlockBatch.Set(this, UnlockBatch.Keys(this, UnlockScope.All), GlobalUnlocks.Boolean(input));
        if (key.StartsWith("global.reward.", StringComparison.Ordinal)) return TimeTrialRewards.Set(this, key, GlobalUnlocks.Boolean(input));
        if (key.StartsWith("global.customization.", StringComparison.Ordinal)) return CustomizationUnlocks.SetMany(this, [key], GlobalUnlocks.Boolean(input));
        if (key.StartsWith("global.ghost.", StringComparison.Ordinal) || key.StartsWith("global.time_attack.", StringComparison.Ordinal))
            return TimeAttack.Set(this, key, input);
        if (key.StartsWith("global.skin.", StringComparison.Ordinal)) return SkinUnlocks.Set(this, key, input);
        if (key.StartsWith("global.driver_wins.", StringComparison.Ordinal))
        {
            if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int wins)) throw new SaveFormatException("Expected a nonnegative driver-win count.");
            return SkinUnlocks.SetWins(this, key[19..], wins);
        }
        if (key == "slot.adventure_difficulty")
        {
            RequireAdventureSlot();
            if (!int.TryParse(input, out int difficulty) || difficulty is < 0 or > 2) throw new SaveFormatException("Adventure difficulty must be 0 (easy), 1 (medium) or 2 (hard).");
            byte[] result = Serialize();
            var field = Graph.Fields.Single(f => f.Key == key);
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(field.Offset, 4), difficulty);
            var header = Get("adventure_difficulty") ?? throw new SaveFormatException("Missing difficulty header.");
            BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(header.Offset, 4), difficulty);
            return Parse(result);
        }
        if (key.StartsWith("global.marketplace.item_", StringComparison.Ordinal))
        {
            if (!int.TryParse(key[24..], out int item)) throw new SaveFormatException("Invalid marketplace key.");
            return GlobalUnlocks.Boolean(input) ? GlobalUnlocks.SetMarketplace(this, [item], []) : GlobalUnlocks.SetMarketplace(this, [], [item]);
        }
        if (key == "global.wumpa_coins" && (!int.TryParse(input, out int coins) || coins < 0))
            throw new SaveFormatException("Wumpa coins must be between 0 and 2147483647.");
        byte[] edited = Serialize();
        HeaderField? f = Get(key);
        if (f is null) Graph.Set(edited, key, input);
        else
        {
            switch (f.Tag)
            {
                case 0:
                    if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
                        throw new SaveFormatException("Expected a signed 32-bit decimal integer.");
                    BinaryPrimitives.WriteInt32LittleEndian(edited.AsSpan(f.Offset, 4), i); break;
                case 1:
                    if (!float.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) || !float.IsFinite(v))
                        throw new SaveFormatException("Expected a finite 32-bit float using '.' as the decimal separator.");
                    BinaryPrimitives.WriteSingleLittleEndian(edited.AsSpan(f.Offset, 4), v); break;
                case 2:
                    if (input.Contains('\0')) throw new SaveFormatException("Strings cannot contain NUL.");
                    byte[] s = Encoding.UTF8.GetBytes(input + "\0");
                    if (s.Length != f.Size)
                        throw new SaveFormatException($"String must keep its encoded length ({f.Size - 1} UTF-8 bytes excluding NUL). Variable-length rebuilding is not verified.");
                    s.CopyTo(edited, f.Offset); break;
            }
        }
        if (key.StartsWith("track.", StringComparison.Ordinal))
        {
            RequireAdventureSlot();
            var totals = AdventureTotals.From(Graph.Tracks.Select(t => t with { Flags = Binary.U32(edited, t.Offset) }));
            void Summary(string name, int value)
            {
                var field = Graph.Fields.FirstOrDefault(x => x.Key == "slot." + name)
                    ?? throw new SaveFormatException("Missing slot summary: " + name);
                BinaryPrimitives.WriteInt32LittleEndian(edited.AsSpan(field.Offset, 4), value);
            }
            void HeaderSummary(string name, int value)
            {
                var field = Get(name) ?? throw new SaveFormatException("Missing header summary: " + name);
                if (field.Tag != 0) throw new SaveFormatException("Invalid summary type: " + name);
                BinaryPrimitives.WriteInt32LittleEndian(edited.AsSpan(field.Offset, 4), value);
            }
            Summary("trophy_count", totals.Trophies);
            Summary("relic_sapphire", totals.Sapphire); Summary("relic_gold", totals.Gold); Summary("relic_platinum", totals.Platinum);
            foreach (var (colour, count) in totals.Tokens)
            {
                Summary("token_" + colour, count);
                // Some format variants may also carry per-colour named headers.
                if (Get("token_" + colour) is not null) HeaderSummary("token_" + colour, count);
            }
            HeaderSummary("trophy_Count", totals.Trophies); HeaderSummary("relic_count", totals.Relics); HeaderSummary("ctr_token_count", totals.TokenCount);
        }
        return Parse(edited);
    }
    public void RequireAdventureSlot()
    {
        if (Kind != "slot" || !Graph.Decoded || Graph.Warnings.Count != 0 ||
            Graph.Tracks.Count != AdventureTracks.All.Count ||
            !AdventureTracks.All.All(d => Graph.Tracks.Any(t => t.Definition.Id == d.Id)))
            throw new SaveFormatException("Unsupported Adventure slot (22 tracks/challenges required). Open a decrypted 153600-byte .sav slot.");
    }

    public SortedDictionary<string, object?> Semantic()
    {
        var result = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        foreach (var f in Fields) result["header." + f.Name] = f.Value;
        foreach (var (key, value) in Graph.Semantic()) result[key] = value;
        return result;
    }
    public object ToJson() => new
    {
        kind = Kind,
        fileSize = bytes.Length,
        sectionHeader = Header,
        sections = Sections,
        header = Fields.ToDictionary(f => f.Name, f => new { type = f.Type, offset = f.Offset, size = f.Size, value = f.Value, editable = true }),
        graph = Graph.ToJson(),
        warnings = Warnings
    };
    public string DumpText()
    {
        StringBuilder s = new($"{Kind} save, {bytes.Length} bytes\n");
        foreach (var f in Fields) s.AppendLine($"header.{f.Name} = {JsonOutput.Render(f.Value)} [{f.Type} @ 0x{f.Offset:X}]");
        foreach (var section in Sections) s.AppendLine($"section {section.Name}: 0x{section.Offset:X}, {section.Size} bytes, {section.Count} entries");
        s.Append(Graph.DumpText());
        foreach (string w in Warnings) s.AppendLine("Note: " + w);
        return s.ToString();
    }
    public static List<object> Diff(Save a, Save b)
    {
        var left = a.Semantic(); var right = b.Semantic();
        return left.Keys.Union(right.Keys).Order(StringComparer.Ordinal)
            .Where(k => !left.ContainsKey(k) || !right.ContainsKey(k) || !JsonOutput.Equal(left[k], right[k]))
            .Select(k => (object)new { key = k, before = left.GetValueOrDefault(k), after = right.GetValueOrDefault(k) }).ToList();
    }
}
