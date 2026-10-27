using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CtrNfSave;

internal static class Program
{
    private const string Help = """
ctrnf-save 0.1.0-preview.1 — decrypted CTR Nitro-Fueled PS4 save inspector/editor

  ctrnf-save dump <file> [--json]
  ctrnf-save set <file> <key> <value> -o <new-file>
  ctrnf-save set <file> <key> <value> --in-place
  ctrnf-save diff <a> <b> [--json]
  ctrnf-save check <file>
  ctrnf-save normalize <file> -o <out>

Use header keys from dump, optionally prefixed with 'header.'.
Track trophy/token booleans, relic tiers (none/sapphire/gold/platinum),
raw other flags, numeric edits and same-byte-length strings are supported.
New outputs never replace an existing file. --in-place creates <file>.bak
first, refuses an existing backup, and replaces via a temporary file.
Global profiles support global.wumpa_coins, global.golden_egg.bit_1..5,
named character conditions, global.marketplace.item_ID, global.skin.DRIVER.INDEX
and global.driver_wins.DRIVER. Use dump --json to find each supported key.
Legacy global.character.bit_N keys represent global challenges, not roster slots.
Global exports and in-place saves always create timestamped .bak backups.
Related win-based skins share their driver's counter. Server inventory is separate.
Existing records: global.time_attack.TRACK.race|relic|best_lap.RANK.
Existing ghost challenges: global.ghost.TRACK.level (0 N. Tropy to 4 all beaten).
Times accept seconds, m:ss.cc or m:ss:cc. Recorded replays stay unchanged.
Kart customization: global.customization.HANDLE. Unknown conditions are read-only.
Shared time-attack rewards: global.reward.all_oxide_times, velo_times, developer_times.
All named unlocks: set global.unlock_all true (unlock) or false (lock).
Base content, unknown flags, coins, eggs, records and Adventure slots are preserved.
The samples/ and target/ input directories are protected against writes.
Progress summaries alone do not complete events or unlock items.
""";

    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        // Keep Unicode arrows and names readable on Windows OEM consoles.
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h" or "help") { Console.WriteLine(Help); return 0; }
            switch (args[0])
            {
                case "--version": Console.WriteLine("ctrnf-save 0.1.0-preview.1"); return 0;
                case "dump":
                    Require(args.Length is 2 or 3 && (args.Length != 3 || args[2] == "--json"), "dump <file> [--json]");
                    var save = Save.Load(args[1]);
                    Console.Write(args.Length == 3 ? JsonOutput.Render(save.ToJson()) + "\n" : save.DumpText()); return 0;
                case "check":
                    Require(args.Length == 2, "check <file>");
                    var issues = AdventureRules.Validate(Save.Load(args[1]));
                    foreach (var issue in issues) Console.WriteLine($"{issue.Severity} [{issue.Code}]: {issue.Message}");
                    Console.WriteLine($"{issues.Count(i => i.Severity == "error")} error(s), {issues.Count(i => i.Severity == "warning")} warning(s)");
                    return issues.Any(i => i.Severity == "error") ? 1 : 0;
                case "normalize":
                    Require(args.Length == 4 && args[2] == "-o", "normalize <file> -o <out>");
                    var fixedSave = AdventureRules.Normalize(Save.Load(args[1]));
                    FileWriter.New(args[3], fixedSave.Serialize());
                    foreach (var issue in AdventureRules.Validate(fixedSave)) Console.WriteLine($"{issue.Severity}: {issue.Message}");
                    Console.WriteLine("Wrote " + Path.GetFullPath(args[3])); return 0;
                case "set": return SetCommand(args);
                case "diff":
                    Require(args.Length is 3 or 4 && (args.Length != 4 || args[3] == "--json"), "diff <a> <b> [--json]");
                    var differences = Save.Diff(Save.Load(args[1]), Save.Load(args[2]));
                    if (args.Length == 4) Console.WriteLine(JsonOutput.Render(differences));
                    else
                    {
                        foreach (object entry in differences)
                        {
                            using var json = JsonDocument.Parse(JsonOutput.Render(entry));
                            var e = json.RootElement;
                            Console.WriteLine($"{e.GetProperty("key").GetString()}: {e.GetProperty("before").GetRawText()} -> {e.GetProperty("after").GetRawText()}");
                        }
                        Console.WriteLine($"{differences.Count} decoded value(s) differ. Unknown bytes are outside semantic coverage.");
                    }
                    return 0;
                default: throw new SaveFormatException($"Unknown command '{args[0]}'. Use --help.");
            }
        }
        catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException or ArgumentException or OverflowException or JsonException)
        { Console.Error.WriteLine("Error: " + e.Message); return 2; }
    }
    private static void Require(bool condition, string usage)
    { if (!condition) throw new SaveFormatException("Usage: ctrnf-save " + usage); }
    private static int SetCommand(string[] args)
    {
        Require(args.Length >= 5, "set <file> <key> <value> (-o <new-file> | --in-place)");
        string? output = null; bool inPlace = false;
        for (int i = 4; i < args.Length; i++)
        {
            if (args[i] is "-o" or "--output")
            {
                Require(output is null && i + 1 < args.Length, "set <file> <key> <value> -o <new-file>"); output = args[++i];
            }
            else if (args[i] == "--in-place" && !inPlace) inPlace = true;
            else throw new SaveFormatException($"Unknown or duplicate option '{args[i]}'.");
        }
        Require(inPlace != (output is not null), "set <file> <key> <value> (-o <new-file> | --in-place)");
        Save original = Save.Load(args[1]);
        bool adventure = args[2].StartsWith("slot.gem_", StringComparison.Ordinal) || args[2] is "slot.oxide_first" or "slot.oxide_second" || args[2].StartsWith("track.", StringComparison.Ordinal) && !args[2].EndsWith(".flags", StringComparison.Ordinal);
        Save edited;
        if (adventure)
        {
            var edit = AdventureRules.Edit(original, args[2], args[3]); edited = edit.Save;
            foreach (string change in edit.Changes) Console.WriteLine(change);
            foreach (var issue in AdventureRules.Validate(edited).Where(i => i.Severity == "warning")) Console.Error.WriteLine(issue.Message);
        }
        else edited = original.Set(args[2], args[3]); // Validate before opening an output.
        if (inPlace) FileWriter.InPlace(args[1], original.Serialize(), edited.Serialize(), timestamped: original.Kind == "global");
        else if (original.Kind == "global") FileWriter.NewWithBackup(output!, original.Serialize(), edited.Serialize());
        else FileWriter.New(output!, edited.Serialize());
        Console.WriteLine($"Wrote {Path.GetFullPath(output ?? args[1])}");
        if (inPlace) Console.WriteLine(original.Kind == "global" ? "Timestamped backup created beside input." : $"Backup: {Path.GetFullPath(args[1] + ".bak")}");
        Console.Error.WriteLine(args[2].StartsWith("track.", StringComparison.Ordinal) ? "Awards, counters and captured progression synchronized; unresolved fields preserved." : "Edited decoded field; progress summaries alone do not complete events.");
        return 0;
    }
}
