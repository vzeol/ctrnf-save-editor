namespace CtrNfSave;

public sealed record TrackDefinition(string Id, string Name, string Hub, bool HasTrophy, bool HasRelic, int? TokenBit)
{
    public string? BossName => Id switch
    {
        "t112_roos_tubes" => "Ripper Roo",
        "t133_papus_pyramid" => "Papu Papu",
        "t142_dragon_mines" => "Komodo Joe",
        "t153_hot_air_skyway" => "Pinstripe",
        _ => null
    };
    public string? TokenColour => TokenBit switch { 6 => "red", 7 => "blue", 8 => "purple", 9 => "green", 10 => "yellow", _ => null };
    public uint AwardMask => (HasTrophy ? 2u : 0) | (BossName is not null ? 4u : 0) | (HasRelic ? AdventureTracks.RelicMask : 0) | (TokenBit is int bit ? 1u << bit : 0);
}

public sealed record TrackState(TrackDefinition Definition, int Offset, uint Flags)
{
    public bool Boss => Definition.BossName is not null && (Flags & 4) != 0;
    public bool Trophy => Definition.HasTrophy && (Flags & 2) != 0;
    public string Relic => !Definition.HasRelic ? "none" : (Flags & 8) != 0 ? "platinum" : (Flags & 32) != 0 ? "gold" : (Flags & 16) != 0 ? "sapphire" : "none";
    public bool Token => Definition.TokenBit is int bit && (Flags & (1u << bit)) != 0;
    public uint OtherFlags => Flags & ~Definition.AwardMask;
}

public sealed record AdventureTotals(int Trophies, int Sapphire, int Gold, int Platinum, IReadOnlyDictionary<string, int> Tokens)
{
    public int Relics => Sapphire + Gold + Platinum;
    public int TokenCount => Tokens.Values.Sum();
    public static AdventureTotals From(IEnumerable<TrackState> tracks)
    {
        var rows = tracks.ToArray();
        return new(rows.Count(t => t.Trophy), rows.Count(t => t.Relic == "sapphire"), rows.Count(t => t.Relic == "gold"),
            rows.Count(t => t.Relic == "platinum"), new[] { "red", "green", "purple", "blue", "yellow" }
                .ToDictionary(c => c, c => rows.Count(t => t.Token && t.Definition.TokenColour == c)));
    }
}

public static class AdventureTracks
{
    public const uint RelicMask = 0x38;
    public static IReadOnlyList<TrackDefinition> All { get; } = Array.AsReadOnly<TrackDefinition>(
    [
        new("t111_crash_cove", "Crash Cove", "N. Sanity Beach", true, true, 6),
        new("t112_roos_tubes", "Roo's Tubes", "N. Sanity Beach", true, true, 9),
        new("t113_mystery_caves", "Mystery Caves", "N. Sanity Beach", true, true, 6),
        new("t114_sewer_speedway", "Sewer Speedway", "N. Sanity Beach", true, true, 7),
        new("t131_coco_park", "Coco Park", "Lost Ruins", true, true, 9),
        new("t132_tiger_temple", "Tiger Temple", "Lost Ruins", true, true, 7),
        new("t133_papus_pyramid", "Papu's Pyramid", "Lost Ruins", true, true, 6),
        new("t134_dingo_canyon", "Dingo Canyon", "Lost Ruins", true, true, 10),
        new("t141_blizzard_bluff", "Blizzard Bluff", "Glacier Park", true, true, 6),
        new("t142_dragon_mines", "Dragon Mines", "Glacier Park", true, true, 7),
        new("t143_polar_pass", "Polar Pass", "Glacier Park", true, true, 9),
        new("t144_tiny_arena", "Tiny Arena", "Glacier Park", true, true, 10),
        new("t151_ngin_labs", "N. Gin Labs", "Citadel City", true, true, 7),
        new("t152_cortex_castle", "Cortex Castle", "Citadel City", true, true, 9),
        new("t153_hot_air_skyway", "Hot Air Skyway", "Citadel City", true, true, 10),
        new("t154_oxide_station", "Oxide Station", "Citadel City", true, true, 10),
        new("t121_slide_coliseum", "Slide Coliseum", "Gem Stone Valley / arenas", false, true, null),
        new("t122_turbo_track", "Turbo Track", "Gem Stone Valley / arenas", false, true, null),
        new("a115_skull_rock", "Skull Rock", "Gem Stone Valley / arenas", false, false, 8),
        new("a135_rampage_ruins", "Rampage Ruins", "Gem Stone Valley / arenas", false, false, 8),
        new("a145_rocky_road", "Rocky Road", "Gem Stone Valley / arenas", false, false, 8),
        new("a155_nitro_court", "Nitro Court", "Gem Stone Valley / arenas", false, false, 8),
    ]);
}
