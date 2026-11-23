using System.Drawing;
using System.Drawing.Printing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Core.Validation;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo de Administración de Usuarios y Permisos RBAC (reemplaza form_usuario.frm y TAB_US_0.bas).
/// Corrige todos los errores de mapeo de columnas de permisos, cabeceras de grilla, RUT y datos hardcodeados.
/// </summary>
public sealed class UsersForm : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly TabControl _tabs;
    private readonly TabPage _tabEditor;
    private readonly TabPage _tabList;

    // Editor fields
    private readonly TextBox _txtAlias;
    private readonly TextBox _txtRutBody;
    private readonly TextBox _txtRutDv;
    private readonly Label _lblRutStatus;
    private readonly ComboBox _cmbNivel;
    private readonly Button _btnApplyPreset;
    private readonly TextBox _txtPass1;
    private readonly TextBox _txtPass2;
    private readonly TextBox _txtNombres;
    private readonly TextBox _txtApePat;
    private readonly TextBox _txtApeMat;
    private readonly TextBox _txtFono;
    private readonly TextBox _txtMail;
    private readonly CheckBox _chkSolicitante;
    private readonly CheckBox _chkActivo;

    // Granular Permission Checkboxes (exact fixed mapping!)
    private readonly CheckBox _chkM1;
    private readonly CheckBox _chkTab01IngresoOt;
    private readonly CheckBox _chkTab0Vbs;
    private readonly CheckBox _chkTab0Vbj;
    private readonly CheckBox _chkTab02BusquedaOt;
    private readonly CheckBox _chkTab03ImpresionOt;
    private readonly CheckBox _chkM2;
    private readonly CheckBox _chkTab10IngresoFac;
    private readonly CheckBox _chkTab11BusquedaFac;
    private readonly CheckBox _chkM3Mantenedor;
    private readonly CheckBox _chkM4;
    private readonly CheckBox _chkTab20IngresoUs;
    private readonly CheckBox _chkTab21ListadoUs;

    private readonly Button _btnSaveNew;
    private readonly Button _btnUpdate;
    private readonly Button _btnDelete;
    private readonly Button _btnClear;

    // List tab
    private readonly TextBox _txtSearchUsers;
    private readonly DataGridView _gridUsers;
    private IReadOnlyList<User> _users = [];
    private string? _editingUserId;

    public UsersForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        _tabs = new TabControl { Dock = DockStyle.Fill, Font = CambiasoTheme.LabelFont, Padding = new Point(14, 6) };

        // --- TAB 0: INGRESO / EDICIÓN DE USUARIOS ---
        _tabEditor = new TabPage("Ingreso / Edición de Usuarios") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };

        _txtAlias = new TextBox { Width = 200, MaxLength = 30, PlaceholderText = "Ej. J Ramos" };
        _txtRutBody = new TextBox { Width = 86, MaxLength = 9, PlaceholderText = "12345678" };
        _txtRutBody.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };
        _txtRutBody.TextChanged += (_, _) => UpdateRutBadge();

        _txtRutDv = new TextBox { Width = 28, MaxLength = 1, CharacterCasing = CharacterCasing.Upper };
        _txtRutDv.TextChanged += (_, _) => UpdateRutBadge();

        _lblRutStatus = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, Margin = new Padding(4, 6, 0, 0) };

        _cmbNivel = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
        _cmbNivel.DataSource = Enum.GetValues<UserRoleLevel>();

        _btnApplyPreset = CambiasoTheme.CreateButton("Aplicar Perfil", AppIcon.Template, ButtonTone.Secondary, _toolTip, "Cargar permisos recomendados según el nivel seleccionado", width: 138, height: 28);
        _btnApplyPreset.Click += (_, _) => ApplySelectedRolePreset();

        _txtPass1 = new TextBox { Width = 200, UseSystemPasswordChar = true, PlaceholderText = "Mín. 6 caracteres" };
        _txtPass2 = new TextBox { Width = 200, UseSystemPasswordChar = true, PlaceholderText = "Repetir contraseña" };
        _txtNombres = new TextBox { Width = 180 };
        _txtApePat = new TextBox { Width = 170 };
        _txtApeMat = new TextBox { Width = 170 };
        _txtFono = new TextBox { Width = 170 };
        _txtMail = new TextBox { Width = 240 };
        _chkSolicitante = new CheckBox { Text = "Es solicitante de trabajos (sincroniza catálogo)", AutoSize = true };
        _chkActivo = new CheckBox { Text = "Cuenta activa", Checked = true, AutoSize = true };

        // Permissions checkboxes
        _chkM1 = new CheckBox { Text = "M1 — Menú Órdenes de Trabajo", Font = CambiasoTheme.LabelFont, AutoSize = true };
        _chkTab01IngresoOt = new CheckBox { Text = "Ingreso y edición de OT", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab0Vbs = new CheckBox { Text = "VB Supervisor (VBS)", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab0Vbj = new CheckBox { Text = "VB Jefatura (VBJ)", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab02BusquedaOt = new CheckBox { Text = "Búsqueda y exportación de OT", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab03ImpresionOt = new CheckBox { Text = "Generar / Imprimir OT", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkM1.CheckedChanged += (_, _) => SyncChildCheckboxes(_chkM1, _chkTab01IngresoOt, _chkTab0Vbs, _chkTab0Vbj, _chkTab02BusquedaOt, _chkTab03ImpresionOt);

        _chkM2 = new CheckBox { Text = "M2 — Menú Facturas y Guías", Font = CambiasoTheme.LabelFont, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
        _chkTab10IngresoFac = new CheckBox { Text = "Ingreso / Localización de guías y facturas", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab11BusquedaFac = new CheckBox { Text = "Búsqueda de facturas", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkM2.CheckedChanged += (_, _) => SyncChildCheckboxes(_chkM2, _chkTab10IngresoFac, _chkTab11BusquedaFac);

        _chkM3Mantenedor = new CheckBox { Text = "M3 — Mantenedores (Proveedores, Solicitantes, Activos)", Font = CambiasoTheme.LabelFont, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };

        _chkM4 = new CheckBox { Text = "M4 — Menú Usuarios y Permisos", Font = CambiasoTheme.LabelFont, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
        _chkTab20IngresoUs = new CheckBox { Text = "Crear / Modificar / Eliminar usuarios", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab21ListadoUs = new CheckBox { Text = "Listar e imprimir usuarios", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkM4.CheckedChanged += (_, _) => SyncChildCheckboxes(_chkM4, _chkTab20IngresoUs, _chkTab21ListadoUs);

        _btnSaveNew = CambiasoTheme.CreateButton("Guardar Nuevo", AppIcon.Save, ButtonTone.Primary, _toolTip, "Crear nuevo usuario y asignar permisos", width: 148);
        _btnSaveNew.Click += (_, _) => CreateUser();

        _btnUpdate = CambiasoTheme.CreateButton("Modificar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Guardar cambios en el usuario seleccionado", width: 124);
        _btnUpdate.Enabled = false;
        _btnUpdate.Click += (_, _) => UpdateUser();

        _btnDelete = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar usuario seleccionado", width: 118);
        _btnDelete.Enabled = false;
        _btnDelete.Click += (_, _) => DeleteUser();

        _btnClear = CambiasoTheme.CreateButton("Limpiar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Limpiar formulario para crear nuevo usuario", width: 114);
        _btnClear.Click += (_, _) => ClearForm();

        BuildEditorLayout();

        // --- TAB 1: LISTADO DE USUARIOS ---
        _tabList = new TabPage("Listado de Usuarios") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtSearchUsers = new TextBox { Width = 260, PlaceholderText = "Buscar por alias, nombre o RUT..." };
        _txtSearchUsers.TextChanged += (_, _) => LoadUsersGrid();

        _gridUsers = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridUsers);
        _gridUsers.CellDoubleClick += (_, _) => LoadSelectedUserIntoEditor();

        BuildListLayout();

        if (_session.Permisos.IngresoUsuarios)
        {
            _tabs.TabPages.Add(_tabEditor);
        }

        if (_session.Permisos.ListadoUsuarios)
        {
            _tabs.TabPages.Add(_tabList);
        }

        Controls.Add(_tabs);
        ClearForm();
        LoadUsersGrid();
    }

    private void BuildEditorLayout()
    {
        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        TableLayoutPanel cols = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        cols.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        GroupBox grpUser = CambiasoTheme.CreateGroupBox("Datos del Usuario");
        grpUser.Dock = DockStyle.Fill;
        TableLayoutPanel uTbl = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 7,
            Padding = new Padding(6)
        };
        uTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        uTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        uTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        uTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 7; r++)
        {
            uTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        }

        _txtAlias.Dock = DockStyle.Fill;
        _cmbNivel.Dock = DockStyle.Fill;
        _txtPass1.Dock = DockStyle.Fill;
        _txtPass2.Dock = DockStyle.Fill;
        _txtNombres.Dock = DockStyle.Fill;
        _txtApePat.Dock = DockStyle.Fill;
        _txtApeMat.Dock = DockStyle.Fill;
        _txtFono.Dock = DockStyle.Fill;
        _txtMail.Dock = DockStyle.Fill;

        FlowLayoutPanel rutBox = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        rutBox.Controls.AddRange([_txtRutBody, new Label { Text = "-", AutoSize = true, Margin = new Padding(2, 5, 2, 0), Font = CambiasoTheme.LabelFont }, _txtRutDv, _lblRutStatus]);

        uTbl.Controls.Add(FieldLbl("Código / Alias:"), 0, 0);
        uTbl.Controls.Add(_txtAlias, 1, 0);
        uTbl.Controls.Add(FieldLbl("RUT:"), 2, 0);
        uTbl.Controls.Add(rutBox, 3, 0);

        uTbl.Controls.Add(FieldLbl("Nivel / Perfil:"), 0, 1);
        uTbl.Controls.Add(_cmbNivel, 1, 1);
        uTbl.Controls.Add(_btnApplyPreset, 2, 1);
        uTbl.SetColumnSpan(_btnApplyPreset, 2);

        uTbl.Controls.Add(FieldLbl("Contraseña:"), 0, 2);
        uTbl.Controls.Add(_txtPass1, 1, 2);
        uTbl.Controls.Add(FieldLbl("Confirmar:"), 2, 2);
        uTbl.Controls.Add(_txtPass2, 3, 2);

        uTbl.Controls.Add(FieldLbl("Nombres:"), 0, 3);
        uTbl.Controls.Add(_txtNombres, 1, 3);
        uTbl.Controls.Add(FieldLbl("Ap. Paterno:"), 2, 3);
        uTbl.Controls.Add(_txtApePat, 3, 3);

        uTbl.Controls.Add(FieldLbl("Ap. Materno:"), 0, 4);
        uTbl.Controls.Add(_txtApeMat, 1, 4);
        uTbl.Controls.Add(FieldLbl("Teléfono:"), 2, 4);
        uTbl.Controls.Add(_txtFono, 3, 4);

        uTbl.Controls.Add(FieldLbl("Correo:"), 0, 5);
        uTbl.Controls.Add(_txtMail, 1, 5);
        uTbl.SetColumnSpan(_txtMail, 3);

        FlowLayoutPanel flagsBox = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        flagsBox.Controls.AddRange([_chkSolicitante, _chkActivo]);
        uTbl.Controls.Add(flagsBox, 0, 6);
        uTbl.SetColumnSpan(flagsBox, 4);

        grpUser.Controls.Add(uTbl);

        GroupBox grpPerms = CambiasoTheme.CreateGroupBox("Permisos por Módulo y Pestaña (RBAC)");
        grpPerms.Dock = DockStyle.Fill;
        Panel permsScroll = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };
        TableLayoutPanel pf = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        pf.Controls.AddRange(
        [
            _chkM1, _chkTab01IngresoOt, _chkTab0Vbs, _chkTab0Vbj, _chkTab02BusquedaOt, _chkTab03ImpresionOt,
            _chkM2, _chkTab10IngresoFac, _chkTab11BusquedaFac,
            _chkM3Mantenedor,
            _chkM4, _chkTab20IngresoUs, _chkTab21ListadoUs
        ]);
        permsScroll.Controls.Add(pf);
        grpPerms.Controls.Add(permsScroll);

        cols.Controls.Add(grpUser, 0, 0);
        cols.Controls.Add(grpPerms, 1, 0);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnSaveNew, _btnUpdate, _btnDelete, _btnClear]);

        root.Controls.Add(cols, 0, 0);
        root.Controls.Add(footer, 0, 1);
        _tabEditor.Controls.Add(root);
    }

    private void BuildListLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel topBar = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 4) };
        Button btnEdit = CambiasoTheme.CreateButton("Editar Seleccionado", AppIcon.Edit, ButtonTone.Primary, _toolTip, "Cargar usuario en el editor", width: 166);
        btnEdit.Enabled = _session.Permisos.IngresoUsuarios;
        btnEdit.Click += (_, _) => LoadSelectedUserIntoEditor();

        Button btnPrint = CambiasoTheme.CreateButton("Vista Previa / Imprimir", AppIcon.Print, ButtonTone.Secondary, _toolTip, "Imprimir listado de usuarios", width: 184);
        btnPrint.Click += (_, _) => PrintUsersList();

        Button btnExcel = CambiasoTheme.CreateButton("Exportar Excel", AppIcon.Excel, ButtonTone.Accent, _toolTip, "Exportar usuarios a archivo Excel (.xlsx)", width: 144);
        btnExcel.Click += (_, _) => ExportUsersExcel();

        topBar.Controls.AddRange([Wrap("Buscar:", _txtSearchUsers), btnEdit, btnPrint, btnExcel]);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Usuarios Registrados (doble clic para editar)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridUsers);

        layout.Controls.Add(topBar, 0, 0);
        layout.Controls.Add(grpGrid, 0, 1);
        _tabList.Controls.Add(layout);
    }

    private void UpdateRutBadge()
    {
        if (string.IsNullOrWhiteSpace(_txtRutBody.Text) && string.IsNullOrWhiteSpace(_txtRutDv.Text))
        {
            _lblRutStatus.Text = string.Empty;
            return;
        }

        bool valid = RutValidator.IsValid(_txtRutBody.Text, _txtRutDv.Text);
        _lblRutStatus.Text = valid ? "RUT válido" : "RUT inválido";
        _lblRutStatus.ForeColor = valid ? CambiasoTheme.Success : CambiasoTheme.Danger;
    }

    private static void SyncChildCheckboxes(CheckBox parent, params CheckBox[] children)
    {
        foreach (CheckBox child in children)
        {
            child.Enabled = parent.Checked;
            if (!parent.Checked)
            {
                child.Checked = false;
            }
        }
    }

    private void ApplySelectedRolePreset()
    {
        UserRoleLevel level = _cmbNivel.SelectedItem is UserRoleLevel lvl ? lvl : UserRoleLevel.Usuario;
        ApplyPermissionsToUi(PermissionPresets.CreateForRole(level));
    }

    private void ApplyPermissionsToUi(UserPermissions p)
    {
        _chkM1.Checked = p.MenuOrdenesTrabajo;
        SyncChildCheckboxes(_chkM1, _chkTab01IngresoOt, _chkTab0Vbs, _chkTab0Vbj, _chkTab02BusquedaOt, _chkTab03ImpresionOt);
        _chkTab01IngresoOt.Checked = p.IngresoOt;
        _chkTab0Vbs.Checked = p.VistoBuenoSupervisor;
        _chkTab0Vbj.Checked = p.VistoBuenoJefatura;
        _chkTab02BusquedaOt.Checked = p.BusquedaOt;
        _chkTab03ImpresionOt.Checked = p.ImpresionOt;

        _chkM2.Checked = p.MenuFacturas;
        SyncChildCheckboxes(_chkM2, _chkTab10IngresoFac, _chkTab11BusquedaFac);
        _chkTab10IngresoFac.Checked = p.IngresoFacturas;
        _chkTab11BusquedaFac.Checked = p.BusquedaFacturas;

        _chkM3Mantenedor.Checked = p.MenuMantenedor;

        _chkM4.Checked = p.MenuUsuarios;
        SyncChildCheckboxes(_chkM4, _chkTab20IngresoUs, _chkTab21ListadoUs);
        _chkTab20IngresoUs.Checked = p.IngresoUsuarios;
        _chkTab21ListadoUs.Checked = p.ListadoUsuarios;
    }

    private User BuildUserFromForm() => new()
    {
        IdUsuario = _editingUserId ?? RutValidator.Normalize(_txtRutBody.Text + _txtRutDv.Text),
        AliasUsuario = _txtAlias.Text.Trim(),
        NombreUsuario = _txtNombres.Text.Trim(),
        ApePatUsuario = _txtApePat.Text.Trim(),
        ApeMatUsuario = _txtApeMat.Text.Trim(),
        Nivel = _cmbNivel.SelectedItem is UserRoleLevel lvl ? lvl : UserRoleLevel.Usuario,
        FonoUsuario = _txtFono.Text.Trim(),
        MailUsuario = _txtMail.Text.Trim(),
        EsSolicitante = _chkSolicitante.Checked,
        Activo = _chkActivo.Checked,
        Permisos = new UserPermissions
        {
            MenuOrdenesTrabajo = _chkM1.Checked,
            IngresoOt = _chkTab01IngresoOt.Checked,
            VistoBuenoSupervisor = _chkTab0Vbs.Checked,
            VistoBuenoJefatura = _chkTab0Vbj.Checked,
            BusquedaOt = _chkTab02BusquedaOt.Checked,
            ImpresionOt = _chkTab03ImpresionOt.Checked,
            MenuFacturas = _chkM2.Checked,
            IngresoFacturas = _chkTab10IngresoFac.Checked,
            BusquedaFacturas = _chkTab11BusquedaFac.Checked,
            MenuMantenedor = _chkM3Mantenedor.Checked,
            MenuUsuarios = _chkM4.Checked,
            IngresoUsuarios = _chkTab20IngresoUs.Checked,
            ListadoUsuarios = _chkTab21ListadoUs.Checked
        }
    };

    private void CreateUser()
    {
        try
        {
            User user = BuildUserFromForm();
            _ctx.UserManagementService.CreateUser(_session, user, _txtPass1.Text, _txtPass2.Text);
            CambiasoTheme.ShowInfo(this, $"Usuario '{user.AliasUsuario}' creado correctamente.");
            ClearForm();
            LoadUsersGrid();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void UpdateUser()
    {
        if (string.IsNullOrEmpty(_editingUserId))
        {
            return;
        }

        try
        {
            User user = BuildUserFromForm();
            _ctx.UserManagementService.UpdateUser(_session, user, _txtPass1.Text, _txtPass2.Text);
            CambiasoTheme.ShowInfo(this, $"Usuario '{user.AliasUsuario}' actualizado correctamente.");
            ClearForm();
            LoadUsersGrid();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void DeleteUser()
    {
        if (string.IsNullOrEmpty(_editingUserId))
        {
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Eliminar permanentemente al usuario '{_txtAlias.Text}'?"))
        {
            return;
        }

        try
        {
            _ctx.UserManagementService.DeleteUser(_session, _editingUserId);
            CambiasoTheme.ShowInfo(this, "Usuario eliminado correctamente.");
            ClearForm();
            LoadUsersGrid();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void ClearForm()
    {
        _editingUserId = null;
        _txtAlias.Clear();
        _txtRutBody.Clear();
        _txtRutDv.Clear();
        _txtRutBody.ReadOnly = false;
        _txtRutDv.ReadOnly = false;
        _cmbNivel.SelectedItem = UserRoleLevel.Usuario;
        _txtPass1.Clear();
        _txtPass2.Clear();
        _txtNombres.Clear();
        _txtApePat.Clear();
        _txtApeMat.Clear();
        _txtFono.Clear();
        _txtMail.Clear();
        _chkSolicitante.Checked = false;
        _chkActivo.Checked = true;
        ApplySelectedRolePreset();
        _btnSaveNew.Enabled = true;
        _btnUpdate.Enabled = false;
        _btnDelete.Enabled = false;
        UpdateRutBadge();
    }

    private void LoadUsersGrid()
    {
        _users = _ctx.UserManagementService.GetAll(_session, _txtSearchUsers.Text);
        _gridUsers.DataSource = _users.Select(u => new
        {
            RUT = RutValidator.Format(u.IdUsuario),
            Alias = u.AliasUsuario,
            Nombre = u.NombreCompleto,
            Nivel = u.Nivel.ToString(),
            Teléfono = u.FonoUsuario,
            Correo = u.MailUsuario,
            Solicitante = u.EsSolicitante ? "Sí" : "No",
            Estado = u.Activo ? "Activo" : "Inactivo"
        }).ToList();
    }

    private void LoadSelectedUserIntoEditor()
    {
        if (!_session.Permisos.IngresoUsuarios || _gridUsers.CurrentRow is null)
        {
            return;
        }

        int idx = _gridUsers.CurrentRow.Index;
        if (idx < 0 || idx >= _users.Count)
        {
            return;
        }

        User u = _users[idx];
        _editingUserId = u.IdUsuario;
        (string body, string dv) = RutValidator.Split(u.IdUsuario);
        _txtAlias.Text = u.AliasUsuario;
        _txtRutBody.Text = body;
        _txtRutDv.Text = dv;
        _txtRutBody.ReadOnly = true;
        _txtRutDv.ReadOnly = true;
        _cmbNivel.SelectedItem = u.Nivel;
        _txtPass1.Clear();
        _txtPass2.Clear();
        _txtNombres.Text = u.NombreUsuario;
        _txtApePat.Text = u.ApePatUsuario;
        _txtApeMat.Text = u.ApeMatUsuario;
        _txtFono.Text = u.FonoUsuario;
        _txtMail.Text = u.MailUsuario;
        _chkSolicitante.Checked = u.EsSolicitante;
        _chkActivo.Checked = u.Activo;
        ApplyPermissionsToUi(u.Permisos);

        _btnSaveNew.Enabled = false;
        _btnUpdate.Enabled = true;
        _btnDelete.Enabled = true;
        _tabs.SelectedTab = _tabEditor;
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

    private static Label FieldLbl(string text)
    {
        Label lbl = CambiasoTheme.CreateFieldLabel(text);
        lbl.Anchor = AnchorStyles.Left;
        return lbl;
    }

    private static FlowLayoutPanel Wrap(string label, Control ctl)
    {
        FlowLayoutPanel p = new() { AutoSize = true, Margin = new Padding(4, 2, 10, 2), WrapContents = false };
        Label lbl = CambiasoTheme.CreateFieldLabel(label);
        lbl.Margin = new Padding(0, 5, 6, 0);
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
