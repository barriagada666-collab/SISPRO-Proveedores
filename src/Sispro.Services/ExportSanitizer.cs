using System.Globalization;
using System.Text;
using Sispro.Core.Dtos;

namespace Sispro.Services;

/// <summary>
/// Utilidades internas compartidas entre los servicios de exportación (Excel, PDF, Word y CSV)
/// para saneamiento XML/WinAnsi, creación segura de directorios y conversión numérica uniforme.
/// </summary>
internal static class ExportSanitizer
{
    private static readonly Lazy<Encoding> LazyWin1252 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            1252,
            new EncoderReplacementFallback("?"),
            new DecoderReplacementFallback("?"));
    });

    internal static Encoding WinAnsi1252 => LazyWin1252.Value;

    internal static void EnsureOutputDirectory(string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        string? dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    internal static string SanitizeXmlText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsClean = false;
        foreach (char c in value)
        {
            if (IsDisallowedXmlChar(c))
            {
                needsClean = true;
                break;
            }
        }

        if (!needsClean)
        {
            return value;
        }

        StringBuilder sb = new(value.Length);
        foreach (char c in value)
        {
            if (!IsDisallowedXmlChar(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    internal static bool TryToDecimal(object? val, out decimal result)
    {
        switch (val)
        {
            case decimal dec:
                result = dec;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
            case short s:
                result = s;
                return true;
            case byte b:
                result = b;
                return true;
            case double dbl:
                result = Convert.ToDecimal(dbl, CultureInfo.InvariantCulture);
                return true;
            case float flt:
                result = Convert.ToDecimal(flt, CultureInfo.InvariantCulture);
                return true;
            default:
                result = 0m;
                return false;
        }
    }

    internal static string FormatDisplayValue(object? val, ExportColumnKind kind, CultureInfo culture)
    {
        if (val is null)
        {
            return string.Empty;
        }

        string formatted = kind switch
        {
            ExportColumnKind.Date when val is DateTime dt => dt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
            ExportColumnKind.DateTime when val is DateTime dt => dt.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
            ExportColumnKind.Currency when TryToDecimal(val, out decimal d) => $"${d.ToString("#,##0", culture)}",
            ExportColumnKind.Integer when TryToDecimal(val, out decimal d) => d.ToString("0", CultureInfo.InvariantCulture),
            ExportColumnKind.Decimal when TryToDecimal(val, out decimal d) => d.ToString("#,##0.##", culture),
            _ => Convert.ToString(val, culture) ?? string.Empty
        };

        return SanitizeXmlText(formatted);
    }

    private static bool IsDisallowedXmlChar(char c) =>
        (c < '\x20' && c != '\t' && c != '\r' && c != '\n') || c == '\uFFFE' || c == '\uFFFF';
}
