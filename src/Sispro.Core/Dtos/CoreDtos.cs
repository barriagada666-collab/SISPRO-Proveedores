using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Core.Dtos;

public sealed class UserSession
{
    public required User Usuario { get; init; }
    public DateTime InicioSesion { get; init; } = DateTime.Now;
    public UserPermissions Permisos => Usuario.Permisos;
}

public sealed class AuthResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public UserSession? Session { get; init; }
    public bool PasswordUpgraded { get; init; }
    public bool RequiresPasswordChange { get; init; }

    public static AuthResult Ok(UserSession session, bool passwordUpgraded = false, bool requiresPasswordChange = false) =>
        new()
        {
            Success = true,
            Session = session,
            PasswordUpgraded = passwordUpgraded,
            RequiresPasswordChange = requiresPasswordChange
        };

    public static AuthResult Fail(string message) =>
        new() { Success = false, Message = message };
}

public sealed class UserSearchFilter
{
    public string? TextoBusqueda { get; set; }
    public UserRoleLevel? Perfil { get; set; }
    public int? IdNivelPerfil { get; set; }
    public int? IdGrupo { get; set; }
    public bool SoloSinGrupo { get; set; }
    public bool? Activo { get; set; }
    public DateTime? FechaCreacionInicio { get; set; }
    public DateTime? FechaCreacionFin { get; set; }
}

