namespace CtrNfSave.Gui;

/// <summary>Task navigation with a persistent rail and one uncluttered workspace.</summary>
internal sealed class SectionNavigator : UserControl
{
    private readonly FlowLayoutPanel rail = new() { Dock = DockStyle.Left, Width = 224, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(10, 18, 10, 12), BackColor = LauncherStyle.Panel };
    private readonly Panel workspace = new() { Dock = DockStyle.Fill, Padding = new Padding(22, 16, 22, 18), BackColor = LauncherStyle.Panel };
    private readonly Label heading = new() { Dock = DockStyle.Top, Height = 48, Font = LauncherStyle.Display(20), ForeColor = LauncherStyle.Text };
    private readonly List<Panel> pages = [];
    private readonly List<Button> buttons = [];
    private int selected = -1;
    public IReadOnlyList<Panel> Pages => pages;
    public event EventHandler? SelectedIndexChanged;
    public SectionNavigator()
    {
        Dock = DockStyle.Fill; Font = new Font("Segoe UI", 10);
        var host = new Panel { Dock = DockStyle.Fill };
        workspace.Controls.Add(host); workspace.Controls.Add(heading);
        Controls.Add(workspace); Controls.Add(rail);
        ContentHost = host;
    }
    private Panel ContentHost { get; }
    public Panel AddPage(string title)
    {
        int index = pages.Count;
        var page = new Panel { Dock = DockStyle.Fill, Text = title, Visible = false, BackColor = LauncherStyle.Panel, Padding = new Padding(12) };
        var button = new LauncherMenuButton
        {
            Text = title,
            Width = 198,
            Height = 48,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 6, 0),
            Margin = new Padding(0, 4, 0, 4),
            ForeColor = Color.FromArgb(204, 216, 230),
            BackColor = rail.BackColor,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderSize = 0; button.FlatAppearance.MouseOverBackColor = Color.FromArgb(43, 65, 91);
        button.Click += (_, _) => SelectedIndex = index;
        pages.Add(page); buttons.Add(button); ContentHost.Controls.Add(page); rail.Controls.Add(button);
        if (selected < 0) SelectedIndex = 0;
        return page;
    }
    public void SetTitle(int index, string title)
    { pages[index].Text = title; buttons[index].Text = title; if (index == selected) heading.Text = title; }
    [System.ComponentModel.DefaultValue(-1)]
    public int SelectedIndex
    {
        get => selected;
        set
        {
            if (value < 0 || value >= pages.Count) return;
            selected = value;
            for (int i = 0; i < pages.Count; i++)
            {
                pages[i].Visible = i == value;
                ((LauncherMenuButton)buttons[i]).Selected = i == value; buttons[i].Invalidate();
                buttons[i].BackColor = i == value ? Color.FromArgb(255, 198, 68) : rail.BackColor;
                buttons[i].ForeColor = i == value ? rail.BackColor : Color.FromArgb(204, 216, 230);
            }
            heading.Text = pages[value].Text; SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

internal static class EditorLayout
{
    public static void StyleGrid(DataGridView grid)
    {
        grid.BorderStyle = BorderStyle.None; grid.BackgroundColor = LauncherStyle.Panel;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(22, 66, 111);
        grid.EnableHeadersVisualStyles = false; grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(8, 60, 115);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = LauncherStyle.Muted;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10);
        grid.DefaultCellStyle.BackColor = LauncherStyle.Panel; grid.DefaultCellStyle.ForeColor = LauncherStyle.Text;
        grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(15, 90, 148);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(5, 46, 92);
        grid.RowTemplate.Height = 34;
    }
    public static TextBox SearchBox() => new() { Width = 280, PlaceholderText = L.T("search"), Margin = new Padding(4, 6, 12, 6) };
    public static FlowLayoutPanel FilterBar() => new() { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4, 6, 4, 10), WrapContents = true };
    public static void StyleList(CheckedListBox list)
    { list.BorderStyle = BorderStyle.None; list.Font = new Font("Segoe UI", 11); list.ItemHeight = 30; list.IntegralHeight = false; }
}
