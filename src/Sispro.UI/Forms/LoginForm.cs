using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

public sealed class LoginForm : Form
{
    private readonly SisproApplicationContext _ctx;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtUser;
    private readonly TextBox _txtPass;
    private readonly Label _lblError;
    private readonly Button _btnLogin;
    private readonly Button _btnExit;
    private readonly Button _btnSettings;

    public UserSession? AuthenticatedSession { get; private set; }

    public LoginForm(SisproApplicationContext ctx)
    {
        _ctx = ctx;

        Text = "SISPRO — Acceso al Sistema";
        Icon = IconCatalog.GetAppIcon();
        ClientSize = new Size(444, 434);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        CambiasoTheme.EnableDoubleBuffering(this);

        // Corporate Banner with Petrol-Navy gradient & transparent gold Cambiaso logo
        Panel banner = new()
        {
            Dock = DockStyle.Top,
            Height = 142,
            BackColor = CambiasoTheme.PrimaryDark
        };
        CambiasoTheme.EnableDoubleBuffering(banner);
        banner.Paint += (_, e) =>
        {
            using System.Drawing.Drawing2D.LinearGradientBrush grad = new(
                banner.ClientRectangle,
                CambiasoTheme.Primary,
                CambiasoTheme.PrimaryDark,
                System.Drawing.Drawing2D.LinearGradientMode.Vertical);
            e.Graphics.FillRectangle(grad, banner.ClientRectangle);
            using SolidBrush accentLine = new(CambiasoTheme.Accent);
            e.Graphics.FillRectangle(accentLine, 0, banner.Height - 3, banner.Width, 3);
        };

        PictureBox picLogo = new()
        {
            Size = new Size(230, 96),
            Location = new Point(107, 10),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Image = IconCatalog.GetCompanyLogo()
        };

        Label lblSubtitle = new()
        {
            Text = "SISPRO · Gestión de Proveedores y Órdenes de Trabajo",
            Font = CambiasoTheme.SmallFont,
            ForeColor = Color.FromArgb(208, 228, 242),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Bottom,
            Height = 30
        };

        banner.Controls.Add(picLogo);
        banner.Controls.Add(lblSubtitle);

        // Card container
        Panel card = new()
        {
            Location = new Point(28, 156),
            Size = new Size(388, 192),
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(18)
        };
        card.Paint += (_, e) =>
        {
            using Pen pen = new(CambiasoTheme.BorderColor, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        Label lblUser = CambiasoTheme.CreateFieldLabel("Usuario (Alias)");
        lblUser.Location = new Point(22, 18);

        _txtUser = new TextBox
        {
            Name = "txtUsuario",
            Location = new Point(22, 38),
            Width = 342,
            Font = CambiasoTheme.BodyFont,
            PlaceholderText = "Ingrese su código o alias de usuario"
        };

        Label lblPass = CambiasoTheme.CreateFieldLabel("Contraseña");
        lblPass.Location = new Point(22, 74);

        _txtPass = new TextBox
        {
            Name = "txtPassword",
            Location = new Point(22, 94),
            Width = 342,
            Font = CambiasoTheme.BodyFont,
            UseSystemPasswordChar = true,
            PlaceholderText = "Ingrese su contraseña"
        };

        CheckBox chkShowPass = new()
        {
            Text = "Mostrar contraseña",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Location = new Point(22, 126),
            AutoSize = true
        };
        chkShowPass.CheckedChanged += (_, _) => _txtPass.UseSystemPasswordChar = !chkShowPass.Checked;

        _lblError = new Label
        {
            Location = new Point(22, 150),
            Size = new Size(342, 40),
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Danger,
            Text = string.Empty
        };

        card.Controls.AddRange([lblUser, _txtUser, lblPass, _txtPass, chkShowPass, _lblError]);

        // Footer Actions
        _btnSettings = CambiasoTheme.CreateButton("Config.", AppIcon.Settings, ButtonTone.Secondary, _toolTip, "Configurar base de datos y empresa", width: 102, height: 34);
        _btnSettings.Location = new Point(28, 356);
        _btnSettings.Click += (_, _) => OpenSettings();

        _btnExit = CambiasoTheme.CreateButton("Salir", AppIcon.Exit, ButtonTone.Secondary, _toolTip, "Cerrar aplicación", width: 100, height: 34);
        _btnExit.Location = new Point(196, 356);
        _btnExit.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnLogin = CambiasoTheme.CreateButton("Ingresar", AppIcon.Login, ButtonTone.Primary, _toolTip, "Iniciar sesión en SISPRO", width: 112, height: 34);
        _btnLogin.Location = new Point(304, 356);
        _btnLogin.Click += (_, _) => AttemptLogin();

        Label lblFooter = new()
        {
            Text = $"{_ctx.Settings.CompanyCity} · {DateTime.Now:dd 'de' MMMM 'de' yyyy}",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Dock = DockStyle.Bottom,
            Height = 22,
            TextAlign = ContentAlignment.MiddleCenter
        };

        AcceptButton = _btnLogin;
        CancelButton = _btnExit;

        Controls.AddRange([banner, card, _btnSettings, _btnExit, _btnLogin, lblFooter]);

        Shown += (_, _) => _txtUser.Focus();
    }

    private void AttemptLogin()
    {
        _lblError.Text = string.Empty;
        AuthResult result = _ctx.AuthService.Login(_txtUser.Text, _txtPass.Text);
        if (!result.Success || result.Session is null)
        {
            _lblError.Text = result.Message;
            _txtPass.SelectAll();
            _txtPass.Focus();
            return;
        }

        if (result.RequiresPasswordChange)
        {
            using ChangePasswordDialog forceDlg = new(_ctx.AuthService, result.Session, forcePasswordChange: true);
            if (forceDlg.ShowDialog(this) != DialogResult.OK)
            {
                _lblError.Text = "Debe actualizar su contraseña inicial para acceder al sistema.";
                _txtPass.Clear();
                _txtPass.Focus();
                return;
            }
        }

        AuthenticatedSession = result.Session;
        _ctx.CurrentSession = result.Session;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OpenSettings()
    {
        using SettingsDialog dlg = new(_ctx);
        dlg.ShowDialog(this);
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
