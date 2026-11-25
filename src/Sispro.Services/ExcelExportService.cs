using System.Globalization;
using ClosedXML.Excel;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Services;

/// <summary>
/// Exportación profesional a Excel (.xlsx) usando ClosedXML (sin dependencia de Microsoft Office COM),
/// con identidad corporativa Cambiaso Hnos S.A.C., logotipo, filtros, fórmulas de totales y pie de página.
/// </summary>
public sealed class ExcelExportService(AppSettings settings)
{
    private static readonly CultureInfo ClCulture = new("es-CL");
    private static readonly XLColor CambiasoNavy = XLColor.FromHtml("#0A263C");
    private static readonly XLColor CambiasoOlive = XLColor.FromHtml("#0E4163");
    private static readonly XLColor CambiasoGold = XLColor.FromHtml("#C8A977");
    private static readonly XLColor CambiasoLight = XLColor.FromHtml("#EBF4FA");
    private static readonly XLColor TotalFill = XLColor.FromHtml("#E0ECF5");
    private static readonly XLColor BorderGray = XLColor.FromHtml("#B8CEDE");

    public void ExportTabularReport(
        TabularReportData report,
        string outputPath,
        string? assetsDirectory = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        string safeSheet = SanitizeSheetName(report.SheetName);

        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add(safeSheet);
        ws.PageSetup.PageOrientation = report.Landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Footer.Left.AddText($"SISPRO — {settings.CompanyName} · {settings.CompanyDepartment}");
        ws.PageSetup.Footer.Right.AddText("Página &P de &N");

        int colCount = Math.Max(1, report.Columns.Count);

        // Banner corporativo (Filas 1 a 3)
        IXLRange banner = ws.Range(1, 1, 3, colCount);
        banner.Style.Fill.BackgroundColor = CambiasoNavy;
        banner.Style.Border.BottomBorder = XLBorderStyleValues.Thick;
        banner.Style.Border.BottomBorderColor = CambiasoGold;
        ws.Row(1).Height = 24;
        ws.Row(2).Height = 19;
        ws.Row(3).Height = 18;

        int textStartCol = 1;
        string logoPath = Path.Combine(assetsDir, "logo_cambiaso.png");
        if (File.Exists(logoPath) && colCount >= 3)
        {
            ws.AddPicture(logoPath)
              .MoveTo(ws.Cell(1, 1), 6, 6)
              .WithSize(112, 52);
            textStartCol = 2;
        }

        IXLRange titleRange = ws.Range(1, textStartCol, 1, colCount).Merge();
        titleRange.Value = ExportSanitizer.SanitizeXmlText(report.Title);
        titleRange.Style.Font.FontName = "Segoe UI";
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 13;
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        IXLRange subRange = ws.Range(2, textStartCol, 2, colCount).Merge();
        subRange.Value = string.IsNullOrWhiteSpace(report.Subtitle)
            ? $"{settings.CompanyName} — {settings.CompanyDepartment}"
            : report.Subtitle;
        subRange.Style.Font.FontName = "Segoe UI";
        subRange.Style.Font.FontSize = 9.5;
        subRange.Style.Font.FontColor = CambiasoGold;

        IXLRange metaRange = ws.Range(3, textStartCol, 3, colCount).Merge();
        metaRange.Value = ExportSanitizer.SanitizeXmlText($"Emitido: {DateTime.Now:dd-MM-yyyy HH:mm}  |  Registros: {report.Rows.Count.ToString("N0", ClCulture)}  |  Filtros: {report.FilterSummary}");
        metaRange.Style.Font.FontName = "Segoe UI";
        metaRange.Style.Font.FontSize = 8.5;
        metaRange.Style.Font.FontColor = XLColor.FromHtml("#D4E3EE");

        // Fila 4: Separador fino
        ws.Row(4).Height = 6;

        // Fila 5: Encabezados de tabla
        const int headerRow = 5;
        for (int c = 0; c < report.Columns.Count; c++)
        {
            ws.Cell(headerRow, c + 1).Value = ExportSanitizer.SanitizeXmlText(report.Columns[c].Header);
        }

        IXLRange tblHeader = ws.Range(headerRow, 1, headerRow, colCount);
        tblHeader.Style.Fill.BackgroundColor = CambiasoOlive;
        tblHeader.Style.Font.FontName = "Segoe UI";
        tblHeader.Style.Font.FontColor = XLColor.White;
        tblHeader.Style.Font.Bold = true;
        tblHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        tblHeader.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(headerRow).Height = 24;
        ws.SheetView.FreezeRows(headerRow);
        ws.PageSetup.SetRowsToRepeatAtTop(1, headerRow);

        // Filas de datos
        int total = Math.Max(1, report.Rows.Count);
        int firstDataRow = headerRow + 1;
        for (int i = 0; i < report.Rows.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object?[] data = report.Rows[i];
            int r = firstDataRow + i;

            for (int c = 0; c < report.Columns.Count; c++)
            {
                object? val = c < data.Length ? data[c] : null;
                IXLCell cell = ws.Cell(r, c + 1);
                WriteTypedCell(cell, val, report.Columns[c].Kind);
            }

            if (i % 2 == 1)
            {
                ws.Range(r, 1, r, colCount).Style.Fill.BackgroundColor = CambiasoLight;
            }

            if (progress is not null && (i % 25 == 0 || i == report.Rows.Count - 1))
            {
                progress.Report((int)((i + 1) * 100.0 / total));
            }
        }

        int lastDataRow = firstDataRow + report.Rows.Count - 1;
        int lastTableRow = Math.Max(headerRow, lastDataRow);

        // Fila de resumen con fórmulas Excel reales (=SUM / =COUNTA)
        bool hasSums = report.Columns.Any(c => c.SumInFooter);
        if (report.Rows.Count > 0 && hasSums)
        {
            int summaryRow = lastDataRow + 1;
            ws.Cell(summaryRow, 1).Value = $"TOTAL ({report.Rows.Count.ToString("N0", ClCulture)})";
            for (int c = 0; c < report.Columns.Count; c++)
            {
                if (report.Columns[c].SumInFooter)
                {
                    string colLetter = XLHelper.GetColumnLetterFromNumber(c + 1);
                    IXLCell sumCell = ws.Cell(summaryRow, c + 1);
                    sumCell.FormulaA1 = $"SUM({colLetter}{firstDataRow}:{colLetter}{lastDataRow})";
                    sumCell.Style.NumberFormat.Format = report.Columns[c].Kind == ExportColumnKind.Currency ? "$#,##0" : "#,##0.##";
                }
            }

            IXLRange sumRange = ws.Range(summaryRow, 1, summaryRow, colCount);
            sumRange.Style.Fill.BackgroundColor = TotalFill;
            sumRange.Style.Font.Bold = true;
            sumRange.Style.Font.FontColor = CambiasoNavy;
            sumRange.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            sumRange.Style.Border.TopBorderColor = CambiasoOlive;
            ws.Row(summaryRow).Height = 22;
            lastTableRow = summaryRow;
        }

        // Bordes, AutoFilter y ajuste de anchos
        IXLRange dataRange = ws.Range(headerRow, 1, lastTableRow, colCount);
        dataRange.Style.Font.FontName = "Segoe UI";
        dataRange.Style.Font.FontSize = 9.5;
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.OutsideBorderColor = CambiasoOlive;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        dataRange.Style.Border.InsideBorderColor = BorderGray;
        ws.Range(headerRow, 1, Math.Max(headerRow, lastDataRow), colCount).SetAutoFilter();

        ws.Columns(1, colCount).AdjustToContents(headerRow, Math.Min(lastTableRow, headerRow + 250), 11, 54);
        if (File.Exists(logoPath) && colCount >= 3 && ws.Column(1).Width < 17)
        {
            ws.Column(1).Width = 17;
        }

        wb.SaveAs(outputPath);
    }

