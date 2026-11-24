using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Services;
using Sispro.UI.Forms;
using Sispro.UI.Theme;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class BulkOperationsAndExtendedWorkOrderTests
{
    [Fact]
    public void SearchGridSelection_PersistsAcrossSortFilterAndPages_AndExportsSelectedExclusivelyWithoutDuplicates()
    {
        using TempTestDatabase temp = new();
        SisproApplicationContext ctx = temp.Context;
        UserSession admin = ctx.AuthService.Login("admin", "Admin2026!").Session!;

        int provId = ctx.MaintainerService.SaveProvider(admin, new Provider
        {
            Nombre = "MAESTRANZA ANDINA SPA",
            Direccion = "Av. Industrial 450",
            Correo = "contacto@andina.cl"
        });

        int solId = ctx.MaintainerService.SaveRequester(admin, CreateRequesterWithSignature("J Ramos", "Juan", "Ramos", "firma_jr.png"));
        (int idN1, int idN2) = EnsureRootAndPlant(ctx, admin);
        int idN3 = ctx.AssetTreeService.AddChild(admin, idN2, "LÍNEA ENVASADO 1", AssetNodeTypes.Maquina);

        // OT 1 multi-ítem (2 detalles -> aparece en 2 filas de búsqueda, pero debe exportarse 1 sola vez)
        WorkOrder ot1 = new()
        {
            NumeroOt = 71001,
            IdProv = provId,
            IdSol = solId,
            FechaOt = new DateTime(2026, 11, 20),
            Observacion = "Prioridad alta de entrega con embalaje protector.",
            ValorNeto = 285000m,
            FirmasSeleccionadas = SignatureOption.JefaturaYTecnico,
            IdNivel = 3,
            IdN1 = idN1,
            IdN2 = idN2,
            IdN3 = idN3,
            Detalles =
            [
                new WorkOrderDetail { Cantidad = 1, Descripcion = "Rectificar eje principal acero" },
                new WorkOrderDetail { Cantidad = 2, Descripcion = "Cambiar rodamientos SKF 6205" }
            ]
        };
        int idOt1 = ctx.WorkOrderService.Create(admin, ot1);

        // OT 2 seleccionada
        WorkOrder ot2 = new()
        {
            NumeroOt = 71002,
            IdProv = provId,
            IdSol = solId,
            FechaOt = new DateTime(2026, 11, 21),
            Observacion = "Incluye calibración dimensional final.",
            ValorNeto = 150000m,
            FirmasSeleccionadas = SignatureOption.MarceloSilva,
            IdNivel = 3,
            IdN1 = idN1,
            IdN2 = idN2,
            IdN3 = idN3,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Fabricar acople bronce" }]
        };
        int idOt2 = ctx.WorkOrderService.Create(admin, ot2);

        // OT 3 NO seleccionada (no debe aparecer en la exportación seleccionada)
        WorkOrder ot3 = new()
        {
            NumeroOt = 71003,
            IdProv = provId,
            IdSol = solId,
            FechaOt = new DateTime(2026, 11, 22),
            Observacion = "Orden excluida de exportación masiva.",
            ValorNeto = 999999m,
            FirmasSeleccionadas = SignatureOption.Ninguna,
            IdNivel = 3,
            IdN1 = idN1,
            IdN2 = idN2,
            IdN3 = idN3,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Servicio externo no seleccionado" }]
        };
        int idOt3 = ctx.WorkOrderService.Create(admin, ot3);

        TempTestDatabase.RunOnStaThread(() =>
        {
            using Form host = new()
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000),
                Size = new Size(1320, 820),
                ShowInTaskbar = false
            };
            host.Show();
            using WorkOrdersForm form = new(ctx, admin);
            host.Controls.Add(form);
            form.OpenSearchTab();
            Application.DoEvents();

            Assert.Equal("OT seleccionadas: 0", form.LblSearchSelectedCount.Text);
            Assert.False(form.BtnExportSelectedSearch.Enabled);
            Assert.False(form.BtnClearSearchSelection.Enabled);

            // Marcar OT 1 (multi-ítem) y OT 2
            form.SearchSelection.SetSelected(idOt1, true);
            form.SearchSelection.SetSelected(idOt2, true);
            Assert.Equal(2, form.SearchSelection.SelectedCount);
            Assert.Equal("OT seleccionadas: 2", form.LblSearchSelectedCount.Text);
            Assert.True(form.BtnExportSelectedSearch.Enabled);
            Assert.True(form.BtnClearSearchSelection.Enabled);

            // Ordenar la grilla por N° OT ascendente y comprobar que ambas filas de OT1 sigan marcadas
            DataGridViewColumn numCol = form.GridSearchResults.Columns.Cast<DataGridViewColumn>()
                .First(c => c.DataPropertyName == "NumeroOt");
            form.GridSearchResults.Sort(numCol, System.ComponentModel.ListSortDirection.Ascending);

            Assert.Equal(2, form.SearchSelection.SelectedCount);
            foreach (DataGridViewRow r in form.GridSearchResults.Rows)
            {
                WorkOrderSearchRow? src = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(form.GridSearchResults, r.Index);
                if (src is not null)
                {
                    bool expected = src.IdOt == idOt1 || src.IdOt == idOt2;
                    Assert.Equal(expected, r.Cells[0].Value is bool b && b);
                }
            }

            // Re-ejecutar búsqueda y validar persistencia de selección
            form.ExecuteSearchForTest();
            Assert.Equal(2, form.SearchSelection.SelectedCount);

            // Seleccionar todos los visibles y luego limpiar selección con el botón real
            form.ChkSelectAllSearchVisible.Checked = true;
            Assert.Equal(3, form.SearchSelection.SelectedCount);
            Assert.True(form.SearchSelection.IsSelected(idOt3));

            form.BtnClearSearchSelection.PerformClick();
            Assert.Equal(0, form.SearchSelection.SelectedCount);
            Assert.False(form.BtnExportSelectedSearch.Enabled);
            host.Hide();
        });

        // Exportación exclusiva de idOt1 e idOt2 a Excel, CSV y PDF sin duplicar idOt1
        IReadOnlyList<WorkOrder> selectedOrders = ctx.WorkOrderService.GetByIds(admin, [idOt1, idOt2]);
        Assert.Equal(2, selectedOrders.Count);

        TabularReportData selectedReport = DocumentExportService.BuildSelectedWorkOrdersReport(
            selectedOrders,
            "OT seleccionadas: 2 órdenes");
        Assert.Equal(2, selectedReport.Rows.Count); // sin duplicados por los 2 ítems de ot1
        Assert.Equal(11, selectedReport.Columns.Count);

        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_bulk_exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            string xlsxPath = Path.Combine(outDir, $"OT_Seleccionadas_{DateTime.Today:yyyy-MM-dd}.xlsx");
            string csvPath = Path.Combine(outDir, $"OT_Seleccionadas_{DateTime.Today:yyyy-MM-dd}.csv");
            string pdfPath = Path.Combine(outDir, $"OT_Seleccionadas_{DateTime.Today:yyyy-MM-dd}.pdf");

            ctx.DocumentExportService.ExportReport(selectedReport, ExportFormat.Excel, xlsxPath);
            ctx.DocumentExportService.ExportReport(selectedReport, ExportFormat.Csv, csvPath);
            ctx.DocumentExportService.ExportReport(selectedReport, ExportFormat.Pdf, pdfPath);

            using (XLWorkbook wb = new(xlsxPath))
            {
                IXLWorksheet ws = wb.Worksheets.First();
                List<int> exportedOtNumbers = [ws.Cell(6, 1).GetValue<int>(), ws.Cell(7, 1).GetValue<int>()];
                Assert.Contains(71001, exportedOtNumbers);
                Assert.Contains(71002, exportedOtNumbers);
                Assert.DoesNotContain(71003, exportedOtNumbers);
                Assert.True(ws.Cell(8, 9).HasFormula); // Suma de Valor Neto
                Assert.Equal(435000m, ws.Cell(6, 9).GetValue<decimal>() + ws.Cell(7, 9).GetValue<decimal>());
            }

            string csvText = File.ReadAllText(csvPath, Encoding.UTF8);
            Assert.Contains("71001", csvText);
            Assert.Contains("71002", csvText);
            Assert.DoesNotContain("71003", csvText);
            Assert.Contains("Prioridad alta de entrega", csvText);
            Assert.Contains("285000", csvText);
            Assert.Contains("Jefatura, Técnico", csvText);

            string pdfText = Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
            Assert.StartsWith("%PDF-1.4", pdfText);
            Assert.Contains("71001", pdfText);
            Assert.Contains("71002", pdfText);
            Assert.DoesNotContain("71003", pdfText);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }
    }

    [Fact]
    public void ExtendedWorkOrderFields_ObservacionValorNetoAndSignatures_ValidatePersistAndRenderInDocuments()
    {
        Assert.True(WorkOrderService.TryParseNetValue("150000", out decimal v1) && v1 == 150000m);
        Assert.True(WorkOrderService.TryParseNetValue("$ 1.250.000", out decimal v2) && v2 == 1250000m);
        Assert.True(WorkOrderService.TryParseNetValue("", out decimal v3) && v3 == 0m);
        Assert.True(WorkOrderService.TryParseNetValue("-50", out decimal vNeg) && vNeg == -50m);
        Assert.False(WorkOrderService.TryParseNetValue("no-numerico", out _));

        using TempTestDatabase temp = new();
        SisproApplicationContext ctx = temp.Context;
        UserSession admin = ctx.AuthService.Login("admin", "Admin2026!").Session!;

        int provId = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "TORNERÍA CENTRAL" });
        int solId = ctx.MaintainerService.SaveRequester(admin, CreateRequesterWithSignature("M Silva", "Marcelo", "Silva", "firma_ms.png"));
        (int idN1, int idN2) = EnsureRootAndPlant(ctx, admin);

        // Validar rechazo de valor neto negativo
        Assert.Throws<ArgumentException>(() => ctx.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 72000,
            IdProv = provId,
            IdSol = solId,
            ValorNeto = -100m,
            IdNivel = 2,
            IdN1 = idN1,
            IdN2 = idN2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Prueba negativa" }]
        }));

        // Verificar controles ampliados en WorkOrdersForm STA y guardar OT con solo Jefatura + Técnico
        TempTestDatabase.RunOnStaThread(() =>
        {
            using WorkOrdersForm form = new(ctx, admin);
            Assert.True(form.TxtValorNetoEntry.Font.Bold);
            Assert.True(form.TxtValorNetoEntry.Font.Size >= 14f);
            Assert.NotNull(form.TxtObservacionEntry);

            form.ChkEntrySigJefatura.Checked = true;
            form.ChkEntrySigTecnico.Checked = true;
            form.ChkEntrySigSupervisor.Checked = false;
            form.TxtObservacionEntry.Text = "Requiere certificado de balanceo dinámico.";
            form.TxtValorNetoEntry.Text = "$ 420.500";
        });

        int otId = ctx.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 72001,
            IdProv = provId,
            IdSol = solId,
            FechaOt = DateTime.Today,
            Observacion = "Requiere certificado de balanceo dinámico.",
            ValorNeto = 420500m,
            FirmasSeleccionadas = SignatureOption.JefaturaYTecnico,
            IdNivel = 2,
            IdN1 = idN1,
            IdN2 = idN2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Balanceo rotor centrífugo" }]
        });

        WorkOrder loaded = ctx.WorkOrderService.GetById(otId)!;
        Assert.Equal("Requiere certificado de balanceo dinámico.", loaded.Observacion);
        Assert.Equal(420500m, loaded.ValorNeto);
        Assert.Equal(SignatureOption.JefaturaYTecnico, loaded.FirmasSeleccionadas);

        // Actualizar selección de firmas a vacío (Ninguna) y confirmar persistencia y pie sin firmas en PDF
        ctx.WorkOrderService.UpdateSignatures(admin, otId, SignatureOption.Ninguna);
        WorkOrder afterClearSig = ctx.WorkOrderService.GetById(otId)!;
        Assert.Equal(SignatureOption.Ninguna, afterClearSig.FirmasSeleccionadas);
        Assert.Equal("Sin firmas", afterClearSig.FirmasSeleccionadas.FormatResumen());

        // Restaurar solo Supervisor y verificar documentos PDF, Excel y Word
        ctx.WorkOrderService.UpdateSignatures(admin, otId, SignatureOption.MarceloSilva);
        WorkOrder afterSupSig = ctx.WorkOrderService.GetById(otId)!;
        Assert.Equal("Supervisor", afterSupSig.FirmasSeleccionadas.FormatResumen());

        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_doc_ext_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            string pdfEmptySigPath = Path.Combine(outDir, "ot_empty_sig.pdf");
            string pdfPath = Path.Combine(outDir, "ot.pdf");
            string xlsxPath = Path.Combine(outDir, "ot.xlsx");
            string docxPath = Path.Combine(outDir, "ot.docx");

            ctx.DocumentExportService.ExportWorkOrder(afterClearSig, ExportFormat.Pdf, pdfEmptySigPath, afterClearSig.FirmasSeleccionadas);
            string emptySigPdfText = Encoding.Latin1.GetString(File.ReadAllBytes(pdfEmptySigPath));
            Assert.DoesNotContain("Juan Ramos Moyano", emptySigPdfText);
            Assert.DoesNotContain("Marcelo Silva Morgado", emptySigPdfText);
            Assert.DoesNotContain("Técnico", emptySigPdfText);

            ctx.DocumentExportService.ExportWorkOrder(afterSupSig, ExportFormat.Pdf, pdfPath, afterSupSig.FirmasSeleccionadas);
            ctx.DocumentExportService.ExportWorkOrder(afterSupSig, ExportFormat.Excel, xlsxPath, afterSupSig.FirmasSeleccionadas);
            ctx.DocumentExportService.ExportWorkOrder(afterSupSig, ExportFormat.Word, docxPath, afterSupSig.FirmasSeleccionadas);

            string pdfText = Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
            Assert.Contains("VALOR NETO", pdfText);
            Assert.Contains("420.500", pdfText);
            Assert.Contains("balanceo", pdfText);
            Assert.Contains("Marcelo Silva", pdfText);
            Assert.DoesNotContain("Juan Ramos Moyano", pdfText);

            using XLWorkbook wb = new(xlsxPath);
            IXLWorksheet ws = wb.Worksheets.First();
            string allExcelText = string.Join(" ", ws.CellsUsed().Select(c => c.GetString()));
            Assert.Contains("VALOR NETO", allExcelText);
            Assert.Contains("balanceo", allExcelText);

            using WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, isEditable: false);
            string wordText = doc.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains("VALOR NETO", wordText);
            Assert.Contains("420.500", wordText);
            Assert.Contains("balanceo", wordText);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        }

        // Verificar carga en WorkOrdersForm STA
        TempTestDatabase.RunOnStaThread(() =>
        {
            using WorkOrdersForm form = new(ctx, admin);
            form.OpenOrderByNumber(72001);
            Assert.Equal("Requiere certificado de balanceo dinámico.", form.TxtObservacionEntry.Text);
            Assert.Contains("420.500", form.TxtValorNetoEntry.Text);
            Assert.False(form.ChkEntrySigJefatura.Checked);
            Assert.False(form.ChkEntrySigTecnico.Checked);
            Assert.True(form.ChkEntrySigSupervisor.Checked);
        });
    }

    [Fact]
    public void MaintainersAndAssetTree_EnforceAdminOnlyOnCriticalActions_AndHandleDependenciesWithoutAutoDeletingOts()
    {
        using TempTestDatabase temp = new();
        SisproApplicationContext ctx = temp.Context;
        UserSession admin = ctx.AuthService.Login("admin", "Admin2026!").Session!;

        ctx.UserManagementService.CreateUser(admin, new User
        {
            IdUsuario = "128498745",
            AliasUsuario = "sup_test",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Vega",
            Nivel = UserRoleLevel.Supervisor,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
        }, "Sup2026!", "Sup2026!");
        UserSession supervisor = ctx.AuthService.Login("sup_test", "Sup2026!").Session!;

        int provUsed = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROVEEDOR CON OT" });
        int provFree = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROVEEDOR LIBRE" });
        int provTarget = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROVEEDOR DESTINO" });

        int solUsed = ctx.MaintainerService.SaveRequester(admin, CreateRequesterWithSignature("SOL1", "Ana", "Gomez", "firma_sol1.png"));
        int solFree = ctx.MaintainerService.SaveRequester(admin, CreateRequesterWithSignature("SOL2", "Luis", "Paz", "firma_sol2.png"));
        int solTarget = ctx.MaintainerService.SaveRequester(admin, CreateRequesterWithSignature("SOL3", "Mario", "Diaz", "firma_sol3.png"));

        (int idN1, int idN2) = EnsureRootAndPlant(ctx, admin);
        int assetUsed = ctx.AssetTreeService.AddChild(admin, idN2, "MAQUINA CON OT", AssetNodeTypes.Maquina);
        int assetChild = ctx.AssetTreeService.AddChild(admin, assetUsed, "MOTOR AUXILIAR", AssetNodeTypes.Componente);
        int assetFree = ctx.AssetTreeService.AddChild(admin, idN2, "MAQUINA SIN OT", AssetNodeTypes.Maquina);
        int assetTarget = ctx.AssetTreeService.AddChild(admin, idN2, "MAQUINA DESTINO", AssetNodeTypes.Maquina);

        int idOt = ctx.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 73001,
            IdProv = provUsed,
            IdSol = solUsed,
            FechaOt = DateTime.Today,
            Observacion = "OT ligada a mantenedores",
            ValorNeto = 50000m,
            IdNivel = 3,
            IdN1 = idN1,
            IdN2 = idN2,
            IdN3 = assetUsed,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Mantención acople" }]
        });

        // 1. Seguridad estricta: Supervisor NO puede desactivar, eliminar, reasignar ni eliminar OTs
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.DeleteProvider(supervisor, provFree));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.SetProviderActive(supervisor, provUsed, false));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.DeleteProvidersBatch(supervisor, [provFree]));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.ReassignProviderWorkOrders(supervisor, [provUsed], provTarget));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.DeleteRequester(supervisor, solFree));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.ClearSignaturesBatch(supervisor, [solFree]));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.ReassignRequesterWorkOrders(supervisor, [solUsed], solTarget));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.MaintainerService.DeleteRelatedWorkOrdersExplicitly(supervisor, [idOt], "Intento no autorizado"));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.AssetTreeService.SetActiveBatch(supervisor, [assetFree], false));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.AssetTreeService.DeleteNodesBatch(supervisor, [assetFree]));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.AssetTreeService.ReassignWorkOrdersNode(supervisor, [assetUsed], assetTarget));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.AssetTreeService.ReassignWorkOrdersProvider(supervisor, [assetUsed], provTarget));
        Assert.Throws<UnauthorizedAccessException>(() => ctx.AssetTreeService.ReassignChildrenAndMove(supervisor, [assetUsed], assetTarget));

        // 2. Inspección de dependencias reales
        MaintainerDependencyReport provDep = ctx.MaintainerService.InspectDependencies(admin, MaintainerEntityKind.Proveedor, [provUsed, provFree]);
        Assert.Single(provDep.RelatedWorkOrders);
        Assert.Equal(73001, provDep.RelatedWorkOrders[0].NumeroOt);

        MaintainerDependencyReport assetDep = ctx.AssetTreeService.InspectDependencies(admin, [assetUsed]);
        Assert.Single(assetDep.RelatedWorkOrders);
        Assert.Equal(1, assetDep.DependentChildNodesCount);

        // 3. Eliminación mixta parcial: elimina libres, omite con OTs SIN borrar la OT
        MaintainerBatchResult provDelRes = ctx.MaintainerService.DeleteProvidersBatch(admin, [provUsed, provFree]);
        Assert.Single(provDelRes.Succeeded);
        Assert.Single(provDelRes.FailedOrSkipped);
        Assert.NotNull(ctx.WorkOrderService.GetById(idOt));

        MaintainerBatchResult sigDelRes = ctx.MaintainerService.ClearSignaturesBatch(admin, [solUsed, solFree]);
        Assert.Single(sigDelRes.Succeeded);
        Assert.Single(sigDelRes.FailedOrSkipped);
        Assert.False(ctx.Requesters.GetById(solFree)!.TieneFirma);
        Assert.True(ctx.Requesters.GetById(solUsed)!.TieneFirma);

        // 4. Desactivar conserva historial y bloquea usar en nuevas OT
        MaintainerBatchResult deactProvRes = ctx.MaintainerService.SetProvidersActiveBatch(admin, [provUsed], false);
        Assert.Single(deactProvRes.Succeeded);
        Assert.NotNull(ctx.WorkOrderService.GetById(idOt)); // historial intacto
        Assert.Throws<InvalidOperationException>(() => ctx.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 73002,
            IdProv = provUsed,
            IdSol = solTarget,
            IdNivel = 3,
            IdN1 = idN1,
            IdN2 = idN2,
            IdN3 = assetTarget,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Debe fallar proveedor inactivo" }]
        }));
        ctx.MaintainerService.SetProvidersActiveBatch(admin, [provUsed], true);

        // 5. Reasignar proveedor, solicitante/firma y activo + subnodos -> luego eliminar orígenes limpiamente
        Assert.Equal(1, ctx.MaintainerService.ReassignProviderWorkOrders(admin, [provUsed], provTarget));
        Assert.Equal(1, ctx.MaintainerService.ReassignRequesterWorkOrders(admin, [solUsed], solTarget, requireTargetSignature: true));
        Assert.Equal(1, ctx.AssetTreeService.ReassignWorkOrdersNode(admin, [assetUsed], assetTarget));

        MaintainerBatchResult moveChildrenRes = ctx.AssetTreeService.ReassignChildrenAndMove(admin, [assetUsed], assetTarget);
        Assert.Single(moveChildrenRes.Succeeded);
        Assert.Equal(assetTarget, ctx.AssetTree.GetById(assetChild)!.ParentId);

        WorkOrder afterReassign = ctx.WorkOrderService.GetById(idOt)!;
        Assert.Equal(provTarget, afterReassign.IdProv);
        Assert.Equal(solTarget, afterReassign.IdSol);
        Assert.Equal(assetTarget, afterReassign.IdN3);

        // Ahora los orígenes ya no tienen dependencias y se eliminan sin errores
        Assert.Single(ctx.MaintainerService.DeleteProvidersBatch(admin, [provUsed]).Succeeded);
        Assert.Single(ctx.MaintainerService.DeleteRequestersBatch(admin, [solUsed]).Succeeded);
        Assert.Equal(2, ctx.AssetTreeService.DeleteNodesBatch(admin, [assetUsed, assetFree]).Succeeded.Count);

        // 6. Eliminación explícita de OT asociada por Administrador con trazabilidad
        int syncHits = 0;
        ctx.SyncHub.DataChanged += (_, e) =>
        {
            if ((e.Kind & DataChangeKind.WorkOrders) != 0) syncHits++;
        };
        int deletedOts = ctx.WorkOrderService.DeleteManyExplicitly(admin, [idOt], "Limpieza controlada QA");
        Assert.Equal(1, deletedOts);
        Assert.Null(ctx.WorkOrderService.GetById(idOt));
        Assert.True(syncHits > 0);
        Assert.Contains(ctx.Audit.GetRecent(40), a => a.Accion == "ACCESO_NO_AUTORIZADO" && a.Usuario == "sup_test");

        // 7. Verificar UI 4 pestañas en MaintainersForm STA (Admin vs Supervisor no ve botones destructivos)
        TempTestDatabase.RunOnStaThread(() =>
        {
            using MaintainersForm form = new(ctx, admin);
            Assert.Equal(4, form.TabControlMain.TabPages.Count);
            Assert.Equal("Proveedores", form.TabControlMain.TabPages[0].Text);
            Assert.Equal("Solicitantes", form.TabControlMain.TabPages[1].Text);
            Assert.Equal("Firmas", form.TabControlMain.TabPages[2].Text);
            Assert.Equal("Árbol de Activos", form.TabControlMain.TabPages[3].Text);
            Assert.True(form.AssetTreePanelControl.ShowCheckBoxes);
            Assert.NotNull(form.GridProviders.Columns[GridCheckboxSelectionController<Provider>.SelectColumnName]);
            Assert.NotNull(form.RequestersControl.GridRequesters.Columns[GridCheckboxSelectionController<Requester>.SelectColumnName]);
            Assert.NotNull(form.SignaturesControl.GridSignatures.Columns[GridCheckboxSelectionController<Requester>.SelectColumnName]);

            using MaintainersForm supForm = new(ctx, supervisor);
            Assert.False(supForm.RequestersControl.BtnEliminar.Visible);
            Assert.False(supForm.RequestersControl.BtnDesactivar.Visible);
            Assert.False(supForm.RequestersControl.BtnReasignar.Visible);
            Assert.False(supForm.SignaturesControl.BtnDeleteSelected.Visible);
        });
    }

    private static (int IdN1, int IdN2) EnsureRootAndPlant(SisproApplicationContext ctx, UserSession admin)
    {
        IReadOnlyList<AssetNode> roots = ctx.AssetTree.GetChildren(null, includeInactive: true);
        int idN1 = roots.Count > 0 ? roots[0].IdActivo : ctx.AssetTree.Create(new AssetNode
        {
            Descripcion = "CAMBIASO HNOS S.A.C.",
            Categoria = AssetNodeTypes.Empresa,
            Nivel = 1,
            Activo = true
        });

        IReadOnlyList<AssetNode> plants = ctx.AssetTree.GetChildren(idN1, includeInactive: false);
        int idN2 = plants.Count > 0
            ? plants[0].IdActivo
            : ctx.AssetTreeService.AddChild(admin, idN1, "PLANTA PRUEBAS", AssetNodeTypes.Planta);

        return (idN1, idN2);
    }

    private static Requester CreateRequesterWithSignature(string alias, string nombre, string pat, string fileName)
    {
        using Bitmap bmp = new(180, 70);
        using Graphics g = Graphics.FromImage(bmp);
        using Font sigFont = new("Segoe UI", 12f, FontStyle.Bold);
        g.Clear(Color.White);
        g.DrawString(alias, sigFont, Brushes.DarkBlue, 10, 20);
        using MemoryStream ms = new();
        bmp.Save(ms, ImageFormat.Png);
        ProcessedSignature proc = SignatureFileService.ProcessBytes(fileName, ms.ToArray());
        return new Requester
        {
            Alias = alias,
            Nombre = nombre,
            ApellidoPaterno = pat,
            Activo = true,
            FirmaBytes = proc.RawBytes,
            FirmaNombre = proc.FileName,
            FirmaTipo = proc.FileType,
            FirmaPreviewPng = proc.PreviewPngBytes
        };
    }
}
