using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Ventana principal corporativa de SISPRO v4.1 (.NET 10).
/// Provee barra lateral con control estricto RBAC, Panel de Control con gráficas vectoriales
/// analíticas en vivo e integración total con BD Mantención Industrial.
/// </summary>
public sealed class MainForm : Form
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly Panel _contentHost;
    private readonly Label _lblModuleTitle;
    private readonly ToolStripStatusLabel _statusUser;
    private readonly ToolStripStatusLabel _statusDb;
    private readonly ToolStripStatusLabel _statusClock;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _syncTimer;
    private bool _syncSubscribed;

    // Navigation buttons (exposed internally for automated RBAC/UI testing)
    internal Button NavDashboard { get; }
    internal Button NavWorkOrders { get; }
    internal Button NavInvoices { get; }
    internal Button NavMaintainers { get; }
    internal Button NavTreeEditor { get; }
    internal Button NavMaintenance { get; }
    internal Button NavUsers { get; }
    internal Button NavAudit { get; }
    internal Button BtnTopNewExternalOt { get; }

    // Cached module views for instant tab-switching without slow reloads
    private Panel? _dashboardView;
    private WorkOrdersForm? _workOrdersView;
    private InvoicesForm? _invoicesView;
    private MaintainersForm? _maintainersView;
    private AssetTreeEditorView? _treeEditorView;
    private MaintenanceHubForm? _maintenanceHubView;
    private UsersForm? _usersView;

    // Dashboard KPI labels & modern vector charts
    private Label? _kpiTotalOts;
    private Label? _kpiProviders;
    private Label? _kpiAssets;
    private Label? _kpiMantOts;
    private Label? _kpiMpSupplies;
    private VerticalBarChartCard? _chartYearlyOts;
    private DonutChartCard? _chartByPlant;
    private HorizontalRankChartCard? _chartTopProviders;
    private DataGridView? _gridRecentOts;
    private List<WorkOrderSearchRow> _recentOts = [];

    public bool LogoutRequested { get; private set; }

    public MainForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);

        Text = $"SISPRO v4.1 — {_ctx.Settings.CompanyName} ({_session.Usuario.AliasUsuario})";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(1380, 840);
        MinimumSize = new Size(1120, 680);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        // --- TOP BAR ---
        Panel topBar = new()
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = CambiasoTheme.Primary,
            Padding = new Padding(12, 8, 14, 8)
        };

        _lblModuleTitle = new Label
        {
            Text = "Panel de Control — SISPRO",
            Font = CambiasoTheme.TitleFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(236, 15)
        };

        FlowLayoutPanel topActions = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 0)
        };

        Button btnLogout = CambiasoTheme.CreateButton("Cerrar Sesión", AppIcon.Logout, ButtonTone.Danger, _toolTip, "Cerrar sesión actual y volver al ingreso", width: 134, height: 32);
        btnLogout.Click += (_, _) =>
        {
            if (CambiasoTheme.Confirm(this, "¿Desea cerrar la sesión actual?"))
            {
                _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Seguridad", "LOGOUT", "Cierre de sesión por el usuario.");
                LogoutRequested = true;
                Close();
            }
        };

        Button btnChangePwd = CambiasoTheme.CreateButton("Mi Clave", AppIcon.Key, ButtonTone.Accent, _toolTip, "Cambiar su contraseña personal", width: 112, height: 32);
        btnChangePwd.Click += (_, _) =>
        {
            using ChangePasswordDialog dlg = new(_ctx.AuthService, _session);
            dlg.ShowDialog(this);
        };

        bool isAdmin = _session.Usuario.Nivel == UserRoleLevel.Administrador && _session.Usuario.Activo;

        Button btnSettings = CambiasoTheme.CreateButton("Configuración", AppIcon.Settings, ButtonTone.Secondary, _toolTip, "Configurar base de datos, respaldos y datos de empresa", width: 136, height: 32);
        btnSettings.Enabled = isAdmin;
        btnSettings.Click += (_, _) =>
        {
            using SettingsDialog dlg = new(_ctx);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                if (_statusDb is not null)
                {
                    _statusDb.Text = $"BD: {_ctx.Settings.DatabasePath}";
                }

                RefreshDashboard();
            }
        };

        BtnTopNewExternalOt = CambiasoTheme.CreateButton("Nueva OT Externa", AppIcon.Add, ButtonTone.Gold, _toolTip, "Crear inmediatamente una nueva Orden de Trabajo externa", width: 156, height: 32);
        BtnTopNewExternalOt.Enabled = _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.IngresoOt;
        BtnTopNewExternalOt.Click += (_, _) =>
        {
            ShowWorkOrders();
            _workOrdersView?.StartNewExternalOrder();
        };

        topActions.Controls.AddRange([btnLogout, btnChangePwd, btnSettings, BtnTopNewExternalOt]);
        topBar.Controls.Add(_lblModuleTitle);
        topBar.Controls.Add(topActions);

        // --- SIDEBAR ---
        Panel sidebar = new()
        {
            Dock = DockStyle.Left,
            Width = 228,
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(10, 12, 10, 12)
        };

        PictureBox picLogo = new()
        {
            Dock = DockStyle.Top,
            Height = 84,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = IconCatalog.GetCompanyLogo(),
            BackColor = Color.Transparent,
            Padding = new Padding(6, 4, 6, 4)
        };

        Label lblUserBadge = new()
        {
            Dock = DockStyle.Top,
            Height = 46,
            Text = $"{_session.Usuario.NombreCompleto}\nPerfil: {_session.Usuario.Nivel}",
            Font = CambiasoTheme.SmallFont,
            ForeColor = Color.FromArgb(200, 224, 240),
            TextAlign = ContentAlignment.MiddleCenter
        };

        Panel navContainer = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) };
        FlowLayoutPanel navFlow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        NavDashboard = CreateNavButton("Inicio / Analítica", AppIcon.Dashboard, "Resumen operativo, KPIs y gráficas en vivo");
        NavDashboard.Click += (_, _) => ShowDashboard();

        NavWorkOrders = CreateNavButton("Trabajos Externos", AppIcon.WorkOrder, "Ingreso, búsqueda e impresión de Órdenes de Trabajo (M1)");
        NavWorkOrders.Enabled = _session.Permisos.MenuOrdenesTrabajo;
        NavWorkOrders.Click += (_, _) => ShowWorkOrders();

        NavInvoices = CreateNavButton("Facturas y Guías", AppIcon.Invoice, "Guías de despacho, localización y búsqueda de facturas (M2)");
        NavInvoices.Enabled = _session.Permisos.MenuFacturas;
        NavInvoices.Click += (_, _) => ShowInvoices();

        NavMaintainers = CreateNavButton("Mantenedores", AppIcon.Provider, "Proveedores, solicitantes y árbol de activos con ficha técnica (M3)");
        NavMaintainers.Enabled = _session.Permisos.MenuMantenedor;
        NavMaintainers.Click += (_, _) => ShowMaintainers();

        NavTreeEditor = CreateNavButton("Editor de Árbol", AppIcon.Tree, "Crear, editar, mover, activar/desactivar y eliminar nodos del árbol de activos (exclusivo Administrador)");
        NavTreeEditor.Enabled = isAdmin;
        NavTreeEditor.Click += (_, _) => ShowTreeEditor();

        NavMaintenance = CreateNavButton("Mantención Industrial", AppIcon.Wrench, "OTs internas, planes preventivos MP, insumos SAP y personal técnico");
        NavMaintenance.Enabled = _session.Permisos.MenuOrdenesTrabajo || _session.Permisos.MenuMantenedor;
        NavMaintenance.Click += (_, _) => ShowMaintenanceHub();

        NavUsers = CreateNavButton("Usuarios y Grupos", AppIcon.Users, "Gestión de usuarios, grupos y operaciones masivas (exclusivo Administrador)");
        NavUsers.Enabled = isAdmin;
        NavUsers.Click += (_, _) => ShowUsers();

        NavAudit = CreateNavButton("Auditoría", AppIcon.Audit, "Registro de auditoría de seguridad y operaciones");
        NavAudit.Enabled = isAdmin;
        NavAudit.Click += (_, _) =>
        {
            using AuditLogDialog dlg = new(_ctx.Audit, _ctx.DocumentExportService);
            dlg.ShowDialog(this);
        };

        navFlow.Controls.AddRange([NavDashboard, NavWorkOrders, NavInvoices, NavMaintainers, NavTreeEditor, NavMaintenance, NavUsers, NavAudit]);
        navContainer.Controls.Add(navFlow);

        Button btnExit = CreateNavButton("Salir del Sistema", AppIcon.Exit, "Cerrar completamente SISPRO");
        btnExit.Dock = DockStyle.Bottom;
        btnExit.Click += (_, _) => Close();

        sidebar.Controls.Add(navContainer);
        sidebar.Controls.Add(lblUserBadge);
        sidebar.Controls.Add(picLogo);
        sidebar.Controls.Add(btnExit);

        // --- CONTENT HOST ---
        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CambiasoTheme.Surface,
            Padding = new Padding(8)
        };
        CambiasoTheme.EnableDoubleBuffering(_contentHost);

        // --- STATUS BAR ---
        StatusStrip status = new() { BackColor = CambiasoTheme.CardBackground };
        _statusUser = new ToolStripStatusLabel($"Usuario: {_session.Usuario.AliasUsuario} ({_session.Usuario.Nivel})") { BorderSides = ToolStripStatusLabelBorderSides.Right };
        _statusDb = new ToolStripStatusLabel($"BD: {_ctx.Settings.DatabasePath}") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
        _statusClock = new ToolStripStatusLabel(DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"));
        status.Items.AddRange([_statusUser, _statusDb, _statusClock]);

        _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _clockTimer.Tick += (_, _) => _statusClock.Text = DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss");
        _clockTimer.Start();

        _syncTimer = new System.Windows.Forms.Timer { Interval = 2500 };
        _syncTimer.Tick += (_, _) => _ctx.SyncHub.PollExternalChanges();
        _syncTimer.Start();

        _ctx.SyncHub.DataChanged += OnSyncDataChanged;
        _syncSubscribed = true;

        Controls.Add(_contentHost);
        Controls.Add(sidebar);
        Controls.Add(topBar);
        Controls.Add(status);

        ShowDashboard();
    }

    private void OnSyncDataChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        void RefreshViews()
        {
            if (IsDisposed)
            {
                return;
            }

            if (_dashboardView is not null && _dashboardView.Visible)
            {
                RefreshDashboard();
            }
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke(RefreshViews);
        }
        else if (!InvokeRequired)
        {
            RefreshViews();
        }
    }

    private Button CreateNavButton(string text, AppIcon icon, string tooltip)
    {
        Button btn = CambiasoTheme.CreateButton(text, icon, ButtonTone.NavItem, _toolTip, tooltip, width: 204, height: 40);
        btn.TextAlign = ContentAlignment.MiddleLeft;
        btn.Margin = new Padding(0, 0, 0, 5);
        btn.Padding = new Padding(12, 0, 8, 0);
        return btn;
    }

    private void HighlightNav(Button active)
    {
        foreach (Button b in new[] { NavDashboard, NavWorkOrders, NavInvoices, NavMaintainers, NavTreeEditor, NavMaintenance, NavUsers, NavAudit })
        {
            b.BackColor = b == active ? CambiasoTheme.Accent : CambiasoTheme.PrimaryDark;
        }
    }

    public void ShowDashboard()
    {
        HighlightNav(NavDashboard);
        _lblModuleTitle.Text = "Panel de Control y Analítica — SISPRO";

        if (_dashboardView is null)
        {
            _dashboardView = BuildDashboardView();
        }

        RefreshDashboard();
        HostControl(_dashboardView);
    }

    public void ShowWorkOrders(int? openOrderNumber = null)
    {
        if (!_session.Permisos.MenuOrdenesTrabajo)
        {
            CambiasoTheme.ShowWarning(this, "No tiene permisos para acceder al módulo de Trabajos Externos.");
            return;
        }

        HighlightNav(NavWorkOrders);
        _lblModuleTitle.Text = "Órdenes de Trabajo — Trabajos Externos";
        _workOrdersView ??= new WorkOrdersForm(_ctx, _session);
        HostControl(_workOrdersView);

        if (openOrderNumber.HasValue)
        {
            _workOrdersView.OpenOrderByNumber(openOrderNumber.Value);
        }
    }

    public void ShowWorkOrderPrint(int numeroOt)
    {
        ShowWorkOrders();
        _workOrdersView?.ShowPrintForOrder(numeroOt);
    }

    public void ShowInvoices()
    {
        if (!_session.Permisos.MenuFacturas)
        {
            CambiasoTheme.ShowWarning(this, "No tiene permisos para acceder al módulo de Facturas y Guías.");
            return;
        }

        HighlightNav(NavInvoices);
        _lblModuleTitle.Text = "Guías de Despacho y Facturas";
        _invoicesView ??= new InvoicesForm(_ctx, _session);
        HostControl(_invoicesView);
    }

    public void ShowMaintainers()
    {
        if (!_session.Permisos.MenuMantenedor)
        {
            CambiasoTheme.ShowWarning(this, "No tiene permisos para acceder al módulo de Mantenedores.");
            return;
        }

        HighlightNav(NavMaintainers);
        _lblModuleTitle.Text = "Mantenedores — Proveedores, Solicitantes y Activos";
        if (_maintainersView is null)
        {
            _maintainersView = new MaintainersForm(_ctx, _session);
            _maintainersView.CatalogsChanged += (_, _) => RefreshDashboard();
            _maintainersView.OpenTreeEditorRequested += (_, _) => ShowTreeEditor();
        }

        HostControl(_maintainersView);
    }

    public void ShowTreeEditor()
    {
        if (!_ctx.AssetTreeService.IsAdministrator(_session))
        {
            CambiasoTheme.ShowWarning(this, "Acceso denegado: el Editor del Árbol de Activos es exclusivo del perfil Administrador.");
            return;
        }

        HighlightNav(NavTreeEditor);
        _lblModuleTitle.Text = "Editor del Árbol de Activos (Administrador)";
        _treeEditorView ??= new AssetTreeEditorView(_ctx, _session);
        HostControl(_treeEditorView);
    }

    public void ShowMaintenanceHub()
    {
        if (!(_session.Permisos.MenuOrdenesTrabajo || _session.Permisos.MenuMantenedor))
        {
            CambiasoTheme.ShowWarning(this, "No tiene permisos para acceder al módulo de Mantención Industrial.");
            return;
        }

        HighlightNav(NavMaintenance);
        _lblModuleTitle.Text = "Mantención Industrial — BD Mantención, MP, Insumos y Personal";
        _maintenanceHubView ??= new MaintenanceHubForm(_ctx, _session);
        HostControl(_maintenanceHubView);
    }

    public void ShowUsers()
    {
        try
        {
            _ctx.UserManagementService.EnsureCanAccessModule(_session);
        }
        catch (UnauthorizedAccessException ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
            return;
        }

        HighlightNav(NavUsers);
        _lblModuleTitle.Text = "GESTIÓN DE USUARIOS Y GRUPOS";
        _usersView ??= new UsersForm(_ctx, _session);
        _usersView.RefreshData();
        HostControl(_usersView);
    }

    private Panel BuildDashboardView()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = CambiasoTheme.Surface, Padding = new Padding(6) };

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 1. KPI Cards Row (5 modern Lucide-badged cards)
        TableLayoutPanel kpis = new() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        for (int i = 0; i < 5; i++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }

        Panel c1 = CambiasoTheme.CreateMetricCard("OTs Proveedores", out _kpiTotalOts, CambiasoTheme.Primary, AppIcon.WorkOrder, "Histórico trabajos externos");
        Panel c2 = CambiasoTheme.CreateMetricCard("Proveedores", out _kpiProviders, CambiasoTheme.Accent, AppIcon.Provider, "Catálogo activo");
        Panel c3 = CambiasoTheme.CreateMetricCard("Activos Reales", out _kpiAssets, CambiasoTheme.GoldAccent, AppIcon.Factory, "Árbol industrial 10 niveles");
        Panel c4 = CambiasoTheme.CreateMetricCard("OTs Mantención", out _kpiMantOts, CambiasoTheme.Success, AppIcon.Wrench, "BD Mantención interna");
        Panel c5 = CambiasoTheme.CreateMetricCard("Planes MP / Insumos", out _kpiMpSupplies, CambiasoTheme.Warning, AppIcon.Package, "Preventivos · Repuestos SAP");
        c1.Dock = c2.Dock = c3.Dock = c4.Dock = c5.Dock = DockStyle.Fill;
        c5.Margin = new Padding(0, 0, 0, 8);

        kpis.Controls.Add(c1, 0, 0);
        kpis.Controls.Add(c2, 1, 0);
        kpis.Controls.Add(c3, 2, 0);
        kpis.Controls.Add(c4, 3, 0);
        kpis.Controls.Add(c5, 4, 0);

        // 2. Live Modern Vector Charts Row
        TableLayoutPanel chartsRow = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 2, 0, 2) };
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        chartsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        _chartYearlyOts = new VerticalBarChartCard("Evolución Anual de OTs", "Órdenes de trabajo externas por año", AppIcon.TrendingUp) { Dock = DockStyle.Fill };
        _chartByPlant = new DonutChartCard("Distribución por Planta", "Participación por unidad productiva", AppIcon.Factory) { Dock = DockStyle.Fill };
        _chartTopProviders = new HorizontalRankChartCard("Top 6 Proveedores", "Ranking histórico por volumen de OTs", AppIcon.BarChart)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 6)
        };

        chartsRow.Controls.Add(_chartYearlyOts, 0, 0);
        chartsRow.Controls.Add(_chartByPlant, 1, 0);
        chartsRow.Controls.Add(_chartTopProviders, 2, 0);

        // 3. Quick Action Bar
        FlowLayoutPanel quickBar = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 4) };
        Button btnNewOt = CambiasoTheme.CreateButton("Nueva OT Externa", AppIcon.Add, ButtonTone.Gold, _toolTip, "Crear inmediatamente una nueva Orden de Trabajo externa", width: 156, height: 32);
        btnNewOt.Enabled = _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.IngresoOt;
        btnNewOt.Click += (_, _) =>
        {
            ShowWorkOrders();
            _workOrdersView?.StartNewExternalOrder();
        };

        Button btnMaint = CambiasoTheme.CreateButton("Explorar BD Mantención", AppIcon.Wrench, ButtonTone.Accent, _toolTip, "Abrir OTs internas, Planes MP, Insumos SAP y Personal Técnico", width: 196, height: 32);
        btnMaint.Enabled = _session.Permisos.MenuOrdenesTrabajo || _session.Permisos.MenuMantenedor;
        btnMaint.Click += (_, _) => ShowMaintenanceHub();

        Button btnRefresh = CambiasoTheme.CreateButton("Actualizar Panel", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar KPIs, gráficas y últimas OTs", width: 150, height: 32);
        btnRefresh.Click += (_, _) => RefreshDashboard();

        quickBar.Controls.AddRange([btnNewOt, btnMaint, btnRefresh]);

        // 4. Recent Work Orders Grid
        GroupBox grpRecent = CambiasoTheme.CreateGroupBox("Últimas Órdenes de Trabajo Registradas (doble clic para abrir)");
        grpRecent.Dock = DockStyle.Fill;
        _gridRecentOts = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridRecentOts);
        _gridRecentOts.CellDoubleClick += (_, _) =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridRecentOts);
            if (row is not null && _session.Permisos.MenuOrdenesTrabajo)
            {
                ShowWorkOrders(row.NumeroOt);
            }
        };
        ContextMenuHelper.AttachToGrid(_gridRecentOts, rowIndex =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridRecentOts, rowIndex);
            if (row is null)
            {
                return [];
            }

            int numOt = row.NumeroOt;
            return
            [
                ContextMenuHelper.Item("Abrir OT", AppIcon.Edit, CambiasoTheme.Primary, () => ShowWorkOrders(numOt), enabled: _session.Permisos.MenuOrdenesTrabajo),
                ContextMenuHelper.Item("Exportar / Imprimir OT", AppIcon.Print, CambiasoTheme.Accent, () => ShowWorkOrderPrint(numOt), enabled: _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.ImpresionOt)
            ];
        });
        grpRecent.Controls.Add(_gridRecentOts);

        layout.Controls.Add(kpis, 0, 0);
        layout.Controls.Add(chartsRow, 0, 1);
        layout.Controls.Add(quickBar, 0, 2);
        layout.Controls.Add(grpRecent, 0, 3);
        panel.Controls.Add(layout);
        return panel;
    }

    private void RefreshDashboard()
    {
        DashboardAnalytics analytics = _ctx.MaintenanceService.GetDashboardAnalytics();
        DashboardMetrics m = analytics.Metrics;

        if (_kpiTotalOts is not null) _kpiTotalOts.Text = m.TotalOrdenesTrabajo.ToString("N0");
        if (_kpiProviders is not null) _kpiProviders.Text = m.TotalProveedoresActivos.ToString("N0");
        if (_kpiAssets is not null) _kpiAssets.Text = m.TotalNodosActivos.ToString("N0");
        if (_kpiMantOts is not null) _kpiMantOts.Text = m.TotalMantOts.ToString("N0");
        if (_kpiMpSupplies is not null) _kpiMpSupplies.Text = $"{m.TotalPlanesMp:N0} / {m.TotalInsumos:N0}";

        _chartYearlyOts?.SetData(analytics.WorkOrdersByYear);
        _chartByPlant?.SetData(analytics.WorkOrdersByPlant);
        _chartTopProviders?.SetData(analytics.TopProviders);

        if (_gridRecentOts is not null)
        {
            _recentOts = _ctx.WorkOrders.Search(new WorkOrderSearchFilter { MaxRows = 40 }).ToList();
            _gridRecentOts.DataSource = new SortableBindingList<RecentOtGridRow>(
                _recentOts.Select(r => new RecentOtGridRow(r)));

            if (_gridRecentOts.Columns.Count >= 9)
            {
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 0, "N° OT", 7);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 1, "Fecha", 9, "dd-MM-yyyy");
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 2, null, 16);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 3, null, 10);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 4, "Cant.", 6);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 5, null, 24);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 6, null, 18);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 7, null, 5);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 8, null, 5);
            }
        }
    }

    private sealed class RecentOtGridRow(WorkOrderSearchRow source) : IGridRowSource<WorkOrderSearchRow>
    {
        [Browsable(false)]
        public WorkOrderSearchRow Source { get; } = source;
        public int NumeroOt => Source.NumeroOt;
        public DateTime Fecha => Source.Fecha;
        public string Proveedor => Source.Proveedor;
        public string Solicitante => Source.Solicitante;
        public int Cantidad => Source.Cantidad;
        public string Detalle => Source.Detalle;
        public string Activo => Source.RutaActivo.StartsWith("CAMBIASO HNOS S.A.C. \\ ", StringComparison.OrdinalIgnoreCase)
            ? Source.RutaActivo["CAMBIASO HNOS S.A.C. \\ ".Length..]
            : Source.RutaActivo;
        public string VBS => Source.TieneVbs ? "Sí" : "No";
        public string VBJ => Source.TieneVbj ? "Sí" : "No";
    }

    private void HostControl(Control view)
    {
        _contentHost.SuspendLayout();
        try
        {
            foreach (Control existing in _contentHost.Controls)
            {
                existing.Visible = false;
            }

            if (!_contentHost.Controls.Contains(view))
            {
                view.Dock = DockStyle.Fill;
                _contentHost.Controls.Add(view);
            }

            view.Visible = true;
            view.BringToFront();
        }
        finally
        {
            _contentHost.ResumeLayout(true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_syncSubscribed)
            {
                _ctx.SyncHub.DataChanged -= OnSyncDataChanged;
                _syncSubscribed = false;
            }

            _syncTimer.Stop();
            _syncTimer.Dispose();
            _clockTimer.Stop();
            _clockTimer.Dispose();
            _toolTip.Dispose();
            _dashboardView?.Dispose();
            _workOrdersView?.Dispose();
            _invoicesView?.Dispose();
            _maintainersView?.Dispose();
            _treeEditorView?.Dispose();
            _maintenanceHubView?.Dispose();
            _usersView?.Dispose();
        }

        base.Dispose(disposing);
    }
}
