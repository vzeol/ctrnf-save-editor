using System.Text.Json;

namespace CtrNfSave;

public sealed record CustomizationEntry(string Handle, string Type, string NameEn, string NameFr, string Condition, int[] MarketplaceIds, int? ChallengeBit)
{
    public string Key => "global.customization." + Handle;
    public bool UsesMarketplace => Condition is "COctanePitStopUnlockable" or "COctaneUnlockable" or "LocalMarketplaceOwnership";
    public string? RewardKey => TimeTrialRewards.ConditionKey(Condition);
    public bool CanEdit => ChallengeBit is >= 0 and < 512 || RewardKey is not null || Condition == "COctaneBeatNefariousTimesUnlockable" || UsesMarketplace && MarketplaceIds.Length > 0;
}

public static class CustomizationUnlocks
{
    public static IReadOnlyList<CustomizationEntry> Items { get; } = Load();
    private static IReadOnlyList<CustomizationEntry> Load()
    {
        using var stream = typeof(CustomizationUnlocks).Assembly.GetManifestResourceStream("CtrNfSave.Core.Data.customization-rewards.json")!;
        var authored = JsonSerializer.Deserialize<CustomizationEntry[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var covered = authored.SelectMany(i => i.MarketplaceIds).ToHashSet();
        var local = GlobalUnlocks.Items.Where(i => i.Type is not ("CRacerDriverData" or "CDriverSkinData") && !covered.Contains(i.Id))
            .Select(i => new CustomizationEntry("LocalItem_" + i.Id, i.Type, i.NameEn, i.NameFr, "LocalMarketplaceOwnership", [i.Id], null));
        return Array.AsReadOnly(authored.Concat(local).ToArray());
    }
    internal static void Interpret(Graph graph)
    {
        if (!graph.Objects.Any(o => o.ClassName == "COctaneGlobalSaveData")) return;
        var values = graph.Fields.ToDictionary(f => f.Key, f => f.Value);
        foreach (var item in Items)
        {
            bool supported = item.CanEdit && (item.RewardKey is null || values.ContainsKey(item.RewardKey));
            object state = !supported ? "unknown" : item.RewardKey is string reward ? values.GetValueOrDefault(reward) is true ||
                item.Condition == "COctaneChampionKartUnlockable" && item.MarketplaceIds.Any(id => values.GetValueOrDefault(GlobalUnlocks.ItemKey(id)) is true) :
                item.ChallengeBit is int bit ? values.GetValueOrDefault(GlobalProfile.CharacterKey(bit)) is true :
                item.Condition == "COctaneBeatNefariousTimesUnlockable" ? values.GetValueOrDefault("global.character.n_tropy") is true :
                item.MarketplaceIds.Any(id => values.GetValueOrDefault(GlobalUnlocks.ItemKey(id)) is true);
            graph.Fields.Add(new(item.Key, supported ? "customization-unlock" : "readonly", 0, 0, state,
                item.NameEn + "; " + item.Condition + "; shared reward conditions can affect characters and other cosmetics."));
        }
    }
    public static Save SetMany(Save save, IEnumerable<string> keys, bool enable)
        => UnlockBatch.Set(save, keys, enable);
}
