using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace CtrNfSave.Gui;

internal static class LauncherStyle
{
    public static readonly Color Base = Color.FromArgb(5, 26, 61), Panel = Color.FromArgb(5, 39, 82), Frame = Color.FromArgb(10, 61, 110),
        Cyan = Color.FromArgb(97, 232, 255), Text = Color.FromArgb(236, 249, 255), Muted = Color.FromArgb(166, 211, 239);
    /// <summary>Fonts follow Windows scaling, fixed pixel sizes do not: scale them with the control's DPI.</summary>
    public static int Px(Control control, int value) => (int)Math.Round(value * control.DeviceDpi / 96.0);
    private static readonly PrivateFontCollection DisplayFonts = new();
    private static IntPtr fontMemory;
    [DllImport("gdi32.dll", ExactSpelling = true)]
    private static extern IntPtr AddFontMemResourceEx(IntPtr data, uint size, IntPtr reserved, ref uint count);
    public static Font Display(float size)
    {
        if (fontMemory == IntPtr.Zero)
        {
            using var source = typeof(LauncherStyle).Assembly.GetManifestResourceStream("CtrNfSave.Gui.Assets.menu.ttf");
            if (source is not null)
            {
                using var memory = new MemoryStream(); source.CopyTo(memory); var bytes = memory.ToArray();
                fontMemory = Marshal.AllocCoTaskMem(bytes.Length); Marshal.Copy(bytes, 0, fontMemory, bytes.Length);
                DisplayFonts.AddMemoryFont(fontMemory, bytes.Length);
                uint count = 0; AddFontMemResourceEx(fontMemory, (uint)bytes.Length, IntPtr.Zero, ref count);
            }
        }
        return new Font(DisplayFonts.Families.FirstOrDefault() ?? new FontFamily("Segoe UI"), size);
    }
    public static void Apply(Control root)
    {
        if (root is LauncherHeader or LauncherMenuButton or SummaryStrip) return;
        root.ForeColor = Text;
        switch (root)
        {
            case DataGridView grid: EditorLayout.StyleGrid(grid); break;
            case TextBox box: box.BackColor = Frame; box.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox combo: combo.BackColor = Frame; combo.FlatStyle = FlatStyle.Flat; break;
            case ListBox list: list.BackColor = Panel; break;
            case NumericUpDown number: number.BackColor = Frame; break;
            case GroupBox group: group.BackColor = Panel; group.ForeColor = Cyan; break;
            case Button button:
                button.BackColor = Frame; button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderColor = Color.FromArgb(34, 107, 160);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(22, 116, 172); button.Padding = new Padding(12, 5, 12, 5); button.Cursor = Cursors.Hand; break;
            case ToolStrip strip:
                strip.BackColor = Color.FromArgb(7, 54, 108); strip.Renderer = new ToolStripProfessionalRenderer(new MenuColours());
                foreach (ToolStripItem item in strip.Items) { item.ForeColor = Text; item.Padding = new Padding(10, 6, 10, 6); }
                break;
            default: root.BackColor = root is Form ? Base : Panel; break;
        }
        foreach (Control child in root.Controls) Apply(child);
    }
    private sealed class MenuColours : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Panel;
        public override Color MenuItemSelected => Frame;
        public override Color ButtonSelectedGradientBegin => Frame;
        public override Color ButtonSelectedGradientEnd => Frame;
        public override Color MenuBorder => Frame;
        public override Color ImageMarginGradientBegin => Panel;
        public override Color ImageMarginGradientMiddle => Panel;
        public override Color ImageMarginGradientEnd => Panel;
    }
}

internal sealed class LauncherMenuButton : Button
{
    [DefaultValue(false)]
    public bool Selected { get; set; }
    private bool hover;
    public LauncherMenuButton() { DoubleBuffered = true; Font = LauncherStyle.Display(12); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(LauncherStyle.Panel);
        Point[] shape = [new(0, 5), new(Width - 13, 5), new(Width - 2, Height - 5), new(12, Height - 5)];
        using var fill = new SolidBrush(Selected ? Color.FromArgb(234, 250, 255) : hover ? Color.FromArgb(10, 82, 148) : Color.FromArgb(6, 55, 113));
        e.Graphics.FillPolygon(fill, shape);
        if (Selected) { using var border = new Pen(LauncherStyle.Cyan, 2); e.Graphics.DrawPolygon(border, shape); }
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(22, 0, Width - 37, Height), Selected ? Color.FromArgb(4, 46, 84) : LauncherStyle.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(15, 8, Width - 30, Height - 16));
    }
}

