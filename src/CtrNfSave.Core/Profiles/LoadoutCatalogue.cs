using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CtrNfSave;

/// <summary>Names and authored IDs only; list positions and named-handle suffixes are distinct.</summary>
internal static class LoadoutCatalogue
{
    internal sealed record Part(int? Index, string Handle, string? NameEn, string? NameFr);
    internal sealed record Driver(int? Index, string Handle, string? NameEn, string? NameFr,
        [property: JsonPropertyName("driver")] string DriverId, Part[] Skins);
    internal sealed record Special(int Index, string Handle, string? NameEn, string? NameFr,
        string Chassis, int Wheels, int Paints, int Stickers, string? DecalHandle);
    private sealed record Data(Driver[] Drivers, Part[] Karts, Part[] Wheels, Part[] Paints, Part[] Stickers, Special[] Presets);
    private static readonly Data Catalogue = Read();
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
