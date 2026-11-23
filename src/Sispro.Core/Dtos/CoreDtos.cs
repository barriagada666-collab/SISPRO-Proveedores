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
    public string Proveedor { get; set; } = string.Empty;
    public string Solicitante { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int Cantidad { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public string RutaActivo { get; set; } = string.Empty;
    public bool TieneVbs { get; set; }
    public bool TieneVbj { get; set; }
    public bool AsociadoAFactura { get; set; }
    public int IdDet { get; set; }
}

public sealed class InvoiceSearchFilter
{
    public int? IdProv { get; set; }
    public int? NumeroDocumento { get; set; }
    public int? NumeroOt { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
}

public sealed class InvoiceSearchRow
{
    public int IdFactura { get; set; }
    public int NumeroDocumento { get; set; }
    public string Proveedor { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string RecibidoPor { get; set; } = string.Empty;
    public decimal TotalNeto { get; set; }
    public decimal Iva { get; set; }
    public decimal TotalConIva { get; set; }
    public int CantidadItems { get; set; }
    public string Detalle { get; set; } = string.Empty;
}

public sealed class DashboardMetrics
{
    public int TotalOrdenesTrabajo { get; set; }
    public int OrdenesSinVistoBueno { get; set; }
    public int TotalProveedoresActivos { get; set; }
    public int TotalSolicitantesActivos { get; set; }
    public int TotalFacturas { get; set; }
    public int TotalUsuariosActivos { get; set; }
    public int TotalGruposActivos { get; set; }
    public int TotalNodosActivos { get; set; }
    public int TotalMantOts { get; set; }
    public int TotalPlanesMp { get; set; }
    public int TotalTareasMp { get; set; }
    public int TotalInsumos { get; set; }
    public int TotalPersonalTecnico { get; set; }
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
