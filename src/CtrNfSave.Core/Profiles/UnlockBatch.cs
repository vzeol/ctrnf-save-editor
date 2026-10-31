using System.Globalization;

namespace CtrNfSave;

public enum UnlockScope { All, Characters, Skins, Customization, PitStop }
public sealed record UnlockChange(string Key, string NameEn, string NameFr, object Before, object After);

/// <summary>One in-memory transaction over named unlock conditions; base/unknown content is excluded.</summary>
public static class UnlockBatch
{
    private static bool SkinSupported(Save save, SkinEntry skin, IReadOnlyDictionary<string, object?> values) => skin.Condition is not null && values.GetValueOrDefault(skin.Key) is bool &&
        (skin.ChallengeBit is >= 0 and < 512 || skin.Wins is not null || skin.Condition == "COctaneBeatOxideTimesUnlockable" ||
            skin.MarketplaceIds.Length == 1 && values.ContainsKey(GlobalUnlocks.ItemKey(skin.MarketplaceIds[0])));

    public static string[] Keys(Save save, UnlockScope scope, string? driver = null)
    {
        GlobalProfile.Require(save); var values = save.Semantic(); var result = new List<string>();
        if (scope == UnlockScope.PitStop)
            return GlobalUnlocks.Items.Where(i => i.UnlockType == "COctanePitStopUnlockable" &&
                values.GetValueOrDefault(GlobalUnlocks.ItemKey(i.Id)) is bool)
                .Select(i => GlobalUnlocks.ItemKey(i.Id)).ToArray();
        if (scope is UnlockScope.All or UnlockScope.Characters)
            result.AddRange(GlobalUnlocks.Drivers.Where(d => d.Key is not null && values.GetValueOrDefault(d.Key) is bool).Select(d => d.Key!));
        if (scope is UnlockScope.All or UnlockScope.Skins)
            result.AddRange(SkinUnlocks.Skins.Where(s => (driver is null || s.Driver == driver) && SkinSupported(save, s, values)).Select(s => s.Key));
        if (scope is UnlockScope.All or UnlockScope.Customization)
            result.AddRange(CustomizationUnlocks.Items.Where(i => i.CanEdit && values.GetValueOrDefault(i.Key) is bool &&
                (!i.UsesMarketplace || i.MarketplaceIds.All(id => values.ContainsKey(GlobalUnlocks.ItemKey(id))))).Select(i => i.Key));
        return result.Distinct().ToArray();
    }
    public static Save Set(Save save, IEnumerable<string> keys, bool enable)
    {
        GlobalProfile.Require(save);
        var values = save.Semantic();
        var drivers = GlobalUnlocks.Drivers.Where(d => d.Key is not null).ToDictionary(d => d.Key!);
        var skins = SkinUnlocks.Skins.ToDictionary(s => s.Key);
        var items = CustomizationUnlocks.Items.ToDictionary(i => i.Key);
        var purchases = GlobalUnlocks.Items.ToDictionary(i => GlobalUnlocks.ItemKey(i.Id));
        var market = new HashSet<int>(); var bits = new HashSet<int>(); var words = new HashSet<int>(); var rewards = new HashSet<string>();
        var wins = new Dictionary<string, int>();
        foreach (string key in keys.Distinct())
        {
            if (purchases.TryGetValue(key, out var purchase))
            {
                if (values.GetValueOrDefault(key) is not bool) throw new SaveFormatException("Local purchase unavailable: " + key);
                market.Add(purchase.Id);
            }
            else if (drivers.TryGetValue(key, out var driver))
            {
                if (values.GetValueOrDefault(key) is not bool) throw new SaveFormatException("Character condition unavailable: " + key);
                if (driver.ChallengeBit is int bit) bits.Add(bit);
                else if (driver.Driver == "DriverPentaPenguin") words.Add(3);
                else if (driver.Driver == "DriverNTropy") words.Add(6);
                else foreach (int id in driver.MarketplaceIds) market.Add(id);
            }
            else if (skins.TryGetValue(key, out var skin))
            {
                if (!SkinSupported(save, skin, values)) throw new SaveFormatException("Default or unsupported skin: " + key);
                if (skin.ChallengeBit is int bit) bits.Add(bit);
                else if (skin.Wins is int threshold)
                {
                    int current = wins.GetValueOrDefault(skin.Driver, Convert.ToInt32(values[SkinUnlocks.WinsKey(skin.Driver)], CultureInfo.InvariantCulture));
                    wins[skin.Driver] = enable ? Math.Max(current, threshold) : Math.Min(current, threshold - 1);
                }
                else if (skin.Condition == "COctaneBeatOxideTimesUnlockable") words.Add(7);
                else foreach (int id in skin.MarketplaceIds) market.Add(id);
            }
            else if (items.TryGetValue(key, out var item))
            {
                if (!item.CanEdit || values.GetValueOrDefault(key) is not bool) throw new SaveFormatException("Unsupported customization condition: " + key);
                if (item.RewardKey is string reward)
                {
                    rewards.Add(reward);
                    // Champion is available through either this reward or an existing local purchase.
                    if (!enable && item.Condition == "COctaneChampionKartUnlockable") foreach (int id in item.MarketplaceIds) market.Add(id);
                }
                else if (item.ChallengeBit is int bit) bits.Add(bit);
                else if (item.Condition == "COctaneBeatNefariousTimesUnlockable") words.Add(6);
                else if (item.UsesMarketplace) foreach (int id in enable ? item.MarketplaceIds.Take(1) : item.MarketplaceIds) market.Add(id);
            }
            else throw new SaveFormatException("Unknown unlock selection: " + key);
        }
        // Resolve structural changes first; all byte offsets are taken from the final graph.
        var changed = market.Count == 0 ? save : GlobalUnlocks.SetMarketplace(save, enable ? market : [], enable ? [] : market);
        foreach (var (driver, count) in wins) changed = SkinUnlocks.SetWins(changed, driver, count);
        var bytes = changed.Serialize(); var fields = changed.Graph.Fields.ToDictionary(f => f.Key);
        foreach (int bit in bits) GlobalProfile.SetCharacter(bytes, fields[GlobalProfile.CharacterKey(bit)], enable ? "true" : "false");
        var root = changed.Graph.Objects.Single(o => o.ClassName == "COctaneGlobalSaveData");
        foreach (int word in words) bytes[root.WordOffset(word)] = enable ? (byte)1 : (byte)0;
        foreach (string key in rewards)
        {
            if (!fields.TryGetValue(key, out var field) || field.Type != "reward-flag") throw new SaveFormatException("Time-trial reward unavailable in this save version.");
            bytes[field.Offset] = enable ? (byte)1 : (byte)0;
        }
        var result = Save.Parse(bytes); GlobalProfile.Require(result); return result;
    }
    public static IReadOnlyList<UnlockChange> Changes(Save before, Save after, UnlockScope scope = UnlockScope.All)
    {
        var a = before.Semantic(); var b = after.Semantic();
        if (scope == UnlockScope.PitStop)
            return GlobalUnlocks.Items.Where(i => i.UnlockType == "COctanePitStopUnlockable")
                .Select(i => (Item: i, Key: GlobalUnlocks.ItemKey(i.Id)))
                .Where(p => a.ContainsKey(p.Key) && b.ContainsKey(p.Key) && !JsonOutput.Equal(a[p.Key], b[p.Key]))
                .Select(p => new UnlockChange(p.Key, p.Item.NameEn, p.Item.NameFr, a[p.Key]!, b[p.Key]!)).ToArray();
        var names = GlobalUnlocks.Drivers.Where(d => d.Key is not null).Select(d => (Key: d.Key!, En: d.NameEn, Fr: d.NameFr))
            .Concat(SkinUnlocks.Skins.Select(s => (Key: s.Key, En: s.DriverEn + " / " + s.NameEn, Fr: s.DriverFr + " / " + s.NameFr)))
            .Concat(CustomizationUnlocks.Items.Select(i => (Key: i.Key, En: i.NameEn, Fr: i.NameFr)))
            .Concat(TimeTrialRewards.All.Select(r => (Key: r.Key, En: r.NameEn, Fr: r.NameFr)))
            .Concat(SkinUnlocks.Skins.Where(s => s.Wins is not null).DistinctBy(s => s.Driver)
                .Select(s => (Key: SkinUnlocks.WinsKey(s.Driver), En: s.DriverEn + " — race wins", Fr: s.DriverFr + " — victoires")))
            .GroupBy(n => n.Key).Select(g => g.First());
        return names.Where(n => a.ContainsKey(n.Key) && b.ContainsKey(n.Key) && !JsonOutput.Equal(a[n.Key], b[n.Key]))
            .Select(n => new UnlockChange(n.Key, n.En, n.Fr, a[n.Key]!, b[n.Key]!)).ToArray();
    }
}
