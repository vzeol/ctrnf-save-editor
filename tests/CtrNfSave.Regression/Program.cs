namespace CtrNfSave;

internal static class RegressionProgram
{
    private static int Main(string[] args)
    {
        if (args.Length != 1 || !Directory.Exists(Path.Combine(args[0], "samples")) || !Directory.Exists(Path.Combine(args[0], "target")))
        {
            Console.Error.WriteLine("Usage: dotnet run --project tests/CtrNfSave.Regression -- <private-fixture-root>");
            Console.Error.WriteLine("The root must contain samples/ and target/. No real saves are distributed with this repository.");
            return 2;
        }
        Directory.SetCurrentDirectory(Path.GetFullPath(args[0]));
        return SelfTest.Run("samples");
    }
}
