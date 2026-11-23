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

    public IReadOnlyList<AuditEntry> GetRecent(int limit = 200)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_log, fecha_utc, usuario, modulo, accion, detalle
            FROM AuditLog
            ORDER BY id_log DESC
            LIMIT $lim;
            """;
        cmd.Parameters.AddWithValue("$lim", Math.Clamp(limit, 1, 2000));

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
                (SELECT COUNT(*) FROM Activos WHERE activo = 1);
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
            TotalNodosActivos = rdr.GetInt32(6)
        };
    }
}
