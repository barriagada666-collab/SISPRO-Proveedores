using System.Globalization;
using System.Text;
using Sispro.Core.Dtos;

namespace Sispro.Services;

/// <summary>
/// Exportación limpia a CSV (UTF-8 con BOM, compatible con Excel en español y herramientas BI).
/// Preserva estructura tabular, delimitadores RFC-4180 y orden exacto sin elementos gráficos.
/// </summary>
public sealed class CsvExportService
{
    private static readonly CultureInfo ClCulture = new("es-CL");

    public void ExportTabularReport(TabularReportData report, string outputPath, char delimiter = ';')
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string? dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using StreamWriter writer = new(outputPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        // Encabezados
        writer.WriteLine(string.Join(delimiter, report.Columns.Select(c => EscapeField(c.Header, delimiter))));

        // Filas preservando el orden y filtros exactos recibidos
        foreach (object?[] row in report.Rows)
        {
            string[] formatted = new string[report.Columns.Count];
            for (int c = 0; c < report.Columns.Count; c++)
            {
                object? val = c < row.Length ? row[c] : null;
                formatted[c] = EscapeField(FormatCellValue(val, report.Columns[c].Kind), delimiter);
            }

            writer.WriteLine(string.Join(delimiter, formatted));
        }
    }

    public static string FormatCellValue(object? val, ExportColumnKind kind)
    {
        if (val is null)
        {
            return string.Empty;
        }

        return kind switch
        {
            ExportColumnKind.Date when val is DateTime dt => dt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture),
            ExportColumnKind.DateTime when val is DateTime dt => dt.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture),
            ExportColumnKind.Currency when val is decimal dec => dec.ToString("0", CultureInfo.InvariantCulture),
            ExportColumnKind.Currency when val is double dbl => dbl.ToString("0", CultureInfo.InvariantCulture),
            ExportColumnKind.Decimal when val is IFormattable fmt => fmt.ToString("0.##", ClCulture),
            ExportColumnKind.Integer when val is IFormattable fmt => fmt.ToString("0", CultureInfo.InvariantCulture),
            _ => Convert.ToString(val, ClCulture) ?? string.Empty
        };
    }

    private static string EscapeField(string value, char delimiter)
    {
        string normalized = value.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
        bool needsQuotes = normalized.Contains(delimiter) || normalized.Contains('"') || normalized.Contains(',');
        if (!needsQuotes)
        {
            return normalized;
        }

        return $"\"{normalized.Replace("\"", "\"\"")}\"";
    }
}
