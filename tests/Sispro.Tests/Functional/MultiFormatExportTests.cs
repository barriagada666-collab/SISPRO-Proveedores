using System.Drawing;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Forms;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class MultiFormatExportTests
{
    [Fact]
    public void DocumentExportService_GeneratesValidExcelPdfWordAndCsv_PreservingFiltersAndOrder()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_exports_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);

        try
        {
            IReadOnlyList<WorkOrderSearchRow> orders =
            [
                new WorkOrderSearchRow
                {
                    IdOt = 2,
                    NumeroOt = 2002,
                    Proveedor = "MAESTRANZA PACÍFICO SPA",
                    Solicitante = "B. Arriagada",
                    Fecha = new DateTime(2026, 11, 20),
                    Cantidad = 2,
                    Detalle = "Reparación eje principal",
                    RutaActivo = "CAMBIASO\\PLANTA PE\\EXTRUSIÓN",
                    TieneVbs = true,
                    VbsUsuario = "M Silva",
                    TieneVbj = true,
                    VbjUsuario = "J Ramos"
                },
                new WorkOrderSearchRow
                {
                    IdOt = 1,
                    NumeroOt = 2001,
                    Proveedor = "ELÉCTRICOS VALPARAÍSO",
                    Solicitante = "J. Ramos",
                    Fecha = new DateTime(2026, 10, 12),
                    Cantidad = 1,
                    Detalle = "Rebobinado motor 15HP",
                    RutaActivo = "CAMBIASO\\PLANTA TE\\ENVASADO",
                    TieneVbs = false,
                    TieneVbj = false
                }
            ];

            TabularReportData report = DocumentExportService.BuildWorkOrdersReport(orders, "Fechas: 01-10-2026 a 30-11-2026");

            string xlsxPath = Path.Combine(outDir, "ordenes.xlsx");
            string pdfPath = Path.Combine(outDir, "ordenes.pdf");
            string docxPath = Path.Combine(outDir, "ordenes.docx");
            string csvPath = Path.Combine(outDir, "ordenes.csv");

            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Excel, xlsxPath);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Pdf, pdfPath);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Word, docxPath);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Csv, csvPath);

            // 1. Validate Excel (.xlsx): corporate banner + row order + SUM formula + approvers
            Assert.True(File.Exists(xlsxPath) && new FileInfo(xlsxPath).Length > 3000);
            using (XLWorkbook wb = new(xlsxPath))
            {
                IXLWorksheet ws = wb.Worksheets.First();
                Assert.Contains("ORDENES DE TRABAJO", ws.Cell(1, 1).GetString().Replace('Ó', 'O') + ws.Cell(1, 2).GetString().Replace('Ó', 'O'), StringComparison.OrdinalIgnoreCase);
                Assert.Equal(2002, ws.Cell(6, 1).GetValue<int>()); // First data row (newest)
                Assert.Equal(2001, ws.Cell(7, 1).GetValue<int>()); // Second data row
                Assert.True(ws.Cell(8, 5).HasFormula); // SUM formula on Cant. column
                Assert.Equal("Valor Neto", ws.Cell(5, 9).GetString());
                Assert.DoesNotContain("VB Supervisor", ws.Row(5).Cells(1, 11).Select(c => c.GetString()));
                Assert.DoesNotContain("VB Jefatura", ws.Row(5).Cells(1, 11).Select(c => c.GetString()));
            }

            // 2. Validate PDF (.pdf): real PDF 1.4 header, EOF, text content & pagination
            byte[] pdfBytes = File.ReadAllBytes(pdfPath);
            string pdfAscii = Encoding.Latin1.GetString(pdfBytes);
            Assert.StartsWith("%PDF-1.4", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("%%EOF", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("2002", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("MAESTRANZA", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("Valor Neto", pdfAscii, StringComparison.Ordinal);
            Assert.DoesNotContain("VB Supervisor", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("de 1", pdfAscii, StringComparison.Ordinal);

            // 3. Validate Word (.docx): real OpenXML package with table rows and footer
            Assert.True(File.Exists(docxPath) && new FileInfo(docxPath).Length > 2000);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, isEditable: false))
            {
                string innerText = doc.MainDocumentPart!.Document.InnerText;
                Assert.Contains("TRABAJO EXTERNAS", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("MAESTRANZA", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Valor Neto", innerText, StringComparison.Ordinal);
                Assert.DoesNotContain("VB Supervisor", innerText, StringComparison.Ordinal);
                Assert.True(innerText.IndexOf("2002", StringComparison.Ordinal) < innerText.IndexOf("2001", StringComparison.Ordinal));
                Assert.NotEmpty(doc.MainDocumentPart.FooterParts);
            }

            // 4. Validate CSV (.csv): UTF-8 BOM, clean text, descending order preserved
            byte[] csvRaw = File.ReadAllBytes(csvPath);
            Assert.True(csvRaw.Length > 3 && csvRaw[0] == 0xEF && csvRaw[1] == 0xBB && csvRaw[2] == 0xBF);
            string[] csvLines = File.ReadAllLines(csvPath, Encoding.UTF8);
            Assert.Equal(3, csvLines.Length); // header + 2 rows
            Assert.StartsWith("2002;", csvLines[1], StringComparison.Ordinal);
            Assert.StartsWith("2001;", csvLines[2], StringComparison.Ordinal);

            // 5. Validate Single WorkOrder Export in all 4 formats (Excel, PDF, Word, CSV)
            WorkOrder sampleOt = new()
            {
                NumeroOt = 14800,
                FechaOt = new DateTime(2026, 11, 23),
                NombreProveedor = "MAESTRANZA CAMBIASO TEST",
                DireccionProveedor = "Placilla, Valparaíso",
                CorreoProveedor = "contacto@maestranza.cl",
                AliasSolicitante = admin.Usuario.AliasUsuario,
                RutaActivo = "CAMBIASO HNOS S.A.C. \\ PLANTA TÉ \\ ENVASADORA 1",
                Detalles = [new WorkOrderDetail { Cantidad = 2, Descripcion = "Fabricación piñón acero templado" }]
            };

            string otXlsx = Path.Combine(outDir, "ot.xlsx");
            string otPdf = Path.Combine(outDir, "ot.pdf");
            string otDocx = Path.Combine(outDir, "ot.docx");
            string otCsv = Path.Combine(outDir, "ot.csv");

            temp.Context.DocumentExportService.ExportWorkOrder(sampleOt, ExportFormat.Excel, otXlsx);
            temp.Context.DocumentExportService.ExportWorkOrder(sampleOt, ExportFormat.Pdf, otPdf);
            temp.Context.DocumentExportService.ExportWorkOrder(sampleOt, ExportFormat.Word, otDocx);
            temp.Context.DocumentExportService.ExportWorkOrder(sampleOt, ExportFormat.Csv, otCsv);

            Assert.True(File.Exists(otXlsx) && new FileInfo(otXlsx).Length > 2000);
            Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(File.ReadAllBytes(otPdf)), StringComparison.Ordinal);
            using (WordprocessingDocument otDoc = WordprocessingDocument.Open(otDocx, false))
            {
                Assert.Contains("14800", otDoc.MainDocumentPart!.Document.InnerText, StringComparison.Ordinal);
            }
            Assert.Contains("Fabricación piñón acero templado", File.ReadAllText(otCsv, Encoding.UTF8), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportServices_SanitizeControlCharsPreserveWin1252AndPaginateLongWorkOrder()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppSettings settings = new();
        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_exp_hard_{Guid.NewGuid():N}");

        try
        {
            TabularReportData dirtyReport = new()
            {
                Title = "Auditoría\u0001\u000B € — Ñandú",
                Subtitle = "Control\u0002 limpieza • Cambiaso",
                FilterSummary = "Filtro ≥ 10 → OK",
                Columns =
                [
                    new ExportColumn("Código", ExportColumnKind.Text, 1.0),
                    new ExportColumn("Detalle", ExportColumnKind.Text, 2.5),
                    new ExportColumn("Monto", ExportColumnKind.Currency, 1.2, SumInFooter: true)
                ],
                Rows =
                [
                    ["IT-01\u0007", "Reparación\u000B motor € — ñ ≤ 5", 125000m]
                ]
            };

            string nestedDir = Path.Combine(outDir, "sub");
            string xlsx = Path.Combine(nestedDir, "dirty.xlsx");
            string docx = Path.Combine(nestedDir, "dirty.docx");
            string pdf = Path.Combine(nestedDir, "dirty.pdf");

            ExcelExportService excel = new(settings);
            excel.ExportTabularReport(dirtyReport, xlsx);
            using (XLWorkbook wb = new(xlsx))
            {
                string allCells = string.Join(" | ", wb.Worksheet(1).CellsUsed().Select(c => c.GetString()));
                Assert.DoesNotContain('\u0001', allCells);
                Assert.DoesNotContain('\u000B', allCells);
                Assert.Contains("Auditoría € — Ñandú", allCells);
                Assert.Contains("Reparación motor € — ñ ≤ 5", allCells);
            }

            WordExportService word = new(settings);
            word.ExportTabularReport(dirtyReport, docx);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(docx, false))
            {
                string bodyText = doc.MainDocumentPart!.Document.Body!.InnerText;
                Assert.Contains("Ñandú", bodyText);
                Assert.Contains("€", bodyText);
            }

            PdfExportService pdfSvc = new(settings);
            pdfSvc.ExportTabularReport(dirtyReport, pdf);
            byte[] dirtyPdfBytes = File.ReadAllBytes(pdf);
            Encoding win1252 = Encoding.GetEncoding(1252);
            string pdfWin = win1252.GetString(dirtyPdfBytes);
            Assert.Contains("€ —", pdfWin);
            Assert.Contains("Ñandú", pdfWin);
            Assert.Contains(">=", pdfWin);
            Assert.Contains("->", pdfWin);
            Assert.Contains("<=", pdfWin);

            WorkOrder longOrder = new()
            {
                NumeroOt = 987654,
                FechaOt = DateTime.Today,
                NombreProveedor = "PROVEEDOR\u0001 INDUSTRIAL SPA",
                DireccionProveedor = "Av. Limpia 123 — Valparaíso",
                CorreoProveedor = "contacto@proveedor.cl",
                RutaActivo = "CAMBIASO\\PLANTA PE\\EXTRUSIÓN",
                AliasSolicitante = "B. Arriagada",
                NombreSolicitante = "Boris Arriagada Rojas",
                CorreoSolicitante = "barriagada@cambiaso.cl",
                TelefonoSolicitante = "+56 32 2000000",
                VistosBuenos =
                [
                    new WorkOrderApproval { Tipo = ApprovalType.Supervisor }
                ],
                Detalles = Enumerable.Range(1, 25)
                    .Select(i => new WorkOrderDetail
                    {
                        IdDet = i,
                        Cantidad = i,
                        Descripcion = $"Ítem {i}: mantención integral de rodamientos y ejes principales en línea {i} (€ / mm)."
                    })
                    .ToList()
            };

            string otPdf = Path.Combine(outDir, "nested_ot", "ot_long.pdf");
            string otXlsx = Path.Combine(outDir, "nested_ot", "ot_long.xlsx");
            string otDocx = Path.Combine(outDir, "nested_ot", "ot_long.docx");
            string otEmptySigPdf = Path.Combine(outDir, "nested_ot", "ot_empty_sig.pdf");

            pdfSvc.ExportWorkOrderDocument(longOrder, otPdf, SignatureOption.AmbasConSolicitante);
            excel.ExportWorkOrderDocument(longOrder, otXlsx, SignatureOption.AmbasConSolicitante);
            word.ExportWorkOrderDocument(longOrder, otDocx, SignatureOption.AmbasConSolicitante);
            pdfSvc.ExportWorkOrderDocument(longOrder, otEmptySigPdf, SignatureOption.Ninguna);

            using (XLWorkbook otWb = new(otXlsx))
            {
                IXLWorksheet wsOt = otWb.Worksheet(1);
                Assert.Contains("PROVEEDOR INDUSTRIAL SPA", wsOt.Cell(8, 2).GetString());
                Assert.Equal(string.Empty, wsOt.Cell(11, 2).GetString()); // Activo ya no está en Proveedor
                Assert.Equal("ACTIVO:", wsOt.Cell(11, 4).GetString());
                Assert.Contains("EXTRUSIÓN", wsOt.Cell(11, 5).GetString());
                Assert.Contains("Boris Arriagada Rojas", wsOt.Cell(8, 5).GetString());
                string allOtText = string.Join(" | ", wsOt.CellsUsed().Select(c => c.GetString()));
                Assert.Contains("987654", allOtText);
                Assert.Contains("Técnico", allOtText);
                Assert.DoesNotContain("VB Supervisor:", allOtText);
                Assert.DoesNotContain("VB Jefatura:", allOtText);
                Assert.DoesNotContain("Guía / Estado", allOtText);
                Assert.DoesNotContain("Valor / Ref.", allOtText);
                Assert.DoesNotContain("Registrado", allOtText);
            }
            using (WordprocessingDocument otDoc = WordprocessingDocument.Open(otDocx, false))
            {
                string otDocText = otDoc.MainDocumentPart!.Document.InnerText;
                Assert.Contains("987654", otDocText);
                Assert.Contains("Boris Arriagada Rojas", otDocText);
                Assert.Contains("Técnico", otDocText);
                Assert.DoesNotContain("VB Supervisor:", otDocText);
                Assert.DoesNotContain("VB Jefatura:", otDocText);
                Assert.DoesNotContain("Estado:", otDocText);
            }

            string otPdfText = win1252.GetString(File.ReadAllBytes(otPdf));
            Assert.StartsWith("%PDF-1.4", otPdfText);
            Assert.DoesNotContain("/Count 1 ", otPdfText);
            Assert.Contains("Página 1 de ", otPdfText);
            Assert.Contains("Página 2 de ", otPdfText);
            Assert.Contains("987654", otPdfText);
            Assert.Contains("Boris Arriagada Rojas", otPdfText);
            Assert.Contains("Técnico", otPdfText);
            Assert.DoesNotContain("VB Supervisor:", otPdfText);
            Assert.DoesNotContain("VB Jefatura:", otPdfText);
            Assert.DoesNotContain("Estado:", otPdfText);
            Assert.DoesNotContain("Registrado", otPdfText);

            string emptySigPdfText = win1252.GetString(File.ReadAllBytes(otEmptySigPdf));
            Assert.DoesNotContain("Juan Ramos Moyano", emptySigPdfText);
            Assert.DoesNotContain("Marcelo Silva Morgado", emptySigPdfText);
            Assert.DoesNotContain("Técnico", emptySigPdfText);
        }
        finally
        {
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }
        }
    }

    [Fact]
    public void DynamicFieldSelection_ExportsOnlySelectedFieldsInOrder_AcrossExcelPdfWordAndCsv()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppSettings settings = new();
        DocumentExportService exportService = new(
            new ExcelExportService(settings),
            new PdfExportService(settings),
            new WordExportService(settings),
            new CsvExportService());
        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_dyn_cols_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);

        try
        {
            IReadOnlyList<WorkOrderSearchRow> searchRows =
            [
                new WorkOrderSearchRow
                {
                    IdOt = 10,
                    NumeroOt = 14815,
                    Fecha = new DateTime(2026, 11, 18),
                    Proveedor = "DAPREMAQ",
                    Solicitante = "Marcelo Silva",
                    Cantidad = 3,
                    Detalle = "Extensión de arriendo grúa horquilla 2.5 ton",
                    Observacion = "Obs interna confidencial",
                    ValorNeto = 350000m,
                    MostrarValorNeto = true,
                    Estado = WorkOrderStatus.Abierta,
                    FirmasSeleccionadas = SignatureOption.AmbasConSolicitante
                }
            ];

            WorkOrder[] selectedOrders =
            [
                new WorkOrder
                {
                    IdOt = 10,
                    NumeroOt = 14815,
                    TipoOrden = "Externa",
                    FechaOt = new DateTime(2026, 11, 18),
                    NombreProveedor = "DAPREMAQ",
                    NombreSolicitante = "Marcelo Silva",
                    RutaActivo = "CAMBIASO HNOS S.A.C. \\ PLANTA PE \\ GRÚAS",
                    Observacion = "Obs interna confidencial",
                    ValorNeto = 350000m,
                    MostrarValorNeto = true,
                    Estado = WorkOrderStatus.Abierta,
                    FirmasSeleccionadas = SignatureOption.JefaturaYTecnico,
                    Detalles =
                    [
                        new WorkOrderDetail { Cantidad = 1, Descripcion = "Arriendo grúa horquilla" },
                        new WorkOrderDetail { Cantidad = 4, Descripcion = "Horas operador adicional" }
                    ]
                }
            ];

            WorkOrderExportField[] subset =
            [
                WorkOrderExportField.NumeroOt,
                WorkOrderExportField.Fecha,
                WorkOrderExportField.Proveedor,
                WorkOrderExportField.Detalle,
                WorkOrderExportField.ValorNeto,
                WorkOrderExportField.Estado
            ];

            TabularReportData listReport = DocumentExportService.BuildWorkOrdersReport(searchRows, "Filtro prueba", subset);
            Assert.Equal(["N° OT", "Fecha", "Proveedor", "Detalle del Trabajo", "Valor Neto", "Estado"], listReport.Columns.Select(c => c.Header));

            string xlsx = Path.Combine(outDir, "subset_list.xlsx");
            string pdf = Path.Combine(outDir, "subset_list.pdf");
            string docx = Path.Combine(outDir, "subset_list.docx");
            string csv = Path.Combine(outDir, "subset_list.csv");

            exportService.ExportReport(listReport, ExportFormat.Excel, xlsx);
            exportService.ExportReport(listReport, ExportFormat.Pdf, pdf);
            exportService.ExportReport(listReport, ExportFormat.Word, docx);
            exportService.ExportReport(listReport, ExportFormat.Csv, csv);

            using (XLWorkbook wb = new(xlsx))
            {
                IXLWorksheet ws = wb.Worksheet(1);
                string[] headers = Enumerable.Range(1, 6).Select(i => ws.Cell(5, i).GetString()).ToArray();
                Assert.Equal(["N° OT", "Fecha", "Proveedor", "Detalle del Trabajo", "Valor Neto", "Estado"], headers);
                Assert.Equal(string.Empty, ws.Cell(5, 7).GetString());
            }

            Encoding win1252 = Encoding.GetEncoding(1252);
            string pdfText = win1252.GetString(File.ReadAllBytes(pdf));
            Assert.Contains("Valor Neto", pdfText);
            Assert.DoesNotContain("Solicitante", pdfText);
            Assert.DoesNotContain("Observaciones", pdfText);
            Assert.DoesNotContain("Firmas", pdfText);

            using (WordprocessingDocument doc = WordprocessingDocument.Open(docx, false))
            {
                string bodyText = doc.MainDocumentPart!.Document.Body!.InnerText;
                Assert.Contains("Valor Neto", bodyText);
                Assert.DoesNotContain("Solicitante", bodyText);
                Assert.DoesNotContain("Firmas", bodyText);
            }

            string csvHeader = File.ReadAllLines(csv, Encoding.UTF8)[0];
            Assert.Equal("N° OT;Fecha;Proveedor;Detalle del Trabajo;Valor Neto;Estado", csvHeader);

            // Exportación de seleccionadas incluyendo campos opcionales (Tipo OT, Ruta Activo, Cant.)
            WorkOrderExportField[] customSelected =
            [
                WorkOrderExportField.NumeroOt,
                WorkOrderExportField.TipoOrden,
                WorkOrderExportField.Cantidad,
                WorkOrderExportField.Detalle,
                WorkOrderExportField.RutaActivo,
                WorkOrderExportField.Firmas
            ];
            TabularReportData selReport = DocumentExportService.BuildSelectedWorkOrdersReport(selectedOrders, "1 OT", customSelected);
            Assert.Equal(["N° OT", "Tipo", "Cant.", "Detalle", "Activo", "Firmas"], selReport.Columns.Select(c => c.Header));
            Assert.Equal(5, selReport.Rows[0][2]); // 1 + 4
            Assert.Contains("Arriendo grúa horquilla", selReport.Rows[0][3]?.ToString());
            Assert.Contains("Horas operador adicional", selReport.Rows[0][3]?.ToString());
            Assert.Contains("PLANTA PE", selReport.Rows[0][4]?.ToString());
            Assert.Equal("Jefatura, Técnico", selReport.Rows[0][5]?.ToString());
        }
        finally
        {
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }
        }
    }

    [Fact]
    public void ExportAutoFitWrapping_NeverTruncatesLongText_AcrossPdfExcelAndWord_AndPaginatesCleanly()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        AppSettings settings = new();
        DocumentExportService exportService = new(
            new ExcelExportService(settings),
            new PdfExportService(settings),
            new WordExportService(settings),
            new CsvExportService());
        string outDir = Path.Combine(Path.GetTempPath(), $"sispro_wrap_{Guid.NewGuid():N}");
        Directory.CreateDirectory(outDir);

        try
        {
            const string longDetailMarker = "FIN_DETALLE_COMPLETO_9988";
            const string longObsMarker = "OBS_COMPLETA_SIN_RECORTE_7766";
            const string longDetailText =
                "Extensión de arriendo grúa horquilla 2.5 toneladas con operador certificado en planta Placilla, " +
                "incluye mantención preventiva de horquillas, revisión hidráulica completa y entrega certificada — " +
                longDetailMarker;
            const string longObsText =
                "Coordinar ingreso por portería principal con 24 horas de anticipación y adjuntar certificado — " +
                longObsMarker;

            List<WorkOrderSearchRow> rows = Enumerable.Range(0, 26)
                .Select(i => new WorkOrderSearchRow
                {
                    IdOt = 100 + i,
                    NumeroOt = 14800 + i,
                    Fecha = new DateTime(2026, 11, 18),
                    Proveedor = i % 2 == 0 ? "DAPREMAQ SERVICIOS INDUSTRIALES SPA" : "INGERSOLL RAND CHILE LTDA",
                    Solicitante = "Marcelo Silva Morgado",
                    Cantidad = i + 1,
                    Detalle = i == 0 ? longDetailText : $"Realizar mantención preventiva de 4000 horas a compresor principal línea {i} con cambio de filtros, aceite sintético y pruebas de carga.",
                    Observacion = i == 0 ? longObsText : "Revisar informes de vibraciones y temperatura.",
                    ValorNeto = 450000m + (i * 15000m),
                    MostrarValorNeto = true,
                    Estado = WorkOrderStatus.EnCurso,
                    FirmasSeleccionadas = SignatureOption.AmbasConSolicitante
                })
                .ToList();

            TabularReportData report = DocumentExportService.BuildWorkOrdersReport(
                rows,
                "Prueba ajuste automático completo",
                WorkOrderExportFieldCatalog.DefaultSearchGridFields);

            string pdfPath = Path.Combine(outDir, "wrap.pdf");
            string xlsxPath = Path.Combine(outDir, "wrap.xlsx");
            string docxPath = Path.Combine(outDir, "wrap.docx");

            exportService.ExportReport(report, ExportFormat.Pdf, pdfPath);
            exportService.ExportReport(report, ExportFormat.Excel, xlsxPath);
            exportService.ExportReport(report, ExportFormat.Word, docxPath);

            // 1. PDF: 100% del texto largo completo sin elipsis '...', encabezados completos y multipágina real
            Encoding win1252 = Encoding.GetEncoding(1252);
            string pdfText = win1252.GetString(File.ReadAllBytes(pdfPath));
            Assert.StartsWith("%PDF-1.4", pdfText);
            Assert.Contains("Valor Neto", pdfText);
            Assert.Contains("Observaciones", pdfText);
            // Extraer texto literal de operadores PDF (...) Tj (excluyendo bytes binarios JPEG del logo)
            string pdfJoinedWords = string.Join(" ",
                System.Text.RegularExpressions.Regex.Matches(pdfText, @"\((?<t>(?:\\.|[^\\)])*)\) Tj")
                    .Select(m => m.Groups["t"].Value.Replace(@"\(", "(").Replace(@"\)", ")").Replace(@"\\", @"\")));
            Assert.DoesNotContain("Valor Ne...", pdfJoinedWords);
            Assert.DoesNotContain("…", pdfJoinedWords);
            Assert.DoesNotContain("...", pdfJoinedWords);
            string pdfCompact = pdfJoinedWords.Replace(" ", string.Empty);
            Assert.Contains(longDetailMarker, pdfCompact);
            Assert.Contains(longObsMarker, pdfCompact);
            Assert.Contains("18-11-2026", pdfJoinedWords);
            Assert.Contains("Jefatura,", pdfJoinedWords);
            Assert.Contains("Técnico,", pdfJoinedWords);
            Assert.Contains("Supervisor", pdfJoinedWords);
            Assert.Contains("Página 1 de ", pdfText);
            Assert.Contains("Página 2 de ", pdfText);
            Assert.DoesNotContain("/Count 1 ", pdfText);

            // 2. Excel: WrapText activo y altura de fila expandida para el texto largo
            using (XLWorkbook wb = new(xlsxPath))
            {
                IXLWorksheet ws = wb.Worksheet(1);
                Assert.True(ws.Cell(5, 6).Style.Alignment.WrapText);
                Assert.True(ws.Cell(6, 6).Style.Alignment.WrapText);
                Assert.Contains(longDetailMarker, ws.Cell(6, 6).GetString());
                Assert.Contains(longObsMarker, ws.Cell(6, 7).GetString());
                Assert.True(ws.Row(6).Height > 28.0, $"Expected wrapped row height > 28, got {ws.Row(6).Height}");
            }

            // 3. Word: texto largo íntegro y anchos proporcionales que suman exactamente 5000 (100%)
            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, false))
            {
                Body body = doc.MainDocumentPart!.Document.Body!;
                Assert.Contains(longDetailMarker, body.InnerText);
                Assert.Contains(longObsMarker, body.InnerText);

                Table dataTable = body.Elements<Table>().Last();
                TableRow headerRow = dataTable.Elements<TableRow>().First();
                int sumPct = headerRow.Elements<TableCell>()
                    .Select(c => int.Parse(c.TableCellProperties!.TableCellWidth!.Width!.Value!, System.Globalization.CultureInfo.InvariantCulture))
                    .Sum();
                Assert.Equal(5000, sumPct);
            }
        }
        finally
        {
            if (Directory.Exists(outDir))
            {
                Directory.Delete(outDir, recursive: true);
            }
        }
    }

    [Fact]
    public void SearchGridColumnHeaderCheckBoxes_ToggleExportFieldsWithoutDisruptingSort_AndSyncUiState()
    {
        using TempTestDatabase temp = new();
        SisproApplicationContext ctx = temp.Context;
        UserSession admin = ctx.AuthService.Login("admin", "Admin2026!").Session!;

        int provId = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "DAPREMAQ SPA" });
        int solId = ctx.MaintainerService.SaveRequester(admin, new Requester { Alias = "M Silva", Nombre = "Marcelo", ApellidoPaterno = "Silva", Activo = true });
        IReadOnlyList<AssetNode> roots = ctx.AssetTree.GetChildren(null, includeInactive: true);
        int idN1 = roots.Count > 0
            ? roots[0].IdActivo
            : ctx.AssetTree.Create(new AssetNode { Descripcion = "CAMBIASO", Categoria = AssetNodeTypes.Empresa, Nivel = 1, Activo = true });
        IReadOnlyList<AssetNode> plants = ctx.AssetTree.GetChildren(idN1, includeInactive: false);
        int idN2 = plants.Count > 0 ? plants[0].IdActivo : ctx.AssetTreeService.AddChild(admin, idN1, "PLANTA PE", AssetNodeTypes.Planta);

        ctx.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 88001,
            IdProv = provId,
            IdSol = solId,
            FechaOt = new DateTime(2026, 11, 20),
            ValorNeto = 120000m,
            IdNivel = 2,
            IdN1 = idN1,
            IdN2 = idN2,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Primera OT de prueba" }]
        });
        ctx.WorkOrderService.Create(admin, new WorkOrder
        {
            NumeroOt = 88002,
            IdProv = provId,
            IdSol = solId,
            FechaOt = new DateTime(2026, 11, 21),
            ValorNeto = 240000m,
            IdNivel = 2,
            IdN1 = idN1,
            IdN2 = idN2,
            Detalles = [new WorkOrderDetail { Cantidad = 2, Descripcion = "Segunda OT de prueba" }]
        });

        TempTestDatabase.RunOnStaThread(() =>
        {
            using Form host = new()
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000),
                Size = new Size(1366, 768),
                ShowInTaskbar = false
            };
            host.Show();
            using WorkOrdersForm form = new(ctx, admin);
            host.Controls.Add(form);
            form.OpenSearchTab();
            Application.DoEvents();

            Assert.Equal(10, form.SearchExportColumns.GridFieldCount);
            Assert.Equal(10, form.SearchExportColumns.SelectedFields.Count);
            Assert.Equal("Campos (10/10)", form.BtnSearchExportColumns.Text);
            Assert.NotNull(form.BtnSearchExportColumns.Image);
            Assert.True(form.GridSearchResults.ColumnHeadersHeight >= 46);

            DataGridViewColumn numCol = form.GridSearchResults.Columns.Cast<DataGridViewColumn>().First(c => c.DataPropertyName == "NumeroOt");
            DataGridViewColumn obsCol = form.GridSearchResults.Columns.Cast<DataGridViewColumn>().First(c => c.DataPropertyName == "Observacion");
            DataGridViewColumn sigCol = form.GridSearchResults.Columns.Cast<DataGridViewColumn>().First(c => c.DataPropertyName == "Firmas");

            int firstBeforeClick = Convert.ToInt32(form.GridSearchResults.Rows[0].Cells[numCol.Index].Value, System.Globalization.CultureInfo.InvariantCulture);

            // Clic en zona inferior de checkbox (y=32) desmarca columnas SIN alterar el orden de las filas
            form.SearchExportColumns.HandleHeaderClick(obsCol.Index, 32);
            form.SearchExportColumns.HandleHeaderClick(sigCol.Index, 32);

            Assert.Equal(8, form.SearchExportColumns.SelectedFields.Count);
            Assert.False(form.SearchExportColumns.IsFieldSelected(WorkOrderExportField.Observacion));
            Assert.False(form.SearchExportColumns.IsFieldSelected(WorkOrderExportField.Firmas));
            Assert.Equal("Campos (8/10)", form.BtnSearchExportColumns.Text);
            Assert.Equal(firstBeforeClick, Convert.ToInt32(form.GridSearchResults.Rows[0].Cells[numCol.Index].Value, System.Globalization.CultureInfo.InvariantCulture));

            // Clic en zona superior de título (y=10) ordena la columna SIN desmarcar el checkbox
            form.SearchExportColumns.HandleHeaderClick(numCol.Index, 10);
            Assert.True(form.SearchExportColumns.IsFieldSelected(WorkOrderExportField.NumeroOt));
            Assert.Equal(SortOrder.Ascending, numCol.HeaderCell.SortGlyphDirection);
            Assert.Equal(88001, Convert.ToInt32(form.GridSearchResults.Rows[0].Cells[numCol.Index].Value, System.Globalization.CultureInfo.InvariantCulture));

            form.SearchExportColumns.HandleHeaderClick(numCol.Index, 10);
            Assert.Equal(SortOrder.Descending, numCol.HeaderCell.SortGlyphDirection);
            Assert.Equal(88002, Convert.ToInt32(form.GridSearchResults.Rows[0].Cells[numCol.Index].Value, System.Globalization.CultureInfo.InvariantCulture));

            // Activar campo adicional (RutaActivo) + presets
            form.SearchExportColumns.ToggleField(WorkOrderExportField.RutaActivo);
            Assert.Equal("Campos (8+1)", form.BtnSearchExportColumns.Text);
            Assert.Contains(WorkOrderExportField.RutaActivo, form.SearchExportColumns.SelectedFields);

            form.SearchExportColumns.SelectEssentialFields();
            Assert.Equal(WorkOrderExportFieldCatalog.EssentialFields.Length, form.SearchExportColumns.SelectedFields.Count);
            Assert.Equal("Campos (7/10)", form.BtnSearchExportColumns.Text);

            form.SearchExportColumns.SelectAllGridFields();
            Assert.Equal(10, form.SearchExportColumns.SelectedGridFieldCount);
            Assert.Equal("Campos (10/10)", form.BtnSearchExportColumns.Text);

            // Validar pintado GDI+ completo del grid con checkboxes de encabezado sin excepciones
            using Bitmap bmp = new(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));

            host.Hide();
        });
    }
}
