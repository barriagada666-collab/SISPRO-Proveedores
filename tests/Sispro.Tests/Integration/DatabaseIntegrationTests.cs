using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Xunit;

namespace Sispro.Tests.Integration;

public sealed class DatabaseIntegrationTests
{
    [Fact]
    public void MigratedRealDatabase_PreservesAllHistoricalDataAndUpgradesLegacyLogin()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        DashboardMetrics metrics = temp.Context.Audit.GetDashboardMetrics();

        Assert.Equal(7284, metrics.TotalOrdenesTrabajo);
        Assert.Equal(214, metrics.TotalProveedoresActivos);
        Assert.Equal(9, metrics.TotalSolicitantesActivos);
        Assert.Equal(4, metrics.TotalUsuariosActivos);
        Assert.Equal(1823, metrics.TotalNodosActivos);
        Assert.Equal(43989, metrics.TotalMantOts);
        Assert.Equal(780, metrics.TotalPlanesMp);
        Assert.Equal(208121, metrics.TotalTareasMp);
        Assert.Equal(2462, metrics.TotalInsumos);
        Assert.Equal(134, metrics.TotalPersonalTecnico);

        // Historical OT 2514 had id_sol = 0 (invisible in VB6 INNER JOIN) and now displays real Cambiaso asset path from BD MANTENCION!
        WorkOrder? ot2514 = temp.Context.WorkOrders.GetByNumber(2514);
        Assert.NotNull(ot2514);
        Assert.Null(ot2514.IdSol);
        Assert.Equal("Sin solicitante", ot2514.AliasSolicitante);
        Assert.Contains("PLANTA PE", ot2514.RutaActivo);
        Assert.Contains("Molino Rapid 400", ot2514.RutaActivo);

        // Verify BD MANTENCION analytics and exploration queries
        DashboardAnalytics analytics = temp.Context.MaintenanceService.GetDashboardAnalytics();
        Assert.NotEmpty(analytics.WorkOrdersByYear);
        Assert.NotEmpty(analytics.WorkOrdersByPlant);
        Assert.NotEmpty(analytics.TopProviders);
        Assert.NotEmpty(analytics.MaintenanceByTrade);

        // Legacy user L Reyes authenticates and transparently upgrades password to PBKDF2
        AuthResult auth = temp.Context.AuthService.Login("L Reyes", "wanderes");
        Assert.True(auth.Success);
        Assert.True(auth.PasswordUpgraded);
        Assert.NotNull(auth.Session);

        // Verify Maintenance Hub queries across all imported tables
        Assert.NotEmpty(temp.Context.MaintenanceService.SearchInternalWorkOrders(auth.Session, new MantWorkOrderFilter { MaxRows = 25 }));
        Assert.NotEmpty(temp.Context.MaintenanceService.SearchPreventivePlans(auth.Session));
        Assert.NotEmpty(temp.Context.MaintenanceService.SearchSupplies(auth.Session, maxRows: 25));
        Assert.NotEmpty(temp.Context.MaintenanceService.SearchPersonnel(auth.Session));
        Assert.Equal(43, temp.Context.MaintenanceService.GetHrPersonnel(auth.Session).Count);
        Assert.Equal(56, temp.Context.MaintenanceService.GetLegacyExternalOrders(auth.Session).Count);
        Assert.Equal(41, temp.Context.MaintenanceService.GetFailureMotives(auth.Session).Count);
        Assert.Equal(33, temp.Context.MaintenanceService.GetTradesCatalog(auth.Session).Count);

        // Verify BORIS migrated from OT_PROV.mdb uses PBKDF2 + forced password change on first login
        User? boris = temp.Context.Users.GetByAlias("BORIS");
        Assert.NotNull(boris);
        Assert.StartsWith("PBKDF2$100000$", boris.PassHash);
        Assert.True(boris.DebeCambiarClave);

        User? upgradedUser = temp.Context.Users.GetByAlias("L Reyes");
        Assert.NotNull(upgradedUser);
        Assert.StartsWith("PBKDF2$100000$", upgradedUser.PassHash);