public sealed class InitialAdminSeedOptions
{
    public required string AliasUsuario { get; init; }
    public string? PlainPassword { get; init; }
    public string? PrecomputedPasswordHash { get; init; }
    public string Nombre { get; init; } = "Administrador";
    public string ApellidoPaterno { get; init; } = "Sistema";
    public string ApellidoMaterno { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public bool RequirePasswordChange { get; init; } = true;
}

public sealed class WorkOrderSearchFilter
{
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public int? IdProv { get; set; }
    public int? IdSol { get; set; }
    public WorkOrderStatus? Estado { get; set; }
    public string? TextoBusqueda { get; set; }
    public int? NumeroOt { get; set; }
    public int? AssetLevel { get; set; }
    public int? AssetNodeId { get; set; }
    public bool SoloPendientesVb { get; set; }
    public int? MaxRows { get; set; }
}

public sealed class WorkOrderSearchRow
{
    public int IdOt { get; set; }
    public int NumeroOt { get; set; }
    public string TipoOrden { get; set; } = "Externa";
    public string Proveedor { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int Cantidad { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
    public decimal ValorNeto { get; set; }
    public WorkOrderStatus Estado { get; set; } = WorkOrderStatus.Abierta;
    public SignatureOption FirmasSeleccionadas { get; set; } = SignatureOption.AmbasConSolicitante;
    public string RutaActivo { get; set; } = string.Empty;
    public bool TieneVbs { get; set; }
    public string VbsUsuario { get; set; } = string.Empty;
    public DateTime? VbsFecha { get; set; }
    public bool TieneVbj { get; set; }
    public string VbjUsuario { get; set; } = string.Empty;
    public DateTime? VbjFecha { get; set; }
    public int IdDet { get; set; }

    public string EstadoDisplay => Estado.ToString();

    public string FirmasDisplay => FirmasSeleccionadas.FormatResumen();

    public string ValorNetoDisplay => $"$ {ValorNeto:N0}";

    public string VbsDisplay => TieneVbs
        ? (string.IsNullOrWhiteSpace(VbsUsuario) ? "Sí" : $"Sí ({VbsUsuario})")
        : "Pendiente";

    public string VbjDisplay => TieneVbj
        ? (string.IsNullOrWhiteSpace(VbjUsuario) ? "Sí" : $"Sí ({VbjUsuario})")
        : "Pendiente";
}

public enum MaintainerEntityKind
{
    Proveedor = 1,
    Solicitante = 2,
    Firma = 3,
    Activo = 4
}

public sealed class RelatedWorkOrderInfo
{
    public int IdOt { get; set; }
    public int NumeroOt { get; set; }
    public DateTime FechaOt { get; set; }
    public WorkOrderStatus Estado { get; set; }
    public string TipoRelacion { get; set; } = string.Empty;
    public string RegistroAsociado { get; set; } = string.Empty;
    public string InfoAdicional { get; set; } = string.Empty;
}

public sealed class MaintainerDependencyReport
{
    public MaintainerEntityKind EntityKind { get; set; }
    public IReadOnlyList<int> SelectedIds { get; set; } = [];
    public IReadOnlyList<string> SelectedLabels { get; set; } = [];
    public IReadOnlyList<RelatedWorkOrderInfo> RelatedWorkOrders { get; set; } = [];
    public int DependentChildNodesCount { get; set; }
}

public sealed class MaintainerBatchResult
{
    public string ActionName { get; set; } = string.Empty;
    public List<string> Succeeded { get; set; } = [];
    public List<(string Target, string Reason)> FailedOrSkipped { get; set; } = [];
    public int ReassignedWorkOrders { get; set; }
    public int DeletedWorkOrders { get; set; }

    public string BuildSummary()
    {
        List<string> lines =
        [
            $"{ActionName}: {Succeeded.Count} registro(s) procesado(s), {FailedOrSkipped.Count} omitido(s)/con error."
        ];
        if (ReassignedWorkOrders > 0)
        {
            lines.Add($"OTs reasignadas: {ReassignedWorkOrders}.");
        }

        if (DeletedWorkOrders > 0)
        {
            lines.Add($"OTs eliminadas explícitamente: {DeletedWorkOrders}.");
        }

        if (Succeeded.Count > 0)
        {
            lines.Add("Procesados: " + string.Join(", ", Succeeded.Take(6)) + (Succeeded.Count > 6 ? $" (+{Succeeded.Count - 6} más)" : string.Empty));
        }

        foreach ((string target, string reason) in FailedOrSkipped.Take(6))
        {
            lines.Add($"- {target}: {reason}");
        }

        if (FailedOrSkipped.Count > 6)
        {
            lines.Add($"... y {FailedOrSkipped.Count - 6} registro(s) adicional(es) con incidencia.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record ChartDataPoint(string Label, double Value);

public sealed record ApproverDirectoryEntry(
    string IdUsuario,
    string AliasUsuario,
    string NombreCompleto,
    bool Vbs,
    bool Vbj);

public sealed class DashboardMetrics
{
    public int TotalOrdenesTrabajo { get; set; }
    public int OrdenesSinVistoBueno { get; set; }
    public int OrdenesSinVbs { get; set; }
    public int OrdenesSinVbj { get; set; }
    public int TotalProveedoresActivos { get; set; }
    public int TotalSolicitantesActivos { get; set; }
    public int TotalSolicitantesConFirma { get; set; }
    public int TotalUsuariosActivos { get; set; }
    public int TotalGruposActivos { get; set; }
    public int TotalNodosActivos { get; set; }
}

public sealed class DashboardAnalytics
{
    public DashboardMetrics Metrics { get; init; } = new();
    public IReadOnlyList<ChartDataPoint> WorkOrdersByYear { get; init; } = [];
    public IReadOnlyList<ChartDataPoint> WorkOrdersByPlant { get; init; } = [];
    public IReadOnlyList<ChartDataPoint> TopProviders { get; init; } = [];
}

public sealed class AuditSearchFilter
{
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public string? Modulo { get; set; }
    public string? Usuario { get; set; }
    public string? TextoBusqueda { get; set; }
    public int MaxRows { get; set; } = 500;
}

public sealed class GroupMovementFilter
{
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public int? IdGrupo { get; set; }
    public string? IdUsuario { get; set; }
    public string? TextoBusqueda { get; set; }
    public int MaxRows { get; set; } = 500;
}

public sealed class BatchItemOutcome
{
    public required string TargetId { get; init; }
    public required string TargetLabel { get; init; }
    public bool Succeeded { get; init; }
    public bool ProtectedSkipped { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class BatchOperationResult
{
    public required string OperationName { get; init; }
    public int RequestedCount { get; init; }
    public List<BatchItemOutcome> Items { get; init; } = [];

    public int SucceededCount => Items.Count(i => i.Succeeded);
    public int ProtectedSkippedCount => Items.Count(i => !i.Succeeded && i.ProtectedSkipped);
    public int FailedCount => Items.Count(i => !i.Succeeded && !i.ProtectedSkipped);

    public string BuildSummary()
    {
        List<string> lines =
        [
            $"Operación: {OperationName}",
            $"Seleccionados: {RequestedCount} | Aplicados: {SucceededCount} | Protegidos omitidos: {ProtectedSkippedCount} | Errores: {FailedCount}"
        ];

        IEnumerable<BatchItemOutcome> issues = Items.Where(i => !i.Succeeded).Take(12);
        foreach (BatchItemOutcome item in issues)
        {
            string tag = item.ProtectedSkipped ? "[PROTEGIDO]" : "[ERROR]";
            lines.Add($"  {tag} {item.TargetLabel}: {item.Message}");
        }

        int extraIssues = Items.Count(i => !i.Succeeded) - 12;
        if (extraIssues > 0)
        {
            lines.Add($"  ... y {extraIssues} elemento(s) adicionales con observaciones.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

public sealed class ConcurrencyConflictException(string message) : InvalidOperationException(message);

public sealed class AssetNodeInfo
{
    public required AssetNode Node { get; init; }
    public int Depth { get; init; }
    public int ActiveChildren { get; init; }
    public int TotalChildren { get; init; }
    public int WorkOrderReferences { get; init; }
    public string Path { get; init; } = string.Empty;
}

public sealed class AssetTreeExportRow
{
    public int IdActivo { get; init; }
    public int? ParentId { get; init; }
    public int Depth { get; init; }
    public string Categoria { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Ruta { get; init; } = string.Empty;
    public string CodSap { get; init; } = string.Empty;
    public string Fabricante { get; init; } = string.Empty;
    public string Modelo { get; init; } = string.Empty;
    public string Serie { get; init; } = string.Empty;
    public bool Activo { get; init; }
}

public sealed class AppSettings
{
    public string DatabasePath { get; set; } = string.Empty;
    public string CompanyName { get; set; } = "CAMBIASO HNOS S.A.C.";
    public string CompanyRut { get; set; } = "91.438.000-6";
    public string CompanyDepartment { get; set; } = "DEPARTAMENTO MANTENCIÓN";
    public string CompanyAddress { get; set; } = "Virginia Bravo Letelier N°777, Placilla, Valparaíso";
    public string CompanyCity { get; set; } = "VALPARAÍSO";
    public string LegacyAccessTreePath { get; set; } = string.Empty;
}

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageIndex,
    int PageSize);

public sealed record SignaturePersonOption(
    string Nombre,
    string Etiqueta,
    string Origen,
    string Cargo = "",
    byte[]? FirmaPreviewPng = null,
    int? IdSol = null)
{
    public override string ToString() => Etiqueta;
}
