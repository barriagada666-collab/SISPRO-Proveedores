using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Visor auditable del Historial de Movimientos entre Grupos con Date Pickers,
/// orden cronológico descendente y exportación en Excel, PDF, Word y CSV.
/// </summary>
public sealed class GroupHistoryDialog : Form
{
    private readonly GroupManagementService _groupService;
    private readonly DocumentExportService _exportService;
    private readonly UserSession _session;
    private readonly DateRangeFilterControl _dateFilter;
    private readonly ComboBox _cmbGrupo;
    private readonly TextBox _txtBuscar;
    private readonly DataGridView _grid;
    private readonly Label _lblCount;
    private IReadOnlyList<GroupMovementEntry> _rows = [];

    public GroupHistoryDialog(
        GroupManagementService groupService,
        DocumentExportService exportService,
        UserSession session,
        int? initialGroupId = null)
    {
        _groupService = groupService;
        _exportService = exportService;
        _session = session;

        Text = "Historial de Movimientos entre Grupos — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(1060, 620);
        MinimumSize = new Size(880, 480);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel top = new()
        {
            Dock = DockStyle.Top,
            Height = 84,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(10, 6, 10, 6)
        };

        TableLayoutPanel filtersLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        filtersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        filtersLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        FlowLayoutPanel row1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        _txtBuscar = new TextBox { Width = 200, PlaceholderText = "Usuario, grupo, motivo..." };
        _txtBuscar.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadHistory(); } };

        _cmbGrupo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
        List<GroupFilterOption> groupOptions = [new GroupFilterOption(null, "Todos los grupos")];
        foreach (UserGroup g in _groupService.GetAll(_session, includeInactive: true))
        {
            groupOptions.Add(new GroupFilterOption(g.IdGrupo, g.Nombre));
        }
        foreach (GroupFilterOption opt in groupOptions)
        {
            _cmbGrupo.Items.Add(opt);
        }
        int initIdx = 0;
        if (initialGroupId.HasValue)
        {
            int idx = groupOptions.FindIndex(o => o.IdGrupo == initialGroupId.Value);
            if (idx >= 0) initIdx = idx;
        }
        _cmbGrupo.SelectedIndex = initIdx;
        _cmbGrupo.SelectedIndexChanged += (_, _) => LoadHistory();

        Button btnBuscar = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, width: 92, height: 28);
        btnBuscar.Click += (_, _) => LoadHistory();

        Button btnLimpiar = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, width: 92, height: 28);
        btnLimpiar.Click += (_, _) =>
        {
            _txtBuscar.Clear();
            _cmbGrupo.SelectedIndex = 0;
            _dateFilter!.Clear();
            LoadHistory();
        };

        Button btnExport = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, width: 106, height: 28);
        btnExport.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            btnExport,
            this,
            _exportService,
            "SISPRO_Historial_Movimientos_Grupos",
            () => DocumentExportService.BuildGroupMovementsReport(_rows, BuildFilterSummary()));

        row1.Controls.AddRange([LabelInline("Buscar:"), _txtBuscar, LabelInline("Grupo:"), _cmbGrupo, btnBuscar, btnLimpiar, btnExport]);

        FlowLayoutPanel row2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        _dateFilter = new DateRangeFilterControl(compact: true);
        _dateFilter.RangeChanged += (_, _) => LoadHistory();
        _lblCount = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(12, 8, 0, 0)
        };
        row2.Controls.AddRange([_dateFilter, _lblCount]);

        filtersLayout.Controls.Add(row1, 0, 0);
        filtersLayout.Controls.Add(row2, 0, 1);
        top.Controls.Add(filtersLayout);

        _grid = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_grid);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(10, 8, 10, 8)
        };
        Button btnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Clear, ButtonTone.Secondary, width: 104, height: 30);
        btnClose.Click += (_, _) => Close();
        footer.Controls.Add(btnClose);

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(footer);

        LoadHistory();
    }

    private void LoadHistory()
    {
        if (!_dateFilter.TryValidate(this, out _))
        {
            return;
        }

        try
        {
            GroupFilterOption? opt = _cmbGrupo.SelectedItem as GroupFilterOption;
            _rows = _groupService.GetMovementHistory(_session, new GroupMovementFilter
            {
                FechaInicio = _dateFilter.FechaInicio,
                FechaFin = _dateFilter.FechaFin,
                IdGrupo = opt?.IdGrupo,
                TextoBusqueda = _txtBuscar.Text,
                MaxRows = 1000
            });

            _grid.DataSource = _rows.Select(m => new
            {
                Fecha = m.FechaMovimiento.ToString("dd-MM-yyyy HH:mm"),
                Usuario = m.AliasUsuario,
                Origen = m.NombreGrupoOrigen,
                Destino = m.NombreGrupoDestino,
                EjecutadoPor = m.EjecutadoPor,
                Motivo = m.Motivo
            }).ToList();

            if (_grid.Columns.Count >= 6)
            {
                _grid.Columns[0].HeaderText = "Fecha y Hora";
                _grid.Columns[0].FillWeight = 14;
                _grid.Columns[1].FillWeight = 13;
                _grid.Columns[2].HeaderText = "Grupo Origen";
                _grid.Columns[2].FillWeight = 15;
                _grid.Columns[3].HeaderText = "Grupo Destino";
                _grid.Columns[3].FillWeight = 15;
                _grid.Columns[4].HeaderText = "Ejecutado Por";
                _grid.Columns[4].FillWeight = 13;
                _grid.Columns[5].FillWeight = 30;
            }

            _lblCount.Text = $"Registros mostrados: {_rows.Count:N0} (orden más reciente primero)";
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private string BuildFilterSummary()
    {
        string grp = (_cmbGrupo.SelectedItem as GroupFilterOption)?.Label ?? "Todos";
        return $"{_dateFilter.BuildFilterDescription()} | Grupo: {grp}";
    }

    private static Label LabelInline(string text) => new()
    {
        Text = text,
        Font = CambiasoTheme.LabelFont,
        ForeColor = CambiasoTheme.TextMuted,
        AutoSize = true,
        Margin = new Padding(6, 7, 4, 0)
    };

    private sealed record GroupFilterOption(int? IdGrupo, string Label)
    {
        public override string ToString() => Label;
    }
}
