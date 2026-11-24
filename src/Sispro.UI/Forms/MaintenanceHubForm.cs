using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Controls;
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
    private readonly DateRangeFilterControl _otDateRange;
    private readonly DataGridView _gridMantOts;
    private readonly GridPagerBar _otPager;
    private readonly TextBox _txtOtDetail;
    private readonly GroupBox _grpMantOts;

    // Tab 2: Planes Preventivos MP
    private readonly TextBox _txtMpSearch;
    private readonly DataGridView _gridMpPlans;
    private readonly DataGridView _gridMpCycles;
    private readonly DataGridView _gridMpTasks;
    private int _selectedMpId;

    // Tab 3: Insumos SAP
    private readonly TextBox _txtSupplySearch;
    private readonly DataGridView _gridSupplies;
    private readonly GridPagerBar _supplyPager;
    private readonly GroupBox _grpSupplies;

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
        _otDateRange = new DateRangeFilterControl(compact: true);
        _otDateRange.RangeChanged += (_, _) => LoadMantOts();

        _gridMantOts = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridMantOts);
        _gridMantOts.SelectionChanged += (_, _) => LoadSelectedOtDetail();
        ContextMenuHelper.AttachToGrid(_gridMantOts, idx =>
        {
            MantWorkOrderRow? row = GridBindingHelper.GetRowSource<MantWorkOrderRow>(_gridMantOts, idx);
            if (row is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Copiar N° OT", AppIcon.Copy, CambiasoTheme.Primary, () => Clipboard.SetText(row.NumeroOt)),
                ContextMenuHelper.Item("Copiar informe / descripción", AppIcon.FileText, CambiasoTheme.Accent, () => Clipboard.SetText(
                    string.IsNullOrWhiteSpace(row.DescripcionTecnica) ? row.DescripcionBreve : $"{row.DescripcionBreve}\r\n{row.DescripcionTecnica}")),
                ContextMenuHelper.Item("Copiar equipo", AppIcon.Cpu, CambiasoTheme.Primary, () => Clipboard.SetText(row.Equipo), enabled: !string.IsNullOrWhiteSpace(row.Equipo))
            ];
        });
        _otPager = new GridPagerBar(200);
        _otPager.PageChanged += (_, _) => LoadMantOts(resetPage: false);

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
        ContextMenuHelper.AttachToGrid(_gridMpPlans, idx =>
        {
            MantPreventivePlanRow? plan = GridBindingHelper.GetRowSource<MantPreventivePlanRow>(_gridMpPlans, idx);
            if (plan is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Copiar descripción MP", AppIcon.Copy, CambiasoTheme.Primary, () => Clipboard.SetText(plan.Descripcion)),
                ContextMenuHelper.Item("Copiar equipo", AppIcon.Cpu, CambiasoTheme.Accent, () => Clipboard.SetText(plan.Equipo), enabled: !string.IsNullOrWhiteSpace(plan.Equipo))
            ];
        });

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
        ContextMenuHelper.AttachToGrid(_gridSupplies, idx =>
        {
            MantSupplyRow? item = GridBindingHelper.GetRowSource<MantSupplyRow>(_gridSupplies, idx);
            if (item is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Copiar código SAP", AppIcon.Copy, CambiasoTheme.Primary, () => Clipboard.SetText(item.CodSap), enabled: !string.IsNullOrWhiteSpace(item.CodSap)),
                ContextMenuHelper.Item("Copiar descripción", AppIcon.Package, CambiasoTheme.Accent, () => Clipboard.SetText(item.Descripcion), enabled: !string.IsNullOrWhiteSpace(item.Descripcion))
            ];
        });
        _supplyPager = new GridPagerBar(200);
        _supplyPager.PageChanged += (_, _) => LoadSupplies(resetPage: false);
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
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 32));

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 2) };
        Button btnSearch = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Consultar OTs de mantención con filtros", width: 92, height: 30);
        btnSearch.Click += (_, _) => LoadMantOts();

        Button btnClear = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Restablecer filtros de búsqueda", width: 94, height: 30);
        btnClear.Click += (_, _) =>
        {
            _txtOtSearch.Clear();
            if (_cmbOtTrade.Items.Count > 0) _cmbOtTrade.SelectedIndex = 0;
            if (_cmbOtStatus.Items.Count > 0) _cmbOtStatus.SelectedIndex = 0;
            if (_cmbOtYear.Items.Count > 0) _cmbOtYear.SelectedIndex = 0;
            _otDateRange.Clear();
            LoadMantOts();
        };

        Button btnExport = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar OTs filtradas a Excel, PDF, Word o CSV", width: 106, height: 30);
        btnExport.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            btnExport,
            this,
            _ctx.DocumentExportService,
            "SISPRO_OTs_Mantencion",
            () => DocumentExportService.BuildMaintenanceOrdersReport(
                _ctx.MaintenanceService.SearchInternalWorkOrders(_session, BuildMantOtFilter(maxRows: 5000)),
                BuildMantOtFilterSummary()));

        bar.Controls.AddRange(
        [
            Wrap("Buscar:", _txtOtSearch),
            Wrap("Oficio:", _cmbOtTrade),
            Wrap("Estado:", _cmbOtStatus),
            Wrap("Año:", _cmbOtYear),
            btnSearch, btnClear, btnExport
        ]);

        FlowLayoutPanel dateBar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2, 2, 0, 2) };
        dateBar.Controls.Add(_otDateRange);

        _grpMantOts.Controls.Add(_gridMantOts);
        _grpMantOts.Controls.Add(_otPager);

        GroupBox grpDetail = CambiasoTheme.CreateGroupBox("Detalle Técnico, Técnicos Asignados e Insumos Consumidos");
        grpDetail.Dock = DockStyle.Fill;
        grpDetail.Controls.Add(_txtOtDetail);

        layout.Controls.Add(bar, 0, 0);
        layout.Controls.Add(dateBar, 0, 1);
        layout.Controls.Add(_grpMantOts, 0, 2);
        layout.Controls.Add(grpDetail, 0, 3);
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
        Button btnExport = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar insumos SAP a Excel, PDF, Word o CSV", width: 110, height: 30);
        btnExport.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            btnExport,
            this,
            _ctx.DocumentExportService,
            "SISPRO_Insumos_SAP",
            () => DocumentExportService.BuildSuppliesReport(
                _ctx.MaintenanceService.SearchSupplies(_session, _txtSupplySearch.Text, maxRows: 3000),
                string.IsNullOrWhiteSpace(_txtSupplySearch.Text) ? "Catálogo completo" : $"Filtro: {_txtSupplySearch.Text}"));

        bar.Controls.AddRange([Wrap("Buscar:", _txtSupplySearch), btnSearch, btnRefresh, btnExport]);
        _grpSupplies.Controls.Add(_gridSupplies);
        _grpSupplies.Controls.Add(_supplyPager);

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

    private MantWorkOrderFilter BuildMantOtFilter(int maxRows = 400) => new()
    {
        TextoBusqueda = _txtOtSearch.Text,
        Oficio = _cmbOtTrade.SelectedIndex > 0 ? _cmbOtTrade.SelectedItem?.ToString() : null,
        Estado = _cmbOtStatus.SelectedIndex > 0 ? _cmbOtStatus.SelectedItem?.ToString() : null,
        Anio = _cmbOtYear.SelectedItem is int yr ? yr : null,
        FechaInicio = _otDateRange.FechaInicio,
        FechaFin = _otDateRange.FechaFin,
        MaxRows = maxRows
    };

    private void LoadMantOts(bool resetPage = true)
    {
        if (!_otDateRange.TryValidate(this, out _))
        {
            return;
        }

        if (resetPage)
        {
            _otPager.ResetPageIndex();
        }

        PagedResult<MantWorkOrderRow> page = _ctx.MaintenanceService.SearchInternalWorkOrdersPaged(
            _session,
            BuildMantOtFilter(),
            _otPager.PageIndex,
            _otPager.PageSize);

        _grpMantOts.Text = $"Órdenes de Trabajo de Mantención Interna ({page.TotalCount:N0} registros — más recientes primero)";
        _gridMantOts.DataSource = new SortableBindingList<MantOtGridRow>(
            page.Items.Select(o => new MantOtGridRow(o)));
        _otPager.SetState(page.PageIndex, page.PageSize, page.TotalCount);

        if (_gridMantOts.Columns.Count >= 8)
        {
            GridBindingHelper.ConfigureColumn(_gridMantOts, 0, "N° OT", 8);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 1, "Inicio", 14);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 2, "Planta", 10);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 3, "Equipo", 14);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 4, "Oficio", 8);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 5, "Estado", 10);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 6, "Det.", 6);
            GridBindingHelper.ConfigureColumn(_gridMantOts, 7, "Descripción", 30);
        }
    }

    private void LoadSelectedOtDetail()
    {
        MantWorkOrderRow? selected = GridBindingHelper.GetRowSource<MantWorkOrderRow>(_gridMantOts);
        if (selected is null)
        {
            _txtOtDetail.Clear();
            return;
        }

        MantWorkOrderDetail? detail = _ctx.MaintenanceService.GetInternalWorkOrderDetail(_session, selected.Id);
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
        IReadOnlyList<MantPreventivePlanRow> plans = _ctx.MaintenanceService.SearchPreventivePlans(_session, _txtMpSearch.Text);
        _gridMpPlans.DataSource = new SortableBindingList<MpPlanGridRow>(plans.Select(p => new MpPlanGridRow(p)));
    }

    private void LoadSelectedMpDetails()
    {
        MantPreventivePlanRow? plan = GridBindingHelper.GetRowSource<MantPreventivePlanRow>(_gridMpPlans);
        if (plan is null) return;

        _selectedMpId = plan.Id;
        IReadOnlyList<MantPreventiveCycleRow> cycles = _ctx.MaintenanceService.GetPreventiveCycles(_session, _selectedMpId);
        _gridMpCycles.DataSource = new SortableBindingList<MpCycleGridRow>(cycles.Select(c => new MpCycleGridRow(c)));

        LoadTasksFor(_selectedMpId, cycles.Count > 0 ? cycles[0].IdCiclo : null);
    }

    private void LoadSelectedCycleTasks()
    {
        MantPreventiveCycleRow? cycle = GridBindingHelper.GetRowSource<MantPreventiveCycleRow>(_gridMpCycles);
        if (_selectedMpId <= 0 || cycle is null) return;
        LoadTasksFor(_selectedMpId, cycle.IdCiclo);
    }

    private void LoadTasksFor(int mpId, int? cycleId)
    {
        IReadOnlyList<MantPreventiveTaskRow> tasks = _ctx.MaintenanceService.GetPreventiveTasks(_session, mpId, cycleId);
        _gridMpTasks.DataSource = new SortableBindingList<MpTaskGridRow>(tasks.Select(t => new MpTaskGridRow(t)));
        GridBindingHelper.ConfigureColumn(_gridMpTasks, 5, "Duración", null, "0.##", DataGridViewContentAlignment.MiddleRight);
    }

    private void LoadSupplies(bool resetPage = true)
    {
        _suppliesLoaded = true;
        if (resetPage)
        {
            _supplyPager.ResetPageIndex();
        }

        PagedResult<MantSupplyRow> page = _ctx.MaintenanceService.SearchSuppliesPaged(_session, _txtSupplySearch.Text, _supplyPager.PageIndex, _supplyPager.PageSize);
        _grpSupplies.Text = $"Catálogo de Insumos y Repuestos SAP ({page.TotalCount:N0} registros)";
        _gridSupplies.DataSource = new SortableBindingList<SupplyGridRow>(page.Items.Select(s => new SupplyGridRow(s)));
        _supplyPager.SetState(page.PageIndex, page.PageSize, page.TotalCount);
        GridBindingHelper.ConfigureColumn(_gridSupplies, 4, "Stock", null, "N1", DataGridViewContentAlignment.MiddleRight);
        GridBindingHelper.ConfigureColumn(_gridSupplies, 5, "Días Entrega", null, "N0", DataGridViewContentAlignment.MiddleRight);
        GridBindingHelper.ConfigureColumn(_gridSupplies, 6, "Usos Históricos", null, "N0", DataGridViewContentAlignment.MiddleRight);
    }

    private void LoadPersonnel()
    {
        _personnelLoaded = true;
        IReadOnlyList<MantPersonnelRow> tech = _ctx.MaintenanceService.SearchPersonnel(_session, _txtPersSearch.Text);
        _gridPersonnel.DataSource = new SortableBindingList<PersonnelGridRow>(tech.Select(p => new PersonnelGridRow(p)));

        IReadOnlyList<MantHrPersonnelRow> hr = _ctx.MaintenanceService.GetHrPersonnel(_session, _txtPersSearch.Text);
        _gridHrPersonnel.DataSource = new SortableBindingList<HrPersonnelGridRow>(hr.Select(h => new HrPersonnelGridRow(h)));
    }

    private void LoadCatalogs()
    {
        _catalogsLoaded = true;
        _gridLegacyExt.DataSource = new SortableBindingList<LegacyExtGridRow>(
            _ctx.MaintenanceService.GetLegacyExternalOrders(_session).Select(e => new LegacyExtGridRow(e)));
        GridBindingHelper.ConfigureColumn(_gridLegacyExt, 4, "Costo", null, "$#,##0", DataGridViewContentAlignment.MiddleRight);

        _gridMotives.DataSource = new SortableBindingList<CatalogGridRow>(
            _ctx.MaintenanceService.GetFailureMotives(_session).Select(m => new CatalogGridRow(m.Id, m.Nombre, m.Detalle)));
        _gridTrades.DataSource = new SortableBindingList<TradeGridRow>(
            _ctx.MaintenanceService.GetTradesCatalog(_session).Select(t => new TradeGridRow(t.Id, t.Nombre)));
        _gridChecklists.DataSource = new SortableBindingList<ChecklistGridRow>(
            _ctx.MaintenanceService.GetChecklistsSummary(_session).Select(c => new ChecklistGridRow(c.Id, c.Nombre, c.Detalle)));
    }

    private string BuildMantOtFilterSummary()
    {
        string trade = _cmbOtTrade.SelectedItem?.ToString() ?? "Todos";
        string status = _cmbOtStatus.SelectedItem?.ToString() ?? "Todos";
        string year = _cmbOtYear.SelectedItem?.ToString() ?? "Todos";
        return $"Oficio: {trade} | Estado: {status} | Año: {year} | {_otDateRange.BuildFilterDescription()}";
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

    private sealed class MantOtGridRow(MantWorkOrderRow source) : IGridRowSource<MantWorkOrderRow>
    {
        [Browsable(false)]
        public MantWorkOrderRow Source { get; } = source;
        public string NumeroOt => Source.NumeroOt;
        public string Inicio => Source.FechaInicio.Length >= 16 ? Source.FechaInicio[..16] : Source.FechaInicio;
        public string Planta => Source.Planta;
        public string Equipo => Source.Equipo;
        public string Oficio => Source.Oficio;
        public string Estado => Source.Estado;
        public string Detencion => FormatDetencion(Source.Detencion);
        public string Descripcion => Source.DescripcionBreve;
    }

    private sealed class MpPlanGridRow(MantPreventivePlanRow source) : IGridRowSource<MantPreventivePlanRow>
    {
        [Browsable(false)]
        public MantPreventivePlanRow Source { get; } = source;
        public int Id => Source.Id;
        public string Planta => Source.Planta;
        public string Equipo => Source.Equipo;
        public string Descripcion => Source.Descripcion;
        public string Tipo => Source.TipoMantenimiento;
        public string Frecuencia => Source.Frecuencia;
        public string Estado => Source.Estado;
        public int Ciclos => Source.TotalCiclos;
        public int Tareas => Source.TotalTareas;
    }

    private sealed class MpCycleGridRow(MantPreventiveCycleRow source) : IGridRowSource<MantPreventiveCycleRow>
    {
        [Browsable(false)]
        public MantPreventiveCycleRow Source { get; } = source;
        public int Ciclo => Source.IdCiclo;
        public string Inicio => Source.FechaInicio;
        public string Cierre => Source.FechaCierre;
        public string Tiempo => Source.Tiempo.ToString("0.##", CultureInfo.InvariantCulture);
        public string Estado => Source.Estado;
    }

    private sealed class MpTaskGridRow(MantPreventiveTaskRow source)
    {
        public int Ciclo { get; } = source.IdCiclo;
        public int Tarea { get; } = source.IdTarea;
        public string Descripcion { get; } = source.Descripcion;
        public string Oficio { get; } = source.Oficio;
        public string Estado { get; } = source.Estado;
        public double Duracion { get; } = source.Duracion;
    }

    private sealed class SupplyGridRow(MantSupplyRow source) : IGridRowSource<MantSupplyRow>
    {
        [Browsable(false)]
        public MantSupplyRow Source { get; } = source;
        public int ID => Source.IdItem;
        public string SAP => Source.CodSap;
        public string CodProveedor => Source.CodProveedor;
        public string Descripcion => Source.Descripcion;
        public double Stock => Source.StockUnidad;
        public int DiasEntrega => Source.DiasEntrega;
        public int UsosHistoricos => Source.UsosHistoricos;
    }

    private sealed class PersonnelGridRow(MantPersonnelRow source)
    {
        public int ID { get; } = source.IdPersonal;
        public string RUT { get; } = source.Rut;
        public string Nombre { get; } = source.NombreCompleto;
        public string Oficio { get; } = source.Oficio;
        public string Correo { get; } = source.Email;
        public string Telefono { get; } = source.Celular;
        public string Departamento { get; } = source.Departamento;
        public int OTsAtendidas { get; } = source.OrdenesAtendidas;
    }

    private sealed class HrPersonnelGridRow(MantHrPersonnelRow source)
    {
        public string ID { get; } = source.IdEmpleado;
        public string Nombre { get; } = source.NombreCompleto;
        public string FechaAlta { get; } = source.FechaAlta;
        public string Puesto { get; } = source.Puesto;
        public string Departamento { get; } = source.Departamento;
        public string CentroTrabajo { get; } = source.CentroTrabajo;
    }

    private sealed class LegacyExtGridRow(MantExternalOrderRow source)
    {
        public int N_OTE { get; } = source.NumeroOte;
        public string Pedido { get; } = source.FechaPedido;
        public string Empresa { get; } = source.Empresa;
        public string Descripcion { get; } = source.Descripcion;
        public decimal Costo { get; } = source.Costo;
        public string Garantia { get; } = source.Garantia;
    }

    private sealed record CatalogGridRow(int Id, string Motivo, string Estado);
    private sealed record TradeGridRow(int Id, string Oficio);
    private sealed record ChecklistGridRow(int Id, string Tarea, string Grupo);
}
