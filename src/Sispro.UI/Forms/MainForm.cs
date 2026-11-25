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
/// Ventana principal corporativa de SISPRO (.NET 10).
/// Provee barra lateral con control estricto RBAC, Panel de Control con gráficas vectoriales
/// analíticas en vivo de Trabajos Externos, botón principal + Nueva OT Externa y gestión integral.
/// </summary>
public sealed class MainForm : Form
{
    private readonly SisproApplicationContext _ctx;
    private UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly Panel _contentHost;
    private readonly Label _lblModuleTitle;
    private readonly Label _lblUserBadge;
    private readonly ToolStripStatusLabel _statusUser;
    private readonly ToolStripStatusLabel _statusDb;
    private readonly ToolStripStatusLabel _statusClock;
    private readonly System.Windows.Forms.Timer _clockTimer;
    private readonly System.Windows.Forms.Timer _syncTimer;
    private bool _syncSubscribed;
    private bool _configSubscribed;

    // Navigation & top-bar buttons (exposed internally for automated RBAC/UI testing)
    internal Button NavDashboard { get; }
    internal Button NavWorkOrders { get; }
    internal Button NavMaintainers { get; }
    internal Button NavUsers { get; }
    internal Button NavAudit { get; }
    internal Button BtnTopSwitchUser { get; }
    internal Button BtnTopAbout { get; }
    internal Button BtnTopLogout { get; }
    internal Button? BtnHeroNewExternalOt { get; private set; }
    internal UserSession ActiveSession => _session;

    // Cached module views for instant tab-switching without slow reloads
    private Panel? _dashboardView;
    private WorkOrdersForm? _workOrdersView;
    private MaintainersForm? _maintainersView;
    private UsersForm? _usersView;

    // Dashboard KPI labels & modern vector charts
    private Label? _kpiTotalOts;
    private Label? _kpiProviders;
    private Label? _kpiAssets;
    private Label? _kpiCurrentYearOts;
    private Label? _kpiRequesters;
    private Label? _lblApproversSummary;
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
        _ctx.CurrentSession = session;
        CambiasoTheme.EnableDoubleBuffering(this);

        UpdateWindowTitle();
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(1380, 840);
        MinimumSize = new Size(1120, 680);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        // --- TOP BAR (solo: Cambiar usuario · Acerca de · Cerrar sesión) ---
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

        BtnTopLogout = CambiasoTheme.CreateButton("Cerrar sesión", AppIcon.Logout, ButtonTone.Danger, _toolTip, "Cerrar sesión actual y volver al inicio de sesión", width: 136, height: 32);
        BtnTopLogout.Click += (_, _) => PerformLogout();

        BtnTopAbout = CambiasoTheme.CreateButton("Acerca de", AppIcon.Info, ButtonTone.Accent, _toolTip, "Información del software, versión y fecha de actualización", width: 118, height: 32);
        BtnTopAbout.Click += (_, _) =>
        {
            using AboutDialog dlg = new(_ctx);
            dlg.ShowDialog(this);
        };

        BtnTopSwitchUser = CambiasoTheme.CreateButton("Cambiar usuario", AppIcon.Users, ButtonTone.Secondary, _toolTip, "Cambiar la cuenta activa sin cerrar la aplicación", width: 150, height: 32);
        BtnTopSwitchUser.Click += (_, _) => PromptSwitchUser();

        topActions.Controls.AddRange([BtnTopLogout, BtnTopAbout, BtnTopSwitchUser]);
        topBar.Controls.Add(_lblModuleTitle);
        topBar.Controls.Add(topActions);

        // --- SIDEBAR (Editor de Árbol movido exclusivamente dentro de Mantenedores) ---
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