        // Second login uses PBKDF2 without re-upgrading
        AuthResult auth2 = temp.Context.AuthService.Login("L Reyes", "wanderes");
        Assert.True(auth2.Success);
        Assert.False(auth2.PasswordUpgraded);
    }

    [Fact]
    public void AssetTree_SupportsFull10LevelsAndBlocksLevel11()
    {
        using TempTestDatabase temp = new();
        AuthResult login = temp.Context.AuthService.Login("admin", "Admin2026!");
        Assert.True(login.Success);
        UserSession session = login.Session!;

        int parentId = 1;
        for (int level = 2; level <= 10; level++)
        {
            parentId = temp.Context.AssetTreeService.AddChild(session, parentId, $"Nivel {level} Test");
        }

        IReadOnlyList<AssetNode> path = temp.Context.MaintainerService.GetAssetPath(parentId);
        Assert.Equal(10, path.Count);
        Assert.Equal(1, path[0].Nivel);
        Assert.Equal(10, path[^1].Nivel);

        Assert.Throws<InvalidOperationException>(() =>
            temp.Context.AssetTreeService.AddChild(session, parentId, "Nivel 11 Inválido"));
    }

    [Fact]
    public void DatabaseHotBackup_CreatesValidRestorableCopy()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        string backupPath = Path.Combine(Path.GetTempPath(), $"sispro_backup_{Guid.NewGuid():N}.db");
        try
        {
            temp.Context.ConnectionFactory.BackupTo(backupPath);
            Assert.True(File.Exists(backupPath));

            using Sispro.Services.SisproApplicationContext restored = new(new AppSettings { DatabasePath = backupPath });
            Assert.Equal(7284, restored.Audit.GetDashboardMetrics().TotalOrdenesTrabajo);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }

    [Fact]
    public void MaintenanceService_PagedQueries_NavigateAllWorkOrdersAndSupplies()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        PagedResult<MantWorkOrderRow> otPage0 = temp.Context.MaintenanceService.SearchInternalWorkOrdersPaged(
            admin,
            new MantWorkOrderFilter(),
            pageIndex: 0,
            pageSize: 100);
        Assert.Equal(43989, otPage0.TotalCount);
        Assert.Equal(100, otPage0.Items.Count);
        Assert.Equal(0, otPage0.PageIndex);

        PagedResult<MantWorkOrderRow> otPage1 = temp.Context.MaintenanceService.SearchInternalWorkOrdersPaged(
            admin,
            new MantWorkOrderFilter(),
            pageIndex: 1,
            pageSize: 100);
        Assert.Equal(100, otPage1.Items.Count);
        Assert.Empty(otPage0.Items.Select(x => x.Id).Intersect(otPage1.Items.Select(x => x.Id)));

        // Página fuera de rango se ajusta a la última página válida
        PagedResult<MantWorkOrderRow> otLast = temp.Context.MaintenanceService.SearchInternalWorkOrdersPaged(
            admin,
            new MantWorkOrderFilter(),
            pageIndex: 99999,
            pageSize: 100);
        Assert.Equal((43989 - 1) / 100, otLast.PageIndex);
        Assert.Equal(89, otLast.Items.Count);

        PagedResult<MantSupplyRow> supPage0 = temp.Context.MaintenanceService.SearchSuppliesPaged(admin, null, 0, 200);
        PagedResult<MantSupplyRow> supPage1 = temp.Context.MaintenanceService.SearchSuppliesPaged(admin, null, 1, 200);
        Assert.Equal(2462, supPage0.TotalCount);
        Assert.Equal(200, supPage0.Items.Count);
        Assert.Empty(supPage0.Items.Select(x => x.IdItem).Intersect(supPage1.Items.Select(x => x.IdItem)));

        // RBAC: usuario sin permiso de Mantención/OT no puede consultar
        UserSession limited = new()
        {
            Usuario = new User
            {
                IdUsuario = "NO-PERM",
                AliasUsuario = "noperm",
                Nivel = UserRoleLevel.Usuario,
                Activo = true,
                Permisos = new UserPermissions()
            }
        };
        Assert.Throws<UnauthorizedAccessException>(() =>
            temp.Context.MaintenanceService.SearchInternalWorkOrdersPaged(limited, new MantWorkOrderFilter(), 0, 50));
    }
}
