using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo profesional GESTIÓN DE USUARIOS Y GRUPOS en interfaz de 3 Áreas:
/// Área 1 (Grupos con iconos, contadores, acciones y destino Drag & Drop),
/// Área 2 (Listado con CheckBoxes, filtros combinados, Date Pickers, orden descendente y origen Drag & Drop),
/// Área 3 (Barra de acciones individuales y operaciones masivas con confirmación y sincronización inmediata).
/// Acceso exclusivo para el perfil Administrador.
/// </summary>
public sealed class UsersForm : UserControl
{
    private enum EditorMode
    {
        View,
        Create,
        Edit
    }

    private sealed record GroupListEntry(
        int? IdGrupo,
        bool IsAllFilter,
        bool IsUnassignedFilter,
        string Name,
        string Subtitle,
        int Count,
        bool IsActive,
        UserGroup? GroupModel);

    private sealed record GroupComboOption(int? IdGrupo, string Label);

    private sealed record UserDragPayload(IReadOnlyList<string> UserIds);

    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private EditorMode _mode = EditorMode.View;
    private bool _suppressGridSelection;
    private bool _suppressProfileChange;
    private bool _suppressGroupSelection;
    private bool _syncSubscribed;

    private IReadOnlyList<UserGroup> _groups = [];
    private IReadOnlyList<UserProfile> _profiles = [];
    private IReadOnlyList<User> _users = [];
    private readonly HashSet<string> _checkedUserIds = new(StringComparer.OrdinalIgnoreCase);
    private User? _selectedUser;
    private UserPermissions _workingPermissions = PermissionPresets.CreateForRole(UserRoleLevel.Usuario);

    // Group filter state (Area 1)
    private int? _activeGroupFilterId;
    private bool _filterUnassignedOnly;
    private int _dragHoverGroupIndex = -1;
    private Point _dragStartPoint = Point.Empty;
    private int _dragSourceRowIndex = -1;

    // Header mode indicator
    private readonly Panel _modeStrip;
    private readonly Label _lblMode;
    private readonly Label _lblTimestamps;

    // Editor fields (DATOS DEL USUARIO)
    private readonly TextBox _txtId;
    private readonly TextBox _txtUsuario;
    private readonly TextBox _txtNombre;
    private readonly TextBox _txtApellido;
    private readonly TextBox _txtCorreo;
    private readonly TextBox _txtTelefono;
    private readonly ComboBox _cmbPerfil;
    private readonly ComboBox _cmbGrupo;
    private readonly ComboBox _cmbEstado;
    private readonly TextBox _txtPassword;
    private readonly TextBox _txtConfirmPassword;
    private readonly CheckBox _chkMostrarPass;
    private readonly CheckBox _chkSolicitante;
    private readonly CheckBox _chkForzarCambioClave;
    private readonly Button _btnPermisos;
    private readonly Button _btnUserAttachSig;
    private readonly Button _btnUserClearSig;
    private readonly Label _lblUserSigInfo;
    private readonly PictureBox _picUserSig;

    private Dictionary<string, Requester> _requestersByAlias = new(StringComparer.OrdinalIgnoreCase);
    private ProcessedSignature? _pendingUserSignature;
    private bool _clearUserSignature;

    internal TabControl MainTabs { get; }
    private readonly TabPage _tabUsers;
    private readonly TabPage _tabRequesters;
    private readonly TabPage _tabProfiles;
    internal RequesterManagerControl RequesterManager { get; }
    internal UserProfileManagerControl ProfileManager { get; }

    // Area 1: Groups panel
    internal ListBox ListGroups { get; }
    private readonly Button _btnNuevoGrupo;
    private readonly Button _btnEditarGrupo;
    private readonly Button _btnEstadoGrupo;
    private readonly Button _btnEliminarGrupo;
    private readonly Button _btnHistorialGrupos;

    // Standard & Batch buttons (exposed internally for automated UI verification)
    internal Button BtnNuevo { get; }
    internal Button BtnGuardar { get; }
    internal Button BtnEditar { get; }
    internal Button BtnActualizar { get; }
    internal Button BtnEliminar { get; }
    internal Button BtnCambiarPassword { get; }
    internal Button BtnCancelar { get; }
    internal Button BtnLimpiar { get; }
    internal Button BtnBuscar { get; }
    internal Button BtnRefrescar { get; }

    internal Button BtnActivar { get; }
    internal Button BtnDesactivar { get; }
    internal Button BtnMover { get; }
    internal Button BtnCambiarPerfil { get; }
    internal Button BtnExportar { get; }
    internal Button BtnImprimir { get; }

    // Area 2: Filters & Grid
    private readonly GroupBox _grpListado;
    private readonly TextBox _txtBuscar;
    private readonly ComboBox _cmbFiltroPerfil;
    private readonly ComboBox _cmbFiltroEstado;
    private readonly DateRangeFilterControl _dateFilter;
    private readonly Label _lblSelectedCounter;
    private readonly Label _lblGridStats;
    internal DataGridView GridUsers { get; }

    public UsersForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;

        // Validación obligatoria en backend/servicio: exclusivo Administrador
        _ctx.UserManagementService.EnsureCanAccessModule(_session);

