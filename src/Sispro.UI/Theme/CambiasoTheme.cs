using System.Drawing;
using System.Reflection;

namespace Sispro.UI.Theme;

public enum ButtonTone
{
    Primary,
    Accent,
    Secondary,
    Danger,
    NavItem
}

public static class CambiasoTheme
{
    public static readonly Color PrimaryDark = Color.FromArgb(42, 54, 20);
    public static readonly Color Primary = Color.FromArgb(58, 74, 28);
    public static readonly Color PrimaryHover = Color.FromArgb(74, 94, 36);
    public static readonly Color Accent = Color.FromArgb(128, 154, 34);
    public static readonly Color AccentHover = Color.FromArgb(110, 134, 26);
    public static readonly Color Surface = Color.FromArgb(245, 247, 242);
    public static readonly Color CardBackground = Color.White;
    public static readonly Color BorderColor = Color.FromArgb(212, 220, 198);
    public static readonly Color SubtleFill = Color.FromArgb(236, 241, 227);
    public static readonly Color TextPrimary = Color.FromArgb(30, 36, 23);
    public static readonly Color TextMuted = Color.FromArgb(90, 99, 80);
    public static readonly Color Danger = Color.FromArgb(184, 50, 39);
    public static readonly Color DangerHover = Color.FromArgb(156, 39, 30);
    public static readonly Color Success = Color.FromArgb(46, 125, 50);
    public static readonly Color Warning = Color.FromArgb(198, 122, 0);

    public static readonly Font TitleFont = new("Segoe UI", 14.5f, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI", 10f, FontStyle.Bold);
    public static readonly Font LabelFont = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    public static readonly Font SmallFont = new("Segoe UI", 8.5f, FontStyle.Regular);
    public static readonly Font ButtonFont = new("Segoe UI", 9f, FontStyle.Bold);
    public static readonly Font MetricNumberFont = new("Segoe UI", 22f, FontStyle.Bold);

    public static Button CreateButton(
        string text,
        AppIcon icon,
        ButtonTone tone = ButtonTone.Primary,
        ToolTip? toolTip = null,
        string? tooltipText = null,
        int width = 132,
        int height = 34)
    {
        Color back;
        Color fore;
        Color hover;
        Color border;

        switch (tone)
        {
            case ButtonTone.Primary:
                back = Primary;
                fore = Color.White;
                hover = PrimaryHover;
                border = PrimaryDark;
                break;
            case ButtonTone.Accent:
                back = Accent;
                fore = Color.White;
                hover = AccentHover;
                border = Primary;
                break;
            case ButtonTone.Danger:
                back = Danger;
                fore = Color.White;
                hover = DangerHover;
                border = DangerHover;
                break;
            case ButtonTone.NavItem:
                back = PrimaryDark;
                fore = Color.FromArgb(236, 242, 226);
                hover = Primary;
                border = PrimaryDark;
                break;
            default:
                back = Color.White;
                fore = TextPrimary;
                hover = SubtleFill;
                border = BorderColor;
                break;
        }

        Button btn = new()
        {
            Text = text,
            Font = ButtonFont,
            BackColor = back,
            ForeColor = fore,
            FlatStyle = FlatStyle.Flat,
            Width = width,
            Height = height,
            Cursor = Cursors.Hand,
            Image = IconCatalog.Get(icon, fore, 16),
            ImageAlign = ContentAlignment.MiddleLeft,
            TextAlign = ContentAlignment.MiddleCenter,
            TextImageRelation = TextImageRelation.ImageBeforeText,
            Padding = new Padding(8, 0, 8, 0),
            UseVisualStyleBackColor = false
        };

        btn.FlatAppearance.BorderSize = tone == ButtonTone.NavItem ? 0 : 1;
        btn.FlatAppearance.BorderColor = border;
        btn.FlatAppearance.MouseOverBackColor = hover;
        btn.FlatAppearance.MouseDownBackColor = tone == ButtonTone.Secondary ? BorderColor : PrimaryDark;

        btn.EnabledChanged += (_, _) =>
        {
            if (tone == ButtonTone.NavItem)
            {
                btn.ForeColor = btn.Enabled ? fore : Color.FromArgb(128, 140, 108);
                btn.Image = IconCatalog.Get(icon, btn.ForeColor, 16);
                return;
            }

            if (btn.Enabled)
            {
                btn.BackColor = back;
                btn.ForeColor = fore;
                btn.Image = IconCatalog.Get(icon, fore, 16);
            }
            else
            {
                btn.BackColor = Color.FromArgb(224, 228, 218);
                btn.ForeColor = TextMuted;
                btn.Image = IconCatalog.Get(icon, TextMuted, 16);
            }
        };

        if (toolTip is not null && !string.IsNullOrWhiteSpace(tooltipText))
        {
            toolTip.SetToolTip(btn, tooltipText);
        }

        return btn;
    }

    public static void StyleGrid(DataGridView grid)
    {
        EnableDoubleBuffering(grid);

        grid.BackgroundColor = CardBackground;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = BorderColor;
        grid.RowHeadersVisible = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersHeight = 32;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 28;

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Primary,
            ForeColor = Color.White,
            SelectionBackColor = Primary,
            SelectionForeColor = Color.White,
            Font = LabelFont,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 6, 0)
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = CardBackground,
            ForeColor = TextPrimary,
            Font = BodyFont,
            SelectionBackColor = Color.FromArgb(217, 230, 190),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(6, 2, 6, 2)
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(217, 230, 190),
            SelectionForeColor = TextPrimary
        };
    }

    public static GroupBox CreateGroupBox(string title) => new()
    {
        Text = title,
        Font = SectionFont,
        ForeColor = Primary,
        BackColor = CardBackground,
        Padding = new Padding(10, 8, 10, 8)
    };

    public static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        Font = LabelFont,
        ForeColor = TextMuted,
        AutoSize = true
    };

    public static Panel CreateMetricCard(string title, out Label valueLabel, Color accentColor)
    {
        Panel card = new()
        {
            BackColor = CardBackground,
            Height = 96,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 0, 12, 12)
        };
        card.Paint += (_, e) =>
        {
            using Pen borderPen = new(BorderColor, 1);
            e.Graphics.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);
            using SolidBrush bar = new(accentColor);
            e.Graphics.FillRectangle(bar, 0, 0, 5, card.Height);
        };

        Label lblTitle = new()
        {
            Text = title.ToUpperInvariant(),
            Font = SmallFont,
            ForeColor = TextMuted,
            Location = new Point(16, 12),
            AutoSize = true
        };

        valueLabel = new Label
        {
            Text = "0",
            Font = MetricNumberFont,
            ForeColor = TextPrimary,
            Location = new Point(16, 36),
            AutoSize = true
        };

        card.Controls.Add(lblTitle);
        card.Controls.Add(valueLabel);
        return card;
    }

    public static void EnableDoubleBuffering(Control control)
    {
        PropertyInfo? prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        prop?.SetValue(control, true, null);
    }

    public static void ShowInfo(IWin32Window? owner, string message, string title = "SISPRO — Información") =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    public static void ShowWarning(IWin32Window? owner, string message, string title = "SISPRO — Atención") =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static void ShowError(IWin32Window? owner, string message, string title = "SISPRO — Error") =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);

    public static bool Confirm(IWin32Window? owner, string message, string title = "SISPRO — Confirmación") =>
        MessageBox.Show(owner, message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
