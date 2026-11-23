using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

public sealed class ChangePasswordDialog : Form
{
    private readonly AuthService _authService;
    private readonly UserSession _session;
    private readonly bool _forcePasswordChange;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtCurrent;
    private readonly TextBox _txtNew;
    private readonly TextBox _txtConfirm;

    public ChangePasswordDialog(AuthService authService, UserSession session, bool forcePasswordChange = false)
    {
        _authService = authService;
        _session = session;
        _forcePasswordChange = forcePasswordChange;

        Text = forcePasswordChange
            ? "Cambio Obligatorio de Contraseña — Primer Acceso"
            : "Cambiar Contraseña — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        ClientSize = new Size(448, forcePasswordChange ? 356 : 308);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 50, BackColor = CambiasoTheme.Primary };
        header.Controls.Add(new Label
        {
            Text = forcePasswordChange
                ? $"Primer Inicio de Sesión — {session.Usuario.AliasUsuario}"
                : $"Actualizar Contraseña ({session.Usuario.AliasUsuario})",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 15)
        });

        Panel? noticeBanner = null;
        if (forcePasswordChange)
        {
            noticeBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(254, 243, 199),
                Padding = new Padding(14, 6, 14, 6)
            };
            noticeBanner.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "Por política de seguridad, debe cambiar su contraseña inicial antes de ingresar al sistema.",
                Font = CambiasoTheme.SmallFont,
                ForeColor = Color.FromArgb(146, 64, 14),
                TextAlign = ContentAlignment.MiddleLeft
            });
        }

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(20, 14, 20, 10) };
        _txtCurrent = new TextBox { Location = new Point(20, 32), Width = 404, UseSystemPasswordChar = true };
        _txtNew = new TextBox { Location = new Point(20, 90), Width = 404, UseSystemPasswordChar = true };
        _txtConfirm = new TextBox { Location = new Point(20, 148), Width = 404, UseSystemPasswordChar = true };

        CheckBox chkShow = new() { Text = "Mostrar contraseñas", Location = new Point(20, 180), AutoSize = true, Font = CambiasoTheme.SmallFont };
        chkShow.CheckedChanged += (_, _) =>
        {
            bool hide = !chkShow.Checked;
            _txtCurrent.UseSystemPasswordChar = hide;
            _txtNew.UseSystemPasswordChar = hide;
            _txtConfirm.UseSystemPasswordChar = hide;
        };

        body.Controls.AddRange(
        [
            new Label { Text = forcePasswordChange ? "Contraseña inicial actual:" : "Contraseña actual:", Font = CambiasoTheme.LabelFont, Location = new Point(20, 12), AutoSize = true },
            _txtCurrent,
            new Label { Text = "Nueva contraseña (mínimo 6 caracteres):", Font = CambiasoTheme.LabelFont, Location = new Point(20, 70), AutoSize = true },
            _txtNew,
            new Label { Text = "Confirmar nueva contraseña:", Font = CambiasoTheme.LabelFont, Location = new Point(20, 128), AutoSize = true },
            _txtConfirm,
            chkShow
        ]);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 16, 10),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cancelar cambio de contraseña", width: 118);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Button btnSave = CambiasoTheme.CreateButton("Actualizar", AppIcon.Key, ButtonTone.Primary, _toolTip, "Guardar nueva contraseña", width: 132);
        btnSave.Click += (_, _) => Submit();

        footer.Controls.Add(btnCancel);
        footer.Controls.Add(btnSave);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(footer);
        if (noticeBanner is not null)
        {
            Controls.Add(noticeBanner);
        }

        Controls.Add(header);

        FormClosed += (_, _) =>
        {
            if (_forcePasswordChange && DialogResult != DialogResult.OK)
            {
                _authService.LogMandatoryPasswordChangeCancelled(_session);
            }
        };
    }

    private void Submit()
    {
        try
        {
            _authService.ChangeOwnPassword(_session, _txtCurrent.Text, _txtNew.Text, _txtConfirm.Text);
            CambiasoTheme.ShowInfo(this, "Contraseña actualizada correctamente.");
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
