using CtrNfSave;
using System.Globalization;

namespace CtrNfSave.Gui;

internal sealed class TimeAttackView : UserControl
{
    public event Action<string, string>? Edit;
    public event Action<string>? TrackChanged;
    private readonly DataGridView recordGrid;
    public bool CommitEdits() => recordGrid.EndEdit();
    public TimeAttackView(Save save, string? selectedTrack)
    {
        Dock = DockStyle.Fill;
        var ghosts = TimeAttack.Ghosts(save);
        var records = TimeAttack.Records(save);
        string[] tracks = ghosts.Select(g => g.Track).Concat(records.Select(r => r.Track)).Distinct().Order(StringComparer.Ordinal).ToArray();
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(4) };
        var note = new Label { Text = L.T("ttEvidence"), AutoSize = true, MaximumSize = new Size(820, 0), Margin = new Padding(4, 4, 4, 16) };
        top.Controls.Add(note);
        var choose = EditorLayout.FilterBar();
        var track = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
        track.Items.AddRange(tracks.Select(t => (object)new Choice(t, L.Optional(t))).ToArray());
        track.DisplayMember = nameof(Choice.Label); choose.Controls.Add(track); top.Controls.Add(choose);
        var progressBar = EditorLayout.FilterBar();
        var progress = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
        for (int i = 0; i <= 4; i++) progress.Items.Add(L.T("ghostLevel" + i));
        var apply = new Button { Text = L.T("applyGhostProgress"), AutoSize = true };
        progressBar.Controls.Add(progress); progressBar.Controls.Add(apply); top.Controls.Add(progressBar);
        var ghostTime = new Label { AutoSize = true, MaximumSize = new Size(820, 0), Margin = new Padding(4, 4, 4, 16) }; top.Controls.Add(ghostTime);
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            MultiSelect = false
        };
        recordGrid = grid;
        grid.Columns.Add("category", L.T("category")); grid.Columns.Add("rank", L.T("rank")); grid.Columns.Add("time", L.T("editableTime")); grid.Columns.Add("driver", L.T("driverName"));
        foreach (DataGridViewColumn column in grid.Columns) { column.ReadOnly = column.Name != "time"; column.SortMode = DataGridViewColumnSortMode.NotSortable; }
        grid.Columns["rank"]!.FillWeight = 35;
        Controls.Add(grid); Controls.Add(top); EditorLayout.StyleGrid(grid);
        SizeChanged += (_, _) =>
        {
            int available = Math.Max(240, ClientSize.Width - 24);
            note.MaximumSize = ghostTime.MaximumSize = new Size(available, 0);
            choose.MaximumSize = progressBar.MaximumSize = new Size(available, 0);
            track.Width = progress.Width = Math.Min(350, available - 12);
        };
        bool binding = false;
        void PopulateTrack()
        {
            if (track.SelectedItem is not Choice selected) return;
            binding = true;
            try
            {
                TrackChanged?.Invoke(selected.Id); grid.Rows.Clear();
                var saved = ghosts.SingleOrDefault(g => g.Track == selected.Id);
                bool supported = saved is not null && saved.Level is >= 0 and <= 4;
                progress.Enabled = supported; apply.Enabled = supported;
                progress.SelectedIndex = supported ? saved!.Level : -1;
                ghostTime.Text = saved is null ? L.T("noGhostProgress") : saved.PlayerSeconds is float time && float.IsFinite(time) && time >= 0
                    ? L.F("playerGhostTime", FormatTime(time)) : L.T("noPlayerGhost");
                foreach (var record in records.Where(r => r.Track == selected.Id))
                {
                    var driver = GlobalUnlocks.Drivers.SingleOrDefault(d => d.Driver == record.Driver);
                    string name = driver is null ? record.Driver : L.Language == "fr" ? driver.NameFr : driver.NameEn;
                    int index = grid.Rows.Add(L.T(record.Category), record.Rank, FormatTime(record.Seconds), name);
                    grid.Rows[index].Tag = record;
                }
            }
            finally { binding = false; }
        }
        track.SelectedIndexChanged += (_, _) => PopulateTrack();
        apply.Click += (_, _) =>
        {
            if (track.SelectedItem is Choice selected && progress.SelectedIndex >= 0)
            {
                var saved = ghosts.Single(g => g.Track == selected.Id);
                if (saved.Level != progress.SelectedIndex) Edit?.Invoke(saved.Key, progress.SelectedIndex.ToString(CultureInfo.InvariantCulture));
            }
        };
        grid.CellValidating += (_, e) =>
        {
            if (binding || e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "time" || grid.Rows[e.RowIndex].Tag is not EditableTime record) return;
            string value = Convert.ToString(e.FormattedValue, CultureInfo.CurrentCulture) ?? "";
            if (value == FormatTime(record.Seconds)) return;
            try { _ = save.Set(record.Key, value.Trim().Replace(',', '.')); }
            catch (SaveFormatException error)
            {
                e.Cancel = true;
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = L.Optional(error.Message);
                MessageBox.Show(this, L.Optional(error.Message), L.T("invalidTime"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        grid.CellEndEdit += (_, e) =>
        {
            if (binding || e.RowIndex < 0 || grid.Columns[e.ColumnIndex].Name != "time") return;
            var row = grid.Rows[e.RowIndex];
            if (row.Tag is not EditableTime record) return;
            string value = Convert.ToString(row.Cells["time"].Value, CultureInfo.CurrentCulture) ?? "";
            row.Cells["time"].ErrorText = "";
            if (value == FormatTime(record.Seconds)) return;
            Edit?.Invoke(record.Key, value.Trim().Replace(',', '.'));
        };
        grid.DataError += (_, e) => e.ThrowException = false;
        int initial = Array.IndexOf(tracks, selectedTrack);
        if (tracks.Length > 0) track.SelectedIndex = Math.Max(0, initial);
        LauncherStyle.Apply(this);
    }
    private sealed record Choice(string Id, string Label);
    private static string FormatTime(float seconds)
    {
        if (seconds > 1e12f) return seconds.ToString("R", CultureInfo.InvariantCulture);
        long centiseconds = (long)Math.Round(seconds * 100d);
        return $"{centiseconds / 6000}:{centiseconds / 100 % 60:00}.{centiseconds % 100:00}";
    }
}
