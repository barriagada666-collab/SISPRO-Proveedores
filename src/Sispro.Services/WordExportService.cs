using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace Sispro.Services;

/// <summary>
/// Generador corporativo de documentos Microsoft Word (.docx) mediante OpenXML SDK
/// con logotipo Cambiaso, paleta institucional Petrol/Dorado, tablas estilizadas y pie con numeración.
/// </summary>
public sealed class WordExportService(AppSettings settings)
{
    private static readonly CultureInfo ClCulture = new("es-CL");
    private static int _imageCounter;
    private const string PrimaryDarkHex = "0A263C";
    private const string PrimaryHex = "0E4163";
    private const string GoldHex = "C8A977";
    private const string ZebraHex = "EBF4FA";
    private const string TotalHex = "E0ECF5";
    private const string BorderHex = "C5D6E2";
    private const string TextHex = "0D2235";
    private const string MutedHex = "4A6278";

    public void ExportTabularReport(TabularReportData report, string outputPath, string? assetsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        string logoPath = Path.Combine(assetsDir, "logo_cambiaso.png");

        using WordprocessingDocument doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        Body body = mainPart.Document.Body!;

        // 1. Banner de encabezado corporativo
        AppendCorporateBanner(mainPart, body, report.Title, report.Subtitle, report.FilterSummary, report.Rows.Count, logoPath);

        // 2. Tabla de datos
        Table table = CreateStyledTable();

        // Encabezado repetible
        TableRow headerRow = new(new TableRowProperties(new TableHeader(), new CantSplit()));
        foreach (ExportColumn col in report.Columns)
        {
            headerRow.Append(CreateCell(col.Header, PrimaryHex, "FFFFFF", bold: true, fontSizeHalfPt: "17", JustificationValues.Left));
        }
        table.Append(headerRow);

        bool hasTotals = report.Columns.Any(c => c.SumInFooter) && report.Rows.Count > 0;
        decimal[] sums = new decimal[report.Columns.Count];

        for (int r = 0; r < report.Rows.Count; r++)
        {
            object?[] data = report.Rows[r];
            string? bg = r % 2 == 1 ? ZebraHex : null;
            TableRow tr = new(new TableRowProperties(new CantSplit()));

            for (int c = 0; c < report.Columns.Count; c++)
            {
                object? val = c < data.Length ? data[c] : null;
                ExportColumn col = report.Columns[c];
                if (hasTotals && col.SumInFooter && ExportSanitizer.TryToDecimal(val, out decimal d))
                {
                    sums[c] += d;
                }

                string text = FormatValue(val, col.Kind);
                JustificationValues align = col.Kind is ExportColumnKind.Integer or ExportColumnKind.Decimal or ExportColumnKind.Currency
                    ? JustificationValues.Right
                    : JustificationValues.Left;
                tr.Append(CreateCell(text, bg, TextHex, bold: false, fontSizeHalfPt: "16", align));
            }

            table.Append(tr);
        }

        if (hasTotals)
        {
            TableRow totalRow = new(new TableRowProperties(new CantSplit()));
            for (int c = 0; c < report.Columns.Count; c++)
            {
                string txt = c == 0
                    ? $"TOTAL ({report.Rows.Count.ToString("N0", ClCulture)})"
                    : (report.Columns[c].SumInFooter ? FormatValue(sums[c], report.Columns[c].Kind) : string.Empty);
                JustificationValues align = c == 0 ? JustificationValues.Left : JustificationValues.Right;
                totalRow.Append(CreateCell(txt, TotalHex, PrimaryDarkHex, bold: true, fontSizeHalfPt: "16", align));
            }
            table.Append(totalRow);
        }

        body.Append(table);

        if (!string.IsNullOrWhiteSpace(report.FooterNote))
        {
            body.Append(CreateStyledParagraph(report.FooterNote, MutedHex, "16", bold: false, spaceBefore: "140"));
        }

        // 3. Pie de página + orientación (Carta horizontal/vertical)
        AttachFooterAndSectionProperties(mainPart, body, report.Landscape);
        mainPart.Document.Save();
    }

