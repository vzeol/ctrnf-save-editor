using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CtrNfSave;

/// <summary>Two named handles and all six scalar words of the ten-word native record.</summary>
public sealed record KartPreset(string Name, string Driver, int Skin, string Chassis, int Wheels, int Paint,
    int Word7, int Sticker, int Word9, int FormatVersion)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public void Validate()
    {
        if (FormatVersion != 1) throw new SaveFormatException("Unsupported Garage preset format version (expected 1).");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 100 || Name.Any(char.IsControl))
            throw new SaveFormatException("Preset name must contain 1 to 100 characters without control characters.");
        KartLoadout.ValidateHandle(Driver, "Drivers"); KartLoadout.ValidateHandle(Chassis, "Karts");
        if (Skin < 0 || Wheels < 0 || Paint < 0 || Sticker < 0)
            throw new SaveFormatException("Skin, wheels, paint and sticker indices must be nonnegative integers.");
        // Word7 and Word9 have unknown meanings: preserve every signed bit, including -1.
    }
    public string ToJson() { Validate(); return JsonSerializer.Serialize(this, Options); }
    public static KartPreset FromJson(string json)
    {
        try
        {
            if (json.Length > 16384) throw new SaveFormatException("Garage preset JSON is too large.");
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1))
                throw new SaveFormatException("Expected a Garage preset object without duplicate properties.");
            var preset = JsonSerializer.Deserialize<KartPreset>(json, Options) ?? throw new SaveFormatException("Missing Garage preset.");
            preset.Validate(); return preset;
        }
        catch (JsonException e) { throw new SaveFormatException("Invalid Garage preset JSON: " + e.Message); }
    }
}

public sealed record ActiveKartLoadout(GraphObject Record, KartPreset Preset);
public sealed record KartOwnership(bool? Driver, bool? Chassis);

