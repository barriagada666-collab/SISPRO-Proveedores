using System.Globalization;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class InvoiceRepository(SqliteConnectionFactory factory) : IInvoiceRepository
{
    public Invoice? GetById(int idFactura)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT f.id_factura, f.n_fac, f.id_prov, p.nom_prov, f.fecha_fac,
                   f.recibido_por, f.pesos_neto, f.pesos_iva, f.pesos_total,
                   f.detalle_fac, f.usuario_registro, f.fecha_registro
            FROM Facturas f
            INNER JOIN Proveedores p ON p.id_prov = f.id_prov
            WHERE f.id_factura = $id LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", idFactura);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            return null;
        }

        Invoice inv = MapInvoice(rdr);
        rdr.Close();
        LoadItems(conn, inv);
        return inv;
    }

    public Invoice? GetByNumberAndProvider(int numeroDocumento, int idProv)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT f.id_factura, f.n_fac, f.id_prov, p.nom_prov, f.fecha_fac,
                   f.recibido_por, f.pesos_neto, f.pesos_iva, f.pesos_total,
                   f.detalle_fac, f.usuario_registro, f.fecha_registro
            FROM Facturas f
            INNER JOIN Proveedores p ON p.id_prov = f.id_prov
            WHERE f.n_fac = $nfac AND f.id_prov = $prov LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$nfac", numeroDocumento);
        cmd.Parameters.AddWithValue("$prov", idProv);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        if (!rdr.Read())
        {
            return null;
        }

        Invoice inv = MapInvoice(rdr);
        rdr.Close();
        LoadItems(conn, inv);
        return inv;
    }

    public bool IsDetailAlreadyInvoiced(int idDet)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM FacturaDetalles WHERE id_det = $id);";
        cmd.Parameters.AddWithValue("$id", idDet);
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    public int Create(Invoice invoice)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO Facturas(
                    n_fac, id_prov, fecha_fac, recibido_por,
                    pesos_neto, pesos_iva, pesos_total, detalle_fac, usuario_registro
                ) VALUES (
                    $nfac, $prov, $fecha, $rec, $neto, $iva, $tot, $det, $usr
                );
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$nfac", invoice.NumeroDocumento);
            cmd.Parameters.AddWithValue("$prov", invoice.IdProv);
            cmd.Parameters.AddWithValue("$fecha", invoice.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$rec", invoice.RecibidoPor.Trim());
            cmd.Parameters.AddWithValue("$neto", (double)invoice.TotalNeto);
            cmd.Parameters.AddWithValue("$iva", (double)invoice.Iva);
            cmd.Parameters.AddWithValue("$tot", (double)invoice.TotalConIva);
            cmd.Parameters.AddWithValue("$det", invoice.Detalle.Trim());
            cmd.Parameters.AddWithValue("$usr", invoice.UsuarioRegistro.Trim());
            invoice.IdFactura = Convert.ToInt32(cmd.ExecuteScalar());
        }

        HashSet<int> touchedOts = [];
        foreach (InvoiceDetail item in invoice.Items)
        {
            using SqliteCommand icmd = conn.CreateCommand();
            icmd.Transaction = tx;
            icmd.CommandText = """
                INSERT INTO FacturaDetalles(id_factura, id_ot, id_det, valor_trabajo, usuario_sesion)
                VALUES ($fac, $ot, $det, $val, $usr);
                SELECT last_insert_rowid();
                """;
            icmd.Parameters.AddWithValue("$fac", invoice.IdFactura);
            icmd.Parameters.AddWithValue("$ot", item.IdOt);
            icmd.Parameters.AddWithValue("$det", item.IdDet.HasValue ? item.IdDet.Value : DBNull.Value);
            icmd.Parameters.AddWithValue("$val", (double)item.ValorTrabajo);
            icmd.Parameters.AddWithValue("$usr", item.UsuarioSesion.Trim());
            item.IdFacDet = Convert.ToInt32(icmd.ExecuteScalar());
            item.IdFactura = invoice.IdFactura;

            if (item.IdDet.HasValue)
            {
                using SqliteCommand updDet = conn.CreateCommand();
                updDet.Transaction = tx;
                updDet.CommandText = "UPDATE DetalleOT SET estado_det = 1 WHERE id_det = $id;";
                updDet.Parameters.AddWithValue("$id", item.IdDet.Value);
                updDet.ExecuteNonQuery();
            }

            touchedOts.Add(item.IdOt);
        }

        foreach (int idOt in touchedOts)
        {
            RefreshWorkOrderInvoiceStatus(conn, tx, idOt, invoice.NumeroDocumento);
        }

        tx.Commit();
        return invoice.IdFactura;
    }

    public void Delete(int idFactura)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        List<(int IdOt, int? IdDet)> links = [];
        using (SqliteCommand q = conn.CreateCommand())
        {
            q.Transaction = tx;
            q.CommandText = "SELECT id_ot, id_det FROM FacturaDetalles WHERE id_factura = $id;";
            q.Parameters.AddWithValue("$id", idFactura);
            using SqliteDataReader rdr = q.ExecuteReader();
            while (rdr.Read())
            {
                links.Add((rdr.GetInt32(0), rdr.IsDBNull(1) ? null : rdr.GetInt32(1)));
            }
        }

        using (SqliteCommand del = conn.CreateCommand())
        {
            del.Transaction = tx;
            del.CommandText = "DELETE FROM Facturas WHERE id_factura = $id;";
            del.Parameters.AddWithValue("$id", idFactura);
            del.ExecuteNonQuery();
        }

        foreach ((_, int? idDet) in links)
        {
            if (idDet.HasValue)
            {
                using SqliteCommand resetDet = conn.CreateCommand();
                resetDet.Transaction = tx;
                resetDet.CommandText = "UPDATE DetalleOT SET estado_det = 0 WHERE id_det = $id;";
                resetDet.Parameters.AddWithValue("$id", idDet.Value);
                resetDet.ExecuteNonQuery();
            }
        }

        foreach (int idOt in links.Select(x => x.IdOt).Distinct())
        {
            RefreshWorkOrderInvoiceStatus(conn, tx, idOt, 0);
        }

        tx.Commit();
    }

    public IReadOnlyList<InvoiceSearchRow> Search(InvoiceSearchFilter filter)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];

        if (filter.IdProv.HasValue && filter.IdProv.Value > 0)
        {
            where.Add("f.id_prov = $prov");
            cmd.Parameters.AddWithValue("$prov", filter.IdProv.Value);
        }

        if (filter.NumeroDocumento.HasValue && filter.NumeroDocumento.Value > 0)
        {
            where.Add("f.n_fac = $nfac");
            cmd.Parameters.AddWithValue("$nfac", filter.NumeroDocumento.Value);
        }

        if (filter.NumeroOt.HasValue && filter.NumeroOt.Value > 0)
        {
            where.Add("""
                EXISTS(
                    SELECT 1 FROM FacturaDetalles fd
                    INNER JOIN OrdenesTrabajo o ON o.id_ot = fd.id_ot
                    WHERE fd.id_factura = f.id_factura AND o.n_ot = $not
                )
                """);
            cmd.Parameters.AddWithValue("$not", filter.NumeroOt.Value);
        }

        if (filter.FechaInicio.HasValue)
        {
            where.Add("f.fecha_fac >= $fini");
            cmd.Parameters.AddWithValue("$fini", filter.FechaInicio.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (filter.FechaFin.HasValue)
        {
            where.Add("f.fecha_fac <= $ffin");
            cmd.Parameters.AddWithValue("$ffin", filter.FechaFin.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        string clause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT f.id_factura, f.n_fac, p.nom_prov, f.fecha_fac, f.recibido_por,
                   f.pesos_neto, f.pesos_iva, f.pesos_total,
                   (SELECT COUNT(*) FROM FacturaDetalles fd WHERE fd.id_factura = f.id_factura),
                   f.detalle_fac
            FROM Facturas f
            INNER JOIN Proveedores p ON p.id_prov = f.id_prov
            {clause}
            ORDER BY f.fecha_fac DESC, f.n_fac DESC;
            """;

        List<InvoiceSearchRow> rows = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            rows.Add(new InvoiceSearchRow
            {
                IdFactura = rdr.GetInt32(0),
                NumeroDocumento = rdr.GetInt32(1),
                Proveedor = rdr.GetString(2),
                Fecha = DateTime.ParseExact(rdr.GetString(3), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                RecibidoPor = rdr.GetString(4),
                TotalNeto = (decimal)rdr.GetDouble(5),
                Iva = (decimal)rdr.GetDouble(6),
                TotalConIva = (decimal)rdr.GetDouble(7),
                CantidadItems = rdr.GetInt32(8),
                Detalle = rdr.GetString(9)
            });
        }

        return rows;
    }

    private static void RefreshWorkOrderInvoiceStatus(SqliteConnection conn, SqliteTransaction tx, int idOt, int guiaNumero)
    {
        using SqliteCommand statCmd = conn.CreateCommand();
        statCmd.Transaction = tx;
        statCmd.CommandText = """
            SELECT COUNT(*), SUM(CASE WHEN estado_det = 1 THEN 1 ELSE 0 END)
            FROM DetalleOT WHERE id_ot = $ot;
            """;
        statCmd.Parameters.AddWithValue("$ot", idOt);
        using SqliteDataReader rdr = statCmd.ExecuteReader();
        int total = 0;
        int invoiced = 0;
        if (rdr.Read())
        {
            total = rdr.GetInt32(0);
            invoiced = rdr.IsDBNull(1) ? 0 : rdr.GetInt32(1);
        }

        rdr.Close();

        int newStatus = invoiced == 0 ? 0 : (invoiced >= total ? 2 : 1);
        using SqliteCommand upd = conn.CreateCommand();
        upd.Transaction = tx;
        upd.CommandText = "UPDATE OrdenesTrabajo SET estado = $est, guia_des_ot = $gd WHERE id_ot = $ot;";
        upd.Parameters.AddWithValue("$est", newStatus);
        upd.Parameters.AddWithValue("$gd", invoiced == 0 ? 0 : guiaNumero);
        upd.Parameters.AddWithValue("$ot", idOt);
        upd.ExecuteNonQuery();
    }

    private static void LoadItems(SqliteConnection conn, Invoice inv)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT fd.id_fac_det, fd.id_factura, fd.id_ot, o.n_ot, fd.id_det,
                   COALESCE(d.detalle_ot, ''), o.fecha_ot, fd.valor_trabajo,
                   fd.fecha_asociacion, fd.usuario_sesion
            FROM FacturaDetalles fd
            INNER JOIN OrdenesTrabajo o ON o.id_ot = fd.id_ot
            LEFT JOIN DetalleOT d ON d.id_det = fd.id_det
            WHERE fd.id_factura = $id
            ORDER BY fd.id_fac_det ASC;
            """;
        cmd.Parameters.AddWithValue("$id", inv.IdFactura);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            inv.Items.Add(new InvoiceDetail
            {
                IdFacDet = rdr.GetInt32(0),
                IdFactura = rdr.GetInt32(1),
                IdOt = rdr.GetInt32(2),
                NumeroOt = rdr.GetInt32(3),
                IdDet = rdr.IsDBNull(4) ? null : rdr.GetInt32(4),
                DescripcionTrabajo = rdr.GetString(5),
                FechaOt = DateTime.ParseExact(rdr.GetString(6), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                ValorTrabajo = (decimal)rdr.GetDouble(7),
                FechaAsociacion = DateTime.TryParse(rdr.GetString(8), out DateTime fa) ? fa : DateTime.Now,
                UsuarioSesion = rdr.GetString(9)
            });
        }
    }

    private static Invoice MapInvoice(SqliteDataReader rdr) => new()
    {
        IdFactura = rdr.GetInt32(0),
        NumeroDocumento = rdr.GetInt32(1),
        IdProv = rdr.GetInt32(2),
        NombreProveedor = rdr.GetString(3),
        Fecha = DateTime.ParseExact(rdr.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        RecibidoPor = rdr.GetString(5),
        TotalNeto = (decimal)rdr.GetDouble(6),
        Iva = (decimal)rdr.GetDouble(7),
        TotalConIva = (decimal)rdr.GetDouble(8),
        Detalle = rdr.GetString(9),
        UsuarioRegistro = rdr.GetString(10),
        FechaRegistro = DateTime.TryParse(rdr.GetString(11), out DateTime fr) ? fr : DateTime.Now
    };
}
