using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;

namespace Sispro.Services;

/// <summary>
/// Gestión CRUD de Perfiles de Usuarios con control RBAC exclusivo para Administradores.
/// Protege los 4 perfiles base del sistema contra eliminación o desactivación y evita degradar al perfil Administrador.
/// </summary>
public sealed class UserProfileService(
    IUserProfileRepository profiles,
    IUserRepository users,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null)
{
    public IReadOnlyList<UserProfile> GetAll(UserSession session, bool includeInactive = true)
    {
        EnsureAdmin(session, "Consultar catálogo de perfiles de usuario");
        return profiles.GetAll(includeInactive);
    }

    public UserProfile? GetById(UserSession session, int idNivel)
    {
        EnsureAdmin(session, "Consultar detalle de perfil");
        return profiles.GetById(idNivel);
    }

    public int Create(UserSession session, UserProfile profile)
    {
        EnsureAdmin(session, "Crear perfil de usuario");
        ValidateAndNormalizeProfile(profile);

        if (profiles.GetByName(profile.Nombre) is not null)
        {
            throw new InvalidOperationException($"Ya existe un perfil llamado '{profile.Nombre}'.");
        }

        // Los perfiles personalizados nunca pueden otorgar acceso al módulo de Gestión de Usuarios (exclusivo Administrador #4)
        EnforceNonAdminBoundary(profile);

        int id = profiles.Create(profile);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Perfiles",
            "CREAR_PERFIL",
            $"Perfil '{profile.Nombre}' (ID {id}, Estado {(profile.Activo ? "Activo" : "Inactivo")}) creado.");
        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "CREAR_PERFIL");
        return id;
    }

    public void Update(UserSession session, UserProfile profile)
    {
        EnsureAdmin(session, "Modificar perfil de usuario");
        if (profile.IdNivel <= 0)
        {
            throw new ArgumentException("Debe indicar un perfil válido para modificar.");
        }

        UserProfile existing = profiles.GetById(profile.IdNivel)
            ?? throw new InvalidOperationException("El perfil seleccionado no existe.");

        ValidateAndNormalizeProfile(profile);

        if (existing.EsSistema && !string.Equals(existing.Nombre, profile.Nombre, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"No se puede renombrar el perfil base del sistema '{existing.Nombre}'.");
        }

        UserProfile? conflict = profiles.GetByName(profile.Nombre);
        if (conflict is not null && conflict.IdNivel != profile.IdNivel)
        {
            throw new InvalidOperationException($"Ya existe otro perfil llamado '{profile.Nombre}'.");
        }

        if (existing.EsAdministrador)
        {
            profile.Activo = true;
            profile.PermisosPorDefecto.MenuOrdenesTrabajo = true;
            profile.PermisosPorDefecto.IngresoOt = true;
            profile.PermisosPorDefecto.BusquedaOt = true;
            profile.PermisosPorDefecto.ImpresionOt = false;
            profile.PermisosPorDefecto.MenuMantenedor = true;
            profile.PermisosPorDefecto.MenuUsuarios = true;
            profile.PermisosPorDefecto.IngresoUsuarios = true;
            profile.PermisosPorDefecto.ListadoUsuarios = true;
        }
        else
        {
            if (existing.EsSistema && !profile.Activo)
            {
                throw new InvalidOperationException($"No se puede desactivar el perfil base del sistema '{existing.Nombre}'.");
            }

            if (!profile.Activo && profiles.CountUsers(profile.IdNivel) > 0)
            {
                throw new InvalidOperationException($"El perfil '{existing.Nombre}' tiene usuarios asignados. Reasígnelos antes de desactivarlo.");
            }

            EnforceNonAdminBoundary(profile);
        }

        profiles.Update(profile);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Perfiles",
            "ACTUALIZAR_PERFIL",
            $"Perfil '{profile.Nombre}' (ID {profile.IdNivel}) actualizado.");
        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "ACTUALIZAR_PERFIL");
    }

    public void SetActive(UserSession session, int idNivel, bool active)
    {
        EnsureAdmin(session, active ? "Activar perfil" : "Desactivar perfil");
        UserProfile existing = profiles.GetById(idNivel)
            ?? throw new InvalidOperationException("El perfil seleccionado no existe.");

        if (!active && existing.EsSistema)
        {
            throw new InvalidOperationException($"El perfil base del sistema '{existing.Nombre}' no puede desactivarse.");
        }

        if (!active && profiles.CountUsers(idNivel) > 0)
        {
            throw new InvalidOperationException($"El perfil '{existing.Nombre}' tiene {profiles.CountUsers(idNivel)} usuario(s) asignado(s). Reasígnelos antes de desactivarlo.");
        }

        profiles.SetActive(idNivel, active);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Perfiles",
            active ? "ACTIVAR_PERFIL" : "DESACTIVAR_PERFIL",
            $"Perfil '{existing.Nombre}' (ID {idNivel}) marcado como {(active ? "Activo" : "Inactivo")}.");
        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, active ? "ACTIVAR_PERFIL" : "DESACTIVAR_PERFIL");
    }

    public void Delete(UserSession session, int idNivel)
    {
        EnsureAdmin(session, "Eliminar perfil de usuario");
        UserProfile existing = profiles.GetById(idNivel)
            ?? throw new InvalidOperationException("El perfil seleccionado no existe.");

        if (existing.EsSistema)
        {
            throw new InvalidOperationException($"El perfil base del sistema '{existing.Nombre}' no se puede eliminar.");
        }

        int count = profiles.CountUsers(idNivel);
        if (count > 0)
        {
            throw new InvalidOperationException($"No se puede eliminar el perfil '{existing.Nombre}' porque tiene {count} usuario(s) asignado(s). Reasigne esos usuarios primero.");
        }

        profiles.Delete(idNivel);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Perfiles",
            "ELIMINAR_PERFIL",
            $"Perfil '{existing.Nombre}' (ID {idNivel}) eliminado.");
        syncHub?.Publish(DataChangeKind.Users | DataChangeKind.Audit, "ELIMINAR_PERFIL");
    }

    private void EnsureAdmin(UserSession session, string action) =>
        AuthService.EnsureAdministrator(session, users, audit, "Perfiles", action);

    private static void ValidateAndNormalizeProfile(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Nombre = profile.Nombre.Trim();
        profile.Descripcion = profile.Descripcion.Trim();

        if (string.IsNullOrWhiteSpace(profile.Nombre) || profile.Nombre.Length < 2)
        {
            throw new ArgumentException("El nombre del perfil debe tener al menos 2 caracteres.");
        }

        profile.PermisosPorDefecto ??= new UserPermissions();
        profile.PermisosPorDefecto.NormalizeHierarchy();
    }

    private static void EnforceNonAdminBoundary(UserProfile profile)
    {
        profile.PermisosPorDefecto.MenuUsuarios = false;
        profile.PermisosPorDefecto.IngresoUsuarios = false;
        profile.PermisosPorDefecto.ListadoUsuarios = false;
        profile.PermisosPorDefecto.NormalizeHierarchy();
    }
}
