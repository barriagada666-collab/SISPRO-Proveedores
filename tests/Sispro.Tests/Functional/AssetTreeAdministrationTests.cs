using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Services;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class AssetTreeAdministrationTests
{
    [Fact]
    public void AdminCrud_CreateUpdateMoveAndCycleDepthProtection()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        AssetTreeService treeSvc = temp.Context.AssetTreeService;

        // 1. Crear jerarquía completa con categorías explícitas y sugeridas
        int plantaId = treeSvc.AddChild(admin, 1, new AssetNode
        {
            Descripcion = "PLANTA NORTE",
            Categoria = AssetNodeTypes.Planta,
            CodSap = "SAP-PN-01"
        });
        int seccionA = treeSvc.AddChild(admin, plantaId, "SECCIÓN ENVASADO");
        int maqA = treeSvc.AddChild(admin, seccionA, "ENVASADORA V10");
        int modA = treeSvc.AddChild(admin, maqA, "CABEZAL SELLADO");

        Assert.Equal(AssetNodeTypes.Seccion, temp.Context.AssetTree.GetById(seccionA)!.Categoria);
        Assert.Equal(AssetNodeTypes.Maquina, temp.Context.AssetTree.GetById(maqA)!.Categoria);
        Assert.Equal(AssetNodeTypes.Modulo, temp.Context.AssetTree.GetById(modA)!.Categoria);

        // Duplicado entre hermanos (sin distinguir mayúsculas/minúsculas) debe rechazarse
        Assert.Throws<InvalidOperationException>(() =>
            treeSvc.AddChild(admin, plantaId, "sección envasado"));

        // Editar nodo y ficha técnica
        AssetNode maqEdit = temp.Context.AssetTree.GetById(maqA)!;
        maqEdit.Descripcion = "ENVASADORA V10 PRO";
        maqEdit.Fabricante = "IMA TEA";
        maqEdit.Modelo = "C27";
        maqEdit.Categoria = "Máquina";
        treeSvc.UpdateNode(admin, maqEdit);

        AssetNodeInfo info = treeSvc.GetNodeInfo(maqA);
        Assert.Equal(4, info.Depth);
        Assert.Equal(AssetNodeTypes.Maquina, info.Node.Categoria);
        Assert.Equal(1, info.ActiveChildren);
        Assert.Equal("CAMBIASO HNOS S.A.C. \\ PLANTA NORTE \\ SECCIÓN ENVASADO \\ ENVASADORA V10 PRO", info.Path);

        // Bloqueo de ciclos (mover padre bajo descendiente)
        Assert.Throws<InvalidOperationException>(() => treeSvc.MoveNode(admin, seccionA, modA));
        Assert.Throws<InvalidOperationException>(() => treeSvc.MoveNode(admin, seccionA, seccionA));

        // Mover rama seccionA > maqA > modA bajo otra planta más profunda D y verificar recálculo de niveles
        int plantaSur = treeSvc.AddChild(admin, 1, "PLANTA SUR", AssetNodeTypes.Planta);
        int sectorSur = treeSvc.AddChild(admin, plantaSur, "SECTOR INDUSTRIAL", AssetNodeTypes.Seccion);

        treeSvc.MoveNode(admin, seccionA, sectorSur);

        Assert.Equal(4, temp.Context.AssetTree.GetById(seccionA)!.Nivel);
        Assert.Equal(5, temp.Context.AssetTree.GetById(maqA)!.Nivel);
        Assert.Equal(6, temp.Context.AssetTree.GetById(modA)!.Nivel);

        // Bloqueo cuando mover excede el máximo de 10 niveles
        int deep = modA; // nivel 6
        for (int lvl = 7; lvl <= 9; lvl++)
        {
            deep = treeSvc.AddChild(admin, deep, $"SUBNIVEL {lvl}");
        }
        // seccionA tiene altura 5 (niveles 4..9). Mover bajo una rama de profundidad 6 produciría 6 + 1 + 5 = 12 > 10.
        int deepDest = treeSvc.AddChild(admin, plantaId, "DESTINO");
        for (int i = 0; i < 4; i++)
        {
            deepDest = treeSvc.AddChild(admin, deepDest, $"D{i}");
        }
        Assert.Throws<InvalidOperationException>(() => treeSvc.MoveNode(admin, seccionA, deepDest));
    }

    [Fact]
    public void NonAdminSupervisor_CannotModifyAssetTree_AndAttemptsAreAudited()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        int plantId = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA PROTEGIDA", AssetNodeTypes.Planta);
        int leafId = temp.Context.AssetTreeService.AddChild(admin, plantId, "MÁQUINA PROTEGIDA", AssetNodeTypes.Maquina);

        UserPermissions supPerms = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor);
        supPerms.MenuMantenedor = true;
        supPerms.NormalizeHierarchy();

        temp.Context.UserManagementService.CreateUser(admin, new User
        {
            IdUsuario = "128498745",
            AliasUsuario = "sup_mant",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Méndez",
            Nivel = UserRoleLevel.Supervisor,
            Permisos = supPerms
        }, "ClaveSup1!", "ClaveSup1!");

        UserSession supervisor = temp.Context.AuthService.Login("sup_mant", "ClaveSup1!").Session!;
        AssetTreeService treeSvc = temp.Context.AssetTreeService;

        Assert.False(treeSvc.IsAdministrator(supervisor));
        Assert.True(treeSvc.IsAdministrator(admin));

        Assert.Throws<UnauthorizedAccessException>(() => treeSvc.AddChild(supervisor, 1, "NO AUTORIZADO"));
        Assert.Throws<UnauthorizedAccessException>(() => treeSvc.UpdateNode(supervisor, new AssetNode { IdActivo = leafId, Descripcion = "X" }));
        Assert.Throws<UnauthorizedAccessException>(() => treeSvc.SetActive(supervisor, leafId, false, true));
        Assert.Throws<UnauthorizedAccessException>(() => treeSvc.MoveNode(supervisor, leafId, 1));
        Assert.Throws<UnauthorizedAccessException>(() => treeSvc.DeleteNode(supervisor, leafId));
        Assert.Throws<UnauthorizedAccessException>(() => treeSvc.NormalizeLevels(supervisor));

        IReadOnlyList<AuditEntry> logs = temp.Context.Audit.GetRecent(20);
        int deniedCount = logs.Count(e => e.Usuario == "sup_mant" && e.Modulo == "Árbol de Activos" && e.Accion == "ACCESO_NO_AUTORIZADO");
        Assert.Equal(6, deniedCount);
    }

    [Fact]
    public void DeleteNode_BlocksChildrenAndHistoricalRefs_DeletesCleanLeaf()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        AssetTreeService treeSvc = temp.Context.AssetTreeService;

        int plantaId = treeSvc.AddChild(admin, 1, "PLANTA DELETE", AssetNodeTypes.Planta);
        int maqSinOt = treeSvc.AddChild(admin, plantaId, "MÁQUINA NUEVA", AssetNodeTypes.Maquina);
        int maqConOt = treeSvc.AddChild(admin, plantaId, "MÁQUINA CON OT", AssetNodeTypes.Maquina);

        // No permite eliminar padre con hijos
        Assert.Throws<InvalidOperationException>(() => treeSvc.DeleteNode(admin, plantaId));

        // Hoja limpia sí se elimina definitivamente
        treeSvc.DeleteNode(admin, maqSinOt);
        Assert.Null(temp.Context.AssetTree.GetById(maqSinOt));

        // Asociar OT histórica a maqConOt -> elimina bloqueado
        int provId = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROV TREE REF" });
        WorkOrder ot = new()
        {
            NumeroOt = 77001,
            IdProv = provId,
            FechaOt = DateTime.Today,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Reparación máquina" }]
        };
        ot.SetLevels([1, plantaId, maqConOt]);
        temp.Context.WorkOrderService.Create(admin, ot);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => treeSvc.DeleteNode(admin, maqConOt));
        Assert.Contains("OT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SetActive_CascadesDeactivation_AndProtectsRootAndInactiveParents()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        AssetTreeService treeSvc = temp.Context.AssetTreeService;

        int plantaId = treeSvc.AddChild(admin, 1, "PLANTA ACT", AssetNodeTypes.Planta);
        int secId = treeSvc.AddChild(admin, plantaId, "SECCIÓN ACT", AssetNodeTypes.Seccion);
        int maqId = treeSvc.AddChild(admin, secId, "MÁQUINA ACT", AssetNodeTypes.Maquina);

        // Raíz protegida
        Assert.Throws<InvalidOperationException>(() => treeSvc.SetActive(admin, 1, false, true));

        // Desactivar sección desactiva en cascada sección y máquina
        int deactivated = treeSvc.SetActive(admin, secId, false, includeDescendants: true);
        Assert.Equal(2, deactivated);
        Assert.False(temp.Context.AssetTree.GetById(secId)!.Activo);
        Assert.False(temp.Context.AssetTree.GetById(maqId)!.Activo);

        // No permite agregar hijo ni activar hijo mientras el padre esté inactivo
        Assert.Throws<InvalidOperationException>(() => treeSvc.AddChild(admin, secId, "OTRA MAQUINA"));
        Assert.Throws<InvalidOperationException>(() => treeSvc.SetActive(admin, maqId, true, includeDescendants: false));

        // Reactivar padre + descendientes
        int reactivated = treeSvc.SetActive(admin, secId, true, includeDescendants: true);
        Assert.Equal(2, reactivated);
        Assert.True(temp.Context.AssetTree.GetById(secId)!.Activo);
        Assert.True(temp.Context.AssetTree.GetById(maqId)!.Activo);
    }

    [Fact]
    public void GetPathToRoot_NormalizeLevels_And_MultiFormatExport_WorkOnRealAndAnomalousTrees()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;
        AssetTreeService treeSvc = temp.Context.AssetTreeService;

        // 1. Verificar normalización real sobre BD migrada
        Assert.True(treeSvc.CountLevelAnomalies() > 0);
        int fixedRows = treeSvc.NormalizeLevels(admin);
        Assert.True(fixedRows > 0);
        Assert.Equal(0, treeSvc.CountLevelAnomalies());

        // 2. Insertar anomalía explícita (hijo con nivel menor que padre) y validar ruta raíz -> hoja
        using (SqliteConnection conn = temp.Context.ConnectionFactory.OpenConnection())
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                INSERT INTO Activos(id_activo, parent_id, nivel, descripcion, activo, categoria)
                VALUES (9901, 1, 6, 'NODO ANOMALO PADRE', 1, 'PLANTA'),
                       (9902, 9901, 2, 'NODO ANOMALO HIJO', 1, 'MAQUINA');
                """;
            cmd.ExecuteNonQuery();
        }

        IReadOnlyList<AssetNode> path = temp.Context.MaintainerService.GetAssetPath(9902);
        Assert.Equal(3, path.Count);
        Assert.Equal(1, path[0].IdActivo);
        Assert.Equal(9901, path[1].IdActivo);
        Assert.Equal(9902, path[2].IdActivo);

        // 3. Exportar subárbol en los 4 formatos (Excel, PDF, Word y CSV)
        IReadOnlyList<AssetTreeExportRow> exportRows = treeSvc.GetSubtreeForExport(9901, includeInactive: true);
        Assert.Equal(2, exportRows.Count);
        TabularReportData report = DocumentExportService.BuildAssetTreeReport(exportRows, "Prueba árbol");

        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_tree_exp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);
        try
        {
            string xlsx = Path.Combine(outDir, "arbol.xlsx");
            string pdf = Path.Combine(outDir, "arbol.pdf");
            string docx = Path.Combine(outDir, "arbol.docx");
            string csv = Path.Combine(outDir, "arbol.csv");

            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Excel, xlsx);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Pdf, pdf);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Word, docx);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Csv, csv);

            using (XLWorkbook wb = new(xlsx))
            {
                Assert.Contains("NODO ANOMALO PADRE", wb.Worksheets.First().Cell(6, 4).GetString(), StringComparison.Ordinal);
            }
            Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(File.ReadAllBytes(pdf)), StringComparison.Ordinal);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(docx, false))
            {
                Assert.Contains("NODO ANOMALO HIJO", doc.MainDocumentPart!.Document.InnerText, StringComparison.Ordinal);
            }
            Assert.Contains("NODO ANOMALO HIJO", File.ReadAllText(csv, Encoding.UTF8), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }
}
