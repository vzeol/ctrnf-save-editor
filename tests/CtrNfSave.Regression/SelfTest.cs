using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CtrNfSave;

internal static class SelfTest
{
    public static int Run(string samplesDir, string? artifacts = null)
    {
        if (!Directory.Exists(samplesDir)) throw new SaveFormatException("Samples directory not found: " + samplesDir);
        string[] files = Directory.GetFiles(samplesDir, "*.sav", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles("target/eggs", "global*.sav"))
            .Concat(Directory.GetFiles("target/mine", "global*.sav"))
            .Concat(new[] { "target/fresh/step0_new.sav", "target/fresh/step1_crashcove.sav", "target/fresh/step2_before_roo.sav", "target/fresh/step3_after_roo.sav" }).Distinct().Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0) throw new SaveFormatException("No .sav samples found; refusing a vacuous selftest.");
        string outputDir = Path.GetFullPath(Path.Combine(artifacts ?? "artifacts", "selftest", Guid.NewGuid().ToString("N")));
        FileWriter.ProtectSamples(outputDir, samplesDir);
        Directory.CreateDirectory(outputDir);
        int passed = 0, failed = 0;
        if (Directory.Exists("target/garage"))
        {
            try { TestGarage(outputDir); Console.WriteLine("PASS Garage: captured loadout apply, name growth and byte preservation"); passed++; }
            catch (Exception e) { Console.WriteLine("FAIL Garage: " + e.Message); failed++; }
        }
        var identities = files.ToDictionary(p => p, p => SHA256.HashData(File.ReadAllBytes(p)));
        foreach (string file in files)
        {
            try
            {
                byte[] original = File.ReadAllBytes(file);
                Save save = Save.Parse(original);
                Check(original.AsSpan().SequenceEqual(save.Serialize()), "parse -> serialize changed bytes");
                Save roundTrip = Save.Parse(save.Serialize());
                Check(Save.Diff(save, roundTrip).Count == 0, "round-trip semantic values changed");
                if (save.Kind == "slot") { TestTracks(save); TestRules(save); }
                else TestGlobal(save);
                string key = save.Kind == "slot" ? "adventure_difficulty" : "most_recent_save_slot";
                var f = save.Get(key) ?? throw new SaveFormatException("Missing test key " + key);
                int old = (int)f.Value, value = old == 0 ? 1 : 0;
                Save changed = save.Set(key, value.ToString(CultureInfo.InvariantCulture));
                if (save.Kind == "global")
                {
                    string exported = Path.Combine(outputDir, "export-" + Path.GetFileName(file));
                    FileWriter.NewWithBackup(exported, original, changed.Serialize());
                    Check(File.ReadAllBytes(exported).AsSpan().SequenceEqual(changed.Serialize()), "global export differs");
                    string backup = Directory.GetFiles(outputDir, Path.GetFileName(exported) + ".*.bak").Single();
                    Check(File.ReadAllBytes(backup).AsSpan().SequenceEqual(original), "global export source backup differs");
                    ExpectFailure(() => FileWriter.NewWithBackup(exported, original, original), "global export replaced existing file");
                }
                string output = Path.Combine(outputDir, Path.GetFileName(file));
                FileWriter.New(output, changed.Serialize());
                Save loaded = Save.Load(output);
                Check((int)loaded.Get(key)!.Value == value, "set then parse lost value");
                using (var dump = JsonDocument.Parse(JsonOutput.Render(loaded.ToJson())))
                    Check(dump.RootElement.GetProperty("header").GetProperty(key).GetProperty("value").GetInt32() == value, "set then JSON dump lost value");
                Check(loaded.DumpText().Contains("header." + key + " = " + value), "set then text dump lost value");
                byte[] changedBytes = loaded.Serialize();
                Check(Enumerable.Range(0, original.Length).All(i => original[i] == changedBytes[i] || i >= f.Offset && i < f.Offset + f.Size), "edit changed unrelated bytes");
                Check(Save.Diff(save, loaded).Count == 1, "edit should change exactly one semantic field");
                Check(original.AsSpan().SequenceEqual(loaded.Set(key, old.ToString(CultureInfo.InvariantCulture)).Serialize()), "undo edit did not restore original");
                foreach (var str in save.Fields.Where(x => x.Tag == 2))
                {
                    Check(save.Set(str.Name, (string)str.Value).Serialize().AsSpan().SequenceEqual(original), "no-op string edit changed bytes");
                    ExpectFailure(() => save.Set(str.Name, (string)str.Value + "X"), "variable-length string accepted");
                }
                ExpectFailure(() => save.Set("missing-key", "0"), "unknown key accepted");
                ExpectFailure(() => save.Set(key, "2147483648"), "overflowing i32 accepted");
                ExpectFailure(() => FileWriter.New(output, original), "existing output was replaced");
                FileWriter.InPlace(output, changedBytes, original);
                Check(File.ReadAllBytes(output + ".bak").AsSpan().SequenceEqual(changedBytes), "backup differs from original input to in-place edit");
                Check(File.ReadAllBytes(output).AsSpan().SequenceEqual(original), "in-place replacement differs");
                ExpectFailure(() => FileWriter.InPlace(output, original, changedBytes), "existing backup was overwritten");
                FileWriter.InPlace(output, original, changedBytes, timestamped: true);
                FileWriter.InPlace(output, changedBytes, original, timestamped: true);
                var timestampedBackups = Directory.GetFiles(outputDir, Path.GetFileName(output) + ".*.bak");
                Check(timestampedBackups.Length == 2, "repeated GUI saves did not keep two timestamped backups");
                Check(timestampedBackups.Any(p => File.ReadAllBytes(p).AsSpan().SequenceEqual(original)) &&
                    timestampedBackups.Any(p => File.ReadAllBytes(p).AsSpan().SequenceEqual(changedBytes)), "timestamped backup contents mismatch");
                ExpectFailure(() => FileWriter.InPlace(output, changedBytes, original, timestamped: true), "externally changed input was overwritten");
                Console.WriteLine($"PASS {Path.GetFileName(file)}: round-trip, set/dump/diff, isolated edit, backup, refusal checks"); passed++;
            }
            catch (Exception e) { Console.WriteLine($"FAIL {Path.GetFileName(file)}: {e.Message}"); failed++; }
        }
        try
        {
            ExpectFailure(() => Save.Parse(new byte[20]), "short file accepted");
            byte[] bad = new byte[153600];
            ExpectFailure(() => Save.Parse(bad), "invalid signature accepted");
            byte[] malformed = File.ReadAllBytes(files[0]);
            Array.Fill(malformed, (byte)0xff, 0x34, 4);
            ExpectFailure(() => Save.Parse(malformed), "invalid header count accepted");
            ExpectFailure(() => FileWriter.ProtectSamples(files[0], samplesDir), "sample path was writable");
            ExpectFailure(() => FileWriter.ProtectSamples(Path.Combine("target", "refused.sav")), "target input directory was writable");
            foreach (string global in files.Where(p => Save.Load(p).Kind == "global"))
            {
                ExpectFailure(() => Save.Load(global).RequireAdventureSlot(), "GUI accepted a global save");
                ExpectFailure(() => AdventureRules.Validate(Save.Load(global)), "validator accepted a global save");
                ExpectFailure(() => AdventureRules.Normalize(Save.Load(global)), "normalizer accepted a global save");
            }
            Check(identities.All(kv => SHA256.HashData(File.ReadAllBytes(kv.Key)).AsSpan().SequenceEqual(kv.Value)), "sample SHA-256 changed");
            var eggBefore = Save.Load("target/eggs/global_before.sav");
            var eggAfter = Save.Load("target/eggs/global_after.sav");
            Check((int)eggBefore.Semantic()["global.golden_egg.count"]! == 1 &&
                (int)eggAfter.Semantic()["global.golden_egg.count"]! == 3, "egg transition must decode 1 to 3");
            var reconstructed = eggBefore.Set("global.golden_egg.bit_2", "true").Set("global.golden_egg.bit_3", "true");
            Check(JsonOutput.Equal(reconstructed.Semantic()["global.golden_egg.bitmask"], eggAfter.Semantic()["global.golden_egg.bitmask"]), "egg reconstruction differs");
            var oldTrials = GlobalProfile.TimeTrials(eggBefore);
            var newTrials = GlobalProfile.TimeTrials(eggAfter);
            Check(newTrials.Count == oldTrials.Count + 1 && oldTrials.All(r => newTrials.Contains(r)), "time trial change must be exactly one new record");
            Check(newTrials.Except(oldTrials).Single() is { Track: "t111_crash_cove", Category: "alternate", Rank: 2 }, "new time trial mapped to wrong track/list");
            var chickenBefore = Save.Load("target/mine/global_015943.sav");
            var chickenAfter = Save.Load("target/mine/global_kingchicken_020306.sav");
            const string chickenKey = "global.character.king_chicken";
            Check(chickenBefore.Semantic()[chickenKey] is false && chickenAfter.Semantic()[chickenKey] is true, "King Chicken capture must decode false to true");
            Check((int)chickenBefore.Semantic()["global.golden_egg.count"]! == 5 && (int)chickenAfter.Semantic()["global.golden_egg.count"]! == 5, "eggs were consumed");
            Check(chickenBefore.Semantic()["global.unknown.patch_3300.bit_6"] is false && chickenAfter.Semantic()["global.unknown.patch_3300.bit_6"] is true, "companion patch flag not decoded");
            var rosterOnly = chickenBefore.Set(chickenKey, "true");
            Check(rosterOnly.Semantic()["global.unknown.patch_3300.bit_6"] is false, "roster edit changed companion patch flag");
            var aRoster = chickenBefore.Graph.Fields.Where(f => f.Type == "character-bit").ToArray();
            Check(aRoster.Count(f => !JsonOutput.Equal(f.Value, chickenAfter.Semantic()[f.Key])) == 1, "capture changed another roster bit");
            var fullRoster = Save.Load(files.Single(p => p.Contains("allPitstop")));
            Check((int)fullRoster.Semantic()["global.character.count"]! == 73, "all-Pit-Stop population must be 73");
            var all = GlobalProfile.CheckAllCharacters(chickenBefore);
            Check(all.Graph.Fields.Where(f => f.Type == "character-bit").All(f => JsonOutput.Equal(f.Value, fullRoster.Semantic()[f.Key])), "check all differs from observed template");
            Console.WriteLine("PASS King Chicken capture: false to true, eggs preserved; 73-bit roster template");
            Console.WriteLine("PASS global eggs: 1 to 3, two-bit reconstruction");
            if (File.Exists("target/mine/global_before_koalakong.sav") && File.Exists("target/mine/global_after_koalakong.sav"))
            {
                var marketBefore = GlobalUnlocks.Owned(Save.Load("target/mine/global_before_koalakong.sav"));
                var marketAfter = GlobalUnlocks.Owned(Save.Load("target/mine/global_after_koalakong.sav"));
                Check(!marketBefore.Contains((ushort)618) && marketAfter.Contains((ushort)618) && marketAfter.Except(marketBefore).SequenceEqual(new ushort[] { 618 }), "Koala Kong live purchase mapping changed");
                Console.WriteLine("PASS Koala Kong controlled purchase: local marketplace ID 618");
            }
            TestAllUnlocks(Save.Load(files.Single(p => Path.GetFileName(p) == "mine_CUSA13795_savedata0.sav")));
            Check(TimeTrialRewards.All.All(r => fullRoster.Semantic()[r.Key] is true), "developer-complete fixture lacks reward flags");
            Console.WriteLine("PASS unlock/lock all: known conditions, shared Champion reward, base content and unrelated data preserved");
            Console.WriteLine("PASS malformed-input checks and all sample SHA-256 identities unchanged"); passed++;
        }
        catch (Exception e) { Console.WriteLine("FAIL validation: " + e.Message); failed++; }
        Console.WriteLine($"{passed} PASS, {failed} FAIL; {files.Length} samples. Outputs: {outputDir}");
        return failed == 0 ? 0 : 1;
    }
    private static void TestGarage(string outputDir)
    {
        string source = "target/garage/g09_firehawk.sav", donor = "target/garage/g04_wheels.sav";
        byte[] original = File.ReadAllBytes(source), donorBytes = File.ReadAllBytes(donor);
        var before = Save.Parse(original); var preset = KartLoadout.Read(Save.Parse(donorBytes), "Regression loadout").Preset;
        var edited = KartLoadout.Apply(before, KartPreset.FromJson(preset.ToJson()));
        Check(KartLoadout.Read(edited, preset.Name).Preset == preset, "Garage differs from donor loadout");
        Check(before.Graph.Names.All(p => edited.Graph.Names.GetValueOrDefault(p.Key) == p.Value), "Garage lost existing graph names");
        byte[] a = before.Serialize(), b = edited.Serialize();
        var names = before.Graph.Sections.Single(s => s.Name == "graph_names");
        var newNames = edited.Graph.Sections.Single(s => s.Name == "graph_names");
        var memory = before.Graph.Sections.Single(s => s.Name == "graph_memory"); int end = memory.Offset + memory.Size;
        Check(a.Length == b.Length && a.AsSpan(0, names.Offset).SequenceEqual(b.AsSpan(0, names.Offset)), "Garage changed prefix or physical length");
        a.AsSpan(KartLoadout.Read(before).Record.WordOffset(0), 40).Clear();
        b.AsSpan(KartLoadout.Read(edited).Record.WordOffset(0), 40).Clear();
        Check(a.AsSpan(names.Offset + names.Size, end - names.Offset - names.Size).SequenceEqual(
            b.AsSpan(newNames.Offset + newNames.Size, end - names.Offset - names.Size)), "Garage changed unrelated graph bytes");
        Check(b.AsSpan(end + newNames.Size - names.Size).IndexOfAnyExcept((byte)0) < 0, "Garage changed padding");
        string output = Path.Combine(outputDir, "garage-applied.sav"); FileWriter.NewWithBackup(output, original, edited.Serialize());
        Check(KartLoadout.Read(Save.Load(output), preset.Name).Preset == preset, "Garage output failed to parse");
        Check(File.ReadAllBytes(source).AsSpan().SequenceEqual(original) && File.ReadAllBytes(donor).AsSpan().SequenceEqual(donorBytes), "Garage input changed");
    }
    private static void TestGlobal(Save save)
    {
        GlobalProfile.Require(save);
        byte[] original = save.Serialize();
        foreach (var egg in save.Graph.Fields.Where(f => f.Type == "egg-bit"))
        {
            var changed = save.Set(egg.Key, (bool)egg.Value ? "false" : "true");
            byte[] bytes = changed.Serialize();
            Check((bool)changed.Semantic()[egg.Key]! != (bool)egg.Value, "egg did not change");
            Check(Enumerable.Range(0, bytes.Length).All(i => bytes[i] == original[i] || i == egg.Offset), "egg edit changed unrelated bytes");
            Check((bytes[egg.Offset] ^ original[egg.Offset]) == (1 << int.Parse(egg.Key.Split('_')[^1], CultureInfo.InvariantCulture)), "egg changed unrelated bits");
            Check(changed.Set(egg.Key, (bool)egg.Value ? "true" : "false").Serialize().AsSpan().SequenceEqual(original), "egg undo not identical");
        }
        var roster = save.Graph.Fields.Where(f => f.Type == "character-bit").ToArray();
        Check(GlobalUnlocks.Drivers.Count == 56 && GlobalUnlocks.Items.Single(i => i.Id == 618).NameEn == "Koala Kong", "native catalogue identity mismatch");
        foreach (string key in new[] { "global.character.penta_penguin", "global.character.n_tropy" })
        {
            var flag = save.Graph.Fields.Single(f => f.Key == key);
            var toggled = save.Set(key, (bool)flag.Value ? "false" : "true");
            var toggledBytes = toggled.Serialize();
            Check(toggled.Set(key, (bool)flag.Value ? "true" : "false").Serialize().AsSpan().SequenceEqual(original), "reward flag undo failed");
            Check(Enumerable.Range(0, original.Length).All(i => original[i] == toggledBytes[i] || i == flag.Offset), "reward flag changed unrelated bytes");
        }
        if (save.Semantic().ContainsKey(GlobalUnlocks.ItemKey(618)))
        {
            var owned = GlobalUnlocks.Owned(save);
            int missing = GlobalUnlocks.Items.First(i => !owned.Contains((ushort)i.Id)).Id;
            var added = save.Set(GlobalUnlocks.ItemKey(missing), "true");
            Check(GlobalUnlocks.Owned(added).SequenceEqual(owned.Append((ushort)missing)), "marketplace append failed");
            Check(added.Set(GlobalUnlocks.ItemKey(missing), "false").Serialize().AsSpan().SequenceEqual(original), "marketplace grow/shrink did not restore exact bytes");
            Check(save.Serialize().Length == added.Serialize().Length && added.Graph.Warnings.Count == 0, "marketplace rebuild changed physical size or failed parsing");
            var addedSemantic = added.Semantic();
            foreach (var f in save.Graph.Fields.Where(f => f.Type is not ("marketplace-item" or "skin-unlock" or "customization-unlock")))
                Check(JsonOutput.Equal(f.Value, addedSemantic[f.Key]), "marketplace edit changed unrelated field: " + f.Key);
            var allNamed = GlobalUnlocks.AllCharacters(save);
            var allSemantic = allNamed.Semantic();
            Check(GlobalUnlocks.Drivers.Where(d => d.Key is not null).All(d => allSemantic[d.Key!] is true), "named character grant incomplete");
            ExpectFailure(() => save.Set("global.marketplace.item_9999", "true"), "unknown marketplace item accepted");
        }
        Check(roster.Length == 512, "512-bit roster unavailable");
        TestSkins(save);
        TestTimeAttack(save);
        TestCustomization(save);
        TestTimeTrialRewards(save);
        // Exercise every byte/word boundary, the anchor, reserved bit and final capacity bit.
        foreach (int bit in new[] { 0, 5, 7, 8, 31, 32, 63, 64, 69, 73, 74, 511 })
        {
            var f = roster[bit];
            var edited = save.Set(f.Key, (bool)f.Value ? "false" : "true");
            var bytes = edited.Serialize();
            Check(edited.Semantic()[f.Key] is bool v && v != (bool)f.Value, "roster edit lost");
            Check(bytes[f.Offset] == (original[f.Offset] ^ (1 << (bit % 8))), "roster changed wrong bit");
            Check(bytes.AsSpan(0, f.Offset).SequenceEqual(original.AsSpan(0, f.Offset)) && bytes.AsSpan(f.Offset + 1).SequenceEqual(original.AsSpan(f.Offset + 1)), "roster edit changed unrelated bytes");
            Check(edited.Set(f.Key, (bool)f.Value ? "true" : "false").Serialize().AsSpan().SequenceEqual(original), "roster undo changed bytes");
        }
        var preserved = save.Set(GlobalProfile.CharacterKey(5), "true").Set(GlobalProfile.CharacterKey(511), "true");
        var bulk = GlobalProfile.CheckAllCharacters(preserved);
        Check(bulk.Semantic()[GlobalProfile.CharacterKey(5)] is true && bulk.Semantic()[GlobalProfile.CharacterKey(511)] is true, "check all erased unknown bits");
        var bulkBytes = bulk.Serialize();
        Check(bulkBytes.AsSpan(0, roster[0].Offset).SequenceEqual(original.AsSpan(0, roster[0].Offset)) && bulkBytes.AsSpan(roster[511].Offset + 1).SequenceEqual(original.AsSpan(roster[511].Offset + 1)), "check all changed bytes outside roster");
        ExpectFailure(() => save.Set(GlobalProfile.CharacterKey(69), "maybe"), "invalid character boolean accepted");
        var field = save.Graph.Fields.Single(f => f.Key == "global.wumpa_coins");
        var coins = save.Set(field.Key, "123456");
        Check((int)coins.Semantic()[field.Key]! == 123456, "coins edit failed");
        var coinBytes = coins.Serialize();
        Check(Enumerable.Range(0, original.Length).All(i => coinBytes[i] == original[i] || i >= field.Offset && i < field.Offset + 4), "coins changed unrelated bytes");
        Check(coins.Set(field.Key, Convert.ToString(field.Value, CultureInfo.InvariantCulture)!).Serialize().AsSpan().SequenceEqual(original), "coins undo failed");
        ExpectFailure(() => save.Set(field.Key, "-1"), "negative coins accepted");
        foreach (var ro in save.Graph.Fields.Where(f => f.Key.StartsWith("global.observed.") || f.Key.StartsWith("global.time_trial.") || f.Key.StartsWith("global.bitmask_")).Take(3))
            ExpectFailure(() => save.Set(ro.Key, "true"), "read-only global field accepted");
        if (save.Graph.Objects.Single(o => o.ClassName == "igCharList").Words[0] == 60)
            Check(!save.Graph.Fields.Any(f => f.Type == "egg-bit"), "v31 guessed egg layout");
        var corrupted = original.ToArray();
        var objects = save.Sections.Single(s => s.Name == "graph_objects");
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(corrupted.AsSpan(objects.Offset + 4, 4), 4);
        var broken = Save.Parse(corrupted);
        ExpectFailure(() => broken.Set("global.wumpa_coins", "1"), "incomplete global graph editable");
        Check(GlobalProfile.TimeTrials(save).Count > 0, "time trials missing");
    }
    private static void TestTimeTrialRewards(Save save)
    {
        var original = save.Serialize(); var values = save.Semantic();
        foreach (var reward in TimeTrialRewards.All)
        {
            var field = save.Graph.Fields.SingleOrDefault(f => f.Key == reward.Key);
            if (field is null)
            {
                ExpectFailure(() => save.Set(reward.Key, "true"), "old patch accepted unavailable reward"); continue;
            }
            bool old = field.Value is true; var changed = save.Set(reward.Key, old ? "false" : "true"); var bytes = changed.Serialize();
            Check(changed.Semantic()[reward.Key] is bool state && state != old, "reward flag did not toggle");
            Check(bytes.AsSpan(0, field.Offset).SequenceEqual(original.AsSpan(0, field.Offset)) && bytes.AsSpan(field.Offset + 1).SequenceEqual(original.AsSpan(field.Offset + 1)), "reward changed unrelated bytes");
            Check(changed.Set(reward.Key, old ? "true" : "false").Serialize().AsSpan().SequenceEqual(original), "reward undo not byte identical");
        }
        if (values.ContainsKey(TimeTrialRewards.AllOxide))
        {
            var champion = CustomizationUnlocks.Items.Where(i => i.Condition == "COctaneChampionKartUnlockable").ToArray();
            Check(champion.Length == 3, "Champion trio missing");
            var kart = champion.Single(i => i.Type == "CRacerKartData"); var granted = save.Set(kart.Key, "true");
            Check(champion.All(i => granted.Semantic()[i.Key] is true) && granted.Semantic()[TimeTrialRewards.AllOxide] is true, "shared Champion grant incomplete");
            Check(GlobalUnlocks.Owned(granted).SequenceEqual(GlobalUnlocks.Owned(save)), "reward fabricated marketplace purchases");
            var alternate = save.Set(TimeTrialRewards.AllOxide, "false").Set(GlobalUnlocks.ItemKey(kart.MarketplaceIds.Single()), "true");
            Check(alternate.Semantic()[kart.Key] is true, "Champion marketplace alternative not recognized");
            Check(alternate.Set(kart.Key, "false").Semantic()[kart.Key] is false, "Champion stayed unlocked through alternate purchase");
        }
        Check(save.Serialize().AsSpan().SequenceEqual(original), "reward tests changed input");
    }
    private static void TestAllUnlocks(Save save)
    {
        byte[] original = save.Serialize(); var before = save.Semantic();
        var unknownIds = GlobalUnlocks.Owned(save).Where(id => !GlobalUnlocks.Items.Any(i => i.Id == id)).ToArray();
        var keys = UnlockBatch.Keys(save, UnlockScope.All); Check(keys.Length > 500, "bulk catalogue unexpectedly incomplete");
        var unlocked = UnlockBatch.Set(save, keys, true); var on = unlocked.Semantic();
        Check(keys.All(k => on[k] is true), "unlock all left a selected condition off");
        var locked = UnlockBatch.Set(unlocked, UnlockBatch.Keys(unlocked, UnlockScope.All), false); var off = locked.Semantic();
        Check(keys.All(k => off[k] is false), "lock all left a selected condition on");
        Check(SkinUnlocks.Skins.Where(s => s.Condition is null).All(s => on[s.Key] is true && off[s.Key] is true), "base skins locked");
        foreach (var edited in new[] { unlocked, locked })
        {
            var current = edited.Semantic();
            Check(edited.Serialize().Length == original.Length && edited.Graph.Warnings.Count == 0, "bulk graph damaged");
            Check(TimeAttack.Ghosts(edited).SequenceEqual(TimeAttack.Ghosts(save)) && TimeAttack.Records(edited).SequenceEqual(TimeAttack.Records(save)), "bulk changed ghost or time records");
            foreach (var field in save.Graph.Fields.Where(f => f.Key == "global.wumpa_coins" || f.Key.StartsWith("global.golden_egg.", StringComparison.Ordinal) || f.Key.StartsWith("global.unknown", StringComparison.Ordinal) && f.Key != "global.unknown_mask_16.count"))
                Check(JsonOutput.Equal(field.Value, current[field.Key]), "bulk changed preserved data: " + field.Key);
            Check(current[GlobalProfile.CharacterKey(5)]!.Equals(before[GlobalProfile.CharacterKey(5)]) && current[GlobalProfile.CharacterKey(511)]!.Equals(before[GlobalProfile.CharacterKey(511)]), "bulk overwrote unknown challenge bits");
            Check(unknownIds.All(id => GlobalUnlocks.Owned(edited).Contains(id)), "bulk erased unknown inventory IDs");
        }
        Check(UnlockBatch.Changes(save, unlocked).Count > 0, "bulk preview empty");
        Check(UnlockBatch.Set(locked, UnlockBatch.Keys(locked, UnlockScope.All), false).Serialize().AsSpan().SequenceEqual(locked.Serialize()), "lock all not idempotent");
        ExpectFailure(() => UnlockBatch.Set(save, ["global.customization.missing"], true), "unknown bulk selection accepted");
        Check(save.Serialize().AsSpan().SequenceEqual(original), "bulk mutated source before confirmation");
    }
    private static void TestTimeAttack(Save save)
    {
        byte[] original = save.Serialize();
        var ghosts = TimeAttack.Ghosts(save);
        foreach (var ghost in ghosts)
        {
            Check(ghost.Level is >= 0 and <= 4, "sample has unsupported ghost index");
            int next = ghost.Level == 4 ? 0 : 4;
            var field = save.Graph.Fields.Single(f => f.Key == ghost.Key);
            var edited = save.Set(ghost.Key, next.ToString(CultureInfo.InvariantCulture));
            byte[] bytes = edited.Serialize();
            Check(TimeAttack.Ghosts(edited).Single(g => g.Track == ghost.Track).Level == next, "ghost edit not retained");
            Check(Enumerable.Range(0, bytes.Length).All(i => original[i] == bytes[i] || i >= field.Offset && i < field.Offset + 4), "ghost edit modified replay or unrelated bytes");
            Check(save.Set(ghost.Key, next.ToString()).Set(ghost.Key, ghost.Level.ToString()).Serialize().AsSpan().SequenceEqual(original), "ghost undo changed bytes");
            ExpectFailure(() => save.Set(ghost.Key, "-1"), "negative ghost index accepted");
            ExpectFailure(() => save.Set(ghost.Key, "5"), "unsupported fifth ghost accepted");
            ExpectFailure(() => save.Set(ghost.Key, "1.5"), "fractional ghost index accepted");
        }
        ExpectFailure(() => save.Set("global.ghost.missing_track.level", "4"), "missing track fabricated");
        var records = TimeAttack.Records(save);
        Check(records.Count == GlobalProfile.TimeTrials(save).Count, "editable record inventory disagrees with legacy readout");
        var lap = records.FirstOrDefault(r => r.Category == "best_lap");
        if (lap is not null)
        {
            var field = save.Graph.Fields.Single(f => f.Key == lap.Key);
            float next = lap.Seconds == 12.345f ? 13.125f : 12.345f;
            var edited = save.Set(lap.Key, next.ToString("R", CultureInfo.InvariantCulture));
            var bytes = edited.Serialize();
            Check(TimeAttack.Records(edited).Single(r => r.Key == lap.Key).Seconds == next, "lap time edit lost");
            Check(Enumerable.Range(0, bytes.Length).All(i => original[i] == bytes[i] || i >= field.Offset && i < field.Offset + 4), "time edit modified replay or unrelated bytes");
            Check(edited.Set(lap.Key, lap.Seconds.ToString("R", CultureInfo.InvariantCulture)).Serialize().AsSpan().SequenceEqual(original), "time undo changed bytes");
            foreach (string formatted in new[] { "1:19.61", "1:19:61", "79.61" })
                Check(Math.Abs((float)save.Set(lap.Key, formatted).Semantic()[lap.Key]! - 79.61f) < 0.0001f, "formatted time parsed incorrectly");
            foreach (string invalid in new[] { "NaN", "Infinity", "-1", "0", "3.4028235E+38", "abc", "1:60.00", "1:10:100", "-1:10.00", "1:10.5:25", "1:2:3:4" })
                ExpectFailure(() => save.Set(lap.Key, invalid), "invalid time accepted: " + invalid);
        }
        var pair = records.GroupBy(r => (r.Track, r.Category)).FirstOrDefault(group => group.Any(r => r.Rank == 1) && group.Any(r => r.Rank == 2));
        if (pair is not null)
        {
            var first = pair.Single(r => r.Rank == 1); var second = pair.Single(r => r.Rank == 2);
            ExpectFailure(() => save.Set(first.Key, (second.Seconds + 5).ToString("R", CultureInfo.InvariantCulture)), "time edit broke ranking order");
        }
        Check(save.Serialize().AsSpan().SequenceEqual(original), "time attack tests mutated source");
    }
    private static void TestCustomization(Save save)
    {
        var original = save.Serialize();
        var blueGrey = CustomizationUnlocks.Items.Single(i => i.NameEn == "Blue-Grey");
        Check(blueGrey.ChallengeBit == 65 && blueGrey.CanEdit, "Fake Crash paint condition mismatch");
        bool owned = save.Semantic()[blueGrey.Key] is true;
        var changed = save.Set(blueGrey.Key, owned ? "false" : "true");
        Check(changed.Semantic()[blueGrey.Key] is bool state && state != owned, "challenge customization not updated");
        Check(changed.Semantic()[GlobalProfile.CharacterKey(65)] is bool driver && driver != owned, "shared driver reward not reflected");
        Check(changed.Set(blueGrey.Key, owned ? "true" : "false").Serialize().AsSpan().SequenceEqual(original), "customization undo changed bytes");
        var unsupported = CustomizationUnlocks.Items.First(i => !i.CanEdit);
        ExpectFailure(() => save.Set(unsupported.Key, "true"), "unknown cosmetic condition edited");
        var reward = CustomizationUnlocks.Items.First(i => i.Condition == "COctaneBeatNefariousTimesUnlockable");
        Check(save.Set(reward.Key, "true").Semantic()["global.character.n_tropy"] is true, "reward customization lost driver flag");
        if (save.Semantic().ContainsKey(GlobalUnlocks.ItemKey(618)))
        {
            var shop = CustomizationUnlocks.Items.First(i => i.CanEdit && i.UsesMarketplace);
            var bulk = CustomizationUnlocks.SetMany(save, [blueGrey.Key, reward.Key, shop.Key], true);
            Check(bulk.Semantic()[blueGrey.Key] is true && bulk.Semantic()[reward.Key] is true && bulk.Semantic()[shop.Key] is true, "mixed shop/challenge batch grant failed");
            Check(TimeAttack.Ghosts(bulk).SequenceEqual(TimeAttack.Ghosts(save)) && TimeAttack.Records(bulk).SequenceEqual(TimeAttack.Records(save)), "cosmetic batch changed time attack data");
            Check(GlobalUnlocks.Owned(save).All(id => GlobalUnlocks.Owned(bulk).Contains(id)), "batch lost existing inventory");
        }
        Check(save.Serialize().AsSpan().SequenceEqual(original), "customization test mutated source");
    }
    private static void TestSkins(Save save)
    {
        Check(SkinUnlocks.Skins.Count == 392, "skin catalogue count changed");
        var skin = SkinUnlocks.Skins.Single(s => s.Driver == "DriverNTropy" && s.Wins == 25);
        var changed = save.Set(skin.Key, "true");
        var semantics = changed.Semantic();
        Check(semantics[skin.Key] is true && (int)semantics[SkinUnlocks.WinsKey("DriverNTropy")]! >= 25, "win skin did not grant required count");
        Check(SkinUnlocks.Skins.Where(s => s.Driver == "DriverNTropy" && s.Wins is not null).All(s => semantics[s.Key] is true), "shared win skins disagree");
        foreach (var f in save.Graph.Fields.Where(f => f.Type is not ("skin-unlock" or "driver-wins")))
            Check(JsonOutput.Equal(f.Value, semantics[f.Key]), "skin win edit changed unrelated data: " + f.Key);
        Check(GlobalProfile.TimeTrials(save).SequenceEqual(GlobalProfile.TimeTrials(changed)), "skin grant modified time trials");
        var ten = SkinUnlocks.Skins.Single(s => s.Driver == "DriverNTropy" && s.Wins == 10);
        var lower = changed.Set(ten.Key, "false").Semantic();
        Check(lower[ten.Key] is false && lower[skin.Key] is false &&
            lower[SkinUnlocks.Skins.Single(s => s.Driver == "DriverNTropy" && s.Wins == 1).Key] is true, "skin threshold removal cascade incorrect");
        var ghost = SkinUnlocks.Skins.Single(s => s.Condition == "COctaneBeatOxideTimesUnlockable");
        Check(save.Set(ghost.Key, "true").Semantic()["global.bool_7"] is true, "Digital N. Tropy reward flag incorrect");
        var missing = SkinUnlocks.Skins.Where(s => s.Wins is not null).Select(s => s.Driver).Distinct().FirstOrDefault(d => !save.Graph.Strings.Contains(d));
        if (missing is not null)
        {
            var inserted = SkinUnlocks.SetWins(save, missing, 25);
            Check(inserted.Graph.Strings.Count == save.Graph.Strings.Count + 1 && inserted.Semantic()[SkinUnlocks.WinsKey(missing)]!.Equals(25), "new driver string/counter insertion failed");
            Check(inserted.Serialize().Length == save.Serialize().Length && inserted.Graph.Warnings.Count == 0, "skin insertion broke graph layout");
        }
        ExpectFailure(() => save.Set("global.driver_wins.DriverNTropy", "-1"), "negative driver wins accepted");
        ExpectFailure(() => save.Set(skin.Key, "maybe"), "invalid skin boolean accepted");
        var defaultSkin = SkinUnlocks.Skins.First(s => s.Condition is null);
        ExpectFailure(() => save.Set(defaultSkin.Key, "false"), "default skin locked independently");
    }
    private static void TestTracks(Save save)
    {
        save.RequireAdventureSlot();
        var totals = AdventureTotals.From(save.Graph.Tracks);
        int Slot(string name) => (int)save.Graph.Fields.Single(f => f.Key == "slot." + name).Value;
        Check(totals.Trophies == (int)save.Get("trophy_Count")!.Value && totals.Trophies == Slot("trophy_count"), "track trophy total mismatch");
        Check(totals.Relics == (int)save.Get("relic_count")!.Value, "track relic total mismatch");
        Check(totals.Sapphire == Slot("relic_sapphire") && totals.Gold == Slot("relic_gold") && totals.Platinum == Slot("relic_platinum"), "track relic tiers mismatch");
        Check(totals.TokenCount == (int)save.Get("ctr_token_count")!.Value, "track token total mismatch");
        foreach (var (colour, count) in totals.Tokens) Check(count == Slot("token_" + colour), "track token colour mismatch: " + colour);
        foreach (var track in save.Graph.Tracks)
        {
            string prefix = "track." + track.Definition.Id + ".";
            var edits = new List<(string Field, string Value, uint Mask)> { ("flags", "0x80000000", ~track.Definition.AwardMask) };
            if (track.Definition.HasTrophy) edits.Add(("trophy", (!track.Trophy).ToString(), 2));
            if (track.Definition.HasRelic)
                foreach (string tier in new[] { "none", "sapphire", "gold", "platinum" }) edits.Add(("relic", tier, 0x38));
            if (track.Definition.TokenBit is int bit) edits.Add(("token", (!track.Token).ToString(), 1u << bit));
            foreach (var (field, value, mask) in edits)
            {
                Save changed = Save.Parse(save.Set(prefix + field, value).Serialize());
                var row = changed.Graph.Tracks.Single(t => t.Definition.Id == track.Definition.Id);
                Check(((row.Flags ^ track.Flags) & ~mask) == 0, "track edit clobbered other bits");
                if (field == "relic") Check(row.Relic == value, "relic round-trip failed");
                if (field == "token") Check(row.Token != track.Token, "token round-trip failed");
                if (field == "trophy") Check(row.Trophy != track.Trophy, "trophy round-trip failed");
                if (field == "flags") Check(row.OtherFlags == 0x80000000, "raw other flags round-trip failed");
                var t = AdventureTotals.From(changed.Graph.Tracks);
                Check(t.Relics == (int)changed.Get("relic_count")!.Value && t.TokenCount == (int)changed.Get("ctr_token_count")!.Value &&
                    t.Trophies == (int)changed.Get("trophy_Count")!.Value, "edited header summaries mismatch");
                Check((int)changed.Graph.Fields.Single(f => f.Key == "slot.relic_sapphire").Value == t.Sapphire &&
                    (int)changed.Graph.Fields.Single(f => f.Key == "slot.relic_gold").Value == t.Gold &&
                    (int)changed.Graph.Fields.Single(f => f.Key == "slot.relic_platinum").Value == t.Platinum, "edited slot relic summaries mismatch");
                foreach (var (colour, count) in t.Tokens)
                    Check((int)changed.Graph.Fields.Single(f => f.Key == "slot.token_" + colour).Value == count, "edited slot token summary mismatch");
                var allowed = changed.Graph.Fields.Where(f => f.Key.StartsWith("slot.token_", StringComparison.Ordinal) ||
                    f.Key.StartsWith("slot.relic_", StringComparison.Ordinal) || f.Key == "slot.trophy_count").Select(f => (f.Offset, f.Size))
                    .Concat(changed.Fields.Where(f => f.Name is "trophy_Count" or "relic_count" or "ctr_token_count").Select(f => (f.Offset, f.Size)))
                    .Append((track.Offset, 4)).ToArray();
                byte[] a = save.Serialize(), b = changed.Serialize();
                Check(a.Length == b.Length && Enumerable.Range(0, a.Length).All(i => a[i] == b[i] || allowed.Any(r => i >= r.Offset && i < r.Offset + r.Item2)), "track edit moved layout or changed unrelated bytes");
                string undo = field switch { "relic" => track.Relic, "token" => track.Token.ToString(), "trophy" => track.Trophy.ToString(), _ => "0x" + track.OtherFlags.ToString("X8") };
                Check(save.Serialize().AsSpan().SequenceEqual(changed.Set(prefix + field, undo).Serialize()), "track undo did not restore bytes");
            }
            if (track.Definition.HasRelic) ExpectFailure(() => save.Set(prefix + "relic", "diamond"), "invalid relic accepted");
            ExpectFailure(() => save.Set(prefix + "flags", "0xffffffff"), "other flags accepted award bits");
        }
        Console.WriteLine($"PASS track awards: {totals.Trophies} trophies, {totals.Sapphire}/{totals.Gold}/{totals.Platinum} relics, {totals.TokenCount} tokens; colours, all tiers, isolation and undo");
    }

    private static void TestRules(Save save)
    {
        Check(!AdventureRules.Validate(save).Any(i => i.Severity == "error"), "sample consistency errors");
        var normalized = AdventureRules.Normalize(save);
        Check(!AdventureRules.Validate(normalized).Any(i => i.Severity == "error"), "normalized sample errors");
        Check(JsonOutput.Equal(normalized.Get("percent_complete")!.Value, save.Get("percent_complete")!.Value), "sample percentage estimate mismatch");
        ExpectFailure(() => AdventureRules.Edit(save, "slot.gem_red", "2"), "invalid gem value accepted");
        var fresh = Save.Load("target/fresh/step0_new.sav");
        var oxide = AdventureRules.Edit(fresh, "slot.oxide_second", "true").Save;
        Check(oxide.Semantic()["slot.oxide_first"] is true && oxide.Semantic()["slot.oxide_second"] is true &&
            AdventureRules.Keys(oxide) == 4 && AdventureTotals.From(oxide.Graph.Tracks).Relics == 18 && AdventureTotals.From(oxide.Graph.Tracks).TokenCount == 20,
            "Oxide rematch prerequisites missing");
        var lostOxide = AdventureRules.Edit(oxide, "slot.oxide_first", "false").Save;
        Check(lostOxide.Semantic()["slot.oxide_first"] is false && lostOxide.Semantic()["slot.oxide_second"] is false, "Oxide first-victory removal did not clear rematch");
        var noKey = AdventureRules.Edit(oxide, "track.t153_hot_air_skyway.boss", "false").Save;
        Check(noKey.Semantic()["slot.oxide_first"] is false && noKey.Semantic()["slot.oxide_second"] is false, "lost key retained Oxide victories");
        var easy = save.Set("slot.adventure_difficulty", "0");
        Check((int)easy.Get("adventure_difficulty")!.Value == 0 && easy.Semantic()["slot.enum_21"]!.Equals(0), "difficulty header and slot diverged");
        ExpectFailure(() => save.Set("slot.adventure_difficulty", "3"), "unsupported Adventure difficulty accepted");
        var first = AdventureRules.Edit(fresh, "track.t111_crash_cove.trophy", "true").Save;
        Check((bool)first.Semantic()["progress.t111_crash_cove.trophy"]! && (int)first.Get("active_progression_index")!.Value == 1, "first progression not updated");
        var rebuilt = first;
        foreach (string id in new[] { "t112_roos_tubes", "t113_mystery_caves", "t114_sewer_speedway" })
            rebuilt = AdventureRules.Edit(rebuilt, "track." + id + ".trophy", "true").Save;
        rebuilt = AdventureRules.Edit(rebuilt, "track.t112_roos_tubes.boss", "true").Save;
        var realRoo = Save.Load("target/fresh/step3_after_roo.sav");
        foreach (string key in new[] { "slot.mask_15", "slot.mask_16", "slot.unknown_17", "header.farthest_progression_index", "header.active_progression_index" })
            Check(JsonOutput.Equal(rebuilt.Semantic()[key], realRoo.Semantic()[key]), "Roo reconstruction differs: " + key);
        Check(JsonOutput.Equal(rebuilt.Get("previous_level_progression_index")!.Value, fresh.Get("previous_level_progression_index")!.Value), "reconstruction invented travel history");
        var cursorHistory = realRoo.Set("slot.unknown_17", "23");
        Check(AdventureRules.Edit(cursorHistory, "track.t112_roos_tubes.boss", "true").Save.Serialize().AsSpan().SequenceEqual(cursorHistory.Serialize()), "no-op award edit changed history");
        var closed = AdventureRules.Edit(rebuilt, "track.t113_mystery_caves.trophy", "false").Save;
        Check(((uint)closed.Semantic()["slot.mask_16"]! & 0x2082) == 0, "Roo doors left open after regression");
        var repairedDoors = AdventureRules.Normalize(fresh.Set("slot.mask_16", "0x2082"));
        Check(((uint)repairedDoors.Semantic()["slot.mask_16"]! & 0x2082) == 0, "Normalize left unsupported Roo doors open");
        var badGroup = fresh.Set("slot.mask_15", "0x180E");
        Check(AdventureRules.Validate(badGroup).Any(i => i.Code == "progress-mask"), "validator missed captured mask group mismatch");
        Check((uint)AdventureRules.Normalize(badGroup).Semantic()["slot.mask_15"]! == 0x1800, "captured group reset failed");
        var unknown = fresh.Set("slot.mask_16", "0x80000000");
        Check((uint)AdventureRules.Normalize(unknown).Semantic()["slot.mask_16"]! == 0x80000000, "unknown unlock bit overwritten");
        Check(AdventureRules.Normalize(realRoo.Set("slot.unknown_17", "23")).Semantic()["slot.unknown_17"]!.Equals(23), "cursor history canonicalized");
        var mystery = AdventureRules.Edit(fresh, "track.t113_mystery_caves.trophy", "true").Save;
        Check(mystery.Graph.Tracks.Single(t => t.Definition.Id == "t111_crash_cove").Trophy, "Mystery gate did not add earlier trophy");
        var roo = AdventureRules.Edit(fresh, "track.t112_roos_tubes.trophy", "true").Save;
        mystery = AdventureRules.Edit(roo, "track.t113_mystery_caves.trophy", "true").Save;
        Check(!mystery.Graph.Tracks.Single(t => t.Definition.Id == "t111_crash_cove").Trophy, "Mystery gate ignored existing Roo trophy");
        var noEarlier = AdventureRules.Edit(AdventureRules.Edit(mystery, "track.t111_crash_cove.trophy", "false").Save, "track.t112_roos_tubes.trophy", "false").Save;
        Check(!noEarlier.Graph.Tracks.Single(t => t.Definition.Id == "t113_mystery_caves").Trophy, "Mystery removal cascade failed");
        if (AdventureTotals.From(save.Graph.Tracks).Trophies == 16)
            foreach (var boss in AdventureRules.BossTracks.Select((id, i) => (id, i)))
            {
                string trophy = AdventureTracks.All.Where(t => t.HasTrophy).ElementAt(boss.i * 4).Id;
                var cascade = AdventureRules.Edit(save, "track." + trophy + ".trophy", "false").Save;
                Check(AdventureRules.Keys(cascade) == boss.i, "hub-specific key cascade failed");
                var restored = AdventureRules.Edit(cascade, "track." + boss.id + ".boss", "true").Save;
                Check(AdventureRules.Keys(restored) == boss.i + 1 && restored.Graph.Tracks.Single(t => t.Definition.Id == trophy).Trophy, "boss prerequisite expansion failed");
            }
        Check(normalized.Serialize().AsSpan().SequenceEqual(save.Serialize()), "normalization changed an already consistent sample");
        Check(normalized.Serialize().AsSpan().SequenceEqual(AdventureRules.Normalize(normalized).Serialize()), "normalization not idempotent");
        var removed = AdventureRules.Edit(save, "track.t111_crash_cove.trophy", "false").Save;
        Check(AdventureRules.Keys(removed) == 0 && removed.Graph.Tracks.All(t => !t.Boss && !t.Token && t.Relic == "none"), "trophy removal did not cascade through hubs");
        Check(!AdventureRules.Validate(removed).Any(i => i.Severity == "error"), "cascade consistency errors");
        var turbo = AdventureRules.Edit(removed, "track.t122_turbo_track.relic", "platinum").Save;
        Check(AdventureRules.Colours.All(c => AdventureRules.Gem(turbo, c)) && AdventureTotals.From(turbo.Graph.Tracks).TokenCount == 20 && AdventureRules.Keys(turbo) == 4, "Turbo prerequisites missing");
        Check(!AdventureRules.Validate(turbo).Any(i => i.Severity == "error"), "Turbo consistency errors");
        var slide = AdventureRules.Edit(removed, "track.t121_slide_coliseum.relic", "gold").Save;
        Check(slide.Graph.Tracks.Count(t => t.Relic != "none" && t.Definition.Id != "t121_slide_coliseum") >= 10, "Slide prerequisites missing");
        var full = turbo;
        var badCounters = full.Set("slot.boss_key_count", "0").Set("boss_key_count", "0");
        Check(AdventureRules.Validate(badCounters).Count(i => i.Code == "summary") == 2, "validator missed key counters");
        Check(AdventureRules.Keys(AdventureRules.Normalize(badCounters)) == 4 && (int)AdventureRules.Normalize(badCounters).Get("boss_key_count")!.Value == 4, "key counter repair failed");
        var broken = full.Set("track.t111_crash_cove.trophy", "false");
        Check(AdventureRules.Validate(broken).Any(i => i.Severity == "error"), "validator missed invalid boss");
        Check(!AdventureRules.Validate(AdventureRules.Normalize(broken)).Any(i => i.Severity == "error"), "normalize failed to repair boss cascade");
        foreach (var (key, known) in new[] { ("slot.mask_15", 0xEu), ("slot.mask_16", 0x2082u) })
            Check(((uint)save.Semantic()[key]! & ~known) == ((uint)removed.Semantic()[key]! & ~known), "unknown mask bits changed: " + key);
        foreach (string key in new[] { "var.KeyTotal", "var.C3_Hub_Progression", "var.Gem_Red" })
            Check(JsonOutput.Equal(save.Semantic()[key], removed.Semantic()[key]), "unverified field overwritten: " + key);
        foreach (string key in new[] { "farthest_chapter", "previous_level_progression_index", "story_completed", "credits_watched" })
            Check(JsonOutput.Equal(save.Get(key)!.Value, removed.Get(key)!.Value), "unverified header overwritten: " + key);
        foreach (var t in save.Graph.Tracks)
            Check(t.OtherFlags == removed.Graph.Tracks.Single(r => r.Definition.Id == t.Definition.Id).OtherFlags, "cascade clobbered unknown flags");
        Check((bool)removed.Semantic()["progress.t111_crash_cove.trophy"]! == false, "Crash progression bit not cleared");
        var red = AdventureRules.Edit(removed, "slot.gem_red", "1").Save;
        Check(AdventureTotals.From(red.Graph.Tracks).Tokens["red"] == 4, "red gem prerequisites missing");
        var noRed = AdventureRules.Edit(red, "track.t111_crash_cove.token", "false").Save;
        Check(!AdventureRules.Gem(noRed, "red"), "token removal did not remove gem");
        Console.WriteLine("PASS Adventure rules: sample validation, cascade, Turbo/Slide prerequisites, repair, idempotence, unknown-field preservation");
    }

    private static void Check(bool ok, string message) { if (!ok) throw new SaveFormatException(message); }
    private static void ExpectFailure(Action action, string message)
    {
        bool rejected = false;
        try { action(); }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException) { rejected = true; }
        Check(rejected, message);
    }
}
