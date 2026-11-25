using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Panel CRUD completo para administrar Perfiles de Usuarios (roles base y personalizados).
/// Sincroniza al instante la lista de perfiles con el editor y filtros de Usuarios.
/// </summary>
public sealed class UserProfileManagerControl : UserControl
{
    private enum EditMode
    {
        View,
        Create,
        Edit
    }

    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private IReadOnlyList<UserProfile> _profiles = [];
    private UserProfile? _selectedProfile;
    private EditMode _mode = EditMode.View;
    private bool _suppressSelection;
    private bool _syncSubscribed;

    public event EventHandler? ProfilesChanged;

    internal TextBox TxtId { get; }
    internal TextBox TxtNombre { get; }
    internal TextBox TxtDescripcion { get; }
    internal CheckBox ChkActivo { get; }
    internal Label LblTipoBadge { get; }

    internal CheckBox ChkM1Ot { get; }
    internal CheckBox ChkIngresoOt { get; }
    internal CheckBox ChkBusquedaOt { get; }
    internal CheckBox ChkM2Mantenedor { get; }

    internal Button BtnNuevo { get; }
    internal Button BtnGuardar { get; }
    internal Button BtnEditar { get; }
    internal Button BtnActualizar { get; }
    internal Button BtnToggleActive { get; }
    internal Button BtnEliminar { get; }
    internal Button BtnCancelar { get; }
    internal Button BtnRefrescar { get; }
    internal DataGridView GridProfiles { get; }

    public UserProfileManagerControl(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;

        Name = "PerfilesUsuariosControl";
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;
        Padding = new Padding(6);

        TxtId = new TextBox
        {
            Text = "(Automático)",
            ReadOnly = true,
            BackColor = CambiasoTheme.Surface,
            ForeColor = CambiasoTheme.TextMuted,
            Dock = DockStyle.Fill
        };
        TxtNombre = new TextBox
        {
            Name = "txtNombrePerfil",
            MaxLength = 40,
            PlaceholderText = "Ej. Planificador, Inspector Mantención...",
            Dock = DockStyle.Fill
        };
        TxtDescripcion = new TextBox
        {
            Name = "txtDescripcionPerfil",
            MaxLength = 180,
            PlaceholderText = "Descripción del alcance y responsabilidades del perfil...",
            Dock = DockStyle.Fill
        };
        ChkActivo = new CheckBox
        {
            Name = "chkPerfilActivo",
            Text = "Perfil activo",
            Checked = true,
            AutoSize = true,
            Margin = new Padding(0, 4, 8, 2)
        };
        LblTipoBadge = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Primary,
            Margin = new Padding(8, 6, 0, 2),
            Text = "Tipo: Personalizado"
        };

        ChkM1Ot = new CheckBox { Text = "M1 — Trabajos Externos (OT)", AutoSize = true, Font = CambiasoTheme.LabelFont, Checked = true };
        ChkIngresoOt = new CheckBox { Text = "Crear / Editar / Eliminar OT", AutoSize = true, Checked = true, Margin = new Padding(18, 2, 10, 2) };
        ChkBusquedaOt = new CheckBox { Text = "Búsqueda, Consulta y Exportación", AutoSize = true, Checked = true, Margin = new Padding(0, 2, 10, 2) };
        ChkM2Mantenedor = new CheckBox { Text = "M2 — Acceso a Mantenedores (consulta)", AutoSize = true, Font = CambiasoTheme.LabelFont };

        ChkM1Ot.CheckedChanged += (_, _) => UpdatePermissionCheckboxes();

        BtnNuevo = CambiasoTheme.CreateButton("Nuevo Perfil", AppIcon.Add, ButtonTone.Primary, _toolTip, "Crear un nuevo perfil de usuario personalizado", width: 126, height: 30);
        BtnNuevo.Click += (_, _) => EnterCreateMode();

        BtnGuardar = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar nuevo perfil", width: 106, height: 30);
        BtnGuardar.Click += (_, _) => SaveNewProfile();

        BtnEditar = CambiasoTheme.CreateButton("Editar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Editar el perfil seleccionado", width: 98, height: 30);
        BtnEditar.Click += (_, _) => EnterEditMode();

