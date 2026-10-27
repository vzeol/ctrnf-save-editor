using System.Buffers.Binary;
using System.Globalization;

namespace CtrNfSave;

public sealed record AdventureIssue(string Severity, string Code, string Message, params string[] Arguments);
public sealed record AdventureEdit(Save Save, IReadOnlyList<string> Changes);

/// <summary>Known award dependencies; unknown progression encodings are preserved.</summary>
public static class AdventureRules
{
    public static IReadOnlyList<string> Colours { get; } = Array.AsReadOnly<string>(["red", "green", "blue", "yellow", "purple"]);
    public static IReadOnlyList<string> BossTracks { get; } = Array.AsReadOnly<string>(["t112_roos_tubes", "t133_papus_pyramid", "t142_dragon_mines", "t153_hot_air_skyway"]);
    private const string Slide = "track.t121_slide_coliseum.relic", Turbo = "track.t122_turbo_track.relic";
    public static int Keys(Save save) => save.Graph.Tracks.Count(t => t.Boss);
    public static bool Gem(Save save, string colour) => Convert.ToInt32(save.Graph.Fields.Single(f => f.Key == "slot.gem_" + colour).Value, CultureInfo.InvariantCulture) != 0;

    private sealed class State
    {
        public readonly Dictionary<string, string> Values = [];
        public readonly Dictionary<string, string[]> Needs = [];
        public State(Save save)
        {
            foreach (var t in save.Graph.Tracks)
            {
                string p = "track." + t.Definition.Id + ".";
                if (t.Definition.HasTrophy) Values[p + "trophy"] = t.Trophy ? "true" : "false";
                if (t.Definition.HasRelic) Values[p + "relic"] = t.Relic;
                if (t.Definition.TokenBit is not null) Values[p + "token"] = t.Token ? "true" : "false";
                if (t.Definition.BossName is not null) Values[p + "boss"] = t.Boss ? "true" : "false";
            }
            foreach (string c in Colours) Values["slot.gem_" + c] = Gem(save, c) ? "1" : "0";
            Values["slot.oxide_first"] = save.Semantic()["slot.oxide_first"] is true ? "true" : "false";
            Values["slot.oxide_second"] = save.Semantic()["slot.oxide_second"] is true ? "true" : "false";
            Needs["slot.oxide_first"] = BossTracks.Select(t => "track." + t + ".boss").ToArray();
            Needs["slot.oxide_second"] = ["slot.oxide_first", .. Colours.Select(c => "slot.gem_" + c),
                .. AdventureTracks.All.Where(t => t.HasRelic).Select(t => "track." + t.Id + ".relic"),
                .. AdventureTracks.All.Where(t => t.TokenBit is not null).Select(t => "track." + t.Id + ".token")];
            for (int hub = 0; hub < 4; hub++)
            {
                string boss = "track." + BossTracks[hub] + ".boss";
                var races = AdventureTracks.All.Where(t => t.HasTrophy).Skip(hub * 4).Take(4).ToArray();
                Needs[boss] = races.Select(t => "track." + t.Id + ".trophy").ToArray();
                foreach (var t in races)
                {
                    string p = "track." + t.Id + ".";
                    if (hub > 0) Needs[p + "trophy"] = ["track." + BossTracks[hub - 1] + ".boss"];
                    Needs[p + "token"] = [boss]; Needs[p + "relic"] = [boss];
                }
                string arena = AdventureTracks.All.Where(t => t.TokenBit == 8).ElementAt(hub).Id;
                Needs["track." + arena + ".token"] = [boss];
            }
            Needs["track.t114_sewer_speedway.trophy"] = ["track.t113_mystery_caves.trophy"];
            Needs[Turbo] = [.. Colours.Select(c => "slot.gem_" + c)];
            foreach (string c in Colours)
                Needs["slot.gem_" + c] = AdventureTracks.All.Where(t => t.TokenColour == c).Select(t => "track." + t.Id + ".token").ToArray();
        }
        private string[] FirstRaces => ["track.t111_crash_cove.trophy", "track.t112_roos_tubes.trophy"];
        private bool MysteryGate(string key) => key == "track.t113_mystery_caves.trophy" && !FirstRaces.Any(On);
        public bool On(string key) => Values[key] is not ("false" or "0" or "none");
        public void Enable(string key)
        {
            if (!On(key)) Values[key] = key.EndsWith(".relic", StringComparison.Ordinal) ? "sapphire" : key.StartsWith("slot.gem_", StringComparison.Ordinal) ? "1" : "true";
            if (MysteryGate(key)) Enable(FirstRaces[0]);
            if (Needs.TryGetValue(key, out var requirements)) foreach (string p in requirements) Enable(p);
            if (key == Slide)
                foreach (var t in AdventureTracks.All.Where(t => t.HasTrophy))
                {
                    if (OtherRelics() >= 10) break;
                    Enable("track." + t.Id + ".relic");
                }
        }
        public int OtherRelics() => Values.Count(p => p.Key.EndsWith(".relic", StringComparison.Ordinal) && p.Key != Slide && On(p.Key));
        public void Prune()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (string key in Values.Keys.ToArray())
                    if (On(key) && ((Needs.TryGetValue(key, out var ps) && ps.Any(p => !On(p))) || (key == Slide && OtherRelics() < 10) || MysteryGate(key)))
                    { Values[key] = Off(key); changed = true; }
            } while (changed);
        }
    }
    private static string Off(string key) => key.EndsWith(".relic", StringComparison.Ordinal) ? "none" : key.StartsWith("slot.gem_", StringComparison.Ordinal) ? "0" : "false";

    /// <summary>Remove invalid downstream awards; never invent victories to repair a file.</summary>
    public static Save Normalize(Save save)
    {
        save.RequireAdventureSlot(); var state = new State(save); state.Prune(); return Write(save, state);
    }
    /// <summary>Preview without disk writes: additions complete prerequisites, removals cascade.</summary>
    public static AdventureEdit Edit(Save save, string key, string input)
    {
        save.RequireAdventureSlot(); var state = new State(save);
        if (!state.Values.ContainsKey(key)) throw new SaveFormatException("Unsupported Adventure field: " + key);
        if (key.StartsWith("slot.gem_", StringComparison.Ordinal) && input is not ("0" or "1"))
            throw new SaveFormatException("A gem accepts only 0 or 1.");
        var requested = new State(save.Set(key, input)); // Strict existing field validation.
        state.Values[key] = requested.Values[key];
        if (state.On(key)) state.Enable(key);
        state.Prune();
        var original = new State(save);
        bool awardsChanged = state.Values.Any(p => original.Values[p.Key] != p.Value);
        Save result = Write(save, state, awardsChanged);
        var before = save.Semantic(); var after = result.Semantic();
        return new(result, after.Where(p => before.TryGetValue(p.Key, out var old) && !JsonOutput.Equal(old, p.Value))
            .Select(p => $"{p.Key} : {before[p.Key]} → {p.Value}").ToArray());
    }
    public static IReadOnlyList<AdventureIssue> Validate(Save save)
    {
        save.RequireAdventureSlot(); var state = new State(save); List<AdventureIssue> issues = [];
        if (state.On("track.t113_mystery_caves.trophy") && !state.On("track.t111_crash_cove.trophy") && !state.On("track.t112_roos_tubes.trophy"))
            issues.Add(new("error", "mystery", "Mystery Caves requires 1 earlier trophy."));
        foreach (var (key, ps) in state.Needs)
            if (state.On(key)) foreach (string p in ps.Where(p => !state.On(p)))
                issues.Add(new("error", "prerequisite", $"{key} requires {p}.", key, p));
        if (state.On(Slide) && state.OtherRelics() < 10) issues.Add(new("error", "slide", "Slide Coliseum requires 10 other relics."));
        var normalized = Write(save, state);
        if (!JsonOutput.Equal(save.Semantic()["slot.mask_15"], normalized.Semantic()["slot.mask_15"]))
            issues.Add(new("error", "progress-mask", "Captured mask 15 progression bits disagree with awards."));
        if (!JsonOutput.Equal(save.Semantic()["slot.mask_16"], normalized.Semantic()["slot.mask_16"]))
            issues.Add(new("error", "unlock-mask", "Captured Ripper Roo unlock bits disagree with their prerequisites."));
        foreach (string name in new[] { "farthest_progression_index", "active_progression_index" })
            if (!JsonOutput.Equal(save.Get(name)!.Value, normalized.Get(name)!.Value))
                issues.Add(new("error", "progress-header", "Progression indices disagree with the captured early progression state."));
        foreach (var f in normalized.Graph.Fields.Where(f => f.Key.StartsWith("slot.", StringComparison.Ordinal) &&
            (f.Key.Contains("count", StringComparison.Ordinal) || f.Key.Contains("relic_", StringComparison.Ordinal) || f.Key.Contains("token_", StringComparison.Ordinal))))
            if (!JsonOutput.Equal(f.Value, save.Graph.Fields.Single(x => x.Key == f.Key).Value)) issues.Add(new("error", "summary", $"{f.Key} does not match awards.", f.Key));
        foreach (string name in new[] { "trophy_Count", "relic_count", "ctr_token_count", "boss_key_count" })
            if (!JsonOutput.Equal(save.Get(name)!.Value, normalized.Get(name)!.Value)) issues.Add(new("error", "summary", $"header.{name} does not match awards.", "header." + name));
        foreach (var t in save.Graph.Tracks.Where(t => t.Definition.HasRelic))
            if ((t.Flags & AdventureTracks.RelicMask) is not (0 or 0x10 or 0x30 or 0x38)) issues.Add(new("error", "relic-tier", $"{t.Definition.Name}: noncanonical relic bits.", t.Definition.Id));
        foreach (string c in Colours)
            if (Convert.ToInt32(save.Graph.Fields.Single(f => f.Key == "slot.gem_" + c).Value, CultureInfo.InvariantCulture) is not (0 or 1))
                issues.Add(new("error", "gem-value", $"slot.gem_{c} must be 0 or 1.", "slot.gem_" + c));
        issues.Add(new("warning", "unverified", "Ripper Roo is capture-confirmed; other hub bosses remain inferred. Oxide victories use separate slot booleans; track collectible flags are preserved."));
        if (Keys(save) < 4 && (save.Graph.Tracks.Single(t => t.Definition.Id == "t154_oxide_station").Flags & (1u << 18)) != 0)
            issues.Add(new("warning", "oxide", "Oxide-star collectible bit 18 is present with fewer than 4 keys; preserved independently of the victory booleans."));
        issues.Add(new("warning", "progression", "Captured early states and Roo unlock group are synchronized. Other bits, advanced indices and word 17 history remain unresolved; complete door/regression safety is unverified."));
        return issues;
    }
    private static Save Write(Save save, State state, bool edited = false)
    {
        byte[] bytes = save.Serialize();
        void Put(string key, int value)
        {
            var header = save.Get(key);
            int? offset = save.Graph.Fields.FirstOrDefault(f => f.Key == key)?.Offset ?? header?.Offset;
            if (offset is int o)
            {
                if (header?.Tag == 1) BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(o, 4), value);
                else BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(o, 4), value);
            }
        }
        foreach (var (key, value) in state.Values) save.Graph.Set(bytes, key, value);
        var tracks = save.Graph.Tracks.Select(t => t with { Flags = Binary.U32(bytes, t.Offset) }).ToArray();
        var totals = AdventureTotals.From(tracks);
        Put("slot.trophy_count", totals.Trophies); Put("trophy_Count", totals.Trophies);
        Put("slot.relic_sapphire", totals.Sapphire); Put("slot.relic_gold", totals.Gold); Put("slot.relic_platinum", totals.Platinum);
        Put("relic_count", totals.Relics); Put("ctr_token_count", totals.TokenCount);
        foreach (var (c, count) in totals.Tokens) { Put("slot.token_" + c, count); Put("header.token_" + c, count); }
        int keys = tracks.Count(t => t.Boss); Put("slot.boss_key_count", keys); Put("boss_key_count", keys);
        int gems = Colours.Count(c => state.On("slot.gem_" + c));
        // Plausible menu estimate. Game recomputes this; no invented Oxide victory.
        int percent = 2 * totals.Trophies + keys + 2 * totals.TokenCount + totals.Relics + gems;
        if (totals.Trophies == 16 && keys == 4 && gems == 5 && totals.TokenCount == 20 && totals.Relics == 18)
            percent = state.On("slot.oxide_second") && totals.Gold + totals.Platinum == 18 ? 101 : 100;
        Put("percent_complete", percent);
        // Bits 2/3 cannot yet be separated: the capture groups three races.
        // Replay their joint state only at the observed 0/1/4-trophy boundaries.
        int oldMask = unchecked((int)Convert.ToUInt32(save.Graph.Fields.Single(f => f.Key == "slot.mask_15").Value, CultureInfo.InvariantCulture));
        bool crash = tracks.Single(t => t.Definition.Id == "t111_crash_cove").Trophy;
        var initial = tracks.Where(t => t.Definition.HasTrophy).Take(4).ToArray();
        bool four = initial.All(t => t.Trophy), roo = initial[1].Boss;
        int mask = (oldMask & ~2) | (crash ? 2 : 0);
        if (four) mask |= 0xC;
        else if (totals.Trophies == (crash ? 1 : 0)) mask &= ~0xC;
        Put("slot.mask_15", mask);
        int doors = unchecked((int)Convert.ToUInt32(save.Graph.Fields.Single(f => f.Key == "slot.mask_16").Value, CultureInfo.InvariantCulture));
        Put("slot.mask_16", (doors & ~0x2082) | (four ? 2 : 0) | (roo ? 0x2080 : 0));
        bool early = totals.Trophies == initial.Count(t => t.Trophy) && keys <= 1 && totals.Relics == 0 && totals.TokenCount == 0 && gems == 0;
        int? capturedCursor = early ? totals.Trophies switch
        {
            0 => -1,
            1 when crash => 0,
            4 when four => roo ? 5 : 3,
            _ => null
        } : null;
        // Word 17 is history-like (meaning unresolved), not a count. Do not canonicalize an existing save's
        // cursor merely because its awards happen to match a captured state.
        if (edited && capturedCursor is int cursor) Put("slot.unknown_17", cursor);
        if (totals.Trophies == 0 && keys == 0)
        {
            Put("farthest_progression_index", 0); Put("active_progression_index", 0);
        }
        else if (early)
        {
            // 2/3 are inferred from track order; 0/1/4 are capture-confirmed.
            int index = Enumerable.Range(0, 4).Where(i => initial[i].Trophy).Select(i => i + 1).DefaultIfEmpty(0).Max();
            Put("farthest_progression_index", index); Put("active_progression_index", index);
        }
        // Previous level index/name are travel history, preserved together.
        return Save.Parse(bytes);
    }
}
