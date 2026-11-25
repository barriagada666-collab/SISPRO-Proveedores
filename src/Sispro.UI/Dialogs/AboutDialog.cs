using System.Drawing;
using Sispro.Core;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Ventana modal informativa "Acerca de" alimentada desde la configuración centralizada de versión (AppMetadata).
/// </summary>
public sealed class AboutDialog : Form
{
    private readonly ToolTip _toolTip = new();

    internal Label LblSoftwareName { get; }
    internal Label LblVersion { get; }
    internal Label LblReleaseDate { get; }
    internal Label LblCompany { get; }

    public AboutDialog(SisproApplicationContext? ctx = null)
    {
        Text = $"Acerca de {AppMetadata.ShortName}";
        Icon = IconCatalog.GetAppIcon();
        ClientSize = new Size(536, 412);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 96,
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(18, 12, 18, 12)
        };

        PictureBox picLogo = new()
        {
            Size = new Size(124, 70),
            Location = new Point(16, 12),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Image = IconCatalog.GetCompanyLogo()
        };

        LblSoftwareName = new Label
        {
            Text = AppMetadata.SoftwareName,
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Location = new Point(154, 16),
            Size = new Size(340, 42)
        };

        LblVersion = new Label
        {
            Text = $"Versión {AppMetadata.Version} (.NET 10)",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.GoldAccent,
            Location = new Point(154, 62),
            AutoSize = true
        };

        header.Controls.AddRange([picLogo, LblSoftwareName, LblVersion]);

        Panel body = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 16, 22, 12),
            BackColor = CambiasoTheme.Surface
        };

        TableLayoutPanel infoGrid = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 5,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(14, 12, 14, 12)
        };
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        LblReleaseDate = AddInfoRow(infoGrid, 0, "Fecha de versión:", AppMetadata.ReleaseDate);
        LblCompany = AddInfoRow(infoGrid, 1, "Empresa desarrolladora:", $"{AppMetadata.DeveloperCompany} — Departamento Mantención");
        AddInfoRow(infoGrid, 2, "Plataforma:", ".NET 10 WinForms · SQLite 3 (WAL)");
        AddInfoRow(infoGrid, 3, "Empresa configurada:", ctx?.Settings.CompanyName ?? AppMetadata.DeveloperCompany);
        AddInfoRow(infoGrid, 4, "Descripción:", AppMetadata.Description);

        body.Controls.Add(infoGrid);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(14, 10, 18, 10),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Check, ButtonTone.Primary, _toolTip, "Cerrar ventana Acerca de", width: 118, height: 32);
        btnClose.Click += (_, _) => Close();
        footer.Controls.Add(btnClose);

        AcceptButton = btnClose;
        CancelButton = btnClose;

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
    }

    private static Label AddInfoRow(TableLayoutPanel grid, int row, string caption, string value)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Label lblCap = new()
        {
            Text = caption,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 4, 8, 4)
        };
        Label lblVal = new()
        {
            Text = value,
            Font = CambiasoTheme.BodyFont,
            ForeColor = CambiasoTheme.TextPrimary,
            AutoSize = true,
            MaximumSize = new Size(275, 0),
            Margin = new Padding(0, 4, 0, 4)
        };
        grid.Controls.Add(lblCap, 0, row);
        grid.Controls.Add(lblVal, 1, row);
        return lblVal;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
