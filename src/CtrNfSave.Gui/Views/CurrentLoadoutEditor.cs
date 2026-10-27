using System.Globalization;
using CtrNfSave;

namespace CtrNfSave.Gui;

/// <summary>Stages part choices; the host applies the completed preset through its normal dirty/save flow.</summary>
internal sealed class CurrentLoadoutEditor : UserControl
{
    public event Action<KartPreset>? ApplyChanges;
    private readonly ComboBox driver = Picker(), skin = Picker(), chassis = Picker(), wheels = Picker(), paint = Picker(), sticker = Picker();
    private readonly Label warning = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly Save save;
    private readonly KartPreset original;
    private KartPreset pending;
    private bool loading;
    private readonly bool french = L.Language == "fr";
    private static ComboBox Picker() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, DropDownWidth = 350 };

    public CurrentLoadoutEditor(Save save, KartPreset active)
    {
        this.save = save; original = pending = active;
        Dock = DockStyle.Top; AutoSize = true;
        var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, RowCount = 5 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        void Part(string key, ComboBox picker, int column, int row)
        {
            grid.Controls.Add(new Label { Text = L.T(key), AutoSize = true, Anchor = AnchorStyles.Left }, column, row);
            grid.Controls.Add(picker, column + 1, row);
        }
        Part("garageDriver", driver, 0, 0); Part("garageSkin", skin, 2, 0);
        Part("garageChassis", chassis, 0, 1); Part("garageWheels", wheels, 2, 1);
        Part("garagePaint", paint, 0, 2); Part("garageSticker", sticker, 2, 2);
        grid.Controls.Add(warning, 0, 3); grid.SetColumnSpan(warning, 4);
        var buttons = EditorLayout.FilterBar(); grid.Controls.Add(buttons, 0, 4); grid.SetColumnSpan(buttons, 4);
        var apply = new Button { Text = L.T("garageApplyChanges"), AutoSize = true };
        apply.Click += (_, _) => ApplyChanges?.Invoke(pending);
        var reset = new Button { Text = L.T("garageReset"), AutoSize = true };
        reset.Click += (_, _) => { pending = original; Populate(); };
        buttons.Controls.Add(apply); buttons.Controls.Add(reset); Controls.Add(grid);
        void Change(ComboBox picker, Func<string, KartPreset> edit)
        {
            picker.SelectedIndexChanged += (_, _) =>
            {
                if (loading || picker.SelectedItem is not LoadoutCatalogue.Choice choice) return;
                try { pending = edit(choice.Value); }
                catch (SaveFormatException error)
                { MessageBox.Show(this, L.F("garageError", error.Message), L.T("garage"), MessageBoxButtons.OK, MessageBoxIcon.Error); }
                Populate();
            };
        }
        Change(driver, value => KartLoadout.PrepareEdit(pending, driver: value));
        Change(skin, value => KartLoadout.PrepareEdit(pending, skin: int.Parse(value, CultureInfo.InvariantCulture)));
        Change(chassis, value => LoadoutCatalogue.Specials().Any(c => c.Value == value)
            ? KartLoadout.PrepareEdit(pending, special: value) : KartLoadout.PrepareEdit(pending, chassis: value));
        Change(wheels, value => KartLoadout.PrepareEdit(pending, wheels: int.Parse(value, CultureInfo.InvariantCulture)));
        Change(paint, value => KartLoadout.PrepareEdit(pending, paint: int.Parse(value, CultureInfo.InvariantCulture)));
        Change(sticker, value => KartLoadout.PrepareEdit(pending, sticker: int.Parse(value, CultureInfo.InvariantCulture)));
        SizeChanged += (_, _) => warning.MaximumSize = new Size(Math.Max(200, ClientSize.Width - 12), 0);
        Populate();
    }
    private void Populate()
    {
        loading = true;
        try
        {
            void Fill(ComboBox picker, IEnumerable<LoadoutCatalogue.Choice> choices, string selected, string label)
            {
                var items = choices.ToList();
                if (!items.Any(c => c.Value == selected)) items.Add(new(selected, label));
                picker.Items.Clear(); picker.Items.AddRange(items.Cast<object>().ToArray());
                picker.SelectedIndex = items.FindIndex(c => c.Value == selected);
            }
            static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
            Fill(driver, LoadoutCatalogue.Drivers(french), pending.Driver, KartLoadout.DriverName(pending.Driver, french));
            Fill(skin, LoadoutCatalogue.Skins(pending.Driver, french), Number(pending.Skin), KartLoadout.SkinName(pending.Driver, pending.Skin, french));
            Fill(chassis, LoadoutCatalogue.Chassis(french).Concat(LoadoutCatalogue.Specials(french)),
                LoadoutCatalogue.SelectedSpecial(pending) ?? pending.Chassis, KartLoadout.ChassisName(pending, french));
            Fill(wheels, LoadoutCatalogue.Wheels(french), Number(pending.Wheels), KartLoadout.WheelsName(pending.Wheels, french));
            Fill(paint, LoadoutCatalogue.Paints(french), Number(pending.Paint), KartLoadout.PaintName(pending.Paint, french));
            Fill(sticker, LoadoutCatalogue.Stickers(french), Number(pending.Sticker), KartLoadout.StickerName(pending.Sticker, french));
            wheels.Enabled = paint.Enabled = sticker.Enabled = LoadoutCatalogue.Chassis().Any(c => c.Value == pending.Chassis);
            var owned = KartLoadout.Ownership(save, pending);
            warning.Text = L.T(owned.Driver is false ? "garageDriverLocked" : owned.Driver is true ? "garageDriverOwned" : "garageDriverUnknown") +
                "\r\n" + L.T("garageOwnershipUnknown");
        }
        finally { loading = false; }
    }
}
