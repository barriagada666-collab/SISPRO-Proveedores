using System.Drawing;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo de configuración granular de permisos RBAC por módulo y pestaña.
/// Garantiza que solo el perfil Administrador posea acceso al módulo Gestión de Usuarios.
/// </summary>
public sealed class UserPermissionsDialog : Form
{
    private readonly UserRoleLevel _roleLevel;
    private readonly bool _readOnly;
    private readonly ToolTip _toolTip = new();

    private readonly CheckBox _chkM1;
    private readonly CheckBox _chkTab01IngresoOt;
    private readonly CheckBox _chkTab02BusquedaOt;
    private readonly CheckBox _chkTab03ImpresionOt;
    private readonly CheckBox _chkM2Mantenedor;
    private readonly CheckBox _chkM3;
    private readonly CheckBox _chkTab20IngresoUs;
    private readonly CheckBox _chkTab21ListadoUs;

    public UserPermissions ResultPermissions { get; private set; }

    public UserPermissionsDialog(
        UserRoleLevel roleLevel,
        UserPermissions currentPermissions,
        bool readOnly,
        string? profileTitle = null)
    {
        _roleLevel = roleLevel;
        _readOnly = readOnly;
        ResultPermissions = ClonePermissions(currentPermissions);
        string titleName = string.IsNullOrWhiteSpace(profileTitle) ? roleLevel.ToString() : profileTitle.Trim();

        Text = $"Permisos del Perfil ({titleName}) — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        ClientSize = new Size(510, 368);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 50, BackColor = CambiasoTheme.Primary };
        header.Controls.Add(new Label
        {
            Text = $"Permisos RBAC — Perfil {titleName}",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 15)
        });

