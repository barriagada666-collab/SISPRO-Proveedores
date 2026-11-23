using System.Globalization;
using ClosedXML.Excel;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Services;

/// <summary>
/// Exportación profesional a Excel (.xlsx) usando ClosedXML (sin dependencia de Microsoft Office COM).
/// </summary>
public sealed class ExcelExportService(AppSettings settings)
{
    private static readonly XLColor CambiasoOlive = XLColor.FromHtml("#0E4163");
    private static readonly XLColor CambiasoLight = XLColor.FromHtml("#EBF4FA");
    private static readonly XLColor BorderGray = XLColor.FromHtml("#B8CEDE");

    public void ExportWorkOrderDocument(
        WorkOrder order,
        string outputPath,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");

        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Orden de Trabajo");
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
        ws.PageSetup.FitToPages(1, 0);

        ws.Column(1).Width = 10;
        ws.Column(2).Width = 10;
        ws.Column(3).Width = 48;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 22;
        ws.Column(6).Width = 16;

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
            ws.Range(1, 1, 4, 3).Merge();
            ws.Cell(1, 1).Value = settings.CompanyName;
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Font.FontColor = CambiasoOlive;
            ws.Cell(1, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        // Right Header Box (mirrors Plantilla OT Externa 2.xls)
        IXLRange headerBox = ws.Range(1, 4, 5, 6);
        headerBox.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        headerBox.Style.Border.OutsideBorderColor = CambiasoOlive;

        ws.Range(1, 4, 1, 6).Merge().Value = $"RUT: {settings.CompanyRut}";
        ws.Range(2, 4, 2, 6).Merge().Value = "ORDEN DE TRABAJO EXTERNA";
        ws.Range(3, 4, 3, 6).Merge().Value = settings.CompanyDepartment;
        ws.Range(4, 4, 4, 6).Merge().Value = $"N° OT : {order.NumeroOt:D6}";
        ws.Range(5, 4, 5, 6).Merge().Value = $"FECHA: {order.FechaOt:dd-MM-yyyy}";

        for (int r = 1; r <= 5; r++)
        {
            ws.Cell(r, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(r, 4).Style.Font.Bold = true;
        }

        ws.Cell(2, 4).Style.Fill.BackgroundColor = CambiasoOlive;
        ws.Cell(2, 4).Style.Font.FontColor = XLColor.White;
        ws.Cell(2, 4).Style.Font.FontSize = 11;
        ws.Cell(4, 4).Style.Font.FontSize = 12;
        ws.Cell(4, 4).Style.Font.FontColor = XLColor.DarkRed;

        // Provider & Requester Info Blocks
        ws.Range(7, 1, 7, 3).Merge().Value = "DATOS DEL PROVEEDOR";
        ws.Range(7, 4, 7, 6).Merge().Value = "DATOS DEL SOLICITANTE";
        foreach (int col in new[] { 1, 4 })
        {
            ws.Cell(7, col).Style.Fill.BackgroundColor = CambiasoOlive;
            ws.Cell(7, col).Style.Font.FontColor = XLColor.White;
            ws.Cell(7, col).Style.Font.Bold = true;
        }

        ws.Cell(8, 1).Value = "SEÑOR(ES):";
        ws.Range(8, 2, 8, 3).Merge().Value = order.NombreProveedor;
        ws.Cell(9, 1).Value = "DIRECCIÓN:";
        ws.Range(9, 2, 9, 3).Merge().Value = order.DireccionProveedor;
        ws.Cell(10, 1).Value = "CORREO:";
        ws.Range(10, 2, 10, 3).Merge().Value = order.CorreoProveedor;
        ws.Cell(11, 1).Value = "ACTIVO:";
        ws.Range(11, 2, 11, 3).Merge().Value = order.RutaActivo;

        ws.Cell(8, 4).Value = "SOLICITADO POR:";
        ws.Range(8, 5, 8, 6).Merge().Value = order.AliasSolicitante;
        ws.Cell(9, 4).Value = "E-MAIL:";
        ws.Range(9, 5, 9, 6).Merge().Value = order.CorreoSolicitante;
        ws.Cell(10, 4).Value = "FONO CONTACTO:";
        ws.Range(10, 5, 10, 6).Merge().Value = order.TelefonoSolicitante;
        ws.Cell(11, 4).Value = "DIRECCIÓN:";
        ws.Range(11, 5, 11, 6).Merge().Value = settings.CompanyAddress;

        for (int r = 8; r <= 11; r++)
        {
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 4).Style.Font.Bold = true;
            ws.Row(r).Height = 20;
        }

        IXLRange infoBox = ws.Range(7, 1, 11, 6);
        infoBox.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        infoBox.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        infoBox.Style.Border.OutsideBorderColor = CambiasoOlive;

        // Work Items Table Header
        int headerRow = 13;
        ws.Cell(headerRow, 1).Value = "Ítem";
        ws.Cell(headerRow, 2).Value = "Cant.";
        ws.Range(headerRow, 3, headerRow, 4).Merge().Value = "DETALLE DE PIEZA EN REPARACIÓN Y TRABAJO";
        ws.Cell(headerRow, 5).Value = "FACTURA / GD";
        ws.Cell(headerRow, 6).Value = "VALOR";

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
            IXLRange descRange = ws.Range(row, 3, row, 4).Merge();
            descRange.Value = d.Descripcion;
            descRange.Style.Alignment.WrapText = true;
            ws.Cell(row, 5).Value = order.GuiaDespacho > 0 ? order.GuiaDespacho.ToString(CultureInfo.InvariantCulture) : string.Empty;
            ws.Cell(row, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(row, 6).Value = string.Empty;

            int lines = Math.Max(1, (int)Math.Ceiling(d.Descripcion.Length / 62.0));
            ws.Row(row).Height = Math.Max(22, lines * 16);

            if (row % 2 == 1)
            {
                ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = CambiasoLight;
            }

            row++;
        }

        // Minimum blank rows for clean physical form layout
        while (row < headerRow + 10)
        {
            ws.Range(row, 3, row, 4).Merge();
            ws.Row(row).Height = 20;
            row++;
        }

        IXLRange itemsBox = ws.Range(headerRow, 1, row - 1, 6);
        itemsBox.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        itemsBox.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        itemsBox.Style.Border.InsideBorderColor = BorderGray;
        itemsBox.Style.Border.OutsideBorderColor = CambiasoOlive;
        itemsBox.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // Approval Status Banner
        int vbRow = row + 1;
        string vbsText = order.TieneVbs ? "VB Supervisor: APROBADO" : "VB Supervisor: Pendiente";
        string vbjText = order.TieneVbj ? "VB Jefatura: APROBADO" : "VB Jefatura: Pendiente";
        ws.Range(vbRow, 1, vbRow, 3).Merge().Value = vbsText;
        ws.Range(vbRow, 4, vbRow, 6).Merge().Value = vbjText;
        ws.Range(vbRow, 1, vbRow, 6).Style.Font.Italic = true;
        ws.Range(vbRow, 1, vbRow, 6).Style.Font.FontColor = XLColor.DimGray;

        // Signatures Area
        int sigStartRow = vbRow + 2;
        string jrPath = Path.Combine(assetsDir, "Firmas", "JR.jpg");
        string msPath = Path.Combine(assetsDir, "Firmas", "MS.jpg");

        if (signatureOption is SignatureOption.JuanRamos or SignatureOption.Ambas && File.Exists(jrPath))
        {
            ws.AddPicture(jrPath)
              .MoveTo(ws.Cell(sigStartRow, 2), 8, 4)
              .WithSize(130, 85);
        }

        if (signatureOption is SignatureOption.MarceloSilva or SignatureOption.Ambas && File.Exists(msPath))
        {
            ws.AddPicture(msPath)
              .MoveTo(ws.Cell(sigStartRow, 4), 24, 4)
              .WithSize(130, 85);
        }

        int lineRow = sigStartRow + 5;
        ws.Range(lineRow, 1, lineRow, 3).Merge().Value = "______________________________";
        ws.Range(lineRow + 1, 1, lineRow + 1, 3).Merge().Value = "Juan Ramos Moyano — Jefatura Mantención";
        ws.Range(lineRow, 4, lineRow, 6).Merge().Value = "______________________________";
        ws.Range(lineRow + 1, 4, lineRow + 1, 6).Merge().Value = "Marcelo Silva Morgado — Supervisor Mantención";
        ws.Range(lineRow, 1, lineRow + 1, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(lineRow + 1, 1, lineRow + 1, 6).Style.Font.Bold = true;

        wb.SaveAs(outputPath);
    }

    public async Task ExportWorkOrdersGridAsync(
        IReadOnlyList<WorkOrderSearchRow> rows,
        string outputPath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            using XLWorkbook wb = new();
            IXLWorksheet ws = wb.Worksheets.Add("Órdenes de Trabajo");

            string[] headers =
            [
                "N° OT", "Fecha", "Proveedor", "Solicitante", "Cantidad",
                "Detalle del Trabajo", "Ruta de Activo", "VB Sup.", "VB Jef.", "Facturado"
            ];

            WriteStyledHeader(ws, headers);

            int total = Math.Max(1, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WorkOrderSearchRow item = rows[i];
                int r = i + 2;

                ws.Cell(r, 1).Value = item.NumeroOt;
                ws.Cell(r, 2).Value = item.Fecha;
                ws.Cell(r, 2).Style.DateFormat.Format = "dd-MM-yyyy";
                ws.Cell(r, 3).Value = item.Proveedor;
                ws.Cell(r, 4).Value = item.Solicitante;
                ws.Cell(r, 5).Value = item.Cantidad;
                ws.Cell(r, 6).Value = item.Detalle;
                ws.Cell(r, 7).Value = item.RutaActivo;
                ws.Cell(r, 8).Value = item.TieneVbs ? "Sí" : "No";
                ws.Cell(r, 9).Value = item.TieneVbj ? "Sí" : "No";
                ws.Cell(r, 10).Value = item.AsociadoAFactura ? "Sí" : "No";

                if (r % 2 == 1)
                {
                    ws.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = CambiasoLight;
                }

                if (progress is not null && (i % 25 == 0 || i == rows.Count - 1))
                {
                    progress.Report((int)((i + 1) * 100.0 / total));
                }
            }

            FinalizeTable(ws, rows.Count + 1, headers.Length);
            ws.Column(6).Width = 60;
            ws.Column(7).Width = 36;
            wb.SaveAs(outputPath);
        }, cancellationToken);
    }

