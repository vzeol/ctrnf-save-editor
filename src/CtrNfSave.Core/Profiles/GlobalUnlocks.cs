using System.Buffers.Binary;
using System.Text.Json;

namespace CtrNfSave;

public sealed record MarketplaceEntry(int Id, string Handle, string Namespace, string Type, string NameEn, string NameFr, string? UnlockType);
public sealed record DriverEntry(int Index, string NameEn, string NameFr, string Driver, string? Condition, int? ChallengeBit, int[] MarketplaceIds)
{
    public string? Key => ChallengeBit is int bit ? GlobalProfile.CharacterKey(bit) :
        Driver == "DriverPentaPenguin" ? "global.character.penta_penguin" :
        Driver == "DriverNTropy" ? "global.character.n_tropy" :
        MarketplaceIds.Length == 1 ? GlobalUnlocks.ItemKey(MarketplaceIds[0]) : null;
}

/// <summary>PS4 1.21 catalogue: native unlock checks plus authored SystemData/MarketplaceData.</summary>
public static class GlobalUnlocks
{
    private static T[] Read<T>(string name)
    {
        using var stream = typeof(GlobalUnlocks).Assembly.GetManifestResourceStream("CtrNfSave.Core.Data." + name)
            ?? throw new InvalidOperationException("Missing unlock catalogue.");
        return JsonSerializer.Deserialize<T[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
    public static IReadOnlyList<MarketplaceEntry> Items { get; } = Array.AsReadOnly(Read<MarketplaceEntry>("marketplace.json"));
    public static IReadOnlyList<DriverEntry> Drivers { get; } = Array.AsReadOnly(Read<DriverEntry>("drivers.json"));
    public static string ItemKey(int id) => "global.marketplace.item_" + id;
    public static bool Boolean(string input) => input.ToLowerInvariant() switch
    { "true" or "1" => true, "false" or "0" => false, _ => throw new SaveFormatException("Expected true/false or 1/0.") };

    private static (GraphObject List, GraphMemory Memory) Layout(Graph graph)
    {
        // LocalMarketplaceData is a named root, not one of the challenge masks.
        var lists = graph.Objects.Where(o => o.ClassName == "igUnsignedShortList").ToArray();
        if (!graph.Strings.Contains("LocalMarketplaceData") || lists.Length != 1)
            throw new SaveFormatException("Local marketplace list unavailable or ambiguous.");
        var list = lists[0];
        if (list.Words.Length != 3 || list.Words[0] != list.Words[1])
            throw new SaveFormatException("Unsupported local marketplace list layout.");
        GraphMemory memory;
        if (list.Words[0] == 0 && list.Words[2] == uint.MaxValue)
            memory = new(-1, "igUnsignedShortMetaField", 0, 0, 2);
        else if (!graph.Memories.TryGetValue((int)list.Words[2], out memory!) || memory.Meta != "igUnsignedShortMetaField" ||
            memory.Stride != 2 || memory.Count != list.Words[0])
            throw new SaveFormatException("Unsupported local marketplace list layout.");
        // Verify its position in the named root directory, rather than trusting its class alone.
        var rootList = graph.Objects.SingleOrDefault(o => o.ClassName == "igObjectList");
        var nameLists = graph.Objects.Where(o => o.ClassName == "igNameList").ToArray();
        if (rootList is null || !graph.Memories.TryGetValue((int)rootList.Words[2], out var roots))
            throw new SaveFormatException("Missing marketplace root directory.");
        var raw = graph.SourceBytes;
        int index = Enumerable.Range(0, roots.Count).Single(i => Binary.U32(raw, roots.Offset + i * 4) == list.Id);
        bool named = nameLists.Any(n => graph.Memories.TryGetValue((int)n.Words[2], out var names) && index < names.Count &&
            Binary.U32(raw, names.Offset + index * names.Stride) < graph.Strings.Count &&
            graph.Strings[(int)Binary.U32(raw, names.Offset + index * names.Stride)] == "LocalMarketplaceData");
        if (!named) throw new SaveFormatException("Marketplace root name does not match.");
        return (list, memory);
    }
    public static IReadOnlyList<ushort> Owned(Save save)
    {
        GlobalProfile.Require(save); var (_, memory) = Layout(save.Graph); var b = save.Serialize();
        return Enumerable.Range(0, memory.Count).Select(i => Binary.U16(b, memory.Offset + i * 2)).ToArray();
    }
    internal static void Interpret(Graph graph, byte[] bytes)
    {
        if (!graph.Objects.Any(o => o.ClassName == "COctaneGlobalSaveData")) return;
        var root = graph.Objects.Single(o => o.ClassName == "COctaneGlobalSaveData");
        foreach (var (key, word) in new[] { ("global.character.penta_penguin", 3), ("global.character.n_tropy", 6) })
            graph.Fields.Add(new(key, "unlock-bool", root.WordOffset(word), 1, (root.Words[word] & 255) != 0,
                "Native unlock condition; N. Tropy reward flag does not fabricate ghost times."));
        try
        {
            var (_, memory) = Layout(graph);
            var owned = Enumerable.Range(0, memory.Count).Select(i => (int)Binary.U16(bytes, memory.Offset + i * 2)).ToHashSet();
            foreach (var item in Items)
                graph.Fields.Add(new(ItemKey(item.Id), "marketplace-item", memory.Offset, 0, owned.Contains(item.Id),
                    item.NameEn + "; local inventory ID; server inventory is separate"));
        }
        catch (SaveFormatException) { /* Older profiles without this root retain existing support. */ }
    }
    public static Save SetMarketplace(Save save, IEnumerable<int> add, IEnumerable<int> remove)
    {
        GlobalProfile.Require(save); var (list, memory) = Layout(save.Graph);
        var allowed = Items.Select(i => i.Id).ToHashSet();
        int[] added = add.Distinct().ToArray(), removed = remove.Distinct().ToArray();
        if (added.Concat(removed).Any(i => !allowed.Contains(i))) throw new SaveFormatException("Unknown marketplace item ID.");
        var old = Owned(save); var values = old.Where(i => !removed.Contains(i)).ToList();
        foreach (int id in added) if (!values.Contains((ushort)id)) values.Add((ushort)id);
        if (old.SequenceEqual(values)) return save;
        if (memory.Id == -1) return AllocateMarketplace(save, list, values);
        byte[] bytes = save.Serialize(); var graph = save.Graph;
        var alloc = graph.Sections.Single(s => s.Name == "graph_allocations");
        int directoryIndex = Binary.U16(bytes, alloc.Offset + 8 + memory.Id * 2);
        if (Enumerable.Range(0, alloc.Count).Count(i => Binary.U16(bytes, alloc.Offset + 8 + i * 2) == directoryIndex) != 1)
            throw new SaveFormatException("Shared marketplace allocation descriptor; resizing refused.");
        int directory = graph.Sections.Single(s => s.Name == "graph_directories").Offset;
        for (int i = 0; i < directoryIndex; i++) directory += checked((int)Binary.U32(bytes, directory + 4));
        int oldSize = (memory.Count * 2 + 3) & ~3, newSize = (values.Count * 2 + 3) & ~3, delta = newSize - oldSize;
        var section = graph.Sections.Single(s => s.Name == "graph_memory");
        int end = section.Offset + section.Size;
        if (end + delta > bytes.Length || bytes.AsSpan(end).IndexOfAnyExcept((byte)0) >= 0)
            throw new SaveFormatException("Insufficient verified zero padding for marketplace inventory.");
        var output = new byte[bytes.Length];
        bytes.AsSpan(0, memory.Offset).CopyTo(output);
        for (int i = 0; i < values.Count; i++) BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(memory.Offset + i * 2, 2), values[i]);
        bytes.AsSpan(memory.Offset + oldSize, end - memory.Offset - oldSize).CopyTo(output.AsSpan(memory.Offset + newSize));
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(directory + 12, 4), values.Count);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(list.WordOffset(0), 4), values.Count);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(list.WordOffset(1), 4), values.Count);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(24, 4), section.Size + delta);
        var result = Save.Parse(output); GlobalProfile.Require(result);
        if (!Owned(result).SequenceEqual(values)) throw new SaveFormatException("Marketplace rebuild verification failed.");
        return result;
    }

    private static Save AllocateMarketplace(Save save, GraphObject list, List<ushort> values)
    {
        var graph = save.Graph; byte[] bytes = save.Serialize();
        var directory = graph.Sections.Single(s => s.Name == "graph_directories");
        var alloc = graph.Sections.Single(s => s.Name == "graph_allocations");
        var memory = graph.Sections.Single(s => s.Name == "graph_memory");
        int meta = graph.MetaFields.IndexOf("igUnsignedShortMetaField");
        if (meta < 0 || directory.Count > ushort.MaxValue)
            throw new SaveFormatException("Marketplace allocation metadata unavailable.");
        // Reuse this file's primitive-memory descriptor, including its pool and flags.
        // The observed envelopes use 28- or 32-byte descriptors; other layouts stay read-only.
        byte[]? descriptor = null;
        for (int at = directory.Offset; at < directory.Offset + directory.Size;)
        {
            int size = checked((int)Binary.U32(bytes, at + 4));
            int type = checked((int)Binary.U32(bytes, at));
            if (graph.Classes[type].Name == "igMemoryDirEntry" && size is 28 or 32 &&
                graph.MetaFields[(int)Binary.U32(bytes, at + 16)] == "igCharMetaField" && Binary.U32(bytes, at + 24) == 1)
            { descriptor = bytes.AsSpan(at, size).ToArray(); break; }
            at += size;
        }
        if (descriptor is null) throw new SaveFormatException("No verified primitive-memory descriptor for empty marketplace.");
        int dataSize = (values.Count * 2 + 3) & ~3, growth = descriptor.Length + 2 + dataSize;
        int end = memory.Offset + memory.Size;
        if (end + growth > bytes.Length || bytes.AsSpan(end).IndexOfAnyExcept((byte)0) >= 0)
            throw new SaveFormatException("Insufficient verified zero padding for empty marketplace allocation.");
        void Write(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at, 4), value);
        Write(0, directory.Size + descriptor.Length); Write(4, directory.Count + 1);
        Write(24, memory.Size + dataSize); Write(28, memory.Count + 1);
        Write(alloc.Offset, checked((int)Binary.U32(bytes, alloc.Offset)) + 2); Write(alloc.Offset + 4, alloc.Count + 1);
        Write(list.WordOffset(0), values.Count); Write(list.WordOffset(1), values.Count); Write(list.WordOffset(2), alloc.Count);
        BinaryPrimitives.WriteInt32LittleEndian(descriptor.AsSpan(12, 4), values.Count);
        BinaryPrimitives.WriteInt32LittleEndian(descriptor.AsSpan(16, 4), meta);
        BinaryPrimitives.WriteInt32LittleEndian(descriptor.AsSpan(24, 4), 2);
        int dirEnd = directory.Offset + directory.Size, allocEnd = alloc.Offset + 8 + alloc.Count * 2;
        byte[] output = new byte[bytes.Length];
        bytes.AsSpan(0, dirEnd).CopyTo(output);
        descriptor.CopyTo(output, dirEnd);
        bytes.AsSpan(dirEnd, allocEnd - dirEnd).CopyTo(output.AsSpan(dirEnd + descriptor.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(allocEnd + descriptor.Length, 2), (ushort)directory.Count);
        bytes.AsSpan(allocEnd, end - allocEnd).CopyTo(output.AsSpan(allocEnd + descriptor.Length + 2));
        for (int i = 0; i < values.Count; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(end + descriptor.Length + 2 + i * 2, 2), values[i]);
        var result = Save.Parse(output); GlobalProfile.Require(result);
        if (!Owned(result).SequenceEqual(values)) throw new SaveFormatException("Empty marketplace rebuild verification failed.");
        return result;
    }
    public static Save AllCharacters(Save save)
    {
        var marketplace = Drivers.SelectMany(d => d.MarketplaceIds);
        Save result = SetMarketplace(save, marketplace, []);
        foreach (var driver in Drivers.Where(d => d.Key is not null && d.MarketplaceIds.Length == 0))
            result = result.Set(driver.Key!, "true");
        return result;
    }
}
