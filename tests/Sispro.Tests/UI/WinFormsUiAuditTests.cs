using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.UI.Dialogs;
using Sispro.UI.Forms;
using Xunit;

namespace Sispro.Tests.UI;

public sealed class WinFormsUiAuditTests
{
    private static readonly string[] ForbiddenLegacyTypos =
    [
        "IINGRESO",
        "Envíar",
        "vualva",
        "Solictante",
        "Form1",
        "wanderes"
    ];

    [Fact]
    public void LoginForm_HasNoHardcodedCredentialsAndAllButtonsHaveIcons()
    {
        using TempTestDatabase temp = new();
        TempTestDatabase.RunOnStaThread(() =>
        {
            using LoginForm form = new(temp.Context);
            TextBox? txtUser = FindControlByName<TextBox>(form, "txtUsuario");
            TextBox? txtPass = FindControlByName<TextBox>(form, "txtPassword");

            Assert.NotNull(txtUser);
            Assert.NotNull(txtPass);
            Assert.Equal(string.Empty, txtUser.Text);
            Assert.Equal(string.Empty, txtPass.Text);

            AssertAllButtonsValid(form, "LoginForm");
            AssertNoForbiddenTypos(form, "LoginForm");
        });
    }

    [Fact]
    public void AllFormsAndDialogs_PassUiUxButtonIconAndTypoAudit()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            using MainForm main = new(temp.Context, admin);
            Assert.True(main.NavWorkOrders.Enabled);
            Assert.True(main.NavInvoices.Enabled);
            Assert.True(main.NavMaintainers.Enabled);
            Assert.True(main.NavMaintenance.Enabled);
            Assert.True(main.NavUsers.Enabled);
            Assert.True(main.NavAudit.Enabled);

            main.ShowDashboard();
            AssertAllButtonsValid(main, "MainForm.Dashboard");

            main.ShowWorkOrders(14752);
            AssertAllButtonsValid(main, "MainForm.WorkOrders");
            AssertNoForbiddenTypos(main, "MainForm.WorkOrders");

            main.ShowInvoices();
            AssertAllButtonsValid(main, "MainForm.Invoices");
            AssertNoForbiddenTypos(main, "MainForm.Invoices");

            main.ShowMaintainers();
            AssertAllButtonsValid(main, "MainForm.Maintainers");
            AssertNoForbiddenTypos(main, "MainForm.Maintainers");

            main.ShowMaintenanceHub();
            AssertAllButtonsValid(main, "MainForm.MaintenanceHub");
            AssertNoForbiddenTypos(main, "MainForm.MaintenanceHub");

            main.ShowUsers();
            AssertAllButtonsValid(main, "MainForm.Users");
            AssertNoForbiddenTypos(main, "MainForm.Users");

            using UsersForm directUsers = new(temp.Context, admin);
            AssertAllButtonsValid(directUsers, "UsersForm.Direct");
            Assert.Equal("Nuevo", directUsers.BtnNuevo.Text);
            Assert.Equal("Guardar", directUsers.BtnGuardar.Text);
            Assert.Equal("Editar", directUsers.BtnEditar.Text);
            Assert.Equal("Actualizar", directUsers.BtnActualizar.Text);
            Assert.Equal("Eliminar", directUsers.BtnEliminar.Text);
            Assert.Equal("Cambiar Contraseña", directUsers.BtnCambiarPassword.Text);
            Assert.Equal("Cancelar", directUsers.BtnCancelar.Text);
            Assert.Equal("Limpiar", directUsers.BtnLimpiar.Text);
            Assert.Equal("Buscar", directUsers.BtnBuscar.Text);
            Assert.Equal("Refrescar", directUsers.BtnRefrescar.Text);
            Assert.Equal(9, directUsers.GridUsers.Columns.Count);
            Assert.True(directUsers.GridUsers.Rows.Count >= 3);

            using SettingsDialog settingsDlg = new(temp.Context);
            AssertAllButtonsValid(settingsDlg, "SettingsDialog");

