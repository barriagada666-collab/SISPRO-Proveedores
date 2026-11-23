using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Enums;
using Sispro.Core.Security;

namespace Sispro.Data.Database;

/// <summary>
/// Procedimiento seguro e idempotente para aprovisionar una cuenta de Administrador inicial
/// sin almacenar contraseñas en texto plano en el código fuente.
/// </summary>
public static class InitialAdminProvisioner
{
    public const string EnvUser = "SISPRO_INIT_ADMIN_USER";
    public const string EnvPassword = "SISPRO_INIT_ADMIN_PASSWORD";
    public const string EnvPasswordHash = "SISPRO_INIT_ADMIN_HASH";
    public const string EnvName = "SISPRO_INIT_ADMIN_NAME";
    public const string EnvSurname = "SISPRO_INIT_ADMIN_SURNAME";
    public const string EnvEmail = "SISPRO_INIT_ADMIN_EMAIL";
    public const string EnvPhone = "SISPRO_INIT_ADMIN_PHONE";

    /// <summary>
    /// Crea la cuenta Administrador inicial si no existe (idempotente).
    /// Devuelve true si se creó la cuenta, o false si ya existía o no se suministraron datos de aprovisionamiento.
    /// </summary>
    public static bool EnsureInitialAdmin(SqliteConnection conn, InitialAdminSeedOptions? options = null)
    {
        InitialAdminSeedOptions? resolved = options ?? TryReadFromEnvironment();
        if (resolved is null || string.IsNullOrWhiteSpace(resolved.AliasUsuario))
        {
            return false;
        }

        string alias = resolved.AliasUsuario.Trim();
        using (SqliteCommand existsCmd = conn.CreateCommand())
        {
            existsCmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE LOWER(alias_usuario) = LOWER($alias);";
            existsCmd.Parameters.AddWithValue("$alias", alias);
            long exists = Convert.ToInt64(existsCmd.ExecuteScalar() ?? 0L);
            if (exists > 0)
            {
                return false;
            }
        }

        string passwordHash = ResolveSecureHash(resolved);

        using SqliteTransaction tx = conn.BeginTransaction();

        int nextCodigoId;
        using (SqliteCommand nextCmd = conn.CreateCommand())
        {
            nextCmd.Transaction = tx;
            nextCmd.CommandText = "SELECT COALESCE(MAX(codigo_id), 0) + 1 FROM Usuarios;";
            nextCodigoId = Convert.ToInt32(nextCmd.ExecuteScalar());
        }

        string userId = $"USR-{nextCodigoId:D4}";
        using (SqliteCommand checkIdCmd = conn.CreateCommand())
        {
            checkIdCmd.Transaction = tx;
            checkIdCmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE id_usuario = $id;";
            checkIdCmd.Parameters.AddWithValue("$id", userId);
            while (Convert.ToInt64(checkIdCmd.ExecuteScalar() ?? 0L) > 0)
            {
                nextCodigoId++;
                userId = $"USR-{nextCodigoId:D4}";
                checkIdCmd.Parameters["$id"].Value = userId;
            }
        }

        using (SqliteCommand userCmd = conn.CreateCommand())
        {
            userCmd.Transaction = tx;
            userCmd.CommandText = """
                INSERT INTO Usuarios(
                    id_usuario, codigo_id, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
                    ape_mat_usuario, id_nivel, mail_usuario, fono_usuario, es_solicitante,
                    activo, debe_cambiar_clave, fecha_creacion, fecha_modificacion
                ) VALUES (
                    $id, $codigo, $alias, $hash, $nombre, $pat,
                    $mat, $nivel, $mail, $fono, 0,
                    1, $forceChange, datetime('now','localtime'), datetime('now','localtime')
                );
                """;
            userCmd.Parameters.AddWithValue("$id", userId);
            userCmd.Parameters.AddWithValue("$codigo", nextCodigoId);
            userCmd.Parameters.AddWithValue("$alias", alias);
            userCmd.Parameters.AddWithValue("$hash", passwordHash);
            userCmd.Parameters.AddWithValue("$nombre", string.IsNullOrWhiteSpace(resolved.Nombre) ? "Administrador" : resolved.Nombre.Trim());
            userCmd.Parameters.AddWithValue("$pat", string.IsNullOrWhiteSpace(resolved.ApellidoPaterno) ? "Sistema" : resolved.ApellidoPaterno.Trim());
            userCmd.Parameters.AddWithValue("$mat", resolved.ApellidoMaterno.Trim());
            userCmd.Parameters.AddWithValue("$nivel", (int)UserRoleLevel.Administrador);
            userCmd.Parameters.AddWithValue("$mail", resolved.Correo.Trim());
            userCmd.Parameters.AddWithValue("$fono", resolved.Telefono.Trim());
            userCmd.Parameters.AddWithValue("$forceChange", resolved.RequirePasswordChange ? 1 : 0);
            userCmd.ExecuteNonQuery();
        }

        using (SqliteCommand permCmd = conn.CreateCommand())
        {
            permCmd.Transaction = tx;
            permCmd.CommandText = """
                INSERT INTO Permisos(
                    id_usuario, m1_ot, tab_0_1_ingreso_ot, tab_0_vbj, tab_0_vbs,
                    tab_0_2_busqueda_ot, tab_0_3_impresion_ot, m2_facturas,
                    tab_1_0_ingreso_fac, tab_1_1_busqueda_fac, m3_mantenedor,
                    m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
                ) VALUES ($id, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
                """;
            permCmd.Parameters.AddWithValue("$id", userId);
            permCmd.ExecuteNonQuery();
        }

        using (SqliteCommand auditCmd = conn.CreateCommand())
        {
            auditCmd.Transaction = tx;
            auditCmd.CommandText = """
                INSERT INTO AuditLog(usuario, modulo, accion, detalle)
                VALUES ('SISTEMA', 'Seguridad', 'INIT_ADMIN_PROVISIONED', $detalle);
                """;
            auditCmd.Parameters.AddWithValue("$detalle",
                $"Cuenta inicial Administrador '{alias}' (ID {nextCodigoId}) creada de forma segura (CambioClavePrimerAcceso={resolved.RequirePasswordChange}).");
            auditCmd.ExecuteNonQuery();
        }

        tx.Commit();
        return true;
    }

