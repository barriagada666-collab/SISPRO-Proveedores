using Microsoft.Data.Sqlite;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class UserRepository(SqliteConnectionFactory factory) : IUserRepository
{
    private const string SelectBase = """
        SELECT u.id_usuario, u.alias_usuario, u.pass_hash, u.nombre_usuario, u.ape_pat_usuario,
               u.ape_mat_usuario, u.id_nivel, u.mail_usuario, u.fono_usuario, u.es_solicitante,
               u.activo, u.fecha_creacion, u.ultimo_acceso,
               p.m1_ot, p.tab_0_1_ingreso_ot, p.tab_0_vbj, p.tab_0_vbs,
               p.tab_0_2_busqueda_ot, p.tab_0_3_impresion_ot,
               p.m2_facturas, p.tab_1_0_ingreso_fac, p.tab_1_1_busqueda_fac,
               p.m3_mantenedor,
               p.m4_usuarios, p.tab_2_0_ingreso_us, p.tab_2_1_listado_us
        FROM Usuarios u
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

    public IReadOnlyList<User> GetAll(string? search = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        if (string.IsNullOrWhiteSpace(search))
        {
            cmd.CommandText = $"{SelectBase} ORDER BY u.alias_usuario;";
        }
        else
        {
            cmd.CommandText = $"""
                {SelectBase}
                WHERE u.alias_usuario LIKE $q
                   OR u.nombre_usuario LIKE $q
                   OR u.ape_pat_usuario LIKE $q
                   OR u.ape_mat_usuario LIKE $q
                   OR u.id_usuario LIKE $q
                ORDER BY u.alias_usuario;
                """;
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        List<User> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapUser(rdr));
        }

        return list;
    }

    public void Create(User user)
    {
        user.Permisos.IdUsuario = user.IdUsuario;
        user.Permisos.NormalizeHierarchy();

        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Usuarios(
                    id_usuario, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
                    ape_mat_usuario, id_nivel, mail_usuario, fono_usuario, es_solicitante, activo
                ) VALUES (
                    $id, $alias, $hash, $nom, $pat, $mat, $nivel, $mail, $fono, $sol, $act
                );
                """;
            BindUserParameters(cmd, user, includePassword: true);
            cmd.ExecuteNonQuery();
        }

        UpsertPermissions(conn, tx, user.Permisos);
        tx.Commit();
    }

    public void Update(User user, bool updatePassword)
    {
        user.Permisos.IdUsuario = user.IdUsuario;
        user.Permisos.NormalizeHierarchy();

        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = updatePassword
                ? """
                  UPDATE Usuarios
                  SET alias_usuario = $alias, pass_hash = $hash, nombre_usuario = $nom,
                      ape_pat_usuario = $pat, ape_mat_usuario = $mat, id_nivel = $nivel,
                      mail_usuario = $mail, fono_usuario = $fono, es_solicitante = $sol, activo = $act
                  WHERE id_usuario = $id;
                  """
                : """
                  UPDATE Usuarios
                  SET alias_usuario = $alias, nombre_usuario = $nom,
                      ape_pat_usuario = $pat, ape_mat_usuario = $mat, id_nivel = $nivel,
                      mail_usuario = $mail, fono_usuario = $fono, es_solicitante = $sol, activo = $act
                  WHERE id_usuario = $id;
                  """;
            BindUserParameters(cmd, user, includePassword: updatePassword);
            cmd.ExecuteNonQuery();
        }

        UpsertPermissions(conn, tx, user.Permisos);
        tx.Commit();
    }

    public void UpdatePasswordHash(string idUsuario, string newHash)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Usuarios SET pass_hash = $hash WHERE id_usuario = $id;";
        cmd.Parameters.AddWithValue("$hash", newHash);
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

    public void Delete(string idUsuario)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Usuarios WHERE id_usuario = $id;";
        cmd.Parameters.AddWithValue("$id", idUsuario);
        cmd.ExecuteNonQuery();
    }

    public int CountActiveAdmins()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM Usuarios u
            INNER JOIN Permisos p ON p.id_usuario = u.id_usuario
            WHERE u.activo = 1 AND (u.id_nivel = 4 OR (p.m4_usuarios = 1 AND p.tab_2_0_ingreso_us = 1));
            """;
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
        cmd.Parameters.AddWithValue("$mail", user.MailUsuario.Trim());
        cmd.Parameters.AddWithValue("$fono", user.FonoUsuario.Trim());
        cmd.Parameters.AddWithValue("$sol", user.EsSolicitante ? 1 : 0);
        cmd.Parameters.AddWithValue("$act", user.Activo ? 1 : 0);
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
        return new User
        {
            IdUsuario = id,
            AliasUsuario = rdr.GetString(1),
            PassHash = rdr.GetString(2),
            NombreUsuario = rdr.GetString(3),
            ApePatUsuario = rdr.GetString(4),
            ApeMatUsuario = rdr.GetString(5),
            Nivel = (UserRoleLevel)rdr.GetInt32(6),
            MailUsuario = rdr.GetString(7),
            FonoUsuario = rdr.GetString(8),
            EsSolicitante = rdr.GetInt32(9) != 0,
            Activo = rdr.GetInt32(10) != 0,
            FechaCreacion = DateTime.TryParse(rdr.GetString(11), out DateTime fc) ? fc : DateTime.Now,
            UltimoAcceso = !rdr.IsDBNull(12) && DateTime.TryParse(rdr.GetString(12), out DateTime ua) ? ua : null,
            Permisos = new UserPermissions
            {
                IdUsuario = id,
                MenuOrdenesTrabajo = !rdr.IsDBNull(13) && rdr.GetInt32(13) != 0,
                IngresoOt = !rdr.IsDBNull(14) && rdr.GetInt32(14) != 0,
                VistoBuenoJefatura = !rdr.IsDBNull(15) && rdr.GetInt32(15) != 0,
                VistoBuenoSupervisor = !rdr.IsDBNull(16) && rdr.GetInt32(16) != 0,
                BusquedaOt = !rdr.IsDBNull(17) && rdr.GetInt32(17) != 0,
                ImpresionOt = !rdr.IsDBNull(18) && rdr.GetInt32(18) != 0,
                MenuFacturas = !rdr.IsDBNull(19) && rdr.GetInt32(19) != 0,
                IngresoFacturas = !rdr.IsDBNull(20) && rdr.GetInt32(20) != 0,
                BusquedaFacturas = !rdr.IsDBNull(21) && rdr.GetInt32(21) != 0,
                MenuMantenedor = !rdr.IsDBNull(22) && rdr.GetInt32(22) != 0,
                MenuUsuarios = !rdr.IsDBNull(23) && rdr.GetInt32(23) != 0,
                IngresoUsuarios = !rdr.IsDBNull(24) && rdr.GetInt32(24) != 0,
                ListadoUsuarios = !rdr.IsDBNull(25) && rdr.GetInt32(25) != 0
            }
        };
    }
}
