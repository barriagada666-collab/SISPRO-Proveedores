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

public enum WorkOrderExportField
{
    NumeroOt = 0,
    Fecha = 1,
    TipoOrden = 2,
    Proveedor = 3,
    Solicitante = 4,
    Cantidad = 5,
    Detalle = 6,
    RutaActivo = 7,
    Observacion = 8,
    ValorNeto = 9,
    Estado = 10,
    Firmas = 11
}

public static class WorkOrderExportFieldCatalog
{
    public static readonly WorkOrderExportField[] DefaultSearchGridFields =
    [
        WorkOrderExportField.NumeroOt,
        WorkOrderExportField.Fecha,
        WorkOrderExportField.Proveedor,
        WorkOrderExportField.Solicitante,
        WorkOrderExportField.Cantidad,
        WorkOrderExportField.Detalle,
        WorkOrderExportField.Observacion,
        WorkOrderExportField.ValorNeto,
        WorkOrderExportField.Estado,
        WorkOrderExportField.Firmas
    ];

    public static readonly WorkOrderExportField[] EssentialFields =
    [
        WorkOrderExportField.NumeroOt,
        WorkOrderExportField.Fecha,
        WorkOrderExportField.Proveedor,
        WorkOrderExportField.Solicitante,
        WorkOrderExportField.Detalle,
        WorkOrderExportField.ValorNeto,
        WorkOrderExportField.Estado
    ];

    public static readonly WorkOrderExportField[] AllFields =
    [
        WorkOrderExportField.NumeroOt,
        WorkOrderExportField.Fecha,
        WorkOrderExportField.TipoOrden,
        WorkOrderExportField.Proveedor,
        WorkOrderExportField.Solicitante,
        WorkOrderExportField.Cantidad,
        WorkOrderExportField.Detalle,
        WorkOrderExportField.RutaActivo,
        WorkOrderExportField.Observacion,
        WorkOrderExportField.ValorNeto,
        WorkOrderExportField.Estado,
        WorkOrderExportField.Firmas
    ];

    public static string GetDisplayName(WorkOrderExportField field) => field switch
    {
        WorkOrderExportField.NumeroOt => "N° OT",
        WorkOrderExportField.Fecha => "Fecha",
        WorkOrderExportField.TipoOrden => "Tipo OT",
        WorkOrderExportField.Proveedor => "Proveedor",
        WorkOrderExportField.Solicitante => "Solicitante",
        WorkOrderExportField.Cantidad => "Cant.",
        WorkOrderExportField.Detalle => "Detalle",
        WorkOrderExportField.RutaActivo => "Ruta Activo",
        WorkOrderExportField.Observacion => "Observación",
        WorkOrderExportField.ValorNeto => "Valor Neto",
        WorkOrderExportField.Estado => "Estado",
        WorkOrderExportField.Firmas => "Firmas",
        _ => field.ToString()
    };
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
