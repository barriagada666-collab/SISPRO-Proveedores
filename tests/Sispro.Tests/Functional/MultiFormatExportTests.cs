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
}
