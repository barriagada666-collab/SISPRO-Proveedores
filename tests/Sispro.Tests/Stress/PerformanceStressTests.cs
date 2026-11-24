using System.Diagnostics;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Xunit;

namespace Sispro.Tests.Stress;

public sealed class PerformanceStressTests
{
    [Fact]
    public async Task RealMigratedDatabase_ConcurrentReadsAndExcelExportStress()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        // 1. Query latency benchmark across all 7,284 OTs / 7,803 details + External Analytics
        Stopwatch sw = Stopwatch.StartNew();
        IReadOnlyList<WorkOrderSearchRow> allRows = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter());
        sw.Stop();
        Assert.Equal(7803, allRows.Count);
        Assert.True(sw.ElapsedMilliseconds < 2500, $"Consulta completa tomó {sw.ElapsedMilliseconds}ms (umbral 2500ms).");

        Stopwatch analyticsSw = Stopwatch.StartNew();
        DashboardAnalytics analytics = temp.Context.WorkOrderService.GetDashboardAnalytics();
        analyticsSw.Stop();
        Assert.Equal(7284, analytics.Metrics.TotalOrdenesTrabajo);
        Assert.True(analyticsSw.ElapsedMilliseconds < 1500, $"Analítica Dashboard tomó {analyticsSw.ElapsedMilliseconds}ms.");

        // 2. Concurrent read stress (16 parallel threads)
        Task[] tasks = Enumerable.Range(0, 16).Select(i => Task.Run(() =>
        {
            IReadOnlyList<WorkOrderSearchRow> res = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
            {
                TextoBusqueda = i % 2 == 0 ? "motor" : "engranaje"
            });
            Assert.NotEmpty(res);
        })).ToArray();
        await Task.WhenAll(tasks);

        // 3. Bulk Excel export of 1,500 real detail rows validated for speed and output integrity
        string outPath = Path.Combine(Path.GetTempPath(), $"sispro_stress_{Guid.NewGuid():N}.xlsx");
        try
        {
            Stopwatch expSw = Stopwatch.StartNew();
            await temp.Context.ExcelExportService.ExportWorkOrdersGridAsync(allRows.Take(1500).ToList(), outPath);
            expSw.Stop();
            Assert.True(File.Exists(outPath));
            Assert.True(new FileInfo(outPath).Length > 20_000);
            Assert.True(expSw.ElapsedMilliseconds < 15_000, $"Exportación masiva tomó {expSw.ElapsedMilliseconds}ms.");
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }

    [Fact]
    public void HighVolumeInsertAndIndexedSearch_CompletesRapidly()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        int prov = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "STRESS PROV" });
        int planta = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA STRESS");

        for (int i = 1; i <= 250; i++)
        {
            WorkOrder ot = new()
            {
                NumeroOt = 10000 + i,
                IdProv = prov,
                FechaOt = new DateTime(2026, 1, 1).AddDays(i % 300),
                Detalles =
                [
                    new WorkOrderDetail { Cantidad = 1, Descripcion = $"Trabajo stress #{i} rodamiento bomba" },
                    new WorkOrderDetail { Cantidad = 2, Descripcion = $"Ajuste eje secundario #{i}" }
                ]
            };
            ot.SetLevels([1, planta]);
            temp.Context.WorkOrderService.Create(admin, ot);
        }

        Stopwatch sw = Stopwatch.StartNew();
        IReadOnlyList<WorkOrderSearchRow> found = temp.Context.WorkOrderService.Search(admin, new WorkOrderSearchFilter
        {
            FechaInicio = new DateTime(2026, 3, 1),
            FechaFin = new DateTime(2026, 5, 31),
            TextoBusqueda = "rodamiento"
        });
        sw.Stop();

        Assert.NotEmpty(found);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Búsqueda indexada tomó {sw.ElapsedMilliseconds}ms.");
    }
}
