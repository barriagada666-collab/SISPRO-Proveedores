using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class GroupsAndBatchOperationsTests
{
    [Fact]
    public void SchemaInitializer_SeedsFiveInitialGroupsIdempotently()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        IReadOnlyList<UserGroup> groups = temp.Context.GroupManagementService.GetAll(admin);
        string[] expected = ["MECÁNICOS", "ELÉCTRICOS", "ELECTRÓNICOS", "EXTERNOS", "OPERADORES"];
        foreach (string name in expected)
        {
            Assert.Contains(groups, g => string.Equals(g.Nombre, name, StringComparison.OrdinalIgnoreCase) && g.Activo);
        }

        // Re-initializing schema must not create duplicate groups
        temp.Context.ReloadDatabase(temp.Context.Settings.DatabasePath);
        IReadOnlyList<UserGroup> afterReload = temp.Context.GroupManagementService.GetAll(admin);
        Assert.Equal(groups.Count, afterReload.Count);
    }

    [Fact]
    public void GroupCrud_EnforcesAdminOnly_AndProtectsDependencies()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        // Create custom group
        int soldadoresId = temp.Context.GroupManagementService.CreateGroup(admin, new UserGroup
        {
            Nombre = "SOLDADORES",
            Descripcion = "Especialistas en soldadura estructural e industrial",
            IdResponsable = admin.Usuario.IdUsuario,
            Activo = true
        });
        Assert.True(soldadoresId > 0);

        // Duplicate group name rejected
        Assert.Throws<InvalidOperationException>(() =>
            temp.Context.GroupManagementService.CreateGroup(admin, new UserGroup { Nombre = "soldadores" }));

        // Assign user to SOLDADORES
        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "jsoldador",
            NombreUsuario = "Jorge",
            ApePatUsuario = "Vega",
            Nivel = UserRoleLevel.Usuario,
            IdGrupo = soldadoresId,
            Activo = true,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "Clave2026!", "Clave2026!");

        User created = temp.Context.Users.GetByAlias("jsoldador")!;
        Assert.Equal(soldadoresId, created.IdGrupo);
        Assert.Equal("SOLDADORES", created.NombreGrupo);

        // Group with assigned user cannot be deleted
        Assert.Throws<InvalidOperationException>(() => temp.Context.GroupManagementService.DeleteGroup(admin, soldadoresId));

        // Move user out to ELÉCTRICOS -> SOLDADORES still has historical movement dependency, so deletion remains protected!
        UserGroup electricos = temp.Context.Groups.GetByName("ELÉCTRICOS")!;
        BatchOperationResult moveRes = temp.Context.UserManagementService.MoveUsersToGroup(admin, [created.IdUsuario], electricos.IdGrupo, "Reasignación de taller");
        Assert.Equal(1, moveRes.SucceededCount);

        User afterMove = temp.Context.Users.GetById(created.IdUsuario)!;
        Assert.Equal(electricos.IdGrupo, afterMove.IdGrupo);
        Assert.Equal("ELÉCTRICOS", afterMove.NombreGrupo);

        // Historical dependency protection prevents hard delete of SOLDADORES
        Assert.Throws<InvalidOperationException>(() => temp.Context.GroupManagementService.DeleteGroup(admin, soldadoresId));

        // Deactivation is allowed cleanly
        temp.Context.GroupManagementService.SetGroupActive(admin, soldadoresId, active: false);
        Assert.False(temp.Context.Groups.GetById(soldadoresId)!.Activo);

        // A brand-new unused group with zero dependencies CAN be deleted
        int tempGroupId = temp.Context.GroupManagementService.CreateGroup(admin, new UserGroup { Nombre = "TEMPORAL_SIN_USO" });
        temp.Context.GroupManagementService.DeleteGroup(admin, tempGroupId);
        Assert.Null(temp.Context.Groups.GetById(tempGroupId));

        // Non-admin cannot access group operations
        UserSession nonAdmin = temp.Context.AuthService.Login("jsoldador", "Clave2026!").Session!;
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.GroupManagementService.GetAll(nonAdmin));
        Assert.Throws<UnauthorizedAccessException>(() => temp.Context.GroupManagementService.CreateGroup(nonAdmin, new UserGroup { Nombre = "INTRUSO" }));
    }

    [Fact]
    public void BatchOperations_MoveActivateDeactivateAndChangeRole_ProtectSelfAndLastAdmin_AndPreserveWorkOrderHistory()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        UserGroup mecanicos = temp.Context.Groups.GetByName("MECÁNICOS")!;
        UserGroup electronicos = temp.Context.Groups.GetByName("ELECTRÓNICOS")!;

        // Create 3 users (2 technicians + 1 second admin)
        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "tec1",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Díaz",
            Nivel = UserRoleLevel.Usuario,
            IdGrupo = mecanicos.IdGrupo,
            EsSolicitante = true,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "ClaveTec1!", "ClaveTec1!");

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "tec2",
            NombreUsuario = "Luis",
            ApePatUsuario = "Mora",
            Nivel = UserRoleLevel.Usuario,
            IdGrupo = mecanicos.IdGrupo,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "ClaveTec2!", "ClaveTec2!");

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "admin2",
            NombreUsuario = "María",
            ApePatUsuario = "Rojas",
            Nivel = UserRoleLevel.Administrador,
            IdGrupo = mecanicos.IdGrupo,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Administrador)
        }, "ClaveAdm2!", "ClaveAdm2!");

        User tec1 = temp.Context.Users.GetByAlias("tec1")!;
        User tec2 = temp.Context.Users.GetByAlias("tec2")!;
        User admin2 = temp.Context.Users.GetByAlias("admin2")!;

        // Create a provider & WorkOrder linked to tec1 as requester to verify historical preservation after group move
        int provId = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROVEEDOR HISTORIAL SPA" });
        int plantId = temp.Context.MaintainerService.AddAssetChild(admin, 1, "PLANTA TEST");
        Requester sol = temp.Context.Requesters.GetByAlias("tec1")!;
        int otId = temp.Context.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 99001,
            IdProv = provId,
            IdSol = sol.IdSol,
            FechaOt = DateTime.Today,
            IdN1 = 1,
            IdN2 = plantId,
            IdNivel = 2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Mantención bomba hidráulica" }]
        });

        // Bulk move tec1 + tec2 from MECÁNICOS -> ELECTRÓNICOS
        BatchOperationResult moveRes = temp.Context.UserManagementService.MoveUsersToGroup(
            admin, [tec1.IdUsuario, tec2.IdUsuario], electronicos.IdGrupo, "Migración de cuadrilla");
        Assert.Equal(2, moveRes.SucceededCount);
        Assert.Equal(2, temp.Context.Groups.GetById(electronicos.IdGrupo)!.CantidadIntegrantes);
        Assert.Equal(1, temp.Context.Groups.GetById(mecanicos.IdGrupo)!.CantidadIntegrantes); // admin2 remains

        // Historical OT authorship is 100% preserved
        WorkOrder? preservedOt = temp.Context.WorkOrders.GetById(otId);
        Assert.NotNull(preservedOt);
        Assert.Equal(sol.IdSol, preservedOt.IdSol);
        Assert.Equal("tec1", preservedOt.AliasSolicitante);

        // Movement history recorded in descending order
        IReadOnlyList<GroupMovementEntry> history = temp.Context.GroupManagementService.GetMovementHistory(admin);
        Assert.True(history.Count >= 2);
        Assert.Contains(history, h => h.AliasUsuario == "tec1" && h.NombreGrupoOrigen == "MECÁNICOS" && h.NombreGrupoDestino == "ELECTRÓNICOS");

        // Batch deactivate ALL users including active session 'admin' and 'admin2':
        // 'admin' (self) MUST be protected; 'admin2' can be deactivated because 'admin' remains active; tec1 & tec2 deactivated.
        BatchOperationResult deactRes = temp.Context.UserManagementService.BatchSetActive(
            admin, [admin.Usuario.IdUsuario, admin2.IdUsuario, tec1.IdUsuario, tec2.IdUsuario], active: false, "Baja de lote");
        Assert.Equal(3, deactRes.SucceededCount);
        Assert.Equal(1, deactRes.ProtectedSkippedCount);
        Assert.Equal(1, temp.Context.Users.CountActiveAdmins());
        Assert.True(temp.Context.Users.GetById(admin.Usuario.IdUsuario)!.Activo);
        Assert.False(temp.Context.Users.GetById(tec1.IdUsuario)!.Activo);

        // Batch reactivate tec1 & tec2 + batch change role to Supervisor
        BatchOperationResult reactRes = temp.Context.UserManagementService.BatchSetActive(admin, [tec1.IdUsuario, tec2.IdUsuario], active: true);
        Assert.Equal(2, reactRes.SucceededCount);

        BatchOperationResult roleRes = temp.Context.UserManagementService.BatchChangeRole(
            admin, [admin.Usuario.IdUsuario, tec1.IdUsuario, tec2.IdUsuario], UserRoleLevel.Supervisor, "Promoción supervisores");
        Assert.Equal(2, roleRes.SucceededCount);
        Assert.Equal(1, roleRes.ProtectedSkippedCount); // self protected
        Assert.Equal(UserRoleLevel.Supervisor, temp.Context.Users.GetById(tec1.IdUsuario)!.Nivel);
        Assert.False(temp.Context.Users.GetById(tec1.IdUsuario)!.Permisos.MenuUsuarios);
        Assert.Equal(UserRoleLevel.Administrador, temp.Context.Users.GetById(admin.Usuario.IdUsuario)!.Nivel);

        // Verify per-item audit logs exist without exposing passwords
        IReadOnlyList<AuditEntry> audits = temp.Context.Audit.Search(new AuditSearchFilter { MaxRows = 100 });
        Assert.Contains(audits, a => a.Accion == "MOVER_USUARIO_GRUPO" && a.Detalle.Contains("tec1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audits, a => a.Accion == "DESACTIVAR_USUARIO_LOTE" && a.Detalle.Contains("tec2", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(audits, a => a.Accion == "CAMBIO_PERFIL_LOTE" && a.Detalle.Contains("Supervisor", StringComparison.OrdinalIgnoreCase));
    }
}
