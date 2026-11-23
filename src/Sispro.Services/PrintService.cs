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
    private static readonly Color CambiasoGreen = Color.FromArgb(14, 65, 99);
    private static readonly Color SubtleFill = Color.FromArgb(235, 244, 250);

    public PrintDocument CreateWorkOrderPrintDocument(
        WorkOrder order,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        string assetsDir = assetsDirectory ?? Path.Combine(AppContext.BaseDirectory, "Assets");
        int currentDetailIndex = 0;
        int pageNumber = 1;

        PrintDocument doc = new()
        {
            DocumentName = $"OT_{order.NumeroOt:D6}_Cambiaso"
        };

        doc.BeginPrint += (_, _) =>
        {
            currentDetailIndex = 0;
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

            using Font titleFont = new("Segoe UI", 13, FontStyle.Bold);
            using Font subtitleFont = new("Segoe UI", 9.5f, FontStyle.Bold);
            using Font boldFont = new("Segoe UI", 9, FontStyle.Bold);
            using Font regularFont = new("Segoe UI", 9, FontStyle.Regular);
            using Font smallFont = new("Segoe UI", 8, FontStyle.Regular);
            using Pen olivePen = new(CambiasoGreen, 1.5f);
            using Pen gridPen = new(Color.FromArgb(180, 188, 165), 0.75f);
            using SolidBrush oliveBrush = new(CambiasoGreen);
            using SolidBrush lightBrush = new(SubtleFill);

            // Header Logo + Box
            string logoPath = Path.Combine(assetsDir, "logo_cambiaso.png");
            if (File.Exists(logoPath))
            {
                using Image logo = Image.FromFile(logoPath);
                g.DrawImage(logo, new Rectangle(bounds.Left, (int)y, 175, 86));
            }
            else
            {
                g.DrawString(settings.CompanyName, titleFont, oliveBrush, bounds.Left, y + 14);
            }

            Rectangle headerBox = new(bounds.Right - 270, (int)y, 270, 88);
            g.DrawRectangle(olivePen, headerBox);
            using StringFormat center = new() { Alignment = StringAlignment.Center };
            g.DrawString($"RUT: {settings.CompanyRut}", boldFont, Brushes.Black, new RectangleF(headerBox.X, headerBox.Y + 5, headerBox.Width, 18), center);
            g.FillRectangle(oliveBrush, headerBox.X + 1, headerBox.Y + 23, headerBox.Width - 2, 20);
            g.DrawString("ORDEN DE TRABAJO EXTERNA", subtitleFont, Brushes.White, new RectangleF(headerBox.X, headerBox.Y + 25, headerBox.Width, 18), center);
            g.DrawString(settings.CompanyDepartment, smallFont, Brushes.Black, new RectangleF(headerBox.X, headerBox.Y + 46, headerBox.Width, 16), center);
            g.DrawString($"N° OT : {order.NumeroOt:D6}", titleFont, Brushes.DarkRed, new RectangleF(headerBox.X, headerBox.Y + 62, headerBox.Width, 24), center);

            y += 102;

            // Info cards
            int halfWidth = (bounds.Width - 12) / 2;
            Rectangle leftCard = new(bounds.Left, (int)y, halfWidth, 92);
            Rectangle rightCard = new(bounds.Left + halfWidth + 12, (int)y, halfWidth, 92);

            DrawInfoCard(g, leftCard, "PROVEEDOR", oliveBrush, olivePen, boldFont, regularFont,
            [
                ("Señor(es):", order.NombreProveedor),
                ("Dirección:", order.DireccionProveedor),
                ("Correo:", order.CorreoProveedor),
                ("Activo:", order.RutaActivo)
            ]);

            DrawInfoCard(g, rightCard, "SOLICITANTE", oliveBrush, olivePen, boldFont, regularFont,
            [
                ("Solicitado por:", order.AliasSolicitante),
                ("Fecha:", order.FechaOt.ToString("dd-MM-yyyy")),
                ("Correo / Fono:", $"{order.CorreoSolicitante}  {order.TelefonoSolicitante}".Trim()),
                ("Dirección:", settings.CompanyAddress)
            ]);

            y += 106;

            // Table Header
            int colItem = 42;
            int colCant = 52;
            int colVal = 90;
            int colDesc = bounds.Width - colItem - colCant - colVal;

            Rectangle tblHdr = new(bounds.Left, (int)y, bounds.Width, 24);
            g.FillRectangle(oliveBrush, tblHdr);
            g.DrawString("Ítem", boldFont, Brushes.White, bounds.Left + 6, y + 4);
            g.DrawString("Cant.", boldFont, Brushes.White, bounds.Left + colItem + 6, y + 4);
            g.DrawString("DETALLE DE PIEZA EN REPARACIÓN Y TRABAJO", boldFont, Brushes.White, bounds.Left + colItem + colCant + 6, y + 4);
            g.DrawString("Valor / Ref.", boldFont, Brushes.White, bounds.Right - colVal + 6, y + 4);
            y += 24;

            float signatureReserve = 165;
            float maxTableBottom = bounds.Bottom - signatureReserve;

            while (currentDetailIndex < order.Detalles.Count)
            {
                WorkOrderDetail detail = order.Detalles[currentDetailIndex];
                SizeF measured = g.MeasureString(detail.Descripcion, regularFont, colDesc - 10);
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
                g.DrawLine(gridPen, bounds.Right - colVal, y, bounds.Right - colVal, y + rowHeight);

                g.DrawString((currentDetailIndex + 1).ToString(), regularFont, Brushes.Black, bounds.Left + 12, y + 5);
                g.DrawString(detail.Cantidad.ToString(), regularFont, Brushes.Black, bounds.Left + colItem + 12, y + 5);
                g.DrawString(detail.Descripcion, regularFont, Brushes.Black,
                    new RectangleF(bounds.Left + colItem + colCant + 5, y + 5, colDesc - 10, rowHeight - 6));

                y += rowHeight;
                currentDetailIndex++;
            }

            // Approvals summary + signatures
            y = Math.Max(y + 18, bounds.Bottom - signatureReserve + 10);
            string vbLine = $"VB Supervisor: {(order.TieneVbs ? "APROBADO" : "Pendiente")}   |   VB Jefatura: {(order.TieneVbj ? "APROBADO" : "Pendiente")}";
            g.DrawString(vbLine, smallFont, Brushes.DimGray, bounds.Left, y);
            y += 18;

            string jrPath = Path.Combine(assetsDir, "Firmas", "JR.jpg");
            string msPath = Path.Combine(assetsDir, "Firmas", "MS.jpg");

            int leftSigCenter = bounds.Left + (bounds.Width / 4);
            int rightSigCenter = bounds.Left + (3 * bounds.Width / 4);

            if (signatureOption is SignatureOption.JuanRamos or SignatureOption.Ambas && File.Exists(jrPath))
            {
                using Image jr = Image.FromFile(jrPath);
                g.DrawImage(jr, new Rectangle(leftSigCenter - 65, (int)y, 130, 75));
            }

            if (signatureOption is SignatureOption.MarceloSilva or SignatureOption.Ambas && File.Exists(msPath))
            {
                using Image ms = Image.FromFile(msPath);
                g.DrawImage(ms, new Rectangle(rightSigCenter - 65, (int)y, 130, 75));
            }

            float sigLineY = y + 82;
            g.DrawLine(Pens.Black, leftSigCenter - 110, sigLineY, leftSigCenter + 110, sigLineY);
            g.DrawLine(Pens.Black, rightSigCenter - 110, sigLineY, rightSigCenter + 110, sigLineY);
            g.DrawString("Juan Ramos Moyano\nJefatura Mantención", smallFont, Brushes.Black,
                new RectangleF(leftSigCenter - 120, sigLineY + 4, 240, 32), center);
            g.DrawString("Marcelo Silva Morgado\nSupervisor Mantención", smallFont, Brushes.Black,
                new RectangleF(rightSigCenter - 120, sigLineY + 4, 240, 32), center);

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
            g.DrawString("Usuario", boldFont, Brushes.White, bounds.Left + 42, y + 4);
            g.DrawString("Nombre Completo", boldFont, Brushes.White, bounds.Left + 148, y + 4);
            g.DrawString("Perfil", boldFont, Brushes.White, bounds.Left + 345, y + 4);
            g.DrawString("Estado", boldFont, Brushes.White, bounds.Left + 440, y + 4);
            g.DrawString("Correo", boldFont, Brushes.White, bounds.Left + 510, y + 4);
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
                g.DrawString(u.AliasUsuario, regularFont, Brushes.Black, bounds.Left + 42, y + 2);
                g.DrawString(u.NombreCompleto, regularFont, Brushes.Black, bounds.Left + 148, y + 2);
                g.DrawString(u.Nivel.ToString(), regularFont, Brushes.Black, bounds.Left + 345, y + 2);
                g.DrawString(u.Activo ? "Activo" : "Inactivo", regularFont, u.Activo ? Brushes.DarkGreen : Brushes.Firebrick, bounds.Left + 440, y + 2);
                g.DrawString(u.MailUsuario, regularFont, Brushes.Black, bounds.Left + 510, y + 2);
                g.DrawLine(Pens.LightGray, bounds.Left, y + 20, bounds.Right, y + 20);

                y += 22;
                index++;
            }

            DrawFooter(g, bounds, smallFont, page);
            e.HasMorePages = false;
        };

        return doc;
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

        float rowY = rect.Y + 24;
        foreach ((string label, string value) in rows)
        {
            g.DrawString(label, boldFont, Brushes.Black, rect.X + 6, rowY);
            g.DrawString(value, regularFont, Brushes.Black, new RectangleF(rect.X + 94, rowY, rect.Width - 98, 16));
            rowY += 16;
        }
    }

    private static void DrawFooter(Graphics g, Rectangle bounds, Font smallFont, int pageNumber)
    {
        g.DrawLine(Pens.LightGray, bounds.Left, bounds.Bottom - 20, bounds.Right, bounds.Bottom - 20);
        g.DrawString($"SISPRO v2.0 — Cambiaso Hnos S.A.C. ({DateTime.Now:dd-MM-yyyy HH:mm})", smallFont, Brushes.Gray, bounds.Left, bounds.Bottom - 16);
        using StringFormat right = new() { Alignment = StringAlignment.Far };
        g.DrawString($"Página {pageNumber}", smallFont, Brushes.Gray, new RectangleF(bounds.Left, bounds.Bottom - 16, bounds.Width, 16), right);
    }
}
