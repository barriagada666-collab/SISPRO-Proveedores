using System.Drawing;
using System.Reflection;

namespace Sispro.UI.Theme;

public enum ButtonTone
{
    Primary,
    Accent,
    Secondary,
    Danger,
    NavItem,
    Gold
}

/// <summary>
/// Paleta corporativa oficial División Mantención Industrial — Cambiaso Hnos S.A.C. (2026):
/// Azul Petrol Profundo (#0A263C), Azul Industrial (#0E4163), Cyan Corporativo (#0089B6) y Dorado Cambiaso (#C8A977).
/// </summary>
public static class CambiasoTheme
{
    public static readonly Color PrimaryDark = Color.FromArgb(10, 38, 60);      // #0A263C
    public static readonly Color Primary = Color.FromArgb(14, 65, 99);          // #0E4163
    public static readonly Color PrimaryHover = Color.FromArgb(20, 84, 126);    // #14547E
    public static readonly Color Accent = Color.FromArgb(0, 137, 182);          // #0089B6
    public static readonly Color AccentHover = Color.FromArgb(0, 114, 154);     // #00729A
    public static readonly Color GoldAccent = Color.FromArgb(200, 169, 119);    // #C8A977
    public static readonly Color Surface = Color.FromArgb(242, 246, 249);       // #F2F6F9
    public static readonly Color CardBackground = Color.White;
    public static readonly Color BorderColor = Color.FromArgb(197, 214, 226);   // #C5D6E2
    public static readonly Color SubtleFill = Color.FromArgb(228, 239, 245);    // #E4EFF5
    public static readonly Color TextPrimary = Color.FromArgb(13, 34, 53);      // #0D2235
    public static readonly Color TextMuted = Color.FromArgb(74, 98, 120);       // #4A6278
    public static readonly Color Danger = Color.FromArgb(192, 57, 43);          // #C0392B
    public static readonly Color DangerHover = Color.FromArgb(162, 43, 31);     // #A22B1F
    public static readonly Color Success = Color.FromArgb(21, 128, 61);         // #15803D
    public static readonly Color Warning = Color.FromArgb(198, 122, 0);         // #C67A00

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
                fore = Color.FromArgb(224, 236, 245);
                hover = Primary;
                border = PrimaryDark;
                break;
            case ButtonTone.Gold:
                back = GoldAccent;
                fore = PrimaryDark;
                hover = Color.FromArgb(182, 150, 98);
                border = Color.FromArgb(160, 130, 80);
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
                btn.ForeColor = btn.Enabled ? fore : Color.FromArgb(100, 126, 148);
                btn.Image = IconCatalog.Get(icon, btn.ForeColor, 16);
                return;
            }

            if (btn.Enabled)
            {
                btn.BackColor = back;
                btn.ForeColor = fore;
                btn.FlatAppearance.BorderColor = border;
                btn.Image = IconCatalog.Get(icon, fore, 16);
            }
            else
            {
                btn.BackColor = Color.FromArgb(226, 233, 240);
                btn.ForeColor = TextMuted;
                btn.FlatAppearance.BorderColor = BorderColor;
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
            SelectionBackColor = Color.FromArgb(207, 232, 245),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(6, 2, 6, 2)
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextPrimary,
            SelectionBackColor = Color.FromArgb(207, 232, 245),
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

    public static Panel CreateMetricCard(
        string title,
        out Label valueLabel,
        Color accentColor,
        AppIcon? icon = null,
        string? subtitle = null)
    {
        Panel card = new()
        {
            BackColor = CardBackground,
            Height = 92,
            Padding = new Padding(14, 10, 12, 8),
            Margin = new Padding(0, 0, 10, 8)
        };
        EnableDoubleBuffering(card);
        card.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using Pen borderPen = new(BorderColor, 1);
            e.Graphics.DrawRectangle(borderPen, 0, 0, card.Width - 1, card.Height - 1);
            using SolidBrush bar = new(accentColor);
            e.Graphics.FillRectangle(bar, 0, 0, 4, card.Height);

            if (icon.HasValue)
            {
                int badgeSize = 34;
                Rectangle badgeRect = new(card.Width - badgeSize - 12, 12, badgeSize, badgeSize);
                using SolidBrush tint = new(Color.FromArgb(26, accentColor));
                e.Graphics.FillEllipse(tint, badgeRect);
                Bitmap ico = IconCatalog.Get(icon.Value, accentColor, 18);
                e.Graphics.DrawImage(ico, badgeRect.X + 8, badgeRect.Y + 8, 18, 18);
            }
        };

        Label lblTitle = new()
        {
            Text = title.ToUpperInvariant(),
            Font = new Font("Segoe UI", 7.8f, FontStyle.Bold),
            ForeColor = TextMuted,
            Location = new Point(14, 10),
            AutoSize = true
        };

        valueLabel = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI", 19.5f, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(14, 30),
            AutoSize = true
        };

        card.Controls.Add(lblTitle);
        card.Controls.Add(valueLabel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            card.Controls.Add(new Label
            {
                Text = subtitle,
                Font = new Font("Segoe UI", 7.8f, FontStyle.Regular),
                ForeColor = TextMuted,
                Location = new Point(14, 67),
                AutoSize = true
            });
        }

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
