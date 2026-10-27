namespace CtrNfSave;

internal static class RegressionProgram
{
    private static int Main(string[] args)
    {
        if (!(args.Length == 1 || args.Length == 3 && args[1] == "--artifacts") || !Directory.Exists(Path.Combine(args[0], "samples")) || !Directory.Exists(Path.Combine(args[0], "target")))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tests/CtrNfSave.Regression -- <private-fixture-root> [--artifacts <artifacts-directory>]");
            Console.Error.WriteLine("The root must contain samples/ and target/. No real saves are distributed with this repository.");
            return 2;
        }
        string? artifacts = args.Length == 3 ? Path.GetFullPath(args[2]) : null;
        if (artifacts is not null && !new DirectoryInfo(artifacts).Name.Equals("artifacts", StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("Regression output directory must be named artifacts."); return 2; }
        Directory.SetCurrentDirectory(Path.GetFullPath(args[0]));
        return SelfTest.Run("samples", artifacts);
    }
}
