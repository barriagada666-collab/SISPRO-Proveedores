using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Services;

/// <summary>
/// Generador vectorial nativo de documentos PDF 1.4 corporativos Cambiaso Hnos S.A.C.
/// Soporta encabezados con logotipo institucional, tablas multipágina, márgenes, numeración
/// ("Página X de Y"), totales, acentos en español (WinAnsiEncoding) y firma de OT.
/// </summary>
public sealed class PdfExportService(AppSettings settings)
{
    private static readonly Encoding WinAnsi = ExportSanitizer.WinAnsi1252;
    private static readonly CultureInfo ClCulture = new("es-CL");

    // Paleta corporativa Cambiaso normalizada (0..1)
    private const string PrimaryDarkFill = "0.039 0.149 0.235 rg";   // #0A263C
    private const string PrimaryFill = "0.055 0.255 0.388 rg";       // #0E4163
    private const string AccentStroke = "0.000 0.537 0.714 RG";      // #0089B6
    private const string GoldFill = "0.784 0.663 0.467 rg";          // #C8A977
    private const string SubtleRowFill = "0.922 0.957 0.980 rg";     // #EBF4FA
    private const string TotalRowFill = "0.878 0.925 0.961 rg";      // #E0ECF5
    private const string BorderStroke = "0.773 0.839 0.886 RG";      // #C5D6E2
    private const string TextPrimaryFill = "0.051 0.133 0.208 rg";   // #0D2235
    private const string TextMutedFill = "0.290 0.384 0.471 rg";     // #4A6278
    private const string WhiteFill = "1 1 1 rg";

