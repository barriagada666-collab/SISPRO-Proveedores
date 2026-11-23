namespace Sispro.Core.Dtos;

public enum ExportColumnKind
{
    Text = 0,
    Integer = 1,
    Decimal = 2,
    Currency = 3,
    Date = 4,
    DateTime = 5
}

public sealed record ExportColumn(
    string Header,
    ExportColumnKind Kind = ExportColumnKind.Text,
    double WidthWeight = 1.0,
    bool SumInFooter = false);

public sealed class TabularReportData
{
    public required string Title { get; init; }
    public string Subtitle { get; init; } = string.Empty;
    public string SheetName { get; init; } = "Reporte";
    public string FilterSummary { get; init; } = "Sin filtros adicionales (orden cronológico descendente)";
    public bool Landscape { get; init; } = true;
    public IReadOnlyList<ExportColumn> Columns { get; init; } = [];
    public IReadOnlyList<object?[]> Rows { get; init; } = [];
    public string FooterNote { get; init; } = string.Empty;
}