        _lblUserBadge = new Label
        {
            Dock = DockStyle.Top,
            Height = 46,
            Text = $"{_session.Usuario.NombreCompleto}\nPerfil: {_session.Usuario.PerfilDisplay}",
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

        NavDashboard = CreateNavButton("Inicio / Analítica", AppIcon.Dashboard, "Resumen operativo, KPIs y gráficas de Órdenes de Trabajo");
        NavDashboard.Click += (_, _) => ShowDashboard();

        NavWorkOrders = CreateNavButton("Trabajos Externos", AppIcon.WorkOrder, "Búsqueda de OT con CRUD completo e Ingreso / Edición de Órdenes de Trabajo (M1)");
        NavWorkOrders.Click += (_, _) => ShowWorkOrders();

        NavMaintainers = CreateNavButton("Mantenedores", AppIcon.Provider, "Proveedores, solicitantes, firmas, activos, Editor de Árbol, Configuración y Mi Clave");
        NavMaintainers.Click += (_, _) => ShowMaintainers();

        NavUsers = CreateNavButton("Usuarios y Perfiles", AppIcon.Users, "Gestión de usuarios, solicitados por con firma PNG/JPG/PDF, grupos y perfiles de usuario (exclusivo Administrador)");
        NavUsers.Click += (_, _) => ShowUsers();

        NavAudit = CreateNavButton("Auditoría", AppIcon.Audit, "Registro de auditoría de seguridad y operaciones (exclusivo Administrador)");
        NavAudit.Click += (_, _) =>
        {
            if (!IsCurrentSessionAdmin())
            {
                CambiasoTheme.ShowWarning(this, "Acceso denegado: el registro de auditoría es exclusivo del perfil Administrador.");
                return;
            }

            using AuditLogDialog dlg = new(_ctx.Audit, _ctx.DocumentExportService);
            dlg.ShowDialog(this);
        };

        navFlow.Controls.AddRange([NavDashboard, NavWorkOrders, NavMaintainers, NavUsers, NavAudit]);
        navContainer.Controls.Add(navFlow);

        Button btnExit = CreateNavButton("Salir del Sistema", AppIcon.Exit, "Cerrar completamente SISPRO");
        btnExit.Dock = DockStyle.Bottom;
        btnExit.Click += (_, _) => Close();

        sidebar.Controls.Add(navContainer);
        sidebar.Controls.Add(_lblUserBadge);
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
        _statusUser = new ToolStripStatusLabel($"Usuario: {_session.Usuario.AliasUsuario} ({_session.Usuario.PerfilDisplay})") { BorderSides = ToolStripStatusLabelBorderSides.Right };
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

        _ctx.ConfigurationChanged += OnConfigurationChanged;
        _configSubscribed = true;

        Controls.Add(_contentHost);
        Controls.Add(sidebar);
        Controls.Add(topBar);
        Controls.Add(status);

        ApplySessionPermissionsToChrome();
        ShowDashboard();
    }

    private bool IsCurrentSessionAdmin() =>
        _session.Usuario.Nivel == UserRoleLevel.Administrador && _session.Usuario.Activo;

    private void UpdateWindowTitle()
    {
        string tempTag = _ctx.SystemConfigService.HasTemporaryChanges ? " [Config. Temporal]" : string.Empty;
        Text = $"{Sispro.Core.AppMetadata.ShortName} v{Sispro.Core.AppMetadata.ShortVersion} — {_ctx.Settings.CompanyName} ({_session.Usuario.AliasUsuario}){tempTag}";
    }

    private void ApplySessionPermissionsToChrome()
    {
        bool isAdmin = IsCurrentSessionAdmin();
        NavWorkOrders.Enabled = _session.Permisos.MenuOrdenesTrabajo;
        // Mantenedores permanece accesible para que cualquier usuario pueda usar "Mi Clave";
        // las pestañas internas de catálogos, Editor de Árbol y Configuración aplican RBAC estricto.
        NavMaintainers.Enabled = true;
        NavUsers.Enabled = isAdmin;
        NavAudit.Enabled = isAdmin;

        _lblUserBadge.Text = $"{_session.Usuario.NombreCompleto}\nPerfil: {_session.Usuario.PerfilDisplay}";
        _statusUser.Text = $"Usuario: {_session.Usuario.AliasUsuario} ({_session.Usuario.PerfilDisplay})";
        _statusDb.Text = $"BD: {_ctx.Settings.DatabasePath}";
        UpdateWindowTitle();
    }

    private void PerformLogout()
    {
        if (!CambiasoTheme.Confirm(this, "¿Desea cerrar la sesión actual?"))
        {
            return;
        }

        _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Seguridad", "LOGOUT", "Cierre de sesión por el usuario.");
        _ctx.CurrentSession = null;
        DisposeCachedModuleViews();
        LogoutRequested = true;
        Close();
    }

