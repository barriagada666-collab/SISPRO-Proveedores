using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Sispro.Core;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Dialogs;
using Sispro.UI.Forms;
using Xunit;

namespace Sispro.Tests.Functional;

/// <summary>
/// Pruebas integrales de verificación de los requisitos v4.6.0:
/// 1. Reorganización del menú Mantenedores (Editor de Árbol, Configuración, Mi Clave).
/// 2. Barra superior simplificada (Cambiar usuario, Acerca de, Cerrar sesión).
/// 3. Eliminación de Generar / Imprimir OT y Exportar / Imprimir.
/// 4. Checkbox "Mostrar valor neto en OT externa" con persistencia no destructiva y plantillas.
/// 5. Configuración del sistema con Probar, Aplicar (temporal) y Guardar (BD centralizada).
/// 6. Búsqueda de OT — CRUD completo (Externa/Interna, Nuevo, Consultar, Editar, Eliminar).
/// </summary>
public sealed class NavigationConfigNetValueAndCrudTests
{
    [Fact]
    public void NavigationReorganization_CentralizesTreeEditorConfigAndMyPasswordInMaintainers_WithRbac()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "jef_nav",
            NombreUsuario = "Laura",
            ApePatUsuario = "Méndez",
            Nivel = UserRoleLevel.Jefatura,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Jefatura)
        }, "JefNav2026!", "JefNav2026!");

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "usr_nav",
            NombreUsuario = "Mario",
            ApePatUsuario = "Díaz",
            Nivel = UserRoleLevel.Usuario,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "UsrNav2026!", "UsrNav2026!");

        UserSession jef = temp.Context.AuthService.Login("jef_nav", "JefNav2026!").Session!;
        UserSession usr = temp.Context.AuthService.Login("usr_nav", "UsrNav2026!").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            // Admin: Editor de Árbol ausente del sidebar, centralizado con Configuración y Mi Clave en Mantenedores
            using MainForm adminMain = new(temp.Context, admin);
            List<Button> sidebarButtons = CollectControls<Button>(adminMain);
            Assert.DoesNotContain(sidebarButtons, b => b.Text == "Editor de Árbol");

            adminMain.ShowMaintainers("Editor de Árbol");
            MaintainersForm? mAdmin = FindFirstChild<MaintainersForm>(adminMain);
            Assert.NotNull(mAdmin);
            Assert.Equal("Editor de Árbol", mAdmin.TabControlMain.SelectedTab?.Text);

            adminMain.ShowMaintainers("Configuración");
            Assert.Equal("Configuración", mAdmin.TabControlMain.SelectedTab?.Text);
            Assert.NotNull(mAdmin.SettingsControl);

            adminMain.ShowMaintainers("Mi Clave");
            Assert.Equal("Mi Clave", mAdmin.TabControlMain.SelectedTab?.Text);
            Assert.NotNull(mAdmin.PasswordControl);

            string[] adminTabs = mAdmin.TabControlMain.TabPages.Cast<TabPage>().Select(t => t.Text).ToArray();
            Assert.Equal(["Proveedores", "Solicitantes", "Firmas", "Árbol de Activos", "Editor de Árbol", "Configuración", "Mi Clave"], adminTabs);

            // Jefatura (con MenuMantenedor=true, no-admin): catálogos + Mi Clave, SIN Editor de Árbol ni Configuración
            using MaintainersForm mJef = new(temp.Context, jef);
            string[] jefTabs = mJef.TabControlMain.TabPages.Cast<TabPage>().Select(t => t.Text).ToArray();
            Assert.Equal(["Proveedores", "Solicitantes", "Firmas", "Árbol de Activos", "Mi Clave"], jefTabs);
            Assert.Null(mJef.TreeEditorView);
            Assert.Null(mJef.SettingsControl);

            // Usuario estándar (sin MenuMantenedor): únicamente Mi Clave dentro de Mantenedores
            using MaintainersForm mUsr = new(temp.Context, usr);
            string[] usrTabs = mUsr.TabControlMain.TabPages.Cast<TabPage>().Select(t => t.Text).ToArray();
            Assert.Equal(["Mi Clave"], usrTabs);

            // Seguridad backend/UI: módulos exclusivos de Administrador rechazados para no-admin
            Assert.Throws<UnauthorizedAccessException>(() => new SystemSettingsControl(temp.Context, jef));
            Assert.Throws<UnauthorizedAccessException>(() => new SystemSettingsControl(temp.Context, usr));
            Assert.Throws<UnauthorizedAccessException>(() => new AssetTreeEditorView(temp.Context, jef));
            Assert.Throws<UnauthorizedAccessException>(() => new AssetTreeEditorView(temp.Context, usr));
        });
    }

    [Fact]
    public void TopBarSimplification_SwitchUserAndAboutDialog_WorkEndToEnd()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "op_switch",
            NombreUsuario = "Elena",
            ApePatUsuario = "Vargas",
            Nivel = UserRoleLevel.Usuario,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "SwitchPass2026!", "SwitchPass2026!");

        UserSession op = temp.Context.AuthService.Login("op_switch", "SwitchPass2026!").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            using MainForm main = new(temp.Context, admin);

            // Barra superior derecha contiene únicamente los 3 botones requeridos
            FlowLayoutPanel? topFlow = main.BtnTopSwitchUser.Parent as FlowLayoutPanel;
            Assert.NotNull(topFlow);
            string[] topTexts = topFlow.Controls.OfType<Button>().Select(b => b.Text).OrderBy(x => x).ToArray();
            Assert.Equal(["Acerca de", "Cambiar usuario", "Cerrar sesión"], topTexts);

            // Cambio de usuario en caliente: Admin -> Operador sin cerrar aplicación
            Assert.True(main.NavUsers.Enabled);
            Assert.True(main.NavAudit.Enabled);
            main.SwitchActiveSession(op);
            Assert.Equal("op_switch", main.ActiveSession.Usuario.AliasUsuario);
            Assert.False(main.NavUsers.Enabled);
            Assert.False(main.NavAudit.Enabled);
            Assert.Contains("(op_switch)", main.Text, StringComparison.Ordinal);

            // Cambio inverso Operador -> Admin restaura privilegios de inmediato
            main.SwitchActiveSession(admin);
            Assert.Equal("admin", main.ActiveSession.Usuario.AliasUsuario);
            Assert.True(main.NavUsers.Enabled);
            Assert.True(main.NavAudit.Enabled);
            Assert.Contains("(admin)", main.Text, StringComparison.Ordinal);

            // Acerca de refleja metadatos centralizados
            using AboutDialog about = new(temp.Context);
            List<Label> labels = CollectControls<Label>(about);
            Assert.Contains(labels, l => l.Text.Contains(AppMetadata.Version, StringComparison.Ordinal));
            Assert.Contains(labels, l => l.Text.Contains(AppMetadata.ReleaseDate, StringComparison.Ordinal));
            Assert.Contains(labels, l => l.Text.Contains(AppMetadata.DeveloperCompany, StringComparison.Ordinal));
            Assert.Contains(labels, l => l.Text.Contains(AppMetadata.ShortName, StringComparison.Ordinal));
        });

        Assert.Contains(temp.Context.Audit.GetRecent(20), e => e.Accion == "CAMBIO_USUARIO" && e.Detalle.Contains("op_switch", StringComparison.Ordinal));
    }

    [Fact]
    public void RemovedPrintAndExportModules_AreAbsentFromUiAndPermissions()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        // Login y presets ya no otorgan ImpresionOt
        Assert.False(admin.Permisos.ImpresionOt);
        Assert.False(PermissionPresets.CreateForRole(UserRoleLevel.Administrador).ImpresionOt);
        Assert.False(PermissionPresets.CreateForRole(UserRoleLevel.Supervisor).ImpresionOt);
        Assert.False(PermissionPresets.CreateForRole(UserRoleLevel.Usuario).ImpresionOt);

        TempTestDatabase.RunOnStaThread(() =>
        {
            using WorkOrdersForm wo = new(temp.Context, admin);
            string[] tabTitles = wo.TabsMain.TabPages.Cast<TabPage>().Select(t => t.Text).ToArray();
            Assert.Equal(["Búsqueda de OT", "Ingreso / Edición de OT"], tabTitles);

            List<Button> buttons = CollectControls<Button>(wo);
            Assert.DoesNotContain(buttons, b => b.Text.Contains("Exportar / Imprimir", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(buttons, b => b.Text.Contains("Generar / Imprimir", StringComparison.OrdinalIgnoreCase));

            using UserPermissionsDialog permsDlg = new(UserRoleLevel.Supervisor, PermissionPresets.CreateForRole(UserRoleLevel.Supervisor), readOnly: false);
            Assert.DoesNotContain(CollectControls<CheckBox>(permsDlg), cb => cb.Text.Contains("Imprimir OT", StringComparison.OrdinalIgnoreCase) || cb.Text.Contains("Generar / Imprimir", StringComparison.OrdinalIgnoreCase));

            using UserProfileManagerControl profCtrl = new(temp.Context, admin);
            Assert.DoesNotContain(CollectControls<CheckBox>(profCtrl), cb => cb.Text.Contains("Imprimir OT", StringComparison.OrdinalIgnoreCase) || cb.Text.Contains("Generar / Imprimir", StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void ShowNetValueCheckbox_TogglesVisibilityPreservesDataAndControlsExternalOtExports()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        int provId = temp.Context.MaintainerService.GetProviders()[0].IdProv;
        int solId = temp.Context.MaintainerService.GetRequesters()[0].IdSol;
        int plantId = temp.Context.MaintainerService.GetAssetChildren(1)[0].IdActivo;
        int otNum = temp.Context.WorkOrderService.GetNextWorkOrderNumber();

        WorkOrder order = new()
        {
            NumeroOt = otNum,
            TipoOrden = "Externa",
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "Mantención de eje principal con control de visibilidad de monto",
            ValorNeto = 450000m,
            MostrarValorNeto = false,
            FirmasSeleccionadas = SignatureOption.JefaturaYTecnico,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Rectificado de eje y cambio de rodamientos" }]
        };
        order.SetLevels([1, plantId]);

        int idOt = temp.Context.WorkOrderService.Create(admin, order);
        WorkOrder loaded = temp.Context.WorkOrderService.GetById(idOt)!;

        // 1. Persistencia no destructiva en SQLite: valor_neto se conserva aunque mostrar_valor_neto sea false
        Assert.False(loaded.MostrarValorNeto);
        Assert.Equal(450000m, loaded.ValorNeto);

        // 2. Búsqueda oculta el valor en display pero mantiene el flag
        IReadOnlyList<WorkOrderSearchRow> searchRows = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
        {
            TextoBusqueda = "visibilidad de monto"
        });
        WorkOrderSearchRow row = Assert.Single(searchRows, r => r.NumeroOt == otNum);
        Assert.False(row.MostrarValorNeto);
        Assert.Equal("Oculto", row.ValorNetoDisplay);

        // 3. UI WinForms: al abrir la OT el checkbox carga desmarcado, oculta TxtValorNetoEntry
        //    y al volver a marcarlo restaura el valor almacenado ($ 450.000) sin perderlo
        TempTestDatabase.RunOnStaThread(() =>
        {
            using Form host = new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-2000, -2000), Size = new(1280, 820) };
            using WorkOrdersForm wo = new(temp.Context, admin);
            host.Controls.Add(wo);
            host.Show();
            Application.DoEvents();

            wo.OpenOrderByNumber(otNum);
            Application.DoEvents();

            Assert.False(wo.ChkMostrarValorNetoEntry.Checked);
            Assert.False(wo.TxtValorNetoEntry.Visible);

            // Volver a marcar muestra el campo con el valor original intacto
            wo.ChkMostrarValorNetoEntry.Checked = true;
            Application.DoEvents();
            Assert.True(wo.TxtValorNetoEntry.Visible);
            Assert.Contains("450.000", wo.TxtValorNetoEntry.Text, StringComparison.Ordinal);

            // Desmarcar oculta inmediatamente sin borrar datos en memoria
            wo.ChkMostrarValorNetoEntry.Checked = false;
            Application.DoEvents();
            Assert.False(wo.TxtValorNetoEntry.Visible);
            host.Hide();
        });

        // 4. Plantillas de exportación (PDF, Word, Excel, CSV): omiten VALOR NETO cuando MostrarValorNeto=false
        //    y lo incluyen cuando MostrarValorNeto=true
        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_netvis_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            string pdfHidden = Path.Combine(outDir, "ot_hidden.pdf");
            string docxHidden = Path.Combine(outDir, "ot_hidden.docx");
            string xlsxHidden = Path.Combine(outDir, "ot_hidden.xlsx");
            string csvHidden = Path.Combine(outDir, "ot_hidden.csv");

            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Pdf, pdfHidden, loaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Word, docxHidden, loaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Excel, xlsxHidden, loaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(loaded, ExportFormat.Csv, csvHidden, loaded.FirmasSeleccionadas);

            string pdfHiddenText = Encoding.GetEncoding(1252).GetString(File.ReadAllBytes(pdfHidden));
            Assert.DoesNotContain("VALOR NETO", pdfHiddenText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("450.000", pdfHiddenText, StringComparison.Ordinal);

            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxHidden, false))
            {
                string docText = doc.MainDocumentPart!.Document.Body!.InnerText;
                Assert.DoesNotContain("VALOR NETO", docText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("450.000", docText, StringComparison.Ordinal);
            }

            using (XLWorkbook wb = new(xlsxHidden))
            {
                string allCells = string.Join(" ", wb.Worksheets.First().CellsUsed().Select(c => c.GetString()));
                Assert.DoesNotContain("VALOR NETO", allCells, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("450000", allCells, StringComparison.Ordinal);
                Assert.DoesNotContain("450.000", allCells, StringComparison.Ordinal);
            }

            string csvHiddenText = File.ReadAllText(csvHidden, Encoding.UTF8);
            Assert.DoesNotContain("Valor Neto", csvHiddenText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("450.000", csvHiddenText, StringComparison.Ordinal);

            // Ahora activar MostrarValorNeto=true y actualizar: el valor 450000 sigue intacto y aparece en todas las exportaciones
            loaded.MostrarValorNeto = true;
            temp.Context.WorkOrderService.Update(admin, loaded);
            WorkOrder reloaded = temp.Context.WorkOrderService.GetById(idOt)!;
            Assert.True(reloaded.MostrarValorNeto);
            Assert.Equal(450000m, reloaded.ValorNeto);

            string pdfVisible = Path.Combine(outDir, "ot_visible.pdf");
            string docxVisible = Path.Combine(outDir, "ot_visible.docx");
            string xlsxVisible = Path.Combine(outDir, "ot_visible.xlsx");
            string csvVisible = Path.Combine(outDir, "ot_visible.csv");

            temp.Context.DocumentExportService.ExportWorkOrder(reloaded, ExportFormat.Pdf, pdfVisible, reloaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(reloaded, ExportFormat.Word, docxVisible, reloaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(reloaded, ExportFormat.Excel, xlsxVisible, reloaded.FirmasSeleccionadas);
            temp.Context.DocumentExportService.ExportWorkOrder(reloaded, ExportFormat.Csv, csvVisible, reloaded.FirmasSeleccionadas);

            string pdfVisibleText = Encoding.GetEncoding(1252).GetString(File.ReadAllBytes(pdfVisible));
            Assert.Contains("VALOR NETO", pdfVisibleText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("450.000", pdfVisibleText, StringComparison.Ordinal);

            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxVisible, false))
            {
                string docText = doc.MainDocumentPart!.Document.Body!.InnerText;
                Assert.Contains("VALOR NETO", docText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("450.000", docText, StringComparison.Ordinal);
            }

            using (XLWorkbook wb = new(xlsxVisible))
            {
                string allCells = string.Join(" ", wb.Worksheets.First().CellsUsed().Select(c => c.GetString()));
                Assert.Contains("VALOR NETO", allCells, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("450000", allCells, StringComparison.Ordinal);
            }

            string csvVisibleText = File.ReadAllText(csvVisible, Encoding.UTF8);
            Assert.Contains("Valor Neto", csvVisibleText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("450000", csvVisibleText, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void SystemConfiguration_TestApplyAndSave_EnforceRbacTempSessionAndCentralizedDbPersistence()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "sup_cfg",
            NombreUsuario = "Sofía",
            ApePatUsuario = "Rojas",
            Nivel = UserRoleLevel.Supervisor,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
        }, "SupCfg2026!", "SupCfg2026!");
        UserSession sup = temp.Context.AuthService.Login("sup_cfg", "SupCfg2026!").Session!;

        string originalCompany = temp.Context.Settings.CompanyName;
        string originalDept = temp.Context.Settings.CompanyDepartment;
        string dbPath = temp.DbPath;

        AppSettings validCandidate = new()
        {
            DatabasePath = dbPath,
            CompanyName = "CAMBIASO HNOS S.A.C. — PLANTA QA",
            CompanyRut = "91.438.000-6",
            CompanyDepartment = "GERENCIA DE MANTENCIÓN",
            CompanyAddress = "Av. Industrial 999, Valparaíso"
        };

        // 1. Seguridad RBAC: no-admin no puede probar, aplicar ni guardar configuración
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.SystemConfigService.TestConfiguration(sup, validCandidate));
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.SystemConfigService.ApplyTemporaryConfiguration(sup, validCandidate));
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.SystemConfigService.SaveConfiguration(sup, validCandidate));
        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "ACCESO_NO_AUTORIZADO" && a.Usuario == "sup_cfg");

        // 2. Probar (Test): valida errores sin modificar estado activo ni BD
        SystemConfigTestResult badRut = temp.Context.SystemConfigService.TestConfiguration(admin, new AppSettings
        {
            DatabasePath = dbPath,
            CompanyName = "Empresa Test",
            CompanyRut = "11.111.111-9",
            CompanyDepartment = "Dept",
            CompanyAddress = "Dir"
        });
        Assert.False(badRut.Succeeded);

        SystemConfigTestResult okTest = temp.Context.SystemConfigService.TestConfiguration(admin, validCandidate);
        Assert.True(okTest.Succeeded);
        Assert.Equal(originalCompany, temp.Context.Settings.CompanyName);
        Assert.False(temp.Context.SystemConfigService.HasTemporaryChanges);

        // 3. Aplicar (temporal): refleja cambios en sesión activa sin guardar en BD centralizada
        bool? lastEventIsTemp = null;
        temp.Context.ConfigurationChanged += (_, isTemp) => lastEventIsTemp = isTemp;

        temp.Context.SystemConfigService.ApplyTemporaryConfiguration(admin, validCandidate);
        Assert.True(temp.Context.SystemConfigService.HasTemporaryChanges);
        Assert.Equal(true, lastEventIsTemp);
        Assert.Equal("CAMBIASO HNOS S.A.C. — PLANTA QA", temp.Context.Settings.CompanyName);

        // Una nueva instancia abierta sobre la misma BD NO debe ver los cambios temporales
        using (SisproApplicationContext freshBeforeSave = new(new AppSettings { DatabasePath = dbPath }))
        {
            Assert.NotEqual("CAMBIASO HNOS S.A.C. — PLANTA QA", freshBeforeSave.Settings.CompanyName);
        }

        // Revertir cambios temporales restaura la configuración persistida
        temp.Context.SystemConfigService.RevertTemporaryConfiguration(admin);
        Assert.False(temp.Context.SystemConfigService.HasTemporaryChanges);
        Assert.Equal(originalCompany, temp.Context.Settings.CompanyName);
        Assert.Equal(originalDept, temp.Context.Settings.CompanyDepartment);

        // 4. Guardar (permanente): persiste en SQLite ConfiguracionSistema, audita y sobrevive reinicio / otros equipos
        temp.Context.SystemConfigService.SaveConfiguration(admin, validCandidate);
        Assert.False(temp.Context.SystemConfigService.HasTemporaryChanges);
        Assert.Equal(false, lastEventIsTemp);
        Assert.Equal("CAMBIASO HNOS S.A.C. — PLANTA QA", temp.Context.Settings.CompanyName);
        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "GUARDAR_CONFIGURACION" && a.Usuario == "admin");

        using (SisproApplicationContext freshAfterSave = new(new AppSettings { DatabasePath = dbPath }))
        {
            Assert.Equal("CAMBIASO HNOS S.A.C. — PLANTA QA", freshAfterSave.Settings.CompanyName);
            Assert.Equal("91.438.000-6", freshAfterSave.Settings.CompanyRut);
            Assert.Equal("GERENCIA DE MANTENCIÓN", freshAfterSave.Settings.CompanyDepartment);
            Assert.Equal("Av. Industrial 999, Valparaíso", freshAfterSave.Settings.CompanyAddress);
        }
    }

    [Fact]
    public void WorkOrderSearchCrud_SupportsExternalAndInternalCreateConsultEditAndDelete()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        int provId = temp.Context.MaintainerService.GetProviders()[0].IdProv;
        int solId = temp.Context.MaintainerService.GetRequesters()[0].IdSol;
        int plantId = temp.Context.MaintainerService.GetAssetChildren(1)[0].IdActivo;

        // 1. Crear OT Externa y OT Interna
        int extNum = temp.Context.WorkOrderService.GetNextWorkOrderNumber();
        WorkOrder extOrder = new()
        {
            NumeroOt = extNum,
            TipoOrden = "Externa",
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "OT Externa CRUD QA",
            ValorNeto = 125000m,
            MostrarValorNeto = true,
            Detalles = [new WorkOrderDetail { Cantidad = 2, Descripcion = "Reparación válvula externa" }]
        };
        extOrder.SetLevels([1, plantId]);
        int extId = temp.Context.WorkOrderService.Create(admin, extOrder);

        int intNum = temp.Context.WorkOrderService.GetNextWorkOrderNumber();
        WorkOrder intOrder = new()
        {
            NumeroOt = intNum,
            TipoOrden = "Interna",
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "OT Interna CRUD QA",
            ValorNeto = 0m,
            MostrarValorNeto = false,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Inspección interna de tablero eléctrico" }]
        };
        intOrder.SetLevels([1, plantId]);
        int intId = temp.Context.WorkOrderService.Create(admin, intOrder);

        WorkOrder loadedExt = temp.Context.WorkOrderService.GetById(extId)!;
        WorkOrder loadedInt = temp.Context.WorkOrderService.GetById(intId)!;
        Assert.Equal("Externa", loadedExt.TipoOrden);
        Assert.Equal("Interna", loadedInt.TipoOrden);

        // 2. Verificar Búsqueda de OT es pestaña por defecto con botones CRUD (Nuevo, Consultar, Editar, Eliminar)
        //    y WorkOrderDetailDialog muestra la información completa de la OT
        TempTestDatabase.RunOnStaThread(() =>
        {
            using Form host = new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-2000, -2000), Size = new(1280, 820) };
            using WorkOrdersForm wo = new(temp.Context, admin);
            host.Controls.Add(wo);
            host.Show();
            Application.DoEvents();

            Assert.Equal("Búsqueda de OT", wo.TabsMain.SelectedTab?.Text);
            Assert.Equal("Nuevo", wo.BtnSearchNewOt.Text);
            Assert.Equal("Consultar", wo.BtnSearchConsultOt.Text);
            Assert.Equal("Editar", wo.BtnSearchEditOt.Text);
            Assert.Equal("Eliminar", wo.BtnSearchDeleteOt.Text);

            using WorkOrderDetailDialog detailExt = new(temp.Context, loadedExt);
            List<Label> extLabels = CollectControls<Label>(detailExt);
            Assert.Contains(extLabels, l => l.Text.Contains("Externa", StringComparison.Ordinal));
            Assert.Contains(extLabels, l => l.Text.Contains("125.000", StringComparison.Ordinal));

            using WorkOrderDetailDialog detailInt = new(temp.Context, loadedInt);
            List<Label> intLabels = CollectControls<Label>(detailInt);
            Assert.Contains(intLabels, l => l.Text.Contains("Interna", StringComparison.Ordinal));
            Assert.Contains(intLabels, l => l.Text.Contains("Oculto", StringComparison.OrdinalIgnoreCase));
            host.Hide();
        });

        // 3. Editar OT y verificar reflejo inmediato en búsqueda
        loadedInt.Observacion = "OT Interna CRUD QA ACTUALIZADA";
        temp.Context.WorkOrderService.Update(admin, loadedInt);
        Assert.Single(temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter { TextoBusqueda = "CRUD QA ACTUALIZADA" }));

        // 4. Eliminar OT con trazabilidad en auditoría
        temp.Context.WorkOrderService.Delete(admin, intId);
        Assert.Null(temp.Context.WorkOrderService.GetById(intId));
        Assert.Empty(temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter { TextoBusqueda = "CRUD QA ACTUALIZADA" }));
        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "ELIMINAR_OT" && a.Detalle.Contains(intNum.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public void WorkOrderStatus_CreateEditRightClickMenuAndBatchUpdate_SupportsAllSixStatusesWithRbacAndAudit()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        // Usuario solo lectura de búsqueda (sin IngresoOt) no puede cambiar estado
        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "consulta_est",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Soto",
            Nivel = UserRoleLevel.Usuario,
            Permisos = new UserPermissions
            {
                MenuOrdenesTrabajo = true,
                BusquedaOt = true,
                IngresoOt = false
            }
        }, "Consulta2026!", "Consulta2026!");
        UserSession readOnlySession = temp.Context.AuthService.Login("consulta_est", "Consulta2026!").Session!;

        int provId = temp.Context.MaintainerService.GetProviders()[0].IdProv;
        int solId = temp.Context.MaintainerService.GetRequesters()[0].IdSol;
        int plantId = temp.Context.MaintainerService.GetAssetChildren(1)[0].IdActivo;

        // 1. Ingresar nueva OT seleccionando estado explícito "En Curso"
        int otNum1 = temp.Context.WorkOrderService.GetNextWorkOrderNumber();
        WorkOrder order1 = new()
        {
            NumeroOt = otNum1,
            TipoOrden = "Externa",
            Estado = WorkOrderStatus.EnCurso,
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "OT prueba estados v4.6.1",
            ValorNeto = 50000m,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Prueba estado inicial En Curso" }]
        };
        order1.SetLevels([1, plantId]);
        int idOt1 = temp.Context.WorkOrderService.Create(admin, order1);

        int otNum2 = temp.Context.WorkOrderService.GetNextWorkOrderNumber();
        WorkOrder order2 = new()
        {
            NumeroOt = otNum2,
            TipoOrden = "Externa",
            Estado = WorkOrderStatus.Abierta,
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "OT prueba lote estados v4.6.1",
            ValorNeto = 25000m,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Prueba cambio estado masivo" }]
        };
        order2.SetLevels([1, plantId]);
        int idOt2 = temp.Context.WorkOrderService.Create(admin, order2);

        WorkOrder loaded1 = temp.Context.WorkOrderService.GetById(idOt1)!;
        Assert.Equal(WorkOrderStatus.EnCurso, loaded1.Estado);
        Assert.Equal("En Curso", loaded1.Estado.ToDisplayName());

        // 2. Al editar una OT, cambiar estado a "Modificada" persiste correctamente
        loaded1.Estado = WorkOrderStatus.Modificada;
        temp.Context.WorkOrderService.Update(admin, loaded1);
        Assert.Equal(WorkOrderStatus.Modificada, temp.Context.WorkOrderService.GetById(idOt1)!.Estado);

        // 3. Validar transición por servicio a los 6 estados oficiales + filtro de búsqueda por estado + auditoría
        WorkOrderStatus[] allSix =
        [
            WorkOrderStatus.Abierta,
            WorkOrderStatus.EnCurso,
            WorkOrderStatus.Modificada,
            WorkOrderStatus.Terminada,
            WorkOrderStatus.Cerrada,
            WorkOrderStatus.Anulada
        ];
        foreach (WorkOrderStatus target in allSix)
        {
            temp.Context.WorkOrderService.UpdateStatus(admin, idOt1, target);
            WorkOrder after = temp.Context.WorkOrderService.GetById(idOt1)!;
            Assert.Equal(target, after.Estado);

            IReadOnlyList<WorkOrderSearchRow> filtered = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
            {
                NumeroOt = otNum1,
                Estado = target
            });
            WorkOrderSearchRow row = Assert.Single(filtered);
            Assert.Equal(target.ToDisplayName(), row.EstadoDisplay);
        }

        Assert.Contains(temp.Context.Audit.GetRecent(30), a => a.Accion == "CAMBIAR_ESTADO_OT" && a.Detalle.Contains("Anulada", StringComparison.Ordinal));

        // 4. Seguridad RBAC: usuario sin IngresoOt no puede cambiar estado
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.WorkOrderService.UpdateStatus(readOnlySession, idOt1, WorkOrderStatus.Abierta));
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.WorkOrderService.UpdateStatusMany(readOnlySession, [idOt1, idOt2], WorkOrderStatus.Cerrada));

        // 5. Cambio por lote (UpdateStatusMany)
        int batchUpdated = temp.Context.WorkOrderService.UpdateStatusMany(admin, [idOt1, idOt2], WorkOrderStatus.Terminada);
        Assert.Equal(2, batchUpdated);
        Assert.Equal(WorkOrderStatus.Terminada, temp.Context.WorkOrderService.GetById(idOt1)!.Estado);
        Assert.Equal(WorkOrderStatus.Terminada, temp.Context.WorkOrderService.GetById(idOt2)!.Estado);
        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "CAMBIAR_ESTADO_OT_LOTE" && a.Detalle.Contains("Terminada", StringComparison.Ordinal));

        // 6. Verificación UI STA: combo de estado en Ingreso/Edición y menú contextual clic derecho con los 6 estados
        TempTestDatabase.RunOnStaThread(() =>
        {
            using Form host = new() { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-2000, -2000), Size = new(1380, 840) };
            using WorkOrdersForm wo = new(temp.Context, admin);
            host.Controls.Add(wo);
            host.Show();
            Application.DoEvents();

            // Combo de ingreso/edición ofrece exactamente los 6 estados requeridos
            string[] comboLabels = wo.CmbEstadoEntry.Items.Cast<WorkOrderStatusOption>().Select(x => x.DisplayName).ToArray();
            Assert.Equal(["Abierta", "En Curso", "Modificada", "Terminada", "Cerrada", "Anulada"], comboLabels);

            // Al abrir la OT1 en edición, el combo refleja automáticamente "Terminada"
            wo.OpenOrderByNumber(otNum1);
            Application.DoEvents();
            Assert.Equal(WorkOrderStatus.Terminada, ((WorkOrderStatusOption)wo.CmbEstadoEntry.SelectedItem!).Value);

            // En Búsqueda de OT, el menú clic derecho ofrece submenú "Cambiar estado" con las 6 opciones
            wo.OpenSearchTab();
            Application.DoEvents();
            int rowIdx = -1;
            for (int i = 0; i < wo.GridSearchResults.Rows.Count; i++)
            {
                if (Sispro.UI.Theme.GridBindingHelper.GetRowSource<WorkOrderSearchRow>(wo.GridSearchResults, i)?.IdOt == idOt1)
                {
                    rowIdx = i;
                    break;
                }
            }

            Assert.True(rowIdx >= 0, "La OT creada debe aparecer en la grilla de Búsqueda de OT.");
            ToolStripItem[] menuItems = wo.BuildSearchContextMenuItems(rowIdx);
            ToolStripMenuItem statusMenu = Assert.Single(menuItems.OfType<ToolStripMenuItem>(), m => m.Text?.StartsWith("Cambiar estado", StringComparison.Ordinal) == true);
            Assert.Equal(6, statusMenu.DropDownItems.Count);

            ToolStripMenuItem cerradaItem = Assert.Single(statusMenu.DropDownItems.OfType<ToolStripMenuItem>(), m => m.Text?.StartsWith("Cerrada", StringComparison.Ordinal) == true);
            Assert.IsType<Action>(cerradaItem.Tag);
            ((Action)cerradaItem.Tag!).Invoke();
            Application.DoEvents();

            // Verificar persistencia inmediata en BD y sincronización con combo de edición
            Assert.Equal(WorkOrderStatus.Cerrada, temp.Context.WorkOrderService.GetById(idOt1)!.Estado);
            Assert.Equal(WorkOrderStatus.Cerrada, ((WorkOrderStatusOption)wo.CmbEstadoEntry.SelectedItem!).Value);

            // Limpiar items construidos manualmente para el test sin liberar íconos cacheados
            foreach (ToolStripItem item in menuItems)
            {
                item.Image = null;
                if (item is ToolStripDropDownItem dd)
                {
                    foreach (ToolStripItem sub in dd.DropDownItems)
                    {
                        sub.Image = null;
                    }
                }
                item.Dispose();
            }

            // WorkOrderDetailDialog muestra correctamente "En Curso"
            temp.Context.WorkOrderService.UpdateStatus(admin, idOt1, WorkOrderStatus.EnCurso);
            using WorkOrderDetailDialog detailDlg = new(temp.Context, temp.Context.WorkOrderService.GetById(idOt1)!);
            Assert.Contains(CollectControls<Label>(detailDlg), l => l.Text.Contains("En Curso", StringComparison.Ordinal));

            host.Hide();
        });
    }

    private static List<T> CollectControls<T>(Control root) where T : Control
    {
        List<T> list = [];
        void Walk(Control current)
        {
            if (current is T typed) list.Add(typed);
            foreach (Control child in current.Controls) Walk(child);
            if (current is TabControl tc)
            {
                foreach (TabPage page in tc.TabPages)
                {
                    if (!tc.Controls.Contains(page)) Walk(page);
                }
            }
        }
        Walk(root);
        return list;
    }

    private static T? FindFirstChild<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T typed) return typed;
            T? found = FindFirstChild<T>(c);
            if (found is not null) return found;
        }
        return null;
    }
}
