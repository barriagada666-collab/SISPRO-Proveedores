using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Printing;
using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Services;
using Sispro.UI.Forms;
using Xunit;

namespace Sispro.Tests.Functional;

public sealed class RequestersSignaturesAndApproversTests
{
    [Fact]
    public void SignatureFileService_SupportsPngJpgAndRealPdf_AndRejectsInvalidFiles()
    {
        using Bitmap bmp = new(260, 110);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.DrawLine(Pens.Navy, 14, 72, 230, 34);
            g.DrawString("Firma Digital", new Font("Segoe UI", 12f, FontStyle.Bold), Brushes.DarkBlue, 18, 28);
        }

        byte[] pngBytes;
        using (MemoryStream pngMs = new())
        {
            bmp.Save(pngMs, ImageFormat.Png);
            pngBytes = pngMs.ToArray();
        }

        byte[] jpgBytes;
        using (MemoryStream jpgMs = new())
        {
            bmp.Save(jpgMs, ImageFormat.Jpeg);
            jpgBytes = jpgMs.ToArray();
        }

        ProcessedSignature pngSig = SignatureFileService.ProcessBytes("firma_ms.png", pngBytes);
        Assert.Equal("PNG", pngSig.FileType);
        Assert.NotEmpty(pngSig.PreviewPngBytes);
        Assert.Equal(pngBytes, pngSig.RawBytes);

        ProcessedSignature jpgSig = SignatureFileService.ProcessBytes("firma_jr.jpeg", jpgBytes);
        Assert.Equal("JPG", jpgSig.FileType);
        Assert.NotEmpty(jpgSig.PreviewPngBytes);

