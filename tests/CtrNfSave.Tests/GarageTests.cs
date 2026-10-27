using System.Buffers.Binary;
using CtrNfSave;

namespace CtrNfSave.Tests;

internal static partial class Program
{
    private static void GarageTests()
    {
        Test("Garage captured name hash pairs", () =>
        {
            foreach (var (name, hash) in new (string, uint)[]
            {
                ("SystemData", 0x6236AC84), ("SystemData_availableDrivers_5", 0xA2F77BC3),
                ("SystemData_availableDrivers_58", 0x86982C21), ("SystemData_availableKarts_0", 0xA40D14D4),
                ("SystemData_availableKarts_7", 0xA30D1341), ("SystemData_availableKarts_15", 0x259A2316),
                ("SystemData_availableKarts_32", 0xB89F87AD)
            }) Assert(KartLoadout.NameHash(name) == hash, "Captured name hash mismatch.");
        });
        Test("Garage active record and display name", () =>
        {
            var current = KartLoadout.Read(Global());
            Assert(current.Preset is { Driver: "SystemData_availableDrivers_5", Skin: 5, Chassis: "SystemData_availableKarts_7", Wheels: 15, Paint: 6, Word7: -1, Sticker: 57, Word9: -1 }, "Loadout word mapping differs.");
            Assert(KartLoadout.DriverName(current.Preset.Driver) == "Komodo Joe", "Driver index mapping differs.");
            Assert(KartLoadout.DriverName("SystemData_availableDrivers_58") == "Iron Checkpoint Crate", "Driver 58 must resolve through DriverHereIAm, rather than Crunch.");
            Assert(KartLoadout.DriverName("SystemData_availableDrivers_0") == "Crash Bandicoot" &&
                KartLoadout.Ownership(Global(), current.Preset with { Driver = "SystemData_availableDrivers_0" }).Driver is true, "Authored Crash identity and unconditional ownership were lost.");
            Assert(KartLoadout.Ownership(Global(), current.Preset).Driver is false, "Known locked driver was not detected.");
            Assert(KartLoadout.Ownership(Global(), current.Preset).Chassis is null, "Unverified chassis ownership guessed.");
            var unlocked = Global().Set(GlobalProfile.CharacterKey(15), "true");
            Assert(KartLoadout.Ownership(unlocked, current.Preset).Driver is true, "Existing driver ownership predicate not used.");
        });
        Test("Garage crate and King Chicken ownership use their authored challenges", () =>
        {
            var save = Global(); var current = KartLoadout.Read(save).Preset;
            var crate = current with { Driver = "SystemData_availableDrivers_58" };
            var chicken = current with { Driver = "SystemData_availableDrivers_48" };
            Assert(KartLoadout.Ownership(save, crate).Driver is false && KartLoadout.Ownership(save, chicken).Driver is false, "Locked challenge drivers must remain locked.");
            var crateUnlocked = save.Set(GlobalProfile.CharacterKey(70), "true");
            Assert(KartLoadout.Ownership(crateUnlocked, crate).Driver is true && KartLoadout.Ownership(crateUnlocked, chicken).Driver is false, "Crate must use challenge 70 independently of King Chicken.");
            var chickenUnlocked = save.Set(GlobalProfile.CharacterKey(69), "true");
            Assert(KartLoadout.Ownership(chickenUnlocked, chicken).Driver is true && KartLoadout.Ownership(chickenUnlocked, crate).Driver is false, "King Chicken must use challenge 69 independently of crate.");
        });
        Test("Garage embedded catalogue reproduces controlled loadout labels", () =>
        {
            const string komodo = "SystemData_availableDrivers_5";
            foreach (bool french in new[] { false, true })
            {
                Assert(KartLoadout.DriverName(komodo, french) == "Komodo Joe", "Driver 5 label differs from the controlled capture.");
                Assert(KartLoadout.SkinName(komodo, 0, french) == "Komodo Joe", "Default skin must resolve within its driver's skin list.");
                Assert(KartLoadout.SkinName(komodo, 5, french) == (french ? "Komodo Joe Sorcier" : "Wizard Komodo Joe"), "Komodo Joe skin 5 label differs.");
                Assert(KartLoadout.ChassisName("SystemData_availableKarts_0", french) == (french ? "Classique" : "Classic"), "Classic chassis label differs.");
                Assert(KartLoadout.ChassisName("SystemData_availableKarts_7", french) == "Team Bandicoot", "Chassis 7 label differs.");
                Assert(KartLoadout.WheelsName(0, french) == (french ? "Classique" : "Classic"), "Classic wheels label differs.");
                Assert(KartLoadout.WheelsName(15, french) == "Cabrio", "Wheels save index 15 must resolve through the item index, rather than handle suffix 15.");
                Assert(KartLoadout.PaintName(0, french) == (french ? "Bleu" : "Blue"), "Default blue paint label differs.");
                Assert(KartLoadout.PaintName(6, french) == (french ? "Violet" : "Purple"), "Paint 6 label differs.");
                Assert(KartLoadout.StickerName(0, french) == (french ? "Pas d'autocollant" : "No Sticker"), "Sticker zero must use the separate noSticker entry.");
                Assert(KartLoadout.StickerName(57, french) == (french ? "Zéro noir" : "Zero Black"), "Sticker 57 label differs.");
            }
            Assert(KartLoadout.DriverName("SystemData_availableDrivers_48") == "King Chicken", "Driver 48 identity differs from authored data.");
        });
        Test("Garage special chassis labels use their complete preset tuple", () =>
        {
            var current = KartLoadout.Read(Global()).Preset;
            var retro = current with { Chassis = "SystemData_availableKarts_15", Wheels = 9, Paint = 16, Word7 = -1, Sticker = 0, Word9 = 0 };
            var firehawk = current with { Chassis = "SystemData_availableKarts_32", Wheels = 0, Paint = 99, Word7 = 0, Sticker = 0, Word9 = 8 };
            Assert(KartLoadout.ChassisName(retro) == "Blue Retro Kart" && KartLoadout.ChassisName(retro, true) == "Kart rétro bleu", "Retro Blue preset label differs from localized game data.");
            Assert(KartLoadout.ChassisName(firehawk) == "Firehawk" && KartLoadout.ChassisName(firehawk, true) == "Faucon de feu", "Firehawk preset label differs from localized game data.");
            Assert(KartLoadout.ChassisName(retro with { Paint = 0 }, true) != "Kart rétro bleu", "Incomplete special tuple was falsely presented as the complete preset.");
        });
        Test("Garage unknown catalogue values remain visible", () =>
        {
            foreach (bool french in new[] { false, true })
            {
                Assert(KartLoadout.DriverName("SystemData_availableDrivers_9999", french) == "SystemData_availableDrivers_9999", "Unknown driver handle was hidden.");
                Assert(KartLoadout.ChassisName("SystemData_availableKarts_9999", french) == "SystemData_availableKarts_9999", "Unknown chassis handle was hidden.");
                Assert(KartLoadout.SkinName("SystemData_availableDrivers_5", 9999, french) == "9999" &&
                    KartLoadout.SkinName("SystemData_availableDrivers_9999", 0, french) == "0", "Unknown skin must retain its raw value.");
                Assert(KartLoadout.WheelsName(9999, french) == "9999" && KartLoadout.PaintName(9999, french) == "9999" &&
                    KartLoadout.StickerName(9999, french) == "9999", "Unknown part index was hidden.");
                Assert(KartLoadout.PaintName(99, french) == "99", "Paint 99 has no verified localized label and must retain its raw value.");
            }
            var unknown = KartLoadout.Read(Global()).Preset with { Driver = "SystemData_availableDrivers_9999" };
            Assert(KartLoadout.Ownership(Global(), unknown).Driver is null, "Unknown driver ownership was guessed.");
        });
        Test("Garage strict JSON round-trip and malformed preset rejection", () =>
        {
            var preset = KartLoadout.Read(Global(), "Sorcier / Wizard 🏁").Preset;
            Assert(KartPreset.FromJson(preset.ToJson()) == preset, "Preset JSON changed values.");
            foreach (string bad in new[] { "null", "[]", "{}", "{", preset.ToJson().Replace("\"skin\": 5,", ""),
                preset.ToJson().Replace("\"skin\": 5", "\"skin\": null"), preset.ToJson().Replace("\"skin\": 5", "\"skin\": 2147483648"),
                preset.ToJson().Replace("\"skin\": 5", "\"skin\": 5, \"skin\": 0"), preset.ToJson().Replace("\"skin\": 5", "\"skin\": 5, \"extra\": 1") })
                Reject(() => KartPreset.FromJson(bad));
            foreach (var bad in new[] { preset with { Name = " " }, preset with { Name = "bad\0name" }, preset with { Name = new string('x', 101) },
                preset with { Driver = null! }, preset with { Driver = "SystemData_availableDrivers_05" }, preset with { Chassis = "bad" },
                preset with { Skin = -1 }, preset with { Wheels = -1 }, preset with { Paint = -1 }, preset with { Sticker = -1 }, preset with { FormatVersion = 2 } })
                Reject(() => KartLoadout.Apply(Global(), bad));
        });
        Test("Garage existing names change only the active ten words", () =>
        {
            var save = Global(); var active = KartLoadout.Read(save); var preset = active.Preset with { Skin = 0, Wheels = 0, Paint = 99, Word7 = 0, Sticker = 0, Word9 = 8 };
            var changed = KartLoadout.Apply(save, preset);
            Assert(KartLoadout.Read(changed).Preset == preset, "Preset not applied verbatim.");
            OnlyRange(save.Serialize(), changed.Serialize(), active.Record.WordOffset(0), 40);
            Assert(KartLoadout.Apply(save, active.Preset).Serialize().AsSpan().SequenceEqual(save.Serialize()), "No-op changed bytes.");
            var unknownBits = preset with { Word7 = int.MinValue, Word9 = int.MaxValue };
            Assert(KartLoadout.Read(KartLoadout.Apply(save, unknownBits)).Preset == unknownBits, "Unknown signed words were normalized.");
        });
        Test("Garage adds two names and preserves all other serialized data", () =>
        {
            var save = Global(); var preset = KartLoadout.Read(save).Preset with { Driver = "SystemData_availableDrivers_58", Chassis = "SystemData_availableKarts_32", Paint = 99, Wheels = 0, Word7 = 0, Sticker = 0, Word9 = 8 };
            var changed = KartLoadout.Apply(save, preset);
            Assert(KartLoadout.Read(changed).Preset == preset, "New named handles did not resolve.");
            Assert(changed.Graph.Names.Count == save.Graph.Names.Count + 2 && save.Graph.Names.All(p => changed.Graph.Names[p.Key] == p.Value), "Existing name table changed.");
            GaragePreserved(save, changed);
            var undo = KartLoadout.Apply(changed, KartLoadout.Read(save).Preset);
            Assert(KartLoadout.Read(undo).Preset == KartLoadout.Read(save).Preset, "Reverse apply lost the normal kart tuple.");
        });
        Test("Garage authored nonnumeric handles survive JSON and safe graph name growth", () =>
        {
            var save = Global();
            var preset = KartLoadout.Read(save, "Spyro Champion").Preset with { Driver = "DriverSpyro", Chassis = "kartChampion", Skin = 0 };
            Assert(KartPreset.FromJson(preset.ToJson()) == preset, "Authored nonnumeric handles were rejected or changed by JSON.");
            var changed = KartLoadout.Apply(save, preset);
            Assert(KartLoadout.Read(changed, preset.Name).Preset == preset, "Authored nonnumeric handles did not survive application.");
            Assert(changed.Graph.Names.Count == save.Graph.Names.Count + 2 &&
                changed.Graph.Names[KartLoadout.NameHash("DriverSpyro")] == "DriverSpyro" &&
                changed.Graph.Names[KartLoadout.NameHash("kartChampion")] == "kartChampion", "Authored nonnumeric handle hashes did not resolve after growth.");
            Assert(save.Graph.Names.All(p => changed.Graph.Names[p.Key] == p.Value), "Authored handle insertion changed existing graph names.");
            Assert(KartLoadout.DriverName(preset.Driver) == "Spyro" && KartLoadout.ChassisName(preset) == "Champion", "Authored nonnumeric handle labels differ.");
            GaragePreserved(save, changed);
            Reject(() => KartPreset.FromJson((preset with { Driver = "DriverSpyroUnknown" }).ToJson()));
        });
        Test("Garage refuses unsafe name growth and garbled records", () =>
        {
            var save = Global(); var active = KartLoadout.Read(save); var extraName = active.Preset with { Chassis = "SystemData_availableKarts_32" };
            byte[] bytes = save.Serialize(); bytes[^1] = 0xA5;
            var padded = Decode(bytes); Reject(() => KartLoadout.Apply(padded, extraName));
            // A fixed-size apply can preserve an unrelated nonzero trailer.
            Assert(KartLoadout.Apply(padded, active.Preset).Serialize()[^1] == 0xA5, "Trailer changed.");
            foreach (int word in new[] { 0, 1, 3, 4 })
            {
                bytes = save.Serialize(); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(active.Record.WordOffset(word), 4), 123);
                Reject(() => KartLoadout.Read(Decode(bytes)));
            }
            bytes = save.Serialize(); var empty = save.Graph.Objects.First(o => o.ClassName == "COctaneCustomizationSaveData" && o.Id != active.Record.Id);
            save.Serialize().AsSpan(active.Record.WordOffset(0), 40).CopyTo(bytes.AsSpan(empty.WordOffset(0), 40));
            Reject(() => KartLoadout.Read(Decode(bytes)));
            bytes = save.Serialize(); BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(empty.WordOffset(6), 4), -1);
            Reject(() => KartLoadout.Read(Decode(bytes)));
            Reject(() => KartLoadout.Read(Adventure()));
        });
    }
    private static void GaragePreserved(Save before, Save after)
    {
        byte[] a = before.Serialize(), b = after.Serialize();
        var names = before.Graph.Sections.Single(s => s.Name == "graph_names");
        var newNames = after.Graph.Sections.Single(s => s.Name == "graph_names"); int delta = newNames.Size - names.Size;
        Assert(a.Length == b.Length && a.AsSpan(0, names.Offset).SequenceEqual(b.AsSpan(0, names.Offset)), "Prefix or physical size changed.");
        a.AsSpan(KartLoadout.Read(before).Record.WordOffset(0), 40).Clear(); b.AsSpan(KartLoadout.Read(after).Record.WordOffset(0), 40).Clear();
        var memory = before.Graph.Sections.Single(s => s.Name == "graph_memory"); int end = memory.Offset + memory.Size;
        Assert(a.AsSpan(names.Offset + names.Size, end - names.Offset - names.Size).SequenceEqual(b.AsSpan(newNames.Offset + newNames.Size, end - names.Offset - names.Size)), "Unrelated graph bytes changed after shift.");
        Assert(b.AsSpan(end + delta).IndexOfAnyExcept((byte)0) < 0, "Padding changed.");
    }
}
