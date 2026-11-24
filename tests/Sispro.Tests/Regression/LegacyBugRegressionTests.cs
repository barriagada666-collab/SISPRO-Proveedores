using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Xunit;

namespace Sispro.Tests.Regression;

/// <summary>
/// Pruebas de regresión dedicadas específicamente a los defectos críticos detectados durante la auditoría VB6.
/// </summary>
public sealed class LegacyBugRegressionTests
{
    [Fact]
    public void Regression_VbsAndVbjPermissions_AreNotSwappedAndNotShiftedOnSave()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        // Create user with ONLY VB Supervisor = true, VB Jefatura = false, IngresoOt = false
        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            IdUsuario = "128498745",
            AliasUsuario = "only_vbs",
            NombreUsuario = "Solo",
            ApePatUsuario = "Supervisor",
            Nivel = UserRoleLevel.Supervisor,
            Permisos = new UserPermissions
            {
                MenuOrdenesTrabajo = true,
                IngresoOt = false,
                VistoBuenoSupervisor = true,
                VistoBuenoJefatura = false,
                BusquedaOt = true,
                ImpresionOt = false
            }
        }, "ClaveSup1", "ClaveSup1");

        User reloaded = temp.Context.Users.GetByAlias("only_vbs")!;
        Assert.True(reloaded.Permisos.MenuOrdenesTrabajo);
        Assert.False(reloaded.Permisos.IngresoOt);
        Assert.True(reloaded.Permisos.VistoBuenoSupervisor);
        Assert.False(reloaded.Permisos.VistoBuenoJefatura);
        Assert.True(reloaded.Permisos.BusquedaOt);
        Assert.False(reloaded.Permisos.ImpresionOt);
    }

    [Fact]
    public void Regression_DateRangeSearch_ComparesDatesChronologicallyNotAsStrings()
    {
        // In VB6: Format("31-01-2026") > Format("01-02-2026") wrongly threw an error AND Access swapped dd/MM with MM/dd!
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        int prov = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROV FECHA" });
        int planta = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA A");

        WorkOrder ot = new()
        {
            NumeroOt = 7001,
            IdProv = prov,
            FechaOt = new DateTime(2026, 2, 1), // 01-02-2026 (Feb 1, not Jan 2!)
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Prueba fecha ISO" }]
        };
        ot.SetLevels([1, planta]);
        temp.Context.WorkOrderService.Create(admin, ot);

        IReadOnlyList<WorkOrderSearchRow> result = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
        {
            FechaInicio = new DateTime(2026, 1, 31),
            FechaFin = new DateTime(2026, 2, 2)
        });

        Assert.Single(result);
        Assert.Equal(7001, result[0].NumeroOt);

        // Inverted range must still throw cleanly
        Assert.Throws<ArgumentException>(() => temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
        {
            FechaInicio = new DateTime(2026, 2, 10),
            FechaFin = new DateTime(2026, 2, 1)
        }));
    }

    [Fact]
    public void Regression_WorkOrderUpdateAndDelete_DoNotDependOnObsoleteInvoices()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        int prov = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROV REG" });
        int planta = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA B");

        WorkOrder ot = new()
        {
            NumeroOt = 8001,
            IdProv = prov,
            FechaOt = DateTime.Today,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Trabajo único" }]
        };
        ot.SetLevels([1, planta]);
        int otId = temp.Context.WorkOrderService.Create(admin, ot);
        WorkOrder loaded = temp.Context.WorkOrders.GetById(otId)!;
        loaded.Detalles.Add(new WorkOrderDetail { Cantidad = 2, Descripcion = "Segundo ítem agregado" });
        temp.Context.WorkOrderService.Update(admin, loaded);

        Assert.Equal(2, temp.Context.WorkOrders.GetById(otId)!.Detalles.Count);
        temp.Context.WorkOrderService.Delete(admin, otId);
        Assert.Null(temp.Context.WorkOrders.GetById(otId));
    }
}
