using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class AuditRepository(SqliteConnectionFactory factory) : IAuditRepository
{
    public void Log(string usuario, string modulo, string accion, string detalle = "")
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO AuditLog(usuario, modulo, accion, detalle)
            VALUES ($u, $m, $a, $d);
            """;
        cmd.Parameters.AddWithValue("$u", string.IsNullOrWhiteSpace(usuario) ? "SISTEMA" : usuario.Trim());
        cmd.Parameters.AddWithValue("$m", modulo.Trim());
        cmd.Parameters.AddWithValue("$a", accion.Trim());
        cmd.Parameters.AddWithValue("$d", detalle.Trim());
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<AuditEntry> GetRecent(int limit = 200) =>
        Search(new AuditSearchFilter { MaxRows = limit });

    public IReadOnlyList<AuditEntry> Search(AuditSearchFilter filter)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];

        if (filter.FechaInicio.HasValue)
        {
            where.Add("fecha_utc >= $fini");
            cmd.Parameters.AddWithValue("$fini", filter.FechaInicio.Value.Date.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture));
        }

        if (filter.FechaFin.HasValue)
        {
            where.Add("fecha_utc <= $ffin");
            cmd.Parameters.AddWithValue("$ffin", filter.FechaFin.Value.Date.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(filter.Modulo))
        {
            where.Add("LOWER(modulo) = LOWER($mod)");
            cmd.Parameters.AddWithValue("$mod", filter.Modulo.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Usuario))
        {
            where.Add("usuario LIKE $usr");
            cmd.Parameters.AddWithValue("$usr", $"%{filter.Usuario.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.TextoBusqueda))
        {
            where.Add("(usuario LIKE $q OR modulo LIKE $q OR accion LIKE $q OR detalle LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{filter.TextoBusqueda.Trim()}%");
        }

        StringBuilder sql = new("SELECT id_log, fecha_utc, usuario, modulo, accion, detalle FROM AuditLog");
        if (where.Count > 0)
        {
            sql.Append(" WHERE ");
            sql.Append(string.Join(" AND ", where));
        }

        sql.Append(" ORDER BY fecha_utc DESC, id_log DESC LIMIT $lim;");
        cmd.CommandText = sql.ToString();
        cmd.Parameters.AddWithValue("$lim", Math.Clamp(filter.MaxRows, 1, 5000));

        List<AuditEntry> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new AuditEntry
            {
                IdLog = rdr.GetInt64(0),
                Fecha = DateTime.TryParse(rdr.GetString(1), out DateTime dt) ? dt : DateTime.Now,
                Usuario = rdr.GetString(2),
                Modulo = rdr.GetString(3),
                Accion = rdr.GetString(4),
                Detalle = rdr.GetString(5)
            });
        }

        return list;
    }

    public DashboardMetrics GetDashboardMetrics()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM OrdenesTrabajo),
                (SELECT COUNT(*) FROM OrdenesTrabajo o
                 WHERE NOT EXISTS(SELECT 1 FROM VistoBueno vb WHERE vb.id_ot = o.id_ot AND vb.tipo_vb = 'VBJ')),
                (SELECT COUNT(*) FROM Proveedores WHERE activo = 1),
                (SELECT COUNT(*) FROM Solicitantes WHERE activo = 1),
                (SELECT COUNT(*) FROM Facturas),
                (SELECT COUNT(*) FROM Usuarios WHERE activo = 1),
                (SELECT COUNT(*) FROM Grupos WHERE activo = 1),
                (SELECT COUNT(*) FROM Activos WHERE activo = 1),
                (SELECT COUNT(*) FROM Mant_OT),
                (SELECT COUNT(*) FROM Mant_MP),
                (SELECT COUNT(*) FROM Mant_TareasMP),
                (SELECT COUNT(*) FROM Mant_Insumos),
                (SELECT COUNT(*) FROM Mant_Personal);
            """;
        using SqliteDataReader rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            return new DashboardMetrics();
        }

        return new DashboardMetrics
        {
            TotalOrdenesTrabajo = rdr.GetInt32(0),
            OrdenesSinVistoBueno = rdr.GetInt32(1),
            TotalProveedoresActivos = rdr.GetInt32(2),
            TotalSolicitantesActivos = rdr.GetInt32(3),
            TotalFacturas = rdr.GetInt32(4),
            TotalUsuariosActivos = rdr.GetInt32(5),
            TotalGruposActivos = rdr.GetInt32(6),
            TotalNodosActivos = rdr.GetInt32(7),
            TotalMantOts = rdr.GetInt32(8),
            TotalPlanesMp = rdr.GetInt32(9),
            TotalTareasMp = rdr.GetInt32(10),
            TotalInsumos = rdr.GetInt32(11),
            TotalPersonalTecnico = rdr.GetInt32(12)
        };
    }
}
