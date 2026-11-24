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
            Assert.True(main.NavMaintainers.Enabled);
            Assert.True(main.NavUsers.Enabled);
            Assert.True(main.NavAudit.Enabled);

            main.ShowDashboard();
            AssertAllButtonsValid(main, "MainForm.Dashboard");
            Assert.True(main.NavTreeEditor.Enabled);
            Assert.True(main.BtnTopNewExternalOt.Enabled);
            Assert.NotNull(main.BtnHeroNewExternalOt);
            Assert.True(main.BtnHeroNewExternalOt.Enabled);
            Assert.Equal("Nueva OT Externa", main.BtnHeroNewExternalOt.Text);

            // Confirm Facturas y Guías & Explorar BD Mantención & Mantención Industrial are completely absent
            List<Button> dashButtons = CollectControls<Button>(main);
            Assert.DoesNotContain(dashButtons, b => b.Text.Contains("Factura", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(dashButtons, b => b.Text.Contains("Explorar BD Mant", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(dashButtons, b => b.Text.Contains("Mantención Industrial", StringComparison.OrdinalIgnoreCase));

            main.ShowWorkOrders(14752);
            AssertAllButtonsValid(main, "MainForm.WorkOrders");
            AssertNoForbiddenTypos(main, "MainForm.WorkOrders");

            main.ShowMaintainers();
            AssertAllButtonsValid(main, "MainForm.Maintainers");
            AssertNoForbiddenTypos(main, "MainForm.Maintainers");

            main.ShowTreeEditor();
            AssertAllButtonsValid(main, "MainForm.TreeEditor");
            AssertNoForbiddenTypos(main, "MainForm.TreeEditor");

            main.ShowUsers();
            AssertAllButtonsValid(main, "MainForm.Users");
            AssertNoForbiddenTypos(main, "MainForm.Users");
            main.ShowUsers(openRequestersTab: true);
            AssertAllButtonsValid(main, "MainForm.Users.RequestersTab");

            using WorkOrdersForm directWorkOrders = new(temp.Context, admin);
            Assert.Equal("Nueva OT Externa", directWorkOrders.BtnNewExternalOt.Text);
            Assert.True(directWorkOrders.ChkSigJefatura.Checked);
            Assert.True(directWorkOrders.ChkSigTecnico.Checked);
            Assert.True(directWorkOrders.ChkSigSupervisor.Checked);
            Assert.Equal(SignatureOption.AmbasConSolicitante, directWorkOrders.SelectedSignature);

            // Verificar selector libre de firmas (incluyendo dejar vacío para no imprimir firmas al pie)
            directWorkOrders.ChkSigSupervisor.Checked = false;
            Assert.Equal(SignatureOption.JefaturaYTecnico, directWorkOrders.SelectedSignature);
            directWorkOrders.ChkSigJefatura.Checked = false;
            directWorkOrders.ChkSigSupervisor.Checked = true;
            Assert.Equal(SignatureOption.SupervisorYTecnico, directWorkOrders.SelectedSignature);
            directWorkOrders.ChkSigTecnico.Checked = false;
            directWorkOrders.ChkSigSupervisor.Checked = false;
            Assert.Equal(SignatureOption.Ninguna, directWorkOrders.SelectedSignature);

            // Restaurar antes de auditar botones
            directWorkOrders.ChkSigJefatura.Checked = true;
            directWorkOrders.ChkSigTecnico.Checked = true;
            directWorkOrders.ChkSigSupervisor.Checked = true;
            AssertAllButtonsValid(directWorkOrders, "WorkOrdersForm.Direct");

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
            Assert.Equal("Activar", directUsers.BtnActivar.Text);
            Assert.Equal("Desactivar", directUsers.BtnDesactivar.Text);
            Assert.Equal("Mover", directUsers.BtnMover.Text);
            Assert.Equal("Exportar", directUsers.BtnExportar.Text);
            Assert.Equal(14, directUsers.GridUsers.Columns.Count);
            Assert.Contains(directUsers.GridUsers.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText == "VBS");
            Assert.Contains(directUsers.GridUsers.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText == "VBJ");
            Assert.Contains(directUsers.GridUsers.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText == "Solicitante / Firma");
            Assert.True(directUsers.GridUsers.Rows.Count >= 3);
            Assert.True(directUsers.ListGroups.Items.Count >= 7); // Todos + Sin grupo + 5 grupos base

            using SettingsDialog settingsDlg = new(temp.Context);
            AssertAllButtonsValid(settingsDlg, "SettingsDialog");
            Assert.NotNull(settingsDlg.AcceptButton);
            Assert.NotNull(settingsDlg.CancelButton);

            using ChangePasswordDialog pwdDlg = new(temp.Context.AuthService, admin);
            AssertAllButtonsValid(pwdDlg, "ChangePasswordDialog");

            using ChangePasswordDialog forcePwdDlg = new(temp.Context.AuthService, admin, forcePasswordChange: true);
            AssertAllButtonsValid(forcePwdDlg, "ChangePasswordDialog.Forced");

            using AdminResetPasswordDialog resetDlg = new(temp.Context.UserManagementService, admin, admin.Usuario);
            AssertAllButtonsValid(resetDlg, "AdminResetPasswordDialog");

            using UserPermissionsDialog permsDlg = new(UserRoleLevel.Supervisor, PermissionPresets.CreateForRole(UserRoleLevel.Supervisor), readOnly: false);
            AssertAllButtonsValid(permsDlg, "UserPermissionsDialog");
            List<CheckBox> permBoxes = CollectControls<CheckBox>(permsDlg);
            Assert.DoesNotContain(permBoxes, cb => cb.Text.Contains("Factura", StringComparison.OrdinalIgnoreCase));

            using AuditLogDialog auditDlg = new(temp.Context.Audit, temp.Context.DocumentExportService);
            AssertAllButtonsValid(auditDlg, "AuditLogDialog");
            List<ComboBox> auditCombos = CollectControls<ComboBox>(auditDlg);
            Assert.Contains(auditCombos, c => c.Items.Contains("Árbol de Activos") && c.Items.Contains("Configuración") && !c.Items.Contains("Facturas y Guías"));

            using GroupEditorDialog groupEditDlg = new(temp.Context.GroupManagementService, temp.Context.UserManagementService, admin);
            AssertAllButtonsValid(groupEditDlg, "GroupEditorDialog");

            using GroupMovementDialog groupMoveDlg = new([admin.Usuario], temp.Context.GroupManagementService.GetAll(admin));
            AssertAllButtonsValid(groupMoveDlg, "GroupMovementDialog");

            using GroupHistoryDialog groupHistDlg = new(temp.Context.GroupManagementService, temp.Context.DocumentExportService, admin);
            AssertAllButtonsValid(groupHistDlg, "GroupHistoryDialog");

            using BatchConfirmDialog batchDlg = new("Desactivar Usuarios", "Confirmación", 1, [admin.Usuario], [], requireRoleSelection: true, isDestructive: true);
            AssertAllButtonsValid(batchDlg, "BatchConfirmDialog");

            AssetNode rootNode = temp.Context.AssetTree.GetById(1)!;
            IReadOnlyList<AssetNode> rootChildren = temp.Context.MaintainerService.GetAssetChildren(1, includeInactive: false);
            AssetNode childNode = rootChildren[0];

            using AssetNodeEditorDialog assetEditDlg = new(null, rootNode, rootNode.Descripcion);
            AssertAllButtonsValid(assetEditDlg, "AssetNodeEditorDialog");

            using AssetNodeMoveDialog assetMoveDlg = new(temp.Context.MaintainerService, childNode, $"{rootNode.Descripcion} \\ {childNode.Descripcion}");
            AssertAllButtonsValid(assetMoveDlg, "AssetNodeMoveDialog");

            MaintainerDependencyReport depReport = temp.Context.MaintainerService.InspectDependencies(admin, MaintainerEntityKind.Activo, [childNode.IdActivo]);
            using MaintainerDependenciesDialog depDlg = new(temp.Context, admin, depReport);
            AssertAllButtonsValid(depDlg, "MaintainerDependenciesDialog");

            using MaintainersForm directMaintainers = new(temp.Context, admin);
            Assert.Equal(4, directMaintainers.TabControlMain.TabPages.Count);
            AssertAllButtonsValid(directMaintainers, "MaintainersForm.Direct4Tabs");
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
            Assert.False(main.NavMaintainers.Enabled);
            Assert.False(main.NavTreeEditor.Enabled);
            Assert.False(main.NavUsers.Enabled);
            Assert.False(main.NavAudit.Enabled);

            // Intento directo de instanciar módulos exclusivos de Administrador debe bloquearse
            Assert.Throws<UnauthorizedAccessException>(() => new UsersForm(temp.Context, opSession));
            Assert.Throws<UnauthorizedAccessException>(() => new AssetTreeEditorView(temp.Context, opSession));
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
