using System.Drawing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo integrado MANTENCIÓN INDUSTRIAL — Acceso completo a los datos históricos
/// importados desde BD MANTENCION.mdb y PERSONAL_MANTENCION.mdb (OTs internas,
/// Planes Preventivos MP, Insumos SAP, Personal Técnico / RRHH y Catálogos).
/// </summary>
public sealed class MaintenanceHubForm : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    // Tab 1: OTs Mantención Interna
    private readonly TextBox _txtOtSearch;
    private readonly ComboBox _cmbOtTrade;
    private readonly ComboBox _cmbOtStatus;
    private readonly ComboBox _cmbOtYear;
    private readonly DataGridView _gridMantOts;
    private readonly TextBox _txtOtDetail;
    private readonly GroupBox _grpMantOts;
    private IReadOnlyList<MantWorkOrderRow> _mantOts = [];

    // Tab 2: Planes Preventivos MP
    private readonly TextBox _txtMpSearch;
    private readonly DataGridView _gridMpPlans;
    private readonly DataGridView _gridMpCycles;
    private readonly DataGridView _gridMpTasks;
    private IReadOnlyList<MantPreventivePlanRow> _mpPlans = [];
    private IReadOnlyList<MantPreventiveCycleRow> _mpCycles = [];
    private int _selectedMpId;

    // Tab 3: Insumos SAP
    private readonly TextBox _txtSupplySearch;
    private readonly DataGridView _gridSupplies;
    private readonly GroupBox _grpSupplies;
    private IReadOnlyList<MantSupplyRow> _supplies = [];

    // Tab 4: Personal Técnico & RRHH
    private readonly TextBox _txtPersSearch;
    private readonly DataGridView _gridPersonnel;
    private readonly DataGridView _gridHrPersonnel;

    // Tab 5: Catálogos & OTs Externas Históricas
    private readonly DataGridView _gridLegacyExt;
    private readonly DataGridView _gridMotives;
    private readonly DataGridView _gridTrades;
    private readonly DataGridView _gridChecklists;

    private bool _mpLoaded;
    private bool _suppliesLoaded;
    private bool _personnelLoaded;
    private bool _catalogsLoaded;

    public MaintenanceHubForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        Name = "MantencionIndustrialForm";
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.LabelFont,
            Padding = new Point(12, 6)
        };

        // --- TAB 1: OTs MANTENCIÓN ---
        TabPage tabOts = new("OTs Mantención Interna") { BackColor = CambiasoTheme.Surface, Padding = new Padding(8) };
        _txtOtSearch = new TextBox { Width = 152, PlaceholderText = "N° OT, máquina o falla..." };
        _txtOtSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadMantOts(); } };

        _cmbOtTrade = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 104 };
        _cmbOtStatus = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 102 };
        _cmbOtYear = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 76 };

        _gridMantOts = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridMantOts);
        _gridMantOts.SelectionChanged += (_, _) => LoadSelectedOtDetail();

        _txtOtDetail = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = CambiasoTheme.Surface,
            Font = CambiasoTheme.BodyFont
        };
        _grpMantOts = CambiasoTheme.CreateGroupBox("Órdenes de Trabajo de Mantención Interna");
        _grpMantOts.Dock = DockStyle.Fill;
        BuildOtsTab(tabOts);

        // --- TAB 2: PLANES PREVENTIVOS MP ---
        TabPage tabMp = new("Planes Preventivos (MP)") { BackColor = CambiasoTheme.Surface, Padding = new Padding(8) };
        _txtMpSearch = new TextBox { Width = 260, PlaceholderText = "Buscar plan MP por descripción, planta o equipo..." };
        _txtMpSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadPreventivePlans(); } };

        _gridMpPlans = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridMpPlans);
        _gridMpPlans.SelectionChanged += (_, _) => LoadSelectedMpDetails();

        _gridMpCycles = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridMpCycles);
        _gridMpCycles.SelectionChanged += (_, _) => LoadSelectedCycleTasks();

        _gridMpTasks = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridMpTasks);
        BuildMpTab(tabMp);

        // --- TAB 3: INSUMOS SAP ---
        TabPage tabSupplies = new("Insumos y Repuestos SAP") { BackColor = CambiasoTheme.Surface, Padding = new Padding(8) };
        _txtSupplySearch = new TextBox { Width = 280, PlaceholderText = "Buscar por código SAP, proveedor o descripción..." };
        _txtSupplySearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadSupplies(); } };
        _gridSupplies = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridSupplies);
        _grpSupplies = CambiasoTheme.CreateGroupBox("Catálogo de Insumos y Repuestos SAP");
        _grpSupplies.Dock = DockStyle.Fill;
        BuildSuppliesTab(tabSupplies);

        // --- TAB 4: PERSONAL TÉCNICO & RRHH ---
        TabPage tabPers = new("Personal Técnico y RRHH") { BackColor = CambiasoTheme.Surface, Padding = new Padding(8) };
        _txtPersSearch = new TextBox { Width = 260, PlaceholderText = "Buscar técnico por nombre, RUT, oficio o depto..." };
        _txtPersSearch.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; LoadPersonnel(); } };
        _gridPersonnel = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridPersonnel);
        _gridHrPersonnel = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridHrPersonnel);
        BuildPersonnelTab(tabPers);

        // --- TAB 5: CATÁLOGOS Y CHECKLISTS ---
        TabPage tabCats = new("Catálogos y OTs Externas") { BackColor = CambiasoTheme.Surface, Padding = new Padding(8) };
        _gridLegacyExt = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridLegacyExt);
        _gridMotives = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridMotives);
        _gridTrades = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridTrades);
        _gridChecklists = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridChecklists);
        BuildCatalogsTab(tabCats);

        tabs.TabPages.AddRange([tabOts, tabMp, tabSupplies, tabPers, tabCats]);
        tabs.SelectedIndexChanged += (_, _) =>
        {
            if (tabs.SelectedTab == tabMp && !_mpLoaded) LoadPreventivePlans();
            else if (tabs.SelectedTab == tabSupplies && !_suppliesLoaded) LoadSupplies();
            else if (tabs.SelectedTab == tabPers && !_personnelLoaded) LoadPersonnel();
            else if (tabs.SelectedTab == tabCats && !_catalogsLoaded) LoadCatalogs();
        };

        Controls.Add(tabs);
        LoadFilterLookups();
        LoadMantOts();
    }

    private void BuildOtsTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 4) };
        Button btnSearch = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Consultar OTs de mantención con filtros", width: 92, height: 30);
        btnSearch.Click += (_, _) => LoadMantOts();

        Button btnClear = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Restablecer filtros de búsqueda", width: 98, height: 30);
        btnClear.Click += (_, _) =>
        {
            _txtOtSearch.Clear();
            if (_cmbOtTrade.Items.Count > 0) _cmbOtTrade.SelectedIndex = 0;
            if (_cmbOtStatus.Items.Count > 0) _cmbOtStatus.SelectedIndex = 0;
            if (_cmbOtYear.Items.Count > 0) _cmbOtYear.SelectedIndex = 0;
            LoadMantOts();
        };

        Button btnExcel = CambiasoTheme.CreateButton("Excel", AppIcon.Excel, ButtonTone.Accent, _toolTip, "Exportar resultados visibles a Excel (.xlsx)", width: 82, height: 30);
        btnExcel.Click += (_, _) => ExportMantOtsExcel();

        bar.Controls.AddRange(
        [
            Wrap("Buscar:", _txtOtSearch),
            Wrap("Oficio:", _cmbOtTrade),
            Wrap("Estado:", _cmbOtStatus),
            Wrap("Año:", _cmbOtYear),
            btnSearch, btnClear, btnExcel
        ]);

        _grpMantOts.Controls.Add(_gridMantOts);

        GroupBox grpDetail = CambiasoTheme.CreateGroupBox("Detalle Técnico, Técnicos Asignados e Insumos Consumidos");
        grpDetail.Dock = DockStyle.Fill;
        grpDetail.Controls.Add(_txtOtDetail);

        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(_grpMantOts, 0, 1);
        layout.Controls.Add(grpDetail, 0, 2);
        page.Controls.Add(layout);
    }

    private void BuildMpTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 4) };
        Button btnSearch = CambiasoTheme.CreateButton("Buscar Planes", AppIcon.Search, ButtonTone.Primary, _toolTip, "Filtrar planes de mantención preventiva", width: 132, height: 30);
        btnSearch.Click += (_, _) => LoadPreventivePlans();
        Button btnRefresh = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar planes MP", width: 106, height: 30);
        btnRefresh.Click += (_, _) => { _txtMpSearch.Clear(); LoadPreventivePlans(); };
        bar.Controls.AddRange([Wrap("Plan / Equipo:", _txtMpSearch), btnSearch, btnRefresh]);

        GroupBox grpPlans = CambiasoTheme.CreateGroupBox("Planes de Mantención Preventiva (MP — BD Mantención)");
        grpPlans.Dock = DockStyle.Fill;
        grpPlans.Controls.Add(_gridMpPlans);

        TableLayoutPanel lower = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        GroupBox grpCycles = CambiasoTheme.CreateGroupBox("Ciclos Ejecutados del Plan");
        grpCycles.Dock = DockStyle.Fill;
        grpCycles.Controls.Add(_gridMpCycles);

        GroupBox grpTasks = CambiasoTheme.CreateGroupBox("Tareas del Plan / Ciclo Seleccionado");
        grpTasks.Dock = DockStyle.Fill;
        grpTasks.Controls.Add(_gridMpTasks);

        lower.Controls.Add(grpCycles, 0, 0);
        lower.Controls.Add(grpTasks, 1, 0);

        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(grpPlans, 0, 1);
        layout.Controls.Add(lower, 0, 2);
        page.Controls.Add(layout);
    }

    private void BuildSuppliesTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 4) };
        Button btnSearch = CambiasoTheme.CreateButton("Buscar Insumo", AppIcon.Search, ButtonTone.Primary, _toolTip, "Buscar repuestos o insumos SAP", width: 136, height: 30);
        btnSearch.Click += (_, _) => LoadSupplies();
        Button btnRefresh = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar catálogo de insumos", width: 106, height: 30);
        btnRefresh.Click += (_, _) => { _txtSupplySearch.Clear(); LoadSupplies(); };
        Button btnExcel = CambiasoTheme.CreateButton("Exportar Excel", AppIcon.Excel, ButtonTone.Accent, _toolTip, "Exportar insumos SAP a Excel (.xlsx)", width: 138, height: 30);
        btnExcel.Click += (_, _) => ExportSuppliesExcel();

        bar.Controls.AddRange([Wrap("Buscar:", _txtSupplySearch), btnSearch, btnRefresh, btnExcel]);
        _grpSupplies.Controls.Add(_gridSupplies);

        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(_grpSupplies, 0, 1);
        page.Controls.Add(layout);
    }

    private void BuildPersonnelTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 4) };
        Button btnSearch = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Filtrar técnicos y fichas RRHH", width: 92, height: 30);
        btnSearch.Click += (_, _) => LoadPersonnel();
        Button btnRefresh = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar todo el personal", width: 106, height: 30);
        btnRefresh.Click += (_, _) => { _txtPersSearch.Clear(); LoadPersonnel(); };
        bar.Controls.AddRange([Wrap("Buscar:", _txtPersSearch), btnSearch, btnRefresh]);

        GroupBox grpTech = CambiasoTheme.CreateGroupBox("Personal Técnico de Mantención (BD MANTENCION.mdb)");
        grpTech.Dock = DockStyle.Fill;
        grpTech.Controls.Add(_gridPersonnel);

        GroupBox grpHr = CambiasoTheme.CreateGroupBox("Dotación RRHH Mantención (PERSONAL_MANTENCION.mdb)");
        grpHr.Dock = DockStyle.Fill;
        grpHr.Controls.Add(_gridHrPersonnel);

        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(grpTech, 0, 1);
        layout.Controls.Add(grpHr, 0, 2);
        page.Controls.Add(layout);
    }

    private void BuildCatalogsTab(TabPage page)
    {
        TableLayoutPanel grid = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 48));

        GroupBox g1 = CambiasoTheme.CreateGroupBox("OTs Externas Históricas (BD Mantención)");
        g1.Dock = DockStyle.Fill;
        g1.Controls.Add(_gridLegacyExt);

        GroupBox g2 = CambiasoTheme.CreateGroupBox("Motivos de Falla (41)");
        g2.Dock = DockStyle.Fill;
        g2.Controls.Add(_gridMotives);

        GroupBox g3 = CambiasoTheme.CreateGroupBox("Tareas de Checklist de Mantención (48)");
        g3.Dock = DockStyle.Fill;
        g3.Controls.Add(_gridChecklists);

        GroupBox g4 = CambiasoTheme.CreateGroupBox("Oficios y Especialidades (33)");
        g4.Dock = DockStyle.Fill;
        g4.Controls.Add(_gridTrades);

        grid.Controls.Add(g1, 0, 0);
        grid.Controls.Add(g2, 1, 0);
        grid.Controls.Add(g3, 0, 1);
        grid.Controls.Add(g4, 1, 1);
        page.Controls.Add(grid);
    }

    private void LoadFilterLookups()
    {
        _cmbOtTrade.Items.Clear();
        _cmbOtTrade.Items.Add("Todos");
        foreach (string t in _ctx.MaintenanceService.GetTradeNames()) _cmbOtTrade.Items.Add(t);
        _cmbOtTrade.SelectedIndex = 0;

        _cmbOtStatus.Items.Clear();
        _cmbOtStatus.Items.Add("Todos");
        foreach (string s in _ctx.MaintenanceService.GetWorkOrderStatuses()) _cmbOtStatus.Items.Add(s);
        _cmbOtStatus.SelectedIndex = 0;

        _cmbOtYear.Items.Clear();
        _cmbOtYear.Items.Add("Todos");
        foreach (int y in _ctx.MaintenanceService.GetWorkOrderYears()) _cmbOtYear.Items.Add(y);
        _cmbOtYear.SelectedIndex = 0;
    }

    private void LoadMantOts()
    {
        MantWorkOrderFilter filter = new()
        {
            TextoBusqueda = _txtOtSearch.Text,
            Oficio = _cmbOtTrade.SelectedIndex > 0 ? _cmbOtTrade.SelectedItem?.ToString() : null,
            Estado = _cmbOtStatus.SelectedIndex > 0 ? _cmbOtStatus.SelectedItem?.ToString() : null,
            Anio = _cmbOtYear.SelectedItem is int yr ? yr : null,
            MaxRows = 400
        };

        _mantOts = _ctx.MaintenanceService.SearchInternalWorkOrders(_session, filter);
        _grpMantOts.Text = $"Órdenes de Trabajo de Mantención Interna (mostrando {_mantOts.Count:N0} registros — tope 400 por consulta)";
        _gridMantOts.DataSource = _mantOts.Select(o => new
        {
            N_OT = o.NumeroOt,
            Inicio = o.FechaInicio.Length >= 16 ? o.FechaInicio[..16] : o.FechaInicio,
            Planta = o.Planta,
            Equipo = o.Equipo,
            Oficio = o.Oficio,
            Estado = o.Estado,
            Detención = FormatDetencion(o.Detencion),
            Descripción = o.DescripcionBreve
        }).ToList();

        if (_gridMantOts.Columns.Count >= 8)
        {
            _gridMantOts.Columns[0].HeaderText = "N° OT";
            _gridMantOts.Columns[0].FillWeight = 8;
            _gridMantOts.Columns[1].FillWeight = 14;
            _gridMantOts.Columns[2].FillWeight = 10;
            _gridMantOts.Columns[3].FillWeight = 14;
            _gridMantOts.Columns[4].FillWeight = 8;
            _gridMantOts.Columns[5].FillWeight = 10;
            _gridMantOts.Columns[6].HeaderText = "Det.";
            _gridMantOts.Columns[6].FillWeight = 6;
            _gridMantOts.Columns[7].FillWeight = 30;
        }
    }

    private void LoadSelectedOtDetail()
    {
        if (_gridMantOts.CurrentRow is null)
        {
            _txtOtDetail.Clear();
            return;
        }

        int idx = _gridMantOts.CurrentRow.Index;
        if (idx < 0 || idx >= _mantOts.Count) return;

        MantWorkOrderDetail? detail = _ctx.MaintenanceService.GetInternalWorkOrderDetail(_session, _mantOts[idx].Id);
        if (detail is null) return;

        MantWorkOrderRow o = detail.Order;
        string techs = detail.TecnicosAsignados.Count > 0 ? string.Join(", ", detail.TecnicosAsignados) : "Sin técnicos registrados";
        string parts = detail.InsumosConsumidos.Count > 0 ? string.Join(" | ", detail.InsumosConsumidos) : "Sin insumos registrados";

        _txtOtDetail.Text =
            $"OT #{o.NumeroOt}  |  Planta: {o.Planta}  |  Equipo: {o.Equipo}  |  Oficio: {o.Oficio}  |  Turno: {o.Turno}  |  Estado: {o.Estado}\r\n" +
            $"Motivo: {o.Motivo}  |  Solicitada por: {o.SolicitadaPor}  |  Inicio: {o.FechaInicio}  |  Término: {o.FechaFin}\r\n" +
            $"Descripción Breve: {o.DescripcionBreve}\r\n" +
            $"Informe Técnico: {(string.IsNullOrWhiteSpace(o.DescripcionTecnica) ? "(Sin informe técnico)" : o.DescripcionTecnica)}\r\n" +
            $"Técnicos Asignados: {techs}\r\n" +
            $"Insumos Consumidos: {parts}";
    }

    private void LoadPreventivePlans()
    {
        _mpLoaded = true;
        _mpPlans = _ctx.MaintenanceService.SearchPreventivePlans(_session, _txtMpSearch.Text);
        _gridMpPlans.DataSource = _mpPlans.Select(p => new
        {
            p.Id,
            p.Planta,
            p.Equipo,
            Descripción = p.Descripcion,
            Tipo = p.TipoMantenimiento,
            Frecuencia = p.Frecuencia,
            Estado = p.Estado,
            Ciclos = p.TotalCiclos,
            Tareas = p.TotalTareas
        }).ToList();
    }

    private void LoadSelectedMpDetails()
    {
        if (_gridMpPlans.CurrentRow is null) return;
        int idx = _gridMpPlans.CurrentRow.Index;
        if (idx < 0 || idx >= _mpPlans.Count) return;

        _selectedMpId = _mpPlans[idx].Id;
        _mpCycles = _ctx.MaintenanceService.GetPreventiveCycles(_session, _selectedMpId);
        _gridMpCycles.DataSource = _mpCycles.Select(c => new
        {
            Ciclo = c.IdCiclo,
            Inicio = c.FechaInicio,
            Cierre = c.FechaCierre,
            Tiempo = c.Tiempo.ToString("0.##", CultureInfo.InvariantCulture),
            Estado = c.Estado
        }).ToList();

        LoadTasksFor(_selectedMpId, _mpCycles.Count > 0 ? _mpCycles[0].IdCiclo : null);
    }

    private void LoadSelectedCycleTasks()
    {
        if (_selectedMpId <= 0 || _gridMpCycles.CurrentRow is null) return;
        int idx = _gridMpCycles.CurrentRow.Index;
        if (idx < 0 || idx >= _mpCycles.Count) return;
        LoadTasksFor(_selectedMpId, _mpCycles[idx].IdCiclo);
    }

    private void LoadTasksFor(int mpId, int? cycleId)
    {
        IReadOnlyList<MantPreventiveTaskRow> tasks = _ctx.MaintenanceService.GetPreventiveTasks(_session, mpId, cycleId);
        _gridMpTasks.DataSource = tasks.Select(t => new
        {
            Ciclo = t.IdCiclo,
            Tarea = t.IdTarea,
            Descripción = t.Descripcion,
            Oficio = t.Oficio,
            Estado = t.Estado,
            Duración = t.Duracion.ToString("0.##", CultureInfo.InvariantCulture)
        }).ToList();
    }

    private void LoadSupplies()
    {
        _suppliesLoaded = true;
        _supplies = _ctx.MaintenanceService.SearchSupplies(_session, _txtSupplySearch.Text, maxRows: 800);
        _grpSupplies.Text = $"Catálogo de Insumos y Repuestos SAP ({_supplies.Count:N0} registros mostrados)";
        _gridSupplies.DataSource = _supplies.Select(s => new
        {
            ID = s.IdItem,
            SAP = s.CodSap,
            CodProveedor = s.CodProveedor,
            Descripción = s.Descripcion,
            Stock = s.StockUnidad.ToString("N1", CultureInfo.InvariantCulture),
            DíasEntrega = s.DiasEntrega,
            UsosHistóricos = s.UsosHistoricos
        }).ToList();
    }

    private void LoadPersonnel()
    {
        _personnelLoaded = true;
        IReadOnlyList<MantPersonnelRow> tech = _ctx.MaintenanceService.SearchPersonnel(_session, _txtPersSearch.Text);
        _gridPersonnel.DataSource = tech.Select(p => new
        {
            ID = p.IdPersonal,
            RUT = p.Rut,
            Nombre = p.NombreCompleto,
            Oficio = p.Oficio,
            Correo = p.Email,
            Teléfono = p.Celular,
            Departamento = p.Departamento,
            OTsAtendidas = p.OrdenesAtendidas
        }).ToList();

        IReadOnlyList<MantHrPersonnelRow> hr = _ctx.MaintenanceService.GetHrPersonnel(_session, _txtPersSearch.Text);
        _gridHrPersonnel.DataSource = hr.Select(h => new
        {
            ID = h.IdEmpleado,
            Nombre = h.NombreCompleto,
            FechaAlta = h.FechaAlta,
            Puesto = h.Puesto,
            Departamento = h.Departamento,
            CentroTrabajo = h.CentroTrabajo
        }).ToList();
    }

    private void LoadCatalogs()
    {
        _catalogsLoaded = true;
        _gridLegacyExt.DataSource = _ctx.MaintenanceService.GetLegacyExternalOrders(_session).Select(e => new
        {
            N_OTE = e.NumeroOte,
            Pedido = e.FechaPedido,
            Empresa = e.Empresa,
            Descripción = e.Descripcion,
            Costo = e.Costo.ToString("N0", CultureInfo.InvariantCulture),
            Garantía = e.Garantia
        }).ToList();

        _gridMotives.DataSource = _ctx.MaintenanceService.GetFailureMotives(_session).Select(m => new { m.Id, Motivo = m.Nombre, Estado = m.Detalle }).ToList();
        _gridTrades.DataSource = _ctx.MaintenanceService.GetTradesCatalog(_session).Select(t => new { t.Id, Oficio = t.Nombre }).ToList();
        _gridChecklists.DataSource = _ctx.MaintenanceService.GetChecklistsSummary(_session).Select(c => new { c.Id, Tarea = c.Nombre, Grupo = c.Detalle }).ToList();
    }

    private void ExportMantOtsExcel()
    {
        using SaveFileDialog dlg = new()
        {
            Title = "Exportar OTs de Mantención a Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"OTs_Mantencion_SISPRO_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _ctx.ExcelExportService.ExportMaintenanceOrders(_mantOts, dlg.FileName);
            CambiasoTheme.ShowInfo(this, $"OTs de mantención exportadas a:\n{dlg.FileName}");
        }
    }

    private void ExportSuppliesExcel()
    {
        using SaveFileDialog dlg = new()
        {
            Title = "Exportar Insumos SAP a Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"Insumos_SAP_SISPRO_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _ctx.ExcelExportService.ExportSupplies(_supplies, dlg.FileName);
            CambiasoTheme.ShowInfo(this, $"Insumos SAP exportados a:\n{dlg.FileName}");
        }
    }

    private static string FormatDetencion(string raw) =>
        raw.Trim().ToUpperInvariant() switch
        {
            "VERDADERO" or "TRUE" or "1" or "SI" or "SÍ" => "Sí",
            "FALSO" or "FALSE" or "0" or "NO" => "No",
            _ => raw
        };

    private static FlowLayoutPanel Wrap(string label, Control ctl)
    {
        FlowLayoutPanel p = new() { AutoSize = true, Margin = new Padding(2, 2, 8, 2), WrapContents = false };
        Label lbl = CambiasoTheme.CreateFieldLabel(label);
        lbl.Margin = new Padding(0, 5, 5, 0);
        p.Controls.Add(lbl);
        p.Controls.Add(ctl);
        return p;
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
