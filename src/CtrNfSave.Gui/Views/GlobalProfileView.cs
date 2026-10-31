using CtrNfSave;
using System.Globalization;
namespace CtrNfSave.Gui;

internal sealed class GlobalProfileView : UserControl
{
    public event Action<string, string>? Edit;
    public event Action<string[], bool>? BulkEdit;
    public event Action<string[]>? UnlockPitStop;
    public event Action<KartPreset>? ApplyPreset;
    private int selectedTab;
    private int selectedSkinDriver;
    private string characterQuery = "", itemQuery = "";
    private int itemCategory;
    private string? selectedTimeTrack;
    private TimeAttackView? timeAttack;
    public bool CommitEdits() => timeAttack is null || timeAttack.IsDisposed || timeAttack.CommitEdits();
    public GlobalProfileView() { Dock = DockStyle.Fill; }
    public void Bind(Save save)
    {
        int tabIndex = selectedTab;
        var previousList = Controls.OfType<SectionNavigator>().SelectMany(t => t.Pages).SelectMany(p => p.Controls.OfType<TableLayoutPanel>()).SelectMany(p => p.Controls.OfType<CheckedListBox>()).FirstOrDefault();
        int topIndex = previousList?.TopIndex ?? 0;
        foreach (Control c in Controls.Cast<Control>().ToArray()) c.Dispose();
        var tabs = new SectionNavigator();
        Controls.Add(tabs);
        var profile = Page("globalProfile");
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(12) };
        profile.Controls.Add(flow);
        var allActions = EditorLayout.FilterBar();
        var unlockAll = new Button { Text = L.T("unlockAll"), AutoSize = true };
        var lockAll = new Button { Text = L.T("lockAll"), AutoSize = true };
        unlockAll.Click += (_, _) => BulkEdit?.Invoke(UnlockBatch.Keys(save, UnlockScope.All), true);
        lockAll.Click += (_, _) => BulkEdit?.Invoke(UnlockBatch.Keys(save, UnlockScope.All), false);
        allActions.Controls.Add(unlockAll); allActions.Controls.Add(lockAll); flow.Controls.Add(allActions);
        flow.Controls.Add(new Label { Text = L.T("bulkUnlockScope"), AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(4, 4, 4, 20) });
        var pitStop = new Button { Text = L.T("unlockPitStop"), AutoSize = true };
        pitStop.Click += (_, _) => UnlockPitStop?.Invoke(UnlockBatch.Keys(save, UnlockScope.PitStop));
        flow.Controls.Add(pitStop);
        flow.Controls.Add(new Label { Text = L.T("pitStopScope"), AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(4, 4, 4, 20) });
        flow.Controls.Add(new Label { Text = L.T("coins"), AutoSize = true });
        var coins = new NumericUpDown
        {
            Minimum = 0,
            Maximum = int.MaxValue,
            Width = 220,
            ThousandsSeparator = true,
            Value = Convert.ToDecimal(save.Semantic()["global.wumpa_coins"], CultureInfo.InvariantCulture)
        };
        flow.Controls.Add(coins);
        var apply = new Button { Text = L.T("Appliquer"), AutoSize = true };
        apply.Click += (_, _) => Edit?.Invoke("global.wumpa_coins", coins.Value.ToString(CultureInfo.InvariantCulture));
        flow.Controls.Add(apply);
        flow.Controls.Add(new Label { Text = L.T("eggEvidence"), AutoSize = true, MaximumSize = new Size(800, 0), Margin = new Padding(3, 20, 3, 10) });
        var eggs = save.Graph.Fields.Where(f => f.Type == "egg-bit").ToArray();
        if (eggs.Length == 0) flow.Controls.Add(new Label { Text = L.T("eggsUnavailable"), AutoSize = true });
        foreach (var f in eggs)
        {
            var box = new CheckBox { Text = L.F("eggBit", L.T("egg" + f.Key.Split('_')[^1])), Checked = (bool)f.Value, AutoSize = true };
            box.CheckedChanged += (_, _) => Edit?.Invoke(f.Key, box.Checked ? "true" : "false");
            flow.Controls.Add(box);
        }
        flow.Controls.Add(new Label { Text = L.F("eggCount", save.Semantic().GetValueOrDefault("global.golden_egg.count") ?? "?"), AutoSize = true });
        flow.Controls.Add(new Label { Text = L.T("timeTrialRewards"), AutoSize = true, Margin = new Padding(3, 20, 3, 8) });
        foreach (var reward in TimeTrialRewards.All)
        {
            var field = save.Graph.Fields.SingleOrDefault(f => f.Key == reward.Key && f.Type == "reward-flag");
            if (field is null) continue;
            var box = new CheckBox { Text = L.Language == "fr" ? reward.NameFr : reward.NameEn, AutoSize = true, Checked = field.Value is true };
            box.CheckedChanged += (_, _) => Edit?.Invoke(reward.Key, box.Checked ? "true" : "false"); flow.Controls.Add(box);
        }
        var characters = Page("characters");
        var characterLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        characterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        characterLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        characterLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        characters.Controls.Add(characterLayout);
        characterLayout.Controls.Add(new Label { Text = L.T("characterWarning"), AutoSize = true, MaximumSize = new Size(850, 0), Padding = new Padding(8) });
        var checkAll = new Button { Text = L.T("checkAllCharacters"), AutoSize = true };
        checkAll.Click += (_, _) => BulkEdit?.Invoke(UnlockBatch.Keys(save, UnlockScope.Characters), true);
        var lockCharacters = new Button { Text = L.T("lockAll"), AutoSize = true };
        lockCharacters.Click += (_, _) => BulkEdit?.Invoke(UnlockBatch.Keys(save, UnlockScope.Characters), false);
        var characterFilters = EditorLayout.FilterBar();
        var characterSearch = EditorLayout.SearchBox(); characterSearch.Text = characterQuery;
        characterFilters.Controls.Add(characterSearch); characterFilters.Controls.Add(checkAll); characterFilters.Controls.Add(lockCharacters);
        characterLayout.Controls.Add(characterFilters);
        var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        EditorLayout.StyleList(list);
        var drivers = GlobalUnlocks.Drivers.ToArray();
        var semantics = save.Semantic();
        DriverEntry[] visibleDrivers = []; bool bindingDrivers = false;
        void BindDrivers()
        {
            bindingDrivers = true;
            try
            {
                visibleDrivers = drivers.Where(d => (d.NameEn + " " + d.NameFr).Contains(characterQuery, StringComparison.OrdinalIgnoreCase)).ToArray(); list.Items.Clear();
                foreach (var driver in visibleDrivers)
                {
                    string name = L.Language == "fr" ? driver.NameFr : driver.NameEn;
                    string evidence = driver.MarketplaceIds.Length > 0 ? L.T("localPurchase") : driver.Key is null ? L.T("baseCharacter") : L.T("conditionCharacter");
                    list.Items.Add(name + "   ·   " + evidence, driver.Key is null || semantics.GetValueOrDefault(driver.Key) is true);
                }
            }
            finally { bindingDrivers = false; }
        }
        BindDrivers(); characterSearch.TextChanged += (_, _) => { characterQuery = characterSearch.Text; BindDrivers(); };
        checkAll.Enabled = semantics.ContainsKey(GlobalUnlocks.ItemKey(618));
        list.ItemCheck += (_, e) =>
        {
            if (bindingDrivers) return;
            string? key = visibleDrivers[e.Index].Key;
            if (key is null || !semantics.ContainsKey(key)) { e.NewValue = e.CurrentValue; return; }
            Edit?.Invoke(key, e.NewValue == CheckState.Checked ? "true" : "false");
        };
        characterLayout.Controls.Add(list);
        var items = Page("profileItems");
        var itemLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        itemLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); itemLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); itemLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        items.Controls.Add(itemLayout);
        itemLayout.Controls.Add(new Label { Text = L.T("marketplaceEvidence"), AutoSize = true, MaximumSize = new Size(900, 0), Padding = new Padding(8) });
        var itemList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        EditorLayout.StyleList(itemList);
        var catalogue = CustomizationUnlocks.Items.ToArray();
        var itemFilters = EditorLayout.FilterBar(); var itemSearch = EditorLayout.SearchBox(); itemSearch.Text = itemQuery;
        var categories = catalogue.Select(i => i.Type).Distinct().ToArray();
        var categoryPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Margin = new Padding(4, 6, 4, 6) };
        categoryPicker.Items.Add(L.T("allCategories")); categoryPicker.Items.AddRange(categories.Select(L.T).Cast<object>().ToArray()); categoryPicker.SelectedIndex = Math.Clamp(itemCategory, 0, categories.Length);
        var unlockVisible = new Button { AutoSize = true, Text = L.T("unlockVisibleItems") };
        var lockVisible = new Button { AutoSize = true, Text = L.T("lockVisibleItems") };
        itemFilters.Controls.Add(itemSearch); itemFilters.Controls.Add(categoryPicker); itemFilters.Controls.Add(unlockVisible); itemFilters.Controls.Add(lockVisible); itemLayout.Controls.Add(itemFilters);
        CustomizationEntry[] visibleItems = []; bool bindingItems = false;
        void BindItems()
        {
            bindingItems = true;
            try
            {
                visibleItems = catalogue.Where(i => (itemCategory == 0 || i.Type == categories[itemCategory - 1]) && (i.NameEn + " " + i.NameFr).Contains(itemQuery, StringComparison.OrdinalIgnoreCase)).ToArray(); itemList.Items.Clear();
                foreach (var item in visibleItems)
                {
                    var reward = TimeTrialRewards.All.SingleOrDefault(r => r.Key == item.RewardKey);
                    string condition = reward is null ? "" : " · " + (L.Language == "fr" ? reward.NameFr : reward.NameEn);
                    itemList.Items.Add((L.Language == "fr" ? item.NameFr : item.NameEn) + "   ·   " + L.T(item.Type) + condition + (semantics.GetValueOrDefault(item.Key) is bool ? "" : " · " + L.T("unknownOwnership")), semantics.GetValueOrDefault(item.Key) is true);
                }
            }
            finally { bindingItems = false; }
        }
        BindItems(); itemSearch.TextChanged += (_, _) => { itemQuery = itemSearch.Text; BindItems(); };
        unlockVisible.Click += (_, _) => BulkEdit?.Invoke(visibleItems.Where(i => i.CanEdit && semantics.GetValueOrDefault(i.Key) is bool).Select(i => i.Key).ToArray(), true);
        lockVisible.Click += (_, _) => BulkEdit?.Invoke(visibleItems.Where(i => i.CanEdit && semantics.GetValueOrDefault(i.Key) is bool).Select(i => i.Key).ToArray(), false);
        categoryPicker.SelectedIndexChanged += (_, _) => { itemCategory = categoryPicker.SelectedIndex; BindItems(); };
        itemList.Enabled = semantics.ContainsKey(GlobalUnlocks.ItemKey(618));
        itemList.ItemCheck += (_, e) =>
        {
            if (bindingItems) return;
            var item = visibleItems[e.Index];
            if (!item.CanEdit || semantics.GetValueOrDefault(item.Key) is not bool) { e.NewValue = e.CurrentValue; return; }
            Edit?.Invoke(item.Key, e.NewValue == CheckState.Checked ? "true" : "false");
        };
        itemLayout.Controls.Add(itemList);
        var skins = Page("skins");
        var skinLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        skinLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); skinLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        skinLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); skinLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); skinLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        skins.Controls.Add(skinLayout);
        var skinNote = new Label { Text = L.T("skinEvidence"), AutoSize = true, MaximumSize = new Size(900, 0), Padding = new Padding(8) };
        skinLayout.Controls.Add(skinNote, 0, 0); skinLayout.SetColumnSpan(skinNote, 2);
        var driverPicker = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 11), ItemHeight = 30, Margin = new Padding(0, 8, 18, 0) };
        driverPicker.Items.AddRange(drivers.Select(d => L.Language == "fr" ? d.NameFr : d.NameEn).Cast<object>().ToArray());
        skinLayout.Controls.Add(driverPicker, 0, 2);
        var skinList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
        EditorLayout.StyleList(skinList); skinLayout.Controls.Add(skinList, 1, 2);
        var skinActions = EditorLayout.FilterBar();
        var unlockSkins = new Button { AutoSize = true, Text = L.T("unlockDriverSkins") }; var lockSkins = new Button { AutoSize = true, Text = L.T("lockDriverSkins") };
        unlockSkins.Click += (_, _) => { if (driverPicker.SelectedIndex >= 0) BulkEdit?.Invoke(UnlockBatch.Keys(save, UnlockScope.Skins, drivers[driverPicker.SelectedIndex].Driver), true); };
        lockSkins.Click += (_, _) => { if (driverPicker.SelectedIndex >= 0) BulkEdit?.Invoke(UnlockBatch.Keys(save, UnlockScope.Skins, drivers[driverPicker.SelectedIndex].Driver), false); };
        skinActions.Controls.Add(unlockSkins); skinActions.Controls.Add(lockSkins); skinLayout.Controls.Add(skinActions, 0, 1); skinLayout.SetColumnSpan(skinActions, 2);
        SkinEntry[] visibleSkins = []; bool bindingSkins = false;
        void BindSkins()
        {
            if (driverPicker.SelectedIndex < 0) return;
            selectedSkinDriver = driverPicker.SelectedIndex; bindingSkins = true;
            try
            {
                visibleSkins = SkinUnlocks.Skins.Where(s => s.Driver == drivers[selectedSkinDriver].Driver).ToArray(); skinList.Items.Clear();
                foreach (var skin in visibleSkins)
                {
                    string condition = skin.Wins is int wins ? L.F("skinWins", wins) : skin.MarketplaceIds.Length > 0 ? L.T("localPurchase") : skin.Condition is null ? L.T("baseCharacter") : L.T("conditionCharacter");
                    skinList.Items.Add((L.Language == "fr" ? skin.NameFr : skin.NameEn) + " — " + condition, semantics.GetValueOrDefault(skin.Key) is true);
                }
            }
            finally { bindingSkins = false; }
        }
        driverPicker.SelectedIndexChanged += (_, _) => BindSkins();
        driverPicker.SelectedIndex = Math.Clamp(selectedSkinDriver, 0, drivers.Length - 1);
        skinList.ItemCheck += (_, e) =>
        {
            if (bindingSkins) return;
            var skin = visibleSkins[e.Index];
            if (skin.Condition is null || !semantics.ContainsKey(skin.Key)) { e.NewValue = e.CurrentValue; return; }
            Edit?.Invoke(skin.Key, e.NewValue == CheckState.Checked ? "true" : "false");
        };
        var masks = Page("unknownMasks");
        var maskGrid = Table(masks, ("key", "itemName"), ("value", "value"), ("state", "ownership"));
        foreach (var f in save.Graph.Fields.Where(f => f.Key != "global.unknown_mask_16.count" && f.Key.StartsWith("global.unknown_mask_", StringComparison.Ordinal) && f.Key.EndsWith(".count", StringComparison.Ordinal)))
            maskGrid.Rows.Add(f.Key, $"{f.Value}/{save.Semantic()[f.Key.Replace(".count", ".capacity")]}", L.T("unknownOwnership"));
        foreach (var f in save.Graph.Fields.Where(f => f.Key.StartsWith("global.unknown.patch_", StringComparison.Ordinal)))
            maskGrid.Rows.Add(f.Key, L.T((bool)f.Value ? "true" : "false"), L.T("unknownOwnership"));
        var tt = Page("timeTrials");
        var timeView = new TimeAttackView(save, selectedTimeTrack);
        timeAttack = timeView;
        timeView.Edit += (key, value) => Edit?.Invoke(key, value);
        timeView.TrackChanged += track => selectedTimeTrack = track;
        tt.Controls.Add(timeView);
        var garage = new GarageView(save);
        garage.ApplyPreset += preset => ApplyPreset?.Invoke(preset);
        Page("garage").Controls.Add(garage);
        tabs.SelectedIndex = Math.Clamp(tabIndex, 0, tabs.Pages.Count - 1);
        if (list.Items.Count > 0 && IsHandleCreated)
            BeginInvoke((Action)(() => { if (!list.IsDisposed) list.TopIndex = Math.Min(topIndex, list.Items.Count - 1); }));
        tabs.SelectedIndexChanged += (_, _) => selectedTab = tabs.SelectedIndex;
        LauncherStyle.Apply(tabs);
        Panel Page(string key) => tabs.AddPage(L.T(key));
    }
    private static DataGridView Table(Control parent, params (string Name, string Label)[] columns)
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window
        };
        foreach (var c in columns) grid.Columns.Add(c.Name, L.T(c.Label));
        EditorLayout.StyleGrid(grid);
        parent.Controls.Add(grid); return grid;
    }
}
