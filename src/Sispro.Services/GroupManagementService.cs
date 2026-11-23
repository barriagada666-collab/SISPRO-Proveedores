using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Core.Validation;

namespace Sispro.Services;

/// <summary>
/// Servicio de Gestión de Grupos y Categorías de trabajo con RBAC exclusivo de Administrador,
/// protección de dependencias históricas, concurrencia optimista y sincronización inmediata.
/// </summary>
public sealed class GroupManagementService(
    IGroupRepository groups,
    IUserRepository users,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null)
{
    public IReadOnlyList<UserGroup> GetAll(UserSession session, bool includeInactive = true, string? search = null)
    {
        EnsureAdministrator(session, "Consultar grupos");
        return groups.GetAll(includeInactive, search);
    }

    public UserGroup? GetById(UserSession session, int idGrupo)
    {
        EnsureAdministrator(session, "Consultar detalle de grupo");
        return groups.GetById(idGrupo);
    }

    public IReadOnlyList<User> GetMembers(UserSession session, int idGrupo)
    {
        EnsureAdministrator(session, "Consultar integrantes del grupo");
        return users.Search(new UserSearchFilter { IdGrupo = idGrupo });
    }

    public int CreateGroup(UserSession session, UserGroup group)
    {
        EnsureAdministrator(session, "Crear grupo");
        ValidateGroup(group);

        string trimmedName = group.Nombre.Trim();
        if (groups.GetByName(trimmedName) is not null)
        {
            throw new InvalidOperationException($"Ya existe un grupo registrado con el nombre '{trimmedName}'.");
        }

        ValidateResponsible(group.IdResponsable);

        group.Nombre = trimmedName;
        group.Descripcion = group.Descripcion.Trim();
        group.IdResponsable = string.IsNullOrWhiteSpace(group.IdResponsable) ? null : group.IdResponsable.Trim();

        int id = groups.Create(group);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Grupos",
            "CREAR_GRUPO",
            $"Grupo '{group.Nombre}' (ID {id}, Estado {(group.Activo ? "Activo" : "Inactivo")}) creado.");

        syncHub?.Publish(DataChangeKind.Groups | DataChangeKind.Audit, "CREAR_GRUPO");
        return id;
    }

    public void UpdateGroup(UserSession session, UserGroup group, int? expectedRowVersion = null)
    {
        EnsureAdministrator(session, "Modificar grupo");
        UserGroup existing = groups.GetById(group.IdGrupo)
            ?? throw new InvalidOperationException("El grupo seleccionado no existe.");

        ValidateGroup(group);

        string trimmedName = group.Nombre.Trim();
        UserGroup? duplicate = groups.GetByName(trimmedName);
        if (duplicate is not null && duplicate.IdGrupo != existing.IdGrupo)
        {
            throw new InvalidOperationException($"Ya existe otro grupo con el nombre '{trimmedName}'.");
        }

        ValidateResponsible(group.IdResponsable);

        group.Nombre = trimmedName;
        group.Descripcion = group.Descripcion.Trim();
        group.IdResponsable = string.IsNullOrWhiteSpace(group.IdResponsable) ? null : group.IdResponsable.Trim();

        groups.Update(group, expectedRowVersion);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Grupos",
            "MODIFICAR_GRUPO",
            $"Grupo '{existing.Nombre}' (ID {existing.IdGrupo}) actualizado a '{group.Nombre}' (Estado {(group.Activo ? "Activo" : "Inactivo")}).");

        syncHub?.Publish(DataChangeKind.Groups | DataChangeKind.Users | DataChangeKind.Audit, "MODIFICAR_GRUPO");
    }

    public void SetGroupActive(UserSession session, int idGrupo, bool active)
    {
        EnsureAdministrator(session, active ? "Activar grupo" : "Desactivar grupo");
        UserGroup existing = groups.GetById(idGrupo)
            ?? throw new InvalidOperationException("El grupo seleccionado no existe.");

        if (existing.Activo == active)
        {
            return;
        }

        groups.SetActive(idGrupo, active);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Grupos",
            active ? "ACTIVAR_GRUPO" : "DESACTIVAR_GRUPO",
            $"Estado del grupo '{existing.Nombre}' (ID {existing.IdGrupo}) cambiado a {(active ? "Activo" : "Inactivo")}.");

        syncHub?.Publish(DataChangeKind.Groups | DataChangeKind.Audit, active ? "ACTIVAR_GRUPO" : "DESACTIVAR_GRUPO");
    }

    public void DeleteGroup(UserSession session, int idGrupo)
    {
        EnsureAdministrator(session, "Eliminar grupo");
        UserGroup existing = groups.GetById(idGrupo)
            ?? throw new InvalidOperationException("El grupo seleccionado no existe.");

        if (groups.HasDependencies(idGrupo))
        {
            int assigned = groups.CountAssignedUsers(idGrupo);
            throw new InvalidOperationException(
                assigned > 0
                    ? $"No se puede eliminar el grupo '{existing.Nombre}' porque tiene {assigned} usuario(s) asignado(s). Mueva los usuarios o desactive el grupo para preservar la trazabilidad."
                    : $"No se puede eliminar el grupo '{existing.Nombre}' porque posee historial de movimientos registrado. Desactive el grupo para preservar la auditoría histórica.");
        }

        groups.Delete(idGrupo);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Grupos",
            "ELIMINAR_GRUPO",
            $"Grupo sin dependencias '{existing.Nombre}' (ID {existing.IdGrupo}) eliminado.");

        syncHub?.Publish(DataChangeKind.Groups | DataChangeKind.Audit, "ELIMINAR_GRUPO");
    }

    public IReadOnlyList<GroupMovementEntry> GetMovementHistory(UserSession session, GroupMovementFilter? filter = null)
    {
        EnsureAdministrator(session, "Consultar historial de movimientos de grupos");
        if (filter is not null)
        {
            string? dateErr = DomainValidator.ValidateDateRange(filter.FechaInicio, filter.FechaFin);
            if (dateErr is not null)
            {
                throw new ArgumentException(dateErr);
            }
        }

        return groups.GetMovements(filter);
    }

    private void ValidateResponsible(string? idResponsable)
    {
        if (string.IsNullOrWhiteSpace(idResponsable))
        {
            return;
        }

        User? responsible = users.GetById(idResponsable);
        if (responsible is null)
        {
            throw new ArgumentException("El responsable seleccionado no existe en el sistema.");
        }
    }

    private static void ValidateGroup(UserGroup group)
    {
        if (string.IsNullOrWhiteSpace(group.Nombre) || group.Nombre.Trim().Length < 2)
        {
            throw new ArgumentException("El nombre del grupo debe tener al menos 2 caracteres.");
        }

        if (group.Nombre.Trim().Length > 60)
        {
            throw new ArgumentException("El nombre del grupo no puede superar 60 caracteres.");
        }
    }

    private void EnsureAdministrator(UserSession? session, string actionDescription)
    {
        if (session is null || !session.Usuario.Activo)
        {
            audit.Log(
                session?.Usuario.AliasUsuario ?? "ANONIMO",
                "Seguridad",
                "ACCESO_NO_AUTORIZADO",
                $"Intento sin sesión activa en Gestión de Grupos: {actionDescription}.");
            throw new UnauthorizedAccessException($"Acceso denegado: no existe una sesión activa autorizada para {actionDescription}.");
        }

        User? currentDbUser = users.GetById(session.Usuario.IdUsuario);
        bool isActiveAdmin = currentDbUser is not null
                             && currentDbUser.Activo
                             && currentDbUser.Nivel == UserRoleLevel.Administrador
                             && session.Usuario.Nivel == UserRoleLevel.Administrador;

        if (!isActiveAdmin)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Seguridad",
                "ACCESO_NO_AUTORIZADO",
                $"Usuario '{session.Usuario.AliasUsuario}' (Perfil {session.Usuario.Nivel}) intentó sin autorización: {actionDescription}.");
            throw new UnauthorizedAccessException($"Acceso denegado: la Gestión de Grupos es exclusiva del perfil Administrador ({actionDescription}).");
        }
    }
}
