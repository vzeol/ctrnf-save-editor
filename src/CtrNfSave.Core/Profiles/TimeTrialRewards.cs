namespace CtrNfSave;

public sealed record TimeTrialReward(string Key, int PatchOffset, string NameEn, string NameFr);

/// <summary>Native PatchTU1 bytes: CGameState offset minus its 12-byte object header.</summary>
public static class TimeTrialRewards
{
    public const string AllOxide = "global.reward.all_oxide_times";
    public const string Velo = "global.reward.velo_times";
    public const string Developer = "global.reward.developer_times";
    public static IReadOnlyList<TimeTrialReward> All { get; } = Array.AsReadOnly(new[]
    {
        new TimeTrialReward(AllOxide, 0xD00, "All Oxide times (CTR, CNK & bonus)", "Tous les temps d’Oxide (CTR, CNK et bonus)"),
        new TimeTrialReward(Velo, 0xD01, "All Velo times", "Tous les temps de Velo"),
        new TimeTrialReward(Developer, 0xCF4, "All developer times", "Tous les temps des développeurs")
    });
    public static string? ConditionKey(string condition) => condition switch
    {
        "COctaneChampionKartUnlockable" or "COctaneBeatAllOxideTimesUnlockable" => AllOxide,
        "COctaneBeatVeloTimesUnlockable" => Velo,
        "COctaneBeatDevTimesUnlockable" => Developer,
        _ => null
    };
    internal static void Interpret(Graph graph, byte[] bytes)
    {
        var lists = graph.Objects.Where(o => o.ClassName == "igCharList").ToArray();
        if (!graph.Objects.Any(o => o.ClassName == "COctaneGlobalSaveData") || !graph.Strings.Contains("PatchTU1") ||
            !graph.Strings.Contains("OctanePatchSaveGameData") || lists.Length != 1) return;
        var list = lists[0];
        if (list.Words.Length != 3 || list.Words[0] != 3352 || list.Words[1] != 3352 ||
            !graph.Memories.TryGetValue((int)list.Words[2], out var memory) || memory.Count != 3352 || memory.Stride != 1 || memory.Meta != "igCharMetaField") return;
        foreach (var reward in All)
            graph.Fields.Add(new(reward.Key, "reward-flag", memory.Offset + reward.PatchOffset, 1,
                bytes[memory.Offset + reward.PatchOffset] != 0, reward.NameEn + "; shared reward only; no race record or ghost trajectory is created."));
    }
    public static Save Set(Save save, string key, bool enabled)
    {
        GlobalProfile.Require(save);
        var field = save.Graph.Fields.SingleOrDefault(f => f.Key == key && f.Type == "reward-flag")
            ?? throw new SaveFormatException("Time-trial reward unavailable in this save version.");
        var bytes = save.Serialize(); bytes[field.Offset] = enabled ? (byte)1 : (byte)0;
        var result = Save.Parse(bytes); GlobalProfile.Require(result); return result;
    }
}
