using System.Globalization;
using CtrNfSave;

namespace CtrNfSave.Tests;

internal static class Program
{
    private static int failures;
    private static int passed;
    private static Save Global() => Decode(SyntheticSaveFactory.Global());
    private static Save Adventure() => Decode(SyntheticSaveFactory.Adventure());
    private static Save Decode(byte[] bytes)
    {
        var save = Save.Parse(bytes);
        Assert(save.Graph.Decoded && save.Graph.Warnings.Count == 0, string.Join("; ", save.Graph.Warnings));
        return save;
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is SaveFormatException or IOException or ArgumentException) { return; }
        throw new InvalidOperationException("An invalid operation was accepted.");
    }
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + ex); }
    }
    private static void OnlyRange(byte[] original, byte[] edited, int offset, int size)
    {
        Assert(original.AsSpan(0, offset).SequenceEqual(edited.AsSpan(0, offset)) && original.AsSpan(offset + size).SequenceEqual(edited.AsSpan(offset + size)), "Unrelated bytes changed.");
    }
    private static int Main()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Test("synthetic global round-trip", () =>
        {
            byte[] raw = SyntheticSaveFactory.Global(); var save = Decode(raw); GlobalProfile.Require(save);
            Assert(raw.AsSpan().SequenceEqual(save.Serialize()), "Round-trip changed bytes.");
            Assert((int)save.Semantic()["global.wumpa_coins"]! == 838 && GlobalUnlocks.Owned(save).Contains((ushort)65000), "Fixture state missing.");
        });
        Test("synthetic Adventure round-trip", () =>
        {
            byte[] raw = SyntheticSaveFactory.Adventure(); var save = Decode(raw); save.RequireAdventureSlot();
            Assert(raw.AsSpan().SequenceEqual(save.Serialize()) && save.Graph.Tracks.Count == 22, "Adventure round-trip failed.");
        });
        Test("parser owns its input buffer", () =>
        {
            byte[] raw = SyntheticSaveFactory.Global(); var save = Decode(raw); raw[0] ^= 0xFF;
            Assert(!raw.AsSpan().SequenceEqual(save.Serialize()), "Caller can mutate parsed save.");
            byte[] exported = save.Serialize(); exported[1] ^= 0xFF;
            Assert(!exported.AsSpan().SequenceEqual(save.Serialize()), "Caller can mutate serialized buffer.");
        });
        Test("malformed and unsupported inputs rejected", () =>
        {
            Reject(() => Save.Parse(new byte[20])); Reject(() => Save.Parse(new byte[153600]));
            byte[] raw = SyntheticSaveFactory.Global(); Array.Fill(raw, (byte)0xFF, 0x34, 4); Reject(() => Save.Parse(raw));
            Reject(() => Global().RequireAdventureSlot()); Reject(() => GlobalProfile.Require(Adventure()));
        });
        Test("local marketplace growth and byte-identical undo", () =>
        {
            var save = Global(); byte[] raw = save.Serialize(); var added = save.Set(GlobalUnlocks.ItemKey(926), "true");
            Assert(GlobalUnlocks.Owned(added).SequenceEqual(new ushort[] { 618, 65000, 926 }), "Existing or unknown ID lost.");
            Assert(added.Serialize().Length == raw.Length && added.Set(GlobalUnlocks.ItemKey(926), "false").Serialize().AsSpan().SequenceEqual(raw), "Growth/shrink failed to restore original.");
            Reject(() => save.Set("global.marketplace.item_9999", "true"));
        });
        Test("named character flag changes only its low byte", () =>
        {
            var save = Global(); var field = save.Graph.Fields.Single(f => f.Key == "global.character.penta_penguin");
            var edited = save.Set(field.Key, "true"); OnlyRange(save.Serialize(), edited.Serialize(), field.Offset, 1);
            Assert(edited.Semantic()[field.Key] is true, "Penta flag missing.");
        });
        Test("Champion shared reward and alternate ownership", () =>
        {
            var save = Global(); var trio = CustomizationUnlocks.Items.Where(i => i.Condition == "COctaneChampionKartUnlockable").ToArray();
            var kart = trio.Single(i => i.Type == "CRacerKartData"); var edited = save.Set(kart.Key, "true");
            Assert(trio.Length == 3 && trio.All(i => edited.Semantic()[i.Key] is true), "Champion trio disagrees.");
            Assert(GlobalUnlocks.Owned(edited).SequenceEqual(GlobalUnlocks.Owned(save)), "Reward fabricated purchases.");
            var purchase = save.Set(GlobalUnlocks.ItemKey(411), "true");
            Assert(purchase.Semantic()[kart.Key] is true && purchase.Set(kart.Key, "false").Semantic()[kart.Key] is false, "Alternate unlock route not handled.");
        });
        Test("time-attack reward bytes isolated and reversible", () =>
        {
            var save = Global();
            foreach (var reward in TimeTrialRewards.All)
            {
                var field = save.Graph.Fields.Single(f => f.Key == reward.Key); var edited = save.Set(reward.Key, "true");
                OnlyRange(save.Serialize(), edited.Serialize(), field.Offset, 1);
                Assert(edited.Set(reward.Key, "false").Serialize().AsSpan().SequenceEqual(save.Serialize()), "Reward undo differs.");
            }
        });
        Test("win-based skins share thresholds and preserve records", () =>
        {
            var save = Global(); var skin = SkinUnlocks.Skins.Single(s => s.Driver == "DriverNTropy" && s.Wins == 25);
            var edited = save.Set(skin.Key, "true");
            Assert(SkinUnlocks.Skins.Where(s => s.Driver == "DriverNTropy" && s.Wins is not null).All(s => edited.Semantic()[s.Key] is true), "Shared win thresholds disagree.");
            Assert(TimeAttack.Records(save).SequenceEqual(TimeAttack.Records(edited)), "Skin edit changed records.");
            var ten = SkinUnlocks.Skins.Single(s => s.Driver == "DriverNTropy" && s.Wins == 10);
            Assert(edited.Set(ten.Key, "false").Semantic()[skin.Key] is false, "Threshold removal did not cascade.");
        });
        Test("full unlock/lock preserves base and unrelated data", () =>
        {
            var save = Global(); byte[] original = save.Serialize();
            var keys = UnlockBatch.Keys(save, UnlockScope.All); var unlocked = UnlockBatch.Set(save, keys, true);
            Assert(keys.Length > 500 && keys.All(k => unlocked.Semantic()[k] is true), "Known grants incomplete.");
            var locked = UnlockBatch.Set(unlocked, UnlockBatch.Keys(unlocked, UnlockScope.All), false);
            var off = locked.Semantic(); Assert(keys.All(k => off[k] is false), "Known locks incomplete.");
            Assert(SkinUnlocks.Skins.Where(s => s.Condition is null).All(s => off[s.Key] is true), "Base skin locked.");
            Assert(off[GlobalProfile.CharacterKey(5)] is true && off[GlobalProfile.CharacterKey(511)] is true, "Unknown challenge bits changed.");
            Assert(GlobalUnlocks.Owned(locked).Contains((ushort)65000), "Unknown inventory ID lost.");
            Assert(off["global.wumpa_coins"]!.Equals(838) && off["global.golden_egg.count"]!.Equals(5), "Coins or eggs changed.");
            Assert(TimeAttack.Ghosts(save).SequenceEqual(TimeAttack.Ghosts(locked)) && TimeAttack.Records(save).SequenceEqual(TimeAttack.Records(locked)), "Time attack changed.");
            Assert(original.AsSpan().SequenceEqual(save.Serialize()), "Preview mutated source.");
            Assert(UnlockBatch.Set(locked, UnlockBatch.Keys(locked, UnlockScope.All), false).Serialize().AsSpan().SequenceEqual(locked.Serialize()), "Lock not idempotent.");
            Reject(() => UnlockBatch.Set(save, ["global.customization.missing"], true));
        });
        Test("ghost progression leaves the replay unchanged", () =>
        {
            var save = Global(); var ghost = TimeAttack.Ghosts(save).Single(); var field = save.Graph.Fields.Single(f => f.Key == ghost.Key);
            var edited = save.Set(ghost.Key, "4"); OnlyRange(save.Serialize(), edited.Serialize(), field.Offset, 4);
            Assert(TimeAttack.Ghosts(edited).Single().PlayerSeconds == ghost.PlayerSeconds, "Stored ghost time changed.");
            Reject(() => save.Set(ghost.Key, "5")); Reject(() => save.Set("global.ghost.absent.level", "4"));
        });
        Test("time formatting, ranking and invalid values", () =>
        {
            var save = Global(); var lap = TimeAttack.Records(save).Single(r => r.Category == "best_lap");
            foreach (string value in new[] { "1:19.61", "1:19:61", "79.61" })
                Assert(Math.Abs((float)save.Set(lap.Key, value).Semantic()[lap.Key]! - 79.61f) < 0.0001, "Formatted time wrong.");
            foreach (string value in new[] { "NaN", "Infinity", "0", "-1", "1:60.00", "1:10:100", "1:2:3:4" }) Reject(() => save.Set(lap.Key, value));
            var first = TimeAttack.Records(save).Single(r => r.Category == "race" && r.Rank == 1);
            Reject(() => save.Set(first.Key, "120")); Assert(save.Set(first.Key, "90").Semantic()[first.Key]!.Equals(90f), "Valid ranking edit rejected.");
        });
        Test("Adventure prerequisites and removal cascades", () =>
        {
            var save = Adventure(); var won = AdventureRules.Edit(save, "track.t112_roos_tubes.boss", "true").Save;
            Assert(AdventureRules.Keys(won) == 1 && won.Graph.Tracks.Take(4).All(t => t.Trophy), "Boss prerequisites missing.");
            Assert(won.Graph.Tracks.All(t => (t.OtherFlags & 0x80000000) != 0), "Unknown track bits lost.");
            var reset = AdventureRules.Edit(won, "track.t111_crash_cove.trophy", "false").Save;
            Assert(AdventureRules.Keys(reset) == 0, "Dependent boss not removed.");
            Assert(!AdventureRules.Validate(reset).Any(i => i.Severity == "error"), "Cascade produced an inconsistent slot.");
        });
        Test("Oxide victories use separate flags", () =>
        {
            var save = AdventureRules.Edit(Adventure(), "slot.oxide_second", "true").Save;
            Assert(save.Semantic()["slot.oxide_first"] is true && save.Semantic()["slot.oxide_second"] is true && AdventureRules.Keys(save) == 4, "Rematch prerequisites incomplete.");
            Assert(AdventureRules.Colours.All(c => AdventureRules.Gem(save, c)), "Rematch gems missing.");
        });
        Test("difficulty synchronizes slot and header", () =>
        {
            var easy = Adventure().Set("slot.adventure_difficulty", "0");
            Assert(easy.Get("adventure_difficulty")!.Value.Equals(0) && easy.Semantic()["slot.adventure_difficulty"]!.Equals(0), "Difficulty mismatch.");
            Reject(() => easy.Set("slot.adventure_difficulty", "3"));
        });
        Test("catalogue identities and keys are consistent", () =>
        {
            Assert(GlobalUnlocks.Drivers.Count == 56 && SkinUnlocks.Skins.Count == 392 && GlobalUnlocks.Items.Count == 856, "Baseline catalogue changed; review expected counts.");
            Assert(GlobalUnlocks.Items.Single(i => i.Id == 618).NameEn == "Koala Kong", "Anchored item ID changed.");
            Assert(SkinUnlocks.Skins.Select(s => s.Key).Distinct().Count() == SkinUnlocks.Skins.Count, "Duplicate skin keys.");
            Assert(CustomizationUnlocks.Items.Select(i => i.Key).Distinct().Count() == CustomizationUnlocks.Items.Count, "Duplicate customization keys.");
        });
        Test("file writer backups, refusal and external changes", () =>
        {
            string root = Path.Combine(Environment.CurrentDirectory, "artifacts", "tests", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                string path = Path.Combine(root, "copy.dat"); FileWriter.New(path, [1, 2, 3]); Reject(() => FileWriter.New(path, [9]));
                FileWriter.InPlace(path, [1, 2, 3], [4, 5, 6]);
                Assert(File.ReadAllBytes(path + ".bak").AsSpan().SequenceEqual(new byte[] { 1, 2, 3 }), "Backup differs.");
                Reject(() => FileWriter.InPlace(path, [0], [7], true));
                string samples = Path.Combine(root, "samples"); Directory.CreateDirectory(samples);
                Reject(() => FileWriter.ProtectSamples(Path.Combine(samples, "output.sav"), samples));
            }
            finally { Directory.Delete(root, true); }
        });
        Console.WriteLine($"{passed} passed, {failures} failed — synthetic inputs only.");
        return failures == 0 ? 0 : 1;
    }
}
