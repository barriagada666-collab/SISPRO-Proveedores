using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Core.Security;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class MdbImportTests
{
    private sealed class FakeMdbReader(MdbSnapshot snapshot) : IMdbTableReader
    {
        public MdbSnapshot ReadSnapshot(string mdbPath) => snapshot;
    }

    [Fact]
    public void AnalyzeMdb_ComputesExactDiffCounts_NewUpdatedAndUnchanged()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        int provUpdatedId = temp.Context.Providers.Create(new Provider
        {
            Nombre = "PROV ALPHA",
            Direccion = "Dirección Vieja 100",
            Telefono = "220011",
            Fax = "",
            Correo = "viejo@alpha.cl",
            Contacto = "Contacto SISPRO",
            CorreoContacto = "directo@alpha.cl",
            Observaciones = "Nota interna SISPRO",
            Activo = true
        });
        temp.Context.Providers.Create(new Provider
        {
            Nombre = "PROV IGUAL",
            Direccion = "Av. Igual 50",
            Telefono = "555000",
            Fax = "555001",
            Correo = "igual@prov.cl",
            Activo = true
        });

        FakeMdbReader fakeReader = new(new MdbSnapshot
        {
            MdbPath = @"C:\fake\OT_PROV.mdb",
            FileSizeBytes = 1024,
            Providers =
            [
                new MdbProviderRow
                {
                    LegacyIdProv = 10,
                    Nombre = "PROV ALPHA",
                    Direccion = "Dirección Nueva 200",
                    Telefono = "", // Vacío no debe borrar teléfono actual por defecto
                    Fax = "229900",
                    Correo = "nuevo@alpha.cl"
                },
                new MdbProviderRow
                {
                    LegacyIdProv = 11,
                    Nombre = "PROV IGUAL",
                    Direccion = "Av. Igual 50",
                    Telefono = "555000",
                    Fax = "555001",
                    Correo = "igual@prov.cl"
                },
                new MdbProviderRow
                {
                    LegacyIdProv = 12,
                    Nombre = "PROV NUEVO MDB",
                    Direccion = "Los Carreras 300",
                    Telefono = "321654",
                    Fax = "",
                    Correo = "contacto@nuevo.cl"
                }
            ],
            Requesters =
            [
                new MdbRequesterRow
                {
                    LegacyIdSol = 7,
                    Alias = "JPerez",
                    Nombre = "Juan",
                    ApellidoPaterno = "Pérez",
                    Telefono = "999888",
                    Correo = "jperez@cambiaso.cl"
                }
            ],
            WorkOrders =
            [
                new MdbWorkOrderRow
                {
                    LegacyIdOt = 5001,
                    NumeroOt = 99901,
                    LegacyIdProv = 12,
                    LegacyIdSol = 7,
                    FechaOt = new DateTime(2026, 11, 24),
                    ActivoFlag = false,
                    IdN1 = 1,
                    IdNivel = 1
                }
            ],
            WorkOrderDetails =
            [
                new MdbWorkOrderDetailRow
                {
                    LegacyIdDet = 9001,
                    LegacyIdOt = 5001,
                    Cantidad = 2,
                    Detalle = "Fabricación de eje inox",
                    EstadoDet = false
                }
            ]
        });

        MdbImportService service = new(
            temp.Context.ConnectionFactory,
            temp.Context.Providers,
            temp.Context.Requesters,
            temp.Context.Audit,
            fakeReader,
            temp.Context.SyncHub,
            temp.Context.Users);

        MdbImportPreview preview = service.AnalyzeMdb(admin, @"C:\fake\OT_PROV.mdb");

        Assert.Equal(3, preview.MdbProvidersTotal);
        Assert.Equal(2, preview.DbProvidersBefore);
        Assert.Equal(1, preview.NewProvidersCount);
        Assert.Equal(1, preview.UpdatedProvidersCount);
        Assert.Equal(1, preview.UnchangedProvidersCount);
        Assert.Equal(1, preview.NewRequestersCount);
        Assert.Equal(1, preview.NewWorkOrdersCount);
        Assert.Equal(1, preview.NewWorkOrderDetailsCount);

        MdbProviderDiffItem alphaDiff = Assert.Single(preview.ProviderDiffs, d => d.Nombre == "PROV ALPHA");
        Assert.Equal(MdbDiffAction.Actualizar, alphaDiff.Action);
        Assert.Equal(provUpdatedId, alphaDiff.TargetIdProv);
        Assert.Equal("220011", alphaDiff.TelefonoFinal); // No sobreescrito por vacío
        Assert.Equal("Dirección Nueva 200", alphaDiff.DireccionFinal);
        Assert.Contains("Dirección", alphaDiff.ChangedFields);
        Assert.Contains("Fax", alphaDiff.ChangedFields);
        Assert.Contains("Correo", alphaDiff.ChangedFields);
        Assert.DoesNotContain("Teléfono", alphaDiff.ChangedFields);
    }

    [Fact]
    public void ApplyImport_InsertsUpdatesPreservesExtrasCreatesBackupAndAudits()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        int rootAssetId = temp.Context.AssetTree.Create(new AssetNode
        {
            ParentId = null,
            Nivel = 1,
            Descripcion = "PLANTA TEST",
            Activo = true
        });

        int existingSolId = temp.Context.Requesters.Create(new Requester
        {
            Alias = "MSilva",
            Nombre = "Marcelo",
            ApellidoPaterno = "Silva",
            Telefono = "111",
            Correo = "old@cambiaso.cl",
            FirmaBytes = [1, 2, 3, 4],
            FirmaNombre = "firma_msilva.png",
            FirmaTipo = ".png",
            FirmaPreviewPng = [5, 6, 7, 8],
            Activo = true
        });

        int alphaId = temp.Context.Providers.Create(new Provider
        {
            Nombre = "PROV ALPHA",
            Direccion = "Dir Vieja",
            Telefono = "220011",
            Correo = "old@alpha.cl",
            Contacto = "Contacto Exclusivo SISPRO",
            CorreoContacto = "contacto@alpha.cl",
            Observaciones = "Observación preservada",
            Activo = true
        });

        FakeMdbReader fakeReader = new(new MdbSnapshot
        {
            MdbPath = @"C:\fake\OT_PROV.mdb",
            FileSizeBytes = 2048,
            Providers =
            [
                new MdbProviderRow
                {
                    LegacyIdProv = 10,
                    Nombre = "PROV ALPHA",
                    Direccion = "Dir Actualizada",
                    Telefono = "",
                    Fax = "228899",
                    Correo = "nuevo@alpha.cl"
                },
                new MdbProviderRow
                {
                    LegacyIdProv = 20,
                    Nombre = "MAESTRANZA NUEVA",
                    Direccion = "Calle Industrial 45",
                    Telefono = "3220001",
                    Fax = "",
                    Correo = "ventas@maestranza.cl"
                }
            ],
            Requesters =
            [
                new MdbRequesterRow
                {
                    LegacyIdSol = 4,
                    Alias = "MSilva",
                    Nombre = "Marcelo",
                    ApellidoPaterno = "Silva",
                    Telefono = "999111",
                    Correo = "msilva@cambiaso.cl"
                }
            ],
            WorkOrders =
            [
                new MdbWorkOrderRow
                {
                    LegacyIdOt = 700,
                    NumeroOt = 88001,
                    LegacyIdProv = 20,
                    LegacyIdSol = 4,
                    FechaOt = new DateTime(2026, 11, 24),
                    ActivoFlag = false,
                    IdN1 = rootAssetId,
                    IdNivel = 1
                }
            ],
            WorkOrderDetails =
            [
                new MdbWorkOrderDetailRow
                {
                    LegacyIdDet = 1,
                    LegacyIdOt = 700,
                    Cantidad = 3,
                    Detalle = "Rectificado de rodillos",
                    EstadoDet = false
                }
            ]
        });

        MdbImportService service = new(
            temp.Context.ConnectionFactory,
            temp.Context.Providers,
            temp.Context.Requesters,
            temp.Context.Audit,
            fakeReader,
            temp.Context.SyncHub,
            temp.Context.Users);

        MdbImportResult result = service.ApplyImport(admin, @"C:\fake\OT_PROV.mdb", new MdbImportOptions
        {
            InsertNewProviders = true,
            UpdateExistingProviders = true,
            OverwriteWithEmptyValues = false,
            SyncNewRequesters = true,
            SyncNewWorkOrders = true,
            CreateBackupBeforeApply = true
        });

        Assert.Equal(1, result.ProvidersInserted);
        Assert.Equal(1, result.ProvidersUpdated);
        Assert.Equal(0, result.RequestersInserted);
        Assert.Equal(1, result.RequestersUpdated);
        Assert.Equal(1, result.WorkOrdersInserted);
        Assert.Equal(1, result.WorkOrderDetailsInserted);
        Assert.Equal(2, result.DbProvidersAfter);
        Assert.Equal(1, result.DbWorkOrdersAfter);
        Assert.False(string.IsNullOrWhiteSpace(result.BackupPath));
        Assert.True(File.Exists(result.BackupPath!));

        Provider updatedAlpha = temp.Context.Providers.GetById(alphaId)!;
        Assert.Equal("Dir Actualizada", updatedAlpha.Direccion);
        Assert.Equal("220011", updatedAlpha.Telefono);
        Assert.Equal("228899", updatedAlpha.Fax);
        Assert.Equal("nuevo@alpha.cl", updatedAlpha.Correo);
        Assert.Equal("Contacto Exclusivo SISPRO", updatedAlpha.Contacto);
        Assert.Equal("contacto@alpha.cl", updatedAlpha.CorreoContacto);
        Assert.Equal("Observación preservada", updatedAlpha.Observaciones);

        Provider newProv = temp.Context.Providers.GetByName("MAESTRANZA NUEVA")!;
        Assert.Equal("Calle Industrial 45", newProv.Direccion);
        Assert.Equal("ventas@maestranza.cl", newProv.Correo);

        // La firma digital del solicitante existente se mantiene intacta
        Requester msilva = temp.Context.Requesters.GetById(existingSolId)!;
        Assert.Equal("999111", msilva.Telefono);
        Assert.Equal("msilva@cambiaso.cl", msilva.Correo);
        Assert.NotNull(msilva.FirmaBytes);
        Assert.Equal(4, msilva.FirmaBytes!.Length);

        // La OT importada quedó vinculada correctamente
        WorkOrder importedOt = temp.Context.WorkOrders.GetByNumber(88001)!;
        Assert.Equal(newProv.IdProv, importedOt.IdProv);
        Assert.Equal(existingSolId, importedOt.IdSol);
        Assert.Equal("PLANTA TEST", importedOt.RutaActivo);
        Assert.Single(importedOt.Detalles);
        Assert.Equal("Rectificado de rodillos", importedOt.Detalles[0].Descripcion);

        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "IMPORTAR_MDB");
    }

    [Fact]
    public void ApplyImport_OverwriteWithEmptyValues_ClearsTargetFields()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        int id = temp.Context.Providers.Create(new Provider
        {
            Nombre = "PROV EMPTY",
            Direccion = "Dir Antigua",
            Telefono = "999",
            Correo = "mail@prov.cl",
            Activo = true
        });

        FakeMdbReader fakeReader = new(new MdbSnapshot
        {
            MdbPath = @"C:\fake\OT_PROV.mdb",
            Providers =
            [
                new MdbProviderRow
                {
                    LegacyIdProv = 1,
                    Nombre = "PROV EMPTY",
                    Direccion = "Dir Nueva",
                    Telefono = "",
                    Fax = "",
                    Correo = ""
                }
            ]
        });

        MdbImportService service = new(
            temp.Context.ConnectionFactory,
            temp.Context.Providers,
            temp.Context.Requesters,
            temp.Context.Audit,
            fakeReader,
            users: temp.Context.Users);

        MdbImportResult res = service.ApplyImport(admin, @"C:\fake\OT_PROV.mdb", new MdbImportOptions
        {
            OverwriteWithEmptyValues = true,
            CreateBackupBeforeApply = false
        });

        Assert.Equal(1, res.ProvidersUpdated);
        Provider reloaded = temp.Context.Providers.GetById(id)!;
        Assert.Equal("Dir Nueva", reloaded.Direccion);
        Assert.Equal(string.Empty, reloaded.Telefono);
        Assert.Equal(string.Empty, reloaded.Correo);
    }

    [Fact]
    public void AnalyzeAndApply_RejectsNonAdminUsers()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        User operador = new()
        {
            IdUsuario = "128498745",
            AliasUsuario = "operador1",
            NombreUsuario = "Op",
            ApePatUsuario = "Uno",
            Nivel = UserRoleLevel.Usuario,
            Activo = true,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        };
        operador.Permisos.MenuMantenedor = true;
        temp.Context.UserManagementService.CreateUser(admin, operador, "Op#Pass2026!", "Op#Pass2026!");

        UserSession opSession = temp.Context.AuthService.Login("operador1", "Op#Pass2026!").Session!;

        Assert.Throws<UnauthorizedAccessException>(() =>
            temp.Context.MdbImportService.AnalyzeMdb(opSession, @"C:\fake\OT_PROV.mdb"));
        Assert.Throws<UnauthorizedAccessException>(() =>
            temp.Context.MdbImportService.ApplyImport(opSession, @"C:\fake\OT_PROV.mdb"));
        Assert.Throws<UnauthorizedAccessException>(() =>
            temp.Context.MdbImportService.GetOperationalCounts(opSession));
        Assert.Throws<UnauthorizedAccessException>(() =>
            temp.Context.MdbImportService.CleanOperationalDatabase(opSession, createBackup: false));
    }

    [Fact]
    public void CleanOperationalDatabase_ClearsOperationalDataPreservesSecurityAssetsAndConfigCreatesBackupAndAudits()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        int provId = temp.Context.Providers.Create(new Provider { Nombre = "PROV VIEJO", Direccion = "Av 1", Activo = true });
        int solId = temp.Context.Requesters.Create(new Requester { Alias = "OldSol", Nombre = "Juan", ApellidoPaterno = "Viejo", Activo = true });
        int plantId = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA CONSERVADA", AssetNodeTypes.Planta);

        WorkOrder ot = new()
        {
            NumeroOt = 77001,
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Detalles = [new WorkOrderDetail { Cantidad = 2, Descripcion = "Trabajo a limpiar" }]
        };
        ot.SetLevels([1, plantId]);
        temp.Context.WorkOrderService.Create(admin, ot);

        temp.Context.SystemConfigService.SaveConfiguration(admin, new AppSettings
        {
            DatabasePath = temp.DbPath,
            CompanyName = "EMPRESA CONSERVADA S.A.",
            CompanyRut = "91.438.000-6",
            CompanyDepartment = "MANTENCION",
            CompanyAddress = "Placilla 777"
        });

        OperationalDatabaseCounts before = temp.Context.MdbImportService.GetOperationalCounts(admin);
        Assert.Equal(1, before.ProvidersCount);
        Assert.Equal(1, before.RequestersCount);
        Assert.Equal(1, before.WorkOrdersCount);
        Assert.Equal(1, before.WorkOrderDetailsCount);

        MdbDatabaseCleanResult cleanRes = temp.Context.MdbImportService.CleanOperationalDatabase(admin, createBackup: true);

        Assert.Equal(1, cleanRes.ProvidersDeleted);
        Assert.Equal(1, cleanRes.RequestersDeleted);
        Assert.Equal(1, cleanRes.WorkOrdersDeleted);
        Assert.Equal(1, cleanRes.WorkOrderDetailsDeleted);
        Assert.False(string.IsNullOrWhiteSpace(cleanRes.BackupPath));
        Assert.True(File.Exists(cleanRes.BackupPath!));

        OperationalDatabaseCounts after = temp.Context.MdbImportService.GetOperationalCounts(admin);
        Assert.Equal(0, after.ProvidersCount);
        Assert.Equal(0, after.RequestersCount);
        Assert.Equal(0, after.WorkOrdersCount);
        Assert.Equal(0, after.WorkOrderDetailsCount);

        // Usuarios, Árbol de Activos, Configuración del Sistema y Auditoría permanecen intactos
        Assert.NotNull(temp.Context.Users.GetByAlias("admin"));
        Assert.NotNull(temp.Context.AssetTree.GetById(plantId));
        Assert.Equal("EMPRESA CONSERVADA S.A.", temp.Context.SystemConfig.GetValue("CompanyName"));
        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "LIMPIAR_BD_OPERATIVA");
    }

    [Fact]
    public void ApplyImport_RewriteAllFromMdb_ReplacesOperationalDataAtomicallyAndPreservesAdminUsersAndAssets()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        int plantId = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA REWRITE", AssetNodeTypes.Planta);
        int oldProvId = temp.Context.Providers.Create(new Provider { Nombre = "PROV OBSOLETO A BORRAR", Direccion = "Vieja 99", Activo = true });
        int oldSolId = temp.Context.Requesters.Create(new Requester { Alias = "SolObsoleto", Nombre = "Viejo", ApellidoPaterno = "Sol", Activo = true });

        WorkOrder oldOt = new()
        {
            NumeroOt = 55555,
            IdProv = oldProvId,
            IdSol = oldSolId,
            FechaOt = DateTime.Today.AddDays(-10),
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "OT obsoleta a eliminar" }]
        };
        oldOt.SetLevels([1, plantId]);
        temp.Context.WorkOrderService.Create(admin, oldOt);

        FakeMdbReader fakeReader = new(new MdbSnapshot
        {
            MdbPath = @"C:\fake\OT_PROV_LIMPIO.mdb",
            FileSizeBytes = 4096,
            Providers =
            [
                new MdbProviderRow { LegacyIdProv = 101, Nombre = "PROV LIMPIO UNO", Direccion = "Calle Nueva 1", Telefono = "3221111", Correo = "uno@prov.cl" },
                new MdbProviderRow { LegacyIdProv = 102, Nombre = "PROV LIMPIO DOS", Direccion = "Calle Nueva 2", Telefono = "3222222", Correo = "dos@prov.cl" }
            ],
            Requesters =
            [
                new MdbRequesterRow { LegacyIdSol = 10, Alias = "SolNuevo", Nombre = "Ana", ApellidoPaterno = "Torres", Telefono = "900111", Correo = "ana@cambiaso.cl" },
                new MdbRequesterRow { LegacyIdSol = 11, Alias = "solnuevo", Nombre = "Ana", ApellidoPaterno = "Torres", Telefono = "", Correo = "" } // Duplicado consolidado
            ],
            WorkOrders =
            [
                new MdbWorkOrderRow
                {
                    LegacyIdOt = 3001,
                    NumeroOt = 90001,
                    LegacyIdProv = 101,
                    LegacyIdSol = 11, // Referencia al duplicado consolidado -> debe mapear bien
                    FechaOt = new DateTime(2026, 11, 25),
                    ActivoFlag = true,
                    IdN1 = 1,
                    IdN2 = plantId,
                    IdNivel = 2
                }
            ],
            WorkOrderDetails =
            [
                new MdbWorkOrderDetailRow { LegacyIdDet = 1, LegacyIdOt = 3001, Cantidad = 4, Detalle = "Mecanizado limpio desde cero", EstadoDet = true }
            ]
        });

        MdbImportService service = new(
            temp.Context.ConnectionFactory,
            temp.Context.Providers,
            temp.Context.Requesters,
            temp.Context.Audit,
            fakeReader,
            temp.Context.SyncHub,
            temp.Context.Users);

        MdbImportOptions rewriteOpts = new()
        {
            RewriteAllFromMdb = true,
            SyncNewRequesters = true,
            SyncNewWorkOrders = true,
            CreateBackupBeforeApply = true
        };

        MdbImportPreview preview = service.AnalyzeMdb(admin, @"C:\fake\OT_PROV_LIMPIO.mdb", rewriteOpts);
        Assert.True(preview.IsFullRewriteMode);
        Assert.Equal(2, preview.NewProvidersCount);
        Assert.Equal(0, preview.UnchangedProvidersCount);
        Assert.Equal(1, preview.NewRequestersCount); // Duplicado consolidado
        Assert.Equal(1, preview.NewWorkOrdersCount);
        Assert.Contains(preview.Warnings, w => w.Contains("REESCRITURA COMPLETA", StringComparison.Ordinal));

        MdbImportResult result = service.ApplyImport(admin, @"C:\fake\OT_PROV_LIMPIO.mdb", rewriteOpts);

        Assert.True(result.WasFullRewrite);
        Assert.Equal(1, result.ProvidersCleared);
        Assert.Equal(1, result.RequestersCleared);
        Assert.Equal(1, result.WorkOrdersCleared);
        Assert.Equal(2, result.ProvidersInserted);
        Assert.Equal(0, result.ProvidersUpdated);
        Assert.Equal(1, result.RequestersInserted);
        Assert.Equal(1, result.WorkOrdersInserted);
        Assert.Equal(1, result.WorkOrderDetailsInserted);
        Assert.Equal(2, result.DbProvidersAfter);
        Assert.Equal(1, result.DbWorkOrdersAfter);
        Assert.False(string.IsNullOrWhiteSpace(result.BackupPath));
        Assert.True(File.Exists(result.BackupPath!));

        // Registros obsoletos eliminados y registros nuevos limpios presentes
        Assert.Null(temp.Context.Providers.GetByName("PROV OBSOLETO A BORRAR"));
        Assert.Null(temp.Context.WorkOrders.GetByNumber(55555));
        Assert.NotNull(temp.Context.Providers.GetByName("PROV LIMPIO UNO"));
        Assert.NotNull(temp.Context.Providers.GetByName("PROV LIMPIO DOS"));

        WorkOrder cleanOt = temp.Context.WorkOrders.GetByNumber(90001)!;
        Assert.Equal(WorkOrderStatus.Terminada, cleanOt.Estado);
        Assert.Equal("SolNuevo", cleanOt.AliasSolicitante);
        Assert.Single(cleanOt.Detalles);
        Assert.Equal("Mecanizado limpio desde cero", cleanOt.Detalles[0].Descripcion);

        // Seguridad y Activos intactos
        Assert.NotNull(temp.Context.Users.GetByAlias("admin"));
        Assert.NotNull(temp.Context.AssetTree.GetById(plantId));
        Assert.Contains(temp.Context.Audit.GetRecent(20), a => a.Accion == "REESCRIBIR_BD_DESDE_MDB");
    }

    [Fact]
    public void DesktopOtProvMdb_IfPresent_AnalyzesRealAccessDatabaseCleanly()
    {
        string realMdb = @"c:\Users\barriagada\Desktop\OT_PROV.mdb";
        if (!File.Exists(realMdb) || !OperatingSystem.IsWindows())
        {
            return;
        }

        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        MdbImportPreview preview = temp.Context.MdbImportService.AnalyzeMdb(admin, realMdb);

        Assert.True(preview.MdbProvidersTotal >= 213);
        Assert.True(preview.MdbWorkOrdersTotal >= 7288);
        Assert.True(preview.NewWorkOrdersCount >= 4);
        Assert.Equal(preview.MdbProvidersTotal, preview.NewProvidersCount + preview.UpdatedProvidersCount + preview.UnchangedProvidersCount);
    }

    [Fact]
    public void MdbImportDialog_StaUiAudit_PassesLayoutAndButtons()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: false);
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            using MdbImportDialog dlg = new(temp.Context, admin, string.Empty);
            dlg.CreateControl();
            Assert.NotNull(dlg.BtnBrowse.Image);
            Assert.NotNull(dlg.BtnAnalyze.Image);
            Assert.NotNull(dlg.BtnCleanDb.Image);
            Assert.NotNull(dlg.BtnApply.Image);
            Assert.NotNull(dlg.BtnClose.Image);
            Assert.False(dlg.BtnApply.Enabled);
            Assert.True(dlg.BtnCleanDb.Enabled);
            Assert.Equal("Examinar...", dlg.BtnBrowse.Text);
            Assert.Equal("Analizar MDB", dlg.BtnAnalyze.Text);
            Assert.Equal("Limpiar sispro.db", dlg.BtnCleanDb.Text);
            Assert.Equal("Actualizar BD desde MDB", dlg.BtnApply.Text);

            // Al activar modo reescritura completa cambia etiqueta del botón principal
            dlg.ChkRewriteAll.Checked = true;
            Application.DoEvents();
            Assert.Equal("Reescribir BD desde MDB", dlg.BtnApply.Text);

            dlg.ChkRewriteAll.Checked = false;
            Application.DoEvents();
            Assert.Equal("Actualizar BD desde MDB", dlg.BtnApply.Text);
        });
    }
}