    public void ExportTabularReport(TabularReportData report, string outputPath, string? assetsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        PdfImage? logo = TryLoadJpegImage(Path.Combine(assetsDir, "logo_cambiaso.png"), Color.FromArgb(10, 38, 60));

        // Carta US: 612 x 792 pt (Horizontal: 792 x 612 pt)
        double pageW = report.Landscape ? 792 : 612;
        double pageH = report.Landscape ? 612 : 792;
        double marginX = 32;
        double marginTop = 28;
        double marginBottom = 36;
        double contentW = pageW - (marginX * 2);

        double totalWeight = Math.Max(1.0, report.Columns.Sum(c => Math.Max(0.3, c.WidthWeight)));
        double[] colWidths = report.Columns.Select(c => contentW * (Math.Max(0.3, c.WidthWeight) / totalWeight)).ToArray();

        double headerBannerH = 66;
        double tableHeaderH = 22;
        double rowH = 18;
        double bodyTopY = pageH - marginTop - headerBannerH - 18;
        double bodyBottomY = marginBottom + 24;

        bool hasTotals = report.Columns.Any(c => c.SumInFooter) && report.Rows.Count > 0;
        int totalRowsCount = report.Rows.Count + (hasTotals ? 1 : 0);
        int rowsPerPage = Math.Max(1, (int)Math.Floor((bodyTopY - tableHeaderH - bodyBottomY) / rowH));
        int totalPages = Math.Max(1, (int)Math.Ceiling(Math.Max(1, totalRowsCount) / (double)rowsPerPage));

        decimal[] sums = new decimal[report.Columns.Count];
        if (hasTotals)
        {
            foreach (object?[] r in report.Rows)
            {
                for (int c = 0; c < report.Columns.Count && c < r.Length; c++)
                {
                    if (report.Columns[c].SumInFooter && ExportSanitizer.TryToDecimal(r[c], out decimal d))
                    {
                        sums[c] += d;
                    }
                }
            }
        }

        List<string> pageStreams = [];
        string emissionText = $"Emitido: {DateTime.Now:dd-MM-yyyy HH:mm}  |  Registros: {report.Rows.Count:N0}";
        string filterLine = $"Filtros: {TruncateText(report.FilterSummary, contentW - 10, 8)}";

        for (int p = 0; p < totalPages; p++)
        {
            StringBuilder sb = new();

            // 1. Banner corporativo superior
            DrawCorporateHeader(sb, pageW, pageH, marginX, marginTop, headerBannerH, contentW, report.Title, report.Subtitle, emissionText, logo is not null);

            // Línea de filtros
            sb.AppendLine(TextMutedFill);
            DrawText(sb, "F1", 8, marginX, pageH - marginTop - headerBannerH - 11, filterLine);

            // 2. Encabezado de tabla
            double curY = bodyTopY;
            sb.AppendLine(PrimaryFill);
            sb.AppendLine(Fmt($"{marginX} {curY - tableHeaderH} {contentW} {tableHeaderH} re f"));

            double x = marginX;
            sb.AppendLine(WhiteFill);
            for (int c = 0; c < report.Columns.Count; c++)
            {
                string header = TruncateText(report.Columns[c].Header, colWidths[c] - 8, 8.2);
                DrawText(sb, "F2", 8.2, x + 4, curY - 14.5, header);
                x += colWidths[c];
            }

            curY -= tableHeaderH;

            // 3. Filas de datos en esta página
            int startIdx = p * rowsPerPage;
            int endIdx = Math.Min(report.Rows.Count, startIdx + rowsPerPage);

            for (int i = startIdx; i < endIdx; i++)
            {
                object?[] row = report.Rows[i];
                if ((i - startIdx) % 2 == 1)
                {
                    sb.AppendLine(SubtleRowFill);
                    sb.AppendLine(Fmt($"{marginX} {curY - rowH} {contentW} {rowH} re f"));
                }

                // Borde inferior sutil
                sb.AppendLine("0.4 w");
                sb.AppendLine(BorderStroke);
                sb.AppendLine(Fmt($"{marginX} {curY - rowH} m {marginX + contentW} {curY - rowH} l S"));

                x = marginX;
                sb.AppendLine(TextPrimaryFill);
                for (int c = 0; c < report.Columns.Count; c++)
                {
                    object? raw = c < row.Length ? row[c] : null;
                    string text = FormatDisplayValue(raw, report.Columns[c].Kind);
                    string clipped = TruncateText(text, colWidths[c] - 8, 7.8);
                    bool rightAlign = report.Columns[c].Kind is ExportColumnKind.Integer or ExportColumnKind.Decimal or ExportColumnKind.Currency;
                    double tx = rightAlign
                        ? Math.Max(x + 4, x + colWidths[c] - 4 - EstimateTextWidth(clipped, 7.8))
                        : x + 4;
                    DrawText(sb, "F1", 7.8, tx, curY - 12.5, clipped);
                    x += colWidths[c];
                }

                curY -= rowH;
            }

            // 4. Fila de totales en última página
            bool renderTotalsHere = hasTotals && p == totalPages - 1;
            if (renderTotalsHere)
            {
                sb.AppendLine(TotalRowFill);
                sb.AppendLine(Fmt($"{marginX} {curY - rowH} {contentW} {rowH} re f"));
                sb.AppendLine("0.8 w");
                sb.AppendLine(AccentStroke);
                sb.AppendLine(Fmt($"{marginX} {curY} m {marginX + contentW} {curY} l S"));

                x = marginX;
                sb.AppendLine(PrimaryDarkFill);
                for (int c = 0; c < report.Columns.Count; c++)
                {
                    string cellText = c == 0
                        ? $"TOTAL ({report.Rows.Count:N0})"
                        : (report.Columns[c].SumInFooter ? FormatDisplayValue(sums[c], report.Columns[c].Kind) : string.Empty);

                    if (!string.IsNullOrEmpty(cellText))
                    {
                        string clipped = TruncateText(cellText, colWidths[c] - 8, 8.0);
                        bool rightAlign = c > 0;
                        double tx = rightAlign
                            ? Math.Max(x + 4, x + colWidths[c] - 4 - EstimateTextWidth(clipped, 8.0))
                            : x + 4;
                        DrawText(sb, "F2", 8.0, tx, curY - 12.5, clipped);
                    }

                    x += colWidths[c];
                }

                curY -= rowH;
            }

            // Marco exterior de tabla
            sb.AppendLine("0.7 w");
            sb.AppendLine(BorderStroke);
            sb.AppendLine(Fmt($"{marginX} {curY} {contentW} {bodyTopY - curY} re S"));

            // 5. Pie de página con numeración
            DrawFooter(sb, pageW, marginX, marginBottom, p + 1, totalPages, report.FooterNote);

            pageStreams.Add(sb.ToString());
        }

        List<(string Name, PdfImage Image)> images = logo is not null ? [("ImLogo", logo)] : [];
        WritePdfDocument(outputPath, pageW, pageH, pageStreams, images);
    }