        BtnActualizar = CambiasoTheme.CreateButton("Actualizar", AppIcon.Check, ButtonTone.Accent, _toolTip, "Guardar cambios del perfil", width: 112, height: 30);
        BtnActualizar.Click += (_, _) => UpdateSelectedProfile();

        BtnToggleActive = CambiasoTheme.CreateButton("Activar / Desact.", AppIcon.Power, ButtonTone.Secondary, _toolTip, "Activar o desactivar perfil personalizado", width: 146, height: 30);
        BtnToggleActive.Click += (_, _) => ToggleProfileActive();

        BtnEliminar = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar perfil personalizado sin usuarios asignados", width: 106, height: 30);
        BtnEliminar.Click += (_, _) => DeleteSelectedProfile();

        BtnCancelar = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cancelar edición actual", width: 108, height: 30);
        BtnCancelar.Click += (_, _) => CancelEdit();

        BtnRefrescar = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar perfiles desde SQLite", width: 110, height: 30);
        BtnRefrescar.Click += (_, _) => Reload();

        GridProfiles = new DataGridView { Name = "gridPerfilesUsuarios", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(GridProfiles);
        GridProfiles.SelectionChanged += (_, _) => OnGridSelectionChanged();
        GridProfiles.CellDoubleClick += (_, _) => EnterEditMode();
        GridProfiles.CellFormatting += OnCellFormatting;
        ContextMenuHelper.AttachToGrid(GridProfiles, idx =>
        {
            UserProfile? prof = GridBindingHelper.GetRowSource<UserProfile>(GridProfiles, idx);
            if (prof is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Editar perfil", AppIcon.Edit, CambiasoTheme.Primary, EnterEditMode),
                ContextMenuHelper.Item(
                    prof.Activo ? "Desactivar perfil" : "Activar perfil",
                    prof.Activo ? AppIcon.Ban : AppIcon.Power,
                    prof.Activo ? CambiasoTheme.Warning : CambiasoTheme.Success,
                    ToggleProfileActive,
                    enabled: !prof.EsSistema),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Eliminar perfil", AppIcon.Delete, CambiasoTheme.Danger, DeleteSelectedProfile, enabled: !prof.EsSistema && prof.CantidadUsuarios == 0)
            ];
        });

        BuildLayout();

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;

        Reload();
        ApplyMode(EditMode.View);
    }

    public void Reload(int? selectIdNivel = null)
    {
        _suppressSelection = true;
        try
        {
            _profiles = _ctx.UserProfileService.GetAll(_session, includeInactive: true);
            GridProfiles.DataSource = new SortableBindingList<ProfileGridRow>(
                _profiles.Select(p => new ProfileGridRow(p)));

            if (GridProfiles.Columns.Count >= 7)
            {
                GridBindingHelper.ConfigureColumn(GridProfiles, 0, "ID", 6);
                GridBindingHelper.ConfigureColumn(GridProfiles, 1, "Perfil", 16);
                GridBindingHelper.ConfigureColumn(GridProfiles, 2, "Descripción", 28);
                GridBindingHelper.ConfigureColumn(GridProfiles, 3, "Tipo", 11);
                GridBindingHelper.ConfigureColumn(GridProfiles, 4, "Usuarios", 8);
                GridBindingHelper.ConfigureColumn(GridProfiles, 5, "Permisos por Defecto", 23);
                GridBindingHelper.ConfigureColumn(GridProfiles, 6, "Estado", 8);
            }

            int targetIdx = -1;
            int targetId = selectIdNivel ?? _selectedProfile?.IdNivel ?? 0;
            if (targetId > 0)
            {
                for (int i = 0; i < GridProfiles.Rows.Count; i++)
                {
                    if (GridBindingHelper.GetRowSource<UserProfile>(GridProfiles, i)?.IdNivel == targetId)
                    {
                        targetIdx = i;
                        break;
                    }
                }
            }
            else if (GridProfiles.Rows.Count > 0)
            {
                targetIdx = 0;
            }

            if (targetIdx >= 0 && targetIdx < GridProfiles.Rows.Count)
            {
                GridProfiles.ClearSelection();
                GridProfiles.Rows[targetIdx].Selected = true;
                GridProfiles.CurrentCell = GridProfiles.Rows[targetIdx].Cells[1];
                _selectedProfile = GridBindingHelper.GetRowSource<UserProfile>(GridProfiles, targetIdx);
            }
            else
            {
                _selectedProfile = null;
            }

            PopulateEditor(_selectedProfile);
        }
        finally
        {
            _suppressSelection = false;
        }

        if (_mode == EditMode.View)
        {
            ApplyMode(EditMode.View);
        }
    }

