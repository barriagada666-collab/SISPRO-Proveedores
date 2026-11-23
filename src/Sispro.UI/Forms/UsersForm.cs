using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo profesional GESTIÓN DE USUARIOS (CRUD completo, eliminación lógica, filtros avanzados y RBAC estricto).
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

    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private EditorMode _mode = EditorMode.View;
    private bool _suppressGridSelection;
    private bool _suppressProfileChange;
    private IReadOnlyList<User> _users = [];
    private User? _selectedUser;
    private UserPermissions _workingPermissions = PermissionPresets.CreateForRole(UserRoleLevel.Usuario);

    // Header mode indicator
    private readonly Panel _modeStrip;
    private readonly Label _lblMode;

    // Top section fields (DATOS DEL USUARIO)
    private readonly TextBox _txtId;
    private readonly TextBox _txtUsuario;
    private readonly TextBox _txtNombre;
    private readonly TextBox _txtApellido;
    private readonly TextBox _txtCorreo;
    private readonly TextBox _txtTelefono;
    private readonly ComboBox _cmbPerfil;
    private readonly ComboBox _cmbEstado;
    private readonly TextBox _txtPassword;
    private readonly TextBox _txtConfirmPassword;
    private readonly TextBox _txtFechaCreacion;
    private readonly TextBox _txtUltimoAcceso;
    private readonly CheckBox _chkMostrarPass;
    private readonly CheckBox _chkSolicitante;
    private readonly CheckBox _chkForzarCambioClave;
    private readonly Button _btnPermisos;

    // 10 required buttons + export actions (internal for automated UI verification)
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

    // Bottom section filters & grid (LISTADO DE USUARIOS)
    private readonly GroupBox _grpListado;
    private readonly TextBox _txtBuscar;
    private readonly ComboBox _cmbFiltroPerfil;
    private readonly ComboBox _cmbFiltroEstado;
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
            Height = 28,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(10, 0, 10, 0)
        };
        _lblMode = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _modeStrip.Controls.Add(_lblMode);

        // --- FORM FIELDS ---
        Padding fieldMargin = new(0, 5, 10, 4);
        _txtId = CreateReadOnlyTextBox("(Automático)");
        _txtId.Margin = fieldMargin;
        _txtUsuario = new TextBox { Name = "txtUsuarioCrud", MaxLength = 30, PlaceholderText = "Ej. barriagada", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtNombre = new TextBox { Name = "txtNombreCrud", MaxLength = 60, PlaceholderText = "Nombres del usuario", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtApellido = new TextBox { Name = "txtApellidoCrud", MaxLength = 80, PlaceholderText = "Apellidos del usuario", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtCorreo = new TextBox { Name = "txtCorreoCrud", MaxLength = 100, PlaceholderText = "correo@cambiaso.cl", Dock = DockStyle.Fill, Margin = fieldMargin };
        _txtTelefono = new TextBox { Name = "txtTelefonoCrud", MaxLength = 30, PlaceholderText = "Ej. +56 9 1234 5678", Dock = DockStyle.Fill, Margin = fieldMargin };

        _cmbPerfil = new ComboBox
        {
            Name = "cmbPerfilCrud",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = fieldMargin
        };
        _cmbPerfil.DataSource = Enum.GetValues<UserRoleLevel>();
        _cmbPerfil.SelectedIndexChanged += (_, _) => OnProfileChanged();

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
            PlaceholderText = "Confirmar contraseña",
            Dock = DockStyle.Fill,
            Margin = fieldMargin
        };

        _txtFechaCreacion = CreateReadOnlyTextBox("—");
        _txtFechaCreacion.Margin = fieldMargin;
        _txtUltimoAcceso = CreateReadOnlyTextBox("Sin accesos");
        _txtUltimoAcceso.Margin = fieldMargin;

        _chkMostrarPass = new CheckBox
        {
            Text = "Mostrar contraseñas",
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(0, 4, 14, 0)
        };
        _chkMostrarPass.CheckedChanged += (_, _) =>
        {
            bool hide = !_chkMostrarPass.Checked;
            _txtPassword.UseSystemPasswordChar = hide;
            _txtConfirmPassword.UseSystemPasswordChar = hide;
        };

        _chkSolicitante = new CheckBox
        {
            Text = "Es solicitante de trabajos",
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary,
            Margin = new Padding(0, 4, 14, 0)
        };

        _chkForzarCambioClave = new CheckBox
        {
            Text = "Solicitar cambio de contraseña en el primer acceso",
            AutoSize = true,
            Checked = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary,
            Margin = new Padding(0, 4, 14, 0)
        };

        _btnPermisos = CambiasoTheme.CreateButton("Permisos RBAC", AppIcon.Template, ButtonTone.Secondary, _toolTip, "Ver o personalizar permisos del perfil seleccionado", width: 138, height: 26);
        _btnPermisos.Margin = new Padding(4, 0, 0, 0);
        _btnPermisos.Click += (_, _) => OpenPermissionsDialog();

        // --- ACTION BUTTONS (EDITOR) ---
        BtnNuevo = CambiasoTheme.CreateButton("Nuevo", AppIcon.UserPlus, ButtonTone.Primary, _toolTip, "Limpiar el formulario para registrar un nuevo usuario", width: 90, height: 32);
        BtnNuevo.Click += (_, _) => EnterCreateMode();

        BtnGuardar = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip, "Insertar el nuevo usuario en la base de datos", width: 96, height: 32);
        BtnGuardar.Click += (_, _) => SaveNewUser();

        BtnEditar = CambiasoTheme.CreateButton("Editar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Habilitar modificación de datos del usuario seleccionado", width: 88, height: 32);
        BtnEditar.Click += (_, _) => EnterEditMode();

        BtnActualizar = CambiasoTheme.CreateButton("Actualizar", AppIcon.Check, ButtonTone.Accent, _toolTip, "Guardar los cambios realizados en el usuario", width: 106, height: 32);
        BtnActualizar.Click += (_, _) => UpdateExistingUser();

        BtnEliminar = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Realizar eliminación lógica (desactivar) del usuario seleccionado", width: 98, height: 32);
        BtnEliminar.Click += (_, _) => DeactivateSelectedUser();

        BtnCambiarPassword = CambiasoTheme.CreateButton("Cambiar Contraseña", AppIcon.Key, ButtonTone.Accent, _toolTip, "Gestionar o restablecer credenciales del usuario seleccionado", width: 164, height: 32);
        BtnCambiarPassword.Click += (_, _) => OpenResetPasswordDialog();

        BtnCancelar = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cancelar la operación actual y restaurar datos", width: 96, height: 32);
        BtnCancelar.Click += (_, _) => CancelOperation();

        BtnLimpiar = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Limpiar campos del formulario o filtros de búsqueda", width: 92, height: 32);
        BtnLimpiar.Click += (_, _) => ClearFieldsOrFilters();

        // --- BOTTOM SECTION FILTERS & GRID ---
        _txtBuscar = new TextBox
        {
            Name = "txtBuscarUsuarios",
            Width = 176,
            PlaceholderText = "Nombre, apellido o usuario..."
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
            Width = 120
        };
        _cmbFiltroPerfil.Items.Add("Todos");
        foreach (UserRoleLevel lvl in Enum.GetValues<UserRoleLevel>())
        {
            _cmbFiltroPerfil.Items.Add(lvl);
        }
        _cmbFiltroPerfil.SelectedIndex = 0;
        _cmbFiltroPerfil.SelectedIndexChanged += (_, _) => LoadUsersGrid();

        _cmbFiltroEstado = new ComboBox
        {
            Name = "cmbFiltroEstado",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 96
        };
        _cmbFiltroEstado.Items.AddRange(["Todos", "Activo", "Inactivo"]);
        _cmbFiltroEstado.SelectedIndex = 0;
        _cmbFiltroEstado.SelectedIndexChanged += (_, _) => LoadUsersGrid();

        BtnBuscar = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Ejecutar búsqueda y aplicar filtros", width: 92, height: 30);
        BtnBuscar.Click += (_, _) => LoadUsersGrid();

        BtnRefrescar = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar listado de usuarios desde la base de datos", width: 106, height: 30);
        BtnRefrescar.Click += (_, _) => RefreshData();

        GridUsers = new DataGridView { Name = "gridUsuarios", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(GridUsers);
        GridUsers.SelectionChanged += (_, _) => OnGridSelectionChanged();
        GridUsers.CellDoubleClick += (_, _) =>
        {
            if (_selectedUser is not null)
            {
                EnterEditMode();
            }
        };
        GridUsers.CellFormatting += OnGridCellFormatting;

        _grpListado = CambiasoTheme.CreateGroupBox("LISTADO DE USUARIOS");
        _grpListado.Dock = DockStyle.Fill;

        BuildLayout();
        ApplyMode(EditorMode.View);
        LoadUsersGrid();
    }

    public void RefreshData()
    {
        string? preserveId = _selectedUser?.IdUsuario;
        LoadUsersGrid(preserveId);
    }

    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(2)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 318));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // === SECCIÓN SUPERIOR: DATOS DEL USUARIO ===
        GroupBox grpDatos = CambiasoTheme.CreateGroupBox("DATOS DEL USUARIO");
        grpDatos.Dock = DockStyle.Fill;

        TableLayoutPanel topContainer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        topContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        topContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        topContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        TableLayoutPanel fieldsGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 5,
            Padding = new Padding(4, 6, 4, 2)
        };
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 144));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128));
        fieldsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));

        for (int r = 0; r < 4; r++)
        {
            fieldsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }
        fieldsGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        // Row 0: ID | Usuario | Perfil
        fieldsGrid.Controls.Add(FieldLabel("ID:"), 0, 0);
        fieldsGrid.Controls.Add(_txtId, 1, 0);
        fieldsGrid.Controls.Add(FieldLabel("Usuario *:"), 2, 0);
        fieldsGrid.Controls.Add(_txtUsuario, 3, 0);
        fieldsGrid.Controls.Add(FieldLabel("Perfil *:"), 4, 0);
        fieldsGrid.Controls.Add(_cmbPerfil, 5, 0);

        // Row 1: Nombre | Apellido | Estado
        fieldsGrid.Controls.Add(FieldLabel("Nombre *:"), 0, 1);
        fieldsGrid.Controls.Add(_txtNombre, 1, 1);
        fieldsGrid.Controls.Add(FieldLabel("Apellido *:"), 2, 1);
        fieldsGrid.Controls.Add(_txtApellido, 3, 1);
        fieldsGrid.Controls.Add(FieldLabel("Estado *:"), 4, 1);
        fieldsGrid.Controls.Add(_cmbEstado, 5, 1);

        // Row 2: Correo electrónico | Teléfono | Fecha de creación
        fieldsGrid.Controls.Add(FieldLabel("Correo electrónico:"), 0, 2);
        fieldsGrid.Controls.Add(_txtCorreo, 1, 2);
        fieldsGrid.Controls.Add(FieldLabel("Teléfono:"), 2, 2);
        fieldsGrid.Controls.Add(_txtTelefono, 3, 2);
        fieldsGrid.Controls.Add(FieldLabel("Fecha de creación:"), 4, 2);
        fieldsGrid.Controls.Add(_txtFechaCreacion, 5, 2);

        // Row 3: Contraseña | Confirmar contraseña | Último acceso
        fieldsGrid.Controls.Add(FieldLabel("Contraseña:"), 0, 3);
        fieldsGrid.Controls.Add(_txtPassword, 1, 3);
        fieldsGrid.Controls.Add(FieldLabel("Confirmar contraseña:"), 2, 3);
        fieldsGrid.Controls.Add(_txtConfirmPassword, 3, 3);
        fieldsGrid.Controls.Add(FieldLabel("Último acceso:"), 4, 3);
        fieldsGrid.Controls.Add(_txtUltimoAcceso, 5, 3);

        // Row 4: Options + Permissions button
        FlowLayoutPanel optionsRow = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Margin = new Padding(0, 2, 0, 0)
        };
        optionsRow.Controls.AddRange([_chkMostrarPass, _chkSolicitante, _chkForzarCambioClave, _btnPermisos]);
        fieldsGrid.Controls.Add(optionsRow, 0, 4);
        fieldsGrid.SetColumnSpan(optionsRow, 6);

        FlowLayoutPanel actionBar = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(2, 6, 2, 2)
        };
        actionBar.Controls.AddRange(
        [
            BtnNuevo, BtnGuardar, BtnEditar, BtnActualizar,
            BtnEliminar, BtnCambiarPassword, BtnCancelar, BtnLimpiar
        ]);

        topContainer.Controls.Add(_modeStrip, 0, 0);
        topContainer.Controls.Add(fieldsGrid, 0, 1);
        topContainer.Controls.Add(actionBar, 0, 2);
        grpDatos.Controls.Add(topContainer);

        // === SECCIÓN INFERIOR: LISTADO DE USUARIOS ===
        TableLayoutPanel bottomContainer = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        bottomContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        bottomContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel filterBar = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(2, 5, 2, 4)
        };

        Button btnPrint = CambiasoTheme.CreateButton("Imprimir", AppIcon.Print, ButtonTone.Secondary, _toolTip, "Vista previa e impresión del listado", width: 108, height: 30);
        btnPrint.Click += (_, _) => PrintUsersList();

        Button btnExcel = CambiasoTheme.CreateButton("Excel", AppIcon.Excel, ButtonTone.Secondary, _toolTip, "Exportar listado a Excel (.xlsx)", width: 86, height: 30);
        btnExcel.Click += (_, _) => ExportUsersExcel();

        filterBar.Controls.AddRange(
        [
            WrapFilter("Buscar:", _txtBuscar),
            WrapFilter("Perfil:", _cmbFiltroPerfil),
            WrapFilter("Estado:", _cmbFiltroEstado),
            BtnBuscar,
            BtnRefrescar,
            btnPrint,
            btnExcel
        ]);

        bottomContainer.Controls.Add(filterBar, 0, 0);
        bottomContainer.Controls.Add(GridUsers, 0, 1);
        _grpListado.Controls.Add(bottomContainer);

        root.Controls.Add(grpDatos, 0, 0);
        root.Controls.Add(_grpListado, 0, 1);
        Controls.Add(root);
    }

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

        // No permitir que el administrador degrade o desactive su propia sesión
        _cmbPerfil.Enabled = editable && !(mode == EditorMode.Edit && isSelf);
        _cmbEstado.Enabled = editable && !(mode == EditorMode.Edit && isSelf);

        _txtPassword.Enabled = isCreate;
        _txtConfirmPassword.Enabled = isCreate;
        _txtPassword.PlaceholderText = isCreate ? "Mínimo 6 caracteres" : "Use el botón Cambiar Contraseña";
        _txtConfirmPassword.PlaceholderText = isCreate ? "Confirmar contraseña" : "Use el botón Cambiar Contraseña";
        _chkMostrarPass.Enabled = isCreate;
        _chkSolicitante.Enabled = editable;
        _chkForzarCambioClave.Enabled = isCreate;
        _btnPermisos.Enabled = isCreate || hasSelection;

        // Button states
        BtnNuevo.Enabled = mode == EditorMode.View;
        BtnGuardar.Enabled = mode == EditorMode.Create;
        BtnEditar.Enabled = mode == EditorMode.View && hasSelection;
        BtnActualizar.Enabled = mode == EditorMode.Edit;
        BtnEliminar.Enabled = mode == EditorMode.View && hasSelection && _selectedUser!.Activo && !isSelf;
        BtnCambiarPassword.Enabled = (mode == EditorMode.View || mode == EditorMode.Edit) && hasSelection;
        BtnCancelar.Enabled = editable;
        BtnLimpiar.Enabled = true;

        // Mode banner styling
        switch (mode)
        {
            case EditorMode.Create:
                _modeStrip.BackColor = Color.FromArgb(224, 244, 252);
                _lblMode.ForeColor = CambiasoTheme.Primary;
                _lblMode.Text = "MODO: NUEVO USUARIO — Complete los datos obligatorios (*) y presione Guardar.";
                break;
            case EditorMode.Edit:
                _modeStrip.BackColor = Color.FromArgb(254, 243, 199);
                _lblMode.ForeColor = Color.FromArgb(146, 64, 14);
                _lblMode.Text = $"MODO: EDITANDO USUARIO '{_selectedUser?.AliasUsuario}' (ID {_selectedUser?.CodigoId}) — Modifique los datos y presione Actualizar.";
                break;
            default:
                _modeStrip.BackColor = CambiasoTheme.SubtleFill;
                _lblMode.ForeColor = CambiasoTheme.Primary;
                _lblMode.Text = hasSelection
                    ? $"MODO: CONSULTA — Usuario seleccionado: '{_selectedUser!.AliasUsuario}' ({_selectedUser.Nivel}, {(_selectedUser.Activo ? "Activo" : "Inactivo")})."
                    : "MODO: CONSULTA — Seleccione un usuario de la grilla o presione Nuevo para registrar uno.";
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
        _txtFechaCreacion.Text = "(Automática al guardar)";
        _txtUltimoAcceso.Text = "Sin accesos";

        _suppressProfileChange = true;
        _cmbPerfil.SelectedItem = UserRoleLevel.Usuario;
        _cmbEstado.SelectedIndex = 0; // Activo
        _suppressProfileChange = false;

        _chkSolicitante.Checked = false;
        _chkForzarCambioClave.Checked = true;
        _workingPermissions = PermissionPresets.CreateForRole(UserRoleLevel.Usuario);

        ApplyMode(EditorMode.Create);
        _txtUsuario.Focus();
    }

    internal void EnterEditMode()
    {
        if (_selectedUser is null)
        {
            CambiasoTheme.ShowWarning(this, "Debe seleccionar un usuario del listado para editar.");
            return;
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
            int assignedId = _ctx.UserManagementService.CreateUser(_session, user, _txtPassword.Text, _txtConfirmPassword.Text);
            CambiasoTheme.ShowInfo(this, $"Usuario '{user.AliasUsuario}' (ID {assignedId}) creado correctamente.");
            ApplyMode(EditorMode.View);
            LoadUsersGrid(selectUserId: user.IdUsuario);
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
            _ctx.UserManagementService.UpdateUser(_session, user);
            CambiasoTheme.ShowInfo(this, $"Usuario '{user.AliasUsuario}' actualizado correctamente.");
            ApplyMode(EditorMode.View);
            LoadUsersGrid(selectUserId: user.IdUsuario);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void DeactivateSelectedUser()
    {
        if (_selectedUser is null)
        {
            return;
        }

        string confirmMsg =
            $"¿Desea desactivar (eliminación lógica) al usuario '{_selectedUser.AliasUsuario}' ({_selectedUser.NombreCompleto})?\n\n" +
            "El usuario no podrá iniciar sesión, pero se conservarán todos sus registros históricos y de auditoría.";

        if (!CambiasoTheme.Confirm(this, confirmMsg))
        {
            return;
        }

        try
        {
            string targetId = _selectedUser.IdUsuario;
            _ctx.UserManagementService.DeleteUser(_session, targetId);
            CambiasoTheme.ShowInfo(this, $"Usuario '{_selectedUser.AliasUsuario}' desactivado correctamente.");
            ApplyMode(EditorMode.View);
            LoadUsersGrid(selectUserId: targetId);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
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
            LoadUsersGrid(selectUserId: _selectedUser.IdUsuario);
        }
    }

    private void OpenPermissionsDialog()
    {
        UserRoleLevel level = _cmbPerfil.SelectedItem is UserRoleLevel lvl ? lvl : UserRoleLevel.Usuario;
        bool readOnly = _mode == EditorMode.View;
        using UserPermissionsDialog dlg = new(level, _workingPermissions, readOnly);
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
        else if (_users.Count > 0 && GridUsers.CurrentRow is not null && GridUsers.CurrentRow.Index >= 0 && GridUsers.CurrentRow.Index < _users.Count)
        {
            _selectedUser = _users[GridUsers.CurrentRow.Index];
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

        // En modo Consulta: limpiar filtros de búsqueda y recargar todo
        _txtBuscar.Clear();
        _cmbFiltroPerfil.SelectedIndex = 0;
        _cmbFiltroEstado.SelectedIndex = 0;
        _selectedUser = null;
        ClearEditorInputs();
        LoadUsersGrid();
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
        _txtFechaCreacion.Text = "—";
        _txtUltimoAcceso.Text = "Sin accesos";
        _chkSolicitante.Checked = false;
        _chkForzarCambioClave.Checked = false;
    }

    private void OnProfileChanged()
    {
        if (_suppressProfileChange || _mode == EditorMode.View)
        {
            return;
        }

        UserRoleLevel level = _cmbPerfil.SelectedItem is UserRoleLevel lvl ? lvl : UserRoleLevel.Usuario;
        _workingPermissions = PermissionPresets.CreateForRole(level, _selectedUser?.IdUsuario ?? string.Empty);
    }

    private User BuildUserFromEditor(string? existingId)
    {
        (string pat, string mat) = User.SplitSurnames(_txtApellido.Text);
        UserRoleLevel level = _cmbPerfil.SelectedItem is UserRoleLevel lvl ? lvl : UserRoleLevel.Usuario;
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
            Activo = active,
            EsSolicitante = _chkSolicitante.Checked,
            DebeCambiarClave = _mode == EditorMode.Create && _chkForzarCambioClave.Checked,
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
        _cmbPerfil.SelectedItem = u.Nivel;
        _cmbEstado.SelectedIndex = u.Activo ? 0 : 1;
        _suppressProfileChange = false;

        _txtPassword.Clear();
        _txtConfirmPassword.Clear();
        _txtFechaCreacion.Text = u.FechaCreacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        _txtUltimoAcceso.Text = u.UltimoAcceso.HasValue
            ? u.UltimoAcceso.Value.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)
            : "Sin accesos";
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
            MenuFacturas = u.Permisos.MenuFacturas,
            IngresoFacturas = u.Permisos.IngresoFacturas,
            BusquedaFacturas = u.Permisos.BusquedaFacturas,
            MenuMantenedor = u.Permisos.MenuMantenedor,
            MenuUsuarios = u.Permisos.MenuUsuarios,
            IngresoUsuarios = u.Permisos.IngresoUsuarios,
            ListadoUsuarios = u.Permisos.ListadoUsuarios
        };
    }

    private UserSearchFilter BuildSearchFilter()
    {
        UserRoleLevel? profileFilter = _cmbFiltroPerfil.SelectedItem is UserRoleLevel lvl ? lvl : null;
        bool? activeFilter = _cmbFiltroEstado.SelectedIndex switch
        {
            1 => true,
            2 => false,
            _ => null
        };

        return new UserSearchFilter
        {
            TextoBusqueda = _txtBuscar.Text,
            Perfil = profileFilter,
            Activo = activeFilter
        };
    }

    private void LoadUsersGrid(string? selectUserId = null)
    {
        _suppressGridSelection = true;
        try
        {
            _users = _ctx.UserManagementService.Search(_session, BuildSearchFilter());

            int activos = _users.Count(u => u.Activo);
            int inactivos = _users.Count - activos;
            _grpListado.Text = $"LISTADO DE USUARIOS ({_users.Count} registros — {activos} activos, {inactivos} inactivos)";

            GridUsers.DataSource = _users.Select(u => new
            {
                ID = u.CodigoId,
                Usuario = u.AliasUsuario,
                NombreCompleto = u.NombreCompleto,
                Correo = u.MailUsuario,
                Telefono = u.FonoUsuario,
                Perfil = u.Nivel.ToString(),
                Estado = u.Activo ? "● Activo" : "● Inactivo",
                FechaCreacion = u.FechaCreacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
                UltimoAcceso = u.UltimoAcceso.HasValue
                    ? u.UltimoAcceso.Value.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)
                    : "Sin accesos"
            }).ToList();

            ConfigureGridColumns();

            int targetIndex = -1;
            if (!string.IsNullOrEmpty(selectUserId))
            {
                for (int i = 0; i < _users.Count; i++)
                {
                    if (string.Equals(_users[i].IdUsuario, selectUserId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetIndex = i;
                        break;
                    }
                }
            }

            if (targetIndex < 0 && _users.Count > 0)
            {
                targetIndex = 0;
            }

            if (targetIndex >= 0 && targetIndex < GridUsers.Rows.Count)
            {
                GridUsers.ClearSelection();
                GridUsers.Rows[targetIndex].Selected = true;
                GridUsers.CurrentCell = GridUsers.Rows[targetIndex].Cells[0];
                _selectedUser = _users[targetIndex];
                if (_mode == EditorMode.View)
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
    }

    private void ConfigureGridColumns()
    {
        if (GridUsers.Columns.Count < 9)
        {
            return;
        }

        GridUsers.Columns[0].HeaderText = "ID";
        GridUsers.Columns[0].FillWeight = 5;
        GridUsers.Columns[0].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        GridUsers.Columns[1].HeaderText = "Usuario";
        GridUsers.Columns[1].FillWeight = 10;

        GridUsers.Columns[2].HeaderText = "Nombre completo";
        GridUsers.Columns[2].FillWeight = 15;

        GridUsers.Columns[3].HeaderText = "Correo electrónico";
        GridUsers.Columns[3].FillWeight = 14;

        GridUsers.Columns[4].HeaderText = "Teléfono";
        GridUsers.Columns[4].FillWeight = 9;

        GridUsers.Columns[5].HeaderText = "Perfil";
        GridUsers.Columns[5].FillWeight = 12;

        GridUsers.Columns[6].HeaderText = "Estado";
        GridUsers.Columns[6].FillWeight = 9;
        GridUsers.Columns[6].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        GridUsers.Columns[7].HeaderText = "Fecha de creación";
        GridUsers.Columns[7].FillWeight = 13;

        GridUsers.Columns[8].HeaderText = "Último acceso";
        GridUsers.Columns[8].FillWeight = 13;
    }

    private void OnGridSelectionChanged()
    {
        if (_suppressGridSelection || _mode != EditorMode.View)
        {
            return;
        }

        if (GridUsers.CurrentRow is null)
        {
            _selectedUser = null;
            ClearEditorInputs();
            ApplyMode(EditorMode.View);
            return;
        }

        int idx = GridUsers.CurrentRow.Index;
        if (idx >= 0 && idx < _users.Count)
        {
            _selectedUser = _users[idx];
            PopulateEditorFromUser(_selectedUser);
            ApplyMode(EditorMode.View);
        }
    }

    private void OnGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _users.Count || e.CellStyle is null)
        {
            return;
        }

        User rowUser = _users[e.RowIndex];

        if (e.ColumnIndex == 6) // Columna Estado con indicador visual
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            if (rowUser.Activo)
            {
                e.CellStyle.ForeColor = CambiasoTheme.Success;
                e.CellStyle.BackColor = Color.FromArgb(226, 247, 233);
                e.CellStyle.SelectionForeColor = CambiasoTheme.Success;
            }
            else
            {
                e.CellStyle.ForeColor = CambiasoTheme.Danger;
                e.CellStyle.BackColor = Color.FromArgb(253, 234, 232);
                e.CellStyle.SelectionForeColor = CambiasoTheme.Danger;
            }

            return;
        }

        if (!rowUser.Activo)
        {
            e.CellStyle.ForeColor = Color.FromArgb(130, 144, 158);
        }
    }

    private void PrintUsersList()
    {
        using PrintDocument doc = _ctx.PrintService.CreateUsersReportPrintDocument(_users);
        using PrintPreviewDialog preview = new()
        {
            Document = doc,
            Width = 920,
            Height = 720,
            StartPosition = FormStartPosition.CenterParent
        };
        preview.ShowDialog(this);
    }

    private void ExportUsersExcel()
    {
        using SaveFileDialog dlg = new()
        {
            Title = "Exportar Usuarios a Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"Usuarios_SISPRO_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _ctx.ExcelExportService.ExportUsersGrid(_users, dlg.FileName);
            CambiasoTheme.ShowInfo(this, $"Listado de usuarios exportado a:\n{dlg.FileName}");
        }
    }

    private static TextBox CreateReadOnlyTextBox(string initialText) => new()
    {
        Text = initialText,
        ReadOnly = true,
        BackColor = CambiasoTheme.Surface,
        ForeColor = CambiasoTheme.TextMuted,
        Dock = DockStyle.Fill
    };

    private static Label FieldLabel(string text)
    {
        Label lbl = CambiasoTheme.CreateFieldLabel(text);
        lbl.Anchor = AnchorStyles.Left;
        lbl.Margin = new Padding(4, 0, 2, 0);
        return lbl;
    }

    private static FlowLayoutPanel WrapFilter(string label, Control ctl)
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
