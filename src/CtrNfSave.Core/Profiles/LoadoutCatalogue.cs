using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CtrNfSave;

/// <summary>Names and authored IDs only; list positions and named-handle suffixes are distinct.</summary>
public static class LoadoutCatalogue
{
    internal sealed record Part(int? Index, string Handle, string? NameEn, string? NameFr, int? ListIndex = null);
    internal sealed record Driver(int? Index, string Handle, string? NameEn, string? NameFr,
        [property: JsonPropertyName("driver")] string DriverId, Part[] Skins, int? ListIndex);
    internal sealed record Special(int Index, string Handle, string? NameEn, string? NameFr,
        string Chassis, int Wheels, int Paints, int Stickers, string? DecalHandle, int? Word7, int? Word9, string? Evidence);
    private sealed record Data(Driver[] Drivers, Part[] Karts, Part[] Wheels, Part[] Paints, Part[] Stickers, Special[] Presets);
    private static readonly Data Catalogue = Read();
    public sealed record Choice(string Value, string Name)
    {
        public override string ToString() => Name;
    }
    // Active roster precedes developer placeholders at list position 56; suffixes are not roster positions.
    public static IReadOnlyList<Choice> Drivers(bool french = false) => Catalogue.Drivers
        .Where(d => d.ListIndex is >= 0 and < 56).Select(d => new Choice(d.Handle, DriverName(d.Handle, french))).ToArray();
    public static IReadOnlyList<Choice> Skins(string driver, bool french = false) =>
        Choices(FindDriver(driver)?.Skins ?? [], french);
    public static IReadOnlyList<Choice> Chassis(bool french = false) => Catalogue.Karts
        .Where(k => k.ListIndex is not null && !Catalogue.Presets.Any(p => p.Chassis == k.Handle))
        .Select(k => new Choice(k.Handle, ChassisName(k.Handle, french))).ToArray();
    public static IReadOnlyList<Choice> Wheels(bool french = false) => Choices(Catalogue.Wheels, french);
    public static IReadOnlyList<Choice> Paints(bool french = false) => Choices(Catalogue.Paints, french);
    public static IReadOnlyList<Choice> Stickers(bool french = false) => Choices(Catalogue.Stickers, french);
    private static IReadOnlyList<Choice> Choices(Part[] parts, bool french) => parts.Where(p => p.Index is not null)
        .Select(p => new Choice(p.Index!.Value.ToString(CultureInfo.InvariantCulture),
            Label(p.NameEn, p.NameFr, french, p.Index.Value.ToString(CultureInfo.InvariantCulture)))).ToArray();
    internal static IEnumerable<Special> CompleteSpecials => Catalogue.Presets
        .Where(p => p.Word7 is not null && p.Word9 is not null && !string.IsNullOrWhiteSpace(p.Evidence));
    public static IReadOnlyList<Choice> Specials(bool french = false) => CompleteSpecials
        .Select(p => new Choice(p.Handle, Label(p.NameEn, p.NameFr, french, p.Handle))).ToArray();
    public static string? SelectedSpecial(KartPreset preset) => CompleteSpecials.SingleOrDefault(p =>
        p.Chassis == preset.Chassis && p.Wheels == preset.Wheels && p.Paints == preset.Paint &&
        p.Stickers == preset.Sticker && p.Word7 == preset.Word7 && p.Word9 == preset.Word9)?.Handle;
    internal static bool SpecialChassis(string chassis) => Catalogue.Presets.Any(p => p.Chassis == chassis);
    private static Data Read()
    {
        using var stream = typeof(LoadoutCatalogue).Assembly.GetManifestResourceStream("CtrNfSave.Core.Data.loadouts.json")
            ?? throw new InvalidOperationException("Missing loadout catalogue.");
        return JsonSerializer.Deserialize<Data>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Invalid loadout catalogue.");
    }
    internal static Driver? FindDriver(string handle) => Catalogue.Drivers.SingleOrDefault(d => d.Handle == handle);
    internal static bool KnownHandle(string handle, string category) => category == "Drivers" ?
        FindDriver(handle) is not null : Catalogue.Karts.Any(k => k.Handle == handle);
    private static string Label(string? en, string? fr, bool french, string raw) =>
        (french ? fr : en) is { Length: > 0 } name ? name : raw;
    internal static string DriverName(string handle, bool french)
    {
        var driver = FindDriver(handle);
        return Label(driver?.NameEn, driver?.NameFr, french, handle);
    }
    internal static string SkinName(string handle, int index, bool french) =>
        Name(FindDriver(handle)?.Skins ?? [], index, french);
    private static string Name(Part[] parts, int index, bool french)
    {
        var part = parts.SingleOrDefault(p => p.Index == index);
        return Label(part?.NameEn, part?.NameFr, french, index.ToString(CultureInfo.InvariantCulture));
    }
    internal static string ChassisName(string handle, bool french)
    {
        var kart = Catalogue.Karts.SingleOrDefault(k => k.Handle == handle);
        return Label(kart?.NameEn, kart?.NameFr, french, handle);
    }
    internal static string ChassisName(KartPreset preset, bool french)
    {
        // Only the two controlled complete special tuples justify a preset display name.
        // Word7/9 retain their unknown signed semantics; this does not enable independent edits.
        bool captured = (preset.Chassis == "SystemData_availableKarts_15" && preset.Word9 == 0 && preset.Word7 == -1) ||
            (preset.Chassis == "SystemData_availableKarts_32" && preset.Word9 == 8 && preset.Word7 == 0);
        var special = captured ? Catalogue.Presets.SingleOrDefault(p => p.Index == preset.Word9 &&
            p.Chassis == preset.Chassis && p.Wheels == preset.Wheels && p.Paints == preset.Paint && p.Stickers == preset.Sticker) : null;
        return special is null ? ChassisName(preset.Chassis, french) :
            Label(special.NameEn, special.NameFr, french, preset.Chassis);
    }
    internal static string WheelsName(int index, bool french) => Name(Catalogue.Wheels, index, french);
    internal static string PaintName(int index, bool french) => Name(Catalogue.Paints, index, french);
    internal static string StickerName(int index, bool french) => Name(Catalogue.Stickers, index, french);
}
