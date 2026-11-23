using System.Drawing;
using System.Drawing.Drawing2D;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Pantalla de arranque corporativa de SISPRO (División Mantención Industrial — Cambiaso Hnos. S.A.C.).
/// </summary>
public sealed class SplashForm : Form
{
    private readonly Label _lblStatus;
    private int _progress;
    private readonly Panel _progressTrack;

    public SplashForm()
    {
        Text = "SISPRO — Iniciando";
        Icon = IconCatalog.GetAppIcon();
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(640, 340);
        BackColor = Color.White;
        ShowInTaskbar = false;
        CambiasoTheme.EnableDoubleBuffering(this);

        // Left Petrol-Blue Gradient Panel with golden Cambiaso logo
        Panel leftPanel = new()
        {
            Dock = DockStyle.Left,
            Width = 300
        };
        CambiasoTheme.EnableDoubleBuffering(leftPanel);
        leftPanel.Paint += (_, e) =>
        {
            using LinearGradientBrush grad = new(
                leftPanel.ClientRectangle,
                Color.FromArgb(0, 108, 148),
                CambiasoTheme.PrimaryDark,
                LinearGradientMode.ForwardDiagonal);
            e.Graphics.FillRectangle(grad, leftPanel.ClientRectangle);

            // Subtle geometric industrial accent rings
            using Pen ringPen = new(Color.FromArgb(42, 0, 185, 230), 2f);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawEllipse(ringPen, 34, 34, 232, 232);
            using Pen goldRing = new(Color.FromArgb(65, 200, 169, 119), 1.5f);
            e.Graphics.DrawEllipse(goldRing, 50, 50, 200, 200);
        };

        PictureBox picLogo = new()
        {
            Size = new Size(216, 130),
            Location = new Point(42, 74),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Image = IconCatalog.GetCompanyLogo()
        };

        Label lblDivision = new()
        {
            Text = "DIVISIÓN MANTENCIÓN INDUSTRIAL\nPlacilla, Valparaíso",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(205, 226, 240),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Dock = DockStyle.Bottom,
            Height = 52
        };

        leftPanel.Controls.Add(picLogo);
        leftPanel.Controls.Add(lblDivision);

        // Cyan vertical accent bar
        Panel accentBar = new()
        {
            Dock = DockStyle.Left,
            Width = 4,
            BackColor = CambiasoTheme.Accent
        };

        // Right Clean White Panel
        Panel rightPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(26, 48, 26, 20)
        };

        Label lblTitle = new()
        {
            Text = "SISPRO · Gestión Proveedores",
            Font = new Font("Segoe UI", 14.5f, FontStyle.Bold),
            ForeColor = CambiasoTheme.TextPrimary,
            Location = new Point(26, 68),
            AutoSize = true
        };

        Label lblDept = new()
        {
            Text = "Departamento Mantención",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Regular),
            ForeColor = CambiasoTheme.TextMuted,
            Location = new Point(26, 100),
            AutoSize = true
        };

        _progressTrack = new Panel
        {
            Location = new Point(26, 154),
            Size = new Size(280, 12),
            BackColor = CambiasoTheme.SubtleFill
        };
        CambiasoTheme.EnableDoubleBuffering(_progressTrack);
        _progressTrack.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using Pen border = new(CambiasoTheme.BorderColor, 1);
            e.Graphics.DrawRectangle(border, 0, 0, _progressTrack.Width - 1, _progressTrack.Height - 1);
            int fillWidth = (int)(_progressTrack.Width * (_progress / 100.0));
            if (fillWidth > 0)
            {
                using LinearGradientBrush fill = new(
                    new Rectangle(0, 0, Math.Max(1, fillWidth), _progressTrack.Height),
                    CambiasoTheme.Primary,
                    CambiasoTheme.Accent,
                    LinearGradientMode.Horizontal);
                e.Graphics.FillRectangle(fill, 1, 1, Math.Max(0, fillWidth - 2), _progressTrack.Height - 2);
            }
        };

        _lblStatus = new Label
        {
            Text = "Inicializando servicios y motor SQLite...",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Location = new Point(26, 174),
            Size = new Size(284, 38)
        };

        Label lblVersion = new()
        {
            Text = "Versión 3.0 LTS (.NET 10)",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Accent,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(26, 266),
            Size = new Size(284, 20)
        };

        Label lblCopyright = new()
        {
            Text = "Copyright © Cambiaso Hnos. S.A.C. 2026",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(26, 288),
            Size = new Size(284, 20)
        };

        rightPanel.Controls.AddRange([lblTitle, lblDept, _progressTrack, _lblStatus, lblVersion, lblCopyright]);

        Paint += (_, e) =>
        {
            using Pen framePen = new(CambiasoTheme.Primary, 1.5f);
            e.Graphics.DrawRectangle(framePen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        };

        Controls.Add(rightPanel);
        Controls.Add(accentBar);
        Controls.Add(leftPanel);
    }

    public void SetProgress(int percent, string statusText)
    {
        _progress = Math.Clamp(percent, 0, 100);
        _lblStatus.Text = statusText;
        _progressTrack.Invalidate();
        Refresh();
    }
}
