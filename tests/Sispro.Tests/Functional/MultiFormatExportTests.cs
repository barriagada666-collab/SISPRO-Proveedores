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
            IReadOnlyList<InvoiceSearchRow> invoices =
            [
                new InvoiceSearchRow
                {
                    IdFactura = 2,
                    NumeroDocumento = 2002,
                    Proveedor = "MAESTRANZA PACÍFICO SPA",
                    Fecha = new DateTime(2026, 11, 20),
                    RecibidoPor = "B. Arriagada",
                    CantidadItems = 2,
                    TotalNeto = 500_000m,
                    Iva = 95_000m,
                    TotalConIva = 595_000m,
                    Detalle = "Reparación eje principal"
                },
                new InvoiceSearchRow
                {
                    IdFactura = 1,
                    NumeroDocumento = 2001,
                    Proveedor = "ELÉCTRICOS VALPARAÍSO",
                    Fecha = new DateTime(2026, 10, 12),
                    RecibidoPor = "J. Ramos",
                    CantidadItems = 1,
                    TotalNeto = 200_000m,
                    Iva = 38_000m,
                    TotalConIva = 238_000m,
                    Detalle = "Rebobinado motor 15HP"
                }
            ];

            TabularReportData report = DocumentExportService.BuildInvoicesReport(invoices, "Fechas: 01-10-2026 a 30-11-2026");

            string xlsxPath = Path.Combine(outDir, "facturas.xlsx");
            string pdfPath = Path.Combine(outDir, "facturas.pdf");
            string docxPath = Path.Combine(outDir, "facturas.docx");
            string csvPath = Path.Combine(outDir, "facturas.csv");

            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Excel, xlsxPath);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Pdf, pdfPath);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Word, docxPath);
            temp.Context.DocumentExportService.ExportReport(report, ExportFormat.Csv, csvPath);

            // 1. Validate Excel (.xlsx): corporate banner + row order + SUM formula
            Assert.True(File.Exists(xlsxPath) && new FileInfo(xlsxPath).Length > 3000);
            using (XLWorkbook wb = new(xlsxPath))
            {
                IXLWorksheet ws = wb.Worksheets.First();
                Assert.Contains("FACTURAS", ws.Cell(1, 1).GetString() + ws.Cell(1, 2).GetString(), StringComparison.OrdinalIgnoreCase);
                Assert.Equal(2002, ws.Cell(6, 1).GetValue<int>()); // First data row (newest)
                Assert.Equal(2001, ws.Cell(7, 1).GetValue<int>()); // Second data row
                Assert.True(ws.Cell(8, 6).HasFormula); // SUM formula on Neto column
            }

            // 2. Validate PDF (.pdf): real PDF 1.4 header, EOF, text content & pagination
            byte[] pdfBytes = File.ReadAllBytes(pdfPath);
            string pdfAscii = Encoding.Latin1.GetString(pdfBytes);
            Assert.StartsWith("%PDF-1.4", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("%%EOF", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("2002", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("MAESTRANZA", pdfAscii, StringComparison.Ordinal);
            Assert.Contains("de 1", pdfAscii, StringComparison.Ordinal);

            // 3. Validate Word (.docx): real OpenXML package with table rows and footer
            Assert.True(File.Exists(docxPath) && new FileInfo(docxPath).Length > 2000);
            using (WordprocessingDocument doc = WordprocessingDocument.Open(docxPath, isEditable: false))
            {
                string innerText = doc.MainDocumentPart!.Document.InnerText;
                Assert.Contains("FACTURAS", innerText, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("MAESTRANZA", innerText, StringComparison.OrdinalIgnoreCase);
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

            pdfSvc.ExportWorkOrderDocument(longOrder, otPdf);
            excel.ExportWorkOrderDocument(longOrder, otXlsx);
            word.ExportWorkOrderDocument(longOrder, otDocx);

            using (XLWorkbook otWb = new(otXlsx))
            {
                Assert.Contains("PROVEEDOR INDUSTRIAL SPA", otWb.Worksheet(1).Cell(8, 2).GetString());
            }
            using (WordprocessingDocument otDoc = WordprocessingDocument.Open(otDocx, false))
            {
                Assert.Contains("987654", otDoc.MainDocumentPart!.Document.InnerText);
            }

            string otPdfText = win1252.GetString(File.ReadAllBytes(otPdf));
            Assert.StartsWith("%PDF-1.4", otPdfText);
            Assert.DoesNotContain("/Count 1 ", otPdfText);
            Assert.Contains("Página 1 de ", otPdfText);
            Assert.Contains("Página 2 de ", otPdfText);
            Assert.Contains("VB Supervisor: APROBADO", otPdfText);
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
