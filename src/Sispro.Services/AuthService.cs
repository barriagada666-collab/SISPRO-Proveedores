using System.Collections.Concurrent;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Core.Security;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class AuthService(IUserRepository users, IAuditRepository audit)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, (int Failures, DateTime? LockedUntil)> _attempts = new(StringComparer.OrdinalIgnoreCase);

    public AuthResult Login(string? alias, string? plainPassword)
    {
        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrEmpty(plainPassword))
        {
            return AuthResult.Fail("Debe ingresar usuario y contraseña.");
        }

        string normalizedAlias = alias.Trim();
        if (_attempts.TryGetValue(normalizedAlias, out (int Failures, DateTime? LockedUntil) state) &&
            state.LockedUntil.HasValue &&
            state.LockedUntil.Value > DateTime.UtcNow)
        {
            int remaining = (int)Math.Ceiling((state.LockedUntil.Value - DateTime.UtcNow).TotalSeconds);
            return AuthResult.Fail($"Cuenta bloqueada temporalmente por intentos fallidos. Intente nuevamente en {remaining} segundos.");
        }

        User? user = users.GetByAlias(normalizedAlias);
        if (user is null)
        {
            RegisterFailure(normalizedAlias);
            audit.Log(normalizedAlias, "Seguridad", "LOGIN_FALLIDO", "Usuario no registrado.");
            return AuthResult.Fail("Usuario o contraseña incorrectos.");
        }

        if (!user.Activo)
        {
            audit.Log(user.AliasUsuario, "Seguridad", "LOGIN_DENEGADO", "Cuenta inactiva.");
            return AuthResult.Fail("La cuenta de usuario se encuentra deshabilitada. Contacte al administrador.");
        }

        if (!PasswordSecurity.VerifyPassword(plainPassword, user.PassHash, user.AliasUsuario, out bool needsUpgrade))
        {
            RegisterFailure(normalizedAlias);
            audit.Log(user.AliasUsuario, "Seguridad", "LOGIN_FALLIDO", "Contraseña incorrecta.");
            return AuthResult.Fail("Usuario o contraseña incorrectos.");
        }

        _attempts.TryRemove(normalizedAlias, out _);

        bool upgraded = false;
        if (needsUpgrade)
        {
            string modernHash = PasswordSecurity.HashPassword(plainPassword);
            users.UpdatePasswordHash(user.IdUsuario, modernHash);
            user.PassHash = modernHash;
            upgraded = true;
            audit.Log(user.AliasUsuario, "Seguridad", "PASSWORD_UPGRADE", "Contraseña migrada automáticamente desde cifrado VB6 a PBKDF2-SHA256.");
        }

        users.UpdateLastLogin(user.IdUsuario);
        user.UltimoAcceso = DateTime.Now;
        user.Permisos.NormalizeHierarchy();

        audit.Log(user.AliasUsuario, "Seguridad", "LOGIN_OK", $"Inicio de sesión exitoso (Nivel: {user.Nivel}).");
        return AuthResult.Ok(new UserSession { Usuario = user }, upgraded);
    }

    public void ChangeOwnPassword(UserSession session, string currentPassword, string newPassword, string confirmPassword)
    {
        User user = users.GetById(session.Usuario.IdUsuario)
            ?? throw new InvalidOperationException("El usuario actual no existe en la base de datos.");

        if (!PasswordSecurity.VerifyPassword(currentPassword, user.PassHash, user.AliasUsuario, out _))
        {
            throw new UnauthorizedAccessException("La contraseña actual ingresada es incorrecta.");
        }

        string? policyError = DomainValidator.ValidatePasswordPolicy(newPassword, confirmPassword);
        if (policyError is not null)
        {
            throw new ArgumentException(policyError);
        }

        string newHash = PasswordSecurity.HashPassword(newPassword);
        users.UpdatePasswordHash(user.IdUsuario, newHash);
        session.Usuario.PassHash = newHash;
        audit.Log(session.Usuario.AliasUsuario, "Seguridad", "CAMBIO_CLAVE", "El usuario actualizó su contraseña.");
    }

    public static void EnsurePermission(UserSession? session, Func<UserPermissions, bool> predicate, string actionDescription)
    {
        if (session is null)
        {
            throw new UnauthorizedAccessException("No hay una sesión activa.");
        }

        if (!predicate(session.Permisos))
        {
            throw new UnauthorizedAccessException($"No posee permisos suficientes para: {actionDescription}.");
        }
    }

    private void RegisterFailure(string alias)
    {
        _attempts.AddOrUpdate(
            alias,
            _ => (1, null),
            (_, existing) =>
            {
                int next = existing.Failures + 1;
                DateTime? lockedUntil = next >= MaxFailedAttempts ? DateTime.UtcNow.Add(LockoutDuration) : null;
                return (next >= MaxFailedAttempts ? 0 : next, lockedUntil);
            });
    }
}
