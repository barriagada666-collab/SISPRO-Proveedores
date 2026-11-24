using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Bitácora auditable de Seguridad y Operaciones con Date Pickers (dd-MM-yyyy),
/// filtro por módulo/usuario/texto, orden cronológico descendente y exportación multiformato.
/// </summary>
public sealed class AuditLogDialog : Form
{
    private readonly IAuditRepository _auditRepo;
    private readonly DocumentExportService? _exportService;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtSearch;
    private readonly ComboBox _cmbModulo;
    private readonly DateRangeFilterControl _dateFilter;
    private readonly Label _lblCount;
    private readonly DataGridView _grid;
    private readonly GridPagerBar _pager;
    private IReadOnlyList<AuditEntry> _entries = [];

    public AuditLogDialog(IAuditRepository auditRepo, DocumentExportService? exportService = null)
    {
        _auditRepo = auditRepo;
        _exportService = exportService;

        Text = "Bitácora de Auditoría — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(1080, 620);
        MinimumSize = new Size(880, 480);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 50, BackColor = CambiasoTheme.PrimaryDark };
        header.Controls.Add(new Label
        {
            Text = "Registro de Auditoría, Seguridad y Operaciones (orden cronológico descendente)",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 14)
        });

        Panel filterPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(12, 6, 12, 6)
        };

        TableLayoutPanel fLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        fLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        fLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        FlowLayoutPanel r1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        _txtSearch = new TextBox { Width = 210, PlaceholderText = "Usuario, acción o detalle..." };
        _txtSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadEntries(); } };

        _cmbModulo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
        _cmbModulo.Items.AddRange(["Todos", "Usuarios", "Grupos", "Seguridad", "Órdenes de Trabajo", "Mantenedores", "Árbol de Activos", "Configuración"]);
        _cmbModulo.SelectedIndex = 0;
        _cmbModulo.SelectedIndexChanged += (_, _) => LoadEntries();

        Button btnSearch = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Filtrar bitácora", width: 92, height: 28);
        btnSearch.Click += (_, _) => LoadEntries();

        Button btnClear = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Restablecer filtros", width: 94, height: 28);
        btnClear.Click += (_, _) =>
        {
            _txtSearch.Clear();
            _cmbModulo.SelectedIndex = 0;
            _dateFilter!.Clear();
            LoadEntries();
        };

        r1.Controls.AddRange([InlineLabel("Buscar:"), _txtSearch, InlineLabel("Módulo:"), _cmbModulo, btnSearch, btnClear]);

        if (_exportService is not null)
        {
            Button btnExport = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar bitácora filtrada a Excel, PDF, Word o CSV", width: 108, height: 28);
            btnExport.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
                btnExport,
                this,
                _exportService,
                "SISPRO_Auditoria",
                () => DocumentExportService.BuildAuditReport(_entries, BuildFilterSummary()));
            r1.Controls.Add(btnExport);
        }

        FlowLayoutPanel r2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        _dateFilter = new DateRangeFilterControl(compact: true);
        _dateFilter.RangeChanged += (_, _) => LoadEntries();
        _lblCount = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(12, 7, 0, 0)
        };
        r2.Controls.AddRange([_dateFilter, _lblCount]);

        fLayout.Controls.Add(r1, 0, 0);
        fLayout.Controls.Add(r2, 0, 1);
        filterPanel.Controls.Add(fLayout);

        _grid = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_grid);
        ContextMenuHelper.AttachToGrid(_grid, idx =>
        {
            AuditEntry? entry = GridBindingHelper.GetRowSource<AuditEntry>(_grid, idx);
            if (entry is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Copiar detalle", AppIcon.Copy, CambiasoTheme.Primary, () => Clipboard.SetText(entry.Detalle), enabled: !string.IsNullOrWhiteSpace(entry.Detalle)),
                ContextMenuHelper.Item("Filtrar por usuario", AppIcon.Search, CambiasoTheme.Accent, () =>
                {
                    _txtSearch.Text = entry.Usuario;
                    LoadEntries();
                }, enabled: !string.IsNullOrWhiteSpace(entry.Usuario)),
                ContextMenuHelper.Item("Filtrar por módulo", AppIcon.Layers, CambiasoTheme.Primary, () =>
                {
                    if (_cmbModulo.Items.Contains(entry.Modulo))
                    {
                        _cmbModulo.SelectedItem = entry.Modulo;
                    }
                }, enabled: !string.IsNullOrWhiteSpace(entry.Modulo))
            ];
        });

        _pager = new GridPagerBar(100);
        _pager.PageChanged += (_, _) => RenderEntriesPage();

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 8, 16, 8),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Exit, ButtonTone.Secondary, _toolTip, "Cerrar bitácora", width: 110, height: 32);
        btnClose.Click += (_, _) => Close();

        Button btnRefresh = CambiasoTheme.CreateButton("Actualizar", AppIcon.Refresh, ButtonTone.Primary, _toolTip, "Recargar eventos de auditoría", width: 120, height: 32);
        btnRefresh.Click += (_, _) => LoadEntries();

        footer.Controls.AddRange([btnClose, btnRefresh]);

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 8) };
        body.Controls.Add(_grid);
        body.Controls.Add(_pager);

        Controls.Add(body);
        Controls.Add(filterPanel);
        Controls.Add(footer);
        Controls.Add(header);

        LoadEntries();
    }

    private void LoadEntries()
    {
        if (!_dateFilter.TryValidate(this, out _))
        {
            return;
        }

        _entries = _auditRepo.Search(new AuditSearchFilter
        {
            FechaInicio = _dateFilter.FechaInicio,
            FechaFin = _dateFilter.FechaFin,
            Modulo = _cmbModulo.SelectedIndex > 0 ? _cmbModulo.SelectedItem?.ToString() : null,
            TextoBusqueda = _txtSearch.Text,
            MaxRows = 2000
        });

        _pager.ResetPageIndex();
        RenderEntriesPage();
        _lblCount.Text = $"Eventos mostrados: {_entries.Count:N0} (más recientes primero)";
    }

    private void RenderEntriesPage()
    {
        IReadOnlyList<AuditEntry> slice = GridBindingHelper.SlicePage(_entries, _pager.PageIndex, _pager.PageSize, out int safePage);
        _grid.DataSource = new SortableBindingList<AuditGridRow>(slice.Select(e => new AuditGridRow(e)));
        _pager.SetState(safePage, _pager.PageSize, _entries.Count);

        if (_grid.Columns.Count >= 5)
        {
            GridBindingHelper.ConfigureColumn(_grid, 0, "Fecha", 16, "dd-MM-yyyy HH:mm:ss");
            GridBindingHelper.ConfigureColumn(_grid, 1, "Usuario", 13);
            GridBindingHelper.ConfigureColumn(_grid, 2, "Módulo", 14);
            GridBindingHelper.ConfigureColumn(_grid, 3, "Acción", 18);
            GridBindingHelper.ConfigureColumn(_grid, 4, "Detalle", 39);
        }
    }

    private string BuildFilterSummary()
    {
        string q = string.IsNullOrWhiteSpace(_txtSearch.Text) ? "(Sin texto)" : $"\"{_txtSearch.Text.Trim()}\"";
        return $"Búsqueda: {q} | Módulo: {_cmbModulo.SelectedItem} | {_dateFilter.BuildFilterDescription()}";
    }

    private static Label InlineLabel(string text) => new()
    {
        Text = text,
        Font = CambiasoTheme.LabelFont,
        ForeColor = CambiasoTheme.TextMuted,
        AutoSize = true,
        Margin = new Padding(4, 7, 4, 0)
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class AuditGridRow(AuditEntry source) : IGridRowSource<AuditEntry>
    {
        [Browsable(false)]
        public AuditEntry Source { get; } = source;
        public DateTime Fecha => Source.Fecha;
        public string Usuario => Source.Usuario;
        public string Modulo => Source.Modulo;
        public string Accion => Source.Accion;
        public string Detalle => Source.Detalle;
    }
}
