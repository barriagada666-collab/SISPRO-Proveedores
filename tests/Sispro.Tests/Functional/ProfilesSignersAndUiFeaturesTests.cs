using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Forms;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class ProfilesSignersAndUiFeaturesTests
{
    [Fact]
    public void UserProfileService_CrudAndCustomProfileAssignment_WorksEndToEnd()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        IReadOnlyList<UserProfile> baseProfiles = temp.Context.UserProfileService.GetAll(admin);
        Assert.Equal(4, baseProfiles.Count(p => p.EsSistema));

        // 1. Crear perfil personalizado
        int customId = temp.Context.UserProfileService.Create(admin, new UserProfile
        {
            Nombre = "Planificador OT",
            Descripcion = "Planifica órdenes externas sin eliminar mantenedores.",
            Activo = true,
            PermisosPorDefecto = new UserPermissions
            {
                MenuOrdenesTrabajo = true,
                IngresoOt = true,
                BusquedaOt = true,
                ImpresionOt = true,
                MenuMantenedor = true,
                MenuUsuarios = true // Debe ser sanitizado a false por RBAC
            }
        });
        Assert.True(customId >= 5);

        UserProfile createdProfile = temp.Context.UserProfileService.GetById(admin, customId)!;
        Assert.Equal("Planificador OT", createdProfile.Nombre);
        Assert.False(createdProfile.EsSistema);
        Assert.False(createdProfile.PermisosPorDefecto.MenuUsuarios);

        // 2. Crear usuario con ese perfil personalizado y validar sesión/RBAC
        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "plan1",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Vega",
            IdNivel = customId,
            Nivel = UserRoleLevel.Usuario,
            Permisos = createdProfile.PermisosPorDefecto,
            DebeCambiarClave = false
        }, "PlanPass2026", "PlanPass2026");

        UserSession planSession = temp.Context.AuthService.Login("plan1", "PlanPass2026").Session!;
        Assert.Equal(customId, planSession.Usuario.IdNivel);
        Assert.Equal("Planificador OT", planSession.Usuario.PerfilDisplay);
        Assert.True(planSession.Permisos.MenuOrdenesTrabajo);
        Assert.True(planSession.Permisos.MenuMantenedor);
        Assert.False(planSession.Permisos.MenuUsuarios);

        // Usuario no administrador no puede crear/consultar perfiles
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.UserProfileService.GetAll(planSession));

        // No puede eliminarse o desactivarse perfil con usuarios asignados ni perfiles base
        Assert.Throws<InvalidOperationException>(() => temp.Context.UserProfileService.Delete(admin, 1));
        Assert.Throws<InvalidOperationException>(() => temp.Context.UserProfileService.Delete(admin, customId));
        Assert.Throws<InvalidOperationException>(() => temp.Context.UserProfileService.SetActive(admin, customId, false));

        // 3. Reasignar lote al perfil base Usuario y editar + desactivar + eliminar el perfil personalizado
        temp.Context.UserManagementService.BatchChangeProfile(admin, [planSession.Usuario.IdUsuario], (int)UserRoleLevel.Usuario, "Fin prueba");

        createdProfile.Descripcion = "Descripción actualizada";
        temp.Context.UserProfileService.Update(admin, createdProfile);
        Assert.Equal("Descripción actualizada", temp.Context.UserProfileService.GetById(admin, customId)!.Descripcion);

        temp.Context.UserProfileService.SetActive(admin, customId, false);
        Assert.False(temp.Context.UserProfileService.GetById(admin, customId)!.Activo);

        temp.Context.UserProfileService.Delete(admin, customId);
        Assert.Null(temp.Context.UserProfileService.GetById(admin, customId));
    }

    [Fact]
    public void SignaturePersonnelCatalog_IncludesPersonnelUsersRequesters_AndExportsSelectedSigners()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        IReadOnlyList<SignaturePersonOption> catalog = temp.Context.WorkOrderService.GetSignaturePersonnelCatalog();
        Assert.NotEmpty(catalog);
        // Proviene de Solicitantes / Usuarios / Mant_Personal
        Assert.Contains(catalog, p => p.Origen == "Personal");
        Assert.Contains(catalog, p => p.Nombre.Contains("Juan Ramos", StringComparison.OrdinalIgnoreCase));

        string customSupervisor = catalog.First(p => p.Origen == "Personal").Nombre;
        int provId = temp.Context.MaintainerService.GetProviders()[0].IdProv;
        int solId = temp.Context.MaintainerService.GetRequesters()[0].IdSol;
        int plantId = temp.Context.MaintainerService.GetAssetChildren(1)[0].IdActivo;
        int nextOt = temp.Context.WorkOrderService.GetNextWorkOrderNumber();

        WorkOrder order = new()
        {
            NumeroOt = nextOt,
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "Prueba de firmantes desde personal",
            ValorNeto = 385000m,
            FirmasSeleccionadas = SignatureOption.AmbasConSolicitante,
            FirmaJefaturaNombre = "Juan Ramos Moyano",
            FirmaTecnicoNombre = "Boris Arriagada Rojas",
            FirmaSupervisorNombre = customSupervisor,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Fabricación de polín con rodamiento" }]
        };
        order.SetLevels([1, plantId]);

        int idOt = temp.Context.WorkOrderService.Create(admin, order);
        WorkOrder loaded = temp.Context.WorkOrderService.GetById(idOt)!;
        Assert.Equal("Juan Ramos Moyano", loaded.FirmaJefaturaNombre);
        Assert.Equal("Boris Arriagada Rojas", loaded.FirmaTecnicoNombre);
        Assert.Equal(customSupervisor, loaded.FirmaSupervisorNombre);

        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_sig_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            string pdfPath = Path.Combine(outDir, "ot_signers.pdf");
            string docxPath = Path.Combine(outDir, "ot_signers.docx");
            string xlsxPath = Path.Combine(outDir, "ot_signers.xlsx");

            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Pdf, pdfPath, loaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Word, docxPath, loaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Excel, xlsxPath, loaded.FirmasSeleccionadas);

            string pdfText = Encoding.GetEncoding(1252).GetString(File.ReadAllBytes(pdfPath));
            Assert.Contains(customSupervisor, pdfText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Boris Arriagada Rojas", pdfText, StringComparison.OrdinalIgnoreCase);

            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, false))
            {
                string docText = doc.MainDocumentPart!.Document.Body!.InnerText;
                Assert.Contains(customSupervisor, docText, StringComparison.OrdinalIgnoreCase);
            }

            using (XLWorkbook wb = new(xlsxPath))
            {
                string allCells = string.Join(" ", wb.Worksheets.First().CellsUsed().Select(c => c.GetString()));
                Assert.Contains(customSupervisor, allCells, StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void StaUi_WorkOrdersAndUsersForms_ExposeAllRequestedControlsWithoutVbApprovals()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            using (Form host = new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-2000, -2000), Size = new(1280, 820) })
            using (WorkOrdersForm wo = new(temp.Context, admin))
            {
                host.Controls.Add(wo);
                host.Show();
                Application.DoEvents();

                // 1. No debe existir VB Supervisor / VB Jefatura ni Pendientes VB en WorkOrdersForm
                List<CheckBox> woChecks = FindControls<CheckBox>(wo);
                Assert.DoesNotContain(woChecks, c => c.Text.Contains("VB Supervisor", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(woChecks, c => c.Text.Contains("VB Jefatura", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(woChecks, c => c.Text.Contains("Pendientes VB", StringComparison.OrdinalIgnoreCase));

                // 2. Los 6 combos de firmantes deben estar poblados y habilitados cuando su checkbox está activo
                Assert.True(wo.CmbEntrySigJefatura.Items.Count > 0);
                Assert.True(wo.CmbEntrySigTecnico.Items.Count > 0);
                Assert.True(wo.CmbEntrySigSupervisor.Items.Count > 0);
                Assert.True(wo.CmbPrintSigJefatura.Items.Count > 0);
                Assert.True(wo.CmbPrintSigTecnico.Items.Count > 0);
                Assert.True(wo.CmbPrintSigSupervisor.Items.Count > 0);

                wo.ChkEntrySigJefatura.Checked = false;
                Assert.False(wo.CmbEntrySigJefatura.Enabled);
                wo.ChkEntrySigJefatura.Checked = true;
                Assert.True(wo.CmbEntrySigJefatura.Enabled);

                // 3. Guardar y Exportar + Vista Previa habilitados tanto en modo nueva OT como tras cargar OT existente
                Assert.True(wo.BtnSaveAndExportOt.Enabled);
                Assert.True(wo.BtnEntryPreview.Enabled);
                wo.OpenOrderByNumber(14752);
                Application.DoEvents();
                Assert.True(wo.BtnSaveAndExportOt.Enabled);
                Assert.True(wo.BtnEntryPreview.Enabled);

                // 4. Búsqueda sin columnas VBS / VBJ
                wo.OpenSearchTab();
                Application.DoEvents();
                Assert.DoesNotContain(wo.GridSearchResults.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText is "VBS" or "VBJ");
                host.Hide();
            }

            // 5. UsersForm sin VBS / VBJ y con pestaña Perfiles de Usuarios
            using Form hostUsers = new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-2000, -2000), Size = new(1280, 820) };
            using UsersForm users = new(temp.Context, admin);
            hostUsers.Controls.Add(users);
            hostUsers.Show();
            Application.DoEvents();
            Assert.Equal(3, users.MainTabs.TabPages.Count);
            Assert.Equal("Perfiles de Usuarios", users.MainTabs.TabPages[2].Text);
            Assert.DoesNotContain(users.GridUsers.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText is "VBS" or "VBJ");
            Assert.True(users.ProfileManager.GridProfiles.Rows.Count >= 4);
            hostUsers.Hide();
        });
    }

    private static List<T> FindControls<T>(Control root) where T : Control
    {
        List<T> result = [];
        void Walk(Control cur)
        {
            if (cur is T typed) result.Add(typed);
            foreach (Control child in cur.Controls) Walk(child);
            if (cur is TabControl tc)
            {
                foreach (TabPage tp in tc.TabPages)
                {
                    if (!tc.Controls.Contains(tp)) Walk(tp);
                }
            }
        }
        Walk(root);
        return result;
    }
}