    public void ExportWorkOrderDocument(
        WorkOrder order,
        string outputPath,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(order);
        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        string logoPath = Path.Combine(assetsDir, "logo_cambiaso.png");

        using WordprocessingDocument doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        MainDocumentPart mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        Body body = mainPart.Document.Body!;

        AppendWorkOrderBanner(mainPart, body, order, logoPath);

        // Datos Proveedor y Solicitante
        Table infoTable = CreateStyledTable();
        infoTable.Append(new TableRow(
            CreateCellWithWidth("DATOS DEL PROVEEDOR", PrimaryHex, "FFFFFF", true, "17", JustificationValues.Left, "2500"),
            CreateCellWithWidth("DATOS DEL SOLICITANTE", PrimaryHex, "FFFFFF", true, "17", JustificationValues.Left, "2500")));
        infoTable.Append(new TableRow(
            CreateCellWithWidth(
                $"Señor(es): {order.NombreProveedor}\nDirección: {order.DireccionProveedor}\nCorreo: {order.CorreoProveedor}",
                null, TextHex, false, "17", JustificationValues.Left, "2500"),
            CreateCellWithWidth(
                $"Solicitado por: {order.SolicitanteDisplayNombre}\nCorreo: {order.CorreoSolicitante}\nTeléfono: {order.TelefonoSolicitante}\nActivo: {order.RutaActivo}",
                null, TextHex, false, "17", JustificationValues.Left, "2500")));
        body.Append(infoTable);
        body.Append(new Paragraph(new Run(new Text(string.Empty))));

        // Detalle de trabajos (3 columnas: Ítem, Cant., Detalle)
        Table itemsTable = CreateStyledTable();
        itemsTable.Append(new TableRow(
            new TableRowProperties(new TableHeader()),
            CreateCellWithWidth("Ítem", PrimaryHex, "FFFFFF", true, "17", JustificationValues.Center, "450"),
            CreateCellWithWidth("Cant.", PrimaryHex, "FFFFFF", true, "17", JustificationValues.Center, "550"),
            CreateCellWithWidth("Detalle de Pieza en Reparación y Trabajo", PrimaryHex, "FFFFFF", true, "17", JustificationValues.Left, "4000")));

        int idx = 1;
        foreach (WorkOrderDetail d in order.Detalles)
        {
            string? bg = idx % 2 == 0 ? ZebraHex : null;
            itemsTable.Append(new TableRow(
                CreateCellWithWidth(idx.ToString(CultureInfo.InvariantCulture), bg, TextHex, false, "17", JustificationValues.Center, "450"),
                CreateCellWithWidth(d.Cantidad.ToString(CultureInfo.InvariantCulture), bg, TextHex, false, "17", JustificationValues.Center, "550"),
                CreateCellWithWidth(d.Descripcion, bg, TextHex, false, "17", JustificationValues.Left, "4000")));
            idx++;
        }

        body.Append(itemsTable);
        body.Append(new Paragraph(new Run(new Text(string.Empty))));

        // Bloque de Observaciones y Valor Neto destacado
        Table summaryTable = CreateStyledTable();
        string obsText = string.IsNullOrWhiteSpace(order.Observacion) ? "Sin observaciones." : order.Observacion;
        summaryTable.Append(new TableRow(
            CreateCellWithWidth($"Observaciones: {obsText}", ZebraHex, TextHex, false, "17", JustificationValues.Left, "3500"),
            CreateCellWithWidth($"VALOR NETO\n$ {order.ValorNeto.ToString("N0", ClCulture)}", GoldHex, PrimaryDarkHex, true, "22", JustificationValues.Right, "1500")));
        body.Append(summaryTable);

        // Firmas dinámicas al pie (se omiten completamente si ninguna está seleccionada)
        List<WorkOrderSignatureSlot> slots = WorkOrderSignatureLayout.BuildSlots(order, signatureOption, assetsDir);
        if (slots.Count > 0)
        {
            Table sigTable = new(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None })));

            string cellPct = (5000 / slots.Count).ToString(CultureInfo.InvariantCulture);
            long imgCx = slots.Count == 3 ? 980_000 : 1_120_000;
            long imgCy = slots.Count == 3 ? 520_000 : 620_000;
            TableRow sigRow = new();

            foreach (WorkOrderSignatureSlot slot in slots)
            {
                TableCell cell = new(new TableCellProperties(
                    new TableCellWidth { Width = cellPct, Type = TableWidthUnitValues.Pct },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Bottom }));

                if (!string.IsNullOrEmpty(slot.AssetImagePath) && File.Exists(slot.AssetImagePath))
                {
                    cell.Append(CreateImageParagraph(mainPart, slot.AssetImagePath, imgCx, imgCy, JustificationValues.Center));
                }
                else if (slot.ImageBytes is { Length: > 0 })
                {
                    cell.Append(CreateImageParagraphFromBytes(mainPart, slot.ImageBytes, ImagePartType.Png, "firma_tecnico.png", imgCx, imgCy, JustificationValues.Center));
                }

                cell.Append(CreateStyledParagraph("______________________", TextHex, "16", false, "100", JustificationValues.Center));
                cell.Append(CreateStyledParagraph(slot.SignerName, PrimaryDarkHex, "16", true, "15", JustificationValues.Center));
                cell.Append(CreateStyledParagraph(slot.RoleTitle, MutedHex, "15", false, "10", JustificationValues.Center));
                sigRow.Append(cell);
            }

            sigTable.Append(sigRow);
            body.Append(new Paragraph());
            body.Append(sigTable);
        }

        AttachFooterAndSectionProperties(mainPart, body, landscape: false);
        mainPart.Document.Save();
    }

    private void AppendWorkOrderBanner(MainDocumentPart mainPart, Body body, WorkOrder order, string logoPath)
    {
        Table banner = new(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Color = GoldHex, Size = 18 },
                new LeftBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));

        TableRow tr = new();
        bool hasLogo = File.Exists(logoPath);
        if (hasLogo)
        {
            TableCell logoCell = new(new TableCellProperties(
                new TableCellWidth { Width = "1150", Type = TableWidthUnitValues.Pct },
                new Shading { Val = ShadingPatternValues.Clear, Fill = PrimaryDarkHex },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                new TableCellMargin(
                    new TopMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa })));
            logoCell.Append(CreateImageParagraph(mainPart, logoPath, 1_180_000, 600_000, JustificationValues.Left));
            tr.Append(logoCell);
        }

        TableCell textCell = new(new TableCellProperties(
            new TableCellWidth { Width = hasLogo ? "2450" : "3600", Type = TableWidthUnitValues.Pct },
            new Shading { Val = ShadingPatternValues.Clear, Fill = PrimaryDarkHex },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
            new TableCellMargin(
                new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "120", Type = TableWidthUnitValues.Dxa })));

        textCell.Append(CreateStyledParagraph("ORDEN DE TRABAJO EXTERNA", "FFFFFF", "26", bold: true, spaceBefore: "0"));
        textCell.Append(CreateStyledParagraph($"{settings.CompanyDepartment} · RUT {settings.CompanyRut}", GoldHex, "17", bold: false, spaceBefore: "35"));
        textCell.Append(CreateStyledParagraph($"Fecha OT: {order.FechaOt:dd-MM-yyyy}", "D4E3EE", "17", bold: false, spaceBefore: "35"));
        tr.Append(textCell);

        TableCell badgeCell = new(new TableCellProperties(
            new TableCellWidth { Width = "1400", Type = TableWidthUnitValues.Pct },
            new Shading { Val = ShadingPatternValues.Clear, Fill = GoldHex },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
            new TableCellBorders(
                new LeftBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 12 }),
            new TableCellMargin(
                new TopMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })));
        badgeCell.Append(CreateStyledParagraph("OT EXTERNA N°", PrimaryDarkHex, "16", bold: true, spaceBefore: "0", JustificationValues.Center));
        badgeCell.Append(CreateStyledParagraph(order.NumeroOt.ToString("D6", CultureInfo.InvariantCulture), PrimaryDarkHex, "44", bold: true, spaceBefore: "20", JustificationValues.Center));
        tr.Append(badgeCell);

        banner.Append(tr);
        body.Append(banner);
        body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "60", After = "60" })));
    }

    private static TableCell CreateCellWithWidth(
        string text,
        string? fillHex,
        string textHex,
        bool bold,
        string fontSizeHalfPt,
        JustificationValues align,
        string widthPct)
    {
        TableCell cell = CreateCell(text, fillHex, textHex, bold, fontSizeHalfPt, align);
        cell.TableCellProperties!.PrependChild(new TableCellWidth { Width = widthPct, Type = TableWidthUnitValues.Pct });
        return cell;
    }

    private void AppendCorporateBanner(
        MainDocumentPart mainPart,
        Body body,
        string title,
        string subtitle,
        string filterSummary,
        int rowCount,
        string logoPath)
    {
        Table banner = new(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Color = GoldHex, Size = 18 },
                new LeftBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 6 },
                new RightBorder { Val = BorderValues.Single, Color = PrimaryDarkHex, Size = 6 },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None })));

        TableRow tr = new();

        bool hasLogo = File.Exists(logoPath);
        if (hasLogo)
        {
            TableCell logoCell = new(new TableCellProperties(
                new TableCellWidth { Width = "1350", Type = TableWidthUnitValues.Pct },
                new Shading { Val = ShadingPatternValues.Clear, Fill = PrimaryDarkHex },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
                new TableCellMargin(
                    new TopMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "140", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa })));
            logoCell.Append(CreateImageParagraph(mainPart, logoPath, 1_350_000, 680_000, JustificationValues.Left));
            tr.Append(logoCell);
        }

        TableCell textCell = new(new TableCellProperties(
            new TableCellWidth { Width = hasLogo ? "3650" : "5000", Type = TableWidthUnitValues.Pct },
            new Shading { Val = ShadingPatternValues.Clear, Fill = PrimaryDarkHex },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
            new TableCellMargin(
                new TopMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "160", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "140", Type = TableWidthUnitValues.Dxa })));

        textCell.Append(CreateStyledParagraph(title, "FFFFFF", "26", bold: true, spaceBefore: "0"));
        string sub = string.IsNullOrWhiteSpace(subtitle) ? $"{settings.CompanyName} — {settings.CompanyDepartment}" : subtitle;
        textCell.Append(CreateStyledParagraph(sub, GoldHex, "18", bold: false, spaceBefore: "40"));
        textCell.Append(CreateStyledParagraph($"Emitido: {DateTime.Now:dd-MM-yyyy HH:mm}  |  Registros: {rowCount.ToString("N0", ClCulture)}", "D4E3EE", "16", bold: false, spaceBefore: "40"));
        tr.Append(textCell);

        banner.Append(tr);
        body.Append(banner);

        body.Append(CreateStyledParagraph($"Filtros aplicados: {filterSummary}", MutedHex, "16", bold: false, spaceBefore: "100"));
        body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "40", After = "60" })));
    }

    private void AttachFooterAndSectionProperties(MainDocumentPart mainPart, Body body, bool landscape)
    {
        FooterPart footerPart = mainPart.AddNewPart<FooterPart>();
        Footer footer = new();
        Paragraph p = new(
            new ParagraphProperties(
                new Justification { Val = JustificationValues.Right },
                new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Color = BorderHex, Size = 4 })),
            CreateRun($"SISPRO — {settings.CompanyName} · {settings.CompanyDepartment}   |   Página ", MutedHex, "15", false),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" PAGE ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
            CreateRun(" de ", MutedHex, "15", false),
            new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
            new Run(new FieldCode(" NUMPAGES ") { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
        footer.Append(p);
        footerPart.Footer = footer;

        string footerRelId = mainPart.GetIdOfPart(footerPart);
        SectionProperties secProps = new(
            new FooterReference { Type = HeaderFooterValues.Default, Id = footerRelId },
            new PageSize
            {
                Width = landscape ? 15840U : 12240U,
                Height = landscape ? 12240U : 15840U,
                Orient = landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait
            },
            new PageMargin { Top = 720, Bottom = 720, Left = 720, Right = 720, Header = 360, Footer = 360 });
        body.Append(secProps);
    }

    private static Table CreateStyledTable() =>
        new(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Color = PrimaryHex, Size = 6 },
                new BottomBorder { Val = BorderValues.Single, Color = PrimaryHex, Size = 6 },
                new LeftBorder { Val = BorderValues.Single, Color = BorderHex, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Color = BorderHex, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Color = BorderHex, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Color = BorderHex, Size = 4 })));

    private static TableCell CreateCell(
        string text,
        string? fillHex,
        string textHex,
        bool bold,
        string fontSizeHalfPt,
        JustificationValues align)
    {
        TableCellProperties props = new(
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
            new TableCellMargin(
                new TopMargin { Width = "70", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "70", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "90", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "90", Type = TableWidthUnitValues.Dxa }));

        if (!string.IsNullOrEmpty(fillHex))
        {
            props.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = fillHex });
        }

        TableCell cell = new(props);
        string[] lines = text.Split('\n');
        foreach (string line in lines)
        {
            cell.Append(CreateStyledParagraph(line, textHex, fontSizeHalfPt, bold, "0", align));
        }

        return cell;
    }

    private static Paragraph CreateStyledParagraph(
        string text,
        string colorHex,
        string fontSizeHalfPt,
        bool bold,
        string spaceBefore,
        JustificationValues? align = null)
    {
        ParagraphProperties pp = new(new SpacingBetweenLines { Before = spaceBefore, After = "20" });
        if (align.HasValue)
        {
            pp.Append(new Justification { Val = align.Value });
        }

        return new Paragraph(pp, CreateRun(text, colorHex, fontSizeHalfPt, bold));
    }

    private static Run CreateRun(string text, string colorHex, string fontSizeHalfPt, bool bold)
    {
        RunProperties rp = new(
            new RunFonts { Ascii = "Segoe UI", HighAnsi = "Segoe UI", ComplexScript = "Segoe UI" },
            new DocumentFormat.OpenXml.Wordprocessing.Color { Val = colorHex },
            new FontSize { Val = fontSizeHalfPt });
        if (bold)
        {
            rp.Append(new Bold());
        }

        return new Run(rp, new Text(ExportSanitizer.SanitizeXmlText(text)) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph CreateImageParagraph(
        MainDocumentPart mainPart,
        string imagePath,
        long cx,
        long cy,
        JustificationValues align)
    {
        PartTypeInfo partType = imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            ? ImagePartType.Png
            : ImagePartType.Jpeg;
        return CreateImageParagraphFromBytes(
            mainPart,
            File.ReadAllBytes(imagePath),
            partType,
            Path.GetFileName(imagePath),
            cx,
            cy,
            align);
    }

    private static Paragraph CreateImageParagraphFromBytes(
        MainDocumentPart mainPart,
        byte[] imageBytes,
        PartTypeInfo partType,
        string imageName,
        long cx,
        long cy,
        JustificationValues align)
    {
        ImagePart imagePart = mainPart.AddImagePart(partType);
        using (MemoryStream ms = new(imageBytes))
        {
            imagePart.FeedData(ms);
        }

        string relId = mainPart.GetIdOfPart(imagePart);
        uint imgId = (uint)Interlocked.Increment(ref _imageCounter);

        Drawing drawing = new(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = imgId, Name = $"Img_{imgId}" },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = imgId, Name = imageName },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = relId },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(new A.Offset { X = 0L, Y = 0L }, new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            });

        return new Paragraph(new ParagraphProperties(new Justification { Val = align }), new Run(drawing));
    }

    private static string FormatValue(object? val, ExportColumnKind kind) =>
        ExportSanitizer.FormatDisplayValue(val, kind, ClCulture);
}