    public void ExportWorkOrderDocument(
        WorkOrder order,
        string outputPath,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");

        PdfImage? logo = TryLoadJpegImage(Path.Combine(assetsDir, "logo_cambiaso.png"), Color.FromArgb(10, 38, 60));
        List<WorkOrderSignatureSlot> slots = WorkOrderSignatureLayout.BuildSlots(order, signatureOption, assetsDir);
        List<(WorkOrderSignatureSlot Slot, PdfImage? Image, string? XObjName)> slotImages = new(slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            WorkOrderSignatureSlot slot = slots[i];
            PdfImage? img = null;
            if (!string.IsNullOrEmpty(slot.AssetImagePath))
            {
                img = TryLoadJpegImage(slot.AssetImagePath, Color.White);
            }
            else if (slot.ImageBytes is { Length: > 0 })
            {
                img = TryLoadJpegFromBytes(slot.ImageBytes, Color.White);
            }

            slotImages.Add((slot, img, img is not null ? $"ImSig{i}" : null));
        }

        double pageW = 612;
        double pageH = 792;
        double marginX = 36;
        double marginTop = 32;
        double marginBottom = 36;
        double contentW = pageW - (marginX * 2);
        double halfW = (contentW - 12) / 2;
        double[] colW = [38, 48, contentW - 86];
        string[] colH = ["Ítem", "Cant.", "Detalle de Pieza en Reparación y Trabajo"];

        void DrawOtBanner(StringBuilder pageSb, bool isContinuation)
        {
            double topY = pageH - marginTop;
            double bannerH = 78;
            double bottomY = topY - bannerH;

            pageSb.AppendLine(PrimaryDarkFill);
            pageSb.AppendLine(Fmt($"{marginX} {bottomY} {contentW} {bannerH} re f"));
            pageSb.AppendLine(GoldFill);
            pageSb.AppendLine(Fmt($"{marginX} {bottomY} {contentW} 3 re f"));

            double textLeftX = marginX + 12;
            if (logo is not null)
            {
                double logoH = bannerH - 20;
                double logoW = logoH * 1.9;
                pageSb.AppendLine(Fmt($"q {logoW} 0 0 {logoH} {marginX + 10} {bottomY + 11} cm /ImLogo Do Q"));
                textLeftX = marginX + logoW + 18;
            }

            // Caja superior derecha resaltada para N° de OT
            double badgeW = 148;
            double badgeH = bannerH - 18;
            double badgeX = marginX + contentW - badgeW - 10;
            double badgeY = bottomY + 10;

            pageSb.AppendLine(GoldFill);
            pageSb.AppendLine(Fmt($"{badgeX} {badgeY} {badgeW} {badgeH} re f"));
            pageSb.AppendLine("1.2 w");
            pageSb.AppendLine(PrimaryDarkFill.Replace(" rg", " RG"));
            pageSb.AppendLine(Fmt($"{badgeX} {badgeY} {badgeW} {badgeH} re S"));

            string badgeTitle = isContinuation ? "OT N° (CONT.)" : "OT EXTERNA N°";
            double bTitleW = EstimateTextWidth(badgeTitle, 8.2);
            pageSb.AppendLine(PrimaryDarkFill);
            DrawText(pageSb, "F2", 8.2, badgeX + ((badgeW - bTitleW) / 2), badgeY + badgeH - 16, badgeTitle);

            string otNumText = order.NumeroOt.ToString("D6", CultureInfo.InvariantCulture);
            double numW = EstimateTextWidth(otNumText, 22);
            DrawText(pageSb, "F2", 22, badgeX + ((badgeW - numW) / 2), badgeY + 12, otNumText);

            double maxTitleW = badgeX - textLeftX - 12;
            string mainTitle = isContinuation ? "ORDEN DE TRABAJO EXTERNA (CONT.)" : "ORDEN DE TRABAJO EXTERNA";
            pageSb.AppendLine(WhiteFill);
            DrawText(pageSb, "F2", 12.5, textLeftX, topY - 25, TruncateText(mainTitle, maxTitleW, 12.5));

            pageSb.AppendLine(GoldFill);
            DrawText(pageSb, "F1", 8.8, textLeftX, topY - 41, TruncateText($"{settings.CompanyDepartment}  |  RUT {settings.CompanyRut}", maxTitleW, 8.8));

            pageSb.AppendLine("0.84 0.90 0.95 rg");
            DrawText(pageSb, "F1", 8.5, textLeftX, topY - 56, TruncateText($"Fecha OT: {order.FechaOt:dd-MM-yyyy}", maxTitleW, 8.5));
        }

        double DrawDetailsHeader(StringBuilder pageSb, double topY)
        {
            pageSb.AppendLine(PrimaryFill);
            pageSb.AppendLine(Fmt($"{marginX} {topY - 22} {contentW} 22 re f"));
            pageSb.AppendLine(WhiteFill);
            double cx = marginX;
            for (int i = 0; i < colH.Length; i++)
            {
                DrawText(pageSb, "F2", 8.5, cx + 6, topY - 14.5, colH[i]);
                cx += colW[i];
            }

            return topY - 22;
        }

        List<StringBuilder> pageBuilders = [];
        StringBuilder sb = new();
        pageBuilders.Add(sb);
        DrawOtBanner(sb, isContinuation: false);

        double y = pageH - marginTop - 94;

        // Bloque Proveedor & Solicitante con ajuste automático de líneas largas y Activo en Solicitante
        List<string> provLines = WrapInfoCardLines(halfW - 16, 8.2,
        [
            $"Señor(es): {order.NombreProveedor}",
            $"Dirección: {order.DireccionProveedor}",
            $"Correo: {order.CorreoProveedor}"
        ]);
        List<string> solLines = WrapInfoCardLines(halfW - 16, 8.2,
        [
            $"Solicitado por: {order.SolicitanteDisplayNombre}",
            $"Correo: {order.CorreoSolicitante}",
            $"Teléfono: {order.TelefonoSolicitante}",
            $"Activo: {order.RutaActivo}"
        ]);
        double cardH = Math.Max(88, 28 + (Math.Max(provLines.Count, solLines.Count) * 13.5));

        DrawWrappedInfoCard(sb, marginX, y, halfW, cardH, "DATOS DEL PROVEEDOR", provLines);
        DrawWrappedInfoCard(sb, marginX + halfW + 12, y, halfW, cardH, "DATOS DEL SOLICITANTE", solLines);

        y -= cardH + 14;

        double tableStart = y;
        y = DrawDetailsHeader(sb, y);
        double minTableY = marginBottom + 36;

        int itemIdx = 1;
        foreach (WorkOrderDetail d in order.Detalles)
        {
            List<string> lines = WrapText(d.Descripcion, colW[2] - 12, 8.5);
            double rh = Math.Max(20, (lines.Count * 11.5) + 8);
            if (y - rh < minTableY)
            {
                sb.AppendLine("0.7 w");
                sb.AppendLine(BorderStroke);
                sb.AppendLine(Fmt($"{marginX} {y} {contentW} {tableStart - y} re S"));

                sb = new();
                pageBuilders.Add(sb);
                DrawOtBanner(sb, isContinuation: true);
                y = pageH - marginTop - 94;
                tableStart = y;
                y = DrawDetailsHeader(sb, y);
            }

            if (itemIdx % 2 == 0)
            {
                sb.AppendLine(SubtleRowFill);
                sb.AppendLine(Fmt($"{marginX} {y - rh} {contentW} {rh} re f"));
            }

            sb.AppendLine("0.4 w");
            sb.AppendLine(BorderStroke);
            sb.AppendLine(Fmt($"{marginX} {y - rh} m {marginX + contentW} {y - rh} l S"));

            sb.AppendLine(TextPrimaryFill);
            DrawText(sb, "F1", 8.5, marginX + 12, y - 13, itemIdx.ToString(CultureInfo.InvariantCulture));
            DrawText(sb, "F1", 8.5, marginX + colW[0] + 15, y - 13, d.Cantidad.ToString(CultureInfo.InvariantCulture));

            double ly = y - 13;
            foreach (string line in lines)
            {
                DrawText(sb, "F1", 8.5, marginX + colW[0] + colW[1] + 6, ly, line);
                ly -= 11.5;
            }

            y -= rh;
            itemIdx++;
        }

        sb.AppendLine("0.7 w");
        sb.AppendLine(BorderStroke);
        sb.AppendLine(Fmt($"{marginX} {y} {contentW} {tableStart - y} re S"));

        // Bloque de Observaciones y Valor Neto destacado
        y -= 10;
        double obsBoxW = contentW - 165;
        double netBoxW = 155;
        string obsBody = string.IsNullOrWhiteSpace(order.Observacion) ? "Sin observaciones." : order.Observacion;
        List<string> obsLines = WrapText($"Observaciones: {obsBody}", obsBoxW - 14, 8.2);
        double summaryH = Math.Max(36, 12 + (obsLines.Count * 11.5));

        if (y - summaryH < marginBottom + 32)
        {
            sb = new();
            pageBuilders.Add(sb);
            DrawOtBanner(sb, isContinuation: true);
            y = pageH - marginTop - 94;
        }

        sb.AppendLine("0.98 0.99 1 rg");
        sb.AppendLine(Fmt($"{marginX} {y - summaryH} {obsBoxW} {summaryH} re f"));
        sb.AppendLine("0.6 w");
        sb.AppendLine(BorderStroke);
        sb.AppendLine(Fmt($"{marginX} {y - summaryH} {obsBoxW} {summaryH} re S"));

        sb.AppendLine(TextPrimaryFill);
        double obsY = y - 14;
        foreach (string line in obsLines)
        {
            DrawText(sb, "F1", 8.2, marginX + 7, obsY, line);
            obsY -= 11.5;
        }

        double netX = marginX + obsBoxW + 10;
        sb.AppendLine(GoldFill);
        sb.AppendLine(Fmt($"{netX} {y - summaryH} {netBoxW} {summaryH} re f"));
        sb.AppendLine("0.8 w");
        sb.AppendLine(PrimaryDarkFill.Replace(" rg", " RG"));
        sb.AppendLine(Fmt($"{netX} {y - summaryH} {netBoxW} {summaryH} re S"));

        sb.AppendLine(PrimaryDarkFill);
        DrawText(sb, "F2", 7.8, netX + 8, y - 13, "VALOR NETO");
        string netStr = $"$ {order.ValorNeto:N0}";
        DrawText(sb, "F2", 13.5, netX + 8, y - 30, TruncateText(netStr, netBoxW - 16, 13.5));

        y -= summaryH;

        // Pie dinámico de firmas: se renderizan solo las seleccionadas; si está vacío no se dibuja ninguna
        if (slotImages.Count > 0)
        {
            double requiredRoom = 135;
            double minSignatureRoomY = marginBottom + requiredRoom;
            bool sigOnNewPage = y < minSignatureRoomY;
            if (sigOnNewPage)
            {
                sb = new();
                pageBuilders.Add(sb);
                DrawOtBanner(sb, isContinuation: true);
                y = pageH - marginTop - 94;
            }

            double sigY = Math.Max(marginBottom + 38, y - 110);
            double slotW = contentW / slotImages.Count;
            double imgW = slotImages.Count == 3 ? 105 : 120;
            double imgH = slotImages.Count == 3 ? 56 : 64;
            double lineHalfW = Math.Min(78, (slotW * 0.40));

            for (int i = 0; i < slotImages.Count; i++)
            {
                (WorkOrderSignatureSlot slot, PdfImage? _, string? xObjName) = slotImages[i];
                double cx = marginX + (slotW * i) + (slotW / 2);

                if (!string.IsNullOrEmpty(xObjName))
                {
                    sb.AppendLine(Fmt($"q {imgW} 0 0 {imgH} {cx - (imgW / 2)} {sigY + 26} cm /{xObjName} Do Q"));
                }

                sb.AppendLine("0.8 w");
                sb.AppendLine(PrimaryDarkFill.Replace(" rg", " RG"));
                sb.AppendLine(Fmt($"{cx - lineHalfW} {sigY + 22} m {cx + lineHalfW} {sigY + 22} l S"));

                string nameText = TruncateText(slot.SignerName, slotW - 14, 8.2);
                double nameW = EstimateTextWidth(nameText, 8.2);
                sb.AppendLine(TextPrimaryFill);
                DrawText(pageSbSafe(sb), "F2", 8.2, cx - (nameW / 2), sigY + 10, nameText);

                string roleText = TruncateText(slot.RoleTitle, slotW - 14, 7.6);
                double roleW = EstimateTextWidth(roleText, 7.6);
                sb.AppendLine(TextMutedFill);
                DrawText(pageSbSafe(sb), "F1", 7.6, cx - (roleW / 2), sigY - 1, roleText);
            }
        }

        int totalPages = pageBuilders.Count;
        List<string> pages = new(totalPages);
        for (int p = 0; p < totalPages; p++)
        {
            DrawFooter(pageBuilders[p], pageW, marginX, marginBottom, p + 1, totalPages, $"{settings.CompanyName} · {settings.CompanyCity}");
            pages.Add(pageBuilders[p].ToString());
        }

        List<(string Name, PdfImage Image)> images = [];
        if (logo is not null) images.Add(("ImLogo", logo));
        foreach ((WorkOrderSignatureSlot _, PdfImage? img, string? xObjName) in slotImages)
        {
            if (img is not null && !string.IsNullOrEmpty(xObjName))
            {
                images.Add((xObjName, img));
            }
        }

        WritePdfDocument(outputPath, pageW, pageH, pages, images);

        static StringBuilder pageSbSafe(StringBuilder target) => target;
    }