    private void BuildLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        GroupBox grpForm = CambiasoTheme.CreateGroupBox("DATOS Y PERMISOS POR DEFECTO DEL PERFIL DE USUARIO");
        grpForm.Dock = DockStyle.Fill;

        TableLayoutPanel tbl = new() { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 4, Padding = new Padding(6, 4, 6, 4) };
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        tbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        tbl.Controls.Add(MakeFieldLabel("ID:"), 0, 0);
        tbl.Controls.Add(TxtId, 1, 0);
        tbl.Controls.Add(MakeFieldLabel("Nombre *:"), 2, 0);
        tbl.Controls.Add(TxtNombre, 3, 0);
        FlowLayoutPanel statusRow = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        statusRow.Controls.AddRange([ChkActivo, LblTipoBadge]);
        tbl.Controls.Add(MakeFieldLabel("Estado:"), 4, 0);
        tbl.Controls.Add(statusRow, 5, 0);

        tbl.Controls.Add(MakeFieldLabel("Descripción:"), 0, 1);
        tbl.Controls.Add(TxtDescripcion, 1, 1);
        tbl.SetColumnSpan(TxtDescripcion, 5);

        FlowLayoutPanel permRow1 = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        permRow1.Controls.AddRange([ChkM1Ot, ChkIngresoOt, ChkBusquedaOt]);
        tbl.Controls.Add(MakeFieldLabel("Módulo OT:"), 0, 2);
        tbl.Controls.Add(permRow1, 1, 2);
        tbl.SetColumnSpan(permRow1, 5);

        FlowLayoutPanel permRow2 = new() { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        Label lblAdminNote = new()
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(14, 5, 0, 0),
            Text = "(El acceso al módulo Gestión de Usuarios y Perfiles permanece exclusivo del Administrador #4)"
        };
        permRow2.Controls.AddRange([ChkM2Mantenedor, lblAdminNote]);
        tbl.Controls.Add(MakeFieldLabel("Otros:"), 0, 3);
        tbl.Controls.Add(permRow2, 1, 3);
        tbl.SetColumnSpan(permRow2, 5);

        grpForm.Controls.Add(tbl);

