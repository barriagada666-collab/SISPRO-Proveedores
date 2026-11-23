using ClosedXML.Excel;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class EndToEndWorkflowTests
{
    [Fact]
    public async Task CompleteBusinessFlow_OtApprovalsInvoicesExcelAndSecurity()
    {
        using TempTestDatabase temp = new();
        var ctx = temp.Context;

        // 1. Admin login
        AuthResult adminLogin = ctx.AuthService.Login("admin", "Admin2026!");
        Assert.True(adminLogin.Success);
        UserSession admin = adminLogin.Session!;

        // 2. Create Providers, Requesters, and Asset Tree nodes
        int provA = ctx.MaintainerService.SaveProvider(admin, new Provider
        {
            Nombre = "TALLER INDUSTRIAL VALPARAISO",
            Direccion = "Av. Brasil 1500",
            Telefono = "32-2201122",
            Correo = "contacto@tallervalpo.cl"
        });
        int provB = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "OTRO PROVEEDOR" });

        int solId = ctx.MaintainerService.SaveRequester(admin, new Requester
        {
            Alias = "M Silva",
            Nombre = "Marcelo",
            ApellidoPaterno = "Silva",
            ApellidoMaterno = "Morgado",
            Telefono = "2206319",
            Correo = "msilva@cambiaso.cl"
        });

        int plantaId = ctx.MaintainerService.AddAssetChild(admin, 1, "PLANTA TÉ");
        int maqId = ctx.MaintainerService.AddAssetChild(admin, plantaId, "MÁQUINA MAI 01");

        // 3. Create Supervisor & Jefatura users with distinct RBAC profiles
        ctx.UserManagementService.CreateUser(admin, new User
        {
            IdUsuario = "128498745",
            AliasUsuario = "sup_test",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Pérez",
            Nivel = UserRoleLevel.Supervisor,
            EsSolicitante = true,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
        }, "SupClave2026", "SupClave2026");

        ctx.UserManagementService.CreateUser(admin, new User
        {
            IdUsuario = "914380006",
            AliasUsuario = "jef_test",
            NombreUsuario = "Juan",
            ApePatUsuario = "Ramos",
            Nivel = UserRoleLevel.Jefatura,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Jefatura)
        }, "JefClave2026", "JefClave2026");

        // Verify EsSolicitante synced into Solicitantes table
        Assert.NotNull(ctx.Requesters.GetByAlias("sup_test"));

        UserSession supervisor = ctx.AuthService.Login("sup_test", "SupClave2026").Session!;
        UserSession jefatura = ctx.AuthService.Login("jef_test", "JefClave2026").Session!;

        // 4. Create Work Order
        WorkOrder newOt = new()
        {
            NumeroOt = 5001,
            IdProv = provA,
            IdSol = solId,
            FechaOt = new DateTime(2026, 11, 20),
            Detalles =
            [
                new WorkOrderDetail { Cantidad = 2, Descripcion = "Fabricar engranaje helicoidal Z-24" },
                new WorkOrderDetail { Cantidad = 1, Descripcion = "Rectificar eje principal" }
            ]
        };
        newOt.SetLevels([1, plantaId, maqId]);
        int otId = ctx.WorkOrderService.Create(supervisor, newOt);
        Assert.True(otId > 0);

        // 5. RBAC Approval Enforcement: Supervisor can set VBS, NOT VBJ!
        ctx.WorkOrderService.SetApproval(supervisor, otId, ApprovalType.Supervisor, true);
        Assert.Throws<UnauthorizedAccessException>(() =>
            ctx.WorkOrderService.SetApproval(supervisor, otId, ApprovalType.Jefatura, true));

        // Jefatura can set VBJ
        ctx.WorkOrderService.SetApproval(jefatura, otId, ApprovalType.Jefatura, true);

        WorkOrder loaded = ctx.WorkOrderService.GetByNumber(5001)!;
        Assert.True(loaded.TieneVbs);
        Assert.True(loaded.TieneVbj);
        Assert.Equal(2, loaded.Detalles.Count);
        Assert.Contains("PLANTA TÉ", loaded.RutaActivo);

        // 6. Invoice / Guía de Despacho Registration + Validation
        // Mismatched provider must fail
        Assert.Throws<InvalidOperationException>(() => ctx.InvoiceService.Register(supervisor, new Invoice
        {
            NumeroDocumento = 9001,
            IdProv = provB,
            Fecha = DateTime.Today,
            Items = [new InvoiceDetail { IdOt = otId, IdDet = loaded.Detalles[0].IdDet, ValorTrabajo = 150000 }]
        }));

        // Valid invoice with both detail items
        int invId = ctx.InvoiceService.Register(supervisor, new Invoice
        {
            NumeroDocumento = 9001,
            IdProv = provA,
            Fecha = new DateTime(2026, 11, 22),
            RecibidoPor = "Bodega Mantención",
            Detalle = "Recepción conforme trabajos OT 5001",
            Items =
            [
                new InvoiceDetail { IdOt = otId, IdDet = loaded.Detalles[0].IdDet, ValorTrabajo = 150000 },
                new InvoiceDetail { IdOt = otId, IdDet = loaded.Detalles[1].IdDet, ValorTrabajo = 100000 }
            ]
        });

        Invoice savedInv = ctx.InvoiceService.GetById(invId)!;
        Assert.Equal(250000m, savedInv.TotalNeto);
        Assert.Equal(47500m, savedInv.Iva);
        Assert.Equal(297500m, savedInv.TotalConIva);

        // Cannot delete OT while invoiced
        Assert.Throws<InvalidOperationException>(() => ctx.WorkOrderService.Delete(admin, otId));

        // 7. Export Excel OT Document + Search Results and verify actual .xlsx contents
        string otExcelPath = Path.Combine(Path.GetTempPath(), $"sispro_ot_{Guid.NewGuid():N}.xlsx");
        string gridExcelPath = Path.Combine(Path.GetTempPath(), $"sispro_grid_{Guid.NewGuid():N}.xlsx");
        try
        {
            ctx.ExcelExportService.ExportWorkOrderDocument(loaded, otExcelPath, SignatureOption.Ambas);
            Assert.True(File.Exists(otExcelPath));

            using (XLWorkbook wb = new(otExcelPath))
            {
                IXLWorksheet ws = wb.Worksheet(1);
                Assert.Contains("005001", ws.Cell(4, 4).GetString());
                Assert.Equal("TALLER INDUSTRIAL VALPARAISO", ws.Cell(8, 2).GetString());
                Assert.Equal("Fabricar engranaje helicoidal Z-24", ws.Cell(14, 3).GetString());
            }

            IReadOnlyList<WorkOrderSearchRow> found = ctx.WorkOrderService.Search(supervisor, new WorkOrderSearchFilter
            {
                FechaInicio = new DateTime(2026, 11, 1),
                FechaFin = new DateTime(2026, 11, 30),
                TextoBusqueda = "engranaje"
            });
            Assert.Single(found);

            List<int> progressValues = [];
            await ctx.ExcelExportService.ExportWorkOrdersGridAsync(found, gridExcelPath, new Progress<int>(p => progressValues.Add(p)));
            Assert.True(File.Exists(gridExcelPath));
            using (XLWorkbook wb2 = new(gridExcelPath))
            {
                // Fila 1..3 = Banner corporativo Cambiaso, Fila 5 = Encabezados, Fila 6 = Primer registro
                Assert.Equal(5001, (int)wb2.Worksheet(1).Cell(6, 1).GetDouble());
            }
        }
        finally
        {
            if (File.Exists(otExcelPath)) File.Delete(otExcelPath);
            if (File.Exists(gridExcelPath)) File.Delete(gridExcelPath);
        }

        // 8. Delete invoice restores detail status and allows OT deletion
        ctx.InvoiceService.Delete(supervisor, invId);
        Assert.False(ctx.InvoiceService.IsDetailAlreadyInvoiced(loaded.Detalles[0].IdDet));
        ctx.WorkOrderService.Delete(admin, otId);
        Assert.Null(ctx.WorkOrderService.GetByNumber(5001));
    }

    [Fact]
    public void SecurityGuards_LockoutSqlInjectionAndLastAdminProtection()
    {
        using TempTestDatabase temp = new();
        var ctx = temp.Context;

        // SQL Injection payload rejected cleanly
        AuthResult sqli = ctx.AuthService.Login("admin' OR '1'='1", "anything");
        Assert.False(sqli.Success);

        // Brute-force lockout after 5 failures
        for (int i = 0; i < 5; i++)
        {
            Assert.False(ctx.AuthService.Login("admin", "wrong").Success);
        }

        AuthResult locked = ctx.AuthService.Login("admin", "Admin2026!");
        Assert.False(locked.Success);
        Assert.Contains("bloqueada", locked.Message);

        // Separate fresh context for admin self-delete & last-admin guards
        using TempTestDatabase temp2 = new();
        UserSession admin = temp2.Context.AuthService.Login("admin", "Admin2026!").Session!;
        Assert.Throws<InvalidOperationException>(() => temp2.Context.UserManagementService.DeleteUser(admin, admin.Usuario.IdUsuario));

        // Cannot demote last active admin
        User demoted = temp2.Context.Users.GetById(admin.Usuario.IdUsuario)!;
        demoted.Nivel = UserRoleLevel.Usuario;
        demoted.Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario, demoted.IdUsuario);
        Assert.Throws<InvalidOperationException>(() => temp2.Context.UserManagementService.UpdateUser(admin, demoted));
    }
}