        Panel infoBanner = new() { Dock = DockStyle.Top, Height = 38, BackColor = CambiasoTheme.SubtleFill, Padding = new Padding(16, 0, 16, 0) };
        infoBanner.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = roleLevel == UserRoleLevel.Administrador
                ? "El perfil Administrador posee acceso total al sistema (protegido contra degradación)."
                : "El módulo Gestión de Usuarios (M3) está restringido exclusivamente al perfil Administrador.",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft
        });

        _chkM1 = new CheckBox { Text = "M1 — Menú Trabajos Externos (Órdenes de Trabajo)", Font = CambiasoTheme.LabelFont, AutoSize = true };
        _chkTab01IngresoOt = new CheckBox { Text = "Ingreso y edición de OT", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab02BusquedaOt = new CheckBox { Text = "Búsqueda y exportación de OT", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab03ImpresionOt = new CheckBox { Text = "Generar / Imprimir OT", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkM1.CheckedChanged += (_, _) => SyncChildCheckboxes(_chkM1, _chkTab01IngresoOt, _chkTab02BusquedaOt, _chkTab03ImpresionOt);

        _chkM2Mantenedor = new CheckBox { Text = "M2 — Mantenedores (Proveedores, Solicitantes y Activos)", Font = CambiasoTheme.LabelFont, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };

        _chkM3 = new CheckBox { Text = "M3 — Gestión de Usuarios, Solicitantes y Perfiles (Exclusivo Administrador)", Font = CambiasoTheme.LabelFont, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
        _chkTab20IngresoUs = new CheckBox { Text = "Crear / Modificar / Desactivar usuarios, perfiles y solicitantes", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };
        _chkTab21ListadoUs = new CheckBox { Text = "Consultar, filtrar e imprimir usuarios y firmas", AutoSize = true, Margin = new Padding(24, 2, 2, 2) };

        Panel body = new() { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(20, 12, 20, 12) };
        TableLayoutPanel list = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1 };
        list.Controls.AddRange(
        [
            _chkM1, _chkTab01IngresoOt, _chkTab02BusquedaOt, _chkTab03ImpresionOt,
            _chkM2Mantenedor,
            _chkM3, _chkTab20IngresoUs, _chkTab21ListadoUs
        ]);
        body.Controls.Add(list);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 16, 10),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnClose = CambiasoTheme.CreateButton(readOnly ? "Cerrar" : "Cancelar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cerrar ventana de permisos", width: 112);
        btnClose.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        footer.Controls.Add(btnClose);

        if (!readOnly && roleLevel != UserRoleLevel.Administrador)
        {
            Button btnApply = CambiasoTheme.CreateButton("Aplicar", AppIcon.Check, ButtonTone.Primary, _toolTip, "Aplicar cambios de permisos", width: 112);
            btnApply.Click += (_, _) =>
            {
                ResultPermissions = ReadFromUi();
                DialogResult = DialogResult.OK;
                Close();
            };

            Button btnPreset = CambiasoTheme.CreateButton("Restablecer Perfil", AppIcon.Template, ButtonTone.Accent, _toolTip, "Cargar plantilla estándar del perfil", width: 158);
            btnPreset.Click += (_, _) => LoadToUi(PermissionPresets.CreateForRole(_roleLevel));

            footer.Controls.Add(btnApply);
            footer.Controls.Add(btnPreset);
            AcceptButton = btnApply;
        }

        CancelButton = btnClose;

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(infoBanner);
        Controls.Add(header);

        LoadToUi(roleLevel == UserRoleLevel.Administrador ? PermissionPresets.CreateForRole(UserRoleLevel.Administrador) : currentPermissions);
        ApplyEditability();
    }

    private void ApplyEditability()
    {
        bool canEditNonAdmin = !_readOnly && _roleLevel != UserRoleLevel.Administrador;
        _chkM1.Enabled = canEditNonAdmin;
        _chkTab01IngresoOt.Enabled = canEditNonAdmin && _chkM1.Checked;
        _chkTab02BusquedaOt.Enabled = canEditNonAdmin && _chkM1.Checked;
        _chkTab03ImpresionOt.Enabled = canEditNonAdmin && _chkM1.Checked;

        _chkM2Mantenedor.Enabled = canEditNonAdmin;

        // M3 siempre bloqueado: true solo para Administrador, false para los demás perfiles
        _chkM3.Enabled = false;
        _chkTab20IngresoUs.Enabled = false;
        _chkTab21ListadoUs.Enabled = false;
    }

    private void SyncChildCheckboxes(CheckBox parent, params CheckBox[] children)
    {
        bool canEdit = !_readOnly && _roleLevel != UserRoleLevel.Administrador;
        foreach (CheckBox child in children)
        {
            child.Enabled = canEdit && parent.Checked;
            if (!parent.Checked)
            {
                child.Checked = false;
            }
        }
    }

    private void LoadToUi(UserPermissions p)
    {
        _chkM1.Checked = p.MenuOrdenesTrabajo;
        _chkTab01IngresoOt.Checked = p.IngresoOt;
        _chkTab02BusquedaOt.Checked = p.BusquedaOt;
        _chkTab03ImpresionOt.Checked = p.ImpresionOt;

        _chkM2Mantenedor.Checked = p.MenuMantenedor;

        bool isAdmin = _roleLevel == UserRoleLevel.Administrador;
        _chkM3.Checked = isAdmin;
        _chkTab20IngresoUs.Checked = isAdmin;
        _chkTab21ListadoUs.Checked = isAdmin;
    }

    private UserPermissions ReadFromUi() => new()
    {
        MenuOrdenesTrabajo = _chkM1.Checked,
        IngresoOt = _chkTab01IngresoOt.Checked,
        VistoBuenoSupervisor = false,
        VistoBuenoJefatura = false,
        BusquedaOt = _chkTab02BusquedaOt.Checked,
        ImpresionOt = _chkTab03ImpresionOt.Checked,
        MenuMantenedor = _chkM2Mantenedor.Checked,
        MenuUsuarios = _roleLevel == UserRoleLevel.Administrador,
        IngresoUsuarios = _roleLevel == UserRoleLevel.Administrador,
        ListadoUsuarios = _roleLevel == UserRoleLevel.Administrador
    };

    private static UserPermissions ClonePermissions(UserPermissions source) => new()
    {
        IdUsuario = source.IdUsuario,
        MenuOrdenesTrabajo = source.MenuOrdenesTrabajo,
        IngresoOt = source.IngresoOt,
        VistoBuenoSupervisor = source.VistoBuenoSupervisor,
        VistoBuenoJefatura = source.VistoBuenoJefatura,
        BusquedaOt = source.BusquedaOt,
        ImpresionOt = source.ImpresionOt,
        MenuMantenedor = source.MenuMantenedor,
        MenuUsuarios = source.MenuUsuarios,
        IngresoUsuarios = source.IngresoUsuarios,
        ListadoUsuarios = source.ListadoUsuarios
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
