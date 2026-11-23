using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;

namespace Sispro.UI.Theme;

/// <summary>
/// Menú desplegable unificado para exportar cualquier reporte corporativo o documento de OT
/// a Excel (.xlsx), PDF (.pdf), Word (.docx) o CSV (.csv), respetando los filtros y orden activos.
/// </summary>
public static class ExportUiHelper
{
    public static void ShowReportExportMenu(
        Control anchor,
        IWin32Window owner,
        DocumentExportService exportService,
        string baseFileName,
        Func<TabularReportData> reportBuilder,
        Action<string>? onCompleted = null)
    {
        ContextMenuStrip menu = new() { Font = CambiasoTheme.BodyFont };

        ToolStripMenuItem mExcel = new("Exportar a Excel (.xlsx)", IconCatalog.Get(AppIcon.Excel, CambiasoTheme.Success, 16));
        mExcel.Click += (_, _) => RunReportExport(owner, exportService, ExportFormat.Excel, baseFileName, reportBuilder, onCompleted);

        ToolStripMenuItem mPdf = new("Exportar a PDF (.pdf)", IconCatalog.Get(AppIcon.FileText, CambiasoTheme.Danger, 16));
        mPdf.Click += (_, _) => RunReportExport(owner, exportService, ExportFormat.Pdf, baseFileName, reportBuilder, onCompleted);

        ToolStripMenuItem mWord = new("Exportar a Word (.docx)", IconCatalog.Get(AppIcon.FileText, CambiasoTheme.Primary, 16));
        mWord.Click += (_, _) => RunReportExport(owner, exportService, ExportFormat.Word, baseFileName, reportBuilder, onCompleted);

        ToolStripMenuItem mCsv = new("Exportar a CSV (.csv)", IconCatalog.Get(AppIcon.Download, CambiasoTheme.Accent, 16));
        mCsv.Click += (_, _) => RunReportExport(owner, exportService, ExportFormat.Csv, baseFileName, reportBuilder, onCompleted);

        menu.Items.AddRange([mExcel, mPdf, mWord, new ToolStripSeparator(), mCsv]);
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    public static void ShowWorkOrderExportMenu(
        Control anchor,
        IWin32Window owner,
        DocumentExportService exportService,
        WorkOrder order,
        SignatureOption signatureOption)
    {
        ContextMenuStrip menu = new() { Font = CambiasoTheme.BodyFont };
        string baseName = $"OT_{order.NumeroOt:D6}_Cambiaso";

        ToolStripMenuItem mExcel = new("Orden en Excel (.xlsx)", IconCatalog.Get(AppIcon.Excel, CambiasoTheme.Success, 16));
        mExcel.Click += (_, _) => RunWorkOrderExport(owner, exportService, order, ExportFormat.Excel, baseName, signatureOption);

        ToolStripMenuItem mPdf = new("Orden en PDF (.pdf)", IconCatalog.Get(AppIcon.FileText, CambiasoTheme.Danger, 16));
        mPdf.Click += (_, _) => RunWorkOrderExport(owner, exportService, order, ExportFormat.Pdf, baseName, signatureOption);

        ToolStripMenuItem mWord = new("Orden en Word (.docx)", IconCatalog.Get(AppIcon.FileText, CambiasoTheme.Primary, 16));
        mWord.Click += (_, _) => RunWorkOrderExport(owner, exportService, order, ExportFormat.Word, baseName, signatureOption);

        ToolStripMenuItem mCsv = new("Ítems en CSV (.csv)", IconCatalog.Get(AppIcon.Download, CambiasoTheme.Accent, 16));
        mCsv.Click += (_, _) => RunWorkOrderExport(owner, exportService, order, ExportFormat.Csv, baseName, signatureOption);

        menu.Items.AddRange([mExcel, mPdf, mWord, new ToolStripSeparator(), mCsv]);
        menu.Closed += (_, _) => menu.Dispose();
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    private static void RunReportExport(
        IWin32Window owner,
        DocumentExportService exportService,
        ExportFormat format,
        string baseFileName,
        Func<TabularReportData> reportBuilder,
        Action<string>? onCompleted)
    {
        TabularReportData report = reportBuilder();
        if (report.Rows.Count == 0)
        {
            CambiasoTheme.ShowWarning(owner, "No hay registros visibles para exportar con los filtros actuales.");
            return;
        }

        (string ext, string filter) = GetFileDialogSpec(format);
        using SaveFileDialog sfd = new()
        {
            Filter = filter,
            FileName = $"{baseFileName}_{DateTime.Now:yyyyMMdd_HHmm}.{ext}"
        };

        if (sfd.ShowDialog(owner) != DialogResult.OK)
        {
            return;
        }

        try
        {
            exportService.ExportReport(report, format, sfd.FileName);
            onCompleted?.Invoke(sfd.FileName);
            CambiasoTheme.ShowInfo(owner, $"Reporte exportado correctamente ({report.Rows.Count:N0} registros):\n{sfd.FileName}");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(owner, $"No se pudo exportar el archivo: {ex.Message}");
        }
    }

    private static void RunWorkOrderExport(
        IWin32Window owner,
        DocumentExportService exportService,
        WorkOrder order,
        ExportFormat format,
        string baseFileName,
        SignatureOption signatureOption)
    {
        (string ext, string filter) = GetFileDialogSpec(format);
        using SaveFileDialog sfd = new()
        {
            Filter = filter,
            FileName = $"{baseFileName}.{ext}"
        };

        if (sfd.ShowDialog(owner) != DialogResult.OK)
        {
            return;
        }

        try
        {
            exportService.ExportWorkOrder(order, format, sfd.FileName, signatureOption);
            CambiasoTheme.ShowInfo(owner, $"Orden de Trabajo N° {order.NumeroOt} exportada correctamente:\n{sfd.FileName}");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(owner, $"No se pudo exportar la OT: {ex.Message}");
        }
    }

    private static (string Ext, string Filter) GetFileDialogSpec(ExportFormat format) => format switch
    {
        ExportFormat.Excel => ("xlsx", "Libro Excel (*.xlsx)|*.xlsx"),
        ExportFormat.Pdf => ("pdf", "Documento PDF (*.pdf)|*.pdf"),
        ExportFormat.Word => ("docx", "Documento Word (*.docx)|*.docx"),
        ExportFormat.Csv => ("csv", "Archivo CSV UTF-8 (*.csv)|*.csv"),
        _ => ("xlsx", "Libro Excel (*.xlsx)|*.xlsx")
    };
}
