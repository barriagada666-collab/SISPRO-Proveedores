using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Core.Security;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class UserManagementService(
    IUserRepository users,
    IRequesterRepository requesters,
    IAuditRepository audit)
{
    public void EnsureCanAccessModule(UserSession? session) =>
        EnsureAdministrator(session, "Acceder al módulo Gestión de Usuarios");

    public IReadOnlyList<User> GetAll(UserSession session, string? search = null) =>
        Search(session, new UserSearchFilter { TextoBusqueda = search });

    public IReadOnlyList<User> Search(UserSession session, UserSearchFilter? filter = null)
    {
        EnsureAdministrator(session, "Consultar usuarios");
        return users.Search(filter ?? new UserSearchFilter());
    }

    public User? GetById(UserSession session, string idUsuario)
    {
        EnsureAdministrator(session, "Consultar detalle de usuario");
        return users.GetById(idUsuario);
    }

    public int CreateUser(UserSession session, User user, string plainPassword, string confirmPassword)
    {
        EnsureAdministrator(session, "Crear usuario");

        if (!string.IsNullOrWhiteSpace(user.IdUsuario))
        {
            string normalizedRut = RutValidator.Normalize(user.IdUsuario);
            if (!RutValidator.IsValid(normalizedRut))
            {
                throw new ArgumentException("El RUT ingresado no es válido (verifique cuerpo y dígito verificador).");
            }

            user.IdUsuario = normalizedRut;
            if (users.GetById(user.IdUsuario) is not null)
            {
                throw new InvalidOperationException($"Ya existe un usuario registrado con el identificador {RutValidator.Format(user.IdUsuario)}.");
            }
        }
        else
        {
            user.IdUsuario = string.Empty;
        }

        ValidateCommonUserFields(user);

        string? pwdError = DomainValidator.ValidatePasswordPolicy(plainPassword, confirmPassword);
        if (pwdError is not null)
        {
            throw new ArgumentException(pwdError);
        }

        if (users.GetByAlias(user.AliasUsuario) is not null)
        {
            throw new InvalidOperationException($"El nombre de usuario '{user.AliasUsuario.Trim()}' ya se encuentra registrado.");
        }

        user.AliasUsuario = user.AliasUsuario.Trim();
        user.NombreUsuario = user.NombreUsuario.Trim();
        user.ApePatUsuario = user.ApePatUsuario.Trim();
        user.ApeMatUsuario = user.ApeMatUsuario.Trim();
        user.MailUsuario = user.MailUsuario.Trim();
        user.FonoUsuario = user.FonoUsuario.Trim();
        user.PassHash = PasswordSecurity.HashPassword(plainPassword);

        EnforceRolePermissionBoundary(user);

        int codigoId = users.Create(user);
        if (user.EsSolicitante)
        {
            requesters.EnsureFromUser(user);
        }

        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "CREAR_USUARIO",
            $"Usuario '{user.AliasUsuario}' (ID {codigoId}, Nombre '{user.NombreCompleto}', Perfil {user.Nivel}, Estado {(user.Activo ? "Activo" : "Inactivo")}) creado.");

        return codigoId;
    }

    public void UpdateUser(UserSession session, User user, string? newPassword = null, string? confirmPassword = null)
    {
        EnsureAdministrator(session, "Modificar usuario");

        User existing = users.GetById(user.IdUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        ValidateCommonUserFields(user);

        string trimmedAlias = user.AliasUsuario.Trim();
        User? aliasConflict = users.GetByAlias(trimmedAlias);
        if (aliasConflict is not null && !string.Equals(aliasConflict.IdUsuario, existing.IdUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"El nombre de usuario '{trimmedAlias}' ya pertenece a otro usuario.");
        }

        bool isSelf = string.Equals(session.Usuario.IdUsuario, existing.IdUsuario, StringComparison.OrdinalIgnoreCase);
        if (isSelf && !user.Activo)
        {
            throw new InvalidOperationException("No puede desactivar su propia cuenta mientras mantiene la sesión activa.");
        }

        if (isSelf && user.Nivel != UserRoleLevel.Administrador)
        {
            throw new InvalidOperationException("No puede quitarse el perfil Administrador a su propia cuenta.");
        }

        bool updatePwd = !string.IsNullOrEmpty(newPassword) || !string.IsNullOrEmpty(confirmPassword);
        if (updatePwd)
        {
            string? pwdError = DomainValidator.ValidatePasswordPolicy(newPassword, confirmPassword);
            if (pwdError is not null)
            {
                throw new ArgumentException(pwdError);
            }

            user.PassHash = PasswordSecurity.HashPassword(newPassword!);
        }
        else
        {
            user.PassHash = existing.PassHash;
            user.DebeCambiarClave = existing.DebeCambiarClave;
        }

        // Garantizar que siempre exista al menos un Administrador activo
        bool wasActiveAdmin = existing.Activo && existing.Nivel == UserRoleLevel.Administrador;
        bool staysActiveAdmin = user.Activo && user.Nivel == UserRoleLevel.Administrador;
        if (wasActiveAdmin && !staysActiveAdmin && users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("No se puede quitar privilegios ni desactivar al último Administrador activo del sistema.");
        }

        user.CodigoId = existing.CodigoId;
        user.AliasUsuario = trimmedAlias;
        user.NombreUsuario = user.NombreUsuario.Trim();
        user.ApePatUsuario = user.ApePatUsuario.Trim();
        user.ApeMatUsuario = user.ApeMatUsuario.Trim();
        user.MailUsuario = user.MailUsuario.Trim();
        user.FonoUsuario = user.FonoUsuario.Trim();
        user.Permisos.IdUsuario = user.IdUsuario;
        EnforceRolePermissionBoundary(user);

        users.Update(user, updatePwd);
        if (user.EsSolicitante)
        {
            requesters.EnsureFromUser(user);
        }

        List<string> changedFields = CollectChangedFields(existing, user, updatePwd);
        string summary = changedFields.Count > 0 ? string.Join(", ", changedFields) : "Sin cambios de datos";
        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "MODIFICAR_USUARIO",
            $"Usuario '{user.AliasUsuario}' (ID {existing.CodigoId}) actualizado [{summary}].");

        if (existing.Nivel != user.Nivel)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Usuarios",
                "CAMBIO_PERFIL",
                $"Perfil de '{user.AliasUsuario}' (ID {existing.CodigoId}) modificado de {existing.Nivel} a {user.Nivel}.");
        }

        if (existing.Activo != user.Activo)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Usuarios",
                user.Activo ? "ACTIVAR_USUARIO" : "DESACTIVAR_USUARIO",
                $"Estado de '{user.AliasUsuario}' (ID {existing.CodigoId}) cambiado a {(user.Activo ? "Activo" : "Inactivo")}.");
        }

        if (updatePwd)
        {
            audit.Log(
                session.Usuario.AliasUsuario,
                "Seguridad",
                "CAMBIO_CLAVE_ADMIN",
                $"Contraseña de '{user.AliasUsuario}' (ID {existing.CodigoId}) actualizada por administrador.");
        }
    }

    public void ChangeUserPassword(
        UserSession session,
        string idUsuario,
        string newPassword,
        string confirmPassword,
        bool requireChangeOnNextLogin = false)
    {
        EnsureAdministrator(session, "Cambiar contraseña de usuario");

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        string? pwdError = DomainValidator.ValidatePasswordPolicy(newPassword, confirmPassword);
        if (pwdError is not null)
        {
            throw new ArgumentException(pwdError);
        }

        string newHash = PasswordSecurity.HashPassword(newPassword);
        users.UpdatePasswordHash(existing.IdUsuario, newHash, requireChangeOnNextLogin);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Seguridad",
            "CAMBIO_CLAVE_ADMIN",
            $"Contraseña del usuario '{existing.AliasUsuario}' (ID {existing.CodigoId}) restablecida por administrador (ForzarCambioPrimerAcceso={requireChangeOnNextLogin}).");
    }

    /// <summary>
    /// Eliminación lógica (desactivación) de usuario. Preserva la integridad de registros históricos.
    /// </summary>
    public void DeleteUser(UserSession session, string idUsuario)
    {
        EnsureAdministrator(session, "Eliminar (desactivar) usuario");

        if (string.Equals(session.Usuario.IdUsuario, idUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No puede eliminar ni desactivar su propia cuenta mientras mantiene la sesión activa.");
        }

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        if (!existing.Activo)
        {
            throw new InvalidOperationException($"El usuario '{existing.AliasUsuario}' ya se encuentra inactivo.");
        }

        if (existing.Nivel == UserRoleLevel.Administrador && users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("No se puede eliminar ni desactivar al último Administrador activo del sistema.");
        }

        users.SetActive(existing.IdUsuario, active: false);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "DESACTIVAR_USUARIO",
            $"Eliminación lógica del usuario '{existing.AliasUsuario}' (ID {existing.CodigoId}, Perfil {existing.Nivel}).");
    }

    public void SetUserActive(UserSession session, string idUsuario, bool active)
    {
        if (!active)
        {
            DeleteUser(session, idUsuario);
            return;
        }

        EnsureAdministrator(session, "Activar usuario");

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        if (existing.Activo)
        {
            return;
        }

        users.SetActive(existing.IdUsuario, active: true);

        audit.Log(
            session.Usuario.AliasUsuario,
            "Usuarios",
            "ACTIVAR_USUARIO",
            $"Reactivación del usuario '{existing.AliasUsuario}' (ID {existing.CodigoId}, Perfil {existing.Nivel}).");
    }

    private void EnsureAdministrator(UserSession? session, string actionDescription)
    {
        if (session is null || !session.Usuario.Activo)
        {
            audit.Log(
                session?.Usuario.AliasUsuario ?? "ANONIMO",
                "Seguridad",
                "ACCESO_NO_AUTORIZADO",
                $"Intento sin sesión activa en Gestión de Usuarios: {actionDescription}.");
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
            throw new UnauthorizedAccessException($"Acceso denegado: el módulo Gestión de Usuarios es exclusivo del perfil Administrador ({actionDescription}).");
        }
    }

    private static void EnforceRolePermissionBoundary(User user)
    {
        if (user.Nivel == UserRoleLevel.Administrador)
        {
            // Garantizar acceso total para el perfil Administrador
            UserPermissions full = PermissionPresets.CreateForRole(UserRoleLevel.Administrador, user.IdUsuario);
            user.Permisos = full;
        }
        else
        {
            // Ningún perfil no-administrador puede poseer permisos de Gestión de Usuarios
            user.Permisos.MenuUsuarios = false;
            user.Permisos.IngresoUsuarios = false;
            user.Permisos.ListadoUsuarios = false;
            user.Permisos.NormalizeHierarchy();
        }
    }

    private static void ValidateCommonUserFields(User user)
    {
        if (string.IsNullOrWhiteSpace(user.AliasUsuario) || user.AliasUsuario.Trim().Length < 3)
        {
            throw new ArgumentException("El nombre de usuario debe tener al menos 3 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(user.NombreUsuario) || string.IsNullOrWhiteSpace(user.ApePatUsuario))
        {
            throw new ArgumentException("Debe ingresar el nombre y apellido del usuario.");
        }

        if (!DomainValidator.IsValidEmailOrEmpty(user.MailUsuario))
        {
            throw new ArgumentException("El correo electrónico ingresado no tiene un formato válido.");
        }
    }

    private static List<string> CollectChangedFields(User before, User after, bool passwordChanged)
    {
        List<string> changes = [];
        if (!string.Equals(before.AliasUsuario, after.AliasUsuario, StringComparison.Ordinal)) changes.Add("Usuario");
        if (!string.Equals(before.NombreUsuario, after.NombreUsuario, StringComparison.Ordinal)) changes.Add("Nombre");
        if (!string.Equals(before.Apellidos, after.Apellidos, StringComparison.Ordinal)) changes.Add("Apellido");
        if (!string.Equals(before.MailUsuario, after.MailUsuario, StringComparison.Ordinal)) changes.Add("Correo");
        if (!string.Equals(before.FonoUsuario, after.FonoUsuario, StringComparison.Ordinal)) changes.Add("Teléfono");
        if (before.Nivel != after.Nivel) changes.Add($"Perfil({before.Nivel}->{after.Nivel})");
        if (before.Activo != after.Activo) changes.Add($"Estado({(after.Activo ? "Activo" : "Inactivo")})");
        if (before.EsSolicitante != after.EsSolicitante) changes.Add("Solicitante");
        if (passwordChanged) changes.Add("Contraseña");
        return changes;
    }
}
