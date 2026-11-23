using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo administrativo seguro para restablecer la contraseña de un usuario seleccionado.
/// </summary>
public sealed class AdminResetPasswordDialog : Form
{
    private readonly UserManagementService _userService;
    private readonly UserSession _session;
    private readonly User _targetUser;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtNew;
    private readonly TextBox _txtConfirm;
    private readonly CheckBox _chkForceNextLogin;

    public AdminResetPasswordDialog(UserManagementService userService, UserSession session, User targetUser)
    {
        _userService = userService;
        _session = session;
        _targetUser = targetUser;

        Text = "Cambiar Contraseña de Usuario — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        ClientSize = new Size(456, 318);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 52, BackColor = CambiasoTheme.Primary };
        header.Controls.Add(new Label
        {
            Text = $"Gestión de Credenciales — {targetUser.AliasUsuario}",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 16)
        });

        Panel infoStrip = new()
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(16, 0, 16, 0)
        };
        infoStrip.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = $"ID: {targetUser.CodigoId}  |  {targetUser.NombreCompleto}  |  Perfil: {targetUser.Nivel}",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft
        });

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 12, 20, 10) };

        _txtNew = new TextBox { Location = new Point(20, 32), Width = 412, UseSystemPasswordChar = true, PlaceholderText = "Mínimo 6 caracteres" };
        _txtConfirm = new TextBox { Location = new Point(20, 90), Width = 412, UseSystemPasswordChar = true, PlaceholderText = "Repita la nueva contraseña" };

        CheckBox chkShow = new() { Text = "Mostrar contraseñas", Location = new Point(20, 122), AutoSize = true, Font = CambiasoTheme.SmallFont };
        chkShow.CheckedChanged += (_, _) =>
        {
            bool hide = !chkShow.Checked;
            _txtNew.UseSystemPasswordChar = hide;
            _txtConfirm.UseSystemPasswordChar = hide;
        };

        _chkForceNextLogin = new CheckBox
        {
            Text = "Solicitar cambio de contraseña en el próximo inicio de sesión",
            Location = new Point(20, 148),
            AutoSize = true,
            Checked = !string.Equals(session.Usuario.IdUsuario, targetUser.IdUsuario, StringComparison.OrdinalIgnoreCase),
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary
        };

        body.Controls.AddRange(
        [
            new Label { Text = "Nueva contraseña:", Font = CambiasoTheme.LabelFont, Location = new Point(20, 12), AutoSize = true },
            _txtNew,
            new Label { Text = "Confirmar nueva contraseña:", Font = CambiasoTheme.LabelFont, Location = new Point(20, 70), AutoSize = true },
            _txtConfirm,
            chkShow,
            _chkForceNextLogin
        ]);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 16, 10),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cancelar cambio de contraseña", width: 114);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Button btnSave = CambiasoTheme.CreateButton("Guardar Contraseña", AppIcon.Key, ButtonTone.Primary, _toolTip, "Aplicar nueva contraseña cifrada con PBKDF2-SHA256", width: 174);
        btnSave.Click += (_, _) => Submit();

        footer.Controls.Add(btnCancel);
        footer.Controls.Add(btnSave);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(infoStrip);
        Controls.Add(header);
    }

    private void Submit()
    {
        try
        {
            _userService.ChangeUserPassword(
                _session,
                _targetUser.IdUsuario,
                _txtNew.Text,
                _txtConfirm.Text,
                _chkForceNextLogin.Checked);

            CambiasoTheme.ShowInfo(this, $"Contraseña del usuario '{_targetUser.AliasUsuario}' actualizada correctamente.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
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