public static class KartLoadout
{
    public const uint SystemDataNamespace = 0x6236AC84;
    internal static void ValidateHandle(string? name, string category)
    {
        // Some active authored handles (Spyro, Champion, etc.) have no available-list suffix.
        if (name is not null && LoadoutCatalogue.KnownHandle(name, category)) return;
        string prefix = "SystemData_available" + category + "_";
        if (name is null || !name.StartsWith(prefix, StringComparison.Ordinal) ||
            !int.TryParse(name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
            index < 0 || name != prefix + index.ToString(CultureInfo.InvariantCulture))
            throw new SaveFormatException("Invalid Garage " + category + " handle name.");
    }
    /// <summary>Verified against controlled driver and kart name/hash pairs; ASCII case folded FNV-1a.</summary>
    public static uint NameHash(string name)
    {
        uint hash = 2166136261;
        foreach (char c in name)
        {
            if (c is < ' ' or > '~') throw new SaveFormatException("Unsupported non-ASCII graph name.");
            hash = unchecked((hash ^ (byte)(c is >= 'A' and <= 'Z' ? c + 32 : c)) * 16777619);
        }
        return hash;
    }
    public static ActiveKartLoadout Read(Save save, string name = "Current loadout")
    {
        GlobalProfile.Require(save); var graph = save.Graph;
        var root = graph.Objects.Single(o => o.ClassName == "COctaneGlobalSaveData");
        var lists = graph.Objects.Where(o => o.ClassName == "COctaneCustomizationSaveDataList").ToArray();
        if (lists.Length != 1 || root.Words[0] != lists[0].Id || lists[0].Words.Length != 3 ||
            lists[0].Words[0] != 4 || lists[0].Words[1] != 4 ||
            !graph.Memories.TryGetValue((int)lists[0].Words[2], out var memory) || memory.Count != 4 ||
            memory.Stride != 4 || memory.Meta != "igObjectRefMetaField")
            throw new SaveFormatException("Unsupported Garage customization list (four records required).");
        var ids = Enumerable.Range(0, 4).Select(i => Binary.U32(graph.SourceBytes, memory.Offset + i * 4)).ToArray();
        var records = graph.Objects.Where(o => ids.Contains((uint)o.Id)).ToArray();
        if (ids.Distinct().Count() != 4 || records.Length != 4 || records.Any(o => o.ClassName != "COctaneCustomizationSaveData" || o.Words.Length != 10) ||
            graph.Objects.Count(o => o.ClassName == "COctaneCustomizationSaveData") != 4)
            throw new SaveFormatException("Invalid or ambiguous Garage records.");
        uint[] empty = [0, 0, uint.MaxValue, 0, 0, uint.MaxValue, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue];
        var filled = records.Where(o => !o.Words.SequenceEqual(empty)).ToArray();
        if (filled.Length != 1) throw new SaveFormatException("Garage requires exactly one active loadout; empty or ambiguous records are read-only.");
        var record = filled[0]; var w = record.Words;
        if (w[1] != SystemDataNamespace || w[4] != SystemDataNamespace ||
            !graph.Names.TryGetValue(w[0], out string? driver) || !graph.Names.TryGetValue(w[3], out string? chassis) ||
            !graph.Names.TryGetValue(SystemDataNamespace, out string? space) || space != "SystemData" ||
            NameHash(driver) != w[0] || NameHash(chassis) != w[3])
            throw new SaveFormatException("Garage handles cannot be resolved safely through the graph name table.");
        var preset = new KartPreset(name, driver, unchecked((int)w[2]), chassis, unchecked((int)w[5]), unchecked((int)w[6]),
            unchecked((int)w[7]), unchecked((int)w[8]), unchecked((int)w[9]), 1);
        preset.Validate(); return new(record, preset);
    }
    public static string DriverName(string name, bool french = false) => LoadoutCatalogue.DriverName(name, french);
    public static string SkinName(string driver, int index, bool french = false) => LoadoutCatalogue.SkinName(driver, index, french);
    public static string ChassisName(string name, bool french = false) => LoadoutCatalogue.ChassisName(name, french);
    public static string ChassisName(KartPreset preset, bool french = false) => LoadoutCatalogue.ChassisName(preset, french);
    public static string WheelsName(int index, bool french = false) => LoadoutCatalogue.WheelsName(index, french);
    public static string PaintName(int index, bool french = false) => LoadoutCatalogue.PaintName(index, french);
    public static string StickerName(int index, bool french = false) => LoadoutCatalogue.StickerName(index, french);
    private static DriverEntry? DriverEntry(string name)
    {
        string? identity = LoadoutCatalogue.FindDriver(name)?.DriverId;
        return GlobalUnlocks.Drivers.SingleOrDefault(d => d.Driver == identity);
    }
    public static KartOwnership Ownership(Save save, KartPreset preset)
    {
        GlobalProfile.Require(save); preset.Validate(); var values = save.Semantic();
        var driver = DriverEntry(preset.Driver);
        bool? owned = driver is null ? null : driver.Key is string key && values.GetValueOrDefault(key) is bool state ? state :
            driver.Condition is null && driver.MarketplaceIds.Length == 0 ? true : null;
        // Authored chassis catalogue handles are names, not SystemData array indices. No verified crosswalk yet.
        return new(owned, null);
    }
    public static Save Edit(Save save, string? driver = null, int? skin = null, string? chassis = null,
        int? wheels = null, int? paint = null, int? sticker = null, string? special = null) =>
        Apply(save, PrepareEdit(Read(save).Preset, driver, skin, chassis, wheels, paint, sticker, special));

    /// <summary>Validate selected catalogue values and compose a complete preset without writing a save.</summary>
    public static KartPreset PrepareEdit(KartPreset current, string? driver = null, int? skin = null, string? chassis = null,
        int? wheels = null, int? paint = null, int? sticker = null, string? special = null)
    {
        static void Member(IReadOnlyList<LoadoutCatalogue.Choice> choices, string value, string part)
        {
            if (!choices.Any(c => c.Value == value)) throw new SaveFormatException("Invalid Garage " + part + ": " + value);
        }
        static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
        if (driver is not null) Member(LoadoutCatalogue.Drivers(), driver, "driver");
        string selectedDriver = driver ?? current.Driver;
        if (skin is not null) Member(LoadoutCatalogue.Skins(selectedDriver), Number(skin.Value), "skin");
        var result = current with { Driver = selectedDriver,
            Skin = skin ?? (selectedDriver != current.Driver ? 0 : current.Skin) };
        if (special is not null)
        {
            if (chassis is not null || wheels is not null || paint is not null || sticker is not null)
                throw new SaveFormatException("A special kart cannot be combined with chassis or part options.");
            var tuple = LoadoutCatalogue.CompleteSpecials.SingleOrDefault(p =>
                p.Handle.Equals(special, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.NameEn, special, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.NameFr, special, StringComparison.OrdinalIgnoreCase))
                ?? throw new SaveFormatException("Unknown or incomplete special kart: " + special);
            return result with { Chassis = tuple.Chassis, Wheels = tuple.Wheels, Paint = tuple.Paints,
                Word7 = tuple.Word7!.Value, Sticker = tuple.Stickers, Word9 = tuple.Word9!.Value };
        }
        if (chassis is not null)
        {
            Member(LoadoutCatalogue.Chassis(), chassis, "chassis");
            result = result with { Chassis = chassis, Word7 = -1, Word9 = -1 };
            // A special kart's own wheels, paint and sticker must not stay on a normal chassis.
            if (LoadoutCatalogue.SpecialChassis(current.Chassis)) result = result with { Wheels = 0, Paint = 0, Sticker = 0 };
        }
        if (LoadoutCatalogue.SpecialChassis(result.Chassis) && (wheels is not null || paint is not null || sticker is not null))
            throw new SaveFormatException("Choose a normal chassis before editing special kart parts.");
        if (wheels is not null) Member(LoadoutCatalogue.Wheels(), Number(wheels.Value), "wheels");
        if (paint is not null) Member(LoadoutCatalogue.Paints(), Number(paint.Value), "paint");
        if (sticker is not null) Member(LoadoutCatalogue.Stickers(), Number(sticker.Value), "sticker");
        return result with { Wheels = wheels ?? result.Wheels, Paint = paint ?? result.Paint, Sticker = sticker ?? result.Sticker };
    }
    public static Save Apply(Save save, KartPreset preset)
    {
        preset.Validate(); Read(save);
        Save prepared = AddNames(save, [preset.Driver, preset.Chassis]);
        var record = Read(prepared).Record; byte[] bytes = prepared.Serialize();
        uint[] words = [NameHash(preset.Driver), SystemDataNamespace, (uint)preset.Skin, NameHash(preset.Chassis), SystemDataNamespace,
            (uint)preset.Wheels, (uint)preset.Paint, unchecked((uint)preset.Word7), (uint)preset.Sticker, unchecked((uint)preset.Word9)];
        for (int i = 0; i < words.Length; i++) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(record.WordOffset(i), 4), words[i]);
        var result = Save.Parse(bytes);
        if (Read(result, preset.Name).Preset != preset) throw new SaveFormatException("Garage apply verification failed.");
        return result;
    }
    private static Save AddNames(Save save, string[] requested)
    {
        var graph = save.Graph; byte[] bytes = save.Serialize();
        var section = graph.Sections.Single(s => s.Name == "graph_names");
        if (section.Count != graph.Names.Count || graph.Names.Values.Distinct(StringComparer.Ordinal).Count() != section.Count ||
            graph.Names.Any(p => NameHash(p.Value) != p.Key))
            throw new SaveFormatException("Ambiguous or unverified graph name hashes; Garage apply refused.");
        var missing = requested.Distinct(StringComparer.Ordinal).Where(n => !graph.Names.ContainsValue(n)).ToArray();
        if (missing.Length == 0) return save;
        if (missing.Any(n => graph.Names.ContainsKey(NameHash(n))) || missing.Select(NameHash).Distinct().Count() != missing.Length)
            throw new SaveFormatException("Graph name hash collision; Garage apply refused.");
        int growth = missing.Sum(n => 4 + Encoding.UTF8.GetByteCount(n) + 1);
        var memory = graph.Sections.Single(s => s.Name == "graph_memory"); int end = memory.Offset + memory.Size;
        if (end + growth > bytes.Length || bytes.AsSpan(end).IndexOfAnyExcept((byte)0) >= 0)
            throw new SaveFormatException("Insufficient verified zero padding to add Garage names; apply refused.");
        // Insert hashes before the old strings, then append names after the old strings.
        // All following sections move together; graph references are allocation IDs, not physical offsets.
        int hashesEnd = section.Offset + 12 + section.Count * 4, namesEnd = section.Offset + section.Size;
        byte[] output = new byte[bytes.Length]; bytes.AsSpan(0, hashesEnd).CopyTo(output);
        int at = hashesEnd;
        foreach (string n in missing) { BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(at, 4), NameHash(n)); at += 4; }
        bytes.AsSpan(hashesEnd, namesEnd - hashesEnd).CopyTo(output.AsSpan(at)); at += namesEnd - hashesEnd;
        foreach (string n in missing) { byte[] text = Encoding.UTF8.GetBytes(n + "\0"); text.CopyTo(output, at); at += text.Length; }
        bytes.AsSpan(namesEnd, end - namesEnd).CopyTo(output.AsSpan(at));
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(section.Offset, 4), section.Size + growth);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(section.Offset + 8, 4), section.Count + missing.Length);
        var parsed = Save.Parse(output); GlobalProfile.Require(parsed);
        if (missing.Any(n => parsed.Graph.Names.GetValueOrDefault(NameHash(n)) != n) ||
            !bytes.AsSpan(namesEnd, end - namesEnd).SequenceEqual(output.AsSpan(namesEnd + growth, end - namesEnd)))
            throw new SaveFormatException("Garage name-table rebuild verification failed.");
        return parsed;
    }
}