        Name = "GestionUsuariosForm";
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        // --- MODE STRIP ---
        _modeStrip = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(8, 0, 8, 0)
        };
        _lblMode = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _modeStrip.Controls.Add(_lblMode);

        // --- EDITOR FIELDS ---
        Padding fieldMargin = new(0, 3, 8, 3);
        _txtId = CreateReadOnlyTextBox("(Automático)");
        _txtId.Margin = fieldMargin;
        _txtUsuario = new TextBox { Name = "txtUsuarioCrud", MaxLength = 30, PlaceholderText = "Ej. barriagada", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtNombre = new TextBox { Name = "txtNombreCrud", MaxLength = 60, PlaceholderText = "Nombres", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtApellido = new TextBox { Name = "txtApellidoCrud", MaxLength = 80, PlaceholderText = "Apellidos", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtCorreo = new TextBox { Name = "txtCorreoCrud", MaxLength = 100, PlaceholderText = "correo@cambiaso.cl", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtTelefono = new TextBox { Name = "txtTelefonoCrud", MaxLength = 30, PlaceholderText = "+56 9 ...", Dock = DockStyle.Fill, Margin = fieldMargin };

        _cmbPerfil = new ComboBox
        {
            Name = "cmbPerfilCrud",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = fieldMargin,
            DisplayMember = nameof(UserProfile.Nombre),
            ValueMember = nameof(UserProfile.IdNivel)
        };
        _cmbPerfil.SelectedIndexChanged += (_, _) => OnProfileChanged();

        _cmbGrupo = new ComboBox
        {
            Name = "cmbGrupoCrud",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = fieldMargin,
            DisplayMember = nameof(GroupComboOption.Label)
        };

        _cmbEstado = new ComboBox
        {
            Name = "cmbEstadoCrud",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = fieldMargin
        };
        _cmbEstado.Items.AddRange(["Activo", "Inactivo"]);
        _cmbEstado.SelectedIndex = 0;

        _txtPassword = new TextBox
        {
            Name = "txtPasswordCrud",
            UseSystemPasswordChar = true,
            PlaceholderText = "Mínimo 6 caracteres",
            Dock = DockStyle.Fill,
            Margin = fieldMargin
        };
        _txtConfirmPassword = new TextBox
        {
            Name = "txtConfirmPasswordCrud",
            UseSystemPasswordChar = true,
            PlaceholderText = "Confirmar clave",
            Dock = DockStyle.Fill,
            Margin = fieldMargin
        };

        _lblTimestamps = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Creación: —   |   Modificación: —   |   Último acceso: —",
            Margin = new Padding(0, 4, 8, 0)
        };

        _chkMostrarPass = new CheckBox
        {
            Text = "Mostrar clave",
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(0, 3, 10, 0)
        };
        _chkMostrarPass.CheckedChanged += (_, _) =>
        {
            bool hide = !_chkMostrarPass.Checked;
            _txtPassword.UseSystemPasswordChar = hide;
            _txtConfirmPassword.UseSystemPasswordChar = hide;
        };

        _chkSolicitante = new CheckBox
        {
            Text = "Solicitante OT",
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary,
            Margin = new Padding(0, 4, 8, 0)
        };
        _chkSolicitante.CheckedChanged += (_, _) => UpdateUserSignatureControls();

        _btnUserAttachSig = CambiasoTheme.CreateButton("Firma (PNG/JPG/PDF)", AppIcon.FileText, ButtonTone.Accent, _toolTip, "Adjuntar firma digital del usuario solicitante (PNG, JPG o PDF)", width: 174, height: 24);
        _btnUserAttachSig.Font = CambiasoTheme.SmallFont;
        _btnUserAttachSig.Margin = new Padding(0, 1, 6, 0);
        _btnUserAttachSig.Click += (_, _) => BrowseUserSignatureFile();

        _btnUserClearSig = CambiasoTheme.CreateButton("Quitar Firma", AppIcon.Remove, ButtonTone.Secondary, _toolTip, "Quitar firma digital del solicitante", width: 124, height: 24);
        _btnUserClearSig.Font = CambiasoTheme.SmallFont;
        _btnUserClearSig.Margin = new Padding(0, 1, 8, 0);
        _btnUserClearSig.Click += (_, _) => ClearUserEditorSignature();

        _picUserSig = new PictureBox
        {
            Size = new Size(72, 24),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 1, 6, 0)
        };
        _lblUserSigInfo = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Sin firma digital",
            Margin = new Padding(0, 4, 6, 0)
        };

        _chkForzarCambioClave = new CheckBox
        {
            Text = "Cambiar clave 1er acceso",
            AutoSize = true,
            Checked = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary,
            Margin = new Padding(0, 3, 10, 0)
        };

        _btnPermisos = CambiasoTheme.CreateButton("Permisos", AppIcon.Template, ButtonTone.Secondary, _toolTip, "Ver o personalizar permisos RBAC", width: 102, height: 24);
        _btnPermisos.Margin = new Padding(2, 0, 0, 0);
        _btnPermisos.Click += (_, _) => OpenPermissionsDialog();

        MainTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.LabelFont,
            Padding = new Point(14, 6)
        };
        _tabUsers = new TabPage("Usuarios y Grupos") { BackColor = CambiasoTheme.Surface, Padding = new Padding(4) };
        _tabRequesters = new TabPage("Solicitados Por y Firmas Digitales") { BackColor = CambiasoTheme.Surface, Padding = new Padding(4) };
        _tabProfiles = new TabPage("Perfiles de Usuarios") { BackColor = CambiasoTheme.Surface, Padding = new Padding(4) };

        RequesterManager = new RequesterManagerControl(_ctx, _session) { Dock = DockStyle.Fill };
        RequesterManager.RequestersChanged += (_, _) =>
        {
            LoadUsersGrid(_selectedUser?.IdUsuario);
            if (_selectedUser is not null && _mode == EditorMode.View)
            {
                PopulateUserSignatureFromCatalog(_selectedUser);
            }
        };
        _tabRequesters.Controls.Add(RequesterManager);

        ProfileManager = new UserProfileManagerControl(_ctx, _session) { Dock = DockStyle.Fill };
        ProfileManager.ProfilesChanged += (_, _) =>
        {
            LoadProfilesCatalog();
            LoadUsersGrid(_selectedUser?.IdUsuario);
        };
        _tabProfiles.Controls.Add(ProfileManager);

        // --- AREA 1: GROUPS PANEL ---
        ListGroups = new ListBox
        {
            Name = "lstGrupos",
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 46,
            BorderStyle = BorderStyle.None,
            BackColor = CambiasoTheme.CardBackground,
            IntegralHeight = false,
            AllowDrop = true
        };
        ListGroups.DrawItem += OnDrawGroupItem;
        ListGroups.SelectedIndexChanged += (_, _) => OnGroupFilterSelected();
        ListGroups.DoubleClick += (_, _) => EditSelectedGroup();
        ListGroups.DragOver += OnGroupsDragOver;
        ListGroups.DragLeave += (_, _) => ClearDragHover();
        ListGroups.DragDrop += OnGroupsDragDrop;

        _btnNuevoGrupo = CreateCompactButton("Nuevo", AppIcon.Add, ButtonTone.Primary, "Crear un nuevo grupo o categoría de trabajo");
        _btnNuevoGrupo.Click += (_, _) => CreateGroup();

        _btnEditarGrupo = CreateCompactButton("Editar", AppIcon.Edit, ButtonTone.Secondary, "Editar el grupo seleccionado");
        _btnEditarGrupo.Click += (_, _) => EditSelectedGroup();

        _btnEstadoGrupo = CreateCompactButton("Estado", AppIcon.Power, ButtonTone.Secondary, "Activar o desactivar el grupo seleccionado");
        _btnEstadoGrupo.Click += (_, _) => ToggleSelectedGroupActive();

        _btnEliminarGrupo = CreateCompactButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, "Eliminar grupo (solo si no tiene integrantes ni historial)");
        _btnEliminarGrupo.Click += (_, _) => DeleteSelectedGroup();

        _btnHistorialGrupos = CreateCompactButton("Historial Mov.", AppIcon.Audit, ButtonTone.Accent, "Consultar historial auditable de movimientos entre grupos");
        _btnHistorialGrupos.Click += (_, _) => OpenGroupHistoryDialog();

        ContextMenuHelper.AttachToListBox(ListGroups, idx =>
        {
            GroupListEntry? entry = idx >= 0 && idx < ListGroups.Items.Count
                ? ListGroups.Items[idx] as GroupListEntry
                : null;

            UserGroup? grp = entry?.GroupModel;
            if (grp is null)
            {
                return
                [
                    ContextMenuHelper.Item("Nuevo grupo…", AppIcon.Add, CambiasoTheme.Primary, CreateGroup)
                ];
            }

            return
            [
                ContextMenuHelper.Item("Nuevo grupo…", AppIcon.Add, CambiasoTheme.Primary, CreateGroup),
                ContextMenuHelper.Item("Editar grupo…", AppIcon.Edit, CambiasoTheme.Accent, EditSelectedGroup, enabled: _btnEditarGrupo.Enabled),
                ContextMenuHelper.Item(
                    grp.Activo ? "Desactivar grupo" : "Activar grupo",
                    grp.Activo ? AppIcon.Ban : AppIcon.Power,
                    grp.Activo ? CambiasoTheme.Warning : CambiasoTheme.Success,
                    ToggleSelectedGroupActive,
                    enabled: _btnEstadoGrupo.Enabled),
                ContextMenuHelper.Item("Historial de movimientos…", AppIcon.Audit, CambiasoTheme.Accent, OpenGroupHistoryDialog),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Eliminar grupo…", AppIcon.Delete, CambiasoTheme.Danger, DeleteSelectedGroup, enabled: _btnEliminarGrupo.Enabled)
            ];
        });

        // --- EDITOR BUTTONS ---
        BtnNuevo = CreateAutoButton("Nuevo", AppIcon.UserPlus, ButtonTone.Primary, "Registrar un nuevo usuario");
        BtnNuevo.Click += (_, _) => EnterCreateMode();

        BtnGuardar = CreateAutoButton("Guardar", AppIcon.Save, ButtonTone.Primary, "Guardar nuevo usuario en la base de datos");
        BtnGuardar.Click += (_, _) => SaveNewUser();

        BtnEditar = CreateAutoButton("Editar", AppIcon.Edit, ButtonTone.Accent, "Editar el usuario seleccionado");
        BtnEditar.Click += (_, _) => EnterEditMode();

        BtnActualizar = CreateAutoButton("Actualizar", AppIcon.Check, ButtonTone.Accent, "Confirmar cambios del usuario en edición");
        BtnActualizar.Click += (_, _) => UpdateExistingUser();

        BtnCambiarPassword = CreateAutoButton("Cambiar Contraseña", AppIcon.Key, ButtonTone.Accent, "Restablecer contraseña del usuario seleccionado");
        BtnCambiarPassword.Click += (_, _) => OpenResetPasswordDialog();

        BtnCancelar = CreateAutoButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, "Cancelar creación/edición");
        BtnCancelar.Click += (_, _) => CancelOperation();

        BtnLimpiar = CreateAutoButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, "Limpiar campos del formulario o filtros");
        BtnLimpiar.Click += (_, _) => ClearFieldsOrFilters();

        // --- AREA 3: BATCH & INDIVIDUAL ACTIONS BAR ---
        _lblSelectedCounter = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(8, 5, 8, 5),
            Margin = new Padding(0, 3, 6, 0),
            Text = "Sel: 0"
        };

        BtnActivar = CreateAutoButton("Activar", AppIcon.Power, ButtonTone.Primary, "Activar usuario(s) seleccionado(s) en lote");
        BtnActivar.Click += (_, _) => RunBatchSetActive(activate: true);

        BtnDesactivar = CreateAutoButton("Desactivar", AppIcon.Ban, ButtonTone.Secondary, "Desactivar usuario(s) seleccionado(s) en lote");
        BtnDesactivar.Click += (_, _) => RunBatchSetActive(activate: false, isDeleteAction: false);

        BtnEliminar = CreateAutoButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, "Eliminación lógica individual o por lotes (protege último Administrador)");
        BtnEliminar.Click += (_, _) => RunBatchSetActive(activate: false, isDeleteAction: true);

        BtnMover = CreateAutoButton("Mover", AppIcon.MoveRight, ButtonTone.Accent, "Mover usuario(s) seleccionado(s) a otro grupo de trabajo");
        BtnMover.Click += (_, _) => OpenMoveUsersDialog();

        BtnCambiarPerfil = CreateAutoButton("Cambiar Perfil", AppIcon.Shield, ButtonTone.Secondary, "Cambiar perfil (rol) de usuario(s) seleccionado(s) por lotes");
        BtnCambiarPerfil.Click += (_, _) => RunBatchChangeRole();

        BtnExportar = CreateAutoButton("Exportar", AppIcon.Download, ButtonTone.Accent, "Exportar listado filtrado a Excel, PDF, Word o CSV");
        BtnExportar.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            BtnExportar,
            this,
            _ctx.DocumentExportService,
            "SISPRO_Usuarios_Grupos",
            () => DocumentExportService.BuildUsersReport(_users, BuildFilterSummary()));

        BtnImprimir = CreateAutoButton("Imprimir", AppIcon.Print, ButtonTone.Secondary, "Vista previa e impresión del listado de usuarios");
        BtnImprimir.Click += (_, _) => PrintUsersList();

        BtnRefrescar = CreateAutoButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, "Sincronizar inmediatamente grupos y usuarios desde SQLite");
        BtnRefrescar.Click += (_, _) => RefreshData();

        // --- AREA 2: FILTERS & GRID ---
        _txtBuscar = new TextBox
        {
            Name = "txtBuscarUsuarios",
            Width = 86,
            PlaceholderText = "Buscar..."
        };
        _txtBuscar.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoadUsersGrid();
            }
        };

        _cmbFiltroPerfil = new ComboBox
        {
            Name = "cmbFiltroPerfil",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 104
        };
        _cmbFiltroPerfil.Items.Add("Todos");
        _cmbFiltroPerfil.SelectedIndex = 0;
        _cmbFiltroPerfil.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressProfileChange) LoadUsersGrid();
        };

        _cmbFiltroEstado = new ComboBox
        {
            Name = "cmbFiltroEstado",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 66
        };
        _cmbFiltroEstado.Items.AddRange(["Todos", "Activo", "Inactivo"]);
        _cmbFiltroEstado.SelectedIndex = 0;
        _cmbFiltroEstado.SelectedIndexChanged += (_, _) => LoadUsersGrid();

        _dateFilter = new DateRangeFilterControl(compact: true);
        _dateFilter.RangeChanged += (_, _) => LoadUsersGrid();

        BtnBuscar = CreateAutoButton("Buscar", AppIcon.Search, ButtonTone.Primary, "Ejecutar búsqueda combinada");
        BtnBuscar.Click += (_, _) => LoadUsersGrid();

        _lblGridStats = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(8, 7, 0, 0)
        };

        GridUsers = new DataGridView { Name = "gridUsuarios", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(GridUsers);
        GridUsers.ReadOnly = false;
        GridUsers.MultiSelect = true;
        GridUsers.AutoGenerateColumns = false;
        ConfigureUsersGridColumns();
        GridUsers.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (GridUsers.IsCurrentCellDirty && GridUsers.CurrentCell?.ColumnIndex == 0)
            {
                GridUsers.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        GridUsers.CellValueChanged += OnGridCellValueChanged;
        GridUsers.SelectionChanged += (_, _) => OnGridSelectionChanged();
        GridUsers.CurrentCellChanged += (_, _) => OnGridSelectionChanged();
        GridUsers.Sorted += (_, _) => OnGridSelectionChanged();
        GridUsers.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex > 0 && _selectedUser is not null)
            {
                EnterEditMode();
            }
        };
        GridUsers.CellFormatting += OnGridCellFormatting;
        GridUsers.SortCompare += OnGridSortCompare;
        GridUsers.MouseDown += OnGridMouseDown;
        GridUsers.MouseMove += OnGridMouseMove;
        ContextMenuHelper.AttachToGrid(GridUsers, idx =>
        {
            User? rowUser = GetUserAtRow(idx);
            if (rowUser is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Editar usuario", AppIcon.Edit, CambiasoTheme.Primary, EnterEditMode, enabled: BtnEditar.Enabled),
                ContextMenuHelper.Item("Activar", AppIcon.Power, CambiasoTheme.Success, () => RunBatchSetActive(activate: true), enabled: BtnActivar.Enabled && (!rowUser.Activo || _checkedUserIds.Count > 0)),
                ContextMenuHelper.Item("Desactivar", AppIcon.Ban, CambiasoTheme.Warning, () => RunBatchSetActive(activate: false, isDeleteAction: false), enabled: BtnDesactivar.Enabled && (rowUser.Activo || _checkedUserIds.Count > 0)),
                ContextMenuHelper.Item("Mover a grupo…", AppIcon.MoveRight, CambiasoTheme.Accent, OpenMoveUsersDialog, enabled: BtnMover.Enabled),
                ContextMenuHelper.Item("Cambiar perfil…", AppIcon.Shield, CambiasoTheme.Primary, RunBatchChangeRole, enabled: BtnCambiarPerfil.Enabled),
                ContextMenuHelper.Item("Restablecer contraseña…", AppIcon.Key, CambiasoTheme.Accent, OpenResetPasswordDialog, enabled: BtnCambiarPassword.Enabled),
                ContextMenuHelper.Item("Permisos…", AppIcon.Template, CambiasoTheme.Primary, OpenPermissionsDialog, enabled: _btnPermisos.Enabled),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Eliminar", AppIcon.Delete, CambiasoTheme.Danger, () => RunBatchSetActive(activate: false, isDeleteAction: true), enabled: BtnEliminar.Enabled)
            ];
        });

        _grpListado = CambiasoTheme.CreateGroupBox("ÁREA 2 — LISTADO DE USUARIOS");
        _grpListado.Dock = DockStyle.Fill;

        BuildThreeAreaLayout();
        LoadProfilesCatalog();
        SubscribeSyncEvents();
        ReloadGroupsAndUsers();
        ApplyMode(EditorMode.View);
    }

    public void RefreshData()
    {
        string? preserveId = _selectedUser?.IdUsuario;
        LoadProfilesCatalog();
        ReloadGroupsAndUsers(preserveId);
        RequesterManager.ReloadRequesters();
        ProfileManager.Reload();
    }

    public void ShowRequestersTab()
    {
        MainTabs.SelectedTab = _tabRequesters;
        RequesterManager.ReloadRequesters();
    }

    internal void AttachSignatureToUserEditor(string filePath)
    {
        ProcessedSignature sig = SignatureFileService.ProcessFile(filePath);
        _pendingUserSignature = sig;
        _clearUserSignature = false;
        _chkSolicitante.Checked = true;
        UpdateUserSignatureControls();
    }

    internal void AttachSignatureBytesToUserEditor(string fileName, byte[] rawBytes)
    {
        ProcessedSignature sig = SignatureFileService.ProcessBytes(fileName, rawBytes);
        _pendingUserSignature = sig;
        _clearUserSignature = false;
        _chkSolicitante.Checked = true;
        UpdateUserSignatureControls();
    }

    private void SubscribeSyncEvents()
    {
        if (_syncSubscribed)
        {
            return;
        }

        _ctx.SyncHub.DataChanged += OnSyncDataChanged;
        _syncSubscribed = true;
    }

    private void OnSyncDataChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & (DataChangeKind.Users | DataChangeKind.Groups | DataChangeKind.Catalogs)) == 0)
        {
            return;
        }

        void ApplyRefresh()
        {
            if (IsDisposed)
            {
                return;
            }

            // No interrumpir si el usuario está escribiendo en modo Nuevo/Edición, pero sí refrescar grupos y grilla
            string? preserve = _selectedUser?.IdUsuario;
            LoadProfilesCatalog();
            LoadGroupsPanel();
            if (_mode == EditorMode.View)
            {
                LoadUsersGrid(preserve);
            }
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke(ApplyRefresh);
        }
        else if (!InvokeRequired)
        {
            ApplyRefresh();
        }
    }

    private void ConfigureUsersGridColumns()
    {
        GridUsers.Columns.Clear();

        DataGridViewCheckBoxColumn colSel = new()
        {
            Name = "colSel",
            HeaderText = "✓",
            Width = 34,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = false
        };

        DataGridViewTextBoxColumn MakeCol(string name, string header, float weight, int minW = 36) => new()
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            MinimumWidth = minW,
            ReadOnly = true
        };

        GridUsers.Columns.AddRange(
        [
            colSel,
            MakeCol("colId", "ID", 6, 44),
            MakeCol("colUsuario", "Usuario", 10, 68),
            MakeCol("colNombre", "Nombre", 15, 96),
            MakeCol("colCorreo", "Correo", 14, 90),
            MakeCol("colTelefono", "Teléfono", 9, 64),
            MakeCol("colPerfil", "Perfil", 10, 74),
            MakeCol("colGrupo", "Grupo", 10, 72),
            MakeCol("colSolicitante", "Solicitante / Firma", 12, 92),
            MakeCol("colEstado", "Estado", 7, 56),
            MakeCol("colCreacion", "Creación", 8, 72),
            MakeCol("colUltimoAcceso", "Últ. Acceso", 8, 72)
        ]);
    }

    private void BuildThreeAreaLayout()
    {
        // Dividir en Área 1 (Izquierda: Grupos) y Panel Derecho (Editor + Área 3 Acciones + Área 2 Listado)
        TableLayoutPanel outer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(2)
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 268));
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // === ÁREA 1: GRUPOS ===
        GroupBox grpAreas1 = CambiasoTheme.CreateGroupBox("ÁREA 1 — GRUPOS DE TRABAJO");
        grpAreas1.Dock = DockStyle.Fill;

        TableLayoutPanel leftLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));

        TableLayoutPanel grpCrudRow1 = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        grpCrudRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grpCrudRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        grpCrudRow1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        _btnNuevoGrupo.Dock = _btnEditarGrupo.Dock = _btnEstadoGrupo.Dock = DockStyle.Fill;
        _btnNuevoGrupo.Margin = _btnEditarGrupo.Margin = new Padding(0, 1, 3, 2);
        _btnEstadoGrupo.Margin = new Padding(0, 1, 0, 2);
        grpCrudRow1.Controls.Add(_btnNuevoGrupo, 0, 0);
        grpCrudRow1.Controls.Add(_btnEditarGrupo, 1, 0);
        grpCrudRow1.Controls.Add(_btnEstadoGrupo, 2, 0);

        TableLayoutPanel grpCrudRow2 = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        grpCrudRow2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));
        grpCrudRow2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
        _btnEliminarGrupo.Dock = _btnHistorialGrupos.Dock = DockStyle.Fill;
        _btnEliminarGrupo.Margin = new Padding(0, 1, 3, 2);
        _btnHistorialGrupos.Margin = new Padding(0, 1, 0, 2);
        grpCrudRow2.Controls.Add(_btnEliminarGrupo, 0, 0);
        grpCrudRow2.Controls.Add(_btnHistorialGrupos, 1, 0);

        Label lblDragHint = new()
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 7.8f, FontStyle.Italic),
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Arrastre usuarios aquí para mover de grupo",
            TextAlign = ContentAlignment.MiddleCenter
        };

        leftLayout.Controls.Add(grpCrudRow1, 0, 0);
        leftLayout.Controls.Add(grpCrudRow2, 0, 1);
        leftLayout.Controls.Add(ListGroups, 0, 2);
        leftLayout.Controls.Add(lblDragHint, 0, 3);
        grpAreas1.Controls.Add(leftLayout);

        // === PANEL DERECHO (Editor + Área 3 + Área 2) ===
        TableLayoutPanel rightPanel = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(4, 0, 0, 0)
        };
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 304));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        rightPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 1. Editor DATOS DEL USUARIO
        GroupBox grpDatos = CambiasoTheme.CreateGroupBox("DATOS DEL USUARIO Y SOLICITANTE");
        grpDatos.Dock = DockStyle.Fill;

        TableLayoutPanel topContainer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        topContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        topContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        topContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        TableLayoutPanel fieldsGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 6,
            Padding = new Padding(2, 4, 2, 0)
        };
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        for (int r = 0; r < 4; r++)
        {
            fieldsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        }
        fieldsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fieldsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        // Row 0: ID | Usuario | Perfil
        fieldsGrid.Controls.Add(FieldLabel("ID:"), 0, 0);
        fieldsGrid.Controls.Add(_txtId, 1, 0);
        fieldsGrid.Controls.Add(FieldLabel("Usuario *:"), 2, 0);
        fieldsGrid.Controls.Add(_txtUsuario, 3, 0);
        fieldsGrid.Controls.Add(FieldLabel("Perfil *:"), 4, 0);
        fieldsGrid.Controls.Add(_cmbPerfil, 5, 0);

        // Row 1: Nombre | Apellido | Grupo
        fieldsGrid.Controls.Add(FieldLabel("Nombre *:"), 0, 1);
        fieldsGrid.Controls.Add(_txtNombre, 1, 1);
        fieldsGrid.Controls.Add(FieldLabel("Apellido *:"), 2, 1);
        fieldsGrid.Controls.Add(_txtApellido, 3, 1);
        fieldsGrid.Controls.Add(FieldLabel("Grupo:"), 4, 1);
        fieldsGrid.Controls.Add(_cmbGrupo, 5, 1);

        // Row 2: Correo | Teléfono | Estado
        fieldsGrid.Controls.Add(FieldLabel("Correo:"), 0, 2);
        fieldsGrid.Controls.Add(_txtCorreo, 1, 2);
        fieldsGrid.Controls.Add(FieldLabel("Teléfono:"), 2, 2);
        fieldsGrid.Controls.Add(_txtTelefono, 3, 2);
        fieldsGrid.Controls.Add(FieldLabel("Estado *:"), 4, 2);
        fieldsGrid.Controls.Add(_cmbEstado, 5, 2);

        // Row 3: Contraseña | Confirmar | Opciones clave
        fieldsGrid.Controls.Add(FieldLabel("Contraseña:"), 0, 3);
        fieldsGrid.Controls.Add(_txtPassword, 1, 3);
        fieldsGrid.Controls.Add(FieldLabel("Confirmar clave:"), 2, 3);
        fieldsGrid.Controls.Add(_txtConfirmPassword, 3, 3);
        FlowLayoutPanel passOpts = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        passOpts.Controls.AddRange([_chkMostrarPass, _btnPermisos]);
        fieldsGrid.Controls.Add(passOpts, 4, 3);
        fieldsGrid.SetColumnSpan(passOpts, 2);

        // Row 4: Solicitante OT + Firma digital PNG/JPG/PDF
        FlowLayoutPanel sigRow = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        sigRow.Controls.AddRange([_chkSolicitante, _btnUserAttachSig, _btnUserClearSig, _picUserSig, _lblUserSigInfo]);
        fieldsGrid.Controls.Add(sigRow, 0, 4);
        fieldsGrid.SetColumnSpan(sigRow, 6);

        // Row 5: Forzar clave + Timestamps
        FlowLayoutPanel metaRow = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        metaRow.Controls.AddRange([_chkForzarCambioClave, _lblTimestamps]);
        fieldsGrid.Controls.Add(metaRow, 0, 5);
        fieldsGrid.SetColumnSpan(metaRow, 6);

        FlowLayoutPanel editorButtons = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 3, 0, 0)
        };
        editorButtons.Controls.AddRange([BtnNuevo, BtnGuardar, BtnEditar, BtnActualizar, BtnCambiarPassword, BtnCancelar, BtnLimpiar]);

        topContainer.Controls.Add(_modeStrip, 0, 0);
        topContainer.Controls.Add(fieldsGrid, 0, 1);
        topContainer.Controls.Add(editorButtons, 0, 2);
        grpDatos.Controls.Add(topContainer);

        // 2. ÁREA 3: BARRA DE ACCIONES INDIVIDUALES Y MASIVAS
        Panel area3Card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(8, 8, 8, 6)
        };
        area3Card.Paint += (_, e) =>
        {
            using Pen p = new(CambiasoTheme.BorderColor);
            e.Graphics.DrawRectangle(p, 0, 0, area3Card.Width - 1, area3Card.Height - 1);
        };

        FlowLayoutPanel area3Flow = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false
        };

        Button btnSelectAll = CreateAutoButton("Todos", AppIcon.Check, ButtonTone.Secondary, "Seleccionar todos los usuarios visibles");
        btnSelectAll.Click += (_, _) => SetAllVisibleChecked(true);

        Button btnSelectNone = CreateAutoButton("Ninguno", AppIcon.Clear, ButtonTone.Secondary, "Desmarcar toda la selección");
        btnSelectNone.Click += (_, _) => SetAllVisibleChecked(false);

        area3Flow.Controls.AddRange(
        [
            _lblSelectedCounter,
            btnSelectAll,
            btnSelectNone,
            BtnActivar,
            BtnDesactivar,
            BtnEliminar,
            BtnMover,
            BtnCambiarPerfil
        ]);
        area3Card.Controls.Add(area3Flow);

        // 3. ÁREA 2: LISTADO Y FILTROS COMBINADOS
        TableLayoutPanel bottomContainer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        bottomContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        bottomContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        bottomContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel filterRow1 = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        filterRow1.Controls.AddRange(
        [
            WrapFilter("Buscar:", _txtBuscar),
            WrapFilter("Perfil:", _cmbFiltroPerfil),
            WrapFilter("Estado:", _cmbFiltroEstado),
            BtnBuscar,
            BtnExportar,
            BtnImprimir,
            BtnRefrescar
        ]);

        FlowLayoutPanel filterRow2 = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 1, 0, 0) };
        filterRow2.Controls.AddRange([_dateFilter, _lblGridStats]);

        bottomContainer.Controls.Add(filterRow1, 0, 0);
        bottomContainer.Controls.Add(filterRow2, 0, 1);
        bottomContainer.Controls.Add(GridUsers, 0, 2);
        _grpListado.Controls.Add(bottomContainer);

        rightPanel.Controls.Add(grpDatos, 0, 0);
        rightPanel.Controls.Add(area3Card, 0, 1);
        rightPanel.Controls.Add(_grpListado, 0, 2);

        outer.Controls.Add(grpAreas1, 0, 0);
        outer.Controls.Add(rightPanel, 1, 0);
        _tabUsers.Controls.Add(outer);
        MainTabs.TabPages.AddRange([_tabUsers, _tabRequesters, _tabProfiles]);
        Controls.Add(MainTabs);
    }

    private void LoadProfilesCatalog()
    {
        _suppressProfileChange = true;
        try
        {
            int? selectedEditorId = _cmbPerfil.SelectedValue is int ev ? ev : (_cmbPerfil.SelectedItem as UserProfile)?.IdNivel;
            int? selectedFilterId = (_cmbFiltroPerfil.SelectedItem as UserProfile)?.IdNivel;

            _profiles = _ctx.UserProfileService.GetAll(_session, includeInactive: true);
            List<UserProfile> activeProfiles = _profiles.Where(p => p.Activo).ToList();
            if (activeProfiles.Count == 0)
            {
                activeProfiles = _profiles.ToList();
            }

            _cmbPerfil.DataSource = null;
            _cmbPerfil.DisplayMember = nameof(UserProfile.Nombre);
            _cmbPerfil.ValueMember = nameof(UserProfile.IdNivel);
            _cmbPerfil.DataSource = activeProfiles;
            if (selectedEditorId.HasValue && activeProfiles.Any(p => p.IdNivel == selectedEditorId.Value))
            {
                _cmbPerfil.SelectedValue = selectedEditorId.Value;
            }

            _cmbFiltroPerfil.Items.Clear();
            _cmbFiltroPerfil.Items.Add("Todos");
            foreach (UserProfile prof in _profiles)
            {
                _cmbFiltroPerfil.Items.Add(prof);
            }

            if (selectedFilterId.HasValue)
            {
                int matchIdx = _profiles.ToList().FindIndex(p => p.IdNivel == selectedFilterId.Value);
                _cmbFiltroPerfil.SelectedIndex = matchIdx >= 0 ? matchIdx + 1 : 0;
            }
            else
            {
                _cmbFiltroPerfil.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressProfileChange = false;
        }
    }

    private void ReloadGroupsAndUsers(string? preserveUserId = null)
    {
        LoadGroupsPanel();
        LoadUsersGrid(preserveUserId);
    }

    private void LoadGroupsPanel()
    {
        _suppressGroupSelection = true;
        try
        {
            _groups = _ctx.GroupManagementService.GetAll(_session, includeInactive: true);
            int totalUsers = _ctx.UserManagementService.Search(_session, new UserSearchFilter()).Count;
            int unassignedCount = _ctx.UserManagementService.CountWithoutGroup(_session);

            List<GroupListEntry> entries =
            [
                new GroupListEntry(null, IsAllFilter: true, IsUnassignedFilter: false, "TODOS LOS USUARIOS", "Ver todo el directorio", totalUsers, true, null),
                new GroupListEntry(null, IsAllFilter: false, IsUnassignedFilter: true, "SIN GRUPO", "Pendientes de asignar", unassignedCount, true, null)
            ];

            foreach (UserGroup g in _groups)
            {
                string sub = !g.Activo
                    ? "Inactivo"
                    : (!string.IsNullOrWhiteSpace(g.AliasResponsable) ? $"Resp: {g.AliasResponsable}" : g.Descripcion);
                entries.Add(new GroupListEntry(g.IdGrupo, false, false, g.Nombre, sub, g.CantidadIntegrantes, g.Activo, g));
            }

            ListGroups.BeginUpdate();
            ListGroups.Items.Clear();
            foreach (GroupListEntry entry in entries)
            {
                ListGroups.Items.Add(entry);
            }

            int idxToSelect = 0;
            if (_filterUnassignedOnly)
            {
                idxToSelect = 1;
            }
            else if (_activeGroupFilterId.HasValue)
            {
                int found = entries.FindIndex(e => e.IdGrupo == _activeGroupFilterId.Value);
                if (found >= 0) idxToSelect = found;
            }

            if (ListGroups.Items.Count > idxToSelect)
            {
                ListGroups.SelectedIndex = idxToSelect;
            }
            ListGroups.EndUpdate();

            // Actualizar ComboBox de grupo en editor
            int? prevEditorGroup = (_cmbGrupo.SelectedItem as GroupComboOption)?.IdGrupo;
            List<GroupComboOption> comboItems = [new GroupComboOption(null, "(Sin grupo)")];
            foreach (UserGroup g in _groups)
            {
                string suffix = g.Activo ? string.Empty : " (Inactivo)";
                comboItems.Add(new GroupComboOption(g.IdGrupo, $"{g.Nombre}{suffix}"));
            }
            _cmbGrupo.DataSource = comboItems;
            SelectGroupInCombo(_selectedUser?.IdGrupo ?? prevEditorGroup);
            UpdateGroupToolbarButtons();
        }
        finally
        {
            _suppressGroupSelection = false;
        }
    }

    private void OnGroupFilterSelected()
    {
        if (_suppressGroupSelection || ListGroups.SelectedItem is not GroupListEntry entry)
        {
            return;
        }

        _activeGroupFilterId = entry.IdGrupo;
        _filterUnassignedOnly = entry.IsUnassignedFilter;
        UpdateGroupToolbarButtons();
        LoadUsersGrid();
    }

    private void UpdateGroupToolbarButtons()
    {
        UserGroup? selGroup = (ListGroups.SelectedItem as GroupListEntry)?.GroupModel;
        bool hasGroup = selGroup is not null;
        _btnEditarGrupo.Enabled = hasGroup;
        _btnEstadoGrupo.Enabled = hasGroup;
        _btnEliminarGrupo.Enabled = hasGroup;
        if (selGroup is not null)
        {
            _toolTip.SetToolTip(_btnEstadoGrupo, selGroup.Activo ? $"Desactivar grupo '{selGroup.Nombre}'" : $"Activar grupo '{selGroup.Nombre}'");
        }
    }

    private void CreateGroup()
    {
        using GroupEditorDialog dlg = new(_ctx.GroupManagementService, _ctx.UserManagementService, _session);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _activeGroupFilterId = dlg.SavedGroupId;
            _filterUnassignedOnly = false;
            ReloadGroupsAndUsers();
        }
    }

    private void EditSelectedGroup()
    {
        if (ListGroups.SelectedItem is not GroupListEntry { GroupModel: UserGroup grp })
        {
            return;
        }

        UserGroup? fresh = _ctx.GroupManagementService.GetById(_session, grp.IdGrupo);
        if (fresh is null)
        {
            ReloadGroupsAndUsers();
            return;
        }

        using GroupEditorDialog dlg = new(_ctx.GroupManagementService, _ctx.UserManagementService, _session, fresh);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            ReloadGroupsAndUsers(_selectedUser?.IdUsuario);
        }
    }

    private void ToggleSelectedGroupActive()
    {
        if (ListGroups.SelectedItem is not GroupListEntry { GroupModel: UserGroup grp })
        {
            return;
        }

        bool targetState = !grp.Activo;
        string action = targetState ? "activar" : "desactivar";
        if (!CambiasoTheme.Confirm(this, $"¿Desea {action} el grupo '{grp.Nombre}'?"))
        {
            return;
        }

        try
        {
            _ctx.GroupManagementService.SetGroupActive(_session, grp.IdGrupo, targetState);
            ReloadGroupsAndUsers(_selectedUser?.IdUsuario);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void DeleteSelectedGroup()
    {
        if (ListGroups.SelectedItem is not GroupListEntry { GroupModel: UserGroup grp })
        {
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Desea eliminar el grupo '{grp.Nombre}'?\n\nSolo se permitirá si no tiene integrantes ni historial de movimientos."))
        {
            return;
        }

        try
        {
            _ctx.GroupManagementService.DeleteGroup(_session, grp.IdGrupo);
            _activeGroupFilterId = null;
            _filterUnassignedOnly = false;
            ReloadGroupsAndUsers();
            CambiasoTheme.ShowInfo(this, $"Grupo '{grp.Nombre}' eliminado correctamente.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void OpenGroupHistoryDialog()
    {
        using GroupHistoryDialog dlg = new(_ctx.GroupManagementService, _ctx.DocumentExportService, _session, _activeGroupFilterId);
        dlg.ShowDialog(this);
    }

    // --- DRAG & DROP ENTRE GRILLA (ÁREA 2) Y GRUPOS (ÁREA 1) ---
    private void OnGridMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            _dragStartPoint = Point.Empty;
            _dragSourceRowIndex = -1;
            return;
        }

        DataGridView.HitTestInfo hit = GridUsers.HitTest(e.X, e.Y);
        if (hit.Type == DataGridViewHitTestType.Cell && hit.RowIndex >= 0 && hit.ColumnIndex > 0)
        {
            _dragStartPoint = e.Location;
            _dragSourceRowIndex = hit.RowIndex;
        }
        else
        {
            _dragStartPoint = Point.Empty;
            _dragSourceRowIndex = -1;
        }
    }

    private void OnGridMouseMove(object? sender, MouseEventArgs e)
    {
        if ((e.Button & MouseButtons.Left) != MouseButtons.Left || _dragStartPoint == Point.Empty || _dragSourceRowIndex < 0)
        {
            return;
        }

        Size dragSize = SystemInformation.DragSize;
        Rectangle dragBox = new(
            _dragStartPoint.X - (dragSize.Width / 2),
            _dragStartPoint.Y - (dragSize.Height / 2),
            dragSize.Width,
            dragSize.Height);

        if (dragBox.Contains(e.Location))
        {
            return;
        }

        _dragStartPoint = Point.Empty;
        User? draggedUser = GetUserAtRow(_dragSourceRowIndex);
        if (draggedUser is null)
        {
            return;
        }

        List<string> ids = _checkedUserIds.Count > 0 && _checkedUserIds.Contains(draggedUser.IdUsuario)
            ? [.. _checkedUserIds]
            : [draggedUser.IdUsuario];

        GridUsers.DoDragDrop(new UserDragPayload(ids), DragDropEffects.Move);
    }

    private void OnGroupsDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(typeof(UserDragPayload)) is not UserDragPayload)
        {
            e.Effect = DragDropEffects.None;
            return;
        }

        Point pt = ListGroups.PointToClient(new Point(e.X, e.Y));
        int idx = ListGroups.IndexFromPoint(pt);
        if (idx >= 0 && idx < ListGroups.Items.Count && ListGroups.Items[idx] is GroupListEntry entry && !entry.IsAllFilter && entry.IsActive)
        {
            e.Effect = DragDropEffects.Move;
            if (_dragHoverGroupIndex != idx)
            {
                _dragHoverGroupIndex = idx;
                ListGroups.Invalidate();
            }
        }
        else
        {
            e.Effect = DragDropEffects.None;
            ClearDragHover();
        }
    }

    private void OnGroupsDragDrop(object? sender, DragEventArgs e)
    {
        int dropIdx = _dragHoverGroupIndex;
        ClearDragHover();

        if (e.Data?.GetData(typeof(UserDragPayload)) is not UserDragPayload payload || dropIdx < 0 || dropIdx >= ListGroups.Items.Count)
        {
            return;
        }

        if (ListGroups.Items[dropIdx] is not GroupListEntry targetEntry || targetEntry.IsAllFilter || !targetEntry.IsActive)
        {
            return;
        }

        List<User> targetUsers = payload.UserIds
            .Select(id => _ctx.UserManagementService.GetById(_session, id))
            .Where(u => u is not null)
            .Cast<User>()
            .ToList();

        if (targetUsers.Count == 0)
        {
            return;
        }

        string destLabel = targetEntry.IsUnassignedFilter ? "Sin grupo asignado" : targetEntry.Name;
        string names = string.Join(", ", targetUsers.Take(5).Select(u => u.AliasUsuario));
        if (targetUsers.Count > 5) names += $" y {targetUsers.Count - 5} más";

        if (!CambiasoTheme.Confirm(this, $"¿Mover {targetUsers.Count} usuario(s) [{names}] al grupo '{destLabel}'?\n\nSe preservará todo su historial de órdenes y operaciones."))
        {
            return;
        }

        try
        {
            BatchOperationResult result = _ctx.UserManagementService.MoveUsersToGroup(
                _session,
                payload.UserIds,
                targetEntry.IsUnassignedFilter ? null : targetEntry.IdGrupo,
                "Movimiento mediante Drag & Drop");

            _checkedUserIds.Clear();
            ReloadGroupsAndUsers(_selectedUser?.IdUsuario);
            CambiasoTheme.ShowInfo(this, result.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void ClearDragHover()
    {
        if (_dragHoverGroupIndex != -1)
        {
            _dragHoverGroupIndex = -1;
            ListGroups.Invalidate();
        }
    }

    private void OnDrawGroupItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= ListGroups.Items.Count || ListGroups.Items[e.Index] is not GroupListEntry item)
        {
            return;
        }

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        bool isDropHover = e.Index == _dragHoverGroupIndex;

        Color bg = isDropHover
            ? Color.FromArgb(207, 238, 250)
            : (selected ? CambiasoTheme.SubtleFill : CambiasoTheme.CardBackground);
        using (SolidBrush br = new(bg))
        {
            g.FillRectangle(br, e.Bounds);
        }

        if (selected || isDropHover)
        {
            using SolidBrush accent = new(isDropHover ? CambiasoTheme.Accent : CambiasoTheme.Primary);
            g.FillRectangle(accent, e.Bounds.X, e.Bounds.Y + 2, 4, e.Bounds.Height - 4);
        }

        if (isDropHover)
        {
            using Pen dash = new(CambiasoTheme.Accent, 1.5f) { DashStyle = DashStyle.Dash };
            g.DrawRectangle(dash, e.Bounds.X + 4, e.Bounds.Y + 2, e.Bounds.Width - 7, e.Bounds.Height - 5);
        }

        // Ícono por categoría
        AppIcon icon = ResolveGroupIcon(item);
        Color iconColor = !item.IsActive
            ? CambiasoTheme.TextMuted
            : (selected ? CambiasoTheme.Primary : CambiasoTheme.Accent);
        Bitmap bmp = IconCatalog.Get(icon, iconColor, 18);
        g.DrawImage(bmp, e.Bounds.X + 12, e.Bounds.Y + 14, 18, 18);

        // Badge de contador
        string badgeText = item.Count.ToString(CultureInfo.InvariantCulture);
        int badgeW = Math.Max(26, (badgeText.Length * 8) + 12);
        Rectangle badgeRect = new(e.Bounds.Right - badgeW - 8, e.Bounds.Y + 12, badgeW, 20);
        using (SolidBrush pillBrush = new(selected ? CambiasoTheme.Primary : CambiasoTheme.SubtleFill))
        {
            g.FillRectangle(pillBrush, badgeRect);
        }
        TextRenderer.DrawText(
            g,
            badgeText,
            CambiasoTheme.SmallFont,
            badgeRect,
            selected ? Color.White : CambiasoTheme.Primary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        // Texto Nombre + Subtítulo
        int textW = Math.Max(40, badgeRect.Left - (e.Bounds.X + 38) - 4);
        Rectangle nameRect = new(e.Bounds.X + 38, e.Bounds.Y + 6, textW, 18);
        Rectangle subRect = new(e.Bounds.X + 38, e.Bounds.Y + 24, textW, 16);

        TextRenderer.DrawText(
            g,
            item.Name,
            CambiasoTheme.LabelFont,
            nameRect,
            item.IsActive ? CambiasoTheme.TextPrimary : CambiasoTheme.TextMuted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        string subText = isDropHover ? "Soltar aquí para mover" : item.Subtitle;
        TextRenderer.DrawText(
            g,
            subText,
            new Font("Segoe UI", 7.8f, item.IsActive ? FontStyle.Regular : FontStyle.Italic),
            subRect,
            isDropHover ? CambiasoTheme.Accent : (!item.IsActive ? CambiasoTheme.Danger : CambiasoTheme.TextMuted),
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        using Pen sep = new(Color.FromArgb(230, 238, 244));
        g.DrawLine(sep, e.Bounds.X + 8, e.Bounds.Bottom - 1, e.Bounds.Right - 8, e.Bounds.Bottom - 1);
    }

    private static AppIcon ResolveGroupIcon(GroupListEntry item)
    {
        if (item.IsAllFilter) return AppIcon.Users;
        if (item.IsUnassignedFilter) return AppIcon.Layers;

        string upper = item.Name.ToUpperInvariant();
        if (upper.Contains("MEC")) return AppIcon.Wrench;
        if (upper.Contains("ELECTRÓN") || upper.Contains("ELECTRON")) return AppIcon.Cpu;
        if (upper.Contains("ELÉCT") || upper.Contains("ELECT")) return AppIcon.Zap;
        if (upper.Contains("EXTER")) return AppIcon.Globe;
        if (upper.Contains("OPERA")) return AppIcon.Gauge;
        return AppIcon.Layers;
    }

    // --- OPERACIONES INDIVIDUALES Y MASIVAS (ÁREA 3) ---
    private List<string> GetTargetUserIdsForAction()
    {
        if (_checkedUserIds.Count > 0)
        {
            return [.. _checkedUserIds];
        }

        return _selectedUser is not null ? [_selectedUser.IdUsuario] : [];
    }

    private void OpenMoveUsersDialog()
    {
        List<string> ids = GetTargetUserIdsForAction();
        if (ids.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione al menos un usuario (mediante CheckBox o fila activa) para mover de grupo.");
            return;
        }

        List<User> selectedUsers = ids
            .Select(id => _ctx.UserManagementService.GetById(_session, id))
            .Where(u => u is not null)
            .Cast<User>()
            .ToList();

        using GroupMovementDialog dlg = new(selectedUsers, _groups, _activeGroupFilterId);
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            BatchOperationResult result = _ctx.UserManagementService.MoveUsersToGroup(
                _session,
                ids,
                dlg.SelectedTargetGroupId,
                dlg.Motivo);

            _checkedUserIds.Clear();
            ReloadGroupsAndUsers(_selectedUser?.IdUsuario);
            CambiasoTheme.ShowInfo(this, result.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void RunBatchSetActive(bool activate, bool isDeleteAction = false)
    {
        List<string> ids = GetTargetUserIdsForAction();
        if (ids.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione al menos un usuario para ejecutar esta operación.");
            return;
        }

        (IReadOnlyList<User> affected, IReadOnlyList<(User User, string Reason)> protectedUsers) =
            _ctx.UserManagementService.PreviewBatchImpact(_session, ids, checkAdminDeactivationOrDemotion: !activate);

        string title = activate
            ? "Activar Usuarios"
            : (isDeleteAction ? "Eliminación Lógica de Usuarios" : "Desactivar Usuarios");

        string warning = activate
            ? "Los usuarios seleccionados volverán a quedar habilitados para iniciar sesión."
            : "Se aplicará eliminación lógica (desactivación). Los registros históricos, órdenes de trabajo y auditoría se preservarán íntegros.";

        using BatchConfirmDialog dlg = new(
            title,
            warning,
            ids.Count,
            affected,
            protectedUsers,
            requireRoleSelection: false,
            isDestructive: !activate);

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            BatchOperationResult result = _ctx.UserManagementService.BatchSetActive(_session, ids, activate, dlg.Motivo);
            _checkedUserIds.Clear();
            ApplyMode(EditorMode.View);
            ReloadGroupsAndUsers(_selectedUser?.IdUsuario);
            CambiasoTheme.ShowInfo(this, result.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void RunBatchChangeRole()
    {
        List<string> ids = GetTargetUserIdsForAction();
        if (ids.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione al menos un usuario para cambiar su perfil.");
            return;
        }

        (IReadOnlyList<User> affected, IReadOnlyList<(User User, string Reason)> protectedUsers) =
            _ctx.UserManagementService.PreviewBatchImpact(_session, ids, checkAdminDeactivationOrDemotion: true);

        using BatchConfirmDialog dlg = new(
            "Cambiar Perfil por Lotes",
            "Los permisos RBAC de cada usuario elegible se ajustarán automáticamente al nuevo perfil seleccionado.",
            ids.Count,
            affected,
            protectedUsers,
            requireRoleSelection: true,
            isDestructive: false,
            availableProfiles: _profiles);

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            BatchOperationResult result = _ctx.UserManagementService.BatchChangeProfile(_session, ids, dlg.SelectedProfileId, dlg.Motivo);
            _checkedUserIds.Clear();
            ReloadGroupsAndUsers(_selectedUser?.IdUsuario);
            ProfileManager.Reload();
            CambiasoTheme.ShowInfo(this, result.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void SetAllVisibleChecked(bool isChecked)
    {
        _suppressGridSelection = true;
        try
        {
            _checkedUserIds.Clear();
            for (int i = 0; i < GridUsers.Rows.Count; i++)
            {
                GridUsers.Rows[i].Cells[0].Value = isChecked;
                if (isChecked && GetUserAtRow(i) is User rowUser)
                {
                    _checkedUserIds.Add(rowUser.IdUsuario);
                }
            }
        }
        finally
        {
            _suppressGridSelection = false;
        }

        UpdateSelectionBadgeAndBatchButtons();
    }

    private void OnGridCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressGridSelection || e.RowIndex < 0 || e.ColumnIndex != 0)
        {
            return;
        }

        User? rowUser = GetUserAtRow(e.RowIndex);
        if (rowUser is null)
        {
            return;
        }

        bool isChecked = Convert.ToBoolean(GridUsers.Rows[e.RowIndex].Cells[0].Value ?? false);
        string id = rowUser.IdUsuario;
        if (isChecked) _checkedUserIds.Add(id);
        else _checkedUserIds.Remove(id);

        UpdateSelectionBadgeAndBatchButtons();
    }

    private void UpdateSelectionBadgeAndBatchButtons()
    {
        int count = _checkedUserIds.Count;
        _lblSelectedCounter.Text = $"Sel: {count}";
        _lblSelectedCounter.BackColor = count > 0 ? CambiasoTheme.Accent : CambiasoTheme.SubtleFill;
        _lblSelectedCounter.ForeColor = count > 0 ? Color.White : CambiasoTheme.Primary;

        bool hasTarget = count > 0 || _selectedUser is not null;
        bool inView = _mode == EditorMode.View;
        BtnActivar.Enabled = inView && hasTarget;
        BtnDesactivar.Enabled = inView && hasTarget;
        BtnEliminar.Enabled = inView && hasTarget;
        BtnMover.Enabled = inView && hasTarget;
        BtnCambiarPerfil.Enabled = inView && hasTarget;
    }

    // --- EDITOR CRUD ---
    private void ApplyMode(EditorMode mode)
    {
        _mode = mode;
        bool hasSelection = _selectedUser is not null;
        bool isSelf = _selectedUser is not null &&
                      string.Equals(_selectedUser.IdUsuario, _session.Usuario.IdUsuario, StringComparison.OrdinalIgnoreCase);

        bool editable = mode is EditorMode.Create or EditorMode.Edit;
        bool isCreate = mode == EditorMode.Create;

        _txtUsuario.ReadOnly = !editable;
        _txtNombre.ReadOnly = !editable;
        _txtApellido.ReadOnly = !editable;
        _txtCorreo.ReadOnly = !editable;
        _txtTelefono.ReadOnly = !editable;

        _cmbPerfil.Enabled = editable && !(mode == EditorMode.Edit && isSelf);
        _cmbGrupo.Enabled = editable;
        _cmbEstado.Enabled = editable && !(mode == EditorMode.Edit && isSelf);

        _txtPassword.Enabled = isCreate;
        _txtConfirmPassword.Enabled = isCreate;
        _txtPassword.PlaceholderText = isCreate ? "Mínimo 6 caracteres" : "Use Cambiar Contraseña";
        _txtConfirmPassword.PlaceholderText = isCreate ? "Confirmar clave" : "Use Cambiar Contraseña";
        _chkMostrarPass.Enabled = isCreate;
        _chkSolicitante.Enabled = editable;
        _chkForzarCambioClave.Enabled = isCreate;
        _btnPermisos.Enabled = isCreate || hasSelection;
        UpdateUserSignatureControls();

        BtnNuevo.Enabled = mode == EditorMode.View;
        BtnGuardar.Enabled = mode == EditorMode.Create;
        BtnEditar.Enabled = mode == EditorMode.View && hasSelection;
        BtnActualizar.Enabled = mode == EditorMode.Edit;
        BtnCambiarPassword.Enabled = (mode == EditorMode.View || mode == EditorMode.Edit) && hasSelection;
        BtnCancelar.Enabled = editable;
        BtnLimpiar.Enabled = true;

        UpdateSelectionBadgeAndBatchButtons();

        switch (mode)
        {
            case EditorMode.Create:
                _modeStrip.BackColor = Color.FromArgb(224, 244, 252);
                _lblMode.ForeColor = CambiasoTheme.Primary;
                _lblMode.Text = "MODO: NUEVO USUARIO — Complete los campos obligatorios (*) y presione Guardar.";
                break;
            case EditorMode.Edit:
                _modeStrip.BackColor = Color.FromArgb(254, 243, 199);
                _lblMode.ForeColor = Color.FromArgb(146, 64, 14);
                _lblMode.Text = $"MODO: EDITANDO '{_selectedUser?.AliasUsuario}' (ID {_selectedUser?.CodigoId}, v{_selectedUser?.RowVersion}) — Presione Actualizar para guardar.";
                break;
            default:
                _modeStrip.BackColor = CambiasoTheme.SubtleFill;
                _lblMode.ForeColor = CambiasoTheme.Primary;
                _lblMode.Text = hasSelection
                    ? $"MODO: CONSULTA — '{_selectedUser!.AliasUsuario}' ({_selectedUser.PerfilDisplay} | {(string.IsNullOrWhiteSpace(_selectedUser.NombreGrupo) ? "Sin grupo" : _selectedUser.NombreGrupo)} | {(_selectedUser.Activo ? "Activo" : "Inactivo")})."
                    : "MODO: CONSULTA — Seleccione usuarios en la grilla, arrástrelos a un grupo o presione Nuevo.";
                break;
        }
    }

    internal void EnterCreateMode()
    {
        _selectedUser = null;
        _txtId.Text = "(Automático)";
        _txtUsuario.Clear();
        _txtNombre.Clear();
        _txtApellido.Clear();
        _txtCorreo.Clear();
        _txtTelefono.Clear();
        _txtPassword.Clear();
        _txtConfirmPassword.Clear();
        _lblTimestamps.Text = "Creación: (Automática)   |   Modificación: (Automática)   |   Último acceso: Sin accesos";

        _suppressProfileChange = true;
        if (_cmbPerfil.Items.Count > 0)
        {
            _cmbPerfil.SelectedValue = (int)UserRoleLevel.Usuario;
            if (_cmbPerfil.SelectedIndex < 0) _cmbPerfil.SelectedIndex = 0;
        }
        _cmbEstado.SelectedIndex = 0;
        SelectGroupInCombo(_activeGroupFilterId);
        _suppressProfileChange = false;

        _chkSolicitante.Checked = false;
        _chkForzarCambioClave.Checked = true;
        _pendingUserSignature = null;
        _clearUserSignature = false;
        UserProfile? initialProfile = _cmbPerfil.SelectedItem as UserProfile;
        _workingPermissions = initialProfile is not null
            ? new UserPermissions
            {
                MenuOrdenesTrabajo = initialProfile.PermisosPorDefecto.MenuOrdenesTrabajo,
                IngresoOt = initialProfile.PermisosPorDefecto.IngresoOt,
                BusquedaOt = initialProfile.PermisosPorDefecto.BusquedaOt,
                ImpresionOt = initialProfile.PermisosPorDefecto.ImpresionOt,
                MenuMantenedor = initialProfile.PermisosPorDefecto.MenuMantenedor,
                MenuUsuarios = initialProfile.PermisosPorDefecto.MenuUsuarios,
                IngresoUsuarios = initialProfile.PermisosPorDefecto.IngresoUsuarios,
                ListadoUsuarios = initialProfile.PermisosPorDefecto.ListadoUsuarios
            }
            : PermissionPresets.CreateForRole(UserRoleLevel.Usuario);

        ApplyMode(EditorMode.Create);
        UpdateUserSignatureControls();
        _txtUsuario.Focus();
    }

    internal void EnterEditMode()
    {
        if (_selectedUser is null)
        {
            CambiasoTheme.ShowWarning(this, "Debe seleccionar un usuario del listado para editar.");
            return;
        }

        // Recargar versión fresca desde BD para control de concurrencia optimista
        User? fresh = _ctx.UserManagementService.GetById(_session, _selectedUser.IdUsuario);
        if (fresh is not null)
        {
            _selectedUser = fresh;
        }

        PopulateEditorFromUser(_selectedUser);
        ApplyMode(EditorMode.Edit);
        _txtNombre.Focus();
    }

    private void SaveNewUser()
    {
        try
        {
            User user = BuildUserFromEditor(existingId: null);
            int assignedId = _ctx.UserManagementService.CreateUser(_session, user, _txtPassword.Text, _txtConfirmPassword.Text, _pendingUserSignature);
            _pendingUserSignature = null;
            _clearUserSignature = false;
            CambiasoTheme.ShowInfo(this, $"Usuario '{user.AliasUsuario}' (ID {assignedId}) creado correctamente.");
            ApplyMode(EditorMode.View);
            ReloadGroupsAndUsers(user.IdUsuario);
            RequesterManager.ReloadRequesters();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void UpdateExistingUser()
    {
        if (_selectedUser is null)
        {
            return;
        }

        try
        {
            User user = BuildUserFromEditor(existingId: _selectedUser.IdUsuario);
            _ctx.UserManagementService.UpdateUser(
                _session,
                user,
                expectedRowVersion: _selectedUser.RowVersion,
                requesterSignature: _pendingUserSignature,
                clearRequesterSignature: _clearUserSignature);
            _pendingUserSignature = null;
            _clearUserSignature = false;
            CambiasoTheme.ShowInfo(this, $"Usuario '{user.AliasUsuario}' actualizado correctamente.");
            ApplyMode(EditorMode.View);
            ReloadGroupsAndUsers(user.IdUsuario);
            RequesterManager.ReloadRequesters();
        }
        catch (ConcurrencyConflictException cx)
        {
            CambiasoTheme.ShowWarning(this, cx.Message, "SISPRO — Conflicto de Concurrencia");
            ApplyMode(EditorMode.View);
            ReloadGroupsAndUsers(_selectedUser.IdUsuario);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void BrowseUserSignatureFile()
    {
        using OpenFileDialog dlg = new()
        {
            Title = "Adjuntar Firma Digital del Solicitante",
            Filter = "Firmas (PNG, JPG, PDF)|*.png;*.jpg;*.jpeg;*.pdf|PNG (*.png)|*.png|JPG (*.jpg;*.jpeg)|*.jpg;*.jpeg|PDF (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            AttachSignatureToUserEditor(dlg.FileName);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message, "SISPRO — Firma Digital");
        }
    }

    private void ClearUserEditorSignature()
    {
        _pendingUserSignature = null;
        _clearUserSignature = true;
        UpdateUserSignatureControls();
    }

    private void UpdateUserSignatureControls()
    {
        bool editable = _mode is EditorMode.Create or EditorMode.Edit;
        Requester? catalogReq = _selectedUser is not null &&
                                _requestersByAlias.TryGetValue(_selectedUser.AliasUsuario, out Requester? found)
            ? found
            : null;

        byte[]? previewBytes = _pendingUserSignature?.PreviewPngBytes ??
                               (!_clearUserSignature ? catalogReq?.FirmaPreviewPng : null);
        string sigLabel = _pendingUserSignature is not null
            ? $"{_pendingUserSignature.FileName} ({_pendingUserSignature.FileType})"
            : (!_clearUserSignature && catalogReq is { TieneFirma: true }
                ? $"{catalogReq.FirmaNombre} ({catalogReq.FirmaBadge})"
                : "Sin firma digital");

        bool hasSig = previewBytes is { Length: > 0 } ||
                      _pendingUserSignature is not null ||
                      (!_clearUserSignature && catalogReq is { TieneFirma: true });

        _btnUserAttachSig.Enabled = editable && _chkSolicitante.Checked;
        _btnUserClearSig.Enabled = editable && _chkSolicitante.Checked && hasSig;
        _lblUserSigInfo.Text = _chkSolicitante.Checked ? sigLabel : "Marque Solicitante OT para adjuntar firma";
        _lblUserSigInfo.ForeColor = hasSig && _chkSolicitante.Checked ? CambiasoTheme.Success : CambiasoTheme.TextMuted;

        Image? old = _picUserSig.Image;
        _picUserSig.Image = previewBytes is { Length: > 0 } && _chkSolicitante.Checked
            ? SignatureFileService.CreateBitmapFromBytes(previewBytes)
            : null;
        old?.Dispose();
    }

    private void PopulateUserSignatureFromCatalog(User u)
    {
        _pendingUserSignature = null;
        _clearUserSignature = false;
        UpdateUserSignatureControls();
    }

    private void OpenResetPasswordDialog()
    {
        if (_selectedUser is null)
        {
            return;
        }

        using AdminResetPasswordDialog dlg = new(_ctx.UserManagementService, _session, _selectedUser);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            ReloadGroupsAndUsers(_selectedUser.IdUsuario);
        }
    }

    private void OpenPermissionsDialog()
    {
        UserProfile? selectedProf = _cmbPerfil.SelectedItem as UserProfile;
        int profId = selectedProf?.IdNivel ?? _selectedUser?.EffectiveIdNivel ?? (int)UserRoleLevel.Usuario;
        UserRoleLevel level = Enum.IsDefined(typeof(UserRoleLevel), profId)
            ? (UserRoleLevel)profId
            : UserRoleLevel.Usuario;
        string title = selectedProf?.Nombre ?? _selectedUser?.PerfilDisplay ?? level.ToString();
        bool readOnly = _mode == EditorMode.View;
        using UserPermissionsDialog dlg = new(level, _workingPermissions, readOnly, title);
        if (dlg.ShowDialog(this) == DialogResult.OK && !readOnly)
        {
            _workingPermissions = dlg.ResultPermissions;
        }
    }

    private void CancelOperation()
    {
        if (_selectedUser is not null)
        {
            PopulateEditorFromUser(_selectedUser);
        }
        else if (GridUsers.CurrentRow is not null && GetUserAtRow(GridUsers.CurrentRow.Index) is User gridUser)
        {
            _selectedUser = gridUser;
            PopulateEditorFromUser(_selectedUser);
        }
        else
        {
            ClearEditorInputs();
        }

        ApplyMode(EditorMode.View);
    }

    private void ClearFieldsOrFilters()
    {
        if (_mode == EditorMode.Create)
        {
            EnterCreateMode();
            return;
        }

        if (_mode == EditorMode.Edit)
        {
            _txtNombre.Clear();
            _txtApellido.Clear();
            _txtCorreo.Clear();
            _txtTelefono.Clear();
            _txtNombre.Focus();
            return;
        }

        _txtBuscar.Clear();
        _cmbFiltroPerfil.SelectedIndex = 0;
        _cmbFiltroEstado.SelectedIndex = 0;
        _dateFilter.Clear();
        _activeGroupFilterId = null;
        _filterUnassignedOnly = false;
        _checkedUserIds.Clear();
        _selectedUser = null;
        ClearEditorInputs();
        ReloadGroupsAndUsers();
    }

    private void ClearEditorInputs()
    {
        _txtId.Text = "(Automático)";
        _txtUsuario.Clear();
        _txtNombre.Clear();
        _txtApellido.Clear();
        _txtCorreo.Clear();
        _txtTelefono.Clear();
        _txtPassword.Clear();
        _txtConfirmPassword.Clear();
        SelectGroupInCombo(null);
        _lblTimestamps.Text = "Creación: —   |   Modificación: —   |   Último acceso: —";
        _chkSolicitante.Checked = false;
        _chkForzarCambioClave.Checked = false;
        _pendingUserSignature = null;
        _clearUserSignature = false;
        UpdateUserSignatureControls();
    }

    private void OnProfileChanged()
    {
        if (_suppressProfileChange || _mode == EditorMode.View)
        {
            return;
        }

        if (_cmbPerfil.SelectedItem is UserProfile prof)
        {
            UserPermissions src = prof.PermisosPorDefecto;
            _workingPermissions = new UserPermissions
            {
                IdUsuario = _selectedUser?.IdUsuario ?? string.Empty,
                MenuOrdenesTrabajo = src.MenuOrdenesTrabajo,
                IngresoOt = src.IngresoOt,
                BusquedaOt = src.BusquedaOt,
                ImpresionOt = src.ImpresionOt,
                MenuMantenedor = src.MenuMantenedor,
                MenuUsuarios = src.MenuUsuarios,
                IngresoUsuarios = src.IngresoUsuarios,
                ListadoUsuarios = src.ListadoUsuarios
            };
            return;
        }

        UserRoleLevel level = _cmbPerfil.SelectedItem is UserRoleLevel lvl ? lvl : UserRoleLevel.Usuario;
        _workingPermissions = PermissionPresets.CreateForRole(level, _selectedUser?.IdUsuario ?? string.Empty);
    }

    private User BuildUserFromEditor(string? existingId)
    {
        (string pat, string mat) = User.SplitSurnames(_txtApellido.Text);
        UserProfile? prof = _cmbPerfil.SelectedItem as UserProfile;
        int idNivel = prof?.IdNivel ?? (int)UserRoleLevel.Usuario;
        UserRoleLevel level = Enum.IsDefined(typeof(UserRoleLevel), idNivel) ? (UserRoleLevel)idNivel : UserRoleLevel.Usuario;
        int? groupId = (_cmbGrupo.SelectedItem as GroupComboOption)?.IdGrupo;
        bool active = _cmbEstado.SelectedIndex != 1;

        return new User
        {
            IdUsuario = existingId ?? string.Empty,
            CodigoId = _selectedUser?.CodigoId ?? 0,
            AliasUsuario = _txtUsuario.Text.Trim(),
            NombreUsuario = _txtNombre.Text.Trim(),
            ApePatUsuario = pat,
            ApeMatUsuario = mat,
            MailUsuario = _txtCorreo.Text.Trim(),
            FonoUsuario = _txtTelefono.Text.Trim(),
            Nivel = level,
            IdNivel = idNivel,
            NombrePerfil = prof?.Nombre ?? level.ToString(),
            IdGrupo = groupId,
            Activo = active,
            EsSolicitante = _chkSolicitante.Checked,
            DebeCambiarClave = _mode == EditorMode.Create && _chkForzarCambioClave.Checked,
            RowVersion = _selectedUser?.RowVersion ?? 1,
            Permisos = _workingPermissions
        };
    }

    private void PopulateEditorFromUser(User u)
    {
        _txtId.Text = u.CodigoId.ToString(CultureInfo.InvariantCulture);
        _txtUsuario.Text = u.AliasUsuario;
        _txtNombre.Text = u.NombreUsuario;
        _txtApellido.Text = u.Apellidos;
        _txtCorreo.Text = u.MailUsuario;
        _txtTelefono.Text = u.FonoUsuario;

        _suppressProfileChange = true;
        _cmbPerfil.SelectedValue = u.EffectiveIdNivel;
        if (_cmbPerfil.SelectedIndex < 0 && _profiles.FirstOrDefault(p => p.IdNivel == u.EffectiveIdNivel) is UserProfile inactiveProf)
        {
            List<UserProfile> ds = (_cmbPerfil.DataSource as IEnumerable<UserProfile>)?.ToList() ?? [];
            ds.Add(inactiveProf);
            _cmbPerfil.DataSource = null;
            _cmbPerfil.DisplayMember = nameof(UserProfile.Nombre);
            _cmbPerfil.ValueMember = nameof(UserProfile.IdNivel);
            _cmbPerfil.DataSource = ds;
            _cmbPerfil.SelectedValue = u.EffectiveIdNivel;
        }
        SelectGroupInCombo(u.IdGrupo);
        _cmbEstado.SelectedIndex = u.Activo ? 0 : 1;
        _suppressProfileChange = false;

        _txtPassword.Clear();
        _txtConfirmPassword.Clear();
        string fc = u.FechaCreacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        string fm = u.FechaModificacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        string ua = u.UltimoAcceso.HasValue ? u.UltimoAcceso.Value.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) : "Sin accesos";
        _lblTimestamps.Text = $"Creación: {fc}   |   Modif.: {fm}   |   Último acceso: {ua}";

        _chkSolicitante.Checked = u.EsSolicitante;
        _chkForzarCambioClave.Checked = u.DebeCambiarClave;
        _workingPermissions = new UserPermissions
        {
            IdUsuario = u.IdUsuario,
            MenuOrdenesTrabajo = u.Permisos.MenuOrdenesTrabajo,
            IngresoOt = u.Permisos.IngresoOt,
            VistoBuenoSupervisor = u.Permisos.VistoBuenoSupervisor,
            VistoBuenoJefatura = u.Permisos.VistoBuenoJefatura,
            BusquedaOt = u.Permisos.BusquedaOt,
            ImpresionOt = u.Permisos.ImpresionOt,
            MenuMantenedor = u.Permisos.MenuMantenedor,
            MenuUsuarios = u.Permisos.MenuUsuarios,
            IngresoUsuarios = u.Permisos.IngresoUsuarios,
            ListadoUsuarios = u.Permisos.ListadoUsuarios
        };
        PopulateUserSignatureFromCatalog(u);
    }

    private void SelectGroupInCombo(int? idGrupo)
    {
        if (_cmbGrupo.DataSource is not List<GroupComboOption> options)
        {
            return;
        }

        int idx = options.FindIndex(o => o.IdGrupo == idGrupo);
        _cmbGrupo.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private UserSearchFilter BuildSearchFilter()
    {
        int? profileIdFilter = _cmbFiltroPerfil.SelectedItem is UserProfile prof ? prof.IdNivel : null;
        bool? activeFilter = _cmbFiltroEstado.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => null
        };

        return new UserSearchFilter
        {
            TextoBusqueda = _txtBuscar.Text,
            IdNivelPerfil = profileIdFilter,
            IdGrupo = _filterUnassignedOnly ? null : _activeGroupFilterId,
            SoloSinGrupo = _filterUnassignedOnly,
            Activo = activeFilter,
            FechaCreacionInicio = _dateFilter.FechaInicio,
            FechaCreacionFin = _dateFilter.FechaFin
        };
    }

    private void LoadUsersGrid(string? selectUserId = null)
    {
        if (!_dateFilter.TryValidate(this, out _))
        {
            return;
        }

        _suppressGridSelection = true;
        try
        {
            _users = _ctx.UserManagementService.Search(_session, BuildSearchFilter());
            IReadOnlyList<Requester> catalogRequesters = _ctx.MaintainerService.GetRequesters(includeInactive: true);
            _requestersByAlias = catalogRequesters
                .GroupBy(r => r.Alias.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            // Retener únicamente checkmarks que siguen en los resultados visibles
            HashSet<string> visibleIds = _users.Select(u => u.IdUsuario).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _checkedUserIds.IntersectWith(visibleIds);

            GridUsers.Rows.Clear();
            foreach (User u in _users)
            {
                bool isChecked = _checkedUserIds.Contains(u.IdUsuario);
                string solDisplay = "No";
                if (u.EsSolicitante)
                {
                    if (_requestersByAlias.TryGetValue(u.AliasUsuario.Trim(), out Requester? req) && req.TieneFirma)
                    {
                        solDisplay = $"✓ {req.FirmaBadge}";
                    }
                    else
                    {
                        solDisplay = "✓ Sin firma";
                    }
                }

                int rIdx = GridUsers.Rows.Add(
                    isChecked,
                    u.CodigoId,
                    u.AliasUsuario,
                    u.NombreCompleto,
                    u.MailUsuario,
                    u.FonoUsuario,
                    u.PerfilDisplay,
                    string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo,
                    solDisplay,
                    u.Activo ? "Activo" : "Inactivo",
                    u.FechaCreacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
                    u.UltimoAcceso.HasValue ? u.UltimoAcceso.Value.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture) : "Sin accesos");
                GridUsers.Rows[rIdx].Tag = u;
            }

            int activos = _users.Count(u => u.Activo);
            int inactivos = _users.Count - activos;
            string groupTag = (ListGroups.SelectedItem as GroupListEntry)?.Name ?? "TODOS LOS USUARIOS";
            _grpListado.Text = $"ÁREA 2 — LISTADO DE USUARIOS · {groupTag} ({_users.Count})";
            _lblGridStats.Text = $"Activos: {activos} | Inactivos: {inactivos} | Clic en columna para ordenar";

            int targetIdx = -1;
            if (!string.IsNullOrWhiteSpace(selectUserId))
            {
                for (int i = 0; i < GridUsers.Rows.Count; i++)
                {
                    if (GetUserAtRow(i) is User candidate && string.Equals(candidate.IdUsuario, selectUserId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIdx = i;
                        break;
                    }
                }
            }
            else if (GridUsers.Rows.Count > 0)
            {
                targetIdx = 0;
            }

            if (targetIdx >= 0 && targetIdx < GridUsers.Rows.Count)
            {
                GridUsers.ClearSelection();
                GridUsers.Rows[targetIdx].Selected = true;
                GridUsers.CurrentCell = GridUsers.Rows[targetIdx].Cells[1];
                _selectedUser = GetUserAtRow(targetIdx);
                if (_mode == EditorMode.View && _selectedUser is not null)
                {
                    PopulateEditorFromUser(_selectedUser);
                }
            }
            else
            {
                _selectedUser = null;
                if (_mode == EditorMode.View)
                {
                    ClearEditorInputs();
                }
            }
        }
        finally
        {
            _suppressGridSelection = false;
        }

        if (_mode == EditorMode.View)
        {
            ApplyMode(EditorMode.View);
        }
        else
        {
            UpdateSelectionBadgeAndBatchButtons();
        }
    }

    private User? GetUserAtRow(int rowIndex) =>
        rowIndex >= 0 && rowIndex < GridUsers.Rows.Count ? GridUsers.Rows[rowIndex].Tag as User : null;

    private void OnGridSortCompare(object? sender, DataGridViewSortCompareEventArgs e)
    {
        User? u1 = GetUserAtRow(e.RowIndex1);
        User? u2 = GetUserAtRow(e.RowIndex2);
        if (u1 is null || u2 is null)
        {
            return;
        }

        switch (e.Column.Name)
        {
            case "colId":
                e.SortResult = u1.CodigoId.CompareTo(u2.CodigoId);
                e.Handled = true;
                break;
            case "colCreacion":
                e.SortResult = u1.FechaCreacion.CompareTo(u2.FechaCreacion);
                e.Handled = true;
                break;
            case "colUltimoAcceso":
                e.SortResult = (u1.UltimoAcceso ?? DateTime.MinValue).CompareTo(u2.UltimoAcceso ?? DateTime.MinValue);
                e.Handled = true;
                break;
            default:
                e.SortResult = StringComparer.CurrentCultureIgnoreCase.Compare(
                    Convert.ToString(e.CellValue1, CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(e.CellValue2, CultureInfo.InvariantCulture) ?? string.Empty);
                e.Handled = true;
                break;
        }
    }

    private void OnGridSelectionChanged()
    {
        if (_suppressGridSelection || _mode != EditorMode.View)
        {
            return;
        }

        User? selected = GridUsers.CurrentRow is not null ? GetUserAtRow(GridUsers.CurrentRow.Index) : null;
        if (selected is null)
        {
            _selectedUser = null;
            ClearEditorInputs();
            ApplyMode(EditorMode.View);
            return;
        }

        if (ReferenceEquals(_selectedUser, selected))
        {
            return;
        }

        _selectedUser = selected;
        PopulateEditorFromUser(_selectedUser);
        ApplyMode(EditorMode.View);
    }

    private void OnGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        User? u = GetUserAtRow(e.RowIndex);
        if (u is null || e.CellStyle is null)
        {
            return;
        }

        string colName = GridUsers.Columns[e.ColumnIndex].Name;
        if (colName == "colEstado")
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = u.Activo ? CambiasoTheme.Success : CambiasoTheme.Danger;
        }
        else if (colName == "colGrupo")
        {
            e.CellStyle.ForeColor = string.IsNullOrWhiteSpace(u.NombreGrupo) ? CambiasoTheme.TextMuted : CambiasoTheme.Primary;
            if (!string.IsNullOrWhiteSpace(u.NombreGrupo))
            {
                e.CellStyle.Font = CambiasoTheme.LabelFont;
            }
        }
        else if (colName == "colSolicitante")
        {
            bool hasSig = _requestersByAlias.TryGetValue(u.AliasUsuario.Trim(), out Requester? req) && req.TieneFirma;
            e.CellStyle.Font = u.EsSolicitante ? CambiasoTheme.LabelFont : CambiasoTheme.BodyFont;
            e.CellStyle.ForeColor = !u.EsSolicitante ? CambiasoTheme.TextMuted : (hasSig ? CambiasoTheme.Success : CambiasoTheme.Accent);
        }
        else if (!u.Activo && e.ColumnIndex > 0)
        {
            e.CellStyle.ForeColor = CambiasoTheme.TextMuted;
        }
    }

    private string BuildFilterSummary()
    {
        string grp = (ListGroups.SelectedItem as GroupListEntry)?.Name ?? "Todos";
        string perfil = _cmbFiltroPerfil.SelectedItem?.ToString() ?? "Todos";
        string estado = _cmbFiltroEstado.SelectedItem?.ToString() ?? "Todos";
        return $"Grupo: {grp} | Perfil: {perfil} | Estado: {estado} | {_dateFilter.BuildFilterDescription()}";
    }

    private void PrintUsersList()
    {
        if (_users.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "No hay usuarios en el listado actual para imprimir.");
            return;
        }

        using PrintDocument doc = _ctx.PrintService.CreateUsersReportPrintDocument(_users);
        using PrintPreviewDialog preview = new()
        {
            Document = doc,
            Width = 960,
            Height = 720,
            StartPosition = FormStartPosition.CenterParent
        };
        preview.ShowDialog(this);
    }

    private Button CreateAutoButton(string text, AppIcon icon, ButtonTone tone, string tooltip)
    {
        Button btn = CambiasoTheme.CreateButton(text, icon, tone, _toolTip, tooltip, width: 80, height: 28);
        btn.AutoSize = true;
        btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btn.MinimumSize = new Size(0, 28);
        btn.Padding = new Padding(6, 0, 8, 0);
        btn.Margin = new Padding(0, 1, 5, 1);
        return btn;
    }

    private Button CreateCompactButton(string text, AppIcon icon, ButtonTone tone, string tooltip)
    {
        Button btn = CambiasoTheme.CreateButton(text, icon, tone, _toolTip, tooltip, width: 72, height: 26);
        btn.Font = new Font("Segoe UI", 8.2f, FontStyle.Bold);
        btn.Padding = new Padding(4, 0, 4, 0);
        return btn;
    }

    private static TextBox CreateReadOnlyTextBox(string text) => new()
    {
        Text = text,
        ReadOnly = true,
        TabStop = false,
        BackColor = CambiasoTheme.Surface,
        ForeColor = CambiasoTheme.TextMuted,
        Dock = DockStyle.Fill
    };

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        Font = CambiasoTheme.LabelFont,
        ForeColor = CambiasoTheme.TextMuted,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 6, 0)
    };

    private static FlowLayoutPanel WrapFilter(string label, Control ctl)
    {
        FlowLayoutPanel p = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = new Padding(0, 0, 4, 0)
        };
        Label lbl = CambiasoTheme.CreateFieldLabel(label);
        lbl.Margin = new Padding(0, 6, 3, 0);
        ctl.Margin = new Padding(0, 2, 0, 0);
        p.Controls.AddRange([lbl, ctl]);
        return p;
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

            _picUserSig.Image?.Dispose();
            _picUserSig.Image = null;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