    public void ExportInvoicesGrid(IReadOnlyList<InvoiceSearchRow> rows, string outputPath)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Facturas y Guías");
        string[] headers = ["N° Documento", "Fecha", "Proveedor", "Recibido Por", "Ítems", "Neto ($)", "IVA 19% ($)", "Total c/IVA ($)", "Detalle"];
        WriteStyledHeader(ws, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            InvoiceSearchRow item = rows[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = item.NumeroDocumento;
            ws.Cell(r, 2).Value = item.Fecha;
            ws.Cell(r, 2).Style.DateFormat.Format = "dd-MM-yyyy";
            ws.Cell(r, 3).Value = item.Proveedor;
            ws.Cell(r, 4).Value = item.RecibidoPor;
            ws.Cell(r, 5).Value = item.CantidadItems;
            ws.Cell(r, 6).Value = item.TotalNeto;
            ws.Cell(r, 7).Value = item.Iva;
            ws.Cell(r, 8).Value = item.TotalConIva;
            ws.Range(r, 6, r, 8).Style.NumberFormat.Format = "#,##0";
            ws.Cell(r, 9).Value = item.Detalle;
        }

        FinalizeTable(ws, rows.Count + 1, headers.Length);
        wb.SaveAs(outputPath);
    }

    public void ExportProvidersGrid(IReadOnlyList<Provider> providers, string outputPath)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Proveedores");
        string[] headers = ["ID", "Proveedor", "Dirección", "Teléfono", "Fax", "Correo", "Contacto", "Correo Contacto", "Estado"];
        WriteStyledHeader(ws, headers);

