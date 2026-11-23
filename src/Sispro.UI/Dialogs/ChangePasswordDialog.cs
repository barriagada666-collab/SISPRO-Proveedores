using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

public sealed class ChangePasswordDialog : Form
{
    private readonly AuthService _authService;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtCurrent;
    private readonly TextBox _txtNew;
    private readonly TextBox _txtConfirm;

    public ChangePasswordDialog(AuthService authService, UserSession session)
    {
        _authService = authService;
        _session = session;

        Text = "Cambiar Contraseña — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(440, 320);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 48, BackColor = CambiasoTheme.Primary };
        header.Controls.Add(new Label
        {
            Text = $"Actualizar Contraseña ({session.Usuario.AliasUsuario})",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 14)
        });

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(20) };
        _txtCurrent = new TextBox { Location = new Point(20, 34), Width = 376, UseSystemPasswordChar = true };
        _txtNew = new TextBox { Location = new Point(20, 94), Width = 376, UseSystemPasswordChar = true };
        _txtConfirm = new TextBox { Location = new Point(20, 154), Width = 376, UseSystemPasswordChar = true };

        CheckBox chkShow = new() { Text = "Mostrar contraseñas", Location = new Point(20, 186), AutoSize = true, Font = CambiasoTheme.SmallFont };
        chkShow.CheckedChanged += (_, _) =>
        {
            bool hide = !chkShow.Checked;
            _txtCurrent.UseSystemPasswordChar = hide;
            _txtNew.UseSystemPasswordChar = hide;
            _txtConfirm.UseSystemPasswordChar = hide;
        };

        body.Controls.AddRange(
        [
            new Label { Text = "Contraseña actual:", Font = CambiasoTheme.LabelFont, Location = new Point(20, 14), AutoSize = true },
            _txtCurrent,
            new Label { Text = "Nueva contraseña (mínimo 6 caracteres):", Font = CambiasoTheme.LabelFont, Location = new Point(20, 74), AutoSize = true },
            _txtNew,
            new Label { Text = "Confirmar nueva contraseña:", Font = CambiasoTheme.LabelFont, Location = new Point(20, 134), AutoSize = true },
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

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Cancelar cambio de contraseña", width: 118);
        btnCancel.Click += (_, _) => Close();

        Button btnSave = CambiasoTheme.CreateButton("Actualizar", AppIcon.Key, ButtonTone.Primary, _toolTip, "Guardar nueva contraseña", width: 132);
        btnSave.Click += (_, _) => Submit();

        footer.Controls.Add(btnCancel);
        footer.Controls.Add(btnSave);

        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
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
