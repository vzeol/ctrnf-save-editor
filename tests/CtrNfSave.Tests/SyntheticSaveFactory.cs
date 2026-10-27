using System.Text;
using CtrNfSave;

namespace CtrNfSave.Tests;

/// <summary>Generated parser fixtures, not game-playable saves. No bytes come from a user's profile.</summary>
internal sealed class SyntheticSaveFactory
{
    private const uint Null = 0xFFFFFFFE;
    private static readonly string[] MetaNames = ["igObjectRefMetaField", "igBoolMetaField", "igIntMetaField", "igUnsignedIntMetaField", "igUnsignedShortMetaField", "igCharMetaField", "igStringMetaField", "igNameMetaField"];
    private readonly List<string> strings = [""];
    private readonly List<string> classes = ["igObjectDirEntry", "igMemoryDirEntry"];
    private readonly List<Allocation> allocations = [];
    private readonly List<(uint Hash, string Name)> graphNames = [];
    private sealed record Allocation(bool Memory, int Type, uint[] Words, byte[] Data, int Count = 0, int Meta = 0, int Stride = 0);

    private uint Text(string value)
    {
        int index = strings.IndexOf(value);
        if (index >= 0) return (uint)index;
        strings.Add(value);
        return (uint)strings.Count - 1;
    }

    private int Type(string name)
    {
        int index = classes.IndexOf(name);
        if (index >= 0) return index;
        classes.Add(name);
        return classes.Count - 1;
    }

    private uint Obj(string type, params uint[] words)
    {
        allocations.Add(new(false, Type(type), words, []));
        return (uint)allocations.Count - 1;
    }

    private uint Memory(int meta, int count, int stride, byte[] data)
    {
        allocations.Add(new(true, 1, [], data, count, meta, stride));
        return (uint)allocations.Count - 1;
    }

