using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class GroupRepository(SqliteConnectionFactory factory) : IGroupRepository
{
    private const string SelectBase = """
        SELECT g.id_grupo, g.nombre, g.descripcion, g.id_responsable,
               COALESCE(r.alias_usuario, ''),
               COALESCE(TRIM(r.nombre_usuario || ' ' || r.ape_pat_usuario || ' ' || r.ape_mat_usuario), ''),
               g.activo,
               (SELECT COUNT(*) FROM Usuarios u WHERE u.id_grupo = g.id_grupo),
               g.fecha_creacion, g.fecha_modificacion, g.row_version
        FROM Grupos g
        LEFT JOIN Usuarios r ON r.id_usuario = g.id_responsable
        """;

    public IReadOnlyList<UserGroup> GetAll(bool includeInactive = true, string? search = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];

        if (!includeInactive)
        {
            where.Add("g.activo = 1");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(g.nombre LIKE $q OR g.descripcion LIKE $q OR COALESCE(r.alias_usuario, '') LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        string clause = where.Count > 0 ? $" WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"{SelectBase}{clause} ORDER BY g.activo DESC, g.id_grupo ASC;";

        List<UserGroup> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapGroup(rdr));
        }

        return list;
    }

    public UserGroup? GetById(int idGrupo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectBase} WHERE g.id_grupo = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", idGrupo);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapGroup(rdr) : null;
    }

    public UserGroup? GetByName(string nombre)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectBase} WHERE LOWER(g.nombre) = LOWER($nom) LIMIT 1;";
        cmd.Parameters.AddWithValue("$nom", nombre.Trim());
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? MapGroup(rdr) : null;
    }

    public int Create(UserGroup group)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Grupos(nombre, descripcion, id_responsable, activo, fecha_creacion, fecha_modificacion, row_version)
            VALUES ($nom, $desc, $resp, $act, datetime('now','localtime'), datetime('now','localtime'), 1);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$nom", group.Nombre.Trim());
        cmd.Parameters.AddWithValue("$desc", group.Descripcion.Trim());
        cmd.Parameters.AddWithValue("$resp", string.IsNullOrWhiteSpace(group.IdResponsable) ? DBNull.Value : group.IdResponsable.Trim());
        cmd.Parameters.AddWithValue("$act", group.Activo ? 1 : 0);
        group.IdGrupo = Convert.ToInt32(cmd.ExecuteScalar());
        group.RowVersion = 1;
        return group.IdGrupo;
    }

    public void Update(UserGroup group, int? expectedRowVersion = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string versionClause = expectedRowVersion.HasValue ? " AND row_version = $ver" : string.Empty;
        cmd.CommandText = $"""
            UPDATE Grupos
            SET nombre = $nom,
                descripcion = $desc,
                id_responsable = $resp,
                activo = $act,
                fecha_modificacion = datetime('now','localtime'),
                row_version = row_version + 1
            WHERE id_grupo = $id{versionClause};
            """;
        cmd.Parameters.AddWithValue("$id", group.IdGrupo);
        cmd.Parameters.AddWithValue("$nom", group.Nombre.Trim());
        cmd.Parameters.AddWithValue("$desc", group.Descripcion.Trim());
        cmd.Parameters.AddWithValue("$resp", string.IsNullOrWhiteSpace(group.IdResponsable) ? DBNull.Value : group.IdResponsable.Trim());
        cmd.Parameters.AddWithValue("$act", group.Activo ? 1 : 0);
        if (expectedRowVersion.HasValue)
        {
            cmd.Parameters.AddWithValue("$ver", expectedRowVersion.Value);
        }

        int affected = cmd.ExecuteNonQuery();
        if (expectedRowVersion.HasValue && affected == 0)
        {
            throw new ConcurrencyConflictException(
                $"El grupo '{group.Nombre}' fue modificado simultáneamente por otra sesión. Los datos se actualizarán antes de reintentar.");
        }
    }

    public void SetActive(int idGrupo, bool active)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Grupos
            SET activo = $act,
                fecha_modificacion = datetime('now','localtime'),
                row_version = row_version + 1
            WHERE id_grupo = $id;
            """;
        cmd.Parameters.AddWithValue("$act", active ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", idGrupo);
        cmd.ExecuteNonQuery();
    }

    public int CountAssignedUsers(int idGrupo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Usuarios WHERE id_grupo = $id;";
        cmd.Parameters.AddWithValue("$id", idGrupo);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool HasDependencies(int idGrupo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT EXISTS(SELECT 1 FROM Usuarios WHERE id_grupo = $id)
                OR EXISTS(SELECT 1 FROM MovimientosGrupo WHERE id_grupo_origen = $id OR id_grupo_destino = $id);
            """;
        cmd.Parameters.AddWithValue("$id", idGrupo);
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    public void Delete(int idGrupo)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Grupos WHERE id_grupo = $id;";
        cmd.Parameters.AddWithValue("$id", idGrupo);
        cmd.ExecuteNonQuery();
    }

    public void RecordMovement(GroupMovementEntry movement)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO MovimientosGrupo(
                id_usuario, codigo_usuario, alias_usuario,
                id_grupo_origen, nombre_grupo_origen,
                id_grupo_destino, nombre_grupo_destino,
                ejecutado_por, motivo, fecha_movimiento
            ) VALUES (
                $idUsr, $codUsr, $aliasUsr,
                $idOrig, $nomOrig,
                $idDest, $nomDest,
                $exec, $motivo, datetime('now','localtime')
            );
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$idUsr", movement.IdUsuario.Trim());
        cmd.Parameters.AddWithValue("$codUsr", movement.CodigoUsuario);
        cmd.Parameters.AddWithValue("$aliasUsr", movement.AliasUsuario.Trim());
        cmd.Parameters.AddWithValue("$idOrig", movement.IdGrupoOrigen.HasValue ? movement.IdGrupoOrigen.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$nomOrig", string.IsNullOrWhiteSpace(movement.NombreGrupoOrigen) ? "Sin grupo" : movement.NombreGrupoOrigen.Trim());
        cmd.Parameters.AddWithValue("$idDest", movement.IdGrupoDestino.HasValue ? movement.IdGrupoDestino.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$nomDest", string.IsNullOrWhiteSpace(movement.NombreGrupoDestino) ? "Sin grupo" : movement.NombreGrupoDestino.Trim());
        cmd.Parameters.AddWithValue("$exec", movement.EjecutadoPor.Trim());
        cmd.Parameters.AddWithValue("$motivo", movement.Motivo.Trim());
        movement.IdMovimiento = Convert.ToInt64(cmd.ExecuteScalar());
    }

    public IReadOnlyList<GroupMovementEntry> GetMovements(GroupMovementFilter? filter = null)
    {
        filter ??= new GroupMovementFilter();
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();

        List<string> where = [];
        if (filter.FechaInicio.HasValue)
        {
            where.Add("fecha_movimiento >= $fini");
            cmd.Parameters.AddWithValue("$fini", filter.FechaInicio.Value.Date.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        }

        if (filter.FechaFin.HasValue)
        {
            where.Add("fecha_movimiento <= $ffin");
            cmd.Parameters.AddWithValue("$ffin", filter.FechaFin.Value.Date.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        }

        if (filter.IdGrupo.HasValue)
        {
            where.Add("(id_grupo_origen = $grp OR id_grupo_destino = $grp)");
            cmd.Parameters.AddWithValue("$grp", filter.IdGrupo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.IdUsuario))
        {
            where.Add("id_usuario = $usr");
            cmd.Parameters.AddWithValue("$usr", filter.IdUsuario.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.TextoBusqueda))
        {
            where.Add("(alias_usuario LIKE $q OR nombre_grupo_origen LIKE $q OR nombre_grupo_destino LIKE $q OR ejecutado_por LIKE $q OR motivo LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{filter.TextoBusqueda.Trim()}%");
        }

        StringBuilder sql = new("""
            SELECT id_movimiento, id_usuario, codigo_usuario, alias_usuario,
                   id_grupo_origen, nombre_grupo_origen,
                   id_grupo_destino, nombre_grupo_destino,
                   ejecutado_por, motivo, fecha_movimiento
            FROM MovimientosGrupo
            """);

        if (where.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", where));
        }

        int limit = Math.Clamp(filter.MaxRows, 1, 5000);
        sql.Append($" ORDER BY fecha_movimiento DESC, id_movimiento DESC LIMIT {limit};");
        cmd.CommandText = sql.ToString();

        List<GroupMovementEntry> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new GroupMovementEntry
            {
                IdMovimiento = rdr.GetInt64(0),
                IdUsuario = rdr.GetString(1),
                CodigoUsuario = rdr.GetInt32(2),
                AliasUsuario = rdr.GetString(3),
                IdGrupoOrigen = rdr.IsDBNull(4) ? null : rdr.GetInt32(4),
                NombreGrupoOrigen = rdr.GetString(5),
                IdGrupoDestino = rdr.IsDBNull(6) ? null : rdr.GetInt32(6),
                NombreGrupoDestino = rdr.GetString(7),
                EjecutadoPor = rdr.GetString(8),
                Motivo = rdr.GetString(9),
                FechaMovimiento = DateTime.TryParse(rdr.GetString(10), out DateTime dt) ? dt : DateTime.Now
            });
        }

        return list;
    }

    private static UserGroup MapGroup(SqliteDataReader rdr)
    {
        DateTime fc = !rdr.IsDBNull(8) && DateTime.TryParse(rdr.GetString(8), out DateTime d1) ? d1 : DateTime.Now;
        DateTime fm = !rdr.IsDBNull(9) && DateTime.TryParse(rdr.GetString(9), out DateTime d2) ? d2 : fc;
        return new UserGroup
        {
            IdGrupo = rdr.GetInt32(0),
            Nombre = rdr.GetString(1),
            Descripcion = rdr.GetString(2),
            IdResponsable = rdr.IsDBNull(3) ? null : rdr.GetString(3),
            AliasResponsable = rdr.GetString(4),
            NombreResponsable = rdr.GetString(5),
            Activo = rdr.GetInt32(6) != 0,
            CantidadIntegrantes = rdr.GetInt32(7),
            FechaCreacion = fc,
            FechaModificacion = fm,
            RowVersion = rdr.IsDBNull(10) ? 1 : rdr.GetInt32(10)
        };
    }
}
