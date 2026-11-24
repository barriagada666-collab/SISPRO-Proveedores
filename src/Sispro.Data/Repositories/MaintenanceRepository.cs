using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

/// <summary>
/// Repositorio de consultas optimizadas sobre las tablas industriales importadas desde BD MANTENCION.mdb
/// y PERSONAL_MANTENCION.mdb, además de agregaciones analíticas para el Panel de Control.
/// </summary>
public sealed class MaintenanceRepository(SqliteConnectionFactory factory) : IMaintenanceRepository
{
    public DashboardAnalytics GetDashboardAnalytics()
    {
        using SqliteConnection conn = factory.OpenConnection();
        AuditRepository auditRepo = new(factory);
        DashboardMetrics metrics = auditRepo.GetDashboardMetrics();

        // 1. OTs de Proveedores por Año (últimos 10 años con actividad)
        List<ChartDataPoint> byYear = [];
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT substr(fecha_ot, 1, 4) AS yr, COUNT(*) AS cnt
                FROM OrdenesTrabajo
                WHERE length(fecha_ot) >= 4
                GROUP BY yr
                ORDER BY yr DESC
                LIMIT 10;
                """;
            using SqliteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                byYear.Add(new ChartDataPoint(rdr.GetString(0), rdr.GetInt32(1)));
            }
        }
        byYear.Reverse();

        // 2. Distribución por Planta (Nivel 2 real de BD MANTENCION)
        List<ChartDataPoint> byPlant = [];
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(a.descripcion, 'Sin Planta') AS planta, COUNT(*) AS cnt
                FROM OrdenesTrabajo o
                LEFT JOIN Activos a ON a.id_activo = o.id_n2
                GROUP BY o.id_n2
                ORDER BY cnt DESC
                LIMIT 6;
                """;
            using SqliteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                byPlant.Add(new ChartDataPoint(rdr.GetString(0), rdr.GetInt32(1)));
            }
        }

        // 3. Top 6 Proveedores por cantidad de OTs
        List<ChartDataPoint> topProviders = [];
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT p.nom_prov, COUNT(*) AS cnt
                FROM OrdenesTrabajo o
                INNER JOIN Proveedores p ON p.id_prov = o.id_prov
                GROUP BY o.id_prov
                ORDER BY cnt DESC
                LIMIT 6;
                """;
            using SqliteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                topProviders.Add(new ChartDataPoint(rdr.GetString(0), rdr.GetInt32(1)));
            }
        }

        // 4. Top Oficios en OTs de Mantención Interna
        List<ChartDataPoint> byTrade = [];
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(NULLIF(trim(oficio_ot), ''), 'SIN OFICIO') AS oficio, COUNT(*) AS cnt
                FROM Mant_OT
                GROUP BY oficio
                ORDER BY cnt DESC
                LIMIT 8;
                """;
            using SqliteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                byTrade.Add(new ChartDataPoint(rdr.GetString(0), rdr.GetInt32(1)));
            }
        }

        return new DashboardAnalytics
        {
            Metrics = metrics,
            WorkOrdersByYear = byYear,
            WorkOrdersByPlant = byPlant,
            TopProviders = topProviders,
            MaintenanceByTrade = byTrade
        };
    }

    public IReadOnlyList<string> GetTradeNames()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT trim(oficio_ot) AS o
            FROM Mant_OT
            WHERE trim(oficio_ot) <> ''
            ORDER BY o;
            """;
        List<string> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(rdr.GetString(0));
        }
        return list;
    }

    public IReadOnlyList<string> GetWorkOrderStatuses()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT trim(estado_ot) AS e
            FROM Mant_OT
            WHERE trim(estado_ot) <> ''
            ORDER BY e;
            """;
        List<string> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(rdr.GetString(0));
        }
        return list;
    }

    public IReadOnlyList<int> GetWorkOrderYears()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT DISTINCT CAST(substr(fechai, 1, 4) AS INTEGER) AS yr
            FROM Mant_OT
            WHERE length(fechai) >= 4 AND CAST(substr(fechai, 1, 4) AS INTEGER) >= 2000
            ORDER BY yr DESC;
            """;
        List<int> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(rdr.GetInt32(0));
        }
        return list;
    }

    public IReadOnlyList<MantWorkOrderRow> SearchInternalWorkOrders(MantWorkOrderFilter filter)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string whereSql = BuildInternalWorkOrderWhere(cmd, filter);
        int limit = Math.Clamp(filter.MaxRows, 10, 5000);

        cmd.CommandText = $"""
            SELECT o.id, o.n_ot, o.fechai, o.fechaf, o.turno, o.oficio_ot,
                   COALESCE(m.desc_motivo, ''), COALESCE(a2.descripcion, ''),
                   COALESCE(a4.descripcion, COALESCE(a3.descripcion, '')),
                   o.estado_ot, o.detencion, o.solicitada_por,
                   o.descripcion_breve, o.descripcion_tecnica, o.prioridad_ot
            FROM Mant_OT o
            LEFT JOIN Mant_Motivos m ON m.id_motivo = o.id_motivo
            LEFT JOIN Activos a2 ON a2.id_activo = o.id_n2
            LEFT JOIN Activos a3 ON a3.id_activo = o.id_n3
            LEFT JOIN Activos a4 ON a4.id_activo = o.id_n4
            {whereSql}
            ORDER BY o.fechai DESC, o.id DESC
            LIMIT {limit};
            """;

        List<MantWorkOrderRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapMantOt(rdr));
        }

        return list;
    }

    public PagedResult<MantWorkOrderRow> SearchInternalWorkOrdersPaged(MantWorkOrderFilter filter, int pageIndex, int pageSize)
    {
        int safeSize = Math.Clamp(pageSize, 25, 500);
        int safePage = Math.Max(0, pageIndex);

        using SqliteConnection conn = factory.OpenConnection();
        int totalCount;
        using (SqliteCommand countCmd = conn.CreateCommand())
        {
            string whereSql = BuildInternalWorkOrderWhere(countCmd, filter);
            string assetJoins = string.IsNullOrWhiteSpace(filter.TextoBusqueda)
                ? string.Empty
                : "\nLEFT JOIN Activos a3 ON a3.id_activo = o.id_n3\nLEFT JOIN Activos a4 ON a4.id_activo = o.id_n4";
            countCmd.CommandText = $"SELECT COUNT(*) FROM Mant_OT o{assetJoins}\n{whereSql};";
            totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        if (totalCount == 0)
        {
            return new PagedResult<MantWorkOrderRow>([], 0, 0, safeSize);
        }

        int maxPage = (totalCount - 1) / safeSize;
        if (safePage > maxPage)
        {
            safePage = maxPage;
        }

        int offset = safePage * safeSize;
        using SqliteCommand cmd = conn.CreateCommand();
        string dataWhere = BuildInternalWorkOrderWhere(cmd, filter);
        cmd.Parameters.AddWithValue("$lim", safeSize);
        cmd.Parameters.AddWithValue("$off", offset);
        cmd.CommandText = $"""
            SELECT o.id, o.n_ot, o.fechai, o.fechaf, o.turno, o.oficio_ot,
                   COALESCE(m.desc_motivo, ''), COALESCE(a2.descripcion, ''),
                   COALESCE(a4.descripcion, COALESCE(a3.descripcion, '')),
                   o.estado_ot, o.detencion, o.solicitada_por,
                   o.descripcion_breve, o.descripcion_tecnica, o.prioridad_ot
            FROM Mant_OT o
            LEFT JOIN Mant_Motivos m ON m.id_motivo = o.id_motivo
            LEFT JOIN Activos a2 ON a2.id_activo = o.id_n2
            LEFT JOIN Activos a3 ON a3.id_activo = o.id_n3
            LEFT JOIN Activos a4 ON a4.id_activo = o.id_n4
            {dataWhere}
            ORDER BY o.fechai DESC, o.id DESC
            LIMIT $lim OFFSET $off;
            """;

        List<MantWorkOrderRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(MapMantOt(rdr));
        }

        return new PagedResult<MantWorkOrderRow>(list, totalCount, safePage, safeSize);
    }

    private static string BuildInternalWorkOrderWhere(SqliteCommand cmd, MantWorkOrderFilter filter)
    {
        List<string> where = [];
        if (!string.IsNullOrWhiteSpace(filter.TextoBusqueda))
        {
            where.Add("""
                (o.n_ot LIKE $q
                 OR o.descripcion_breve LIKE $q
                 OR o.descripcion_tecnica LIKE $q
                 OR o.solicitada_por LIKE $q
                 OR COALESCE(a4.descripcion, '') LIKE $q
                 OR COALESCE(a3.descripcion, '') LIKE $q)
                """);
            cmd.Parameters.AddWithValue("$q", $"%{filter.TextoBusqueda.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filter.Oficio))
        {
            where.Add("trim(o.oficio_ot) = $oficio");
            cmd.Parameters.AddWithValue("$oficio", filter.Oficio.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.Estado))
        {
            where.Add("trim(o.estado_ot) = $estado");
            cmd.Parameters.AddWithValue("$estado", filter.Estado.Trim());
        }

        if (filter.Anio.HasValue && filter.Anio.Value > 0)
        {
            where.Add("substr(o.fechai, 1, 4) = $anio");
            cmd.Parameters.AddWithValue("$anio", filter.Anio.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (filter.FechaInicio.HasValue)
        {
            where.Add("o.fechai <> '' AND substr(o.fechai, 1, 10) >= $fini");
            cmd.Parameters.AddWithValue("$fini", filter.FechaInicio.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        if (filter.FechaFin.HasValue)
        {
            where.Add("o.fechai <> '' AND substr(o.fechai, 1, 10) <= $ffin");
            cmd.Parameters.AddWithValue("$ffin", filter.FechaFin.Value.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        }

        return where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
    }

    public MantWorkOrderDetail? GetInternalWorkOrderDetail(int id)
    {
        using SqliteConnection conn = factory.OpenConnection();
        MantWorkOrderRow? row = null;
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT o.id, o.n_ot, o.fechai, o.fechaf, o.turno, o.oficio_ot,
                       COALESCE(m.desc_motivo, ''), COALESCE(a2.descripcion, ''),
                       COALESCE(a4.descripcion, COALESCE(a3.descripcion, '')),
                       o.estado_ot, o.detencion, o.solicitada_por,
                       o.descripcion_breve, o.descripcion_tecnica, o.prioridad_ot
                FROM Mant_OT o
                LEFT JOIN Mant_Motivos m ON m.id_motivo = o.id_motivo
                LEFT JOIN Activos a2 ON a2.id_activo = o.id_n2
                LEFT JOIN Activos a3 ON a3.id_activo = o.id_n3
                LEFT JOIN Activos a4 ON a4.id_activo = o.id_n4
                WHERE o.id = $id LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            using SqliteDataReader rdr = cmd.ExecuteReader();
            if (rdr.Read())
            {
                row = MapMantOt(rdr);
            }
        }

        if (row is null)
        {
            return null;
        }

        List<string> techs = [];
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT DISTINCT trim(p.nombres || ' ' || p.apellidos) ||
                       CASE WHEN trim(p.oficio) <> '' THEN ' (' || trim(p.oficio) || ')' ELSE '' END
                FROM Mant_PersonalUtilizado pu
                INNER JOIN Mant_Personal p ON p.id_personal = pu.id_personal
                WHERE pu.id_ref = $id AND UPPER(pu.origen) = 'OT'
                ORDER BY 1;
                """;
            cmd.Parameters.AddWithValue("$id", id);
            using SqliteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                techs.Add(rdr.GetString(0));
            }
        }

        List<string> parts = [];
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(i.desc_item, 'Ítem #' || iu.id_item) || ' × ' || iu.cantidad
                FROM Mant_InsumosUtilizados iu
                LEFT JOIN Mant_Insumos i ON i.id_item = iu.id_item
                WHERE iu.id_ref = $id AND UPPER(iu.origen) = 'OT';
                """;
            cmd.Parameters.AddWithValue("$id", id);
            using SqliteDataReader rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                parts.Add(rdr.GetString(0));
            }
        }

        return new MantWorkOrderDetail
        {
            Order = row,
            TecnicosAsignados = techs,
            InsumosConsumidos = parts
        };
    }

    public IReadOnlyList<MantPreventivePlanRow> SearchPreventivePlans(string? search = null, string? estado = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(m.descripcionmp LIKE $q OR COALESCE(a4.descripcion, '') LIKE $q OR COALESCE(a2.descripcion, '') LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(estado))
        {
            where.Add("trim(m.estadomp) = $est");
            cmd.Parameters.AddWithValue("$est", estado.Trim());
        }

        string whereSql = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT m.id, COALESCE(a2.descripcion, ''), COALESCE(a4.descripcion, COALESCE(a3.descripcion, '')),
                   m.descripcionmp, m.estadomp, m.tipo_mant,
                   CASE WHEN m.frecuenciamp > 0 THEN m.frecuenciamp || ' ' || m.ufrecuenciamp ELSE m.diasmp || ' días' END,
                   m.prioridad,
                   (SELECT COUNT(*) FROM Mant_CicloMP c WHERE c.id_mp = m.id),
                   (SELECT COUNT(*) FROM Mant_TareasMP t WHERE t.id_mp = m.id)
            FROM Mant_MP m
            LEFT JOIN Activos a2 ON a2.id_activo = m.id_n2
            LEFT JOIN Activos a3 ON a3.id_activo = m.id_n3
            LEFT JOIN Activos a4 ON a4.id_activo = m.id_n4
            {whereSql}
            ORDER BY m.id DESC;
            """;

        List<MantPreventivePlanRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantPreventivePlanRow
            {
                Id = rdr.GetInt32(0),
                Planta = rdr.GetString(1),
                Equipo = rdr.GetString(2),
                Descripcion = rdr.GetString(3),
                Estado = rdr.GetString(4),
                TipoMantenimiento = rdr.GetString(5),
                Frecuencia = rdr.GetString(6).Trim(),
                Prioridad = rdr.GetInt32(7),
                TotalCiclos = rdr.GetInt32(8),
                TotalTareas = rdr.GetInt32(9)
            });
        }
        return list;
    }

    public IReadOnlyList<MantPreventiveCycleRow> GetPreventiveCycles(int idMp)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_ciclo, fecha_inicio_ciclo, fecha_cierre_ciclo, tiempo, estado_ciclo, observacion_ciclo_mp
            FROM Mant_CicloMP
            WHERE id_mp = $id
            ORDER BY id_ciclo DESC
            LIMIT 200;
            """;
        cmd.Parameters.AddWithValue("$id", idMp);

        List<MantPreventiveCycleRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantPreventiveCycleRow
            {
                IdCiclo = rdr.GetInt32(0),
                FechaInicio = rdr.GetString(1),
                FechaCierre = rdr.GetString(2),
                Tiempo = rdr.GetDouble(3),
                Estado = rdr.GetString(4),
                Observacion = rdr.GetString(5)
            });
        }
        return list;
    }

    public IReadOnlyList<MantPreventiveTaskRow> GetPreventiveTasks(int idMp, int? idCiclo = null, int maxRows = 300)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string cycleFilter = idCiclo.HasValue ? "AND id_ciclo = $ciclo" : string.Empty;
        cmd.CommandText = $"""
            SELECT id_tarea, id_ciclo, descripcion_tarea, estado, duracion, oficio, observacion_tarea_ciclo
            FROM Mant_TareasMP
            WHERE id_mp = $id {cycleFilter}
            ORDER BY id_ciclo DESC, id_tarea ASC
            LIMIT {Math.Clamp(maxRows, 10, 2000)};
            """;
        cmd.Parameters.AddWithValue("$id", idMp);
        if (idCiclo.HasValue)
        {
            cmd.Parameters.AddWithValue("$ciclo", idCiclo.Value);
        }

        List<MantPreventiveTaskRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantPreventiveTaskRow
            {
                IdTarea = rdr.GetInt32(0),
                IdCiclo = rdr.GetInt32(1),
                Descripcion = rdr.GetString(2),
                Estado = rdr.GetString(3),
                Duracion = rdr.GetDouble(4),
                Oficio = rdr.GetString(5),
                Observacion = rdr.GetString(6)
            });
        }
        return list;
    }

    public IReadOnlyList<MantSupplyRow> SearchSupplies(string? search = null, int maxRows = 500)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string whereSql = BuildSuppliesWhere(cmd, search);

        cmd.CommandText = $"""
            SELECT i.id_item, i.cod_sap, i.cod_proveedor, i.desc_item, i.stock_unidad,
                   i.dias_entrega, i.prioridad_item,
                   (SELECT COUNT(*) FROM Mant_InsumosUtilizados iu WHERE iu.id_item = i.id_item)
            FROM Mant_Insumos i
            {whereSql}
            ORDER BY i.desc_item ASC
            LIMIT {Math.Clamp(maxRows, 10, 3000)};
            """;

        return ReadSupplies(cmd);
    }

    public PagedResult<MantSupplyRow> SearchSuppliesPaged(string? search = null, int pageIndex = 0, int pageSize = 200)
    {
        int safeSize = Math.Clamp(pageSize, 25, 500);
        int safePage = Math.Max(0, pageIndex);

        using SqliteConnection conn = factory.OpenConnection();
        int totalCount;
        using (SqliteCommand countCmd = conn.CreateCommand())
        {
            string whereSql = BuildSuppliesWhere(countCmd, search);
            countCmd.CommandText = $"SELECT COUNT(*) FROM Mant_Insumos i\n{whereSql};";
            totalCount = Convert.ToInt32(countCmd.ExecuteScalar() ?? 0, CultureInfo.InvariantCulture);
        }

        if (totalCount == 0)
        {
            return new PagedResult<MantSupplyRow>([], 0, 0, safeSize);
        }

        int maxPage = (totalCount - 1) / safeSize;
        if (safePage > maxPage)
        {
            safePage = maxPage;
        }

        using SqliteCommand cmd = conn.CreateCommand();
        string dataWhere = BuildSuppliesWhere(cmd, search);
        cmd.Parameters.AddWithValue("$lim", safeSize);
        cmd.Parameters.AddWithValue("$off", safePage * safeSize);
        cmd.CommandText = $"""
            SELECT i.id_item, i.cod_sap, i.cod_proveedor, i.desc_item, i.stock_unidad,
                   i.dias_entrega, i.prioridad_item,
                   (SELECT COUNT(*) FROM Mant_InsumosUtilizados iu WHERE iu.id_item = i.id_item)
            FROM Mant_Insumos i
            {dataWhere}
            ORDER BY i.desc_item ASC
            LIMIT $lim OFFSET $off;
            """;

        return new PagedResult<MantSupplyRow>(ReadSupplies(cmd), totalCount, safePage, safeSize);
    }

    private static string BuildSuppliesWhere(SqliteCommand cmd, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return string.Empty;
        }

        cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        return "WHERE i.desc_item LIKE $q OR i.cod_sap LIKE $q OR i.cod_proveedor LIKE $q";
    }

    private static List<MantSupplyRow> ReadSupplies(SqliteCommand cmd)
    {
        List<MantSupplyRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantSupplyRow
            {
                IdItem = rdr.GetInt32(0),
                CodSap = rdr.GetString(1),
                CodProveedor = rdr.GetString(2),
                Descripcion = rdr.GetString(3),
                StockUnidad = rdr.GetDouble(4),
                DiasEntrega = rdr.GetInt32(5),
                Prioridad = rdr.GetInt32(6),
                UsosHistoricos = rdr.GetInt32(7)
            });
        }

        return list;
    }

    public IReadOnlyList<MantPersonnelRow> SearchPersonnel(string? search = null, string? oficio = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];
        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(p.nombres LIKE $q OR p.apellidos LIKE $q OR p.rut_personal LIKE $q OR p.oficio LIKE $q OR p.departamento LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(oficio))
        {
            where.Add("trim(p.oficio) = $oficio");
            cmd.Parameters.AddWithValue("$oficio", oficio.Trim());
        }

        string whereSql = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT p.id_personal, p.cod_personal, p.rut_personal,
                   trim(p.nombres || ' ' || p.apellidos),
                   p.oficio, p.email, COALESCE(NULLIF(p.celular, ''), p.fono),
                   p.departamento, p.centro_trabajo,
                   (SELECT COUNT(*) FROM Mant_PersonalUtilizado pu WHERE pu.id_personal = p.id_personal)
            FROM Mant_Personal p
            {whereSql}
            ORDER BY p.apellidos ASC, p.nombres ASC;
            """;

        List<MantPersonnelRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantPersonnelRow
            {
                IdPersonal = rdr.GetInt32(0),
                Codigo = rdr.GetString(1),
                Rut = rdr.GetString(2),
                NombreCompleto = rdr.GetString(3),
                Oficio = rdr.GetString(4),
                Email = rdr.GetString(5),
                Celular = rdr.GetString(6),
                Departamento = rdr.GetString(7),
                CentroTrabajo = rdr.GetString(8),
                OrdenesAtendidas = rdr.GetInt32(9)
            });
        }
        return list;
    }

    public IReadOnlyList<MantHrPersonnelRow> GetHrPersonnel(string? search = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        string whereSql = string.Empty;
        if (!string.IsNullOrWhiteSpace(search))
        {
            whereSql = "WHERE nombre LIKE $q OR apellido_p LIKE $q OR apellido_m LIKE $q OR n_puesto LIKE $q OR n_departamento LIKE $q";
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        cmd.CommandText = $"""
            SELECT id_empleado, trim(nombre || ' ' || apellido_p || ' ' || apellido_m),
                   fec_alta_empleado, n_puesto, n_departamento, n_centro_costo, n_centro_trabajo
            FROM Mant_PersonalRRHH
            {whereSql}
            ORDER BY apellido_p ASC, nombre ASC;
            """;

        List<MantHrPersonnelRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantHrPersonnelRow
            {
                IdEmpleado = rdr.GetString(0),
                NombreCompleto = rdr.GetString(1),
                FechaAlta = rdr.GetString(2),
                Puesto = rdr.GetString(3),
                Departamento = rdr.GetString(4),
                CentroCosto = rdr.GetString(5),
                CentroTrabajo = rdr.GetString(6)
            });
        }
        return list;
    }

    public IReadOnlyList<MantExternalOrderRow> GetLegacyExternalOrders()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT o.id_ote, o.n_ote, o.fecha_pedido, o.fecha_entrega,
                   COALESCE(e.desc_empresa, 'Sin empresa'), o.descripcion_ote, o.costo, o.garantia
            FROM Mant_OTExterna o
            LEFT JOIN Mant_Empresas e ON e.id_empresa = o.id_empresa
            ORDER BY o.fecha_pedido DESC, o.id_ote DESC;
            """;
        List<MantExternalOrderRow> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantExternalOrderRow
            {
                IdOte = rdr.GetInt32(0),
                NumeroOte = rdr.GetInt32(1),
                FechaPedido = rdr.GetString(2),
                FechaEntrega = rdr.GetString(3),
                Empresa = rdr.GetString(4),
                Descripcion = rdr.GetString(5),
                Costo = Convert.ToDecimal(rdr.GetDouble(6)),
                Garantia = rdr.GetString(7)
            });
        }
        return list;
    }

    public IReadOnlyList<MantCatalogItem> GetFailureMotives()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id_motivo, desc_motivo, CASE WHEN considerado = 1 THEN 'Considerado' ELSE 'Excluido' END FROM Mant_Motivos ORDER BY desc_motivo;";
        return ReadCatalog(cmd);
    }

    public IReadOnlyList<MantCatalogItem> GetTradesCatalog()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id_oficio, desc_oficio, 'Oficio de Mantención' FROM Mant_Oficios ORDER BY desc_oficio;";
        return ReadCatalog(cmd);
    }

    public IReadOnlyList<MantCatalogItem> GetChecklistsSummary()
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_tarea, descripcion, COALESCE(NULLIF(grupo, ''), 'General')
            FROM Mant_TareasChecklist
            ORDER BY grupo, id_tarea;
            """;
        return ReadCatalog(cmd);
    }

    private static List<MantCatalogItem> ReadCatalog(SqliteCommand cmd)
    {
        List<MantCatalogItem> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new MantCatalogItem
            {
                Id = rdr.GetInt32(0),
                Nombre = rdr.GetString(1),
                Detalle = rdr.GetString(2)
            });
        }
        return list;
    }

    private static MantWorkOrderRow MapMantOt(SqliteDataReader rdr) => new()
    {
        Id = rdr.GetInt32(0),
        NumeroOt = rdr.GetString(1),
        FechaInicio = rdr.GetString(2),
        FechaFin = rdr.GetString(3),
        Turno = rdr.GetString(4),
        Oficio = rdr.GetString(5),
        Motivo = rdr.GetString(6),
        Planta = rdr.GetString(7),
        Equipo = rdr.GetString(8),
        Estado = rdr.GetString(9),
        Detencion = rdr.GetString(10),
        SolicitadaPor = rdr.GetString(11),
        DescripcionBreve = rdr.GetString(12),
        DescripcionTecnica = rdr.GetString(13),
        Prioridad = rdr.GetInt32(14)
    };
}
