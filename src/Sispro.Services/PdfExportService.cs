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
    private static readonly Encoding WinAnsi = Encoding.Latin1;
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
                    if (report.Columns[c].SumInFooter && TryToDecimal(r[c], out decimal d))
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
        PdfImage? sigJr = signatureOption is SignatureOption.JuanRamos or SignatureOption.Ambas
            ? TryLoadJpegImage(Path.Combine(assetsDir, "Firmas", "JR.jpg"), Color.White)
            : null;
        PdfImage? sigMs = signatureOption is SignatureOption.MarceloSilva or SignatureOption.Ambas
            ? TryLoadJpegImage(Path.Combine(assetsDir, "Firmas", "MS.jpg"), Color.White)
            : null;

        double pageW = 612;
        double pageH = 792;
        double marginX = 36;
        double marginTop = 32;
        double marginBottom = 36;
        double contentW = pageW - (marginX * 2);

        StringBuilder sb = new();
        DrawCorporateHeader(
            sb,
            pageW,
            pageH,
            marginX,
            marginTop,
            72,
            contentW,
            $"ORDEN DE TRABAJO EXTERNA N° {order.NumeroOt:D6}",
            $"{settings.CompanyDepartment}  |  RUT {settings.CompanyRut}",
            $"Fecha OT: {order.FechaOt:dd-MM-yyyy}  |  Estado: {order.Estado}",
            logo is not null);

        double y = pageH - marginTop - 88;

        // Bloque Proveedor & Solicitante
        double halfW = (contentW - 12) / 2;
        DrawInfoCard(sb, marginX, y, halfW, 92, "DATOS DEL PROVEEDOR",
        [
            $"Señor(es): {order.NombreProveedor}",
            $"Dirección: {order.DireccionProveedor}",
            $"Correo: {order.CorreoProveedor}",
            $"Activo: {order.RutaActivo}"
        ]);
        DrawInfoCard(sb, marginX + halfW + 12, y, halfW, 92, "DATOS DEL SOLICITANTE",
        [
            $"Solicitado por: {order.AliasSolicitante}",
            $"Correo: {order.CorreoSolicitante}",
            $"Teléfono: {order.TelefonoSolicitante}",
            $"Planta: {settings.CompanyAddress}"
        ]);

        y -= 108;

        // Tabla de detalles
        double[] colW = [38, 46, contentW - 184, 100];
        string[] colH = ["Ítem", "Cant.", "Detalle de Pieza en Reparación y Trabajo", "Guía / Estado"];
        sb.AppendLine(PrimaryFill);
        sb.AppendLine(Fmt($"{marginX} {y - 22} {contentW} 22 re f"));
        sb.AppendLine(WhiteFill);
        double cx = marginX;
        for (int i = 0; i < colH.Length; i++)
        {
            DrawText(sb, "F2", 8.5, cx + 5, y - 14.5, colH[i]);
            cx += colW[i];
        }

        y -= 22;
        double tableStart = y + 22;

        int itemIdx = 1;
        foreach (WorkOrderDetail d in order.Detalles)
        {
            List<string> lines = WrapText(d.Descripcion, colW[2] - 10, 8.5);
            double rh = Math.Max(20, (lines.Count * 11.5) + 8);
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
            DrawText(sb, "F1", 8.5, marginX + colW[0] + 14, y - 13, d.Cantidad.ToString(CultureInfo.InvariantCulture));

            double ly = y - 13;
            foreach (string line in lines)
            {
                DrawText(sb, "F1", 8.5, marginX + colW[0] + colW[1] + 5, ly, line);
                ly -= 11.5;
            }

            string gd = order.GuiaDespacho > 0 ? $"GD {order.GuiaDespacho}" : (d.AsociadoAFactura ? "Facturado" : "Abierta");
            DrawText(sb, "F1", 8.5, marginX + colW[0] + colW[1] + colW[2] + 6, y - 13, gd);

            y -= rh;
            itemIdx++;
        }

        sb.AppendLine("0.7 w");
        sb.AppendLine(BorderStroke);
        sb.AppendLine(Fmt($"{marginX} {y} {contentW} {tableStart - y} re S"));

        // Estado Vistos Buenos
        y -= 24;
        string vbInfo = $"VB Supervisor: {(order.TieneVbs ? "APROBADO" : "Pendiente")}     |     VB Jefatura: {(order.TieneVbj ? "APROBADO" : "Pendiente")}";
        sb.AppendLine(TextMutedFill);
        DrawText(sb, "F2", 9, marginX, y, vbInfo);

        // Firmas
        double sigY = Math.Max(marginBottom + 66, Math.Min(y - 95, 190));
        if (sigJr is not null)
        {
            sb.AppendLine(Fmt($"q 115 0 0 70 {marginX + 60} {sigY + 18} cm /ImJR Do Q"));
        }

        if (sigMs is not null)
        {
            sb.AppendLine(Fmt($"q 115 0 0 70 {marginX + halfW + 72} {sigY + 18} cm /ImMS Do Q"));
        }

        sb.AppendLine("0.8 w");
        sb.AppendLine(PrimaryDarkFill);
        sb.AppendLine(Fmt($"{marginX + 32} {sigY + 14} m {marginX + halfW - 24} {sigY + 14} l S"));
        sb.AppendLine(Fmt($"{marginX + halfW + 44} {sigY + 14} m {marginX + contentW - 24} {sigY + 14} l S"));

        DrawText(sb, "F2", 8.5, marginX + 38, sigY + 2, "Juan Ramos Moyano — Jefatura Mantención");
        DrawText(sb, "F2", 8.5, marginX + halfW + 46, sigY + 2, "Marcelo Silva Morgado — Supervisor Mantención");

        DrawFooter(sb, pageW, marginX, marginBottom, 1, 1, $"{settings.CompanyName} · {settings.CompanyCity}");

        List<(string Name, PdfImage Image)> images = [];
        if (logo is not null) images.Add(("ImLogo", logo));
        if (sigJr is not null) images.Add(("ImJR", sigJr));
        if (sigMs is not null) images.Add(("ImMS", sigMs));

        WritePdfDocument(outputPath, pageW, pageH, [sb.ToString()], images);
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
            using Image src = Image.FromFile(path);
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

    private static string FormatDisplayValue(object? val, ExportColumnKind kind)
    {
        if (val is null)
        {
            return string.Empty;
        }

        return kind switch
        {
            ExportColumnKind.Date when val is DateTime dt => dt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
            ExportColumnKind.DateTime when val is DateTime dt => dt.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
            ExportColumnKind.Currency when TryToDecimal(val, out decimal d) => $"${d.ToString("#,##0", ClCulture)}",
            ExportColumnKind.Integer when TryToDecimal(val, out decimal d) => d.ToString("0", CultureInfo.InvariantCulture),
            ExportColumnKind.Decimal when TryToDecimal(val, out decimal d) => d.ToString("#,##0.##", ClCulture),
            _ => Convert.ToString(val, ClCulture) ?? string.Empty
        };
    }

    private static bool TryToDecimal(object? val, out decimal result)
    {
        switch (val)
        {
            case decimal dec: result = dec; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case double dbl: result = Convert.ToDecimal(dbl); return true;
            case float flt: result = Convert.ToDecimal(flt); return true;
            default:
                result = 0;
                return false;
        }
    }

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
        StringBuilder sb = new(text.Length + 8);
        foreach (char c in text)
        {
            char mapped = c switch
            {
                '—' or '–' => '-',
                '…' => '.',
                '→' => '>',
                '•' or '·' => '-',
                '“' or '”' => '"',
                '‘' or '’' => '\'',
                _ => c
            };

            if (mapped is '\\' or '(' or ')')
            {
                sb.Append('\\');
            }

            sb.Append(mapped);
        }

        return sb.ToString();
    }

    private static string Fmt(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private sealed record PdfImage(int Width, int Height, byte[] JpegBytes);
}
