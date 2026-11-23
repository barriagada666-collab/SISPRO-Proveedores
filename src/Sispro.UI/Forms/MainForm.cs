using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Ventana principal corporativa de SISPRO (reemplaza MDI.frm).
/// Provee barra lateral con control estricto RBAC, panel de indicadores en vivo y navegación fluida sin parpadeos.
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

    // Navigation buttons (exposed internally for automated RBAC/UI testing)
    internal Button NavDashboard { get; }
    internal Button NavWorkOrders { get; }
    internal Button NavInvoices { get; }
    internal Button NavMaintainers { get; }
    internal Button NavUsers { get; }
    internal Button NavAudit { get; }

    // Cached module views for instant tab-switching without slow reloads
    private Panel? _dashboardView;
    private WorkOrdersForm? _workOrdersView;
    private InvoicesForm? _invoicesView;
    private MaintainersForm? _maintainersView;
    private UsersForm? _usersView;

    private Label? _kpiTotalOts;
    private Label? _kpiPendingVb;
    private Label? _kpiProviders;
    private Label? _kpiInvoices;
    private DataGridView? _gridRecentOts;
    private List<WorkOrderSearchRow> _recentOts = [];

    public bool LogoutRequested { get; private set; }

    public MainForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);

        Text = $"SISPRO v2.0 — {_ctx.Settings.CompanyName} ({_session.Usuario.AliasUsuario})";
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

        Button btnSettings = CambiasoTheme.CreateButton("Configuración", AppIcon.Settings, ButtonTone.Secondary, _toolTip, "Configurar base de datos, respaldos y datos de empresa", width: 136, height: 32);
        btnSettings.Enabled = _session.Usuario.Nivel == UserRoleLevel.Administrador || _session.Permisos.MenuUsuarios;
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

        topActions.Controls.AddRange([btnLogout, btnChangePwd, btnSettings]);
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
            Height = 86,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = IconCatalog.GetCompanyLogo(),
            BackColor = Color.Transparent,
            Padding = new Padding(6, 4, 6, 4)
        };

        Label lblUserBadge = new()
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = $"{_session.Usuario.NombreCompleto}\nPerfil: {_session.Usuario.Nivel}",
            Font = CambiasoTheme.SmallFont,
            ForeColor = Color.FromArgb(200, 224, 240),
            TextAlign = ContentAlignment.MiddleCenter
        };

        Panel navContainer = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };
        FlowLayoutPanel navFlow = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false
        };

        NavDashboard = CreateNavButton("Inicio / Panel", AppIcon.Dashboard, "Resumen operativo e indicadores");
        NavDashboard.Click += (_, _) => ShowDashboard();

        NavWorkOrders = CreateNavButton("Trabajos Externos", AppIcon.WorkOrder, "Ingreso, búsqueda e impresión de Órdenes de Trabajo (M1)");
        NavWorkOrders.Enabled = _session.Permisos.MenuOrdenesTrabajo;
        NavWorkOrders.Click += (_, _) => ShowWorkOrders();

        NavInvoices = CreateNavButton("Facturas y Guías", AppIcon.Invoice, "Guías de despacho, localización y búsqueda de facturas (M2)");
        NavInvoices.Enabled = _session.Permisos.MenuFacturas;
        NavInvoices.Click += (_, _) => ShowInvoices();

        NavMaintainers = CreateNavButton("Mantenedores", AppIcon.Provider, "Proveedores, solicitantes y árbol de activos (M3)");
        NavMaintainers.Enabled = _session.Permisos.MenuMantenedor;
        NavMaintainers.Click += (_, _) => ShowMaintainers();

        NavUsers = CreateNavButton("Usuarios", AppIcon.Users, "Gestión de usuarios y permisos RBAC (M4)");
        NavUsers.Enabled = _session.Permisos.MenuUsuarios;
        NavUsers.Click += (_, _) => ShowUsers();

        NavAudit = CreateNavButton("Auditoría", AppIcon.Audit, "Registro de auditoría de seguridad y operaciones");
        NavAudit.Enabled = _session.Usuario.Nivel == UserRoleLevel.Administrador || _session.Permisos.MenuUsuarios;
        NavAudit.Click += (_, _) =>
        {
            using AuditLogDialog dlg = new(_ctx.Audit);
            dlg.ShowDialog(this);
        };

        navFlow.Controls.AddRange([NavDashboard, NavWorkOrders, NavInvoices, NavMaintainers, NavUsers, NavAudit]);
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

        Controls.Add(_contentHost);
        Controls.Add(sidebar);
        Controls.Add(topBar);
        Controls.Add(status);

        ShowDashboard();
    }

    private Button CreateNavButton(string text, AppIcon icon, string tooltip)
    {
        Button btn = CambiasoTheme.CreateButton(text, icon, ButtonTone.NavItem, _toolTip, tooltip, width: 204, height: 42);
        btn.TextAlign = ContentAlignment.MiddleLeft;
        btn.Margin = new Padding(0, 0, 0, 6);
        btn.Padding = new Padding(12, 0, 8, 0);
        return btn;
    }

    private void HighlightNav(Button active)
    {
        foreach (Button b in new[] { NavDashboard, NavWorkOrders, NavInvoices, NavMaintainers, NavUsers, NavAudit })
        {
            b.BackColor = b == active ? CambiasoTheme.Accent : CambiasoTheme.PrimaryDark;
        }
    }

    public void ShowDashboard()
    {
        HighlightNav(NavDashboard);
        _lblModuleTitle.Text = "Panel de Control — SISPRO";

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
            _maintainersView.CatalogsChanged += (_, _) =>
            {
                _workOrdersView?.ReloadCatalogs();
                _invoicesView?.ReloadCatalogs();
                RefreshDashboard();
            };
        }

        HostControl(_maintainersView);
    }

    public void ShowUsers()
    {
        if (!_session.Permisos.MenuUsuarios)
        {
            CambiasoTheme.ShowWarning(this, "No tiene permisos para acceder al módulo de Usuarios.");
            return;
        }

        HighlightNav(NavUsers);
        _lblModuleTitle.Text = "Administración de Usuarios y Permisos";
        _usersView ??= new UsersForm(_ctx, _session);
        HostControl(_usersView);
    }

    private Panel BuildDashboardView()
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = CambiasoTheme.Surface, Padding = new Padding(8) };

        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel kpis = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++)
        {
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        Panel c1 = CambiasoTheme.CreateMetricCard("Órdenes de Trabajo", out _kpiTotalOts, CambiasoTheme.Primary);
        Panel c2 = CambiasoTheme.CreateMetricCard("Pendientes VB Jefatura", out _kpiPendingVb, CambiasoTheme.Warning);
        Panel c3 = CambiasoTheme.CreateMetricCard("Proveedores Activos", out _kpiProviders, CambiasoTheme.Accent);
        Panel c4 = CambiasoTheme.CreateMetricCard("Facturas / Guías", out _kpiInvoices, CambiasoTheme.Success);
        c1.Dock = c2.Dock = c3.Dock = c4.Dock = DockStyle.Fill;

        kpis.Controls.Add(c1, 0, 0);
        kpis.Controls.Add(c2, 1, 0);
        kpis.Controls.Add(c3, 2, 0);
        kpis.Controls.Add(c4, 3, 0);

        FlowLayoutPanel quickBar = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 4) };
        Button btnNewOt = CambiasoTheme.CreateButton("Nueva OT", AppIcon.Add, ButtonTone.Primary, _toolTip, "Ir al ingreso de Órdenes de Trabajo", width: 132);
        btnNewOt.Enabled = _session.Permisos.MenuOrdenesTrabajo && _session.Permisos.IngresoOt;
        btnNewOt.Click += (_, _) => ShowWorkOrders();

        Button btnRefresh = CambiasoTheme.CreateButton("Actualizar Panel", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar indicadores y últimas OTs", width: 156);
        btnRefresh.Click += (_, _) => RefreshDashboard();

        quickBar.Controls.AddRange([btnNewOt, btnRefresh]);

        GroupBox grpRecent = CambiasoTheme.CreateGroupBox("Últimas Órdenes de Trabajo Registradas (doble clic para abrir)");
        grpRecent.Dock = DockStyle.Fill;
        _gridRecentOts = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridRecentOts);
        _gridRecentOts.CellDoubleClick += (_, _) =>
        {
            if (_gridRecentOts.CurrentRow is null)
            {
                return;
            }

            int idx = _gridRecentOts.CurrentRow.Index;
            if (idx >= 0 && idx < _recentOts.Count && _session.Permisos.MenuOrdenesTrabajo)
            {
                ShowWorkOrders(_recentOts[idx].NumeroOt);
            }
        };
        grpRecent.Controls.Add(_gridRecentOts);

        layout.Controls.Add(kpis, 0, 0);
        layout.Controls.Add(quickBar, 0, 1);
        layout.Controls.Add(grpRecent, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private void RefreshDashboard()
    {
        DashboardMetrics m = _ctx.Audit.GetDashboardMetrics();
        if (_kpiTotalOts is not null) _kpiTotalOts.Text = m.TotalOrdenesTrabajo.ToString("N0");
        if (_kpiPendingVb is not null) _kpiPendingVb.Text = m.OrdenesSinVistoBueno.ToString("N0");
        if (_kpiProviders is not null) _kpiProviders.Text = m.TotalProveedoresActivos.ToString("N0");
        if (_kpiInvoices is not null) _kpiInvoices.Text = m.TotalFacturas.ToString("N0");

        if (_gridRecentOts is not null)
        {
            _recentOts = _ctx.WorkOrders.Search(new WorkOrderSearchFilter()).Take(40).ToList();
            _gridRecentOts.DataSource = _recentOts.Select(r => new
            {
                N_OT = r.NumeroOt,
                Fecha = r.Fecha.ToString("dd-MM-yyyy"),
                r.Proveedor,
                r.Solicitante,
                r.Cantidad,
                r.Detalle,
                VBS = r.TieneVbs ? "Sí" : "No",
                VBJ = r.TieneVbj ? "Sí" : "No"
            }).ToList();

            if (_gridRecentOts.Columns.Count >= 8)
            {
                _gridRecentOts.Columns[0].HeaderText = "N° OT";
                _gridRecentOts.Columns[0].FillWeight = 8;
                _gridRecentOts.Columns[1].FillWeight = 10;
                _gridRecentOts.Columns[2].FillWeight = 18;
                _gridRecentOts.Columns[3].FillWeight = 12;
                _gridRecentOts.Columns[4].HeaderText = "Cant.";
                _gridRecentOts.Columns[4].FillWeight = 7;
                _gridRecentOts.Columns[5].FillWeight = 35;
                _gridRecentOts.Columns[6].FillWeight = 5;
                _gridRecentOts.Columns[7].FillWeight = 5;
            }
        }
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
            _clockTimer.Stop();
            _clockTimer.Dispose();
            _toolTip.Dispose();
            _dashboardView?.Dispose();
            _workOrdersView?.Dispose();
            _invoicesView?.Dispose();
            _maintainersView?.Dispose();
            _usersView?.Dispose();
        }

        base.Dispose(disposing);
    }
}