    private static List<string> WrapInfoCardLines(double maxWidth, double fontSize, string[] entries)
    {
        List<string> wrapped = [];
        foreach (string entry in entries)
        {
            wrapped.AddRange(WrapText(entry, maxWidth, fontSize));
        }

        return wrapped;
    }

    private static void DrawWrappedInfoCard(
        StringBuilder sb,
        double x,
        double topY,
        double w,
        double h,
        string heading,
        List<string> lines)
    {
        sb.AppendLine("0.98 0.99 1 rg");
        sb.AppendLine(Fmt($"{x} {topY - h} {w} {h} re f"));
        sb.AppendLine(PrimaryFill);
        sb.AppendLine(Fmt($"{x} {topY - 18} {w} 18 re f"));
        sb.AppendLine(WhiteFill);
        DrawText(sb, "F2", 8.2, x + 8, topY - 12.5, heading);

        sb.AppendLine("0.6 w");
        sb.AppendLine(BorderStroke);
        sb.AppendLine(Fmt($"{x} {topY - h} {w} {h} re S"));

        sb.AppendLine(TextPrimaryFill);
        double ly = topY - 31;
        foreach (string line in lines)
        {
            DrawText(sb, "F1", 8.2, x + 8, ly, line);
            ly -= 13.5;
        }
    }