        for (int i = 0; i < providers.Count; i++)
        {
            Provider p = providers[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = p.IdProv;
            ws.Cell(r, 2).Value = p.Nombre;
            ws.Cell(r, 3).Value = p.Direccion;
            ws.Cell(r, 4).Value = p.Telefono;
            ws.Cell(r, 5).Value = p.Fax;
            ws.Cell(r, 6).Value = p.Correo;
            ws.Cell(r, 7).Value = p.Contacto;
            ws.Cell(r, 8).Value = p.CorreoContacto;
            ws.Cell(r, 9).Value = p.Activo ? "Activo" : "Inactivo";
        }

        FinalizeTable(ws, providers.Count + 1, headers.Length);
        wb.SaveAs(outputPath);
    }

    public void ExportUsersGrid(IReadOnlyList<User> users, string outputPath)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Usuarios");
        string[] headers = ["ID", "Usuario", "Nombre Completo", "Correo Electrónico", "Teléfono", "Perfil", "Estado", "Fecha Creación", "Último Acceso"];
        WriteStyledHeader(ws, headers);

        for (int i = 0; i < users.Count; i++)
        {
            User u = users[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = u.CodigoId;
            ws.Cell(r, 2).Value = u.AliasUsuario;
            ws.Cell(r, 3).Value = u.NombreCompleto;
            ws.Cell(r, 4).Value = u.MailUsuario;
            ws.Cell(r, 5).Value = u.FonoUsuario;
            ws.Cell(r, 6).Value = u.Nivel.ToString();
            ws.Cell(r, 7).Value = u.Activo ? "Activo" : "Inactivo";
            ws.Cell(r, 8).Value = u.FechaCreacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
            ws.Cell(r, 9).Value = u.UltimoAcceso.HasValue
                ? u.UltimoAcceso.Value.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)
                : "Sin accesos";
        }

