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

        // Historical OT 2514 had id_sol = 0 (invisible in VB6 INNER JOIN) and now displays real Cambiaso asset path
        WorkOrder? ot2514 = temp.Context.WorkOrders.GetByNumber(2514);
        Assert.NotNull(ot2514);
        Assert.Null(ot2514.IdSol);
        Assert.Equal("Sin solicitante", ot2514.AliasSolicitante);
        Assert.Contains("PLANTA PE", ot2514.RutaActivo);
        Assert.Contains("Molino Rapid 400", ot2514.RutaActivo);

        // Verify External OT analytics
        DashboardAnalytics analytics = temp.Context.WorkOrderService.GetDashboardAnalytics();
        Assert.NotEmpty(analytics.WorkOrdersByYear);
        Assert.NotEmpty(analytics.WorkOrdersByPlant);
        Assert.NotEmpty(analytics.TopProviders);

        // Legacy user L Reyes authenticates and transparently upgrades password to PBKDF2
        AuthResult auth = temp.Context.AuthService.Login("L Reyes", "wanderes");
        Assert.True(auth.Success);
        Assert.True(auth.PasswordUpgraded);
        Assert.NotNull(auth.Session);

        // Verify approver directory contains active VBS/VBJ approvers
        IReadOnlyList<ApproverDirectoryEntry> approvers = temp.Context.WorkOrderService.GetApproverDirectory();
        Assert.NotEmpty(approvers);

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
    public void SchemaMigration_LegacyDatabaseWithoutSignatureColumns_UpgradesCleanlyPreservingRequesters()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        IReadOnlyList<Requester> reqs = temp.Context.Requesters.GetAll(includeInactive: true);
        Assert.NotEmpty(reqs);
        Assert.All(reqs, r => Assert.False(string.IsNullOrWhiteSpace(r.Alias)));
    }
}