    public void ExportWorkOrderDocument(
        WorkOrder order,
        string outputPath,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        ExportSanitizer.EnsureOutputDirectory(outputPath);
        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");

        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Orden de Trabajo");
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Footer.Left.AddText($"SISPRO — {settings.CompanyName} · {settings.CompanyDepartment}");
        ws.PageSetup.Footer.Right.AddText("Página &P de &N");

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 38;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 24;
        ws.Column(6).Width = 24;

        // Corporate Logo
        string logoPath = Path.Combine(assetsDir, "logo_cambiaso.png");
        if (File.Exists(logoPath))
        {
            ws.AddPicture(logoPath)
              .MoveTo(ws.Cell(1, 1), 6, 4)
              .WithSize(165, 84);
        }
        else
        {
            ws.Range(1, 1, 5, 3).Merge();
            ws.Cell(1, 1).Value = settings.CompanyName;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Font.FontColor = CambiasoOlive;
            ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // Caja superior derecha con N° de OT grande y destacado
        IXLRange headerBox = ws.Range(1, 4, 5, 6);
        headerBox.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        headerBox.Style.Border.OutsideBorderColor = CambiasoNavy;

        ws.Range(1, 4, 1, 6).Merge().Value = ExportSanitizer.SanitizeXmlText($"RUT: {settings.CompanyRut}  ·  {settings.CompanyDepartment}");
        ws.Range(2, 4, 2, 6).Merge().Value = "ORDEN DE TRABAJO EXTERNA";
        ws.Range(3, 4, 4, 6).Merge().Value = $"OT N° {order.NumeroOt:D6}";
        ws.Range(5, 4, 5, 6).Merge().Value = $"FECHA OT: {order.FechaOt:dd-MM-yyyy}";

        for (int r = 1; r <= 5; r++)
        {
            ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Cell(r, 4).Style.Font.Bold = true;
        }

        ws.Cell(1, 4).Style.Font.FontSize = 9;
        ws.Cell(2, 4).Style.Fill.BackgroundColor = CambiasoOlive;
        ws.Cell(2, 4).Style.Font.FontColor = XLColor.White;
        ws.Cell(2, 4).Style.Font.FontSize = 11;
        ws.Range(3, 4, 4, 6).Style.Fill.BackgroundColor = CambiasoGold;
        ws.Range(3, 4, 4, 6).Style.Font.FontColor = CambiasoNavy;
        ws.Range(3, 4, 4, 6).Style.Font.FontSize = 20;
        ws.Range(3, 4, 4, 6).Style.Border.TopBorder = XLBorderStyleValues.Thin;
        ws.Range(3, 4, 4, 6).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        ws.Range(3, 4, 4, 6).Style.Border.TopBorderColor = CambiasoNavy;
        ws.Range(3, 4, 4, 6).Style.Border.BottomBorderColor = CambiasoNavy;
        ws.Row(3).Height = 22;
        ws.Row(4).Height = 22;
        ws.Cell(5, 4).Style.Font.FontSize = 10;

        // Bloque Proveedor & Solicitante (Activo ubicado en Datos del Solicitante)
        ws.Range(7, 1, 7, 3).Merge().Value = "DATOS DEL PROVEEDOR";
        ws.Range(7, 4, 7, 6).Merge().Value = "DATOS DEL SOLICITANTE";
        foreach (int col in new[] { 1, 4 })
        {
            ws.Cell(7, col).Style.Fill.BackgroundColor = CambiasoOlive;
            ws.Cell(7, col).Style.Font.FontColor = XLColor.White;
            ws.Cell(7, col).Style.Font.Bold = true;
        }

        ws.Cell(8, 1).Value = "SEÑOR(ES):";
        ws.Range(8, 2, 8, 3).Merge().Value = ExportSanitizer.SanitizeXmlText(order.NombreProveedor);
        ws.Cell(9, 1).Value = "DIRECCIÓN:";
        ws.Range(9, 2, 9, 3).Merge().Value = ExportSanitizer.SanitizeXmlText(order.DireccionProveedor);
        ws.Cell(10, 1).Value = "CORREO:";
        ws.Range(10, 2, 10, 3).Merge().Value = ExportSanitizer.SanitizeXmlText(order.CorreoProveedor);
        ws.Range(11, 1, 11, 3).Merge().Value = string.Empty;

        ws.Cell(8, 4).Value = "SOLICITADO POR:";
        ws.Range(8, 5, 8, 6).Merge().Value = ExportSanitizer.SanitizeXmlText(order.SolicitanteDisplayNombre);
        ws.Cell(9, 4).Value = "CORREO:";
        ws.Range(9, 5, 9, 6).Merge().Value = ExportSanitizer.SanitizeXmlText(order.CorreoSolicitante);
        ws.Cell(10, 4).Value = "TELÉFONO:";
        ws.Range(10, 5, 10, 6).Merge().Value = ExportSanitizer.SanitizeXmlText(order.TelefonoSolicitante);
        ws.Cell(11, 4).Value = "ACTIVO:";
        ws.Range(11, 5, 11, 6).Merge().Value = ExportSanitizer.SanitizeXmlText(order.RutaActivo);

        for (int r = 8; r <= 11; r++)
        {
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 4).Style.Font.Bold = true;
            ws.Range(r, 2, r, 3).Style.Alignment.WrapText = true;
            ws.Range(r, 5, r, 6).Style.Alignment.WrapText = true;
            ws.Range(r, 1, r, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            int leftLines = Math.Max(1, (int)Math.Ceiling(ws.Cell(r, 2).GetString().Length / 44.0));
            int rightLines = Math.Max(1, (int)Math.Ceiling(ws.Cell(r, 5).GetString().Length / 40.0));
            ws.Row(r).Height = Math.Max(20, Math.Max(leftLines, rightLines) * 16);
        }

        IXLRange infoBox = ws.Range(7, 1, 11, 6);
        infoBox.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        infoBox.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        infoBox.Style.Border.OutsideBorderColor = CambiasoOlive;

        // Tabla de detalle sin columna Estado / Guía / Valor
        int headerRow = 13;
        ws.Cell(headerRow, 1).Value = "Ítem";
        ws.Cell(headerRow, 2).Value = "Cant.";
        ws.Range(headerRow, 3, headerRow, 6).Merge().Value = "DETALLE DE PIEZA EN REPARACIÓN Y TRABAJO";

        IXLRange tblHeader = ws.Range(headerRow, 1, headerRow, 6);
        tblHeader.Style.Fill.BackgroundColor = CambiasoOlive;
        tblHeader.Style.Font.FontColor = XLColor.White;
        tblHeader.Style.Font.Bold = true;
        tblHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(headerRow).Height = 24;

        int row = headerRow + 1;
        int itemNum = 1;
        foreach (WorkOrderDetail d in order.Detalles)
        {
            ws.Cell(row, 1).Value = itemNum++;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 2).Value = d.Cantidad;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            IXLRange descRange = ws.Range(row, 3, row, 6).Merge();
            descRange.Value = ExportSanitizer.SanitizeXmlText(d.Descripcion);
            descRange.Style.Alignment.WrapText = true;

            int lines = Math.Max(1, (int)Math.Ceiling(d.Descripcion.Length / 92.0));
            ws.Row(row).Height = Math.Max(22, lines * 16);

            if (row % 2 == 1)
            {
                ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = CambiasoLight;
            }

            row++;
        }

        while (row < headerRow + 10)
        {
            ws.Range(row, 3, row, 6).Merge();
            ws.Row(row).Height = 20;
            row++;
        }

        IXLRange itemsBox = ws.Range(headerRow, 1, row - 1, 6);
        itemsBox.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        itemsBox.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        itemsBox.Style.Border.InsideBorderColor = BorderGray;
        itemsBox.Style.Border.OutsideBorderColor = CambiasoOlive;
        itemsBox.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Bloque de Observaciones y Valor Neto destacado
        int obsRow = row + 1;
        string obsText = string.IsNullOrWhiteSpace(order.Observacion) ? "Sin observaciones." : order.Observacion;
        IXLRange obsRange = ws.Range(obsRow, 1, obsRow, 4).Merge();
        obsRange.Value = ExportSanitizer.SanitizeXmlText($"OBSERVACIONES: {obsText}");
        obsRange.Style.Alignment.WrapText = true;
        obsRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        obsRange.Style.Fill.BackgroundColor = CambiasoLight;
        obsRange.Style.Font.FontSize = 9.5;

        ws.Cell(obsRow, 5).Value = "VALOR NETO:";
        ws.Cell(obsRow, 5).Style.Font.Bold = true;
        ws.Cell(obsRow, 5).Style.Font.FontColor = CambiasoNavy;
        ws.Cell(obsRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(obsRow, 5).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Cell(obsRow, 5).Style.Fill.BackgroundColor = CambiasoGold;

        ws.Cell(obsRow, 6).Value = order.ValorNeto;
        ws.Cell(obsRow, 6).Style.NumberFormat.Format = "$#,##0";
        ws.Cell(obsRow, 6).Style.Font.Bold = true;
        ws.Cell(obsRow, 6).Style.Font.FontSize = 13;
        ws.Cell(obsRow, 6).Style.Font.FontColor = CambiasoNavy;
        ws.Cell(obsRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(obsRow, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Cell(obsRow, 6).Style.Fill.BackgroundColor = CambiasoGold;

        IXLRange summaryBox = ws.Range(obsRow, 1, obsRow, 6);
        summaryBox.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        summaryBox.Style.Border.OutsideBorderColor = CambiasoOlive;
        int obsLines = Math.Max(1, (int)Math.Ceiling(obsText.Length / 68.0));
        ws.Row(obsRow).Height = Math.Max(26, obsLines * 16);

        // Pie dinámico de firmas (si no hay ninguna seleccionada, el pie queda vacío)
        List<WorkOrderSignatureSlot> slots = WorkOrderSignatureLayout.BuildSlots(order, signatureOption, assetsDir);
        if (slots.Count > 0)
        {
            int sigStartRow = obsRow + 2;
            int lineRow = sigStartRow + 5;
            (int StartCol, int EndCol)[] spans = slots.Count switch
            {
                1 => [(2, 5)],
                2 => [(1, 3), (4, 6)],
                _ => [(1, 2), (3, 4), (5, 6)]
            };

            for (int i = 0; i < slots.Count; i++)
            {
                WorkOrderSignatureSlot slot = slots[i];
                (int c1, int c2) = spans[i];
                int picCol = c1 == 1 && c2 == 3 ? 2 : (c1 == 4 && c2 == 6 ? 5 : c1);

                if (!string.IsNullOrEmpty(slot.AssetImagePath) && File.Exists(slot.AssetImagePath))
                {
                    ws.AddPicture(slot.AssetImagePath)
                      .MoveTo(ws.Cell(sigStartRow, picCol), 8, 4)
                      .WithSize(120, 78);
                }
                else if (slot.ImageBytes is { Length: > 0 })
                {
                    using MemoryStream sigMs = new(slot.ImageBytes);
                    ws.AddPicture(sigMs)
                      .MoveTo(ws.Cell(sigStartRow, picCol), 8, 4)
                      .WithSize(125, 72);
                }

                ws.Range(lineRow, c1, lineRow, c2).Merge().Value = "__________________________";
                ws.Range(lineRow + 1, c1, lineRow + 1, c2).Merge().Value = ExportSanitizer.SanitizeXmlText(slot.SignerName);
                ws.Range(lineRow + 2, c1, lineRow + 2, c2).Merge().Value = ExportSanitizer.SanitizeXmlText(slot.RoleTitle);
                ws.Range(lineRow, c1, lineRow + 2, c2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(lineRow + 1, c1, lineRow + 1, c2).Style.Font.Bold = true;
                ws.Range(lineRow + 2, c1, lineRow + 2, c2).Style.Font.FontColor = XLColor.DimGray;
            }
        }

        wb.SaveAs(outputPath);
    }

    public Task ExportWorkOrdersGridAsync(
        IReadOnlyList<WorkOrderSearchRow> rows,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default) =>
        ExportWorkOrdersGridAsync(rows, outputPath, filterSummary: null, progress, cancellationToken);

    public async Task ExportWorkOrdersGridAsync(
        IReadOnlyList<WorkOrderSearchRow> rows,
        string outputPath,
        string? filterSummary,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        TabularReportData report = DocumentExportService.BuildWorkOrdersReport(rows, filterSummary);
        await Task.Run(() => ExportTabularReport(report, outputPath, progress: progress, cancellationToken: cancellationToken), cancellationToken);
    }

    private static void WriteTypedCell(IXLCell cell, object? val, ExportColumnKind kind)
    {
        if (val is null)
        {
            cell.Value = string.Empty;
            return;
        }

        switch (kind)
        {
            case ExportColumnKind.Date when val is DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd-MM-yyyy";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                break;

            case ExportColumnKind.DateTime when val is DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd-MM-yyyy HH:mm";
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                break;

            case ExportColumnKind.Currency when val is decimal dec:
                cell.Value = dec;
                cell.Style.NumberFormat.Format = "$#,##0";
                break;

            case ExportColumnKind.Currency when val is double dbl:
                cell.Value = dbl;
                cell.Style.NumberFormat.Format = "$#,##0";
                break;

            case ExportColumnKind.Integer when val is int i:
                cell.Value = i;
                cell.Style.NumberFormat.Format = "#,##0";
                break;

            case ExportColumnKind.Integer when val is long l:
                cell.Value = l;
                cell.Style.NumberFormat.Format = "#,##0";
                break;

            case ExportColumnKind.Decimal when val is double dbl:
                cell.Value = dbl;
                cell.Style.NumberFormat.Format = "#,##0.##";
                break;

            case ExportColumnKind.Decimal when val is decimal dec:
                cell.Value = dec;
                cell.Style.NumberFormat.Format = "#,##0.##";
                break;

            default:
                cell.Value = ExportSanitizer.SanitizeXmlText(Convert.ToString(val, CultureInfo.InvariantCulture));
                break;
        }
    }

    private static string SanitizeSheetName(string name)
    {
        string cleaned = new(name.Where(ch => !":\\/?*[]".Contains(ch)).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "Reporte";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
