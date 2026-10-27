namespace CtrNfSave.Gui;

internal sealed class AboutDialog : Form
{
    public AboutDialog(UpdateService updates, Func<Form, Task> check)
    {
        Text = L.T("about"); ClientSize = new Size(720, 540); MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterParent; Font = new Font("Segoe UI", 10);
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24) };
        using var stream = typeof(AboutDialog).Assembly.GetManifestResourceStream("CtrNfSave.Gui.Assets.app.ico");
        if (stream is not null)
        {
            var icon = new Icon(stream); Icon = icon;
            var image = icon.ToBitmap();
            flow.Controls.Add(new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(64, 64) });
            Disposed += (_, _) => { image.Dispose(); icon.Dispose(); };
        }
        void Paragraph(string text, bool title = false) => flow.Controls.Add(new Label { Text = text, AutoSize = true, MaximumSize = new Size(LauncherStyle.Px(this, 650), 0), Margin = new Padding(3, 6, 3, 10), Font = title ? LauncherStyle.Display(20) : Font });
        Paragraph("CTR Nitro-Fueled Save Editor", true); Paragraph(UpdateService.Version);
        Paragraph(L.T("aboutDescription")); Paragraph("PolyForm Noncommercial 1.0.0"); Paragraph(L.T("aboutCredits"));
        var links = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(LauncherStyle.Px(this, 650), 0), WrapContents = true };
        foreach (var (key, path) in new[] { ("github", ""), ("releases", "/releases"), ("reportIssue", "/issues/new/choose"), ("credits", "/blob/main/CREDITS.md") })
        {
            var link = new Button { Text = L.T(key), AutoSize = true };
            link.Click += (_, _) => UpdateService.OpenPage(UpdateService.Repository + path, this); links.Controls.Add(link);
        }
        flow.Controls.Add(links);
        var startup = new CheckBox { Text = L.T("startupUpdates"), Checked = updates.Settings.CheckUpdatesAtStartup, AutoSize = true, Margin = new Padding(3, 16, 3, 12) };
        bool restoringSetting = false;
        startup.CheckedChanged += (_, _) =>
        {
            if (restoringSetting) return;
            try { updates.SetSettings(updates.Settings with { CheckUpdatesAtStartup = startup.Checked }); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { MessageBox.Show(this, L.T("settingsFailed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Error); restoringSetting = true; startup.Checked = updates.Settings.CheckUpdatesAtStartup; restoringSetting = false; }
        };
        flow.Controls.Add(startup);
        var actions = new FlowLayoutPanel { AutoSize = true };
        var checkButton = new Button { Text = L.T("checkUpdates"), AutoSize = true };
        checkButton.Click += async (_, _) => { checkButton.Enabled = false; try { await check(this); } finally { if (!IsDisposed) checkButton.Enabled = true; } };
        var close = new Button { Text = L.T("close"), AutoSize = true, DialogResult = DialogResult.Cancel };
        actions.Controls.Add(checkButton); actions.Controls.Add(close); flow.Controls.Add(actions); Controls.Add(flow); CancelButton = close;
        LauncherStyle.Apply(this);
        // Size to the content at the current DPI so the last row of buttons is never cut off.
        var needed = flow.GetPreferredSize(Size.Empty);
        var area = Screen.FromPoint(Cursor.Position).WorkingArea;
        ClientSize = new Size(Math.Min(area.Width - 40, Math.Max(LauncherStyle.Px(this, 640), needed.Width + LauncherStyle.Px(this, 24))),
            Math.Min(area.Height - 80, needed.Height + LauncherStyle.Px(this, 24)));
        MinimumSize = Size.Empty;
    }
}
