using System.Globalization;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class AssetTreeRepository(SqliteConnectionFactory factory) : IAssetTreeRepository
{
    private const string SelectFields = """
        a.id_activo, a.parent_id, a.nivel, a.descripcion, a.activo,
        a.cod_sap, a.fabricante, a.serie, a.modelo, a.tipo_equipo,
        a.estado_operativo, a.categoria, a.prioridad, a.material,
        a.vida_util, a.observacion, a.fecha_instalacion, a.valor_compra, a.info_tecnica_json
        """;

    private static readonly string[] AllowedAssetLevelColumns =
    [
        "id_n1", "id_n2", "id_n3", "id_n4", "id_n5",
        "id_n6", "id_n7", "id_n8", "id_n9", "id_n10"
    ];

    private static readonly string[] AssetReferenceTables =
    [
        "OrdenesTrabajo",
        "Mant_OT",
        "Mant_MP",
        "Mant_TareasMP",
        "Mant_Checklist"
    ];

    public AssetNode? GetById(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            SELECT {SelectFields},
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
            SELECT {SelectFields},
                   EXISTS(SELECT 1 FROM Activos c WHERE c.parent_id = a.id_activo {(includeInactive ? string.Empty : "AND c.activo = 1")})
            FROM Activos a
            WHERE {parentClause} {activeClause}
            ORDER BY a.descripcion COLLATE NOCASE ASC, a.id_activo ASC;
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
            SELECT {SelectFields},
                   EXISTS(SELECT 1 FROM Activos c WHERE c.parent_id = a.id_activo AND c.activo = 1)
            FROM Activos a
            WHERE (a.descripcion LIKE $q
                OR a.cod_sap LIKE $q
                OR a.fabricante LIKE $q
                OR a.modelo LIKE $q
                OR a.serie LIKE $q
                OR CAST(a.id_activo AS TEXT) = $exact) {activeClause}
            ORDER BY a.descripcion COLLATE NOCASE ASC, a.id_activo ASC
            LIMIT 120;
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
            WITH RECURSIVE tree_path(id_activo, parent_id, nivel, descripcion, activo, depth) AS (
                SELECT id_activo, parent_id, nivel, descripcion, activo, 0
                FROM Activos WHERE id_activo = $id
                UNION ALL
                SELECT a.id_activo, a.parent_id, a.nivel, a.descripcion, a.activo, tp.depth + 1
                FROM Activos a
                INNER JOIN tree_path tp ON a.id_activo = tp.parent_id
                WHERE tp.depth < 64
            )
            SELECT id_activo, parent_id, nivel, descripcion, activo
            FROM tree_path
            ORDER BY depth DESC;
            """;
        cmd.Parameters.AddWithValue("$id", idActivo);

        List<AssetNode> path = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            path.Add(new AssetNode
            {
                IdActivo = rdr.GetInt32(0),
                ParentId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Nivel = rdr.GetInt32(2),
                Descripcion = rdr.GetString(3),
                Activo = rdr.GetInt32(4) != 0
            });
        }

        return path;
    }

    public int Create(AssetNode node)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        if (node.IdActivo <= 0)
        {
            using SqliteCommand nextCmd = conn.CreateCommand();
            nextCmd.Transaction = tx;
            nextCmd.CommandText = "SELECT COALESCE(MAX(id_activo), 0) + 1 FROM Activos;";
            node.IdActivo = Convert.ToInt32(nextCmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Activos(
                    id_activo, parent_id, nivel, descripcion, activo,
                    cod_sap, fabricante, serie, modelo, tipo_equipo,
                    estado_operativo, categoria, prioridad, observacion
                ) VALUES (
                    $id, $pid, $lvl, $desc, $act,
                    $sap, $fab, $ser, $mod, $tipo,
                    $estOp, $cat, $prio, $obs
                );
                """;
            cmd.Parameters.AddWithValue("$id", node.IdActivo);
            cmd.Parameters.AddWithValue("$pid", node.ParentId.HasValue ? node.ParentId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("$lvl", Math.Clamp(node.Nivel, 1, 10));
            cmd.Parameters.AddWithValue("$desc", node.Descripcion.Trim());
            cmd.Parameters.AddWithValue("$act", node.Activo ? 1 : 0);
            cmd.Parameters.AddWithValue("$sap", node.CodSap.Trim());
            cmd.Parameters.AddWithValue("$fab", node.Fabricante.Trim());
            cmd.Parameters.AddWithValue("$ser", node.Serie.Trim());
            cmd.Parameters.AddWithValue("$mod", node.Modelo.Trim());
            cmd.Parameters.AddWithValue("$tipo", node.TipoEquipo.Trim());
            cmd.Parameters.AddWithValue("$estOp", node.EstadoOperativo.Trim());
            cmd.Parameters.AddWithValue("$cat", node.Categoria.Trim());
            cmd.Parameters.AddWithValue("$prio", node.Prioridad);
            cmd.Parameters.AddWithValue("$obs", node.Observacion.Trim());
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return node.IdActivo;
    }

    public void Update(AssetNode node)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Activos
            SET descripcion = $desc,
                activo = $act,
                cod_sap = $sap,
                fabricante = $fab,
                serie = $ser,
                modelo = $mod,
                tipo_equipo = $tipo,
                estado_operativo = $estOp,
                categoria = $cat,
                prioridad = $prio,
                observacion = $obs
            WHERE id_activo = $id;
            """;
        cmd.Parameters.AddWithValue("$id", node.IdActivo);
        cmd.Parameters.AddWithValue("$desc", node.Descripcion.Trim());
        cmd.Parameters.AddWithValue("$act", node.Activo ? 1 : 0);
        cmd.Parameters.AddWithValue("$sap", node.CodSap.Trim());
        cmd.Parameters.AddWithValue("$fab", node.Fabricante.Trim());
        cmd.Parameters.AddWithValue("$ser", node.Serie.Trim());
        cmd.Parameters.AddWithValue("$mod", node.Modelo.Trim());
        cmd.Parameters.AddWithValue("$tipo", node.TipoEquipo.Trim());
        cmd.Parameters.AddWithValue("$estOp", node.EstadoOperativo.Trim());
        cmd.Parameters.AddWithValue("$cat", node.Categoria.Trim());
        cmd.Parameters.AddWithValue("$prio", node.Prioridad);
        cmd.Parameters.AddWithValue("$obs", node.Observacion.Trim());
        cmd.ExecuteNonQuery();
    }

    public void SetActive(int idActivo, bool active)
    {
        SetActiveMany([idActivo], active);
    }

    public int SetActiveMany(IReadOnlyCollection<int> ids, bool active)
    {
        int[] uniqueIds = ids.Where(id => id > 0).Distinct().ToArray();
        if (uniqueIds.Length == 0)
        {
            return 0;
        }

        int totalUpdated = 0;
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        const int chunkSize = 400;
        for (int offset = 0; offset < uniqueIds.Length; offset += chunkSize)
        {
            int count = Math.Min(chunkSize, uniqueIds.Length - offset);
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            List<string> paramNames = new(count);
            for (int i = 0; i < count; i++)
            {
                string pName = $"$id{i}";
                paramNames.Add(pName);
                cmd.Parameters.AddWithValue(pName, uniqueIds[offset + i]);
            }

            cmd.Parameters.AddWithValue("$act", active ? 1 : 0);
            cmd.CommandText = $"UPDATE Activos SET activo = $act WHERE id_activo IN ({string.Join(", ", paramNames)});";
            totalUpdated += cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return totalUpdated;
    }

    public int GetTotalCount()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Activos WHERE activo = 1;";
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int CountChildren(int idActivo, bool includeInactive = true)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string activeClause = includeInactive ? string.Empty : "AND activo = 1";
        cmd.CommandText = $"SELECT COUNT(*) FROM Activos WHERE parent_id = $id {activeClause};";
        cmd.Parameters.AddWithValue("$id", idActivo);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int CountWorkOrderReferences(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        int total = 0;

        foreach (string table in AssetReferenceTables)
        {
            List<string> existingCols = GetExistingLevelColumns(conn, table);
            if (existingCols.Count == 0)
            {
                continue;
            }

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM {table} WHERE $id IN ({string.Join(", ", existingCols)});";
            cmd.Parameters.AddWithValue("$id", idActivo);
            total += Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        return total;
    }

    public IReadOnlyList<int> GetDescendantIds(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE sub(id, depth) AS (
                SELECT id_activo, 1
                FROM Activos
                WHERE parent_id = $id
                UNION ALL
                SELECT a.id_activo, sub.depth + 1
                FROM Activos a
                INNER JOIN sub ON a.parent_id = sub.id
                WHERE sub.depth < 64
            )
            SELECT id FROM sub ORDER BY depth ASC, id ASC;
            """;
        cmd.Parameters.AddWithValue("$id", idActivo);

        List<int> result = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            result.Add(rdr.GetInt32(0));
        }

        return result;
    }

    public int GetSubtreeHeight(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE sub(id, h) AS (
                SELECT id_activo, 0
                FROM Activos
                WHERE id_activo = $id
                UNION ALL
                SELECT a.id_activo, sub.h + 1
                FROM Activos a
                INNER JOIN sub ON a.parent_id = sub.id
                WHERE sub.h < 64
            )
            SELECT COALESCE(MAX(h), 0) FROM sub;
            """;
        cmd.Parameters.AddWithValue("$id", idActivo);
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int GetDepth(int idActivo) => GetPathToRoot(idActivo).Count;

    public void Delete(int idActivo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Activos WHERE id_activo = $id;";
        cmd.Parameters.AddWithValue("$id", idActivo);
        cmd.ExecuteNonQuery();
    }

    public void MoveSubtree(int idActivo, int newParentId, int newLevel)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand moveCmd = conn.CreateCommand())
        {
            moveCmd.Transaction = tx;
            moveCmd.CommandText = "UPDATE Activos SET parent_id = $newParent WHERE id_activo = $id;";
            moveCmd.Parameters.AddWithValue("$newParent", newParentId);
            moveCmd.Parameters.AddWithValue("$id", idActivo);
            moveCmd.ExecuteNonQuery();
        }

        using (SqliteCommand lvlCmd = conn.CreateCommand())
        {
            lvlCmd.Transaction = tx;
            lvlCmd.CommandText = """
                WITH RECURSIVE sub(id, lvl) AS (
                    SELECT $id, $newLevel
                    UNION ALL
                    SELECT a.id_activo, sub.lvl + 1
                    FROM Activos a
                    INNER JOIN sub ON a.parent_id = sub.id
                    WHERE sub.lvl < 10
                )
                UPDATE Activos
                SET nivel = (SELECT lvl FROM sub WHERE sub.id = Activos.id_activo)
                WHERE id_activo IN (SELECT id FROM sub);
                """;
            lvlCmd.Parameters.AddWithValue("$id", idActivo);
            lvlCmd.Parameters.AddWithValue("$newLevel", Math.Clamp(newLevel, 1, 10));
            lvlCmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    public int CountLevelAnomalies()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE tree(id, real_lvl) AS (
                SELECT id_activo, 1
                FROM Activos
                WHERE parent_id IS NULL
                UNION ALL
                SELECT a.id_activo, tree.real_lvl + 1
                FROM Activos a
                INNER JOIN tree ON a.parent_id = tree.id
                WHERE tree.real_lvl < 64
            )
            SELECT COUNT(*)
            FROM Activos a
            INNER JOIN tree t ON t.id = a.id_activo
            WHERE a.nivel <> t.real_lvl;
            """;
        return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public int NormalizeLevels()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            WITH RECURSIVE tree(id, real_lvl) AS (
                SELECT id_activo, 1
                FROM Activos
                WHERE parent_id IS NULL
                UNION ALL
                SELECT a.id_activo, tree.real_lvl + 1
                FROM Activos a
                INNER JOIN tree ON a.parent_id = tree.id
                WHERE tree.real_lvl < 10
            )
            UPDATE Activos
            SET nivel = (SELECT real_lvl FROM tree WHERE tree.id = Activos.id_activo)
            WHERE id_activo IN (
                SELECT t.id
                FROM tree t
                INNER JOIN Activos a ON a.id_activo = t.id
                WHERE a.nivel <> t.real_lvl
            );
            """;
        return cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AssetTreeExportRow> GetSubtreeRows(int rootId, bool includeInactive = true)
    {
        AssetNode? rootNode = GetById(rootId);
        if (rootNode is null || (!includeInactive && !rootNode.Activo))
        {
            return [];
        }

        IReadOnlyList<AssetNode> path = GetPathToRoot(rootId);
        int startDepth = Math.Max(1, path.Count);
        string initialRuta = path.Count > 0 ? string.Join(" \\ ", path.Select(p => p.Descripcion)) : rootNode.Descripcion;
        string activeClause = includeInactive ? string.Empty : "AND a.activo = 1";

        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            WITH RECURSIVE sub(id_activo, depth, ruta, sortkey) AS (
                SELECT a.id_activo,
                       $startDepth,
                       $initialRuta,
                       lower(a.descripcion) || '_' || printf('%06d', a.id_activo)
                FROM Activos a
                WHERE a.id_activo = $rootId
                UNION ALL
                SELECT a.id_activo,
                       sub.depth + 1,
                       sub.ruta || ' \ ' || a.descripcion,
                       sub.sortkey || char(31) || lower(a.descripcion) || '_' || printf('%06d', a.id_activo)
                FROM Activos a
                INNER JOIN sub ON a.parent_id = sub.id_activo
                WHERE sub.depth < 64 {activeClause}
            )
            SELECT a.id_activo, a.parent_id, sub.depth, a.categoria, a.descripcion, sub.ruta,
                   a.cod_sap, a.fabricante, a.modelo, a.serie, a.activo
            FROM sub
            INNER JOIN Activos a ON a.id_activo = sub.id_activo
            ORDER BY sub.sortkey COLLATE NOCASE ASC;
            """;
        cmd.Parameters.AddWithValue("$rootId", rootId);
        cmd.Parameters.AddWithValue("$startDepth", startDepth);
        cmd.Parameters.AddWithValue("$initialRuta", initialRuta);

        List<AssetTreeExportRow> rows = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            rows.Add(new AssetTreeExportRow
            {
                IdActivo = rdr.GetInt32(0),
                ParentId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Depth = rdr.GetInt32(2),
                Categoria = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                Descripcion = rdr.GetString(4),
                Ruta = rdr.GetString(5),
                CodSap = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6),
                Fabricante = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
                Modelo = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8),
                Serie = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9),
                Activo = rdr.GetInt32(10) != 0
            });
        }

        return rows;
    }

    private static List<string> GetExistingLevelColumns(SqliteConnection conn, string tableName)
    {
        HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);
        using SqliteCommand info = conn.CreateCommand();
        info.CommandText = $"PRAGMA table_info({tableName});";
        using SqliteDataReader rdr = info.ExecuteReader();
        while (rdr.Read())
        {
            found.Add(rdr.GetString(1));
        }

        List<string> result = new(10);
        foreach (string allowed in AllowedAssetLevelColumns)
        {
            if (found.Contains(allowed))
            {
                result.Add(allowed);
            }
        }

        return result;
    }

    private static AssetNode Map(SqliteDataReader rdr) => new()
    {
        IdActivo = rdr.GetInt32(0),
        ParentId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
        Nivel = rdr.GetInt32(2),
        Descripcion = rdr.GetString(3),
        Activo = rdr.GetInt32(4) != 0,
        CodSap = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
        Fabricante = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6),
        Serie = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
        Modelo = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8),
        TipoEquipo = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9),
        EstadoOperativo = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10),
        Categoria = rdr.IsDBNull(11) ? string.Empty : rdr.GetString(11),
        Prioridad = rdr.IsDBNull(12) ? 0 : rdr.GetInt32(12),
        Material = rdr.IsDBNull(13) ? string.Empty : rdr.GetString(13),
        VidaUtil = rdr.IsDBNull(14) ? string.Empty : rdr.GetString(14),
        Observacion = rdr.IsDBNull(15) ? string.Empty : rdr.GetString(15),
        FechaInstalacion = rdr.IsDBNull(16) ? string.Empty : rdr.GetString(16),
        ValorCompra = rdr.IsDBNull(17) ? 0m : Convert.ToDecimal(rdr.GetDouble(17)),
        InfoTecnicaJson = rdr.IsDBNull(18) ? string.Empty : rdr.GetString(18),
        TieneHijos = rdr.GetInt32(19) != 0
    };
}