        FlowLayoutPanel actions = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 7, 2, 4), WrapContents = false };
        actions.Controls.AddRange([BtnNuevo, BtnGuardar, BtnEditar, BtnActualizar, BtnToggleActive, BtnEliminar, BtnCancelar, BtnRefrescar]);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Catálogo de Perfiles Registrados (Sistema + Personalizados)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(GridProfiles);

        layout.Controls.Add(grpForm, 0, 0);
        layout.Controls.Add(actions, 0, 1);
        layout.Controls.Add(grpGrid, 0, 2);
        Controls.Add(layout);
    }

    private void OnGridSelectionChanged()
    {
        if (_suppressSelection || _mode != EditMode.View)
        {
            return;
        }

        _selectedProfile = GridBindingHelper.GetRowSource<UserProfile>(GridProfiles);
        PopulateEditor(_selectedProfile);
        ApplyMode(EditMode.View);
    }

    private void PopulateEditor(UserProfile? profile)
    {
        if (profile is null)
        {
            TxtId.Text = "(Automático)";
            TxtNombre.Clear();
            TxtDescripcion.Clear();
            ChkActivo.Checked = true;
            LblTipoBadge.Text = "Tipo: Personalizado";
            ChkM1Ot.Checked = true;
            ChkIngresoOt.Checked = true;
            ChkBusquedaOt.Checked = true;
            ChkM2Mantenedor.Checked = false;
            return;
        }

        TxtId.Text = profile.IdNivel.ToString();
        TxtNombre.Text = profile.Nombre;
        TxtDescripcion.Text = profile.Descripcion;
        ChkActivo.Checked = profile.Activo;
        LblTipoBadge.Text = profile.EsSistema ? "Tipo: Base del Sistema" : "Tipo: Personalizado";
        LblTipoBadge.ForeColor = profile.EsSistema ? CambiasoTheme.PrimaryDark : CambiasoTheme.Accent;

        UserPermissions p = profile.PermisosPorDefecto;
        ChkM1Ot.Checked = p.MenuOrdenesTrabajo;
        ChkIngresoOt.Checked = p.IngresoOt;
        ChkBusquedaOt.Checked = p.BusquedaOt;
        ChkM2Mantenedor.Checked = p.MenuMantenedor;
    }

    private void ApplyMode(EditMode mode)
    {
        _mode = mode;
        bool hasSelection = _selectedProfile is not null;
        bool isCreate = mode == EditMode.Create;
        bool isEdit = mode == EditMode.Edit;
        bool editable = isCreate || isEdit;
        bool isSystem = _selectedProfile?.EsSistema == true && isEdit;
        bool isAdminProfile = _selectedProfile?.EsAdministrador == true && isEdit;

        TxtNombre.ReadOnly = !editable || isSystem;
        TxtDescripcion.ReadOnly = !editable;
        ChkActivo.Enabled = editable && !isSystem;

        UpdatePermissionCheckboxes();
        if (isAdminProfile)
        {
            ChkM1Ot.Enabled = false;
            ChkIngresoOt.Enabled = false;
            ChkBusquedaOt.Enabled = false;
            ChkM2Mantenedor.Enabled = false;
        }

        BtnNuevo.Enabled = mode == EditMode.View;
        BtnGuardar.Enabled = isCreate;
        BtnEditar.Enabled = mode == EditMode.View && hasSelection;
        BtnActualizar.Enabled = isEdit;
        BtnToggleActive.Enabled = mode == EditMode.View && hasSelection && !_selectedProfile!.EsSistema;
        BtnEliminar.Enabled = mode == EditMode.View && hasSelection && !_selectedProfile!.EsSistema && _selectedProfile.CantidadUsuarios == 0;
        BtnCancelar.Enabled = editable;
        BtnRefrescar.Enabled = mode == EditMode.View;
    }

    private void UpdatePermissionCheckboxes()
    {
        bool editable = _mode is EditMode.Create or EditMode.Edit;
        bool isAdminProfile = _mode == EditMode.Edit && _selectedProfile?.EsAdministrador == true;
        bool canEdit = editable && !isAdminProfile;

        ChkM1Ot.Enabled = canEdit;
        bool m1 = ChkM1Ot.Checked;
        ChkIngresoOt.Enabled = canEdit && m1;
        ChkBusquedaOt.Enabled = canEdit && m1;
        if (!m1)
        {
            ChkIngresoOt.Checked = false;
            ChkBusquedaOt.Checked = false;
        }

        ChkM2Mantenedor.Enabled = canEdit;
    }

    internal void EnterCreateMode()
    {
        PopulateEditor(null);
        ApplyMode(EditMode.Create);
        TxtNombre.Focus();
    }

    internal void EnterEditMode()
    {
        if (_selectedProfile is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un perfil de la grilla para editar.");
            return;
        }

        PopulateEditor(_selectedProfile);
        ApplyMode(EditMode.Edit);
        if (TxtNombre.ReadOnly) TxtDescripcion.Focus();
        else TxtNombre.Focus();
    }

    private void SaveNewProfile()
    {
        try
        {
            UserProfile profile = BuildFromEditor(existingId: 0);
            int id = _ctx.UserProfileService.Create(_session, profile);
            CambiasoTheme.ShowInfo(this, $"Perfil '{profile.Nombre}' (ID {id}) creado correctamente.");
            ApplyMode(EditMode.View);
            Reload(id);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void UpdateSelectedProfile()
    {
        if (_selectedProfile is null)
        {
            return;
        }

        try
        {
            UserProfile updated = BuildFromEditor(_selectedProfile.IdNivel);
            updated.EsSistema = _selectedProfile.EsSistema;
            _ctx.UserProfileService.Update(_session, updated);
            CambiasoTheme.ShowInfo(this, $"Perfil '{updated.Nombre}' actualizado correctamente.");
            ApplyMode(EditMode.View);
            Reload(updated.IdNivel);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ToggleProfileActive()
    {
        if (_selectedProfile is null)
        {
            return;
        }

        bool nextState = !_selectedProfile.Activo;
        if (!CambiasoTheme.Confirm(this, $"¿Desea {(nextState ? "activar" : "desactivar")} el perfil '{_selectedProfile.Nombre}'?"))
        {
            return;
        }

        try
        {
            _ctx.UserProfileService.SetActive(_session, _selectedProfile.IdNivel, nextState);
            Reload(_selectedProfile.IdNivel);
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void DeleteSelectedProfile()
    {
        if (_selectedProfile is null)
        {
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Confirma que desea eliminar permanentemente el perfil '{_selectedProfile.Nombre}'?"))
        {
            return;
        }

        try
        {
            string removedName = _selectedProfile.Nombre;
            _ctx.UserProfileService.Delete(_session, _selectedProfile.IdNivel);
            CambiasoTheme.ShowInfo(this, $"Perfil '{removedName}' eliminado.");
            Reload();
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void CancelEdit()
    {
        PopulateEditor(_selectedProfile);
        ApplyMode(EditMode.View);
    }

    private UserProfile BuildFromEditor(int existingId) => new()
    {
        IdNivel = existingId,
        Nombre = TxtNombre.Text.Trim(),
        Descripcion = TxtDescripcion.Text.Trim(),
        Activo = ChkActivo.Checked,
        PermisosPorDefecto = new UserPermissions
        {
            MenuOrdenesTrabajo = ChkM1Ot.Checked,
            IngresoOt = ChkIngresoOt.Checked,
            BusquedaOt = ChkBusquedaOt.Checked,
            ImpresionOt = false,
            MenuMantenedor = ChkM2Mantenedor.Checked
        }
    };

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        UserProfile? prof = GridBindingHelper.GetRowSource<UserProfile>(GridProfiles, e.RowIndex);
        if (prof is null || e.CellStyle is null)
        {
            return;
        }

        if (e.ColumnIndex == 3)
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = prof.EsSistema ? CambiasoTheme.PrimaryDark : CambiasoTheme.Accent;
        }
        else if (e.ColumnIndex == 6)
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = prof.Activo ? CambiasoTheme.Success : CambiasoTheme.Danger;
        }
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & DataChangeKind.Users) == 0 || _mode != EditMode.View)
        {
            return;
        }

        void RefreshSafe()
        {
            if (!IsDisposed && _mode == EditMode.View)
            {
                Reload(_selectedProfile?.IdNivel);
            }
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshSafe);
        else if (!InvokeRequired) RefreshSafe();
    }

    private static Label MakeFieldLabel(string text) => new()
    {
        Text = text,
        Font = CambiasoTheme.LabelFont,
        ForeColor = CambiasoTheme.TextMuted,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 6, 0)
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_syncSubscribed)
            {
                _ctx.SyncHub.DataChanged -= OnSyncChanged;
                _syncSubscribed = false;
            }

            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class ProfileGridRow(UserProfile source) : IGridRowSource<UserProfile>
    {
        [Browsable(false)]
        public UserProfile Source { get; } = source;
        public int Id => Source.IdNivel;
        public string Nombre => Source.Nombre;
        public string Descripcion => Source.Descripcion;
        public string Tipo => Source.EsSistema ? "Sistema" : "Personalizado";
        public int Usuarios => Source.CantidadUsuarios;
        public string Permisos => FormatPermSummary(Source);
        public string Estado => Source.Activo ? "Activo" : "Inactivo";

        private static string FormatPermSummary(UserProfile p)
        {
            if (p.EsAdministrador)
            {
                return "Acceso Total (M1 + M2 + M3)";
            }

            List<string> parts = [];
            UserPermissions pr = p.PermisosPorDefecto;
            if (pr.MenuOrdenesTrabajo)
            {
                List<string> m1 = [];
                if (pr.IngresoOt) m1.Add("Ingreso");
                if (pr.BusquedaOt) m1.Add("Búsqueda");
                parts.Add(m1.Count > 0 ? $"OT ({string.Join("/", m1)})" : "OT");
            }
            if (pr.MenuMantenedor)
            {
                parts.Add("Mantenedores");
            }

            return parts.Count == 0 ? "Solo lectura Dashboard" : string.Join(" + ", parts);
        }
    }
}
