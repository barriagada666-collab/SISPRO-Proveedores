using Microsoft.Data.Sqlite;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class AssetTreeRepository(SqliteConnectionFactory factory) : IAssetTreeRepository
{
    public AssetNode? GetById(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT a.id_activo, a.parent_id, a.nivel, a.descripcion, a.activo,
                   EXISTS(SELECT 1 FROM Activos c WHERE c.parent_id = a.id_activo AND c.activo = 1)
            FROM Activos a
            WHERE a.id_activo = $id LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", idActivo);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? Map(rdr) : null;
    }

    public IReadOnlyList<AssetNode> GetChildren(int? parentId, bool includeInactive = false)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string parentClause = parentId.HasValue ? "a.parent_id = $pid" : "a.parent_id IS NULL";
        string activeClause = includeInactive ? string.Empty : "AND a.activo = 1";
        cmd.CommandText = $"""
            SELECT a.id_activo, a.parent_id, a.nivel, a.descripcion, a.activo,
                   EXISTS(SELECT 1 FROM Activos c WHERE c.parent_id = a.id_activo {(includeInactive ? string.Empty : "AND c.activo = 1")})
            FROM Activos a
            WHERE {parentClause} {activeClause}
            ORDER BY a.descripcion, a.id_activo;
            """;
        if (parentId.HasValue)
        {
            cmd.Parameters.AddWithValue("$pid", parentId.Value);
        }

        List<AssetNode> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(Map(rdr));
        }

        return list;
    }

    public IReadOnlyList<AssetNode> Search(string query, bool includeInactive = false)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string activeClause = includeInactive ? string.Empty : "AND a.activo = 1";
        cmd.CommandText = $"""
            SELECT a.id_activo, a.parent_id, a.nivel, a.descripcion, a.activo,
                   EXISTS(SELECT 1 FROM Activos c WHERE c.parent_id = a.id_activo AND c.activo = 1)
            FROM Activos a
            WHERE (a.descripcion LIKE $q OR CAST(a.id_activo AS TEXT) = $exact) {activeClause}
            ORDER BY a.nivel, a.descripcion
            LIMIT 100;
            """;
        cmd.Parameters.AddWithValue("$q", $"%{query.Trim()}%");
        cmd.Parameters.AddWithValue("$exact", query.Trim());

        List<AssetNode> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(Map(rdr));
        }

        return list;
    }

    public IReadOnlyList<AssetNode> GetPathToRoot(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE tree_path(id_activo, parent_id, nivel, descripcion, activo) AS (
                SELECT id_activo, parent_id, nivel, descripcion, activo
                FROM Activos WHERE id_activo = $id
                UNION ALL
                SELECT a.id_activo, a.parent_id, a.nivel, a.descripcion, a.activo
                FROM Activos a
                INNER JOIN tree_path tp ON a.id_activo = tp.parent_id
            )
            SELECT id_activo, parent_id, nivel, descripcion, activo, 0
            FROM tree_path
            ORDER BY nivel ASC;
            """;
        cmd.Parameters.AddWithValue("$id", idActivo);

        List<AssetNode> path = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            path.Add(Map(rdr));
        }

        return path;
    }

    public int Create(AssetNode node)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        if (node.IdActivo <= 0)
        {
            using SqliteCommand nextCmd = conn.CreateCommand();
            nextCmd.CommandText = "SELECT COALESCE(MAX(id_activo), 0) + 1 FROM Activos;";
            node.IdActivo = Convert.ToInt32(nextCmd.ExecuteScalar());
        }

        cmd.CommandText = """
            INSERT INTO Activos(id_activo, parent_id, nivel, descripcion, activo)
            VALUES ($id, $pid, $lvl, $desc, $act);
            """;
        cmd.Parameters.AddWithValue("$id", node.IdActivo);
        cmd.Parameters.AddWithValue("$pid", node.ParentId.HasValue ? node.ParentId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$lvl", Math.Clamp(node.Nivel, 1, 10));
        cmd.Parameters.AddWithValue("$desc", node.Descripcion.Trim());
        cmd.Parameters.AddWithValue("$act", node.Activo ? 1 : 0);
        cmd.ExecuteNonQuery();
        return node.IdActivo;
    }

    public void Update(AssetNode node)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Activos
            SET descripcion = $desc, activo = $act
            WHERE id_activo = $id;
            """;
        cmd.Parameters.AddWithValue("$id", node.IdActivo);
        cmd.Parameters.AddWithValue("$desc", node.Descripcion.Trim());
        cmd.Parameters.AddWithValue("$act", node.Activo ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public void SetActive(int idActivo, bool active)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Activos SET activo = $act WHERE id_activo = $id;";
        cmd.Parameters.AddWithValue("$id", idActivo);
        cmd.Parameters.AddWithValue("$act", active ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public int GetTotalCount()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Activos WHERE activo = 1;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static AssetNode Map(SqliteDataReader rdr) => new()
    {
        IdActivo = rdr.GetInt32(0),
        ParentId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
        Nivel = rdr.GetInt32(2),
        Descripcion = rdr.GetString(3),
        Activo = rdr.GetInt32(4) != 0,
        TieneHijos = rdr.GetInt32(5) != 0
    };
}
