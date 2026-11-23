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
    public IReadOnlyList<User> GetAll(UserSession session, string? search = null)
    {
        AuthService.EnsurePermission(session, p => p.MenuUsuarios && (p.ListadoUsuarios || p.IngresoUsuarios), "Consultar usuarios");
        return users.GetAll(search);
    }

    public User? GetById(UserSession session, string idUsuario)
    {
        AuthService.EnsurePermission(session, p => p.MenuUsuarios, "Consultar usuario");
        return users.GetById(idUsuario);
    }

    public void CreateUser(UserSession session, User user, string plainPassword, string confirmPassword)
    {
        AuthService.EnsurePermission(session, p => p.MenuUsuarios && p.IngresoUsuarios, "Crear usuarios");

        string normalizedRut = RutValidator.Normalize(user.IdUsuario);
        if (!RutValidator.IsValid(normalizedRut))
        {
            throw new ArgumentException("El RUT ingresado no es válido (verifique cuerpo y dígito verificador).");
        }

        ValidateCommonUserFields(user);

        string? pwdError = DomainValidator.ValidatePasswordPolicy(plainPassword, confirmPassword);
        if (pwdError is not null)
        {
            throw new ArgumentException(pwdError);
        }

        user.IdUsuario = normalizedRut;
        if (users.GetById(user.IdUsuario) is not null)
        {
            throw new InvalidOperationException($"Ya existe un usuario registrado con el RUT {RutValidator.Format(user.IdUsuario)}.");
        }

        if (users.GetByAlias(user.AliasUsuario) is not null)
        {
            throw new InvalidOperationException($"El código/alias de usuario '{user.AliasUsuario}' ya se encuentra en uso.");
        }

        user.PassHash = PasswordSecurity.HashPassword(plainPassword);
        user.Permisos.IdUsuario = user.IdUsuario;
        user.Permisos.NormalizeHierarchy();

        users.Create(user);
        if (user.EsSolicitante)
        {
            requesters.EnsureFromUser(user);
        }

        audit.Log(session.Usuario.AliasUsuario, "Usuarios", "CREAR_USUARIO",
            $"Usuario '{user.AliasUsuario}' ({RutValidator.Format(user.IdUsuario)}, Nivel {user.Nivel}) creado.");
    }

    public void UpdateUser(UserSession session, User user, string? newPassword = null, string? confirmPassword = null)
    {
        AuthService.EnsurePermission(session, p => p.MenuUsuarios && p.IngresoUsuarios, "Modificar usuarios");

        User existing = users.GetById(user.IdUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        ValidateCommonUserFields(user);

        User? aliasConflict = users.GetByAlias(user.AliasUsuario);
        if (aliasConflict is not null && !string.Equals(aliasConflict.IdUsuario, existing.IdUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"El alias '{user.AliasUsuario}' ya pertenece a otro usuario.");
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
        }

        // Protect against demoting/disabling the last active administrator
        bool wasAdmin = existing.Activo && (existing.Nivel == UserRoleLevel.Administrador || (existing.Permisos.MenuUsuarios && existing.Permisos.IngresoUsuarios));
        bool staysAdmin = user.Activo && (user.Nivel == UserRoleLevel.Administrador || (user.Permisos.MenuUsuarios && user.Permisos.IngresoUsuarios));
        if (wasAdmin && !staysAdmin && users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("No se puede quitar privilegios ni desactivar al último administrador activo del sistema.");
        }

        user.Permisos.IdUsuario = user.IdUsuario;
        user.Permisos.NormalizeHierarchy();

        users.Update(user, updatePwd);
        if (user.EsSolicitante)
        {
            requesters.EnsureFromUser(user);
        }

        audit.Log(session.Usuario.AliasUsuario, "Usuarios", "MODIFICAR_USUARIO",
            $"Usuario '{user.AliasUsuario}' actualizado (CambioClave={updatePwd}, Activo={user.Activo}).");
    }

    public void DeleteUser(UserSession session, string idUsuario)
    {
        AuthService.EnsurePermission(session, p => p.MenuUsuarios && p.IngresoUsuarios, "Eliminar usuarios");

        if (string.Equals(session.Usuario.IdUsuario, idUsuario, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No puede eliminar su propia cuenta mientras mantiene la sesión activa.");
        }

        User existing = users.GetById(idUsuario)
            ?? throw new InvalidOperationException("El usuario seleccionado no existe.");

        bool isAdmin = existing.Activo && (existing.Nivel == UserRoleLevel.Administrador || (existing.Permisos.MenuUsuarios && existing.Permisos.IngresoUsuarios));
        if (isAdmin && users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("No se puede eliminar al último administrador activo del sistema.");
        }

        users.Delete(idUsuario);
        audit.Log(session.Usuario.AliasUsuario, "Usuarios", "ELIMINAR_USUARIO",
            $"Usuario '{existing.AliasUsuario}' ({RutValidator.Format(existing.IdUsuario)}) eliminado.");
    }

    private static void ValidateCommonUserFields(User user)
    {
        if (string.IsNullOrWhiteSpace(user.AliasUsuario) || user.AliasUsuario.Trim().Length < 3)
        {
            throw new ArgumentException("El código/alias de usuario debe tener al menos 3 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(user.NombreUsuario) || string.IsNullOrWhiteSpace(user.ApePatUsuario))
        {
            throw new ArgumentException("Debe ingresar los nombres y apellido paterno del usuario.");
        }

        if (!DomainValidator.IsValidEmailOrEmpty(user.MailUsuario))
        {
            throw new ArgumentException("El correo electrónico ingresado no es válido.");
        }
    }
}
