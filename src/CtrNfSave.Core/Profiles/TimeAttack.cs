using System.Buffers.Binary;
using System.Globalization;

namespace CtrNfSave;

public sealed record GhostProgress(string Track, int Level, float? PlayerSeconds, string Key);
public sealed record EditableTime(string Track, string Category, int Rank, float Seconds, string Driver, string Name, string Key);

/// <summary>Existing records only. Never creates or rewrites a recorded ghost trajectory.</summary>
public static class TimeAttack
{
    private static IEnumerable<(string Track, GraphObject Info)> GhostEntries(Graph graph, byte[] bytes)
    {
        var root = graph.Objects.SingleOrDefault(o => o.ClassName == "COctaneGlobalSaveData");
        if (root is null) yield break;
        var table = graph.Objects.SingleOrDefault(o => o.Id == root.Words[1]);
        if (table is null || table.ClassName != "CZoneInfoGhostHashTable" || table.Words.Length != 5 ||
            !graph.Memories.TryGetValue((int)table.Words[0], out var values) ||
            !graph.Memories.TryGetValue((int)table.Words[1], out var keys) || values.Count != keys.Count ||
            values.Meta != "igObjectRefMetaField" || keys.Meta != "igStringMetaField")
            throw new SaveFormatException("Unsupported ghost progress table.");
        var objects = graph.Objects.ToDictionary(o => o.Id);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < keys.Count; i++)
        {
            uint name = Binary.U32(bytes, keys.Offset + i * 4);
            if (name == 0) continue;
            if (name >= graph.Strings.Count || !objects.TryGetValue((int)Binary.U32(bytes, values.Offset + i * 4), out var info) ||
                info.ClassName != "CGhostSavedInfo" || info.Words.Length != 2)
                throw new SaveFormatException("Invalid saved ghost entry.");
            string track = graph.Strings[(int)name].Split('/')[^1];
            if (!seen.Add(track)) throw new SaveFormatException("Ambiguous ghost track name.");
            yield return (track, info);
        }
    }
    public static IReadOnlyList<GhostProgress> Ghosts(Save save)
    {
        GlobalProfile.Require(save);
        return GhostEntries(save.Graph, save.Serialize()).Select(entry =>
        {
            var ghost = save.Graph.Objects.SingleOrDefault(o => o.Id == entry.Info.Words[1] && o.ClassName == "CGhost");
            float? seconds = ghost is not null && ghost.Words.Length == 6 ? BitConverter.UInt32BitsToSingle(ghost.Words[3]) : null;
            return new GhostProgress(entry.Track, unchecked((int)entry.Info.Words[0]), seconds,
                "global.ghost." + entry.Track + ".level");
        }).OrderBy(g => g.Track, StringComparer.Ordinal).ToArray();
    }
    private static IEnumerable<(EditableTime Time, GraphObject Record)> RecordEntries(Graph graph, byte[] bytes)
    {
        var objects = graph.Objects.ToDictionary(o => o.Id);
        foreach (var table in graph.Objects.Where(o => o.ClassName == "COctaneMapTimeTrialHashTable"))
        {
            if (!graph.Memories.TryGetValue((int)table.Words[0], out var values) ||
                !graph.Memories.TryGetValue((int)table.Words[1], out var keys) || values.Count != keys.Count)
                throw new SaveFormatException("Unsupported time record table.");
            for (int i = 0; i < keys.Count; i++)
            {
                uint name = Binary.U32(bytes, keys.Offset + i * 4);
                if (name == 0) continue;
                if (name >= graph.Strings.Count || !objects.TryGetValue((int)Binary.U32(bytes, values.Offset + i * 4), out var map) ||
                    map.ClassName != "COctaneMapTimeTrialData" || map.Words.Length != 3)
                    throw new SaveFormatException("Invalid time record map.");
                string track = graph.Strings[(int)name].Split('/')[^1];
                for (int category = 0; category < 3; category++)
                {
                    var holder = objects[(int)map.Words[category]];
                    string kind = category switch { 0 => "race", 1 => "relic", _ => "best_lap" };
                    var records = new List<GraphObject>();
                    if (category == 2) records.Add(holder);
                    else
                    {
                        if (holder.ClassName != "COctaneTimeTrialList" || holder.Words.Length != 3)
                            throw new SaveFormatException("Invalid time record list.");
                        if (holder.Words[0] == 0) continue;
                        if (!graph.Memories.TryGetValue((int)holder.Words[2], out var memory) || holder.Words[0] > memory.Count)
                            throw new SaveFormatException("Invalid time record allocation.");
                        for (int n = 0; n < holder.Words[0]; n++) records.Add(objects[(int)Binary.U32(bytes, memory.Offset + n * 4)]);
                    }
                    for (int rank = 0; rank < records.Count; rank++)
                    {
                        var record = records[rank];
                        if (record.ClassName != "COctaneTimeTrialData" || record.Words.Length != 3 ||
                            record.Words[1] >= graph.Strings.Count || record.Words[2] >= graph.Strings.Count)
                            throw new SaveFormatException("Invalid time record.");
                        float seconds = BitConverter.UInt32BitsToSingle(record.Words[0]);
                        if (seconds == float.MaxValue) continue;
                        if (!float.IsFinite(seconds) || seconds < 0) throw new SaveFormatException("Invalid record time.");
                        yield return (new(track, kind, rank + 1, seconds, graph.Strings[(int)record.Words[1]], graph.Strings[(int)record.Words[2]],
                            $"global.time_attack.{track}.{kind}.{rank + 1}"), record);
                    }
                }
            }
        }
    }
    public static IReadOnlyList<EditableTime> Records(Save save)
    {
        GlobalProfile.Require(save);
        return RecordEntries(save.Graph, save.Serialize()).Select(e => e.Time).OrderBy(r => r.Track, StringComparer.Ordinal).ThenBy(r => r.Category).ThenBy(r => r.Rank).ToArray();
    }
    internal static void Interpret(Graph graph, byte[] bytes)
    {
        if (!graph.Objects.Any(o => o.ClassName == "COctaneGlobalSaveData")) return;
        foreach (var (track, info) in GhostEntries(graph, bytes))
            graph.Fields.Add(new("global.ghost." + track + ".level", "ghost-level", info.WordOffset(0), 4, unchecked((int)info.Words[0]),
                "Next challenge ghost index: 0 N. Tropy, 1 Oxide, 2 Velo, 3 developer, 4 all beaten. Existing tracks only; replay untouched."));
        foreach (var (time, record) in RecordEntries(graph, bytes))
            graph.Fields.Add(new(time.Key, "time-record", record.WordOffset(0), 4, time.Seconds,
                "Seconds; existing leaderboard entry, not the stored player ghost trajectory or time."));
    }
    public static Save Set(Save save, string key, string input)
    {
        GlobalProfile.Require(save);
        var field = save.Graph.Fields.SingleOrDefault(f => f.Key == key) ?? throw new SaveFormatException("No existing record for this track.");
        var bytes = save.Serialize();
        if (field.Type == "ghost-level")
        {
            if (!int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) || level is < 0 or > 4 ||
                (int)field.Value is < 0 or > 4) throw new SaveFormatException("Ghost progress must be a supported level from 0 to 4.");
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(field.Offset, 4), level);
        }
        else if (field.Type == "time-record")
        {
            float seconds = ParseSeconds(input);
            var records = Records(save); var current = records.Single(r => r.Key == key);
            if (records.Any(r => r.Track == current.Track && r.Category == current.Category &&
                (r.Rank < current.Rank && r.Seconds > seconds || r.Rank > current.Rank && r.Seconds < seconds)))
                throw new SaveFormatException("The time must preserve the existing ranking order.");
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(field.Offset, 4), seconds);
        }
        else throw new SaveFormatException("Unsupported time attack field.");
        var result = Save.Parse(bytes); GlobalProfile.Require(result); return result;
    }
    private static float ParseSeconds(string input)
    {
        string[] parts = input.Trim().Split(':');
        if (parts.Length is < 1 or > 3) throw new SaveFormatException("Use seconds, m:ss.cc or m:ss:cc.");
        float seconds;
        if (parts.Length == 1)
        {
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                throw new SaveFormatException("Enter a valid time in seconds or m:ss.cc.");
        }
        else
        {
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int minutes) ||
                !float.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out float remainder) || remainder is < 0 or >= 60 || !float.IsFinite(remainder))
                throw new SaveFormatException("Invalid minutes or seconds.");
            float fraction = 0;
            if (parts.Length == 3)
            {
                if (remainder != MathF.Truncate(remainder) || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int centiseconds) || centiseconds is < 0 or > 99)
                    throw new SaveFormatException("Invalid centiseconds.");
                fraction = centiseconds / 100f;
            }
            seconds = minutes * 60f + remainder + fraction;
        }
        if (!float.IsFinite(seconds) || seconds <= 0 || seconds == float.MaxValue)
            throw new SaveFormatException("Enter a finite, positive time.");
        return seconds;
    }
}
