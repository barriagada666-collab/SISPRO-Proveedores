using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace Sispro.Services;

public sealed record ProcessedSignature(
    byte[] RawBytes,
    string FileName,
    string FileType,
    byte[] PreviewPngBytes);

/// <summary>
/// Procesa, valida y genera vista previa PNG normalizada para firmas digitales de solicitantes
/// en formatos PNG, JPG y PDF (renderizado nativo de página 1 mediante Windows.Data.Pdf).
/// </summary>
public static class SignatureFileService
{
    public const int MaxFileSizeBytes = 10 * 1024 * 1024;
    private const int PreviewMaxWidth = 420;
    private const int PreviewMaxHeight = 180;

    public static readonly IReadOnlyList<string> AllowedExtensions = [".png", ".jpg", ".jpeg", ".pdf"];

    public static ProcessedSignature ProcessFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("El archivo de firma seleccionado no existe.", filePath);
        }

        FileInfo info = new(filePath);
        if (info.Length == 0)
        {
            throw new ArgumentException("El archivo de firma está vacío.");
        }

        if (info.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("El archivo de firma supera el límite máximo permitido de 10 MB.");
        }

        byte[] raw = File.ReadAllBytes(filePath);
        return ProcessBytes(info.Name, raw);
    }

    public static ProcessedSignature ProcessBytes(string fileName, byte[] rawBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Debe indicar el nombre del archivo de firma.");
        }

        if (rawBytes is null || rawBytes.Length == 0)
        {
            throw new ArgumentException("El contenido de la firma está vacío.");
        }

        if (rawBytes.Length > MaxFileSizeBytes)
        {
            throw new ArgumentException("El archivo de firma supera el límite máximo permitido de 10 MB.");
        }

        string safeName = Path.GetFileName(fileName.Trim());
        string ext = Path.GetExtension(safeName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException("Formato de firma no soportado. Use un archivo PNG, JPG o PDF.");
        }

        if (ext == ".pdf")
        {
            byte[] pdfPreview = RenderPdfFirstPageToPreviewPng(rawBytes);
            return new ProcessedSignature(rawBytes, safeName, "PDF", pdfPreview);
        }

        string tipo = ext == ".png" ? "PNG" : "JPG";
        byte[] imgPreview = RenderImageToPreviewPng(rawBytes, preserveTransparency: ext == ".png");
        return new ProcessedSignature(rawBytes, safeName, tipo, imgPreview);
    }

    public static Bitmap? CreateBitmapFromBytes(byte[]? pngOrImageBytes)
    {
        if (pngOrImageBytes is null || pngOrImageBytes.Length == 0)
        {
            return null;
        }

        using MemoryStream ms = new(pngOrImageBytes);
        using Image decoded = Image.FromStream(ms);
        return new Bitmap(decoded);
    }

    private static byte[] RenderImageToPreviewPng(byte[] rawBytes, bool preserveTransparency)
    {
        try
        {
            using MemoryStream ms = new(rawBytes);
            using Image img = Image.FromStream(ms, useEmbeddedColorManagement: true, validateImageData: true);
            if (img.Width <= 0 || img.Height <= 0)
            {
                throw new ArgumentException("La imagen de firma no tiene dimensiones válidas.");
            }

            return ScaleToPng(img, preserveTransparency);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("No se pudo leer la imagen de firma. Verifique que sea un PNG o JPG válido.", ex);
        }
    }

    private static byte[] RenderPdfFirstPageToPreviewPng(byte[] rawBytes)
    {
        string header = Encoding.ASCII.GetString(rawBytes, 0, Math.Min(8, rawBytes.Length));
        if (!header.StartsWith("%PDF-", StringComparison.Ordinal))
        {
            throw new ArgumentException("El archivo seleccionado no corresponde a un documento PDF válido (%PDF-).");
        }

        try
        {
            byte[] renderedPng = Task.Run(async () =>
            {
                using InMemoryRandomAccessStream pdfStream = new();
                await pdfStream.WriteAsync(rawBytes.AsBuffer());
                pdfStream.Seek(0);

                PdfDocument pdfDoc = await PdfDocument.LoadFromStreamAsync(pdfStream);
                if (pdfDoc.PageCount == 0)
                {
                    throw new ArgumentException("El documento PDF no contiene páginas para extraer la firma.");
                }

                using PdfPage page = pdfDoc.GetPage(0);
                double width = Math.Max(1.0, page.Size.Width);
                double height = Math.Max(1.0, page.Size.Height);
                uint targetW = (uint)Math.Clamp(width * 2.0, 420.0, 1200.0);
                uint targetH = Math.Max(1u, (uint)Math.Round(targetW * (height / width)));

                using InMemoryRandomAccessStream outStream = new();
                PdfPageRenderOptions options = new()
                {
                    DestinationWidth = targetW,
                    DestinationHeight = targetH,
                    BitmapEncoderId = Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId,
                    BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255)
                };

                await page.RenderToStreamAsync(outStream, options);
                outStream.Seek(0);

                using Stream netStream = outStream.AsStreamForRead();
                using MemoryStream buffer = new();
                await netStream.CopyToAsync(buffer);
                return buffer.ToArray();
            }).GetAwaiter().GetResult();

            using MemoryStream renderedMs = new(renderedPng);
            using Image renderedImg = Image.FromStream(renderedMs);
            return ScaleToPng(renderedImg, preserveTransparency: false);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ArgumentException("No se pudo renderizar la primera página del PDF adjunto. Verifique que no esté cifrado o dañado.", ex);
        }
    }

    private static byte[] ScaleToPng(Image source, bool preserveTransparency)
    {
        double ratio = Math.Min((double)PreviewMaxWidth / source.Width, (double)PreviewMaxHeight / source.Height);
        ratio = Math.Min(ratio, 1.0);
        int targetW = Math.Max(1, (int)Math.Round(source.Width * ratio));
        int targetH = Math.Max(1, (int)Math.Round(source.Height * ratio));

        using Bitmap canvas = new(targetW, targetH, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.Clear(preserveTransparency ? Color.Transparent : Color.White);
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.DrawImage(source, new Rectangle(0, 0, targetW, targetH));
        }

        using MemoryStream outMs = new();
        canvas.Save(outMs, ImageFormat.Png);
        return outMs.ToArray();
    }
}
