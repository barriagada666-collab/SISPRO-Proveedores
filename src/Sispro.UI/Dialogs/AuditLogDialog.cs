using System.Drawing;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

public sealed class AuditLogDialog : Form
{
    private readonly IAuditRepository _auditRepo;
    private readonly ToolTip _toolTip = new();
    private readonly DataGridView _grid;

    public AuditLogDialog(IAuditRepository auditRepo)
    {
        _auditRepo = auditRepo;

        Text = "Bitácora de Auditoría — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(960, 560);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 52, BackColor = CambiasoTheme.Primary };
        header.Controls.Add(new Label
        {
            Text = "Registro de Auditoría y Seguridad (últimos 300 eventos)",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 15)
        });

        _grid = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_grid);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 16, 8),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Exit, ButtonTone.Secondary, _toolTip, "Cerrar bitácora", width: 118);
        btnClose.Click += (_, _) => Close();

        Button btnRefresh = CambiasoTheme.CreateButton("Actualizar", AppIcon.Refresh, ButtonTone.Primary, _toolTip, "Recargar eventos de auditoría", width: 128);
        btnRefresh.Click += (_, _) => LoadEntries();

        footer.Controls.Add(btnClose);
        footer.Controls.Add(btnRefresh);

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(12) };
        body.Controls.Add(_grid);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);

        LoadEntries();
    }

    private void LoadEntries()
    {
        IReadOnlyList<AuditEntry> entries = _auditRepo.GetRecent(300);
        _grid.DataSource = entries.Select(e => new
        {
            Fecha = e.Fecha.ToString("dd-MM-yyyy HH:mm:ss"),
            e.Usuario,
            Módulo = e.Modulo,
            Acción = e.Accion,
            e.Detalle
        }).ToList();

        if (_grid.Columns.Count >= 5)
        {
            _grid.Columns[0].FillWeight = 16;
            _grid.Columns[1].FillWeight = 14;
            _grid.Columns[2].FillWeight = 16;
            _grid.Columns[3].FillWeight = 18;
            _grid.Columns[4].FillWeight = 36;
        }
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
