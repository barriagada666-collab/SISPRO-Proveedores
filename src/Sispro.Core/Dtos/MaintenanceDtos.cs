namespace Sispro.Core.Dtos;

public sealed record ChartDataPoint(string Label, double Value);

public sealed class DashboardAnalytics
{
    public DashboardMetrics Metrics { get; init; } = new();
    public IReadOnlyList<ChartDataPoint> WorkOrdersByYear { get; init; } = [];
    public IReadOnlyList<ChartDataPoint> WorkOrdersByPlant { get; init; } = [];
    public IReadOnlyList<ChartDataPoint> TopProviders { get; init; } = [];
    public IReadOnlyList<ChartDataPoint> MaintenanceByTrade { get; init; } = [];
}

public sealed class MantWorkOrderFilter
{
    public string? TextoBusqueda { get; set; }
    public string? Oficio { get; set; }
    public string? Estado { get; set; }
    public int? Anio { get; set; }
    public int MaxRows { get; set; } = 300;
}

public sealed class MantWorkOrderRow
{
    public int Id { get; init; }
    public string NumeroOt { get; init; } = string.Empty;
    public string FechaInicio { get; init; } = string.Empty;
    public string FechaFin { get; init; } = string.Empty;
    public string Turno { get; init; } = string.Empty;
    public string Oficio { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public string Planta { get; init; } = string.Empty;
    public string Equipo { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string Detencion { get; init; } = string.Empty;
    public string SolicitadaPor { get; init; } = string.Empty;
    public string DescripcionBreve { get; init; } = string.Empty;
    public string DescripcionTecnica { get; init; } = string.Empty;
    public int Prioridad { get; init; }
}

public sealed class MantWorkOrderDetail
{
    public required MantWorkOrderRow Order { get; init; }
    public IReadOnlyList<string> TecnicosAsignados { get; init; } = [];
    public IReadOnlyList<string> InsumosConsumidos { get; init; } = [];
}

public sealed class MantPreventivePlanRow
{
    public int Id { get; init; }
    public string Planta { get; init; } = string.Empty;
    public string Equipo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public string TipoMantenimiento { get; init; } = string.Empty;
    public string Frecuencia { get; init; } = string.Empty;
    public int Prioridad { get; init; }
    public int TotalCiclos { get; init; }
    public int TotalTareas { get; init; }
}

public sealed class MantPreventiveCycleRow
{
    public int IdCiclo { get; init; }
    public string FechaInicio { get; init; } = string.Empty;
    public string FechaCierre { get; init; } = string.Empty;
    public double Tiempo { get; init; }
    public string Estado { get; init; } = string.Empty;
    public string Observacion { get; init; } = string.Empty;
}

public sealed class MantPreventiveTaskRow
{
    public int IdTarea { get; init; }
    public int IdCiclo { get; init; }
    public string Descripcion { get; init; } = string.Empty;
    public string Estado { get; init; } = string.Empty;
    public double Duracion { get; init; }
    public string Oficio { get; init; } = string.Empty;
    public string Observacion { get; init; } = string.Empty;
}

public sealed class MantSupplyRow
{
    public int IdItem { get; init; }
    public string CodSap { get; init; } = string.Empty;
    public string CodProveedor { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public double StockUnidad { get; init; }
    public int DiasEntrega { get; init; }
    public int Prioridad { get; init; }
    public int UsosHistoricos { get; init; }
}

public sealed class MantPersonnelRow
{
    public int IdPersonal { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Rut { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Oficio { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Celular { get; init; } = string.Empty;
    public string Departamento { get; init; } = string.Empty;
    public string CentroTrabajo { get; init; } = string.Empty;
    public int OrdenesAtendidas { get; init; }
}

public sealed class MantHrPersonnelRow
{
    public string IdEmpleado { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string FechaAlta { get; init; } = string.Empty;
    public string Puesto { get; init; } = string.Empty;
    public string Departamento { get; init; } = string.Empty;
    public string CentroCosto { get; init; } = string.Empty;
    public string CentroTrabajo { get; init; } = string.Empty;
}

public sealed class MantExternalOrderRow
{
    public int IdOte { get; init; }
    public int NumeroOte { get; init; }
    public string FechaPedido { get; init; } = string.Empty;
    public string FechaEntrega { get; init; } = string.Empty;
    public string Empresa { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public decimal Costo { get; init; }
    public string Garantia { get; init; } = string.Empty;
}

public sealed class MantCatalogItem
{
    public int Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Detalle { get; init; } = string.Empty;
}
