using System.Drawing;
using System.Globalization;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo de consulta completa de una Orden de Trabajo (lectura integral de datos generales,
/// detalle, observaciones, valor neto y visibilidad en OT externa, firmas y estado).
/// </summary>
public sealed class WorkOrderDetailDialog : Form
{
    private static readonly CultureInfo ClCulture = new("es-CL");
    private readonly SisproApplicationContext _ctx;
    private readonly WorkOrder _order;
    private readonly ToolTip _toolTip = new();

    internal Button BtnPreview { get; }
    internal Button BtnClose { get; }
    internal Label LblValorNetoInfo { get; }

    public WorkOrderDetailDialog(SisproApplicationContext ctx, WorkOrder order)
    {
        _ctx = ctx;
        _order = order;

        Text = $"Consulta Completa — OT N° {order.NumeroOt} ({order.TipoOrden})";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(860, 640);
        MinimumSize = new Size(760, 540);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = CambiasoTheme.Primary,
            Padding = new Padding(18, 10, 18, 10)
        };
        header.Controls.Add(new Label
        {
            Text = $"Orden de Trabajo {order.TipoOrden} N° {order.NumeroOt:D6} — Estado: {order.Estado.ToDisplayName()}",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(18, 9)
        });
        header.Controls.Add(new Label
        {
            Text = $"Fecha: {order.FechaOt:dd-MM-yyyy}  ·  Proveedor: {order.NombreProveedor}  ·  Solicitante: {order.SolicitanteDisplayNombre}",
            Font = CambiasoTheme.SmallFont,
            ForeColor = Color.FromArgb(208, 228, 242),
            AutoSize = true,
            Location = new Point(18, 32)
        });

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(14, 10, 14, 8)
        };
        body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));

        // 1. Datos Generales
        GroupBox grpGen = CambiasoTheme.CreateGroupBox("Datos Generales, Proveedor, Solicitante y Activo");
        grpGen.Dock = DockStyle.Top;
        grpGen.AutoSize = true;
        TableLayoutPanel genTbl = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 4,
            Padding = new Padding(8, 6, 8, 14)
        };
        genTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        genTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        genTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        genTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        AddPair(genTbl, 0, "N° OT:", order.NumeroOt.ToString(CultureInfo.InvariantCulture), "Tipo / Estado:", $"OT {order.TipoOrden} · {order.Estado.ToDisplayName()}");
        AddPair(genTbl, 1, "Fecha:", order.FechaOt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture), "Solicitante:", $"{order.SolicitanteDisplayNombre} ({order.CorreoSolicitante})");
        AddPair(genTbl, 2, "Proveedor:", order.NombreProveedor, "Dirección / Mail:", $"{order.DireccionProveedor} · {order.CorreoProveedor}".Trim(' ', '·'));
        AddPair(genTbl, 3, "Ruta Activo:", string.IsNullOrWhiteSpace(order.RutaActivo) ? "(Sin ruta)" : order.RutaActivo, "Teléfono Sol.:", string.IsNullOrWhiteSpace(order.TelefonoSolicitante) ? "-" : order.TelefonoSolicitante);

        string valorNetoText = order.MostrarValorNeto
            ? $"$ {order.ValorNeto.ToString("N0", ClCulture)} (Visible en OT externa)"
            : $"$ {order.ValorNeto.ToString("N0", ClCulture)} (Oculto en OT externa — dato conservado en BD)";
        LblValorNetoInfo = AddPair(genTbl, 4, "Valor Neto:", valorNetoText, "Firmas al Pie:", FormatSignersDetail(order));

        grpGen.Controls.Add(genTbl);

        // 2. Detalle de ítems
        GroupBox grpItems = CambiasoTheme.CreateGroupBox($"Detalle de Trabajos ({order.Detalles.Count} ítem(s))");
        grpItems.Dock = DockStyle.Fill;
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false
        };
        CambiasoTheme.StyleGrid(grid);
        grid.AutoGenerateColumns = false;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Ítem",
            DataPropertyName = "Ítem",
            Width = 65,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "Cantidad",
            DataPropertyName = "Cantidad",
            Width = 90,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        });
        DataGridViewTextBoxColumn descCol = new()
        {
            HeaderText = "Descripción",
            DataPropertyName = "Descripción",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        descCol.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.Columns.Add(descCol);
        grid.DataSource = order.Detalles.Select((d, i) => new
        {
            Ítem = i + 1,
            Cantidad = d.Cantidad,
            Descripción = d.Descripcion
        }).ToList();
        grpItems.Controls.Add(grid);

        // 3. Observaciones
        GroupBox grpObs = CambiasoTheme.CreateGroupBox("Observaciones");
        grpObs.Dock = DockStyle.Fill;
        TextBox txtObs = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = CambiasoTheme.CardBackground,
            Text = string.IsNullOrWhiteSpace(order.Observacion) ? "Sin observaciones registradas." : order.Observacion
        };
        grpObs.Controls.Add(txtObs);

        body.Controls.Add(grpGen, 0, 0);
        body.Controls.Add(grpItems, 0, 1);
        body.Controls.Add(grpObs, 0, 2);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(14, 10, 16, 10),
            BackColor = CambiasoTheme.CardBackground
        };

        BtnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cerrar ventana de consulta", width: 112, height: 34);
        BtnClose.Click += (_, _) => Close();

        BtnPreview = CambiasoTheme.CreateButton("Vista Previa OT", AppIcon.Preview, ButtonTone.Primary, _toolTip, "Abrir vista previa del documento oficial respetando visibilidad del valor neto", width: 158, height: 34);
        BtnPreview.Click += (_, _) => ExportUiHelper.ShowWorkOrderPreview(this, _ctx, _order, _order.FirmasSeleccionadas);

        footer.Controls.Add(BtnClose);
        footer.Controls.Add(BtnPreview);

        CancelButton = BtnClose;
        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
    }

    private static Label AddPair(TableLayoutPanel tbl, int row, string k1, string v1, string k2, string v2)
    {
        tbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Label lk1 = new() { Text = k1, Font = CambiasoTheme.LabelFont, ForeColor = CambiasoTheme.TextMuted, AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        Label lv1 = new() { Text = v1, Font = CambiasoTheme.BodyFont, ForeColor = CambiasoTheme.TextPrimary, AutoSize = true, MaximumSize = new Size(290, 0), Margin = new Padding(0, 3, 12, 3) };
        Label lk2 = new() { Text = k2, Font = CambiasoTheme.LabelFont, ForeColor = CambiasoTheme.TextMuted, AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        Label lv2 = new() { Text = v2, Font = CambiasoTheme.BodyFont, ForeColor = CambiasoTheme.TextPrimary, AutoSize = true, MaximumSize = new Size(280, 0), Margin = new Padding(0, 3, 0, 3) };
        tbl.Controls.Add(lk1, 0, row);
        tbl.Controls.Add(lv1, 1, row);
        tbl.Controls.Add(lk2, 2, row);
        tbl.Controls.Add(lv2, 3, row);
        return lv1;
    }

    private static string FormatSignersDetail(WorkOrder o)
    {
        if (o.FirmasSeleccionadas == SignatureOption.Ninguna)
        {
            return "Sin firmas al pie";
        }

        List<string> parts = [];
        if (o.FirmasSeleccionadas.IncludeJefatura())
            parts.Add(string.IsNullOrWhiteSpace(o.FirmaJefaturaNombre) ? "Jefatura" : $"Jefatura: {o.FirmaJefaturaNombre}");
        if (o.FirmasSeleccionadas.IncludeTecnico())
            parts.Add(string.IsNullOrWhiteSpace(o.FirmaTecnicoNombre) ? "Técnico" : $"Técnico: {o.FirmaTecnicoNombre}");
        if (o.FirmasSeleccionadas.IncludeSupervisor())
            parts.Add(string.IsNullOrWhiteSpace(o.FirmaSupervisorNombre) ? "Supervisor" : $"Supervisor: {o.FirmaSupervisorNombre}");
        return string.Join(" | ", parts);
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
