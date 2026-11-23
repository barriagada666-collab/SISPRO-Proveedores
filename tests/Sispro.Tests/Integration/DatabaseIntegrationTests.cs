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

        Assert.Equal(1514, metrics.TotalOrdenesTrabajo);
        Assert.Equal(73, metrics.TotalProveedoresActivos);
        Assert.Equal(7, metrics.TotalSolicitantesActivos);
        Assert.Equal(3, metrics.TotalUsuariosActivos);
        Assert.Equal(272, metrics.TotalNodosActivos);

        // Historical OT 2514 had id_sol = 0 (invisible in VB6 INNER JOIN); must now load cleanly!
        WorkOrder? ot2514 = temp.Context.WorkOrders.GetByNumber(2514);
        Assert.NotNull(ot2514);
        Assert.Null(ot2514.IdSol);
        Assert.Equal("Sin solicitante", ot2514.AliasSolicitante);

        // Legacy user L Reyes authenticates and transparently upgrades password to PBKDF2
        AuthResult auth = temp.Context.AuthService.Login("L Reyes", "wanderes");
        Assert.True(auth.Success);
        Assert.True(auth.PasswordUpgraded);
        Assert.NotNull(auth.Session);

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
            parentId = temp.Context.MaintainerService.AddAssetChild(session, parentId, $"Nivel {level} Test");
        }

        IReadOnlyList<AssetNode> path = temp.Context.MaintainerService.GetAssetPath(parentId);
        Assert.Equal(10, path.Count);
        Assert.Equal(1, path[0].Nivel);
        Assert.Equal(10, path[^1].Nivel);

        Assert.Throws<InvalidOperationException>(() =>
            temp.Context.MaintainerService.AddAssetChild(session, parentId, "Nivel 11 Inválido"));
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

            Sispro.Services.SisproApplicationContext restored = new(new AppSettings { DatabasePath = backupPath });
            Assert.Equal(1514, restored.Audit.GetDashboardMetrics().TotalOrdenesTrabajo);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
    }
}
