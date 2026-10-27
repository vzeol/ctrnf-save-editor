using CtrNfSave;

namespace CtrNfSave.Gui;

internal sealed class GarageView : UserControl
{
    public event Action<KartPreset>? ApplyPreset;
    public GarageView(Save save)
    {
        Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);
        var current = new Label { AutoSize = true, Margin = new Padding(4, 4, 4, 16) };
        KartPreset? active = null;
        try
        {
            active = KartLoadout.Read(save).Preset;
            current.Text = Describe(active, "garageCurrent");
        }
        catch (SaveFormatException error) { current.Text = L.F("garageUnavailable", error.Message); }
        layout.Controls.Add(current);
        layout.Controls.Add(new Label { Text = L.T("garageHint"), AutoSize = true, Margin = new Padding(4, 4, 4, 12) });
        var list = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
        layout.Controls.Add(list);
        var note = new Label { AutoSize = true, Margin = new Padding(4, 12, 4, 8) }; layout.Controls.Add(note);
        var buttons = EditorLayout.FilterBar(); layout.Controls.Add(buttons);
        void Run(Action action)
        {
            try { action(); }
            catch (Exception e) when (e is SaveFormatException or IOException or UnauthorizedAccessException or ArgumentException)
            { MessageBox.Show(this, L.F("garageError", e.Message), L.T("garage"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        Button Button(string key, Action action)
        {
            var button = new Button { Text = L.T(key), AutoSize = true };
            button.Click += (_, _) => Run(action); buttons.Controls.Add(button); return button;
        }
        void Selection()
        {
            note.Text = L.T("garageOwnershipUnknown");
            if (list.SelectedItem is StoredKartPreset selected)
            {
                var ownership = KartLoadout.Ownership(save, selected.Preset);
                if (ownership.Driver is false) note.Text = L.T("garageDriverLocked") + "\r\n" + note.Text;
                else if (ownership.Driver is null) note.Text = L.T("garageDriverUnknown") + "\r\n" + note.Text;
                note.Text = Describe(selected.Preset, "garagePresetDetails") + "\r\n" + note.Text;
            }
        }
        void Reload()
        {
            string? path = (list.SelectedItem as StoredKartPreset)?.Path;
            var presets = GarageStore.Read(out int invalid); list.Items.Clear();
            list.Items.AddRange(presets.Cast<object>().ToArray());
            list.SelectedIndex = Math.Max(-1, presets.ToList().FindIndex(p => p.Path == path));
            if (list.SelectedIndex < 0 && list.Items.Count > 0) list.SelectedIndex = 0;
            Selection(); if (invalid > 0) note.Text += "\r\n" + L.F("garageInvalidFiles", invalid);
        }
        var capture = Button("garageCapture", () =>
        {
            string? name = AskName(this, ""); if (name is null || active is null) return;
            GarageStore.Write(active with { Name = name }); Reload();
        }); capture.Enabled = active is not null;
        var apply = Button("Appliquer", () =>
        {
            if (list.SelectedItem is StoredKartPreset selected) ApplyPreset?.Invoke(selected.Preset);
        });
        var rename = Button("garageRename", () =>
        {
            if (list.SelectedItem is not StoredKartPreset selected) return;
            string? name = AskName(this, selected.Preset.Name); if (name is null) return;
            GarageStore.Write(selected.Preset with { Name = name }, selected.Path); Reload();
        });
        var delete = Button("garageDelete", () =>
        {
            if (list.SelectedItem is StoredKartPreset selected && MessageBox.Show(this, L.F("garageDeleteConfirm", selected.Preset.Name),
                L.T("garage"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            { GarageStore.Delete(selected); Reload(); }
        });
        var export = Button("garageExport", () =>
        {
            if (list.SelectedItem is not StoredKartPreset selected) return;
            using var dialog = new SaveFileDialog { Filter = L.T("garageJsonFilter"), DefaultExt = "json", FileName = "kart-preset.json", RestoreDirectory = true };
            if (dialog.ShowDialog(this) == DialogResult.OK)
                FileWriter.New(dialog.FileName, System.Text.Encoding.UTF8.GetBytes(selected.Preset.ToJson()));
        });
        Button("garageImport", () =>
        {
            using var dialog = new OpenFileDialog { Filter = L.T("garageJsonFilter"), RestoreDirectory = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            GarageStore.Write(KartPreset.FromJson(File.ReadAllText(dialog.FileName))); Reload();
        });
        list.SelectedIndexChanged += (_, _) =>
        {
            bool selected = list.SelectedItem is StoredKartPreset;
            apply.Enabled = selected && active is not null; rename.Enabled = delete.Enabled = export.Enabled = selected; Selection();
        };
        apply.Enabled = rename.Enabled = delete.Enabled = export.Enabled = false;
        Run(Reload);
        SizeChanged += (_, _) =>
        {
            int width = Math.Max(200, ClientSize.Width - 24);
            foreach (var label in layout.Controls.OfType<Label>()) label.MaximumSize = new Size(width, 0);
            buttons.MaximumSize = new Size(width, 0);
        };
        LauncherStyle.Apply(this);
    }
    private static string Describe(KartPreset preset, string key)
    {
        bool french = L.Language == "fr";
        return L.F(key, KartLoadout.DriverName(preset.Driver, french), KartLoadout.SkinName(preset.Driver, preset.Skin, french),
            KartLoadout.ChassisName(preset, french), KartLoadout.WheelsName(preset.Wheels, french),
            KartLoadout.PaintName(preset.Paint, french), KartLoadout.StickerName(preset.Sticker, french)) +
            "\r\n" + L.F("garageUnknownWords", preset.Word7, preset.Word9);
    }
    private static string? AskName(IWin32Window owner, string previous)
    {
        using var dialog = new Form { Text = L.T("garagePresetName"), ClientSize = new Size(460, 160), StartPosition = FormStartPosition.CenterParent, Font = new Font("Segoe UI", 10), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var text = new TextBox { Text = previous, Dock = DockStyle.Top, MaxLength = 100 };
        var label = new Label { Text = L.T("garagePresetName"), Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 4, 4, 12) };
        var bar = EditorLayout.FilterBar(); bar.Dock = DockStyle.Bottom;
        var ok = new Button { Text = L.T("Enregistrer"), AutoSize = true };
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(text.Text) || text.Text.Any(char.IsControl)) return;
            dialog.DialogResult = DialogResult.OK;
        };
        var cancel = new Button { Text = L.T("Annuler"), AutoSize = true, DialogResult = DialogResult.Cancel };
        bar.Controls.Add(ok); bar.Controls.Add(cancel); dialog.Controls.Add(text); dialog.Controls.Add(label); dialog.Controls.Add(bar);
        dialog.AcceptButton = ok; dialog.CancelButton = cancel; dialog.Padding = new Padding(16); LauncherStyle.Apply(dialog);
        return dialog.ShowDialog(owner) == DialogResult.OK ? text.Text.Trim() : null;
    }
}