internal sealed class LauncherHeader : Control
{
    private Image? hero, logo;
    private readonly Font titleFont = LauncherStyle.Display(27);
    [DefaultValue("")]
    public string Subtitle { get; set; } = "";
    public LauncherHeader()
    {
        Dock = DockStyle.Fill; DoubleBuffered = true; Height = 170;
        SetStyle(ControlStyles.ResizeRedraw, true); // a partial repaint left a ghost of the centred title
        for (DirectoryInfo? parent = new(AppContext.BaseDirectory); parent is not null; parent = parent.Parent)
        {
            foreach (string candidate in new[] { Path.Combine(parent.FullName, "art"), Path.Combine(parent.FullName, "CTR Nitro Fueled", "art") })
            {
                if (!File.Exists(Path.Combine(candidate, "ctr-hero.png"))) continue;
                try { hero = Load(Path.Combine(candidate, "ctr-hero.png")); if (File.Exists(Path.Combine(candidate, "ctr-logo.png"))) logo = Load(Path.Combine(candidate, "ctr-logo.png")); }
                catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { }
                if (hero is not null) break;
            }
            if (hero is not null) break;
        }
    }
    private static Bitmap Load(string path)
    { using var stream = new MemoryStream(File.ReadAllBytes(path)); using var image = Image.FromStream(stream); return new Bitmap(image); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        var g = e.Graphics; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        using var backdrop = new LinearGradientBrush(ClientRectangle, Color.FromArgb(8, 96, 164), Color.FromArgb(3, 32, 83), 0f); g.FillRectangle(backdrop, ClientRectangle);
        if (hero is not null)
        {
            float scale = Math.Max((float)Width / hero.Width, (float)Height / hero.Height);
            g.DrawImage(hero, (Width - hero.Width * scale) / 2, (Height - hero.Height * scale) / 2, hero.Width * scale, hero.Height * scale);
            using var shade = new SolidBrush(Color.FromArgb(90, 0, 22, 55)); g.FillRectangle(shade, ClientRectangle);
        }
        if (logo is not null)
        {
            float h = Height - 28, w = Math.Min(270, h * logo.Width / logo.Height); h = w * logo.Height / logo.Width;
            g.DrawImage(logo, 24, (Height - h) / 2, w, h);
        }
        else TextRenderer.DrawText(g, "CTR NITRO-FUELED", titleFont, new Point(25, 35), LauncherStyle.Text);
        int left = logo is null ? Width / 2 : Math.Max(325, Width / 2);
        TextRenderer.DrawText(g, "SAVE EDITOR", titleFont, new Rectangle(left, 34, Width - left - 24, 54), LauncherStyle.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        using var subtitle = new Font("Segoe UI", 10); TextRenderer.DrawText(g, Subtitle, subtitle, new Rectangle(left + 2, 91, Width - left - 28, 55), LauncherStyle.Text, TextFormatFlags.WordBreak);
        using var stripe = new SolidBrush(Color.FromArgb(238, 169, 48)); g.FillRectangle(stripe, 0, Height - 4, Width, 4);
    }
    protected override void Dispose(bool disposing)
    { if (disposing) { hero?.Dispose(); logo?.Dispose(); titleFont.Dispose(); } base.Dispose(disposing); }
}

internal sealed class SummaryStrip : FlowLayoutPanel
{
    public SummaryStrip() { Dock = DockStyle.Fill; AutoSize = true; WrapContents = true; Padding = new Padding(16, 8, 16, 8); BackColor = LauncherStyle.Base; }
    public void SetCards(params (string Value, string Caption)[] values)
    {
        SuspendLayout(); foreach (Control old in Controls.Cast<Control>().ToArray()) old.Dispose();
        foreach (var item in values)
        {
            var valueFont = LauncherStyle.Display(20); var captionFont = new Font("Segoe UI", 9);
            int valueHeight = TextRenderer.MeasureText(item.Value, valueFont).Height + LauncherStyle.Px(this, 8);
            int width = Math.Max(LauncherStyle.Px(this, 145), Math.Max(TextRenderer.MeasureText(item.Value, valueFont).Width + LauncherStyle.Px(this, 22),
                TextRenderer.MeasureText(item.Caption, captionFont).Width + LauncherStyle.Px(this, 24)));
            var card = new Panel { Width = width, Height = valueHeight + TextRenderer.MeasureText(item.Caption, captionFont).Height + LauncherStyle.Px(this, 10),
                BackColor = LauncherStyle.Panel, Margin = new Padding(4) };
            card.Controls.Add(new Label { Text = item.Caption, Dock = DockStyle.Fill, ForeColor = LauncherStyle.Muted, Padding = new Padding(12, 0, 0, 0), Font = captionFont });
            card.Controls.Add(new Label { Text = item.Value, Dock = DockStyle.Top, Height = valueHeight, ForeColor = LauncherStyle.Cyan, Padding = new Padding(10, 5, 0, 0), Font = valueFont });
            Controls.Add(card);
        }
        ResumeLayout();
    }
}