            using ChangePasswordDialog pwdDlg = new(temp.Context.AuthService, admin);
            AssertAllButtonsValid(pwdDlg, "ChangePasswordDialog");

            using ChangePasswordDialog forcePwdDlg = new(temp.Context.AuthService, admin, forcePasswordChange: true);
            AssertAllButtonsValid(forcePwdDlg, "ChangePasswordDialog.Forced");

            using AdminResetPasswordDialog resetDlg = new(temp.Context.UserManagementService, admin, admin.Usuario);
            AssertAllButtonsValid(resetDlg, "AdminResetPasswordDialog");

            using UserPermissionsDialog permsDlg = new(UserRoleLevel.Supervisor, PermissionPresets.CreateForRole(UserRoleLevel.Supervisor), readOnly: false);
            AssertAllButtonsValid(permsDlg, "UserPermissionsDialog");

            using AuditLogDialog auditDlg = new(temp.Context.Audit);
            AssertAllButtonsValid(auditDlg, "AuditLogDialog");
        });
    }

    [Fact]
    public void MainForm_EnforcesRbacNavigationPerRole()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            IdUsuario = "128498745",
            AliasUsuario = "operador1",
            NombreUsuario = "Pedro",
            ApePatUsuario = "Soto",
            Nivel = UserRoleLevel.Usuario,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "ClaveOp2026", "ClaveOp2026");

        UserSession opSession = temp.Context.AuthService.Login("operador1", "ClaveOp2026").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            using MainForm main = new(temp.Context, opSession);
            Assert.True(main.NavDashboard.Enabled);
            Assert.True(main.NavWorkOrders.Enabled);
            Assert.False(main.NavInvoices.Enabled);
            Assert.False(main.NavMaintainers.Enabled);
            Assert.False(main.NavUsers.Enabled);
            Assert.False(main.NavAudit.Enabled);

            // Intento directo de instanciar el formulario de Gestión de Usuarios por un no-administrador debe bloquearse
            Assert.Throws<UnauthorizedAccessException>(() => new UsersForm(temp.Context, opSession));
        });

        Assert.Contains(temp.Context.Audit.GetRecent(20), e => e.Accion == "ACCESO_NO_AUTORIZADO" && e.Usuario == "operador1");
    }

    private static void AssertAllButtonsValid(Control root, string contextName)
    {
        List<Button> buttons = CollectControls<Button>(root);
        Assert.NotEmpty(buttons);

        foreach (Button btn in buttons)
        {
            Assert.True(btn.Image is not null, $"Botón '{btn.Text}' en {contextName} no tiene ícono asignado.");
            Assert.True(btn.Width > 0 && btn.Height > 0, $"Botón '{btn.Text}' en {contextName} tiene tamaño inválido.");
            // Ensure no broken leading-spaces formatting hacks from VB6
            if (!string.IsNullOrEmpty(btn.Text))
            {
                Assert.Equal(btn.Text.Trim(), btn.Text);
            }
        }
    }

    private static void AssertNoForbiddenTypos(Control root, string contextName)
    {
        foreach (Control ctl in CollectControls<Control>(root))
        {
            if (ctl is TextBox || string.IsNullOrEmpty(ctl.Text))
            {
                continue;
            }

            foreach (string typo in ForbiddenLegacyTypos)
            {
                Assert.DoesNotContain(typo, ctl.Text, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.NotNull(contextName);
    }

    private static List<T> CollectControls<T>(Control root) where T : Control
    {
        List<T> result = [];
        void Walk(Control current)
        {
            if (current is T typed)
            {
                result.Add(typed);
            }

            foreach (Control child in current.Controls)
            {
                Walk(child);
            }

            if (current is TabControl tc)
            {
                foreach (TabPage page in tc.TabPages)
                {
                    if (!current.Controls.Contains(page))
                    {
                        Walk(page);
                    }
                }
            }
        }

        Walk(root);
        return result;
    }

    private static T? FindControlByName<T>(Control root, string name) where T : Control =>
        CollectControls<T>(root).FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
}