        FinalizeTable(ws, users.Count + 1, headers.Length);
        wb.SaveAs(outputPath);
    }

    public void ExportMaintenanceOrders(IReadOnlyList<MantWorkOrderRow> rows, string outputPath)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("OTs Mantención");
        string[] headers = ["N° OT", "Fecha Inicio", "Fecha Fin", "Turno", "Oficio", "Motivo", "Planta", "Equipo", "Estado", "Detención", "Solicitada Por", "Descripción"];
        WriteStyledHeader(ws, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            MantWorkOrderRow o = rows[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = o.NumeroOt;
            ws.Cell(r, 2).Value = o.FechaInicio;
            ws.Cell(r, 3).Value = o.FechaFin;
            ws.Cell(r, 4).Value = o.Turno;
            ws.Cell(r, 5).Value = o.Oficio;
            ws.Cell(r, 6).Value = o.Motivo;
            ws.Cell(r, 7).Value = o.Planta;
            ws.Cell(r, 8).Value = o.Equipo;
            ws.Cell(r, 9).Value = o.Estado;
            ws.Cell(r, 10).Value = o.Detencion;
            ws.Cell(r, 11).Value = o.SolicitadaPor;
            ws.Cell(r, 12).Value = o.DescripcionBreve;
        }

        FinalizeTable(ws, rows.Count + 1, headers.Length);
        wb.SaveAs(outputPath);
    }

    public void ExportSupplies(IReadOnlyList<MantSupplyRow> rows, string outputPath)
    {
        using XLWorkbook wb = new();
        IXLWorksheet ws = wb.Worksheets.Add("Insumos SAP");
        string[] headers = ["ID", "Código SAP", "Cód. Proveedor", "Descripción", "Stock", "Días Entrega", "Prioridad", "Usos Históricos"];
        WriteStyledHeader(ws, headers);

        for (int i = 0; i < rows.Count; i++)
        {
            MantSupplyRow s = rows[i];
            int r = i + 2;
            ws.Cell(r, 1).Value = s.IdItem;
            ws.Cell(r, 2).Value = s.CodSap;
            ws.Cell(r, 3).Value = s.CodProveedor;
            ws.Cell(r, 4).Value = s.Descripcion;
            ws.Cell(r, 5).Value = s.StockUnidad;
            ws.Cell(r, 6).Value = s.DiasEntrega;
            ws.Cell(r, 7).Value = s.Prioridad;
            ws.Cell(r, 8).Value = s.UsosHistoricos;
        }

        FinalizeTable(ws, rows.Count + 1, headers.Length);
        wb.SaveAs(outputPath);
    }

    private static void WriteStyledHeader(IXLWorksheet ws, string[] headers)
    {
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        IXLRange header = ws.Range(1, 1, 1, headers.Length);
        header.Style.Fill.BackgroundColor = CambiasoOlive;
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(1).Height = 24;
        ws.SheetView.FreezeRows(1);
    }

    private static void FinalizeTable(IXLWorksheet ws, int lastRow, int lastCol)
    {
        if (lastRow >= 1)
        {
            IXLRange used = ws.Range(1, 1, Math.Max(1, lastRow), lastCol);
            used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            used.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
            used.Style.Border.InsideBorderColor = BorderGray;
            used.SetAutoFilter();
        }

        ws.Columns(1, lastCol).AdjustToContents(1, Math.Min(lastRow, 200), 10, 48);
    }
}
