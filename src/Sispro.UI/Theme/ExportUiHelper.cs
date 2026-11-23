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
        ContextMenuHelper.ShowBelow(anchor,
        [
            ContextMenuHelper.Item("Exportar a Excel (.xlsx)", AppIcon.Excel, CambiasoTheme.Success,
                () => RunReportExport(owner, exportService, ExportFormat.Excel, baseFileName, reportBuilder, onCompleted)),
            ContextMenuHelper.Item("Exportar a PDF (.pdf)", AppIcon.FileText, CambiasoTheme.Danger,
                () => RunReportExport(owner, exportService, ExportFormat.Pdf, baseFileName, reportBuilder, onCompleted)),
            ContextMenuHelper.Item("Exportar a Word (.docx)", AppIcon.FileText, CambiasoTheme.Primary,
                () => RunReportExport(owner, exportService, ExportFormat.Word, baseFileName, reportBuilder, onCompleted)),
            ContextMenuHelper.Separator(),
            ContextMenuHelper.Item("Exportar a CSV (.csv)", AppIcon.Download, CambiasoTheme.Accent,
                () => RunReportExport(owner, exportService, ExportFormat.Csv, baseFileName, reportBuilder, onCompleted))
        ]);
    }

    public static void ShowWorkOrderExportMenu(
        Control anchor,
        IWin32Window owner,
        DocumentExportService exportService,
        WorkOrder order,
        SignatureOption signatureOption)
    {
        string baseName = $"OT_{order.NumeroOt:D6}_Cambiaso";
        ContextMenuHelper.ShowBelow(anchor,
        [
            ContextMenuHelper.Item("Orden en Excel (.xlsx)", AppIcon.Excel, CambiasoTheme.Success,
                () => RunWorkOrderExport(owner, exportService, order, ExportFormat.Excel, baseName, signatureOption)),
            ContextMenuHelper.Item("Orden en PDF (.pdf)", AppIcon.FileText, CambiasoTheme.Danger,
                () => RunWorkOrderExport(owner, exportService, order, ExportFormat.Pdf, baseName, signatureOption)),
            ContextMenuHelper.Item("Orden en Word (.docx)", AppIcon.FileText, CambiasoTheme.Primary,
                () => RunWorkOrderExport(owner, exportService, order, ExportFormat.Word, baseName, signatureOption)),
            ContextMenuHelper.Separator(),
            ContextMenuHelper.Item("Ítems en CSV (.csv)", AppIcon.Download, CambiasoTheme.Accent,
                () => RunWorkOrderExport(owner, exportService, order, ExportFormat.Csv, baseName, signatureOption))
        ]);
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