    private void DrawCorporateHeader(
        StringBuilder sb,
        double pageW,
        double pageH,
        double marginX,
        double marginTop,
        double bannerH,
        double contentW,
        string title,
        string subtitle,
        string rightMeta,
        bool hasLogo)
    {
        double topY = pageH - marginTop;
        double bottomY = topY - bannerH;

        // Fondo Petrol Navy
        sb.AppendLine(PrimaryDarkFill);
        sb.AppendLine(Fmt($"{marginX} {bottomY} {contentW} {bannerH} re f"));

        // Franja dorada Cambiaso inferior
        sb.AppendLine(GoldFill);
        sb.AppendLine(Fmt($"{marginX} {bottomY} {contentW} 3 re f"));

        double textLeftX = marginX + 14;
        if (hasLogo)
        {
            double logoH = bannerH - 18;
            double logoW = logoH * 1.9;
            sb.AppendLine(Fmt($"q {logoW} 0 0 {logoH} {marginX + 10} {bottomY + 10} cm /ImLogo Do Q"));
            textLeftX = marginX + logoW + 22;
        }

        double maxTitleW = (marginX + contentW) - textLeftX - 14;
        sb.AppendLine(WhiteFill);
        DrawText(sb, "F2", 13, textLeftX, topY - 24, TruncateText(title, maxTitleW, 13));

        string sub = string.IsNullOrWhiteSpace(subtitle)
            ? $"{settings.CompanyName} — {settings.CompanyDepartment}"
            : subtitle;
        sb.AppendLine(GoldFill);
        DrawText(sb, "F1", 9, textLeftX, topY - 40, TruncateText(sub, maxTitleW, 9));

        sb.AppendLine("0.82 0.89 0.94 rg");
        DrawText(sb, "F1", 8, textLeftX, topY - 54, TruncateText(rightMeta, maxTitleW, 8));
    }

