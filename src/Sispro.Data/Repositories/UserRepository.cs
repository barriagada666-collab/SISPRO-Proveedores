using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class UserRepository(SqliteConnectionFactory factory) : IUserRepository
{
    private const string SelectBase = """
        SELECT u.id_usuario, u.codigo_id, u.alias_usuario, u.pass_hash, u.nombre_usuario, u.ape_pat_usuario,
               u.ape_mat_usuario, u.id_nivel, u.mail_usuario, u.fono_usuario, u.es_solicitante,
               u.activo, u.debe_cambiar_clave, u.fecha_creacion, u.fecha_modificacion, u.ultimo_acceso,
               u.id_grupo, COALESCE(g.nombre, ''), COALESCE(u.row_version, 1),
               p.m1_ot, p.tab_0_1_ingreso_ot, p.tab_0_vbj, p.tab_0_vbs,
               p.tab_0_2_busqueda_ot, p.tab_0_3_impresion_ot,
               p.m2_facturas, p.tab_1_0_ingreso_fac, p.tab_1_1_busqueda_fac,
               p.m3_mantenedor,
               p.m4_usuarios, p.tab_2_0_ingreso_us, p.tab_2_1_listado_us
        FROM Usuarios u
        LEFT JOIN Grupos g ON g.id_grupo = u.id_grupo
        LEFT JOIN Permisos p ON p.id_usuario = u.id_usuario
        """;

    public User? GetByAlias(string alias)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectBase} WHERE LOWER(u.alias_usuario) = LOWER($alias) LIMIT 1;";
        cmd.Parameters.AddWithValue("$alias", alias.Trim());
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapUser(rdr) : null;
    }

    public User? GetById(string idUsuario)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectBase} WHERE u.id_usuario = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", idUsuario.Trim());
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapUser(rdr) : null;
    }

    public User? GetByCodigoId(int codigoId)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectBase} WHERE u.codigo_id = $code LIMIT 1;";
        cmd.Parameters.AddWithValue("$code", codigoId);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapUser(rdr) : null;
    }

    public IReadOnlyList<User> GetAll(string? search = null) =>
        Search(new UserSearchFilter { TextoBusqueda = search });

    public IReadOnlyList<User> Search(UserSearchFilter filter)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        StringBuilder sql = new(SelectBase);
        List<string> clauses = [];

        if (!string.IsNullOrWhiteSpace(filter.TextoBusqueda))
        {
            clauses.Add("""
                (
                    u.alias_usuario LIKE $q
                    OR u.nombre_usuario LIKE $q
                    OR u.ape_pat_usuario LIKE $q
                    OR u.ape_mat_usuario LIKE $q
                    OR (u.nombre_usuario || ' ' || u.ape_pat_usuario || ' ' || u.ape_mat_usuario) LIKE $q
                    OR u.mail_usuario LIKE $q
                    OR u.fono_usuario LIKE $q
                    OR COALESCE(g.nombre, '') LIKE $q
                    OR u.id_usuario LIKE $q
                    OR CAST(u.codigo_id AS TEXT) LIKE $q
                )
                """);
            cmd.Parameters.AddWithValue("$q", $"%{filter.TextoBusqueda.Trim()}%");
        }

        if (filter.Perfil.HasValue)
        {
            clauses.Add("u.id_nivel = $nivel");
            cmd.Parameters.AddWithValue("$nivel", (int)filter.Perfil.Value);
        }

        if (filter.SoloSinGrupo)
        {
            clauses.Add("u.id_grupo IS NULL");
        }
        else if (filter.IdGrupo.HasValue)
        {
            clauses.Add("u.id_grupo = $grupo");
            cmd.Parameters.AddWithValue("$grupo", filter.IdGrupo.Value);
        }

        if (filter.Activo.HasValue)
        {
            clauses.Add("u.activo = $activo");
            cmd.Parameters.AddWithValue("$activo", filter.Activo.Value ? 1 : 0);
        }

        if (filter.FechaCreacionInicio.HasValue)
        {
            clauses.Add("u.fecha_creacion >= $fini");
            cmd.Parameters.AddWithValue("$fini", filter.FechaCreacionInicio.Value.Date.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        }

        if (filter.FechaCreacionFin.HasValue)
        {
            clauses.Add("u.fecha_creacion <= $ffin");
            cmd.Parameters.AddWithValue("$ffin", filter.FechaCreacionFin.Value.Date.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        }

        if (clauses.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", clauses));
        }

        // Orden cronológico descendente predeterminado: registros más recientes primero
        sql.Append(" ORDER BY u.fecha_creacion DESC, u.codigo_id DESC;");
        cmd.CommandText = sql.ToString();

        List<User> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapUser(rdr));
        }

        return list;
    }

    public int Create(User user)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        int nextCodigo;
        using (SqliteCommand maxCmd = conn.CreateCommand())
        {
            maxCmd.Transaction = tx;
            maxCmd.CommandText = "SELECT COALESCE(MAX(codigo_id), 0) + 1 FROM Usuarios;";
            nextCodigo = Convert.ToInt32(maxCmd.ExecuteScalar());
        }

        if (string.IsNullOrWhiteSpace(user.IdUsuario))
        {
            string candidate = $"USR-{nextCodigo:D4}";
            using SqliteCommand checkCmd = conn.CreateCommand();
            checkCmd.Transaction = tx;
            checkCmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE id_usuario = $id;";
            checkCmd.Parameters.AddWithValue("$id", candidate);
            while (Convert.ToInt64(checkCmd.ExecuteScalar() ?? 0L) > 0)
            {
                nextCodigo++;
                candidate = $"USR-{nextCodigo:D4}";
                checkCmd.Parameters["$id"].Value = candidate;
            }

            user.IdUsuario = candidate;
        }

        user.CodigoId = nextCodigo;
        user.RowVersion = 1;
        user.Permisos.IdUsuario = user.IdUsuario;
        user.Permisos.NormalizeHierarchy();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Usuarios(
                    id_usuario, codigo_id, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
                    ape_mat_usuario, id_nivel, id_grupo, mail_usuario, fono_usuario, es_solicitante,
                    activo, debe_cambiar_clave, fecha_creacion, fecha_modificacion, row_version
                ) VALUES (
                    $id, $codigo, $alias, $hash, $nom, $pat,
                    $mat, $nivel, $grupo, $mail, $fono, $sol,
                    $act, $forceChange, datetime('now','localtime'), datetime('now','localtime'), 1
                );
                """;
            cmd.Parameters.AddWithValue("$codigo", user.CodigoId);
            BindUserParameters(cmd, user, includePassword: true);
            cmd.ExecuteNonQuery();
        }

        UpsertPermissions(conn, tx, user.Permisos);
        tx.Commit();
        return user.CodigoId;
    }

    public void Update(User user, bool updatePassword, int? expectedRowVersion = null)
    {
        user.Permisos.IdUsuario = user.IdUsuario;
        user.Permisos.NormalizeHierarchy();

        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        string versionClause = expectedRowVersion.HasValue ? " AND row_version = $ver" : string.Empty;

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = updatePassword
                ? $"""
                  UPDATE Usuarios
                  SET alias_usuario = $alias, pass_hash = $hash, nombre_usuario = $nom,
                      ape_pat_usuario = $pat, ape_mat_usuario = $mat, id_nivel = $nivel,
                      id_grupo = $grupo, mail_usuario = $mail, fono_usuario = $fono, es_solicitante = $sol,
                      activo = $act, debe_cambiar_clave = $forceChange,
                      fecha_modificacion = datetime('now','localtime'),
                      row_version = row_version + 1
                  WHERE id_usuario = $id{versionClause};
                  """
                : $"""
                  UPDATE Usuarios
                  SET alias_usuario = $alias, nombre_usuario = $nom,
                      ape_pat_usuario = $pat, ape_mat_usuario = $mat, id_nivel = $nivel,
                      id_grupo = $grupo, mail_usuario = $mail, fono_usuario = $fono, es_solicitante = $sol,
                      activo = $act, debe_cambiar_clave = $forceChange,
                      fecha_modificacion = datetime('now','localtime'),
                      row_version = row_version + 1
                  WHERE id_usuario = $id{versionClause};
                  """;
            BindUserParameters(cmd, user, includePassword: updatePassword);
            if (expectedRowVersion.HasValue)
            {
                cmd.Parameters.AddWithValue("$ver", expectedRowVersion.Value);
            }

            int affected = cmd.ExecuteNonQuery();
            if (expectedRowVersion.HasValue && affected == 0)
            {
                tx.Rollback();
                throw new ConcurrencyConflictException(
                    $"El usuario '{user.AliasUsuario}' fue modificado simultáneamente en otra sesión. Los datos fueron recargados desde la base de datos.");
            }
        }

        UpsertPermissions(conn, tx, user.Permisos);
        tx.Commit();
    }

    public void UpdatePasswordHash(string idUsuario, string newHash, bool requirePasswordChange = false)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Usuarios
            SET pass_hash = $hash,
                debe_cambiar_clave = $forceChange,
                fecha_modificacion = datetime('now','localtime'),
                row_version = row_version + 1
            WHERE id_usuario = $id;
            """;
        cmd.Parameters.AddWithValue("$hash", newHash);
        cmd.Parameters.AddWithValue("$forceChange", requirePasswordChange ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", idUsuario);
        cmd.ExecuteNonQuery();
    }

    public void UpdateLastLogin(string idUsuario)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Usuarios SET ultimo_acceso = datetime('now','localtime') WHERE id_usuario = $id;";
        cmd.Parameters.AddWithValue("$id", idUsuario);
        cmd.ExecuteNonQuery();
    }

    public void SetActive(string idUsuario, bool active)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Usuarios
            SET activo = $act,
                fecha_modificacion = datetime('now','localtime'),
                row_version = row_version + 1
            WHERE id_usuario = $id;
            """;
        cmd.Parameters.AddWithValue("$act", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", idUsuario);
        cmd.ExecuteNonQuery();
    }

    public void SetGroup(string idUsuario, int? idGrupo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Usuarios
            SET id_grupo = $grp,
                fecha_modificacion = datetime('now','localtime'),
                row_version = row_version + 1
            WHERE id_usuario = $id;
            """;
        cmd.Parameters.AddWithValue("$grp", idGrupo.HasValue ? idGrupo.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$id", idUsuario);
        cmd.ExecuteNonQuery();
    }

    public void SetRole(string idUsuario, UserRoleLevel newRole, UserPermissions normalizedPermissions)
    {
        normalizedPermissions.IdUsuario = idUsuario;
        normalizedPermissions.NormalizeHierarchy();

        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE Usuarios
                SET id_nivel = $nivel,
                    fecha_modificacion = datetime('now','localtime'),
                    row_version = row_version + 1
                WHERE id_usuario = $id;
                """;
            cmd.Parameters.AddWithValue("$nivel", (int)newRole);
            cmd.Parameters.AddWithValue("$id", idUsuario);
            cmd.ExecuteNonQuery();
        }

        UpsertPermissions(conn, tx, normalizedPermissions);
        tx.Commit();
    }

    public int CountActiveAdmins()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE activo = 1 AND id_nivel = 4;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int CountWithoutGroup()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE id_grupo IS NULL;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void BindUserParameters(SqliteCommand cmd, User user, bool includePassword)
    {
        cmd.Parameters.AddWithValue("$id", user.IdUsuario.Trim());
        cmd.Parameters.AddWithValue("$alias", user.AliasUsuario.Trim());
        if (includePassword)
        {
            cmd.Parameters.AddWithValue("$hash", user.PassHash);
        }

        cmd.Parameters.AddWithValue("$nom", user.NombreUsuario.Trim());
        cmd.Parameters.AddWithValue("$pat", user.ApePatUsuario.Trim());
        cmd.Parameters.AddWithValue("$mat", user.ApeMatUsuario.Trim());
        cmd.Parameters.AddWithValue("$nivel", (int)user.Nivel);
        cmd.Parameters.AddWithValue("$grupo", user.IdGrupo.HasValue ? user.IdGrupo.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$mail", user.MailUsuario.Trim());
        cmd.Parameters.AddWithValue("$fono", user.FonoUsuario.Trim());
        cmd.Parameters.AddWithValue("$sol", user.EsSolicitante ? 1 : 0);
        cmd.Parameters.AddWithValue("$act", user.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("$forceChange", user.DebeCambiarClave ? 1 : 0);
    }

    private static void UpsertPermissions(SqliteConnection conn, SqliteTransaction tx, UserPermissions p)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO Permisos(
                id_usuario, m1_ot, tab_0_1_ingreso_ot, tab_0_vbj, tab_0_vbs,
                tab_0_2_busqueda_ot, tab_0_3_impresion_ot, m2_facturas,
                tab_1_0_ingreso_fac, tab_1_1_busqueda_fac, m3_mantenedor,
                m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
            ) VALUES (
                $id, $m1, $t01, $vbj, $vbs, $t02, $t03, $m2, $t10, $t11, $m3, $m4, $t20, $t21
            )
            ON CONFLICT(id_usuario) DO UPDATE SET
                m1_ot = excluded.m1_ot,
                tab_0_1_ingreso_ot = excluded.tab_0_1_ingreso_ot,
                tab_0_vbj = excluded.tab_0_vbj,
                tab_0_vbs = excluded.tab_0_vbs,
                tab_0_2_busqueda_ot = excluded.tab_0_2_busqueda_ot,
                tab_0_3_impresion_ot = excluded.tab_0_3_impresion_ot,
                m2_facturas = excluded.m2_facturas,
                tab_1_0_ingreso_fac = excluded.tab_1_0_ingreso_fac,
                tab_1_1_busqueda_fac = excluded.tab_1_1_busqueda_fac,
                m3_mantenedor = excluded.m3_mantenedor,
                m4_usuarios = excluded.m4_usuarios,
                tab_2_0_ingreso_us = excluded.tab_2_0_ingreso_us,
                tab_2_1_listado_us = excluded.tab_2_1_listado_us;
            """;
        cmd.Parameters.AddWithValue("$id", p.IdUsuario.Trim());
        cmd.Parameters.AddWithValue("$m1", p.MenuOrdenesTrabajo ? 1 : 0);
        cmd.Parameters.AddWithValue("$t01", p.IngresoOt ? 1 : 0);
        cmd.Parameters.AddWithValue("$vbj", p.VistoBuenoJefatura ? 1 : 0);
        cmd.Parameters.AddWithValue("$vbs", p.VistoBuenoSupervisor ? 1 : 0);
        cmd.Parameters.AddWithValue("$t02", p.BusquedaOt ? 1 : 0);
        cmd.Parameters.AddWithValue("$t03", p.ImpresionOt ? 1 : 0);
        cmd.Parameters.AddWithValue("$m2", p.MenuFacturas ? 1 : 0);
        cmd.Parameters.AddWithValue("$t10", p.IngresoFacturas ? 1 : 0);
        cmd.Parameters.AddWithValue("$t11", p.BusquedaFacturas ? 1 : 0);
        cmd.Parameters.AddWithValue("$m3", p.MenuMantenedor ? 1 : 0);
        cmd.Parameters.AddWithValue("$m4", p.MenuUsuarios ? 1 : 0);
        cmd.Parameters.AddWithValue("$t20", p.IngresoUsuarios ? 1 : 0);
        cmd.Parameters.AddWithValue("$t21", p.ListadoUsuarios ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    private static User MapUser(SqliteDataReader rdr)
    {
        string id = rdr.GetString(0);
        DateTime fechaCreacion = !rdr.IsDBNull(13) && DateTime.TryParse(rdr.GetString(13), out DateTime fc) ? fc : DateTime.Now;
        DateTime fechaModificacion = !rdr.IsDBNull(14) && DateTime.TryParse(rdr.GetString(14), out DateTime fm) ? fm : fechaCreacion;
        DateTime? ultimoAcceso = !rdr.IsDBNull(15) && DateTime.TryParse(rdr.GetString(15), out DateTime ua) ? ua : null;

        return new User
        {
            IdUsuario = id,
            CodigoId = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1),
            AliasUsuario = rdr.GetString(2),
            PassHash = rdr.GetString(3),
            NombreUsuario = rdr.GetString(4),
            ApePatUsuario = rdr.GetString(5),
            ApeMatUsuario = rdr.GetString(6),
            Nivel = (UserRoleLevel)rdr.GetInt32(7),
            MailUsuario = rdr.GetString(8),
            FonoUsuario = rdr.GetString(9),
            EsSolicitante = rdr.GetInt32(10) != 0,
            Activo = rdr.GetInt32(11) != 0,
            DebeCambiarClave = !rdr.IsDBNull(12) && rdr.GetInt32(12) != 0,
            FechaCreacion = fechaCreacion,
            FechaModificacion = fechaModificacion,
            UltimoAcceso = ultimoAcceso,
            IdGrupo = rdr.IsDBNull(16) ? null : rdr.GetInt32(16),
            NombreGrupo = rdr.GetString(17),
            RowVersion = rdr.IsDBNull(18) ? 1 : rdr.GetInt32(18),
            Permisos = new UserPermissions
            {
                IdUsuario = id,
                MenuOrdenesTrabajo = !rdr.IsDBNull(19) && rdr.GetInt32(19) != 0,
                IngresoOt = !rdr.IsDBNull(20) && rdr.GetInt32(20) != 0,
                VistoBuenoJefatura = !rdr.IsDBNull(21) && rdr.GetInt32(21) != 0,
                VistoBuenoSupervisor = !rdr.IsDBNull(22) && rdr.GetInt32(22) != 0,
                BusquedaOt = !rdr.IsDBNull(23) && rdr.GetInt32(23) != 0,
                ImpresionOt = !rdr.IsDBNull(24) && rdr.GetInt32(24) != 0,
                MenuFacturas = !rdr.IsDBNull(25) && rdr.GetInt32(25) != 0,
                IngresoFacturas = !rdr.IsDBNull(26) && rdr.GetInt32(26) != 0,
                BusquedaFacturas = !rdr.IsDBNull(27) && rdr.GetInt32(27) != 0,
                MenuMantenedor = !rdr.IsDBNull(28) && rdr.GetInt32(28) != 0,
                MenuUsuarios = !rdr.IsDBNull(29) && rdr.GetInt32(29) != 0,
                IngresoUsuarios = !rdr.IsDBNull(30) && rdr.GetInt32(30) != 0,
                ListadoUsuarios = !rdr.IsDBNull(31) && rdr.GetInt32(31) != 0
            }
        };
    }
}
