using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Core.Security;

/// <summary>
/// Plantillas de permisos por perfil de usuario. Garantiza el principio de mínimo privilegio.
/// </summary>
public static class PermissionPresets
{
    public static UserPermissions CreateForRole(UserRoleLevel level, string userId = "")
    {
        return level switch
        {
            UserRoleLevel.Usuario => new UserPermissions
            {
                IdUsuario = userId,
                MenuOrdenesTrabajo = true,
                IngresoOt = true,
                BusquedaOt = true,
                ImpresionOt = false,
                VistoBuenoSupervisor = false,
                VistoBuenoJefatura = false,
                MenuMantenedor = false,
                MenuUsuarios = false,
                IngresoUsuarios = false,
                ListadoUsuarios = false
            },
            UserRoleLevel.Supervisor => new UserPermissions
            {
                IdUsuario = userId,
                MenuOrdenesTrabajo = true,
                IngresoOt = true,
                VistoBuenoSupervisor = true,
                VistoBuenoJefatura = false,
                BusquedaOt = true,
                ImpresionOt = false,
                MenuMantenedor = false,
                MenuUsuarios = false,
                IngresoUsuarios = false,
                ListadoUsuarios = false
            },
            UserRoleLevel.Jefatura => new UserPermissions
            {
                IdUsuario = userId,
                MenuOrdenesTrabajo = true,
                IngresoOt = true,
                VistoBuenoSupervisor = true,
                VistoBuenoJefatura = true,
                BusquedaOt = true,
                ImpresionOt = false,
                MenuMantenedor = true,
                MenuUsuarios = false,
                IngresoUsuarios = false,
                ListadoUsuarios = false
            },
            UserRoleLevel.Administrador => new UserPermissions
            {
                IdUsuario = userId,
                MenuOrdenesTrabajo = true,
                IngresoOt = true,
                VistoBuenoSupervisor = true,
                VistoBuenoJefatura = true,
                BusquedaOt = true,
                ImpresionOt = false,
                MenuMantenedor = true,
                MenuUsuarios = true,
                IngresoUsuarios = true,
                ListadoUsuarios = true
            },
            _ => new UserPermissions { IdUsuario = userId }
        };
    }
}