        // Generate real valid PDF document and verify native Windows.Data.Pdf page-1 rasterization
        string tempPdfPath = Path.Combine(Path.GetTempPath(), $"sispro_sig_{Guid.NewGuid():N}.pdf");
        try
        {
            PdfExportService pdfSvc = new(new AppSettings());
            pdfSvc.ExportTabularReport(new TabularReportData
            {
                Title = "FIRMA DIGITAL AUTORIZADA",
                SheetName = "Firma",
                Columns = [new ExportColumn("Solicitante", ExportColumnKind.Text, 2.0)],
                Rows = [["J Ramos — Jefe de Mantención"]]
            }, tempPdfPath);

            ProcessedSignature pdfSig = SignatureFileService.ProcessFile(tempPdfPath);
            Assert.Equal("PDF", pdfSig.FileType);
            Assert.NotEmpty(pdfSig.PreviewPngBytes);
            using Bitmap? rendered = SignatureFileService.CreateBitmapFromBytes(pdfSig.PreviewPngBytes);
            Assert.NotNull(rendered);
            Assert.True(rendered.Width > 0 && rendered.Height > 0);
        }
        finally
        {
            if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath);
        }

        // Invalid extension or malformed PDF must be rejected cleanly
        Assert.Throws<ArgumentException>(() => SignatureFileService.ProcessBytes("firma.txt", Encoding.UTF8.GetBytes("bad")));
        Assert.Throws<ArgumentException>(() => SignatureFileService.ProcessBytes("broken.pdf", Encoding.ASCII.GetBytes("%PDF-1.4\ncorrupt")));
        Assert.Throws<ArgumentException>(() => SignatureFileService.ProcessBytes("empty.png", []));
    }

    [Fact]
    public void UserCreationWithRequesterAndPdfSignature_AndStandaloneRequesterCrud_PersistAndRenderInWorkOrderExports()
    {
        using TempTestDatabase temp = new();
        SisproApplicationContext ctx = temp.Context;
        UserSession admin = ctx.AuthService.Login("admin", "Admin2026!").Session!;

        // 1. Prepare valid PNG and PDF signatures
        byte[] pngBytes = CreateSampleSignaturePng("Firma PNG");
        string tempPdf = Path.Combine(Path.GetTempPath(), $"sispro_req_sig_{Guid.NewGuid():N}.pdf");
        try
        {
            ctx.PdfExportService.ExportTabularReport(new TabularReportData
            {
                Title = "FIRMA PDF SOLICITANTE",
                SheetName = "Firma",
                Columns = [new ExportColumn("Nombre", ExportColumnKind.Text, 2.0)],
                Rows = [["C. Méndez"]]
            }, tempPdf);
            byte[] pdfBytes = File.ReadAllBytes(tempPdf);

            // 2. Create user marked EsSolicitante with attached PDF signature via UserManagementService
            ProcessedSignature pdfProcessed = SignatureFileService.ProcessBytes("firma_cmendez.pdf", pdfBytes);
            ctx.UserManagementService.CreateUser(admin, new User
            {
                IdUsuario = "128498745",
                AliasUsuario = "cmendez",
                NombreUsuario = "Carlos",
                ApePatUsuario = "Méndez",
                ApeMatUsuario = "Vega",
                MailUsuario = "cmendez@cambiaso.cl",
                FonoUsuario = "+56 32 2201000",
                Nivel = UserRoleLevel.Supervisor,
                EsSolicitante = true,
                Activo = true,
                Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
            }, "ClaveSegura1!", "ClaveSegura1!", pdfProcessed);

            Requester? linkedRequester = ctx.Requesters.GetByAlias("cmendez");
            Assert.NotNull(linkedRequester);
            Assert.True(linkedRequester.TieneFirma);
            Assert.Equal("PDF", linkedRequester.FirmaTipo);
            Assert.Equal("firma_cmendez.pdf", linkedRequester.FirmaNombre);
            Assert.NotEmpty(linkedRequester.FirmaBytes!);
            Assert.NotEmpty(linkedRequester.FirmaPreviewPng!);

            // 3. Create standalone Solicitado Por directly inside UsersForm -> Solicitados Por y Firmas tab (UI flow)
            TempTestDatabase.RunOnStaThread(() =>
            {
                using UsersForm usersForm = new(ctx, admin);
                usersForm.ShowRequestersTab();
                usersForm.RequesterManager.TxtAlias.Text = "A Cuevas";
                usersForm.RequesterManager.TxtNombre.Text = "Andrés";
                usersForm.RequesterManager.TxtPat.Text = "Cuevas";
                usersForm.RequesterManager.TxtMail.Text = "acuevas@cambiaso.cl";
                usersForm.RequesterManager.AttachSignatureBytes("firma_acuevas.png", pngBytes);
                Assert.True(usersForm.RequesterManager.SaveFromEditor(isUpdate: false, showConfirmationDialog: false));
                Assert.NotNull(usersForm.RequesterManager.GridRequesters.DataSource);
            });

            Requester? standaloneRequester = ctx.Requesters.GetByAlias("A Cuevas");
            Assert.NotNull(standaloneRequester);
            Assert.True(standaloneRequester.TieneFirma);
            Assert.Equal("PNG", standaloneRequester.FirmaTipo);
            Assert.Equal("firma_acuevas.png", standaloneRequester.FirmaNombre);

            // 4. Create Work Order referencing requester with signature & approve with distinct VBS / VBJ users
            ctx.UserManagementService.CreateUser(admin, new User
            {
                IdUsuario = "914380006",
                AliasUsuario = "jramos_jef",
                NombreUsuario = "Juan",
                ApePatUsuario = "Ramos",
                Nivel = UserRoleLevel.Jefatura,
                Activo = true,
                Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Jefatura)
            }, "JefClave#2026", "JefClave#2026");

            UserSession supSession = ctx.AuthService.Login("cmendez", "ClaveSegura1!").Session!;
            UserSession jefSession = ctx.AuthService.Login("jramos_jef", "JefClave#2026").Session!;

            int provId = ctx.MaintainerService.SaveProvider(admin, new Provider { Nombre = "TALLER FIRMAS SPA" });
            int plantId = ctx.AssetTreeService.AddChild(admin, 1, "PLANTA FIRMAS");
            int otId = ctx.WorkOrderService.Create(admin, new WorkOrder
            {
                NumeroOt = 7771,
                IdProv = provId,
                IdSol = linkedRequester.IdSol,
                FechaOt = new DateTime(2026, 11, 24),
                IdN1 = 1,
                IdN2 = plantId,
                IdNivel = 2,
                Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Fabricación pieza especial con firma digital" }]
            });

            ctx.WorkOrderService.SetApproval(supSession, otId, ApprovalType.Supervisor, true);
            ctx.WorkOrderService.SetApproval(jefSession, otId, ApprovalType.Jefatura, true);

            // 5. Verify VB approver identity in GetApproverDirectory, WorkOrder entity, and Search results
            IReadOnlyList<ApproverDirectoryEntry> dir = ctx.WorkOrderService.GetApproverDirectory();
            Assert.Contains(dir, a => a.AliasUsuario == "cmendez" && a.Vbs && !a.Vbj);
            Assert.Contains(dir, a => a.AliasUsuario == "jramos_jef" && a.Vbs && a.Vbj);

            WorkOrder? loadedOt = ctx.WorkOrderService.GetByNumber(7771);
            Assert.NotNull(loadedOt);
            Assert.Equal("cmendez", loadedOt.VbsApproval?.AliasUsuario);
            Assert.Contains("Carlos Méndez", loadedOt.VbsResumen);
            Assert.Equal("jramos_jef", loadedOt.VbjApproval?.AliasUsuario);
            Assert.Contains("Juan Ramos", loadedOt.VbjResumen);
            Assert.NotNull(loadedOt.FirmaSolicitantePreviewPng);
            Assert.Equal("PDF", loadedOt.FirmaSolicitanteTipo);

            IReadOnlyList<WorkOrderSearchRow> searchRows = ctx.WorkOrderService.Search(admin, new WorkOrderSearchFilter { NumeroOt = 7771 });
            Assert.Single(searchRows);
            Assert.Equal("cmendez", searchRows[0].VbsUsuario);
            Assert.Equal("jramos_jef", searchRows[0].VbjUsuario);
            Assert.Equal("Sí (cmendez)", searchRows[0].VbsDisplay);
            Assert.Equal("Sí (jramos_jef)", searchRows[0].VbjDisplay);

            // 6. Verify WorkOrder exports (Excel, PDF, Word) embed requester signature & approver names cleanly
            string outDir = Path.Combine(Path.GetTempPath(), $"sispro_sig_exports_{Guid.NewGuid():N}");
            Directory.CreateDirectory(outDir);
            try
            {
                string xlsxPath = Path.Combine(outDir, "ot_sig.xlsx");
                string pdfPath = Path.Combine(outDir, "ot_sig.pdf");
                string docxPath = Path.Combine(outDir, "ot_sig.docx");

                ctx.DocumentExportService.ExportWorkOrder(loadedOt, ExportFormat.Excel, xlsxPath, SignatureOption.AmbasConSolicitante);
                ctx.DocumentExportService.ExportWorkOrder(loadedOt, ExportFormat.Pdf, pdfPath, SignatureOption.Solicitante);
                ctx.DocumentExportService.ExportWorkOrder(loadedOt, ExportFormat.Word, docxPath, SignatureOption.AmbasConSolicitante);

                using (XLWorkbook wb = new(xlsxPath))
                {
                    IXLWorksheet ws = wb.Worksheet(1);
                    Assert.True(ws.Pictures.Count >= 1);
                    string allText = string.Join(" ", ws.CellsUsed().Select(c => c.GetString()));
                    Assert.Contains("cmendez", allText);
                    Assert.Contains("jramos_jef", allText);
                }

                string pdfContent = Encoding.Latin1.GetString(File.ReadAllBytes(pdfPath));
                Assert.Contains("cmendez", pdfContent);
                Assert.Contains("jramos_jef", pdfContent);

                using (WordprocessingDocument docx = WordprocessingDocument.Open(docxPath, false))
                {
                    string docText = docx.MainDocumentPart!.Document.InnerText;
                    Assert.Contains("cmendez", docText);
                    Assert.Contains("jramos_jef", docText);
                    Assert.NotEmpty(docx.MainDocumentPart.ImageParts);
                }

                using PrintDocument pdoc = ctx.PrintService.CreateWorkOrderPrintDocument(loadedOt, SignatureOption.AmbasConSolicitante);
                Assert.NotNull(pdoc);
            }
            finally
            {
                if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
            }
        }
        finally
        {
            if (File.Exists(tempPdf)) File.Delete(tempPdf);
        }
    }

    private static byte[] CreateSampleSignaturePng(string label)
    {
        using Bitmap bmp = new(240, 90);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            g.DrawLine(Pens.Black, 10, 62, 220, 28);
            g.DrawString(label, new Font("Segoe UI", 11f, FontStyle.Italic), Brushes.Black, 20, 20);
        }

        using MemoryStream ms = new();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }
}