    private static void DrawInfoCard(
        StringBuilder sb,
        double x,
        double topY,
        double w,
        double h,
        string heading,
        string[] lines)
    {
        sb.AppendLine("0.98 0.99 1 rg");
        sb.AppendLine(Fmt($"{x} {topY - h} {w} {h} re f"));
        sb.AppendLine(PrimaryFill);
        sb.AppendLine(Fmt($"{x} {topY - 18} {w} 18 re f"));
        sb.AppendLine(WhiteFill);
        DrawText(sb, "F2", 8.2, x + 8, topY - 12.5, heading);

        sb.AppendLine("0.6 w");
        sb.AppendLine(BorderStroke);
        sb.AppendLine(Fmt($"{x} {topY - h} {w} {h} re S"));

        sb.AppendLine(TextPrimaryFill);
        double ly = topY - 32;
        foreach (string line in lines)
        {
            DrawText(sb, "F1", 8.2, x + 8, ly, TruncateText(line, w - 16, 8.2));
            ly -= 16;
        }
    }

    private void DrawFooter(StringBuilder sb, double pageW, double marginX, double marginBottom, int pageNum, int totalPages, string footerNote)
    {
        double lineY = marginBottom + 12;
        sb.AppendLine("0.5 w");
        sb.AppendLine(BorderStroke);
        sb.AppendLine(Fmt($"{marginX} {lineY} m {pageW - marginX} {lineY} l S"));

        sb.AppendLine(TextMutedFill);
        string left = string.IsNullOrWhiteSpace(footerNote)
            ? $"SISPRO — {settings.CompanyName} · {settings.CompanyDepartment}"
            : footerNote;
        DrawText(sb, "F1", 7.8, marginX, marginBottom, TruncateText(left, pageW - (marginX * 2) - 110, 7.8));

        string pageLabel = $"Página {pageNum} de {totalPages}";
        double rightX = pageW - marginX - EstimateTextWidth(pageLabel, 7.8);
        DrawText(sb, "F2", 7.8, rightX, marginBottom, pageLabel);
    }

