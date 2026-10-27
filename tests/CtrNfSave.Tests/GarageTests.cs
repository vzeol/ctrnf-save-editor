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
            Assert(KartLoadout.DriverName("SystemData_availableDrivers_58") == "Crunch Bandicoot", "Captured Crunch identity lost.");
            Assert(KartLoadout.DriverName("SystemData_availableDrivers_0") == "SystemData_availableDrivers_0" &&
                KartLoadout.Ownership(Global(), current.Preset with { Driver = "SystemData_availableDrivers_0" }).Driver is null, "Unverified catalogue ordinal used as a loadout index.");
            Assert(KartLoadout.Ownership(Global(), current.Preset).Driver is false, "Known locked driver was not detected.");
            Assert(KartLoadout.Ownership(Global(), current.Preset).Chassis is null, "Unverified chassis ownership guessed.");
            var unlocked = Global().Set(GlobalProfile.CharacterKey(15), "true");
            Assert(KartLoadout.Ownership(unlocked, current.Preset).Driver is true, "Existing driver ownership predicate not used.");
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
