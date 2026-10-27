using CtrNfSave;

namespace CtrNfSave.Gui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        using var source = typeof(Program).Assembly.GetManifestResourceStream("CtrNfSave.Gui.Assets.app.ico");
        using var icon = source is null ? null : new Icon(source);
        using var editor = new EditorForm(args.FirstOrDefault());
        if (icon is not null) editor.Icon = icon;
        Application.Run(editor);
    }
}
