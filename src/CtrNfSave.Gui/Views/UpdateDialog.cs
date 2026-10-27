using CtrNfSave.Updates;

namespace CtrNfSave.Gui;

internal sealed class UpdateDialog : Form
{
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool InstallRequested { get; private set; }
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SkipRequested { get; private set; }
    public UpdateDialog(UpdateRelease release, bool automatic)
    {
        Text = L.T("updateAvailable"); ClientSize = new Size(760, 520); MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
        var title = new Label { Text = $"{UpdateService.Version} → {release.Version}", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16), Font = LauncherStyle.Display(20) };
        var body = new TextBox { Text = release.Body.Replace("\r\n", "\n").Replace("\n", "\r\n"), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        var install = new Button { Text = L.T("installRestart"), AutoSize = true, Enabled = UpdateService.CanInstall() && release.SelectAssets() is not null };
        install.Click += (_, _) => { InstallRequested = true; DialogResult = DialogResult.OK; };
        var page = new Button { Text = L.T("openRelease"), AutoSize = true };
        page.Click += (_, _) => UpdateService.OpenPage(release.PageUrl, this);
        var later = new Button { Text = L.T("later"), AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(install); buttons.Controls.Add(page); buttons.Controls.Add(later);
        if (automatic)
        {
            var skip = new Button { Text = L.T("skipVersion"), AutoSize = true };
            skip.Click += (_, _) => { SkipRequested = true; DialogResult = DialogResult.OK; }; buttons.Controls.Add(skip);
        }
        var hint = new Label { Text = install.Enabled ? L.T("rollbackHint") : L.T("releaseOnly"), Dock = DockStyle.Bottom, AutoSize = true, MaximumSize = new Size(720, 0), Padding = new Padding(12) };
        Controls.Add(body); Controls.Add(title); Controls.Add(hint); Controls.Add(buttons); CancelButton = later;
        LauncherStyle.Apply(this);
    }
    public static async Task<string?> Prepare(IWin32Window owner, UpdateRelease release)
    {
        using var cancellation = new CancellationTokenSource();
        using var dialog = new Form { Text = L.T("preparingUpdate"), ClientSize = new Size(520, 150), StartPosition = FormStartPosition.CenterParent, ControlBox = true, Font = new Font("Segoe UI", 10) };
        var progress = new ProgressBar { Dock = DockStyle.Top, Height = 30 };
        var cancel = new Button { Text = L.T("Annuler"), Dock = DockStyle.Bottom, AutoSize = true };
        var label = new Label { Text = L.T("downloadVerify"), Dock = DockStyle.Fill, Padding = new Padding(12) };
        dialog.Controls.Add(label); dialog.Controls.Add(progress); dialog.Controls.Add(cancel); LauncherStyle.Apply(dialog);
        bool finished = false; string? script = null;
        cancel.Click += (_, _) => { cancellation.Cancel(); cancel.Enabled = false; };
        dialog.FormClosing += (_, e) => { if (!finished) { e.Cancel = true; cancellation.Cancel(); cancel.Enabled = false; } };
        dialog.Shown += async (_, _) =>
        {
            try { script = await UpdateService.Prepare(release, new Progress<int>(value => { if (!dialog.IsDisposed) progress.Value = value; }), cancellation.Token); cancellation.Token.ThrowIfCancellationRequested(); }
            catch (OperationCanceledException) { script = null; }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Net.Http.HttpRequestException or System.Security.Cryptography.CryptographicException or ArgumentException)
            { script = null; MessageBox.Show(dialog, L.T("installFailed"), dialog.Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { finished = true; dialog.Close(); }
        };
        dialog.ShowDialog(owner); await Task.CompletedTask; return script;
    }
}