    private void PromptSwitchUser()
    {
        using LoginForm dlg = new(_ctx, isSwitchUserMode: true);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.AuthenticatedSession is null)
        {
            return;
        }

        SwitchActiveSession(dlg.AuthenticatedSession);
    }

    /// <summary>
    /// Cambia la cuenta activa en caliente sin cerrar la aplicación, descartando vistas en caché
    /// y refrescando permisos, menús, barra de estado y datos visibles para el nuevo usuario.
    /// </summary>
    public void SwitchActiveSession(UserSession newSession)
    {
        ArgumentNullException.ThrowIfNull(newSession);
        string previousAlias = _session.Usuario.AliasUsuario;
        _session = newSession;
        _ctx.CurrentSession = newSession;

        _ctx.Audit.Log(
            newSession.Usuario.AliasUsuario,
            "Seguridad",
            "CAMBIO_USUARIO",
            $"Cambio de sesión activa sin cerrar aplicación: '{previousAlias}' → '{newSession.Usuario.AliasUsuario}' (Perfil: {newSession.Usuario.PerfilDisplay}).");

        DisposeCachedModuleViews();
        ApplySessionPermissionsToChrome();
        ShowDashboard();
    }

    private void DisposeCachedModuleViews()
    {
        _contentHost.Controls.Clear();
        _dashboardView?.Dispose();
        _dashboardView = null;
        _workOrdersView?.Dispose();
        _workOrdersView = null;
        _maintainersView?.Dispose();
        _maintainersView = null;
        _usersView?.Dispose();
        _usersView = null;
    }

    private void OnConfigurationChanged(object? sender, bool isTemporary)
    {
        if (IsDisposed)
        {
            return;
        }

        void RefreshChrome()
        {
            if (IsDisposed) return;
            UpdateWindowTitle();
            _statusDb.Text = isTemporary
                ? $"BD (Temporal): {_ctx.Settings.DatabasePath}"
                : $"BD: {_ctx.Settings.DatabasePath}";
            if (_dashboardView is not null && _dashboardView.Visible)
            {
                RefreshDashboard();
            }
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshChrome);
        else if (!InvokeRequired) RefreshChrome();
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
        foreach (Button b in new[] { NavDashboard, NavWorkOrders, NavMaintainers, NavUsers, NavAudit })
        {
            b.BackColor = b == active ? CambiasoTheme.Accent : CambiasoTheme.PrimaryDark;
        }
    }

    private void StartNewExternalOrderFlow()
    {
        ShowWorkOrders();
        _workOrdersView?.StartNewExternalOrder();
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

    public void ShowMaintainers(string? initialTabTitle = null)
    {
        HighlightNav(NavMaintainers);
        _lblModuleTitle.Text = "Mantenedores — Administración, Configuración y Mi Clave";
        if (_maintainersView is null)
        {
            _maintainersView = new MaintainersForm(_ctx, _session);
            _maintainersView.CatalogsChanged += (_, _) => RefreshDashboard();
        }

        if (!string.IsNullOrWhiteSpace(initialTabTitle))
        {
            _maintainersView.SelectTabByTitle(initialTabTitle);
        }

        HostControl(_maintainersView);
    }

    public void ShowUsers(bool openRequestersTab = false)
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
        _lblModuleTitle.Text = "GESTIÓN DE USUARIOS, SOLICITANTES Y GRUPOS";
        _usersView ??= new UsersForm(_ctx, _session);
        _usersView.RefreshData();
        if (openRequestersTab)
        {
            _usersView.ShowRequestersTab();
        }

        HostControl(_usersView);
    }

    private Panel BuildDashboardView()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = CambiasoTheme.Surface, Padding = new Padding(6) };

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 1. HERO ACTION BANNER (+ Nueva OT Externa como acción principal)
        Panel heroCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(16, 8, 16, 10),
            Margin = new Padding(0, 0, 0, 8)
        };
        CambiasoTheme.EnableDoubleBuffering(heroCard);
        heroCard.Paint += (_, e) =>
        {
            using System.Drawing.Drawing2D.LinearGradientBrush grad = new(
                heroCard.ClientRectangle,
                CambiasoTheme.PrimaryDark,
                CambiasoTheme.Primary,
                System.Drawing.Drawing2D.LinearGradientMode.Horizontal);
            e.Graphics.FillRectangle(grad, heroCard.ClientRectangle);
            using SolidBrush goldBar = new(CambiasoTheme.GoldAccent);
            e.Graphics.FillRectangle(goldBar, 0, heroCard.Height - 3, heroCard.Width, 3);
        };

        FlowLayoutPanel heroLeft = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0)
        };
        Label heroTitle = new()
        {
            Text = "Trabajos Externos — Crear y Gestionar Órdenes de Trabajo",
            Font = CambiasoTheme.HeroTitleFont,
            ForeColor = Color.White,
            AutoSize = true
        };
        _lblApproversSummary = new Label
        {
            Text = "Cargando indicadores de Órdenes de Trabajo...",
            Font = CambiasoTheme.SmallFont,
            ForeColor = Color.FromArgb(215, 232, 245),
            AutoSize = false,
            Width = 590,
            Height = 20,
            AutoEllipsis = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        heroLeft.Controls.AddRange([heroTitle, _lblApproversSummary]);

        FlowLayoutPanel heroActions = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };

        BtnHeroNewExternalOt = CambiasoTheme.CreateButton(
            "Nueva OT Externa",
            AppIcon.Add,
            ButtonTone.Gold,
            _toolTip,
            "Acción principal: crear inmediatamente una nueva Orden de Trabajo externa",
            width: 248,
            height: 44);
        BtnHeroNewExternalOt.Font = CambiasoTheme.HeroButtonFont;
        BtnHeroNewExternalOt.Enabled = _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.IngresoOt;
        BtnHeroNewExternalOt.Click += (_, _) => StartNewExternalOrderFlow();

        Button btnSearchOts = CambiasoTheme.CreateButton("Buscar OTs", AppIcon.Search, ButtonTone.Accent, _toolTip, "Abrir buscador y exportación masiva de OTs externas", width: 132, height: 36);
        btnSearchOts.Margin = new Padding(8, 6, 4, 2);
        btnSearchOts.Enabled = _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.BusquedaOt;
        btnSearchOts.Click += (_, _) =>
        {
            ShowWorkOrders();
            _workOrdersView?.OpenSearchTab();
        };

        Button btnRefresh = CambiasoTheme.CreateButton("Actualizar Panel", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar KPIs, gráficas y últimas OTs", width: 152, height: 36);
        btnRefresh.Margin = new Padding(8, 6, 4, 2);
        btnRefresh.Click += (_, _) => RefreshDashboard();

        heroActions.Controls.AddRange([BtnHeroNewExternalOt, btnSearchOts, btnRefresh]);
        heroCard.Controls.Add(heroLeft);
        heroCard.Controls.Add(heroActions);

        // 2. KPI Cards Row (5 modern External-OT Lucide-badged cards)
        TableLayoutPanel kpis = new() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        for (int i = 0; i < 5; i++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        }

        Panel c1 = CambiasoTheme.CreateMetricCard("OTs Externas", out _kpiTotalOts, CambiasoTheme.Primary, AppIcon.WorkOrder, "Histórico trabajos externos");
        Panel c2 = CambiasoTheme.CreateMetricCard("Proveedores", out _kpiProviders, CambiasoTheme.Accent, AppIcon.Provider, "Catálogo activo");
        Panel c3 = CambiasoTheme.CreateMetricCard("Activos Reales", out _kpiAssets, CambiasoTheme.GoldAccent, AppIcon.Factory, "Árbol industrial 10 niveles");
        Panel c4 = CambiasoTheme.CreateMetricCard($"OTs Año {DateTime.Today.Year}", out _kpiCurrentYearOts, CambiasoTheme.Primary, AppIcon.Calendar, "Órdenes emitidas este año");
        Panel c5 = CambiasoTheme.CreateMetricCard("Solicitantes / Firmas", out _kpiRequesters, CambiasoTheme.Success, AppIcon.Users, "Con firma PNG/JPG/PDF");
        c1.Dock = c2.Dock = c3.Dock = c4.Dock = c5.Dock = DockStyle.Fill;
        c5.Margin = new Padding(0, 0, 0, 8);

        MakeCardClickable(c1, () =>
        {
            if (_session.Permisos.MenuOrdenesTrabajo)
            {
                ShowWorkOrders();
                _workOrdersView?.OpenSearchTab();
            }
        });
        MakeCardClickable(c2, () => { if (_session.Permisos.MenuMantenedor || IsCurrentSessionAdmin()) ShowMaintainers("Proveedores"); });
        MakeCardClickable(c3, () => { if (IsCurrentSessionAdmin()) ShowMaintainers("Editor de Árbol"); else if (_session.Permisos.MenuMantenedor) ShowMaintainers("Árbol de Activos"); });
        MakeCardClickable(c4, () =>
        {
            if (_session.Permisos.MenuOrdenesTrabajo && _session.Permisos.BusquedaOt)
            {
                ShowWorkOrders();
                _workOrdersView?.OpenSearchTab();
            }
        });
        MakeCardClickable(c5, () =>
        {
            if (_ctx.AssetTreeService.IsAdministrator(_session)) ShowUsers(openRequestersTab: true);
            else if (_session.Permisos.MenuMantenedor) ShowMaintainers();
        });

        kpis.Controls.Add(c1, 0, 0);
        kpis.Controls.Add(c2, 1, 0);
        kpis.Controls.Add(c3, 2, 0);
        kpis.Controls.Add(c4, 3, 0);
        kpis.Controls.Add(c5, 4, 0);

        // 3. Live Modern Vector Charts Row
        TableLayoutPanel chartsRow = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 2, 0, 6) };
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

        // 4. Recent Work Orders Grid
        GroupBox grpRecent = CambiasoTheme.CreateGroupBox("Últimas Órdenes de Trabajo Registradas (doble clic para editar · clic derecho para cambiar estado o visualizar)");
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
        _gridRecentOts.CellFormatting += (_, e) =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridRecentOts, e.RowIndex);
            if (row is null || e.CellStyle is null || e.ColumnIndex < 0 || e.ColumnIndex >= _gridRecentOts.Columns.Count) return;

            string prop = _gridRecentOts.Columns[e.ColumnIndex].DataPropertyName;
            if (string.Equals(prop, nameof(RecentOtGridRow.Estado), StringComparison.Ordinal))
            {
                e.CellStyle.Font = CambiasoTheme.LabelFont;
                e.CellStyle.ForeColor = WorkOrderStatusUi.GetColor(row.Estado);
            }
            else if (string.Equals(prop, nameof(RecentOtGridRow.ValorNeto), StringComparison.Ordinal))
            {
                if (!row.MostrarValorNeto)
                {
                    e.Value = "Oculto";
                    e.FormattingApplied = true;
                    e.CellStyle.Font = CambiasoTheme.SmallItalicFont;
                    e.CellStyle.ForeColor = CambiasoTheme.TextMuted;
                }
                else
                {
                    e.CellStyle.Font = CambiasoTheme.LabelFont;
                    e.CellStyle.ForeColor = CambiasoTheme.PrimaryDark;
                }
            }
        };
        ContextMenuHelper.AttachToGrid(_gridRecentOts, rowIndex =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridRecentOts, rowIndex);
            if (row is null)
            {
                return [];
            }

            int idOt = row.IdOt;
            int numOt = row.NumeroOt;
            bool canEdit = _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.IngresoOt;
            return
            [
                ContextMenuHelper.Item("Visualizar OT (Vista Previa)", AppIcon.Preview, CambiasoTheme.Primary, () => ExportUiHelper.ShowWorkOrderPreview(this, _ctx, numOt), enabled: _session.Permisos.MenuOrdenesTrabajo),
                ContextMenuHelper.Item("Abrir / Editar OT", AppIcon.Edit, CambiasoTheme.Accent, () => ShowWorkOrders(numOt), enabled: canEdit),
                WorkOrderStatusUi.BuildChangeStatusMenu(row.Estado, canEdit, newStatus => ChangeDashboardOrderStatus(idOt, numOt, newStatus)),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Actualizar listado", AppIcon.Refresh, CambiasoTheme.Primary, RefreshDashboard)
            ];
        });
        grpRecent.Controls.Add(_gridRecentOts);

        layout.Controls.Add(heroCard, 0, 0);
        layout.Controls.Add(kpis, 0, 1);
        layout.Controls.Add(chartsRow, 0, 2);
        layout.Controls.Add(grpRecent, 0, 3);
        panel.Controls.Add(layout);
        return panel;
    }

    private static void MakeCardClickable(Control root, Action onClick)
    {
        root.Cursor = Cursors.Hand;
        root.Click += (_, _) => onClick();
        foreach (Control child in root.Controls)
        {
            child.Cursor = Cursors.Hand;
            child.Click += (_, _) => onClick();
        }
    }

    private void RefreshDashboard()
    {
        DashboardAnalytics analytics = _ctx.WorkOrderService.GetDashboardAnalytics();
        DashboardMetrics m = analytics.Metrics;
        int nextOt = _ctx.WorkOrderService.GetNextWorkOrderNumber();
        double currentYearRaw = analytics.WorkOrdersByYear
            .FirstOrDefault(y => y.Label == DateTime.Today.Year.ToString())?.Value ?? 0d;
        int currentYearOts = (int)Math.Round(currentYearRaw);

        if (_kpiTotalOts is not null) _kpiTotalOts.Text = m.TotalOrdenesTrabajo.ToString("N0");
        if (_kpiProviders is not null) _kpiProviders.Text = m.TotalProveedoresActivos.ToString("N0");
        if (_kpiAssets is not null) _kpiAssets.Text = m.TotalNodosActivos.ToString("N0");
        if (_kpiCurrentYearOts is not null) _kpiCurrentYearOts.Text = currentYearOts.ToString("N0");
        if (_kpiRequesters is not null) _kpiRequesters.Text = $"{m.TotalSolicitantesActivos:N0} ({m.TotalSolicitantesConFirma:N0})";

        if (_lblApproversSummary is not null)
        {
            _lblApproversSummary.Text = $"Próxima OT: #{nextOt}   ·   Gestión integral de Trabajos Externos, Proveedores y Activos";
            _toolTip.SetToolTip(_lblApproversSummary, "Panel centralizado de órdenes de trabajo externas y firmas digitales");
        }

        _chartYearlyOts?.SetData(analytics.WorkOrdersByYear);
        _chartByPlant?.SetData(analytics.WorkOrdersByPlant);
        _chartTopProviders?.SetData(analytics.TopProviders);

        if (_gridRecentOts is not null)
        {
            _recentOts = _ctx.WorkOrders.Search(new WorkOrderSearchFilter { MaxRows = 40 }).ToList();
            _gridRecentOts.DataSource = new SortableBindingList<RecentOtGridRow>(
                _recentOts.Select(r => new RecentOtGridRow(r)));

            if (_gridRecentOts.Columns.Count >= 10)
            {
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 0, "N° OT", 7);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 1, "Fecha", 10, "dd-MM-yyyy");
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 2, "Proveedor", 15);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 3, "Solicitante", 9);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 4, "Cant.", 5);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 5, "Detalle", 19);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 6, "Activo", 10);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 7, "Valor Neto", 8, "$#,##0", DataGridViewContentAlignment.MiddleRight);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 8, "Estado", 8);
                GridBindingHelper.ConfigureColumn(_gridRecentOts, 9, "Firmas", 9);
            }
        }
    }

    private void ChangeDashboardOrderStatus(int idOt, int numeroOt, WorkOrderStatus newStatus)
    {
        try
        {
            _ctx.WorkOrderService.UpdateStatus(_session, idOt, newStatus);
            RefreshDashboard();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, $"No se pudo cambiar el estado de la OT N° {numeroOt}: {ex.Message}");
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
        public decimal ValorNeto => Source.ValorNeto;
        public string Estado => Source.EstadoDisplay;
        public string Firmas => Source.FirmasDisplay;
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

            if (_configSubscribed)
            {
                _ctx.ConfigurationChanged -= OnConfigurationChanged;
                _configSubscribed = false;
            }

            _syncTimer.Stop();
            _syncTimer.Dispose();
            _clockTimer.Stop();
            _clockTimer.Dispose();
            _toolTip.Dispose();
            _dashboardView?.Dispose();
            _workOrdersView?.Dispose();
            _maintainersView?.Dispose();
            _usersView?.Dispose();
        }

        base.Dispose(disposing);
    }
}
