using System.Drawing;
using System.Drawing.Printing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Services;

/// <summary>
/// Servicio de impresión nativa .NET 10 (reemplaza las llamadas Win32 heredadas de API_IMPRESORA.bas).
/// </summary>
public sealed class PrintService(AppSettings settings)
{
    private static readonly Color CambiasoNavy = Color.FromArgb(10, 38, 60);
    private static readonly Color CambiasoGreen = Color.FromArgb(14, 65, 99);
    private static readonly Color CambiasoGold = Color.FromArgb(200, 169, 119);
    private static readonly Color SubtleFill = Color.FromArgb(235, 244, 250);

    public PrintDocument CreateWorkOrderPrintDocument(
        WorkOrder order,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        int currentDetailIndex = 0;
        bool detailsFinished = false;
        bool summaryDrawn = false;
        int pageNumber = 1;
        List<WorkOrderSignatureSlot> slots = WorkOrderSignatureLayout.BuildSlots(order, signatureOption, assetsDir);

        PrintDocument doc = new()
        {
            DocumentName = $"OT_{order.NumeroOt:D6}_Cambiaso"
        };

        doc.BeginPrint += (_, _) =>
        {
            currentDetailIndex = 0;
            detailsFinished = false;
            summaryDrawn = false;
            pageNumber = 1;
        };

        doc.PrintPage += (_, e) =>
        {
            Graphics? g = e.Graphics;
            if (g is null)
            {
                e.HasMorePages = false;
                return;
            }

            Rectangle bounds = e.MarginBounds;
            float y = bounds.Top;

            using Font otBigFont = new("Segoe UI", 19, FontStyle.Bold);
            using Font titleFont = new("Segoe UI", 13, FontStyle.Bold);
            using Font subtitleFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font boldFont = new("Segoe UI", 9, FontStyle.Bold);
            using Font regularFont = new("Segoe UI", 9, FontStyle.Regular);
            using Font smallFont = new("Segoe UI", 8, FontStyle.Regular);
            using Font smallBoldFont = new("Segoe UI", 8, FontStyle.Bold);
            using Pen olivePen = new(CambiasoNavy, 1.5f);
            using Pen gridPen = new(Color.FromArgb(197, 214, 226), 0.75f);
            using SolidBrush navyBrush = new(CambiasoNavy);
            using SolidBrush oliveBrush = new(CambiasoGreen);
            using SolidBrush goldBrush = new(CambiasoGold);
            using SolidBrush lightBrush = new(SubtleFill);

            // Header Logo + Caja Superior Derecha Destacada para N° OT
            string logoPath = Path.Combine(assetsDir, "logo_cambiaso.png");
            if (File.Exists(logoPath))
            {
                using Image logo = Image.FromFile(logoPath);
                g.DrawImage(logo, new Rectangle(bounds.Left, (int)y, 178, 90));
            }
            else
            {
                g.DrawString(settings.CompanyName, titleFont, oliveBrush, bounds.Left, y + 18);
            }

            Rectangle headerBox = new(bounds.Right - 285, (int)y, 285, 96);
            g.DrawRectangle(olivePen, headerBox);
            using StringFormat center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString($"RUT: {settings.CompanyRut}  ·  {settings.CompanyDepartment}", smallFont, Brushes.Black,
                new RectangleF(headerBox.X + 2, headerBox.Y + 3, headerBox.Width - 4, 17), center);
            g.FillRectangle(oliveBrush, headerBox.X + 1, headerBox.Y + 21, headerBox.Width - 2, 20);
            g.DrawString("ORDEN DE TRABAJO EXTERNA", subtitleFont, Brushes.White,
                new RectangleF(headerBox.X + 2, headerBox.Y + 21, headerBox.Width - 4, 20), center);
            g.FillRectangle(goldBrush, headerBox.X + 1, headerBox.Y + 41, headerBox.Width - 2, 35);
            g.DrawString($"OT N° {order.NumeroOt:D6}", otBigFont, navyBrush,
                new RectangleF(headerBox.X + 2, headerBox.Y + 41, headerBox.Width - 4, 35), center);
            g.DrawString($"Fecha OT: {order.FechaOt:dd-MM-yyyy}", boldFont, Brushes.Black,
                new RectangleF(headerBox.X + 2, headerBox.Y + 77, headerBox.Width - 4, 18), center);

            y += 108;

            if (!detailsFinished)
            {
                int halfWidth = (bounds.Width - 12) / 2;
                (string Label, string Value)[] provRows =
                [
                    ("Señor(es):", order.NombreProveedor),
                    ("Dirección:", order.DireccionProveedor),
                    ("Correo:", order.CorreoProveedor)
                ];
                (string Label, string Value)[] solRows =
                [
                    ("Solicitado por:", order.SolicitanteDisplayNombre),
                    ("Correo:", order.CorreoSolicitante),
                    ("Teléfono:", order.TelefonoSolicitante),
                    ("Activo:", order.RutaActivo)
                ];

                int cardHeight = Math.Max(
                    MeasureInfoCardHeight(g, halfWidth, regularFont, provRows),
                    MeasureInfoCardHeight(g, halfWidth, regularFont, solRows));

                Rectangle leftCard = new(bounds.Left, (int)y, halfWidth, cardHeight);
                Rectangle rightCard = new(bounds.Left + halfWidth + 12, (int)y, halfWidth, cardHeight);

                DrawInfoCard(g, leftCard, "DATOS DEL PROVEEDOR", oliveBrush, olivePen, boldFont, regularFont, provRows);
                DrawInfoCard(g, rightCard, "DATOS DEL SOLICITANTE", oliveBrush, olivePen, boldFont, regularFont, solRows);

                y += cardHeight + 12;

                // Tabla de Detalle (3 columnas: Ítem, Cant., Detalle)
                int colItem = 44;
                int colCant = 54;
                int colDesc = bounds.Width - colItem - colCant;

                Rectangle tblHdr = new(bounds.Left, (int)y, bounds.Width, 24);
                g.FillRectangle(oliveBrush, tblHdr);
                g.DrawString("Ítem", boldFont, Brushes.White, bounds.Left + 6, y + 4);
                g.DrawString("Cant.", boldFont, Brushes.White, bounds.Left + colItem + 6, y + 4);
                g.DrawString("DETALLE DE PIEZA EN REPARACIÓN Y TRABAJO", boldFont, Brushes.White, bounds.Left + colItem + colCant + 6, y + 4);
                y += 24;

                float maxTableBottom = bounds.Bottom - 36;
                while (currentDetailIndex < order.Detalles.Count)
                {
                    WorkOrderDetail detail = order.Detalles[currentDetailIndex];
                    SizeF measured = g.MeasureString(detail.Descripcion, regularFont, colDesc - 12);
                    float rowHeight = Math.Max(26, measured.Height + 10);

                    if (y + rowHeight > maxTableBottom && currentDetailIndex > 0)
                    {
                        DrawFooter(g, bounds, smallFont, pageNumber++);
                        e.HasMorePages = true;
                        return;
                    }

                    RectangleF rowRect = new(bounds.Left, y, bounds.Width, rowHeight);
                    if (currentDetailIndex % 2 == 1)
                    {
                        g.FillRectangle(lightBrush, rowRect);
                    }

                    g.DrawRectangle(gridPen, rowRect.X, rowRect.Y, rowRect.Width, rowRect.Height);
                    g.DrawLine(gridPen, bounds.Left + colItem, y, bounds.Left + colItem, y + rowHeight);
                    g.DrawLine(gridPen, bounds.Left + colItem + colCant, y, bounds.Left + colItem + colCant, y + rowHeight);

                    g.DrawString((currentDetailIndex + 1).ToString(), regularFont, Brushes.Black, bounds.Left + 12, y + 5);
                    g.DrawString(detail.Cantidad.ToString(), regularFont, Brushes.Black, bounds.Left + colItem + 12, y + 5);
                    g.DrawString(detail.Descripcion, regularFont, Brushes.Black,
                        new RectangleF(bounds.Left + colItem + colCant + 6, y + 5, colDesc - 12, rowHeight - 6));

                    y += rowHeight;
                    currentDetailIndex++;
                }

                detailsFinished = true;
            }

            // Bloque de Observaciones y Valor Neto
            if (!summaryDrawn)
            {
                string obsText = string.IsNullOrWhiteSpace(order.Observacion) ? "Sin observaciones." : order.Observacion;
                int netBoxW = 190;
                int obsBoxW = bounds.Width - netBoxW - 10;
                SizeF obsSize = g.MeasureString($"Observaciones: {obsText}", regularFont, obsBoxW - 14);
                float summaryH = Math.Max(42f, obsSize.Height + 14f);

                if (y + 10 + summaryH > bounds.Bottom - 36)
                {
                    DrawFooter(g, bounds, smallFont, pageNumber++);
                    e.HasMorePages = true;
                    return;
                }

                y += 10;
                RectangleF obsRect = new(bounds.Left, y, obsBoxW, summaryH);
                RectangleF netRect = new(bounds.Right - netBoxW, y, netBoxW, summaryH);

                g.FillRectangle(lightBrush, obsRect);
                g.DrawRectangle(gridPen, obsRect.X, obsRect.Y, obsRect.Width, obsRect.Height);
                g.DrawString($"Observaciones: {obsText}", regularFont, Brushes.Black,
                    new RectangleF(obsRect.X + 7, obsRect.Y + 6, obsRect.Width - 14, obsRect.Height - 10));

                g.FillRectangle(goldBrush, netRect);
                g.DrawRectangle(olivePen, netRect.X, netRect.Y, netRect.Width, netRect.Height);
                using Font netTitleFont = new("Segoe UI", 8f, FontStyle.Bold);
                using Font netValueFont = new("Segoe UI", 13.5f, FontStyle.Bold);
                using StringFormat rightAlign = new() { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                g.DrawString("VALOR NETO", netTitleFont, navyBrush, netRect.X + 8, netRect.Y + 5);
                g.DrawString($"$ {order.ValorNeto:N0}", netValueFont, navyBrush,
                    new RectangleF(netRect.X + 8, netRect.Y + 18, netRect.Width - 16, summaryH - 22), rightAlign);

                y += summaryH + 6;
                summaryDrawn = true;
            }

            // Pie dinámico de firmas (si está vacío, no se dibuja nada)
            if (slots.Count > 0)
            {
                const float sigBlockHeight = 128f;
                if (y + 18 + sigBlockHeight > bounds.Bottom - 24)
                {
                    DrawFooter(g, bounds, smallFont, pageNumber++);
                    e.HasMorePages = true;
                    return;
                }

                float sigStartY = Math.Max(y + 18, bounds.Bottom - 148);
                int slotWidth = bounds.Width / slots.Count;
                int imgWidth = slots.Count == 3 ? 115 : 130;
                int imgHeight = slots.Count == 3 ? 62 : 70;
                int halfLine = Math.Min(96, (slotWidth / 2) - 10);

                for (int i = 0; i < slots.Count; i++)
                {
                    WorkOrderSignatureSlot slot = slots[i];
                    int centerX = bounds.Left + (slotWidth * i) + (slotWidth / 2);

                    if (!string.IsNullOrEmpty(slot.AssetImagePath) && File.Exists(slot.AssetImagePath))
                    {
                        using Image img = Image.FromFile(slot.AssetImagePath);
                        g.DrawImage(img, new Rectangle(centerX - (imgWidth / 2), (int)sigStartY, imgWidth, imgHeight));
                    }
                    else if (slot.ImageBytes is { Length: > 0 })
                    {
                        using Bitmap? solBmp = SignatureFileService.CreateBitmapFromBytes(slot.ImageBytes);
                        if (solBmp is not null)
                        {
                            g.DrawImage(solBmp, new Rectangle(centerX - (imgWidth / 2), (int)sigStartY + 6, imgWidth, imgHeight - 8));
                        }
                    }

                    float lineY = sigStartY + imgHeight + 4;
                    g.DrawLine(Pens.Black, centerX - halfLine, lineY, centerX + halfLine, lineY);
                    g.DrawString(slot.SignerName, smallBoldFont, Brushes.Black,
                        new RectangleF(bounds.Left + (slotWidth * i) + 4, lineY + 3, slotWidth - 8, 18), center);
                    g.DrawString(slot.RoleTitle, smallFont, Brushes.DimGray,
                        new RectangleF(bounds.Left + (slotWidth * i) + 4, lineY + 19, slotWidth - 8, 16), center);
                }
            }

            DrawFooter(g, bounds, smallFont, pageNumber);
            e.HasMorePages = false;
        };

        return doc;
    }

    public PrintDocument CreateUsersReportPrintDocument(IReadOnlyList<User> users)
    {
        int index = 0;
        int page = 1;
        PrintDocument doc = new() { DocumentName = "Listado_Usuarios_SISPRO" };

        doc.BeginPrint += (_, _) =>
        {
            index = 0;
            page = 1;
        };

        doc.PrintPage += (_, e) =>
        {
            Graphics? g = e.Graphics;
            if (g is null)
            {
                e.HasMorePages = false;
                return;
            }

            Rectangle bounds = e.MarginBounds;
            float y = bounds.Top;
            using Font titleFont = new("Segoe UI", 13, FontStyle.Bold);
            using Font boldFont = new("Segoe UI", 8.5f, FontStyle.Bold);
            using Font regularFont = new("Segoe UI", 8.5f, FontStyle.Regular);
            using Font smallFont = new("Segoe UI", 8, FontStyle.Regular);
            using SolidBrush oliveBrush = new(CambiasoGreen);

            g.DrawString($"{settings.CompanyName} — Listado de Usuarios SISPRO", titleFont, oliveBrush, bounds.Left, y);
            y += 26;
            g.DrawString($"Emitido: {DateTime.Now:dd-MM-yyyy HH:mm}   |   Total registros: {users.Count}", smallFont, Brushes.DimGray, bounds.Left, y);
            y += 22;

            g.FillRectangle(oliveBrush, bounds.Left, y, bounds.Width, 22);
            g.DrawString("ID", boldFont, Brushes.White, bounds.Left + 4, y + 4);
            g.DrawString("Usuario", boldFont, Brushes.White, bounds.Left + 38, y + 4);
            g.DrawString("Nombre Completo", boldFont, Brushes.White, bounds.Left + 130, y + 4);
            g.DrawString("Perfil", boldFont, Brushes.White, bounds.Left + 300, y + 4);
            g.DrawString("Grupo", boldFont, Brushes.White, bounds.Left + 388, y + 4);
            g.DrawString("Estado", boldFont, Brushes.White, bounds.Left + 485, y + 4);
            g.DrawString("Correo", boldFont, Brushes.White, bounds.Left + 545, y + 4);
            y += 24;

            while (index < users.Count)
            {
                if (y + 22 > bounds.Bottom - 30)
                {
                    DrawFooter(g, bounds, smallFont, page++);
                    e.HasMorePages = true;
                    return;
                }

                User u = users[index];
                g.DrawString(u.CodigoId.ToString(), regularFont, Brushes.Black, bounds.Left + 4, y + 2);
                g.DrawString(u.AliasUsuario, regularFont, Brushes.Black, bounds.Left + 38, y + 2);
                g.DrawString(u.NombreCompleto, regularFont, Brushes.Black, new RectangleF(bounds.Left + 130, y + 2, 166, 18));
                g.DrawString(u.Nivel.ToString(), regularFont, Brushes.Black, bounds.Left + 300, y + 2);
                g.DrawString(string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo, regularFont, Brushes.Black, new RectangleF(bounds.Left + 388, y + 2, 94, 18));
                g.DrawString(u.Activo ? "Activo" : "Inactivo", regularFont, u.Activo ? Brushes.DarkGreen : Brushes.Firebrick, bounds.Left + 485, y + 2);
                g.DrawString(u.MailUsuario, regularFont, Brushes.Black, new RectangleF(bounds.Left + 545, y + 2, Math.Max(80, bounds.Right - (bounds.Left + 545)), 18));
                g.DrawLine(Pens.LightGray, bounds.Left, y + 20, bounds.Right, y + 20);

                y += 22;
                index++;
            }

            DrawFooter(g, bounds, smallFont, page);
            e.HasMorePages = false;
        };

        return doc;
    }

    private static int MeasureInfoCardHeight(
        Graphics g,
        int cardWidth,
        Font regularFont,
        (string Label, string Value)[] rows)
    {
        float valWidth = Math.Max(60, cardWidth - 102);
        float total = 28f;
        foreach ((string _, string value) in rows)
        {
            SizeF size = g.MeasureString(string.IsNullOrEmpty(value) ? " " : value, regularFont, (int)valWidth);
            total += Math.Max(16f, size.Height + 2f);
        }

        return Math.Max(88, (int)Math.Ceiling(total) + 6);
    }

    private static void DrawInfoCard(
        Graphics g,
        Rectangle rect,
        string title,
        Brush headerBrush,
        Pen borderPen,
        Font boldFont,
        Font regularFont,
        (string Label, string Value)[] rows)
    {
        g.DrawRectangle(borderPen, rect);
        g.FillRectangle(headerBrush, rect.X, rect.Y, rect.Width, 20);
        g.DrawString(title, boldFont, Brushes.White, rect.X + 6, rect.Y + 2);

        float valWidth = Math.Max(60, rect.Width - 102);
        float rowY = rect.Y + 24;
        foreach ((string label, string value) in rows)
        {
            g.DrawString(label, boldFont, Brushes.Black, rect.X + 6, rowY);
            SizeF size = g.MeasureString(string.IsNullOrEmpty(value) ? " " : value, regularFont, (int)valWidth);
            float h = Math.Max(16f, size.Height + 2f);
            g.DrawString(value, regularFont, Brushes.Black, new RectangleF(rect.X + 96, rowY, valWidth, h));
            rowY += h;
        }
    }

    private void DrawFooter(Graphics g, Rectangle bounds, Font smallFont, int pageNumber)
    {
        g.DrawLine(Pens.LightGray, bounds.Left, bounds.Bottom - 20, bounds.Right, bounds.Bottom - 20);
        g.DrawString($"SISPRO v4.4 — {settings.CompanyName} ({DateTime.Now:dd-MM-yyyy HH:mm})", smallFont, Brushes.Gray, bounds.Left, bounds.Bottom - 16);
        using StringFormat right = new() { Alignment = StringAlignment.Far };
        g.DrawString($"Página {pageNumber}", smallFont, Brushes.Gray, new RectangleF(bounds.Left, bounds.Bottom - 16, bounds.Width, 16), right);
    }
}
