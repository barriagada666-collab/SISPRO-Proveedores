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

    public static void ShowSelectedWorkOrdersExportMenu(
        Control anchor,
        IWin32Window owner,
        DocumentExportService exportService,
        Func<IReadOnlyList<WorkOrder>> ordersLoader,
        Action<string, int>? onCompleted = null)
    {
        ContextMenuHelper.ShowBelow(anchor,
        [
            ContextMenuHelper.Item("Exportar seleccionadas a Excel (.xlsx)", AppIcon.Excel, CambiasoTheme.Success,
                () => RunSelectedWorkOrdersExport(owner, exportService, ExportFormat.Excel, ordersLoader, onCompleted)),
            ContextMenuHelper.Item("Exportar seleccionadas a CSV (.csv)", AppIcon.Download, CambiasoTheme.Accent,
                () => RunSelectedWorkOrdersExport(owner, exportService, ExportFormat.Csv, ordersLoader, onCompleted)),
            ContextMenuHelper.Item("Exportar seleccionadas a PDF (.pdf)", AppIcon.FileText, CambiasoTheme.Danger,
                () => RunSelectedWorkOrdersExport(owner, exportService, ExportFormat.Pdf, ordersLoader, onCompleted))
        ]);
    }

    private static void RunSelectedWorkOrdersExport(
        IWin32Window owner,
        DocumentExportService exportService,
        ExportFormat format,
        Func<IReadOnlyList<WorkOrder>> ordersLoader,
        Action<string, int>? onCompleted)
    {
        IReadOnlyList<WorkOrder> orders = ordersLoader();
        List<WorkOrder> distinctOrders = orders
            .Where(o => o.IdOt > 0 || o.NumeroOt > 0)
            .GroupBy(o => o.IdOt > 0 ? o.IdOt : o.NumeroOt)
            .Select(g => g.First())
            .ToList();

        if (distinctOrders.Count == 0)
        {
            CambiasoTheme.ShowWarning(owner, "Debe seleccionar al menos una orden de trabajo antes de exportar.");
            return;
        }

        string formatLabel = format switch
        {
            ExportFormat.Excel => "Excel (.xlsx)",
            ExportFormat.Csv => "CSV (.csv)",
            ExportFormat.Pdf => "PDF (.pdf)",
            _ => format.ToString()
        };

        if (!CambiasoTheme.Confirm(
                owner,
                $"Se exportarán exactamente {distinctOrders.Count:N0} orden(es) de trabajo seleccionada(s) a {formatLabel}.\n\n¿Desea continuar?"))
        {
            return;
        }

        (string ext, string filter) = GetFileDialogSpec(format);
        using SaveFileDialog sfd = new()
        {
            Filter = filter,
            FileName = $"OT_Seleccionadas_{DateTime.Today:yyyy-MM-dd}.{ext}"
        };

        if (sfd.ShowDialog(owner) != DialogResult.OK)
        {
            return;
        }

        try
        {
            TabularReportData report = DocumentExportService.BuildSelectedWorkOrdersReport(
                distinctOrders,
                $"OT seleccionadas: {distinctOrders.Count:N0} orden(es) única(s)");
            exportService.ExportReport(report, format, sfd.FileName);
            onCompleted?.Invoke(sfd.FileName, distinctOrders.Count);
            CambiasoTheme.ShowInfo(owner, $"Se exportaron {distinctOrders.Count:N0} orden(es) seleccionada(s):\n{sfd.FileName}");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(owner, $"No se pudo exportar la selección de OTs: {ex.Message}");
        }
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
