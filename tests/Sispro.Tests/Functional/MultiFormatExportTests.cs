using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
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
                Assert.Contains("M Silva", ws.Cell(6, 8).GetString());
                Assert.Contains("J Ramos", ws.Cell(6, 9).GetString());
            }

            // 2. Validate PDF (.pdf): real PDF 1.4 header, EOF, text content & pagination
            byte[] pdfBytes = File.ReadAllBytes(pdfPath);
            string pdfAscii = Encoding.Latin1.GetString(pdfBytes);
            Assert.StartsWith("%PDF-1.4", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("%%EOF", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("2002", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("MAESTRANZA", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("M Silva", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("de 1", pdfAscii, StringComparison.Ordinal);

            // 3. Validate Word (.docx): real OpenXML package with table rows and footer
            Assert.True(File.Exists(docxPath) && new FileInfo(docxPath).Length > 2000);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, isEditable: false))
            {
                string innerText = doc.MainDocumentPart!.Document.InnerText;
                Assert.Contains("TRABAJO EXTERNAS", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("MAESTRANZA", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("M Silva", innerText, StringComparison.Ordinal);
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
}