    private uint Words(int meta, params uint[] values) => Memory(meta, values.Length, 4, Bytes(w => { foreach (uint value in values) w.Write(value); }));
    private static uint Float(float value) => BitConverter.SingleToUInt32Bits(value);
    private static byte[] Bytes(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true)) write(writer);
        return stream.ToArray();
    }

    public static byte[] Global()
    {
        var b = new SyntheticSaveFactory();
        uint[] root = Enumerable.Repeat(Null, 24).ToArray();
        root[3] = root[4] = root[5] = root[6] = root[7] = 0;
        root[10] = 838;
        uint global = b.Obj("COctaneGlobalSaveData", root);
        b.graphNames.AddRange([(0x6236AC84, "SystemData"), (0xA2F77BC3, "SystemData_availableDrivers_5"), (0xA30D1341, "SystemData_availableKarts_7")]);
        uint[] emptyKart = [0, 0, uint.MaxValue, 0, 0, uint.MaxValue, 0, uint.MaxValue, uint.MaxValue, uint.MaxValue];
        uint activeKart = b.Obj("COctaneCustomizationSaveData", 0xA2F77BC3, 0x6236AC84, 5, 0xA30D1341, 0x6236AC84, 15, 6, uint.MaxValue, 57, uint.MaxValue);
        root[0] = b.Obj("COctaneCustomizationSaveDataList", 4, 4, b.Words(0,
            b.Obj("COctaneCustomizationSaveData", emptyKart), activeKart,
            b.Obj("COctaneCustomizationSaveData", emptyKart), b.Obj("COctaneCustomizationSaveData", emptyKart)));
        uint[] mask = new uint[16];
        mask[0] = 1u << 5; // Deliberately unknown/reserved challenge, preserved by bulk edits.
        mask[15] = 1u << 31;
        root[16] = b.Obj("igBitMask", 16, 16, b.Words(3, mask), 512);

        uint marketData = b.Memory(4, 2, 2, Bytes(w => { w.Write((ushort)618); w.Write((ushort)65000); }));
        uint market = b.Obj("igUnsignedShortList", 2, 2, marketData);
        byte[] patch = new byte[3352];
        patch[3300] = 0x3E;
        patch[3348] = 0xA5; // Unknown byte sentinel outside the mapped reward fields.
        uint patchList = b.Obj("igCharList", 3352, 3352, b.Memory(5, patch.Length, 1, patch));
        b.Text("OctanePatchSaveGameData");
        b.Obj("igObjectList", 3, 3, b.Words(0, global, market, patchList));
        uint[] names = [b.Text("GlobalProfile"), 0, b.Text("LocalMarketplaceData"), 0, b.Text("PatchTU1"), 0];
        uint nameMemory = b.Memory(7, 3, 8, Bytes(w => { foreach (uint value in names) w.Write(value); }));
        b.Obj("igNameList", 3, 3, nameMemory);

        uint[] counts = new uint[131], driverKeys = new uint[131];
        uint crash = b.Text("DriverCrash");
        uint hash = 2166136261;
        foreach (byte c in Encoding.ASCII.GetBytes("DriverCrash")) hash = unchecked((hash ^ c) * 16777619);
        int bucket = (int)(hash % 131);
        driverKeys[bucket] = crash;
        counts[bucket] = 38;
        root[18] = b.Obj("igStringIntHashTable", b.Words(2, counts), b.Words(6, driverKeys), 1, 1, Float(0.5f));

        uint player = b.Text("Synthetic test player"), coco = b.Text("DriverCoco"), track = b.Text("octane/t111_crash_cove/t111_crash_cove");
        uint ghost = b.Obj("CGhost", 3, 10, 8, Float(86.98f), Null, Null);
        uint progress = b.Obj("CGhostSavedInfo", 1, ghost);
        root[1] = b.Obj("CZoneInfoGhostHashTable", b.Words(0, progress, 0, 0, 0, 0, 0, 0, 0), b.Words(6, track, 0, 0, 0, 0, 0, 0, 0), 1, 1, Float(0.5f));
        uint race1 = b.Obj("COctaneTimeTrialData", Float(100.125f), crash, player);
        uint race2 = b.Obj("COctaneTimeTrialData", Float(110.25f), coco, player);
        uint relic = b.Obj("COctaneTimeTrialData", Float(60.5f), crash, player);
        uint lap = b.Obj("COctaneTimeTrialData", Float(30.125f), crash, player);
        uint raceList = b.Obj("COctaneTimeTrialList", 2, 2, b.Words(0, race1, race2));
        uint relicList = b.Obj("COctaneTimeTrialList", 1, 1, b.Words(0, relic));
        uint map = b.Obj("COctaneMapTimeTrialData", raceList, relicList, lap);
        root[2] = b.Obj("COctaneMapTimeTrialHashTable", b.Words(0, map), b.Words(6, track), 1, 1, Float(0.5f));
        return b.Build(7340032, new() { ["most_recent_save_slot"] = 2 });
    }

    public static byte[] Adventure()
    {
        var b = new SyntheticSaveFactory();
        uint[] slot = new uint[23];
        slot[17] = uint.MaxValue;
        slot[21] = 1;
        b.Obj("COctaneSaveSlotData", slot);
        var zones = new List<uint>();
        foreach (var track in AdventureTracks.All)
        {
            uint[] state = new uint[15];
            state[13] = 0x80000000; // Unmapped bit must survive award edits.
            uint session = b.Obj("CZoneInfoUserSession", state);
            zones.Add(b.Obj("CZoneInfoSave", b.Text("octane/" + track.Id + "/" + track.Id), session));
        }
        b.Obj("CZoneInfoSaveList", (uint)zones.Count, (uint)zones.Count, b.Words(0, zones.ToArray()));
        var header = new Dictionary<string, int>
        {
            ["percent_complete"] = 0,
            ["adventure_difficulty"] = 1,
            ["trophy_Count"] = 0,
            ["relic_count"] = 0,
            ["ctr_token_count"] = 0,
            ["boss_key_count"] = 0,
            ["farthest_progression_index"] = 0,
            ["active_progression_index"] = 0,
            ["previous_level_progression_index"] = 0,
            ["farthest_chapter"] = 0,
            ["story_completed"] = 0,
            ["credits_watched"] = 0
        };
        return b.Build(153600, header);
    }

    private byte[] Build(int fileSize, Dictionary<string, int> headerFields)
    {
        byte[] stringSection = Bytes(w =>
        {
            w.Write(0u); w.Write(strings.Count);
            foreach (string text in strings)
            {
                byte[] encoded = Encoding.UTF8.GetBytes(text + "\0"); w.Write(encoded.Length); w.Write(encoded);
            }
        });
        BitConverter.GetBytes(stringSection.Length).CopyTo(stringSection, 0);
        byte[] meta = Bytes(w =>
        {
            foreach (string name in MetaNames) { w.Write(Encoding.UTF8.GetByteCount(name) + 1); w.Write(0); w.Write(0); }
            foreach (string name in MetaNames) w.Write(Encoding.UTF8.GetBytes(name + "\0"));
        });
        byte[] classSection = Bytes(w =>
        {
            foreach (string name in classes) { w.Write(Encoding.UTF8.GetByteCount(name) + 1); w.Write(0); w.Write(0); w.Write(0); w.Write(-1); w.Write(0); }
            foreach (string name in classes) w.Write(Encoding.UTF8.GetBytes(name + "\0"));
        });
        byte[] directories = Bytes(w =>
        {
            foreach (var allocation in allocations)
            {
                w.Write(allocation.Memory ? 1 : 0);
                if (allocation.Memory)
                {
                    w.Write(28); w.Write(0); w.Write(allocation.Count); w.Write(allocation.Meta); w.Write(0); w.Write(allocation.Stride);
                }
                else { w.Write(16); w.Write(0); w.Write(allocation.Type); }
            }
        });
        byte[] order = Bytes(w =>
        {
            w.Write(4 + allocations.Count * 2); w.Write(allocations.Count);
            for (int i = 0; i < allocations.Count; i++) w.Write((ushort)i);
        });
        byte[] objects = Bytes(w =>
        {
            foreach (var allocation in allocations.Where(a => !a.Memory))
            {
                w.Write(allocation.Type); w.Write(8 + allocation.Words.Length * 4);
                foreach (uint value in allocation.Words) w.Write(value);
            }
        });
        byte[] memory = Bytes(w =>
        {
            foreach (var allocation in allocations.Where(a => a.Memory))
            {
                w.Write(allocation.Data);
                for (int pad = allocation.Data.Length; pad % 4 != 0; pad++) w.Write((byte)0);
            }
        });
        byte[] headerNames = Bytes(w => { foreach (string name in headerFields.Keys) w.Write(Encoding.UTF8.GetBytes(name + "\0")); });
        byte[] headerValues = Bytes(w => { foreach (int value in headerFields.Values) w.Write(value); });
        byte[] prefix = Bytes(w =>
        {
            foreach (uint value in new uint[] { (uint)directories.Length, (uint)allocations.Count, (uint)classSection.Length, (uint)classes.Count, (uint)objects.Length,
                (uint)allocations.Count(a => !a.Memory), (uint)memory.Length, (uint)allocations.Count(a => a.Memory), (uint)meta.Length, (uint)MetaNames.Length, 0xFADA, 0xB400000C, 3 }) w.Write(value);
            w.Write(headerFields.Count * 12);
            int nameOffset = 0, valueOffset = 0;
            foreach (string name in headerFields.Keys) { w.Write(0); w.Write(nameOffset); w.Write(valueOffset); nameOffset += Encoding.UTF8.GetByteCount(name) + 1; valueOffset += 4; }
            w.Write(headerNames.Length); w.Write(headerNames); w.Write(headerValues.Length); w.Write(headerValues);
            w.Write(stringSection); w.Write(meta); w.Write(classSection);
            w.Write(12 + graphNames.Sum(n => 4 + Encoding.UTF8.GetByteCount(n.Name) + 1)); w.Write(0); w.Write(graphNames.Count);
            foreach (var n in graphNames) w.Write(n.Hash);
            foreach (var n in graphNames) w.Write(Encoding.UTF8.GetBytes(n.Name + "\0"));
            w.Write(4); w.Write(4); // Empty pools and pool-index sections.
            w.Write(directories); w.Write(order); w.Write(objects); w.Write(memory);
        });
        var result = new byte[fileSize]; prefix.CopyTo(result, 0); return result;
    }
}