    private static void DrawText(StringBuilder sb, string fontKey, double fontSize, double x, double y, string text)
    {
        sb.AppendLine("BT");
        sb.AppendLine(Fmt($"/{fontKey} {fontSize} Tf"));
        sb.AppendLine(Fmt($"1 0 0 1 {x} {y} Tm"));
        sb.Append('(');
        sb.Append(EscapePdfText(text));
        sb.AppendLine(") Tj");
        sb.AppendLine("ET");
    }

    private static void WritePdfDocument(
        string outputPath,
        double pageW,
        double pageH,
        List<string> pageStreams,
        List<(string Name, PdfImage Image)> images)
    {
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Estructura de objetos PDF 1.4:
        // 1: Catalog, 2: Pages, 3: Font Helvetica (F1), 4: Font Helvetica-Bold (F2),
        // 5..(4+imgCount): Image XObjects,
        // Luego pares (Page, ContentStream) por cada página.
        List<byte[]> objects = [];

        int imgStartObj = 5;
        int firstPageObj = imgStartObj + images.Count;

        // Placeholder 1 & 2 (llenados al conocer IDs)
        objects.Add([]);
        objects.Add([]);

        // 3: F1 Helvetica (WinAnsiEncoding para tildes y ñ)
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"));

        // 4: F2 Helvetica-Bold
        objects.Add(Encoding.ASCII.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"));

        // Imágenes XObject (DCTDecode / JPEG)
        StringBuilder xObjDict = new();
        for (int i = 0; i < images.Count; i++)
        {
            (string name, PdfImage img) = images[i];
            int objId = imgStartObj + i;
            xObjDict.Append($"/{name} {objId} 0 R ");

            string header = $"<< /Type /XObject /Subtype /Image /Width {img.Width} /Height {img.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {img.JpegBytes.Length} >>\nstream\n";
            byte[] hBytes = Encoding.ASCII.GetBytes(header);
            byte[] tBytes = Encoding.ASCII.GetBytes("\nendstream");
            byte[] full = new byte[hBytes.Length + img.JpegBytes.Length + tBytes.Length];
            Buffer.BlockCopy(hBytes, 0, full, 0, hBytes.Length);
            Buffer.BlockCopy(img.JpegBytes, 0, full, hBytes.Length, img.JpegBytes.Length);
            Buffer.BlockCopy(tBytes, 0, full, hBytes.Length + img.JpegBytes.Length, tBytes.Length);
            objects.Add(full);
        }

        string xObjPart = images.Count > 0 ? $"/XObject << {xObjDict}>>" : string.Empty;
        string resources = $"<< /Font << /F1 3 0 R /F2 4 0 R >> {xObjPart} >>";

        List<int> pageObjIds = [];
        for (int i = 0; i < pageStreams.Count; i++)
        {
            int pageObjId = firstPageObj + (i * 2);
            int contentObjId = pageObjId + 1;
            pageObjIds.Add(pageObjId);

            string pageDict = Fmt($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageW} {pageH}] /Resources {resources} /Contents {contentObjId} 0 R >>");
            objects.Add(Encoding.ASCII.GetBytes(pageDict));

            byte[] streamData = WinAnsi.GetBytes(pageStreams[i]);
            byte[] sHead = Encoding.ASCII.GetBytes($"<< /Length {streamData.Length} >>\nstream\n");
            byte[] sTail = Encoding.ASCII.GetBytes("\nendstream");
            byte[] fullStream = new byte[sHead.Length + streamData.Length + sTail.Length];
            Buffer.BlockCopy(sHead, 0, fullStream, 0, sHead.Length);
            Buffer.BlockCopy(streamData, 0, fullStream, sHead.Length, streamData.Length);
            Buffer.BlockCopy(sTail, 0, fullStream, sHead.Length + streamData.Length, sTail.Length);
            objects.Add(fullStream);
        }

        objects[0] = Encoding.ASCII.GetBytes("<< /Type /Catalog /Pages 2 0 R >>");
        string kids = string.Join(" ", pageObjIds.Select(id => $"{id} 0 R"));
        objects[1] = Encoding.ASCII.GetBytes($"<< /Type /Pages /Kids [ {kids} ] /Count {pageObjIds.Count} >>");

        using FileStream fs = File.Create(outputPath);
        byte[] headerBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
        fs.Write(headerBytes);

        List<long> offsets = [0];
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(fs.Position);
            byte[] open = Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n");
            byte[] close = Encoding.ASCII.GetBytes("\nendobj\n");
            fs.Write(open);
            fs.Write(objects[i]);
            fs.Write(close);
        }