    private static string ResolveSecureHash(InitialAdminSeedOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PrecomputedPasswordHash))
        {
            string hash = options.PrecomputedPasswordHash.Trim();
            if (!hash.StartsWith("PBKDF2$", StringComparison.Ordinal))
            {
                throw new ArgumentException("El hash precomputado debe utilizar formato PBKDF2-HMAC-SHA256 válido.");
            }

            return hash;
        }

        if (!string.IsNullOrWhiteSpace(options.PlainPassword))
        {
            return PasswordSecurity.HashPassword(options.PlainPassword);
        }

        throw new InvalidOperationException("Debe proporcionar una contraseña o hash PBKDF2 para aprovisionar el administrador inicial.");
    }

    private static InitialAdminSeedOptions? TryReadFromEnvironment()
    {
        string? alias = Environment.GetEnvironmentVariable(EnvUser);
        string? hash = Environment.GetEnvironmentVariable(EnvPasswordHash);
        string? plain = Environment.GetEnvironmentVariable(EnvPassword);

        if (string.IsNullOrWhiteSpace(alias) || (string.IsNullOrWhiteSpace(hash) && string.IsNullOrWhiteSpace(plain)))
        {
            return null;
        }

        return new InitialAdminSeedOptions
        {
            AliasUsuario = alias.Trim(),
            PrecomputedPasswordHash = string.IsNullOrWhiteSpace(hash) ? null : hash.Trim(),
            PlainPassword = string.IsNullOrWhiteSpace(plain) ? null : plain,
            Nombre = Environment.GetEnvironmentVariable(EnvName) ?? "Administrador",
            ApellidoPaterno = Environment.GetEnvironmentVariable(EnvSurname) ?? "Sistema",
            Correo = Environment.GetEnvironmentVariable(EnvEmail) ?? string.Empty,
            Telefono = Environment.GetEnvironmentVariable(EnvPhone) ?? string.Empty,
            RequirePasswordChange = true
        };
    }
}
