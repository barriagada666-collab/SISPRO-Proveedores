using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Panel "Mi Clave" integrado dentro de Mantenedores para cambio seguro de contraseña personal.
/// </summary>
public sealed class MyPasswordControl : UserControl
{
    private readonly AuthService _authService;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly TextBox _txtCurrent;
    private readonly TextBox _txtNew;
    private readonly TextBox _txtConfirm;
    private readonly CheckBox _chkShow;
    private readonly Label _lblFeedback;

    internal Button BtnUpdatePassword { get; }
    internal Button BtnClear { get; }

    public MyPasswordControl(AuthService authService, UserSession session)
    {
        _authService = authService;
        _session = session;

        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;
        Padding = new Padding(16);

        GroupBox card = CambiasoTheme.CreateGroupBox($"Cambio de Contraseña Personal — Usuario: {session.Usuario.AliasUsuario} ({session.Usuario.PerfilDisplay})");
        card.Location = new Point(16, 16);
        card.Size = new Size(520, 326);

        _txtCurrent = new TextBox { Location = new Point(22, 52), Width = 468, UseSystemPasswordChar = true };
        _txtNew = new TextBox { Location = new Point(22, 110), Width = 468, UseSystemPasswordChar = true };
        _txtConfirm = new TextBox { Location = new Point(22, 168), Width = 468, UseSystemPasswordChar = true };

        _chkShow = new CheckBox { Text = "Mostrar contraseñas", Location = new Point(22, 200), AutoSize = true, Font = CambiasoTheme.SmallFont };
        _chkShow.CheckedChanged += (_, _) =>
        {
            bool hide = !_chkShow.Checked;
            _txtCurrent.UseSystemPasswordChar = hide;
            _txtNew.UseSystemPasswordChar = hide;
            _txtConfirm.UseSystemPasswordChar = hide;
        };

        BtnUpdatePassword = CambiasoTheme.CreateButton("Actualizar Contraseña", AppIcon.Key, ButtonTone.Primary, _toolTip, "Guardar nueva contraseña personal en la base de datos", width: 186, height: 34);
        BtnUpdatePassword.Location = new Point(22, 234);
        BtnUpdatePassword.Click += (_, _) => Submit();

        BtnClear = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Limpiar campos de contraseña", width: 108, height: 34);
        BtnClear.Location = new Point(216, 234);
        BtnClear.Click += (_, _) => ClearFields();

        _lblFeedback = new Label
        {
            Location = new Point(22, 278),
            Size = new Size(468, 36),
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Ingrese su contraseña actual y la nueva contraseña (mínimo 6 caracteres)."
        };

        card.Controls.AddRange(
        [
            new Label { Text = "Contraseña actual:", Font = CambiasoTheme.LabelFont, Location = new Point(22, 32), AutoSize = true },
            _txtCurrent,
            new Label { Text = "Nueva contraseña (mínimo 6 caracteres):", Font = CambiasoTheme.LabelFont, Location = new Point(22, 90), AutoSize = true },
            _txtNew,
            new Label { Text = "Confirmar nueva contraseña:", Font = CambiasoTheme.LabelFont, Location = new Point(22, 148), AutoSize = true },
            _txtConfirm,
            _chkShow,
            BtnUpdatePassword,
            BtnClear,
            _lblFeedback
        ]);

        Controls.Add(card);
    }

    private void Submit()
    {
        try
        {
            _authService.ChangeOwnPassword(_session, _txtCurrent.Text, _txtNew.Text, _txtConfirm.Text);
            ClearFields();
            _lblFeedback.ForeColor = CambiasoTheme.Success;
            _lblFeedback.Text = "Contraseña actualizada correctamente con cifrado PBKDF2-SHA256.";
            CambiasoTheme.ShowInfo(this, "Contraseña actualizada correctamente.");
        }
        catch (Exception ex)
        {
            _lblFeedback.ForeColor = CambiasoTheme.Danger;
            _lblFeedback.Text = ex.Message;
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ClearFields()
    {
        _txtCurrent.Clear();
        _txtNew.Clear();
        _txtConfirm.Clear();
        _txtCurrent.Focus();
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
