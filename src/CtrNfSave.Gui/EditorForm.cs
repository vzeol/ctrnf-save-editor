using CtrNfSave;

namespace CtrNfSave.Gui;

internal sealed class EditorForm : Form
{
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        MultiSelect = false,
        BackgroundColor = SystemColors.Window
    };
    private readonly Label totals = new() { Dock = DockStyle.Fill, Padding = new Padding(8), AutoSize = true };
    private readonly Label status = new() { Dock = DockStyle.Fill, Padding = new Padding(8), AutoSize = true };
    private readonly Dictionary<string, CheckBox> gems = [];
    private readonly CheckBox oxideFirst = new() { AutoSize = true };
    private readonly CheckBox oxideSecond = new() { AutoSize = true };
    private readonly ComboBox difficulty = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ToolStripButton checkButton = new(L.T("Vérifier la cohérence")) { Enabled = false };
    private readonly ToolStripButton normalizeButton = new(L.T("Normaliser")) { Enabled = false };
    private readonly ToolStripButton saveButton = new(L.T("Enregistrer")) { Enabled = false };
    private readonly ToolStripButton saveAsButton = new(L.T("Enregistrer sous…")) { Enabled = false };
    private readonly List<Action> relabel = [];
    private readonly GlobalProfileView globalView = new();
    private readonly SectionNavigator adventureNavigation = new();
    private readonly LauncherHeader banner = new();
    private readonly SummaryStrip summaryStrip = new();
    private readonly Panel welcome = new() { Dock = DockStyle.Fill };
    private readonly TextBox trackSearch = EditorLayout.SearchBox();
    private readonly DataGridView technical = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    private Control? adventurePanel;
    private Save? save;
    private byte[]? diskBytes;
    private string? path;
    private bool dirty, filling, bulkWorking;
    private string? statusKey;
    private object[] statusArgs = [];
    private void SetStatus(string key, params object[] args)
    {
        statusKey = key; statusArgs = args; RenderStatus();
    }
    private void RenderStatus()
    {
        status.Text = statusKey is null ? "" : L.F(statusKey, statusArgs);
    }

    public EditorForm(string? initialPath)
    {
        Text = L.T("CTR Nitro-Fueled — Éditeur Aventure");
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        Width = Math.Min(1280, workArea.Width - 40); Height = Math.Min(950, workArea.Height - 40);
        MinimumSize = new Size(1040, 680); Font = new Font("Segoe UI", 10); DoubleBuffered = true;
        StartPosition = FormStartPosition.CenterScreen;
        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(8) };
        var openAdventure = new ToolStripButton(L.T("openAdventure"));
        var openGlobal = new ToolStripButton(L.T("openGlobal"));
        toolbar.Items.AddRange([openAdventure, openGlobal, new ToolStripSeparator(), saveButton, saveAsButton]);
        var language = new ToolStripDropDownButton(L.T("Language / Langue"));
        foreach (string code in new[] { "en", "fr" })
        {
            string c = code;
            var item = new ToolStripMenuItem(L.T(c == "en" ? "English" : "Français")) { CheckOnClick = false };
            item.Click += (_, _) => { if (!grid.EndEdit() || !globalView.CommitEdits()) return; L.Language = c; L.Remember(); RefreshLanguage(); };
            language.DropDownItems.Add(item);
            relabel.Add(() => item.Checked = L.Language == c);
        }
        toolbar.Items.Add(language);
        var gemPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
        foreach (string colour in AdventureRules.Colours)
        {
            string c = colour;
            var box = new CheckBox { Text = Colour(c), AutoSize = true, Enabled = false };
            gems[c] = box; gemPanel.Controls.Add(box);
            box.CheckedChanged += (_, _) => { if (!filling && save is not null) ApplyEdit("slot.gem_" + c, box.Checked ? "1" : "0"); };
        }
        var difficultyLabel = new Label { AutoSize = true, Margin = new Padding(12, 7, 3, 3) };
        var difficultyPanel = EditorLayout.FilterBar(); difficultyPanel.Controls.Add(difficultyLabel); difficultyPanel.Controls.Add(difficulty);
        var oxidePanel = EditorLayout.FilterBar(); oxidePanel.Controls.Add(oxideFirst); oxidePanel.Controls.Add(oxideSecond);
        relabel.Add(() =>
        {
            bool previous = filling; filling = true;
            try
            {
                difficultyLabel.Text = L.T("difficulty");
                difficulty.Items.Clear(); difficulty.Items.AddRange([L.T("easy"), L.T("medium"), L.T("hard")]);
                if (save is not null && save.Kind != "global") difficulty.SelectedIndex = Convert.ToInt32(save.Semantic()["slot.adventure_difficulty"]);
                oxideFirst.Text = L.T("oxideFirst"); oxideSecond.Text = L.T("oxideSecond");
            }
            finally { filling = previous; }
        });
        oxideFirst.CheckedChanged += (_, _) => { if (!filling && save is not null) ApplyEdit("slot.oxide_first", oxideFirst.Checked ? "true" : "false"); };
        oxideSecond.CheckedChanged += (_, _) => { if (!filling && save is not null) ApplyEdit("slot.oxide_second", oxideSecond.Checked ? "true" : "false"); };
        difficulty.SelectedIndexChanged += (_, _) => { if (!filling && save is not null && difficulty.SelectedIndex >= 0) ApplyEdit("slot.adventure_difficulty", difficulty.SelectedIndex.ToString()); };
        adventurePanel = adventureNavigation;
        var rewardsPage = adventureNavigation.AddPage(L.T("trackRewards"));
        var rewardFilters = EditorLayout.FilterBar(); rewardFilters.Controls.Add(trackSearch);
        rewardsPage.Controls.Add(grid); rewardsPage.Controls.Add(rewardFilters);
        var globalHint = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(4, 4, 4, 12) };
        relabel.Add(() => globalHint.Text = L.T("globalProfileHint"));
        rewardsPage.SizeChanged += (_, _) => globalHint.MaximumSize = new Size(Math.Max(260, rewardsPage.ClientSize.Width - 24), 0);
        rewardsPage.Controls.Add(globalHint);
        trackSearch.TextChanged += (_, _) => FilterTracks();
        var settingsPage = adventureNavigation.AddPage(L.T("adventureSettings"));
        var settingsFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(4) };
        settingsPage.Controls.Add(settingsFlow);
        void SettingsCard(string key, Control content, int height)
        {
            var card = new GroupBox { Text = L.T(key), Width = 700, Height = height, Padding = new Padding(16, 22, 16, 12), Margin = new Padding(4, 4, 4, 16) };
            content.Dock = DockStyle.Fill; card.Controls.Add(content); settingsFlow.Controls.Add(card); relabel.Add(() => card.Text = L.T(key));
        }
        SettingsCard("gemsTitle", gemPanel, 110); SettingsCard("difficulty", difficultyPanel, 100); SettingsCard("oxideTitle", oxidePanel, 110);
        var consistencyPanel = EditorLayout.FilterBar();
        var check = new Button { AutoSize = true }; var normalize = new Button { AutoSize = true };
        relabel.Add(() => { check.Text = L.T("Vérifier la cohérence"); normalize.Text = L.T("Normaliser"); });
        check.Click += (_, _) => checkButton.PerformClick(); normalize.Click += (_, _) => normalizeButton.PerformClick();
        consistencyPanel.Controls.Add(check); consistencyPanel.Controls.Add(normalize); SettingsCard("consistencyTitle", consistencyPanel, 110);
        settingsFlow.SizeChanged += (_, _) => { foreach (Control card in settingsFlow.Controls) card.Width = Math.Max(320, settingsFlow.ClientSize.Width - 32); };
        var advancedPage = adventureNavigation.AddPage(L.T("advanced"));
        technical.Columns.Add("field", L.T("itemName")); technical.Columns.Add("value", L.T("value"));
        var advancedBar = EditorLayout.FilterBar();
        var rawFlags = new CheckBox { AutoSize = true }; relabel.Add(() => rawFlags.Text = L.T("showFlags"));
        rawFlags.CheckedChanged += (_, _) => grid.Columns["flags"]!.Visible = rawFlags.Checked;
        advancedBar.Controls.Add(rawFlags); advancedPage.Controls.Add(technical); advancedPage.Controls.Add(advancedBar);
        relabel.Add(() => { adventureNavigation.SetTitle(0, L.T("trackRewards")); adventureNavigation.SetTitle(1, L.T("adventureSettings")); adventureNavigation.SetTitle(2, L.T("advanced")); trackSearch.PlaceholderText = L.T("searchTracks"); });
        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.Add(adventureNavigation); body.Controls.Add(globalView); body.Controls.Add(welcome); globalView.Visible = false; adventureNavigation.Visible = false;
        var welcomeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(40, 20, 24, 20) };
        var welcomeTitle = new Label { AutoSize = true, Font = LauncherStyle.Display(26), MaximumSize = new Size(800, 0) };
        var welcomeBody = new Label { AutoSize = true, MaximumSize = new Size(650, 0), Margin = new Padding(3, 20, 3, 25) };
        welcomeFlow.Controls.Add(welcomeTitle); welcomeFlow.Controls.Add(welcomeBody);
        void SaveChoice(bool global)
        {
            var card = new Panel { Width = 820, Height = 166, Padding = new Padding(16), Margin = new Padding(3, 4, 3, 14) };
            var button = new Button { AutoSize = true, Dock = DockStyle.Top, Font = LauncherStyle.Display(14) };
            button.Click += (_, _) => (global ? openGlobal : openAdventure).PerformClick();
            var description = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 10, 0, 4), MaximumSize = new Size(780, 0) };
            var location = new Label { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 6, 0, 4), MaximumSize = new Size(780, 0) };
            card.Controls.Add(location); card.Controls.Add(description); card.Controls.Add(button); welcomeFlow.Controls.Add(card);
            relabel.Add(() =>
            {
                button.Text = L.T(global ? "openGlobal" : "openAdventure"); description.Text = L.T(global ? "globalSaveDescription" : "adventureSaveDescription");
                string? root = SaveLocations.FindProfileRoot(path);
                string folder = SaveLocations.RelativeProfilePath(root);
                location.Text = L.T("saveLocation") + "\n" + Path.Combine(folder, global ? @"savedata0\SYS-DATA.sav" : @"savedata1…4\SYS-DATA.sav");
            });
            void FitCard()
            {
                card.Width = Math.Max(300, Math.Min(820, welcomeFlow.ClientSize.Width - 90));
                description.MaximumSize = location.MaximumSize = new Size(card.Width - 32, 0);
                card.Height = button.PreferredSize.Height + description.PreferredSize.Height + location.PreferredSize.Height + 38;
            }
            welcomeFlow.SizeChanged += (_, _) => FitCard();
            description.SizeChanged += (_, _) => FitCard();
            location.SizeChanged += (_, _) => FitCard();
            button.SizeChanged += (_, _) => FitCard();
        }
        SaveChoice(false); SaveChoice(true); welcome.Controls.Add(welcomeFlow);
        relabel.Add(() => { welcomeTitle.Text = L.T("welcomeTitle"); welcomeBody.Text = L.T("welcomeBody"); banner.Subtitle = L.T("editorSubtitle"); banner.Invalidate(); });
        globalView.Edit += (key, value) => ApplyEdit(key, value);
        globalView.BulkEdit += ApplyBulkUnlocks;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(banner, 0, 0); layout.Controls.Add(toolbar, 0, 1); layout.Controls.Add(summaryStrip, 0, 2);
        layout.Controls.Add(body, 0, 3); layout.Controls.Add(status, 0, 4); Controls.Add(layout); summaryStrip.Visible = false;
        checkButton.Click += (_, _) => ShowIssues();
        normalizeButton.Click += (_, _) =>
        {
            if (save is null) return;
            Save normalized = AdventureRules.Normalize(save);
            var differences = Save.Diff(save, normalized);
            if (differences.Count == 0) { SetStatus("Aucune correction automatique nécessaire. Les avertissements restent à vérifier."); return; }
            if (!ConfirmChanges(L.T("Normaliser"), L.T("Appliquer les corrections proposées ?"), L.Differences(save, normalized))) return;
            save = normalized; dirty = true; Populate(); UpdateTotals(); SetStatus("normalization", differences.Count);
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "hub", HeaderText = L.T("Zone"), ReadOnly = true, FillWeight = 150 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "track", HeaderText = L.T("Circuit / défi"), ReadOnly = true, FillWeight = 135 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "trophy", HeaderText = L.T("Trophée"), FillWeight = 60 });
        grid.Columns.Add(new DataGridViewComboBoxColumn { Name = "relic", HeaderText = L.T("Relique"), DisplayMember = "Label", ValueMember = "Value", DataSource = new[] { new { Value = "none", Label = L.T("Aucune") }, new { Value = "sapphire", Label = L.T("Saphir") }, new { Value = "gold", Label = L.T("Or") }, new { Value = "platinum", Label = L.T("Platine") } }, FillWeight = 75 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "boss", HeaderText = L.T("Boss"), FillWeight = 65 });
        grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "token", HeaderText = L.T("Jeton CTR"), FillWeight = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "colour", HeaderText = L.T("Couleur"), ReadOnly = true, FillWeight = 65 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "flags", HeaderText = L.T("Autres flags (hex)"), ReadOnly = true, FillWeight = 90 });
        grid.Columns["flags"]!.Visible = false;
        foreach (DataGridViewColumn column in grid.Columns) column.SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.CurrentCellDirtyStateChanged += (_, _) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        grid.CellValueChanged += EditCell;
        grid.DataError += (_, e) => { e.ThrowException = false; ShowError(new SaveFormatException(L.T("Valeur de cellule invalide."))); };
        openAdventure.Click += (_, _) => ChooseSave(false);
        openGlobal.Click += (_, _) => ChooseSave(true);
        saveButton.Click += (_, _) => Write(false);
        saveAsButton.Click += (_, _) => Write(true);
        FormClosing += (_, e) => { if (bulkWorking || !CanLeave()) e.Cancel = true; };
        totals.Text = L.T("Ouvrez un slot Aventure .sav décrypté (153600 octets).");
        foreach (ToolStripItem item in toolbar.Items)
        {
            string? key = L.Key(item.Text ?? "");
            if (key is not null) relabel.Add(() => item.Text = L.T(key));
        }
        foreach (Control control in gemPanel.Controls)
        {
            string? key = L.Key(control.Text);
            if (key is not null) relabel.Add(() => control.Text = L.T(key));
        }
        foreach (DataGridViewColumn column in grid.Columns)
        {
            string key = L.Key(column.HeaderText)!;
            relabel.Add(() => column.HeaderText = L.T(key));
        }
        RefreshLanguage();
        LauncherStyle.Apply(this); EditorLayout.StyleGrid(technical);
        if (initialPath is not null) Shown += (_, _) => Open(initialPath);
    }

    private void RefreshLanguage()
    {
        filling = true;
        try
        {
            foreach (var action in relabel) action();
            grid.Rows.Clear();
            ((DataGridViewComboBoxColumn)grid.Columns["relic"]!).DataSource = new[] { "none", "sapphire", "gold", "platinum" }
                .Select(v => new { Value = v, Label = L.Relic(v) }).ToArray();
            if (save is null) { Text = L.T("CTR Nitro-Fueled — Éditeur Aventure"); totals.Text = L.T("Ouvrez un slot Aventure .sav décrypté (153600 octets)."); }
            else { Populate(); UpdateTotals(); }
            RenderStatus();
        }
        finally { filling = false; }
    }

    private void ChooseSave(bool global)
    {
        if (!CanLeave()) return;
        string? root = SaveLocations.FindProfileRoot(path);
        using var dialog = new OpenFileDialog { Title = L.T(global ? "openGlobal" : "openAdventure"), Filter = L.T("saveFileFilter"), RestoreDirectory = true };
        string? folder = SaveLocations.InitialFolder(root, global);
        if (folder is not null) dialog.InitialDirectory = folder;
        if (global) dialog.FileName = "SYS-DATA.sav";
        if (root is not null)
        {
            foreach (int slot in global ? new[] { 0 } : new[] { 1, 2, 3, 4 })
            {
                string place = Path.Combine(root, "savedata" + slot);
                if (Directory.Exists(place)) dialog.CustomPlaces.Add(place);
            }
        }
        if (dialog.ShowDialog(this) == DialogResult.OK) Open(dialog.FileName, global ? "global" : "slot");
    }

    private void Open(string file, string? expectedKind = null)
    {
        try
        {
            var loaded = Save.Load(file);
            if (expectedKind is not null && loaded.Kind != expectedKind)
                throw new SaveFormatException(L.T(expectedKind == "global" ? "selectedAdventureInstead" : "selectedGlobalInstead"));
            if (loaded.Kind == "global") GlobalProfile.Require(loaded); else loaded.RequireAdventureSlot();
            save = loaded; diskBytes = loaded.Serialize(); path = Path.GetFullPath(file); dirty = false;
            Populate(); saveAsButton.Enabled = true; saveButton.Enabled = true; checkButton.Enabled = loaded.Kind == "slot"; normalizeButton.Enabled = loaded.Kind == "slot"; UpdateTotals();
            SetStatus(loaded.Kind == "global" ? "globalStatus" : "Boss et dépendances à vérifier en jeu ; progression non décodée conservée. Cliquez sur Vérifier la cohérence.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or ArgumentException)
        { ShowError(e); }
    }

    private void Populate()
    {
        if (save is null) return;
        bool global = save.Kind == "global";
        welcome.Visible = false; summaryStrip.Visible = true;
        globalView.Visible = global; grid.Visible = !global;
        if (adventurePanel is not null) adventurePanel.Visible = !global;
        if (global) { globalView.Bind(save); return; }
        technical.Rows.Clear();
        foreach (var field in save.Graph.Fields.Where(f => f.Key is "slot.mask_15" or "slot.mask_16" or "slot.unknown_17" or "slot.oxide_first" or "slot.oxide_second" or "slot.adventure_difficulty"))
            technical.Rows.Add(L.Field(field.Key), field.Value);
        int scroll = grid.FirstDisplayedScrollingRowIndex;
        string? selectedId = (grid.CurrentRow?.Tag as TrackDefinition)?.Id;
        string? selectedColumn = grid.CurrentCell?.OwningColumn?.Name;
        bool previousFilling = filling;
        filling = true;
        try
        {
            foreach (var (colour, box) in gems) { box.Checked = AdventureRules.Gem(save, colour); box.Enabled = true; }
            difficulty.SelectedIndex = Convert.ToInt32(save.Semantic()["slot.adventure_difficulty"]);
            oxideFirst.Checked = save.Semantic()["slot.oxide_first"] is true;
            oxideSecond.Checked = save.Semantic()["slot.oxide_second"] is true;
            grid.Rows.Clear(); string? hub = null;
            foreach (var definition in AdventureTracks.All)
            {
                var t = save.Graph.Tracks.Single(t => t.Definition.Id == definition.Id);
                string colour = definition.TokenColour switch { "red" => L.T("Rouge"), "green" => L.T("Vert"), "blue" => L.T("Bleu"), "yellow" => L.T("Jaune"), "purple" => L.T("Violet"), _ => "—" };
                int index = grid.Rows.Add(definition.Hub == hub ? "" : L.Hub(definition.Hub), L.T(definition.Id), t.Trophy, t.Relic, t.Boss, t.Token, colour, $"0x{t.OtherFlags:X8}");
                var row = grid.Rows[index]; row.Tag = definition;
                if (definition.Hub != hub) row.DividerHeight = 3;
                hub = definition.Hub;
                row.Cells["boss"].ToolTipText = definition.BossName is null ? "" : L.T(definition.BossName);
                if (definition.Id == "t154_oxide_station")
                    row.Cells["boss"].ToolTipText = L.T("Nitros Oxide : bit 18 non décodé, victoire non éditable.");
                foreach (string column in new[] { "trophy", "relic", "token", "boss" })
                {
                    bool available = column switch { "trophy" => definition.HasTrophy, "relic" => definition.HasRelic, "boss" => definition.BossName is not null, _ => definition.TokenBit is not null };
                    if (!available)
                    {
                        // ReadOnly can only be set once the cell belongs to a row.
                        var cell = new DataGridViewTextBoxCell { Value = "—", Style = new DataGridViewCellStyle { BackColor = LauncherStyle.Panel, ForeColor = LauncherStyle.Muted } };
                        row.Cells[column] = cell;
                        cell.ReadOnly = true;
                    }
                }
                if (definition.Id == "t154_oxide_station")
                {
                    var oxide = new DataGridViewTextBoxCell
                    {
                        Value = (oxideFirst.Checked ? "1" : "—") + " / " + (oxideSecond.Checked ? "2" : "—"),
                        Style = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, BackColor = LauncherStyle.Panel, ForeColor = LauncherStyle.Cyan },
                        ToolTipText = L.T("oxideControls")
                    };
                    row.Cells["boss"] = oxide; oxide.ReadOnly = true;
                }
            }
        }
        finally { filling = previousFilling; }
        if (selectedId is not null && selectedColumn is not null)
        {
            var selected = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => (r.Tag as TrackDefinition)?.Id == selectedId);
            if (selected is not null) grid.CurrentCell = selected.Cells[selectedColumn];
        }
        if (scroll >= 0 && scroll < grid.Rows.Count) grid.FirstDisplayedScrollingRowIndex = scroll;
        FilterTracks();
    }

    private void FilterTracks()
    {
        string query = trackSearch.Text.Trim();
        var selected = grid.CurrentCell; grid.CurrentCell = null;
        foreach (DataGridViewRow row in grid.Rows)
            if (row.Tag is TrackDefinition track) row.Visible = (L.T(track.Id) + " " + L.Hub(track.Hub) + " " + track.Name).Contains(query, StringComparison.OrdinalIgnoreCase);
        if (selected?.OwningRow?.Visible is true) grid.CurrentCell = selected;
    }

    private void EditCell(object? sender, DataGridViewCellEventArgs e)
    {
        if (filling || save is null || e.RowIndex < 0 || e.ColumnIndex < 0) return;
        string field = grid.Columns[e.ColumnIndex].Name;
        if (field is not ("trophy" or "relic" or "token" or "boss")) return;
        var row = grid.Rows[e.RowIndex];
        if (row.Tag is not TrackDefinition definition || row.Cells[e.ColumnIndex].ReadOnly) return;
        try
        {
            ApplyEdit($"track.{definition.Id}.{field}", Convert.ToString(row.Cells[e.ColumnIndex].Value, System.Globalization.CultureInfo.InvariantCulture) ?? "");
        }
        catch (SaveFormatException error) { ShowError(error); BeginInvoke((Action)Populate); }
    }

    private static string Colour(string c) => c switch { "red" => L.T("Rouge"), "green" => L.T("Vert"), "blue" => L.T("Bleu"), "yellow" => L.T("Jaune"), "purple" => L.T("Violet"), _ => c };

    private async void ApplyBulkUnlocks(string[] keys, bool enable)
    {
        if (save is null || bulkWorking || !globalView.CommitEdits()) return;
        var source = save; Save changed;
        bulkWorking = true; UseWaitCursor = true; Enabled = false;
        try { changed = await Task.Run(() => UnlockBatch.Set(source, keys, enable)); }
        catch (Exception error) when (error is SaveFormatException or ArgumentException)
        { ShowError(error); return; }
        finally { bulkWorking = false; UseWaitCursor = false; Enabled = true; }
        if (!ReferenceEquals(save, source)) return;
        var changes = UnlockBatch.Changes(source, changed);
        if (changes.Count == 0) { SetStatus("noUnlockChanges"); return; }
        string Value(object value) => value is bool state ? L.T(state ? "true" : "false") : Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) ?? "";
        string details = string.Join("\r\n", changes.Select(c => (L.Language == "fr" ? c.NameFr : c.NameEn) + " : " + Value(c.Before) + " → " + Value(c.After)));
        if (!ConfirmChanges(L.T(enable ? "unlockAll" : "lockAll"), L.F("bulkUnlockPreview", changes.Count), details)) return;
        save = changed; dirty = true; Populate(); UpdateTotals(); SetStatus("globalStatus");
    }

    private void ApplyEdit(string key, string value)
    {
        if (save is null) return;
        try
        {
            if (save.Kind == "global")
            {
                var changed = save.Set(key, value);
                if (key.StartsWith("global.skin.", StringComparison.Ordinal) || key.StartsWith("global.customization.", StringComparison.Ordinal) || key.StartsWith("global.character.", StringComparison.Ordinal) || key.StartsWith("global.reward.", StringComparison.Ordinal))
                {
                    var beforeSkin = save.Semantic(); var afterSkin = changed.Semantic();
                    var names = SkinUnlocks.Skins.Select(s => (s.Key, Name: L.Language == "fr" ? s.NameFr : s.NameEn))
                        .Concat(CustomizationUnlocks.Items.Select(i => (i.Key, Name: L.Language == "fr" ? i.NameFr : i.NameEn)))
                        .Concat(GlobalUnlocks.Drivers.Where(d => d.Key is not null).Select(d => (Key: d.Key!, Name: L.Language == "fr" ? d.NameFr : d.NameEn)))
                        .GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.First().Name);
                    var relocked = beforeSkin.Where(p => p.Key != key && names.ContainsKey(p.Key) && p.Value is true && afterSkin.GetValueOrDefault(p.Key) is false).Select(p => names[p.Key]).Distinct().ToArray();
                    if (relocked.Length > 0 && !ConfirmChanges(L.T("Dépendances"), L.T("globalUnlockCascade"), string.Join("\r\n", relocked)))
                    { BeginInvoke((Action)Populate); return; }
                }
                save = changed; dirty = true; UpdateTotals();
                SetStatus("globalStatus"); BeginInvoke((Action)Populate); return;
            }
            if (key == "slot.adventure_difficulty")
            {
                save = save.Set(key, value); dirty = true; SetStatus("difficultyChanged"); BeginInvoke((Action)Populate); return;
            }
            var edit = AdventureRules.Edit(save, key, value);
            var before = save.Semantic(); var after = edit.Save.Semantic();
            var removed = before.Where(p => p.Key != key && (p.Key.StartsWith("track.", StringComparison.Ordinal) && !p.Key.EndsWith(".flags", StringComparison.Ordinal) || p.Key.StartsWith("slot.gem_", StringComparison.Ordinal)))
                .Where(p => after.TryGetValue(p.Key, out var next) && !JsonOutput.Equal(p.Value, next) && (next is false || next is string text && text == "none" || next is int number && number == 0)).Select(p => p.Key).ToArray();
            if (removed.Length > 0 && !ConfirmChanges(L.T("Dépendances"), L.T("Retirer aussi les récompenses dépendantes ? Refuser annule toute la modification."), string.Join("\r\n", removed.Select(L.Field))))
            { BeginInvoke((Action)Populate); return; }
            var oldTotals = AdventureTotals.From(save.Graph.Tracks); int oldKeys = AdventureRules.Keys(save);
            save = edit.Save; dirty = true; var newTotals = AdventureTotals.From(save.Graph.Tracks);
            SetStatus("cascade", oldTotals.Trophies, newTotals.Trophies, oldKeys, AdventureRules.Keys(save), oldTotals.TokenCount, newTotals.TokenCount, oldTotals.Relics, newTotals.Relics);

            UpdateTotals(); BeginInvoke((Action)Populate);
        }
        catch (SaveFormatException error) { ShowError(error); BeginInvoke((Action)Populate); }
    }
    private bool ConfirmChanges(string title, string description, string details)
    {
        using var dialog = new Form { Text = title, Width = 800, Height = 500, MinimumSize = new Size(600, 350), StartPosition = FormStartPosition.CenterParent };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var yes = new Button { Text = L.T("Appliquer"), DialogResult = DialogResult.Yes, AutoSize = true };
        var no = new Button { Text = L.T("Annuler"), DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(no); buttons.Controls.Add(yes); dialog.AcceptButton = yes; dialog.CancelButton = no;
        dialog.Controls.Add(new TextBox { Text = details, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill });
        dialog.Controls.Add(new Label { Text = description, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) });
        dialog.Controls.Add(buttons);
        return dialog.ShowDialog(this) == DialogResult.Yes;
    }
    private void ShowIssues()
    {
        if (save is null) return;
        var issues = AdventureRules.Validate(save);
        using var dialog = new Form { Text = L.T("Cohérence Aventure"), Width = 850, Height = 480, StartPosition = FormStartPosition.CenterParent };
        dialog.Controls.Add(new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Text = L.F("issueCounts", issues.Count(i => i.Severity == "error"), issues.Count(i => i.Severity == "warning")) + "\r\n\r\n" +
                string.Join("\r\n\r\n", issues.Select(i => $"[{(i.Severity == "error" ? L.T("Erreur") : L.T("Avertissement"))}] {L.Issue(i)}"))
        });
        dialog.ShowDialog(this);
    }

    private void UpdateTotals()
    {
        if (save is null) return;
        banner.Subtitle = (save.Kind == "global" ? L.T("globalProfile") : L.T("adventureMode")) + "  ·  " + Path.GetFileName(Path.GetDirectoryName(path)) + " / " + Path.GetFileName(path) + (dirty ? "  •  " + L.T("unsaved") : ""); banner.Invalidate();
        if (save.Kind == "global")
        {
            var semantic = save.Semantic();
            int unlocked = GlobalUnlocks.Drivers.Count(d => d.Key is null || semantic.GetValueOrDefault(d.Key) is true);
            string owned; try { owned = GlobalUnlocks.Owned(save).Count.ToString(); } catch (SaveFormatException) { owned = "?"; }
            summaryStrip.SetCards(($"{unlocked} / 56", L.T("characters")), (owned, L.T("localItems")),
                ($"{semantic.GetValueOrDefault("global.golden_egg.count") ?? "?"} / 5", L.T("goldenEggs")),
                (Convert.ToInt32(semantic["global.wumpa_coins"]).ToString("N0"), L.T("localCoins")));
            totals.Text = L.F("globalTotals", save.Semantic()["global.wumpa_coins"]!, save.Semantic().GetValueOrDefault("global.golden_egg.count") ?? "?");
            Text = L.T("globalProfile") + " — " + (dirty ? "* " : "") + Path.GetFileName(path); return;
        }
        var t = AdventureTotals.From(save.Graph.Tracks);
        summaryStrip.SetCards(($"{t.Trophies} / 16", L.T("Trophée")), ($"{t.Relics} / 18", L.T("Relique")), ($"{t.TokenCount} / 20", L.T("Jeton CTR")),
            ($"{AdventureRules.Keys(save)} / 4", L.T("keys")), ($"{AdventureRules.Colours.Count(c => AdventureRules.Gem(save, c))} / 5", L.T("gemsTitle")), ($"{save.Get("percent_complete")!.Value} %", L.T("progressCaption")));
        totals.Text = L.F("awardsTotals", t.Trophies, t.Relics, t.Sapphire, t.Gold, t.Platinum, t.TokenCount) +
            L.F("colourTotals", t.Tokens["red"], t.Tokens["green"], t.Tokens["blue"], t.Tokens["yellow"], t.Tokens["purple"], AdventureRules.Keys(save), AdventureRules.Colours.Count(c => AdventureRules.Gem(save, c)), save.Get("percent_complete")!.Value);
        Text = L.F("windowTitle", dirty ? "* " : "", Path.GetFileName(path)!);
    }

    private bool Write(bool saveAs)
    {
        if (save is null || path is null || diskBytes is null) return false;
        if (!grid.EndEdit() || !globalView.CommitEdits()) return false;
        try
        {
            string destination = path;
            if (saveAs)
            {
                using var dialog = new SaveFileDialog { Title = L.T("Enregistrer sous…"), Filter = L.T("Slot Aventure (*.sav)|*.sav"), FileName = Path.GetFileNameWithoutExtension(path) + ".edited.sav", OverwritePrompt = true };
                if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                destination = Path.GetFullPath(dialog.FileName);
                // Existing destinations also receive a backup before replacement.
                if (File.Exists(destination))
                {
                    byte[] expected = string.Equals(destination, path, StringComparison.OrdinalIgnoreCase) ? diskBytes : File.ReadAllBytes(destination);
                    FileWriter.InPlace(destination, expected, save.Serialize(), timestamped: true);
                }
                else if (save.Kind == "global") FileWriter.NewWithBackup(destination, diskBytes, save.Serialize());
                else FileWriter.New(destination, save.Serialize());
            }
            else FileWriter.InPlace(destination, diskBytes, save.Serialize(), timestamped: true);
            path = destination; diskBytes = save.Serialize(); dirty = false; UpdateTotals();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SaveFormatException or ArgumentException)
        { ShowError(e); return false; }
    }

    private bool CanLeave()
    {
        if (!grid.EndEdit() || !globalView.CommitEdits()) return false;
        if (!dirty) return true;
        using var dialog = new Form { Text = L.T("Modifications non enregistrées"), Width = 560, Height = 180, StartPosition = FormStartPosition.CenterParent };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft };
        foreach (var (key, result) in new[] { ("Annuler", DialogResult.Cancel), ("discard", DialogResult.No), ("Enregistrer", DialogResult.Yes) })
            buttons.Controls.Add(new Button { Text = L.T(key), DialogResult = result, AutoSize = true });
        dialog.Controls.Add(new Label { Text = L.T("Enregistrer les modifications avant de continuer ?"), Dock = DockStyle.Fill, Padding = new Padding(12) });
        dialog.Controls.Add(buttons);
        var choice = dialog.ShowDialog(this);
        return choice == DialogResult.No || choice == DialogResult.Yes && Write(false);
    }
    private void ShowError(Exception error)
    {
        using var dialog = new Form { Text = L.T("Impossible de traiter ce fichier"), Width = 700, Height = 300, StartPosition = FormStartPosition.CenterParent };
        var close = new Button { Text = L.T("close"), Dock = DockStyle.Bottom, DialogResult = DialogResult.OK, AutoSize = true };
        dialog.Controls.Add(new TextBox { Text = L.Optional(error.Message), Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical });
        dialog.Controls.Add(new Label { Text = L.T(error is IOException or UnauthorizedAccessException ? "ioError" : "formatError"), Dock = DockStyle.Top, AutoSize = true, MaximumSize = new Size(650, 0), Padding = new Padding(12) });
        dialog.Controls.Add(close); dialog.AcceptButton = close; dialog.CancelButton = close;
        LauncherStyle.Apply(dialog);
        dialog.ShowDialog(this);
    }
}