        long xrefPos = fs.Position;
        StringBuilder xref = new();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {objects.Count + 1}");
        xref.AppendLine("0000000000 65535 f ");
        for (int i = 1; i <= objects.Count; i++)
        {
            xref.AppendLine($"{offsets[i]:D10} 00000 n ");
        }

        xref.AppendLine("trailer");
        xref.AppendLine($"<< /Size {objects.Count + 1} /Root 1 0 R >>");
        xref.AppendLine("startxref");
        xref.AppendLine(xrefPos.ToString(CultureInfo.InvariantCulture));
        xref.AppendLine("%%EOF");

        fs.Write(Encoding.ASCII.GetBytes(xref.ToString()));
    }

    private static PdfImage? TryLoadJpegImage(string path, Color backgroundForAlpha)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return TryLoadJpegFromBytes(File.ReadAllBytes(path), backgroundForAlpha);
        }
        catch
        {
            return null;
        }
    }

    private static PdfImage? TryLoadJpegFromBytes(byte[] rawBytes, Color backgroundForAlpha)
    {
        if (rawBytes is null || rawBytes.Length == 0)
        {
            return null;
        }

        try
        {
            using MemoryStream srcStream = new(rawBytes);
            using Image src = Image.FromStream(srcStream);
            int w = Math.Min(src.Width, 640);
            int h = Math.Max(1, (int)Math.Round(src.Height * (w / (double)src.Width)));
            using Bitmap rgb = new(w, h, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(rgb))
            {
                g.Clear(backgroundForAlpha);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.DrawImage(src, 0, 0, w, h);
            }

            using MemoryStream ms = new();
            ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
            if (jpegCodec is not null)
            {
                using EncoderParameters ep = new(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                rgb.Save(ms, jpegCodec, ep);
            }
            else
            {
                rgb.Save(ms, ImageFormat.Jpeg);
            }

            return new PdfImage(w, h, ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    private static string FormatDisplayValue(object? val, ExportColumnKind kind) =>
        ExportSanitizer.FormatDisplayValue(val, kind, ClCulture);

    private static double EstimateTextWidth(string text, double fontSize) =>
        text.Length * fontSize * 0.51;

    private static string TruncateText(string text, double maxWidthPt, double fontSize)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string singleLine = text.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        int maxChars = Math.Max(3, (int)Math.Floor(maxWidthPt / (fontSize * 0.53)));
        if (singleLine.Length <= maxChars)
        {
            return singleLine;
        }

        return string.Concat(singleLine.AsSpan(0, maxChars - 1), "…");
    }

    private static List<string> WrapText(string text, double maxWidthPt, double fontSize)
    {
        int maxChars = Math.Max(12, (int)Math.Floor(maxWidthPt / (fontSize * 0.52)));
        string[] words = text.Replace("\r\n", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> lines = [];
        StringBuilder current = new();

        foreach (string word in words)
        {
            if (current.Length + word.Length + 1 > maxChars && current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(word);
        }

        if (current.Length > 0)
        {
            lines.Add(current.ToString());
        }

        return lines.Count > 0 ? lines : [string.Empty];
    }

    private static string EscapePdfText(string text)
    {
        string clean = ExportSanitizer.SanitizeXmlText(text);
        StringBuilder sb = new(clean.Length + 8);
        foreach (char c in clean)
        {
            string? multi = c switch
            {
                '→' => "->",
                '≥' => ">=",
                '≤' => "<=",
                _ => null
            };
            if (multi is not null)
            {
                sb.Append(multi);
                continue;
            }

            if (c is '\\' or '(' or ')')
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private sealed record PdfImage(int Width, int Height, byte[] JpegBytes);
}
