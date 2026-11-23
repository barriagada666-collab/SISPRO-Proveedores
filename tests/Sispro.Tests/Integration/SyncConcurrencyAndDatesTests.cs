using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Core.Validation;
using Sispro.Services;
using Xunit;

namespace Sispro.Tests.Integration;

public sealed class SyncConcurrencyAndDatesTests
{
    [Fact]
    public void ImmediateInAppSync_AndCrossSessionPolling_DetectExternalWrites()
    {
        using TempTestDatabase temp = new();
        UserSession adminA = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        // 1. In-app immediate sync fires after commit
        List<DataChangedEventArgs> inAppEvents = [];
        temp.Context.SyncHub.DataChanged += (_, e) => inAppEvents.Add(e);

        int grpId = temp.Context.GroupManagementService.CreateGroup(adminA, new UserGroup { Nombre = "HIDRÁULICOS" });
        Assert.Contains(inAppEvents, e => (e.Kind & DataChangeKind.Groups) != 0 && !e.FromExternalSession);

        // 2. Second concurrent session pointing to the SAME SQLite file
        using SisproApplicationContext sessionB = new(new AppSettings { DatabasePath = temp.Context.Settings.DatabasePath });
        UserSession adminB = sessionB.AuthService.Login("admin", "Admin2026!").Session!;

        List<DataChangedEventArgs> externalEventsInA = [];
        temp.Context.SyncHub.DataChanged += (_, e) =>
        {
            if (e.FromExternalSession)
            {
                externalEventsInA.Add(e);
            }
        };

        // Session B commits a new user while Session A is open
        sessionB.UserManagementService.CreateUser(adminB, new User
        {
            AliasUsuario = "concurrente1",
            NombreUsuario = "Ana",
            ApePatUsuario = "Pérez",
            Nivel = UserRoleLevel.Supervisor,
            IdGrupo = grpId,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
        }, "ClaveConc2026", "ClaveConc2026");

        // Session A polls PRAGMA data_version and MUST detect Session B's commit!
        bool detected = temp.Context.SyncHub.PollExternalChanges();
        Assert.True(detected);
        Assert.Single(externalEventsInA);
        Assert.Equal(DataChangeKind.All, externalEventsInA[0].Kind);
        Assert.NotNull(temp.Context.Users.GetByAlias("concurrente1"));
    }

    [Fact]
    public void OptimisticConcurrency_RejectsStaleUpdateOnUserAndGroup()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            AliasUsuario = "concur_user",
            NombreUsuario = "Original",
            ApePatUsuario = "Uno",
            Nivel = UserRoleLevel.Usuario,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
        }, "Clave2026!", "Clave2026!");

        User copyA = temp.Context.Users.GetByAlias("concur_user")!;
        User copyB = temp.Context.Users.GetByAlias("concur_user")!;
        Assert.Equal(1, copyA.RowVersion);

        // Session B saves first -> increments row_version to 2
        copyB.NombreUsuario = "EditadoPorB";
        temp.Context.UserManagementService.UpdateUser(admin, copyB, expectedRowVersion: copyB.RowVersion);

        // Session A tries to save with stale row_version 1 -> MUST throw ConcurrencyConflictException
        copyA.NombreUsuario = "EditadoPorA";
        Assert.Throws<ConcurrencyConflictException>(() =>
            temp.Context.UserManagementService.UpdateUser(admin, copyA, expectedRowVersion: copyA.RowVersion));

        // Same optimistic concurrency guard for Groups
        int gid = temp.Context.GroupManagementService.CreateGroup(admin, new UserGroup { Nombre = "LUBRICADORES" });
        UserGroup gA = temp.Context.Groups.GetById(gid)!;
        UserGroup gB = temp.Context.Groups.GetById(gid)!;
        gB.Descripcion = "Actualizado por B";
        temp.Context.GroupManagementService.UpdateGroup(admin, gB, expectedRowVersion: gB.RowVersion);

        gA.Descripcion = "Actualizado por A";
        Assert.Throws<ConcurrencyConflictException>(() =>
            temp.Context.GroupManagementService.UpdateGroup(admin, gA, expectedRowVersion: gA.RowVersion));
    }

    [Fact]
    public void DateRangeValidation_Presets_AndDescendingChronologicalOrder_WorkAcrossModules()
    {
        // Validation rejects inverted range
        Assert.NotNull(DomainValidator.ValidateDateRange(new DateTime(2026, 5, 10), new DateTime(2026, 5, 1)));
        Assert.Null(DomainValidator.ValidateDateRange(new DateTime(2026, 5, 1), new DateTime(2026, 5, 10)));

        (DateTime? d1, DateTime? d2) = DomainValidator.ResolveDatePreset(DateRangePreset.Hoy, new DateTime(2026, 11, 23));
        Assert.Equal(new DateTime(2026, 11, 23), d1);
        Assert.Equal(new DateTime(2026, 11, 23), d2);

        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        int provId = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROV CRONOLOGICO" });
        int plantId = temp.Context.MaintainerService.AddAssetChild(admin, 1, "PLANTA CRONO");

        // Create 3 OTs on different dates (out of order)
        temp.Context.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 5001, IdProv = provId, FechaOt = new DateTime(2025, 1, 15),
            IdN1 = 1, IdN2 = plantId, IdNivel = 2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "OT Antigua" }]
        });
        temp.Context.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 5003, IdProv = provId, FechaOt = new DateTime(2026, 10, 20),
            IdN1 = 1, IdN2 = plantId, IdNivel = 2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "OT Más Reciente" }]
        });
        temp.Context.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 5002, IdProv = provId, FechaOt = new DateTime(2026, 3, 5),
            IdN1 = 1, IdN2 = plantId, IdNivel = 2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "OT Intermedia" }]
        });

        // Must return newest first (2026-10-20 -> 2026-03-05 -> 2025-01-15)
        IReadOnlyList<WorkOrderSearchRow> allOts = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter());
        Assert.Equal(3, allOts.Count);
        Assert.Equal(5003, allOts[0].NumeroOt);
        Assert.Equal(5002, allOts[1].NumeroOt);
        Assert.Equal(5001, allOts[2].NumeroOt);

        // Date range filter must restrict strictly to 2026
        IReadOnlyList<WorkOrderSearchRow> only2026 = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
        {
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 12, 31)
        });
        Assert.Equal(2, only2026.Count);
        Assert.Equal(5003, only2026[0].NumeroOt);

        // AuditLog must also be descending and respect inclusive end-of-day date filter
        IReadOnlyList<AuditEntry> audits = temp.Context.Audit.Search(new AuditSearchFilter
        {
            FechaInicio = DateTime.Today,
            FechaFin = DateTime.Today
        });
        Assert.NotEmpty(audits);
        for (int i = 1; i < audits.Count; i++)
        {
            Assert.True(audits[i - 1].IdLog > audits[i].IdLog);
        }
    }
}
