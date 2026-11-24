using Sispro.Core.Enums;

namespace Sispro.Core.Entities;

public sealed class User
{
    public int CodigoId { get; set; }
    public string IdUsuario { get; set; } = string.Empty;
    public string AliasUsuario { get; set; } = string.Empty;
    public string PassHash { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string ApePatUsuario { get; set; } = string.Empty;
    public string ApeMatUsuario { get; set; } = string.Empty;
    public UserRoleLevel Nivel { get; set; } = UserRoleLevel.Usuario;
    public string MailUsuario { get; set; } = string.Empty;
    public string FonoUsuario { get; set; } = string.Empty;
    public bool EsSolicitante { get; set; }
    public bool Activo { get; set; } = true;
    public bool DebeCambiarClave { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime FechaModificacion { get; set; } = DateTime.Now;
    public DateTime? UltimoAcceso { get; set; }
    public int? IdGrupo { get; set; }
    public string NombreGrupo { get; set; } = string.Empty;
    public int RowVersion { get; set; } = 1;
    public UserPermissions Permisos { get; set; } = new();

    public string Apellidos =>
        string.Join(" ", new[] { ApePatUsuario, ApeMatUsuario }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string NombreCompleto =>
        string.Join(" ", new[] { NombreUsuario, ApePatUsuario, ApeMatUsuario }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static (string Paterno, string Materno) SplitSurnames(string? fullSurnames)
    {
        if (string.IsNullOrWhiteSpace(fullSurnames))
        {
            return (string.Empty, string.Empty);
        }

        string trimmed = fullSurnames.Trim();
        int spaceIdx = trimmed.IndexOf(' ');
        if (spaceIdx < 0)
        {
            return (trimmed, string.Empty);
        }

        return (trimmed[..spaceIdx].Trim(), trimmed[(spaceIdx + 1)..].Trim());
    }
}

public sealed class UserPermissions
{
    public string IdUsuario { get; set; } = string.Empty;

    // M1: Módulo Trabajos Externos / Órdenes de Trabajo
    public bool MenuOrdenesTrabajo { get; set; }
    public bool IngresoOt { get; set; }
    public bool VistoBuenoJefatura { get; set; }
    public bool VistoBuenoSupervisor { get; set; }
    public bool BusquedaOt { get; set; }
    public bool ImpresionOt { get; set; }

    // M2: Módulo Mantenedores (Proveedores, Solicitantes, Árbol de Activos)
    public bool MenuMantenedor { get; set; }

    // M3: Módulo Usuarios, Solicitantes y Grupos
    public bool MenuUsuarios { get; set; }
    public bool IngresoUsuarios { get; set; }
    public bool ListadoUsuarios { get; set; }

    public void NormalizeHierarchy()
    {
        if (!MenuOrdenesTrabajo)
        {
            IngresoOt = false;
            VistoBuenoJefatura = false;
            VistoBuenoSupervisor = false;
            BusquedaOt = false;
            ImpresionOt = false;
        }

        if (!MenuUsuarios)
        {
            IngresoUsuarios = false;
            ListadoUsuarios = false;
        }
    }
}

public sealed class Provider
{
    public int IdProv { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Direccion { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Fax { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Contacto { get; set; } = string.Empty;
    public string CorreoContacto { get; set; } = string.Empty;
    public string Observaciones { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public override string ToString() => Nombre;
}

public sealed class Requester
{
    public int IdSol { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string ApellidoPaterno { get; set; } = string.Empty;
    public string ApellidoMaterno { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public byte[]? FirmaBytes { get; set; }
    public string FirmaNombre { get; set; } = string.Empty;
    public string FirmaTipo { get; set; } = string.Empty;
    public byte[]? FirmaPreviewPng { get; set; }

    public bool TieneFirma => (FirmaBytes is { Length: > 0 }) || (FirmaPreviewPng is { Length: > 0 });

    public string FirmaBadge => !TieneFirma
        ? "Sin firma"
        : string.IsNullOrWhiteSpace(FirmaTipo) ? "Adjunta" : FirmaTipo.ToUpperInvariant();

    public string NombreCompleto =>
        string.Join(" ", new[] { Nombre, ApellidoPaterno, ApellidoMaterno }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public override string ToString() => Alias;
}

public sealed class AssetNode
{
    public int IdActivo { get; set; }
    public int? ParentId { get; set; }
    public int Nivel { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public bool TieneHijos { get; set; }

    // Ficha técnica del equipo industrial
    public string CodSap { get; set; } = string.Empty;
    public string Fabricante { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string TipoEquipo { get; set; } = string.Empty;
    public string EstadoOperativo { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public int Prioridad { get; set; }
    public string Material { get; set; } = string.Empty;
    public string VidaUtil { get; set; } = string.Empty;
    public string Observacion { get; set; } = string.Empty;
    public string FechaInstalacion { get; set; } = string.Empty;
    public decimal ValorCompra { get; set; }
    public string InfoTecnicaJson { get; set; } = string.Empty;

    public override string ToString() => Descripcion;
}

public sealed class WorkOrder
{
    public int IdOt { get; set; }
    public int NumeroOt { get; set; }
    public int IdProv { get; set; }
    public string NombreProveedor { get; set; } = string.Empty;
    public string DireccionProveedor { get; set; } = string.Empty;
    public string CorreoProveedor { get; set; } = string.Empty;
    public int? IdSol { get; set; }
    public string AliasSolicitante { get; set; } = string.Empty;
    public string NombreSolicitante { get; set; } = string.Empty;
    public string TelefonoSolicitante { get; set; } = string.Empty;
    public string CorreoSolicitante { get; set; } = string.Empty;
    public byte[]? FirmaSolicitantePreviewPng { get; set; }
    public string FirmaSolicitanteTipo { get; set; } = string.Empty;
    public string FirmaSolicitanteNombre { get; set; } = string.Empty;

    public string SolicitanteDisplayNombre => !string.IsNullOrWhiteSpace(NombreSolicitante)
        ? NombreSolicitante.Trim()
        : AliasSolicitante;
    public DateTime FechaOt { get; set; } = DateTime.Today;
    public WorkOrderStatus Estado { get; set; } = WorkOrderStatus.Abierta;
    public int IdN1 { get; set; } = 1;
    public int IdN2 { get; set; }
    public int IdN3 { get; set; }
    public int IdN4 { get; set; }
    public int IdN5 { get; set; }
    public int IdN6 { get; set; }
    public int IdN7 { get; set; }
    public int IdN8 { get; set; }
    public int IdN9 { get; set; }
    public int IdN10 { get; set; }
    public int IdNivel { get; set; } = 1;
    public string RutaActivo { get; set; } = string.Empty;
    public List<WorkOrderDetail> Detalles { get; set; } = [];
    public List<WorkOrderApproval> VistosBuenos { get; set; } = [];

    public bool TieneVbs => VistosBuenos.Any(v => v.Tipo == ApprovalType.Supervisor);
    public bool TieneVbj => VistosBuenos.Any(v => v.Tipo == ApprovalType.Jefatura);

    public WorkOrderApproval? VbsApproval => VistosBuenos.FirstOrDefault(v => v.Tipo == ApprovalType.Supervisor);
    public WorkOrderApproval? VbjApproval => VistosBuenos.FirstOrDefault(v => v.Tipo == ApprovalType.Jefatura);

    public string VbsResumen => VbsApproval is null
        ? "Pendiente"
        : string.IsNullOrWhiteSpace(VbsApproval.AprobadorDisplay)
            ? $"APROBADO ({VbsApproval.Fecha:dd-MM-yyyy HH:mm})"
            : $"APROBADO por {VbsApproval.AprobadorDisplay} ({VbsApproval.Fecha:dd-MM-yyyy HH:mm})";

    public string VbjResumen => VbjApproval is null
        ? "Pendiente"
        : string.IsNullOrWhiteSpace(VbjApproval.AprobadorDisplay)
            ? $"APROBADO ({VbjApproval.Fecha:dd-MM-yyyy HH:mm})"
            : $"APROBADO por {VbjApproval.AprobadorDisplay} ({VbjApproval.Fecha:dd-MM-yyyy HH:mm})";

    public int[] GetLevelArray() => [IdN1, IdN2, IdN3, IdN4, IdN5, IdN6, IdN7, IdN8, IdN9, IdN10];

    public void SetLevels(IReadOnlyList<int> pathIds)
    {
        IdN1 = pathIds.Count > 0 ? pathIds[0] : 1;
        IdN2 = pathIds.Count > 1 ? pathIds[1] : 0;
        IdN3 = pathIds.Count > 2 ? pathIds[2] : 0;
        IdN4 = pathIds.Count > 3 ? pathIds[3] : 0;
        IdN5 = pathIds.Count > 4 ? pathIds[4] : 0;
        IdN6 = pathIds.Count > 5 ? pathIds[5] : 0;
        IdN7 = pathIds.Count > 6 ? pathIds[6] : 0;
        IdN8 = pathIds.Count > 7 ? pathIds[7] : 0;
        IdN9 = pathIds.Count > 8 ? pathIds[8] : 0;
        IdN10 = pathIds.Count > 9 ? pathIds[9] : 0;
        IdNivel = Math.Clamp(pathIds.Count, 1, 10);
    }
}

public sealed class WorkOrderDetail
{
    public int IdDet { get; set; }
    public int IdOt { get; set; }
    public int Cantidad { get; set; } = 1;
    public string Descripcion { get; set; } = string.Empty;
}

public sealed class WorkOrderApproval
{
    public int IdVb { get; set; }
    public int IdOt { get; set; }
    public string IdUsuario { get; set; } = string.Empty;
    public string AliasUsuario { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public ApprovalType Tipo { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;

    public string AprobadorDisplay =>
        string.IsNullOrWhiteSpace(NombreUsuario) || string.Equals(NombreUsuario, AliasUsuario, StringComparison.OrdinalIgnoreCase)
            ? AliasUsuario
            : $"{NombreUsuario} ({AliasUsuario})";
}

public sealed class AuditEntry
{
    public long IdLog { get; set; }
    public DateTime Fecha { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
}

public sealed class UserGroup
{
    public int IdGrupo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string? IdResponsable { get; set; }
    public string AliasResponsable { get; set; } = string.Empty;
    public string NombreResponsable { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public int CantidadIntegrantes { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
    public DateTime FechaModificacion { get; set; } = DateTime.Now;
    public int RowVersion { get; set; } = 1;

    public override string ToString() => Nombre;
}

public sealed class GroupMovementEntry
{
    public long IdMovimiento { get; set; }
    public string IdUsuario { get; set; } = string.Empty;
    public int CodigoUsuario { get; set; }
    public string AliasUsuario { get; set; } = string.Empty;
    public int? IdGrupoOrigen { get; set; }
    public string NombreGrupoOrigen { get; set; } = "Sin grupo";
    public int? IdGrupoDestino { get; set; }
    public string NombreGrupoDestino { get; set; } = "Sin grupo";
    public string EjecutadoPor { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
    public DateTime FechaMovimiento { get; set; } = DateTime.Now;
}
