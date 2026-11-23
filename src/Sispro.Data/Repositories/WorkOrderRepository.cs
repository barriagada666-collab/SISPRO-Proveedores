using System.Globalization;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class WorkOrderRepository(SqliteConnectionFactory factory) : IWorkOrderRepository
{
    private const string SelectOrderSql = """
        SELECT o.id_ot, o.n_ot, o.id_prov, p.nom_prov, p.dir_prov, p.mail_prov,
               o.id_sol, COALESCE(s.alias, 'Sin solicitante'), COALESCE(s.fono_sol, ''), COALESCE(s.mail_sol, ''),
               o.fecha_ot, o.estado, o.guia_des_ot,
               o.id_n1, o.id_n2, o.id_n3, o.id_n4, o.id_n5,
               o.id_n6, o.id_n7, o.id_n8, o.id_n9, o.id_n10,
               o.id_nivel, o.ruta_activo
        FROM OrdenesTrabajo o
        INNER JOIN Proveedores p ON p.id_prov = o.id_prov
        LEFT JOIN Solicitantes s ON s.id_sol = o.id_sol
        """;

    public WorkOrder? GetByNumber(int numeroOt)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectOrderSql} WHERE o.n_ot = $not LIMIT 1;";
        cmd.Parameters.AddWithValue("$not", numeroOt);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            return null;
        }

        WorkOrder order = MapOrder(rdr);
        rdr.Close();
        LoadDetailsAndApprovals(conn, order);
        return order;
    }

    public WorkOrder? GetById(int idOt)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = $"{SelectOrderSql} WHERE o.id_ot = $id LIMIT 1;";
        cmd.Parameters.AddWithValue("$id", idOt);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            return null;
        }

        WorkOrder order = MapOrder(rdr);
        rdr.Close();
        LoadDetailsAndApprovals(conn, order);
        return order;
    }

    public int GetNextWorkOrderNumber()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(n_ot), 0) + 1 FROM OrdenesTrabajo;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int Create(WorkOrder workOrder)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO OrdenesTrabajo(
                    n_ot, id_prov, id_sol, fecha_ot, estado, guia_des_ot,
                    id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
                    id_nivel, ruta_activo
                ) VALUES (
                    $not, $prov, $sol, $fecha, $est, $gd,
                    $n1, $n2, $n3, $n4, $n5, $n6, $n7, $n8, $n9, $n10,
                    $niv, $ruta
                );
                SELECT last_insert_rowid();
                """;
            BindOrder(cmd, workOrder);
            workOrder.IdOt = Convert.ToInt32(cmd.ExecuteScalar());
        }

        InsertDetails(conn, tx, workOrder.IdOt, workOrder.Detalles);
        tx.Commit();
        return workOrder.IdOt;
    }

    public void Update(WorkOrder workOrder)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                UPDATE OrdenesTrabajo
                SET n_ot = $not, id_prov = $prov, id_sol = $sol, fecha_ot = $fecha,
                    estado = $est, guia_des_ot = $gd,
                    id_n1 = $n1, id_n2 = $n2, id_n3 = $n3, id_n4 = $n4, id_n5 = $n5,
                    id_n6 = $n6, id_n7 = $n7, id_n8 = $n8, id_n9 = $n9, id_n10 = $n10,
                    id_nivel = $niv, ruta_activo = $ruta
                WHERE id_ot = $id;
                """;
            cmd.Parameters.AddWithValue("$id", workOrder.IdOt);
            BindOrder(cmd, workOrder);
            cmd.ExecuteNonQuery();
        }

        // Preserve invoiced detail IDs if they exist; replace only uninvoiced details cleanly
        HashSet<int> invoicedIds = [];
        using (SqliteCommand invCmd = conn.CreateCommand())
        {
            invCmd.Transaction = tx;
            invCmd.CommandText = "SELECT id_det FROM FacturaDetalles WHERE id_ot = $id AND id_det IS NOT NULL;";
            invCmd.Parameters.AddWithValue("$id", workOrder.IdOt);
            using SqliteDataReader rdr = invCmd.ExecuteReader();
            while (rdr.Read())
            {
                invoicedIds.Add(rdr.GetInt32(0));
            }
        }

        using (SqliteCommand delCmd = conn.CreateCommand())
        {
            delCmd.Transaction = tx;
            delCmd.CommandText = """
                DELETE FROM DetalleOT
                WHERE id_ot = $id
                  AND id_det NOT IN (SELECT id_det FROM FacturaDetalles WHERE id_ot = $id AND id_det IS NOT NULL);
                """;
            delCmd.Parameters.AddWithValue("$id", workOrder.IdOt);
            delCmd.ExecuteNonQuery();
        }

        List<WorkOrderDetail> toInsert = [];
        foreach (WorkOrderDetail detail in workOrder.Detalles)
        {
            if (detail.IdDet > 0 && invoicedIds.Contains(detail.IdDet))
            {
                using SqliteCommand updDet = conn.CreateCommand();
                updDet.Transaction = tx;
                updDet.CommandText = "UPDATE DetalleOT SET cantidad_det = $cant, detalle_ot = $det WHERE id_det = $id;";
                updDet.Parameters.AddWithValue("$cant", Math.Max(1, detail.Cantidad));
                updDet.Parameters.AddWithValue("$det", detail.Descripcion.Trim());
                updDet.Parameters.AddWithValue("$id", detail.IdDet);
                updDet.ExecuteNonQuery();
            }
            else
            {
                toInsert.Add(detail);
            }
        }

        InsertDetails(conn, tx, workOrder.IdOt, toInsert);
        tx.Commit();
    }

    public bool HasInvoices(int idOt)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM FacturaDetalles WHERE id_ot = $id);";
        cmd.Parameters.AddWithValue("$id", idOt);
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    public void Delete(int idOt)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM OrdenesTrabajo WHERE id_ot = $id;";
        cmd.Parameters.AddWithValue("$id", idOt);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<WorkOrderSearchRow> Search(WorkOrderSearchFilter filter)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];

        if (filter.FechaInicio.HasValue)
        {
            where.Add("o.fecha_ot >= $fini");
            cmd.Parameters.AddWithValue("$fini", filter.FechaInicio.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (filter.FechaFin.HasValue)
        {
            where.Add("o.fecha_ot <= $ffin");
            cmd.Parameters.AddWithValue("$ffin", filter.FechaFin.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (filter.IdProv.HasValue && filter.IdProv.Value > 0)
        {
            where.Add("o.id_prov = $prov");
            cmd.Parameters.AddWithValue("$prov", filter.IdProv.Value);
        }

        if (filter.NumeroOt.HasValue && filter.NumeroOt.Value > 0)
        {
            where.Add("o.n_ot = $not");
            cmd.Parameters.AddWithValue("$not", filter.NumeroOt.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.TextoBusqueda))
        {
            where.Add("(d.detalle_ot LIKE $txt OR p.nom_prov LIKE $txt OR o.ruta_activo LIKE $txt OR CAST(o.n_ot AS TEXT) LIKE $txt)");
            cmd.Parameters.AddWithValue("$txt", $"%{filter.TextoBusqueda.Trim()}%");
        }

        if (filter.AssetLevel is >= 1 and <= 10 && filter.AssetNodeId is > 0)
        {
            string col = $"o.id_n{filter.AssetLevel.Value}";
            where.Add($"{col} = $nodeId");
            cmd.Parameters.AddWithValue("$nodeId", filter.AssetNodeId.Value);
        }

        if (filter.SoloPendientesVb)
        {
            where.Add("""
                (NOT EXISTS(SELECT 1 FROM VistoBueno vb WHERE vb.id_ot = o.id_ot AND vb.tipo_vb = 'VBS')
                 OR NOT EXISTS(SELECT 1 FROM VistoBueno vb WHERE vb.id_ot = o.id_ot AND vb.tipo_vb = 'VBJ'))
                """);
        }

        string whereSql = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        string limitSql = filter.MaxRows.HasValue && filter.MaxRows.Value > 0
            ? $"LIMIT {Math.Clamp(filter.MaxRows.Value, 1, 50_000)}"
            : string.Empty;

        cmd.CommandText = $"""
            SELECT o.id_ot, o.n_ot, p.nom_prov, COALESCE(s.alias, 'Sin solicitante'),
                   o.fecha_ot, d.cantidad_det, d.detalle_ot, o.ruta_activo,
                   EXISTS(SELECT 1 FROM VistoBueno vb WHERE vb.id_ot = o.id_ot AND vb.tipo_vb = 'VBS'),
                   EXISTS(SELECT 1 FROM VistoBueno vb WHERE vb.id_ot = o.id_ot AND vb.tipo_vb = 'VBJ'),
                   d.estado_det, d.id_det
            FROM OrdenesTrabajo o
            INNER JOIN Proveedores p ON p.id_prov = o.id_prov
            LEFT JOIN Solicitantes s ON s.id_sol = o.id_sol
            INNER JOIN DetalleOT d ON d.id_ot = o.id_ot
            {whereSql}
            ORDER BY o.fecha_ot DESC, o.n_ot DESC, d.id_det ASC
            {limitSql};
            """;

        List<WorkOrderSearchRow> rows = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            rows.Add(new WorkOrderSearchRow
            {
                IdOt = rdr.GetInt32(0),
                NumeroOt = rdr.GetInt32(1),
                Proveedor = rdr.GetString(2),
                Solicitante = rdr.GetString(3),
                Fecha = DateTime.ParseExact(rdr.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Cantidad = rdr.GetInt32(5),
                Detalle = rdr.GetString(6),
                RutaActivo = rdr.GetString(7),
                TieneVbs = rdr.GetInt32(8) != 0,
                TieneVbj = rdr.GetInt32(9) != 0,
                AsociadoAFactura = rdr.GetInt32(10) != 0,
                IdDet = rdr.GetInt32(11)
            });
        }

        return rows;
    }

    public void SetApproval(int idOt, string idUsuario, ApprovalType type, bool approved)
    {
        string code = type == ApprovalType.Supervisor ? "VBS" : "VBJ";
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        if (approved)
        {
            cmd.CommandText = """
                INSERT INTO VistoBueno(id_ot, id_usuario, tipo_vb, fecha_vb)
                VALUES ($ot, $us, $tipo, datetime('now','localtime'))
                ON CONFLICT(id_ot, tipo_vb) DO UPDATE SET
                    id_usuario = excluded.id_usuario,
                    fecha_vb = excluded.fecha_vb;
                """;
            cmd.Parameters.AddWithValue("$ot", idOt);
            cmd.Parameters.AddWithValue("$us", idUsuario);
            cmd.Parameters.AddWithValue("$tipo", code);
        }
        else
        {
            cmd.CommandText = "DELETE FROM VistoBueno WHERE id_ot = $ot AND tipo_vb = $tipo;";
            cmd.Parameters.AddWithValue("$ot", idOt);
            cmd.Parameters.AddWithValue("$tipo", code);
        }

        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<WorkOrderApproval> GetApprovals(int idOt)
    {
        using SqliteConnection conn = factory.OpenConnection();
        return ReadApprovals(conn, idOt);
    }

    private static void LoadDetailsAndApprovals(SqliteConnection conn, WorkOrder order)
    {
        using (SqliteCommand dcmd = conn.CreateCommand())
        {
            dcmd.CommandText = """
                SELECT id_det, id_ot, cantidad_det, detalle_ot, estado_det
                FROM DetalleOT
                WHERE id_ot = $id
                ORDER BY id_det ASC;
                """;
            dcmd.Parameters.AddWithValue("$id", order.IdOt);
            using SqliteDataReader rdr = dcmd.ExecuteReader();
            while (rdr.Read())
            {
                order.Detalles.Add(new WorkOrderDetail
                {
                    IdDet = rdr.GetInt32(0),
                    IdOt = rdr.GetInt32(1),
                    Cantidad = rdr.GetInt32(2),
                    Descripcion = rdr.GetString(3),
                    AsociadoAFactura = rdr.GetInt32(4) != 0
                });
            }
        }

        order.VistosBuenos = [.. ReadApprovals(conn, order.IdOt)];
    }

    private static List<WorkOrderApproval> ReadApprovals(SqliteConnection conn, int idOt)
    {
        List<WorkOrderApproval> list = [];
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT vb.id_vb, vb.id_ot, vb.id_usuario, COALESCE(u.alias_usuario, vb.id_usuario),
                   vb.tipo_vb, vb.fecha_vb
            FROM VistoBueno vb
            LEFT JOIN Usuarios u ON u.id_usuario = vb.id_usuario
            WHERE vb.id_ot = $id;
            """;
        cmd.Parameters.AddWithValue("$id", idOt);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new WorkOrderApproval
            {
                IdVb = rdr.GetInt32(0),
                IdOt = rdr.GetInt32(1),
                IdUsuario = rdr.GetString(2),
                AliasUsuario = rdr.GetString(3),
                Tipo = rdr.GetString(4) == "VBJ" ? ApprovalType.Jefatura : ApprovalType.Supervisor,
                Fecha = DateTime.TryParse(rdr.GetString(5), out DateTime dt) ? dt : DateTime.Now
            });
        }

        return list;
    }

    private static void InsertDetails(SqliteConnection conn, SqliteTransaction tx, int idOt, IEnumerable<WorkOrderDetail> details)
    {
        foreach (WorkOrderDetail d in details)
        {
            using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO DetalleOT(id_ot, cantidad_det, detalle_ot, estado_det)
                VALUES ($ot, $cant, $det, $est);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$ot", idOt);
            cmd.Parameters.AddWithValue("$cant", Math.Max(1, d.Cantidad));
            cmd.Parameters.AddWithValue("$det", d.Descripcion.Trim());
            cmd.Parameters.AddWithValue("$est", d.AsociadoAFactura ? 1 : 0);
            d.IdDet = Convert.ToInt32(cmd.ExecuteScalar());
            d.IdOt = idOt;
        }
    }

    private static void BindOrder(SqliteCommand cmd, WorkOrder o)
    {
        cmd.Parameters.AddWithValue("$not", o.NumeroOt);
        cmd.Parameters.AddWithValue("$prov", o.IdProv);
        cmd.Parameters.AddWithValue("$sol", o.IdSol.HasValue && o.IdSol.Value > 0 ? o.IdSol.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("$fecha", o.FechaOt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$est", (int)o.Estado);
        cmd.Parameters.AddWithValue("$gd", o.GuiaDespacho);
        cmd.Parameters.AddWithValue("$n1", o.IdN1);
        cmd.Parameters.AddWithValue("$n2", o.IdN2);
        cmd.Parameters.AddWithValue("$n3", o.IdN3);
        cmd.Parameters.AddWithValue("$n4", o.IdN4);
        cmd.Parameters.AddWithValue("$n5", o.IdN5);
        cmd.Parameters.AddWithValue("$n6", o.IdN6);
        cmd.Parameters.AddWithValue("$n7", o.IdN7);
        cmd.Parameters.AddWithValue("$n8", o.IdN8);
        cmd.Parameters.AddWithValue("$n9", o.IdN9);
        cmd.Parameters.AddWithValue("$n10", o.IdN10);
        cmd.Parameters.AddWithValue("$niv", Math.Clamp(o.IdNivel, 1, 10));
        cmd.Parameters.AddWithValue("$ruta", o.RutaActivo.Trim());
    }

    private static WorkOrder MapOrder(SqliteDataReader rdr) => new()
    {
        IdOt = rdr.GetInt32(0),
        NumeroOt = rdr.GetInt32(1),
        IdProv = rdr.GetInt32(2),
        NombreProveedor = rdr.GetString(3),
        DireccionProveedor = rdr.GetString(4),
        CorreoProveedor = rdr.GetString(5),
        IdSol = rdr.IsDBNull(6) ? null : rdr.GetInt32(6),
        AliasSolicitante = rdr.GetString(7),
        TelefonoSolicitante = rdr.GetString(8),
        CorreoSolicitante = rdr.GetString(9),
        FechaOt = DateTime.ParseExact(rdr.GetString(10), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        Estado = (WorkOrderStatus)rdr.GetInt32(11),
        GuiaDespacho = rdr.GetInt32(12),
        IdN1 = rdr.GetInt32(13),
        IdN2 = rdr.GetInt32(14),
        IdN3 = rdr.GetInt32(15),
        IdN4 = rdr.GetInt32(16),
        IdN5 = rdr.GetInt32(17),
        IdN6 = rdr.GetInt32(18),
        IdN7 = rdr.GetInt32(19),
        IdN8 = rdr.GetInt32(20),
        IdN9 = rdr.GetInt32(21),
        IdN10 = rdr.GetInt32(22),
        IdNivel = rdr.GetInt32(23),
        RutaActivo = rdr.GetString(24)
    };
}
