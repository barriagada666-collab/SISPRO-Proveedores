using Microsoft.Data.Sqlite;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

/// <summary>
/// Repositorio SQLite parametrizado para el catálogo dinámico de Perfiles de Usuarios (tabla Niveles).
/// </summary>
public sealed class UserProfileRepository(SqliteConnectionFactory factory) : IUserProfileRepository
{
    private const string SelectSql = """
        SELECT n.id_nivel, n.tipo_nivel, COALESCE(n.descripcion, ''), COALESCE(n.es_sistema, 0),
               COALESCE(n.activo, 1),
               COALESCE(n.m1_ot, 1), COALESCE(n.tab_0_1_ingreso_ot, 1),
               COALESCE(n.tab_0_2_busqueda_ot, 1), COALESCE(n.tab_0_3_impresion_ot, 1),
               COALESCE(n.m3_mantenedor, 0), COALESCE(n.m4_usuarios, 0),
               COALESCE(n.tab_2_0_ingreso_us, 0), COALESCE(n.tab_2_1_listado_us, 0),
               (SELECT COUNT(*) FROM Usuarios u WHERE u.id_nivel = n.id_nivel) AS usr_cnt
        FROM Niveles n
        """;

    public IReadOnlyList<UserProfile> GetAll(bool includeInactive = true)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = includeInactive
            ? $"{SelectSql} ORDER BY COALESCE(n.es_sistema, 0) DESC, n.id_nivel ASC;"
            : $"{SelectSql} WHERE COALESCE(n.activo, 1) = 1 ORDER BY COALESCE(n.es_sistema, 0) DESC, n.id_nivel ASC;";

        List<UserProfile> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapProfile(rdr));
        }

        return list;
    }

    public UserProfile? GetById(int idNivel)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectSql} WHERE n.id_nivel = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", idNivel);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapProfile(rdr) : null;
    }

    public UserProfile? GetByName(string nombre)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectSql} WHERE LOWER(TRIM(n.tipo_nivel)) = LOWER(TRIM($nom)) LIMIT 1;";
        cmd.Parameters.AddWithValue("$nom", nombre.Trim());
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapProfile(rdr) : null;
    }

    public int Create(UserProfile profile)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        int nextId;
        using (SqliteCommand maxCmd = conn.CreateCommand())
        {
            maxCmd.Transaction = tx;
            maxCmd.CommandText = "SELECT MAX(4, COALESCE(MAX(id_nivel), 4)) + 1 FROM Niveles;";
            nextId = Convert.ToInt32(maxCmd.ExecuteScalar());
        }

        profile.IdNivel = nextId;
        profile.PermisosPorDefecto.NormalizeHierarchy();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Niveles(
                    id_nivel, tipo_nivel, descripcion, es_sistema, activo,
                    m1_ot, tab_0_1_ingreso_ot, tab_0_2_busqueda_ot, tab_0_3_impresion_ot,
                    m3_mantenedor, m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
                ) VALUES (
                    $id, $nom, $desc, 0, $act,
                    $m1, $t01, $t02, $t03, $m3, $m4, $t20, $t21
                );
                """;
            cmd.Parameters.AddWithValue("$id", nextId);
            BindProfileParameters(cmd, profile);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return nextId;
    }

    public void Update(UserProfile profile)
    {
        profile.PermisosPorDefecto.NormalizeHierarchy();
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Niveles
            SET tipo_nivel = $nom,
                descripcion = $desc,
                activo = $act,
                m1_ot = $m1,
                tab_0_1_ingreso_ot = $t01,
                tab_0_2_busqueda_ot = $t02,
                tab_0_3_impresion_ot = $t03,
                m3_mantenedor = $m3,
                m4_usuarios = $m4,
                tab_2_0_ingreso_us = $t20,
                tab_2_1_listado_us = $t21
            WHERE id_nivel = $id;
            """;
        cmd.Parameters.AddWithValue("$id", profile.IdNivel);
        BindProfileParameters(cmd, profile);
        cmd.ExecuteNonQuery();
    }

    public void SetActive(int idNivel, bool active)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Niveles SET activo = $act WHERE id_nivel = $id;";
        cmd.Parameters.AddWithValue("$id", idNivel);
        cmd.Parameters.AddWithValue("$act", active ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int idNivel)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Niveles WHERE id_nivel = $id AND COALESCE(es_sistema, 0) = 0;";
        cmd.Parameters.AddWithValue("$id", idNivel);
        cmd.ExecuteNonQuery();
    }

    public int CountUsers(int idNivel)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE id_nivel = $id;";
        cmd.Parameters.AddWithValue("$id", idNivel);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void BindProfileParameters(SqliteCommand cmd, UserProfile profile)
    {
        UserPermissions p = profile.PermisosPorDefecto;
        cmd.Parameters.AddWithValue("$nom", profile.Nombre.Trim());
        cmd.Parameters.AddWithValue("$desc", profile.Descripcion.Trim());
        cmd.Parameters.AddWithValue("$act", profile.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("$m1", p.MenuOrdenesTrabajo ? 1 : 0);
        cmd.Parameters.AddWithValue("$t01", p.IngresoOt ? 1 : 0);
        cmd.Parameters.AddWithValue("$t02", p.BusquedaOt ? 1 : 0);
        cmd.Parameters.AddWithValue("$t03", p.ImpresionOt ? 1 : 0);
        cmd.Parameters.AddWithValue("$m3", p.MenuMantenedor ? 1 : 0);
        cmd.Parameters.AddWithValue("$m4", p.MenuUsuarios ? 1 : 0);
        cmd.Parameters.AddWithValue("$t20", p.IngresoUsuarios ? 1 : 0);
        cmd.Parameters.AddWithValue("$t21", p.ListadoUsuarios ? 1 : 0);
    }

    private static UserProfile MapProfile(SqliteDataReader rdr)
    {
        int id = rdr.GetInt32(0);
        return new UserProfile
        {
            IdNivel = id,
            Nombre = rdr.GetString(1),
            Descripcion = rdr.GetString(2),
            EsSistema = rdr.GetInt32(3) != 0 || id is >= 1 and <= 4,
            Activo = rdr.GetInt32(4) != 0,
            CantidadUsuarios = rdr.IsDBNull(13) ? 0 : rdr.GetInt32(13),
            PermisosPorDefecto = new UserPermissions
            {
                MenuOrdenesTrabajo = rdr.GetInt32(5) != 0,
                IngresoOt = rdr.GetInt32(6) != 0,
                BusquedaOt = rdr.GetInt32(7) != 0,
                ImpresionOt = rdr.GetInt32(8) != 0,
                MenuMantenedor = rdr.GetInt32(9) != 0,
                MenuUsuarios = rdr.GetInt32(10) != 0,
                IngresoUsuarios = rdr.GetInt32(11) != 0,
                ListadoUsuarios = rdr.GetInt32(12) != 0
            }
        };
    }
}
