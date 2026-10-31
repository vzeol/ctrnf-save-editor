using System.Globalization;
using CtrNfSave;

namespace CtrNfSave.Tests;

internal static partial class Program
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
        catch (Exception ex) when (ex is SaveFormatException or IOException or InvalidDataException or ArgumentException) { return; }
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
        UpdateTests();
        GarageTests();
        Test("Garage accepts case-folded namespace without rewriting it", () =>
        {
            var save = Global(); var section = save.Graph.Sections.Single(s => s.Name == "graph_names");
            byte[] raw = save.Serialize();
            int namespaceAt = section.Offset + 12 + section.Count * 4;
            Assert(raw[namespaceAt] == (byte)'S', "Synthetic namespace location changed.");
            raw[namespaceAt] = (byte)'s'; save = Decode(raw);
            var current = KartLoadout.Read(save);
            var edited = KartLoadout.Apply(save, current.Preset with { Paint = 2 });
            Assert(edited.Graph.Names[KartLoadout.SystemDataNamespace] == "systemData", "Namespace spelling rewritten.");
            OnlyRange(raw, edited.Serialize(), current.Record.WordOffset(6), 4);
            Assert(KartLoadout.Read(edited).Preset.Paint == 2, "Case-folded namespace Garage edit failed.");
        });
        Test("fresh global without slot summary or name table", () =>
        {
            byte[] raw = SyntheticSaveFactory.Global(fresh: true);
            var save = Decode(raw); GlobalProfile.Require(save);
            Assert(save.Kind == "global" && save.Get("most_recent_save_slot") is null &&
                save.Graph.Names.Count == 0 && !save.Graph.Sections.Any(s => s.Name == "graph_names"), "Fresh envelope misidentified.");
            Assert(save.Serialize().AsSpan().SequenceEqual(raw), "Fresh round-trip changed bytes.");
            var coins = save.Graph.Fields.Single(f => f.Key == "global.wumpa_coins");
            var edited = save.Set(coins.Key, "1500");
            OnlyRange(raw, edited.Serialize(), coins.Offset, 4);
            GlobalProfile.Require(edited);
            Assert((int)edited.Semantic()[coins.Key]! == 1500, "Fresh coin edit failed.");
            Reject(() => KartLoadout.Read(save)); // No names: preserve unresolved handles.
        });
        Test("fresh global marketplace growth preserves format and unknown purchases", () =>
        {
            var save = Decode(SyntheticSaveFactory.Global(fresh: true));
            var edited = save.Set(GlobalUnlocks.ItemKey(926), "true");
            GlobalProfile.Require(edited);
            Assert(edited.Header[11] == 0xB000000C && edited.Get("most_recent_save_slot") is null &&
                edited.Graph.Names.Count == 0 && GlobalUnlocks.Owned(edited).SequenceEqual(new ushort[] { 618, 65000, 926 }), "Fresh marketplace rebuild lost state.");
            Assert(edited.Set(GlobalUnlocks.ItemKey(926), "false").Serialize().AsSpan().SequenceEqual(save.Serialize()), "Fresh marketplace undo changed bytes.");
            var unlocked = save.Set("global.unlock_all", "true");
            GlobalProfile.Require(unlocked);
            Assert(GlobalUnlocks.Owned(unlocked).Contains((ushort)65000), "Bulk edit lost unknown purchase.");
        });
        Test("fresh global identification requires valid structure", () =>
        {
            byte[] raw = SyntheticSaveFactory.Global(fresh: true);
            // A size and version key alone do not establish the save kind.
            var parsed = Decode(raw);
            byte[] invalid = (byte[])raw.Clone();
            Array.Clear(invalid, parsed.Graph.Sections.Single(s => s.Name == "graph_strings").Offset, 4);
            Reject(() => Save.Parse(invalid));
            invalid = (byte[])raw.Clone(); invalid[0x2F] = 0xB1;
            Reject(() => Save.Parse(invalid));
            // Reinterpreting pools as a name table (or vice versa) must not enable editing.
            invalid = (byte[])raw.Clone(); invalid[0x2F] = 0xB4;
            Reject(() => Save.Parse(invalid));
            invalid = SyntheticSaveFactory.Global(); invalid[0x2F] = 0xB0;
            Reject(() => GlobalProfile.Require(Save.Parse(invalid)));
        });
        Test("empty marketplace allocates without changing existing IDs or data", () =>
        {
            foreach (bool fresh in new[] { false, true })
            {
                var save = Decode(SyntheticSaveFactory.Global(fresh, emptyMarketplace: true));
                Assert(GlobalUnlocks.Owned(save).Count == 0, "Empty marketplace not recognized.");
                byte[] raw = save.Serialize();
                Assert(save.Set(GlobalUnlocks.ItemKey(926), "false").Serialize().AsSpan().SequenceEqual(raw), "Empty removal allocated memory.");
                var edited = save.Set(GlobalUnlocks.ItemKey(926), "true");
                Assert(GlobalUnlocks.Owned(edited).SequenceEqual(new ushort[] { 926 }) && edited.Header[11] == save.Header[11], "First purchase allocation failed.");
                foreach (var obj in save.Graph.Objects.Where(o => o.ClassName != "igUnsignedShortList"))
                    Assert(edited.Graph.Objects.Single(o => o.Id == obj.Id).Words.SequenceEqual(obj.Words), "Existing object/reference changed.");
                foreach (var block in save.Graph.Memories.Values)
                {
                    var moved = edited.Graph.Memories[block.Id];
                    Assert(raw.AsSpan(block.Offset, block.Count * block.Stride).SequenceEqual(edited.Serialize().AsSpan(moved.Offset, moved.Count * moved.Stride)), "Existing memory changed.");
                }
                var removed = edited.Set(GlobalUnlocks.ItemKey(926), "false");
                Assert(GlobalUnlocks.Owned(removed).Count == 0, "Allocated marketplace cannot be emptied.");
                Assert(GlobalUnlocks.Owned(removed.Set(GlobalUnlocks.ItemKey(618), "true")).SequenceEqual(new ushort[] { 618 }), "Empty allocated block cannot grow again.");
                var all = save.Set("global.unlock_all", "true");
                var unlockedValues = all.Semantic();
                Assert(GlobalUnlocks.Owned(all).Count > 0 && UnlockBatch.Keys(save, UnlockScope.All).All(key => unlockedValues[key] is true), "Fresh bulk unlock omitted supported conditions.");
                raw[^1] = 1;
                Reject(() => Decode(raw).Set(GlobalUnlocks.ItemKey(926), "true"));
            }
        });
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
        Test("gem awards write their gem cup track bits", () =>
        {
            static bool Cup(Save s, string colour) => s.Graph.Tracks.Where(t => AdventureTracks.GemCups[colour].Tracks.Contains(t.Definition.Id)).All(t => (t.Flags >> AdventureTracks.GemCups[colour].Bit & 1) != 0);
            var red = AdventureRules.Edit(Adventure(), "slot.gem_red", "1").Save;
            Assert(Cup(red, "red") && !Cup(red, "blue") && !AdventureRules.Validate(red).Any(i => i.Code == "gem-cup"), "Red cup bits not written.");
            Assert(red.Graph.Tracks.All(t => (t.OtherFlags & 0x80000000) != 0), "Unknown track bits lost.");
            var none = AdventureRules.Edit(red, "track.t111_crash_cove.token", "false").Save;
            Assert(!AdventureRules.Gem(none, "red") && !Cup(none, "red"), "Removed gem kept its cup bits.");
            var wordOnly = AdventureRules.Edit(red, "slot.gem_red", "0").Save.Set("slot.gem_red", "1");
            Assert(AdventureRules.Validate(wordOnly).Any(i => i.Code == "gem-cup"), "Gem without cup bits not reported.");
            Assert(Cup(AdventureRules.Normalize(wordOnly), "red"), "Normalize did not repair cup bits.");
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
