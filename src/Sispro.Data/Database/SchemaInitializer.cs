using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;

namespace Sispro.Data.Database;

public static class SchemaInitializer
{
    private const string SchemaSql = """
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS Niveles (
            id_nivel INTEGER PRIMARY KEY,
            tipo_nivel TEXT NOT NULL UNIQUE
        );

        CREATE TABLE IF NOT EXISTS Grupos (
            id_grupo INTEGER PRIMARY KEY AUTOINCREMENT,
            nombre TEXT NOT NULL UNIQUE COLLATE NOCASE,
            descripcion TEXT NOT NULL DEFAULT '',
            id_responsable TEXT REFERENCES Usuarios(id_usuario) ON DELETE SET NULL,
            activo INTEGER NOT NULL DEFAULT 1,
            fecha_creacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            fecha_modificacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            row_version INTEGER NOT NULL DEFAULT 1
        );

        CREATE INDEX IF NOT EXISTS IX_Grupos_Activo_Nombre ON Grupos(activo, nombre);

        CREATE TABLE IF NOT EXISTS Usuarios (
            id_usuario TEXT PRIMARY KEY,
            codigo_id INTEGER,
            alias_usuario TEXT NOT NULL UNIQUE COLLATE NOCASE,
            pass_hash TEXT NOT NULL,
            nombre_usuario TEXT NOT NULL,
            ape_pat_usuario TEXT NOT NULL,
            ape_mat_usuario TEXT NOT NULL DEFAULT '',
            id_nivel INTEGER NOT NULL DEFAULT 1 REFERENCES Niveles(id_nivel),
            id_grupo INTEGER REFERENCES Grupos(id_grupo) ON DELETE SET NULL,
            mail_usuario TEXT NOT NULL DEFAULT '',
            fono_usuario TEXT NOT NULL DEFAULT '',
            es_solicitante INTEGER NOT NULL DEFAULT 0,
            activo INTEGER NOT NULL DEFAULT 1,
            debe_cambiar_clave INTEGER NOT NULL DEFAULT 0,
            fecha_creacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            fecha_modificacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            ultimo_acceso TEXT,
            row_version INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS MovimientosGrupo (
            id_movimiento INTEGER PRIMARY KEY AUTOINCREMENT,
            id_usuario TEXT NOT NULL REFERENCES Usuarios(id_usuario),
            codigo_usuario INTEGER NOT NULL DEFAULT 0,
            alias_usuario TEXT NOT NULL DEFAULT '',
            id_grupo_origen INTEGER REFERENCES Grupos(id_grupo) ON DELETE SET NULL,
            nombre_grupo_origen TEXT NOT NULL DEFAULT 'Sin grupo',
            id_grupo_destino INTEGER REFERENCES Grupos(id_grupo) ON DELETE SET NULL,
            nombre_grupo_destino TEXT NOT NULL DEFAULT 'Sin grupo',
            ejecutado_por TEXT NOT NULL,
            motivo TEXT NOT NULL DEFAULT '',
            fecha_movimiento TEXT NOT NULL DEFAULT (datetime('now','localtime'))
        );

        CREATE INDEX IF NOT EXISTS IX_MovimientosGrupo_Fecha ON MovimientosGrupo(fecha_movimiento DESC, id_movimiento DESC);
        CREATE INDEX IF NOT EXISTS IX_MovimientosGrupo_Usuario ON MovimientosGrupo(id_usuario, fecha_movimiento DESC);

        CREATE TABLE IF NOT EXISTS Permisos (
            id_usuario TEXT PRIMARY KEY REFERENCES Usuarios(id_usuario) ON DELETE CASCADE,
            m1_ot INTEGER NOT NULL DEFAULT 0,
            tab_0_1_ingreso_ot INTEGER NOT NULL DEFAULT 0,
            tab_0_vbj INTEGER NOT NULL DEFAULT 0,
            tab_0_vbs INTEGER NOT NULL DEFAULT 0,
            tab_0_2_busqueda_ot INTEGER NOT NULL DEFAULT 0,
            tab_0_3_impresion_ot INTEGER NOT NULL DEFAULT 0,
            m2_facturas INTEGER NOT NULL DEFAULT 0,
            tab_1_0_ingreso_fac INTEGER NOT NULL DEFAULT 0,
            tab_1_1_busqueda_fac INTEGER NOT NULL DEFAULT 0,
            m3_mantenedor INTEGER NOT NULL DEFAULT 0,
            m4_usuarios INTEGER NOT NULL DEFAULT 0,
            tab_2_0_ingreso_us INTEGER NOT NULL DEFAULT 0,
            tab_2_1_listado_us INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Proveedores (
            id_prov INTEGER PRIMARY KEY AUTOINCREMENT,
            nom_prov TEXT NOT NULL UNIQUE COLLATE NOCASE,
            dir_prov TEXT NOT NULL DEFAULT '',
            fono_prov TEXT NOT NULL DEFAULT '',
            fax_prov TEXT NOT NULL DEFAULT '',
            mail_prov TEXT NOT NULL DEFAULT '',
            contacto_prov TEXT NOT NULL DEFAULT '',
            mail_contacto_prov TEXT NOT NULL DEFAULT '',
            observaciones TEXT NOT NULL DEFAULT '',
            activo INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS Solicitantes (
            id_sol INTEGER PRIMARY KEY AUTOINCREMENT,
            alias TEXT NOT NULL UNIQUE COLLATE NOCASE,
            nombre_sol TEXT NOT NULL,
            ape_pat_sol TEXT NOT NULL,
            ape_mat_sol TEXT NOT NULL DEFAULT '',
            fono_sol TEXT NOT NULL DEFAULT '',
            mail_sol TEXT NOT NULL DEFAULT '',
            activo INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS Activos (
            id_activo INTEGER PRIMARY KEY,
            parent_id INTEGER REFERENCES Activos(id_activo),
            nivel INTEGER NOT NULL CHECK (nivel BETWEEN 1 AND 10),
            descripcion TEXT NOT NULL,
            activo INTEGER NOT NULL DEFAULT 1,
            cod_sap TEXT NOT NULL DEFAULT '',
            fabricante TEXT NOT NULL DEFAULT '',
            serie TEXT NOT NULL DEFAULT '',
            modelo TEXT NOT NULL DEFAULT '',
            fecha_compra TEXT NOT NULL DEFAULT '',
            valor_compra REAL NOT NULL DEFAULT 0,
            valor_actual REAL NOT NULL DEFAULT 0,
            fecha_instalacion TEXT NOT NULL DEFAULT '',
            tipo_equipo TEXT NOT NULL DEFAULT '',
            estado_operativo TEXT NOT NULL DEFAULT '',
            vida_util TEXT NOT NULL DEFAULT '',
            observacion TEXT NOT NULL DEFAULT '',
            prioridad INTEGER NOT NULL DEFAULT 0,
            material TEXT NOT NULL DEFAULT '',
            categoria TEXT NOT NULL DEFAULT '',
            ruta_plano TEXT NOT NULL DEFAULT '',
            nombre_plano TEXT NOT NULL DEFAULT '',
            ruta_foto TEXT NOT NULL DEFAULT '',
            nombre_foto TEXT NOT NULL DEFAULT '',
            info_tecnica_json TEXT NOT NULL DEFAULT ''
        );

        CREATE INDEX IF NOT EXISTS IX_Activos_Parent ON Activos(parent_id, activo, descripcion);
        CREATE INDEX IF NOT EXISTS IX_Activos_Nivel ON Activos(nivel, descripcion);
        CREATE INDEX IF NOT EXISTS IX_Activos_CodSap ON Activos(cod_sap);

        CREATE TABLE IF NOT EXISTS OrdenesTrabajo (
            id_ot INTEGER PRIMARY KEY AUTOINCREMENT,
            n_ot INTEGER NOT NULL,
            id_prov INTEGER NOT NULL REFERENCES Proveedores(id_prov),
            id_sol INTEGER REFERENCES Solicitantes(id_sol),
            fecha_ot TEXT NOT NULL,
            estado INTEGER NOT NULL DEFAULT 0,
            guia_des_ot INTEGER NOT NULL DEFAULT 0,
            id_n1 INTEGER NOT NULL DEFAULT 1,
            id_n2 INTEGER NOT NULL DEFAULT 0,
            id_n3 INTEGER NOT NULL DEFAULT 0,
            id_n4 INTEGER NOT NULL DEFAULT 0,
            id_n5 INTEGER NOT NULL DEFAULT 0,
            id_n6 INTEGER NOT NULL DEFAULT 0,
            id_n7 INTEGER NOT NULL DEFAULT 0,
            id_n8 INTEGER NOT NULL DEFAULT 0,
            id_n9 INTEGER NOT NULL DEFAULT 0,
            id_n10 INTEGER NOT NULL DEFAULT 0,
            id_nivel INTEGER NOT NULL DEFAULT 1,
            ruta_activo TEXT NOT NULL DEFAULT ''
        );

        CREATE INDEX IF NOT EXISTS IX_OT_Numero ON OrdenesTrabajo(n_ot);
        CREATE INDEX IF NOT EXISTS IX_OT_Fecha ON OrdenesTrabajo(fecha_ot);
        CREATE INDEX IF NOT EXISTS IX_OT_Prov ON OrdenesTrabajo(id_prov);
        CREATE INDEX IF NOT EXISTS IX_OT_Sol ON OrdenesTrabajo(id_sol);
        CREATE INDEX IF NOT EXISTS IX_OT_Arbol ON OrdenesTrabajo(id_n2, id_n3, id_n4, id_n5);

        CREATE TABLE IF NOT EXISTS DetalleOT (
            id_det INTEGER PRIMARY KEY AUTOINCREMENT,
            id_ot INTEGER NOT NULL REFERENCES OrdenesTrabajo(id_ot) ON DELETE CASCADE,
            cantidad_det INTEGER NOT NULL CHECK (cantidad_det > 0),
            detalle_ot TEXT NOT NULL,
            estado_det INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS IX_DetalleOT_IdOt ON DetalleOT(id_ot);

        CREATE TABLE IF NOT EXISTS VistoBueno (
            id_vb INTEGER PRIMARY KEY AUTOINCREMENT,
            id_ot INTEGER NOT NULL REFERENCES OrdenesTrabajo(id_ot) ON DELETE CASCADE,
            id_usuario TEXT NOT NULL REFERENCES Usuarios(id_usuario),
            tipo_vb TEXT NOT NULL CHECK (tipo_vb IN ('VBS', 'VBJ')),
            fecha_vb TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            UNIQUE(id_ot, tipo_vb)
        );

        CREATE TABLE IF NOT EXISTS Facturas (
            id_factura INTEGER PRIMARY KEY AUTOINCREMENT,
            n_fac INTEGER NOT NULL,
            id_prov INTEGER NOT NULL REFERENCES Proveedores(id_prov),
            fecha_fac TEXT NOT NULL,
            recibido_por TEXT NOT NULL DEFAULT '',
            pesos_neto REAL NOT NULL DEFAULT 0,
            pesos_iva REAL NOT NULL DEFAULT 0,
            pesos_total REAL NOT NULL DEFAULT 0,
            detalle_fac TEXT NOT NULL DEFAULT '',
            usuario_registro TEXT NOT NULL DEFAULT '',
            fecha_registro TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            UNIQUE(n_fac, id_prov)
        );

        CREATE INDEX IF NOT EXISTS IX_Facturas_Prov ON Facturas(id_prov, fecha_fac);

        CREATE TABLE IF NOT EXISTS FacturaDetalles (
            id_fac_det INTEGER PRIMARY KEY AUTOINCREMENT,
            id_factura INTEGER NOT NULL REFERENCES Facturas(id_factura) ON DELETE CASCADE,
            id_ot INTEGER NOT NULL REFERENCES OrdenesTrabajo(id_ot),
            id_det INTEGER REFERENCES DetalleOT(id_det),
            valor_trabajo REAL NOT NULL DEFAULT 0,
            fecha_asociacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            usuario_sesion TEXT NOT NULL DEFAULT '',
            UNIQUE(id_factura, id_det)
        );

        CREATE INDEX IF NOT EXISTS IX_FacDet_Ot ON FacturaDetalles(id_ot);

        CREATE TABLE IF NOT EXISTS AuditLog (
            id_log INTEGER PRIMARY KEY AUTOINCREMENT,
            fecha_utc TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            usuario TEXT NOT NULL,
            modulo TEXT NOT NULL,
            accion TEXT NOT NULL,
            detalle TEXT NOT NULL DEFAULT ''
        );

        CREATE INDEX IF NOT EXISTS IX_AuditLog_Fecha ON AuditLog(fecha_utc DESC);

        CREATE TABLE IF NOT EXISTS Mant_Oficios (
            id_oficio INTEGER PRIMARY KEY,
            desc_oficio TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Mant_Motivos (
            id_motivo INTEGER PRIMARY KEY,
            desc_motivo TEXT NOT NULL,
            considerado INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS Mant_Empresas (
            id_empresa INTEGER PRIMARY KEY,
            desc_empresa TEXT NOT NULL,
            direccion TEXT NOT NULL DEFAULT '',
            telefono TEXT NOT NULL DEFAULT '',
            email TEXT NOT NULL DEFAULT '',
            oficio TEXT NOT NULL DEFAULT '',
            fax TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_DescInformes (
            id_desc INTEGER PRIMARY KEY,
            desc_inf TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Mant_Personal (
            id_personal INTEGER PRIMARY KEY,
            cod_personal TEXT NOT NULL DEFAULT '',
            rut_personal TEXT NOT NULL DEFAULT '',
            nombres TEXT NOT NULL DEFAULT '',
            apellidos TEXT NOT NULL DEFAULT '',
            desc_personal TEXT NOT NULL DEFAULT '',
            oficio TEXT NOT NULL DEFAULT '',
            email TEXT NOT NULL DEFAULT '',
            celular TEXT NOT NULL DEFAULT '',
            fono TEXT NOT NULL DEFAULT '',
            direccion TEXT NOT NULL DEFAULT '',
            fecha_ingreso TEXT NOT NULL DEFAULT '',
            departamento TEXT NOT NULL DEFAULT '',
            centro_trabajo TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_PersonalRRHH (
            id_empleado TEXT PRIMARY KEY,
            fec_alta_empleado TEXT NOT NULL DEFAULT '',
            nombre TEXT NOT NULL DEFAULT '',
            apellido_p TEXT NOT NULL DEFAULT '',
            apellido_m TEXT NOT NULL DEFAULT '',
            n_centro_costo TEXT NOT NULL DEFAULT '',
            n_puesto TEXT NOT NULL DEFAULT '',
            n_departamento TEXT NOT NULL DEFAULT '',
            n_centro_trabajo TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_Ropa (
            id_registro INTEGER PRIMARY KEY AUTOINCREMENT,
            id_personal INTEGER NOT NULL,
            id_item INTEGER NOT NULL DEFAULT 0,
            descripcion TEXT NOT NULL DEFAULT '',
            talla TEXT NOT NULL DEFAULT '',
            color TEXT NOT NULL DEFAULT '',
            cantidad INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Mant_Insumos (
            id_item INTEGER PRIMARY KEY,
            cod_sap TEXT NOT NULL DEFAULT '',
            cod_proveedor TEXT NOT NULL DEFAULT '',
            desc_item TEXT NOT NULL DEFAULT '',
            stock_unidad REAL NOT NULL DEFAULT 0,
            dias_entrega INTEGER NOT NULL DEFAULT 0,
            prioridad_item INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Mant_InsumosUtilizados (
            id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
            id_ref INTEGER NOT NULL DEFAULT 0,
            id_item INTEGER NOT NULL DEFAULT 0,
            origen TEXT NOT NULL DEFAULT '',
            id_ciclo INTEGER NOT NULL DEFAULT 0,
            cantidad REAL NOT NULL DEFAULT 0,
            costo_unidad REAL NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Mant_OT (
            id INTEGER PRIMARY KEY,
            n_ot TEXT NOT NULL DEFAULT '',
            fecha_recepcion_ot TEXT NOT NULL DEFAULT '',
            fechai TEXT NOT NULL DEFAULT '',
            fechaf TEXT NOT NULL DEFAULT '',
            fecha_pm TEXT NOT NULL DEFAULT '',
            turno TEXT NOT NULL DEFAULT '',
            id_motivo INTEGER NOT NULL DEFAULT 0,
            detencion TEXT NOT NULL DEFAULT '',
            oficio_ot TEXT NOT NULL DEFAULT '',
            id_n1 INTEGER NOT NULL DEFAULT 1,
            id_n2 INTEGER NOT NULL DEFAULT 0,
            id_n3 INTEGER NOT NULL DEFAULT 0,
            id_n4 INTEGER NOT NULL DEFAULT 0,
            id_n5 INTEGER NOT NULL DEFAULT 0,
            id_n6 INTEGER NOT NULL DEFAULT 0,
            id_n7 INTEGER NOT NULL DEFAULT 0,
            id_n8 INTEGER NOT NULL DEFAULT 0,
            id_n9 INTEGER NOT NULL DEFAULT 0,
            id_n10 INTEGER NOT NULL DEFAULT 0,
            nivel INTEGER NOT NULL DEFAULT 1,
            solicitada_por TEXT NOT NULL DEFAULT '',
            recibido_por TEXT NOT NULL DEFAULT '',
            entregado_a TEXT NOT NULL DEFAULT '',
            descripcion_breve TEXT NOT NULL DEFAULT '',
            descripcion_tecnica TEXT NOT NULL DEFAULT '',
            estado_ot TEXT NOT NULL DEFAULT '',
            prioridad_ot INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Mant_OTExterna (
            id_ote INTEGER PRIMARY KEY,
            n_ote INTEGER NOT NULL DEFAULT 0,
            fecha_pedido TEXT NOT NULL DEFAULT '',
            fecha_entrega TEXT NOT NULL DEFAULT '',
            id_empresa INTEGER NOT NULL DEFAULT 0,
            descripcion_ote TEXT NOT NULL DEFAULT '',
            costo REAL NOT NULL DEFAULT 0,
            garantia TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_OTExternaMP (
            id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
            id_mp INTEGER NOT NULL DEFAULT 0,
            id_ote INTEGER NOT NULL DEFAULT 0,
            id_ciclo INTEGER NOT NULL DEFAULT 0
        );

        CREATE TABLE IF NOT EXISTS Mant_MP (
            id INTEGER PRIMARY KEY,
            id_n1 INTEGER NOT NULL DEFAULT 1,
            id_n2 INTEGER NOT NULL DEFAULT 0,
            id_n3 INTEGER NOT NULL DEFAULT 0,
            id_n4 INTEGER NOT NULL DEFAULT 0,
            id_n5 INTEGER NOT NULL DEFAULT 0,
            id_n6 INTEGER NOT NULL DEFAULT 0,
            id_n7 INTEGER NOT NULL DEFAULT 0,
            id_n8 INTEGER NOT NULL DEFAULT 0,
            id_n9 INTEGER NOT NULL DEFAULT 0,
            id_n10 INTEGER NOT NULL DEFAULT 0,
            nivel INTEGER NOT NULL DEFAULT 1,
            fechai_mp TEXT NOT NULL DEFAULT '',
            fecha_cierremp TEXT NOT NULL DEFAULT '',
            descripcionmp TEXT NOT NULL DEFAULT '',
            estadomp TEXT NOT NULL DEFAULT '',
            prioridad INTEGER NOT NULL DEFAULT 0,
            ufrecuenciamp TEXT NOT NULL DEFAULT '',
            frecuenciamp INTEGER NOT NULL DEFAULT 0,
            diasmp INTEGER NOT NULL DEFAULT 0,
            tiempo_previo_aviso INTEGER NOT NULL DEFAULT 0,
            tipo_mant TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_CicloMP (
            id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
            id_mp INTEGER NOT NULL DEFAULT 0,
            id_ciclo INTEGER NOT NULL DEFAULT 0,
            fecha_inicio_ciclo TEXT NOT NULL DEFAULT '',
            fecha_cierre_ciclo TEXT NOT NULL DEFAULT '',
            tiempo REAL NOT NULL DEFAULT 0,
            observacion_ciclo_mp TEXT NOT NULL DEFAULT '',
            estado_ciclo TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_TareasMP (
            id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
            id_mp INTEGER NOT NULL DEFAULT 0,
            id_tarea INTEGER NOT NULL DEFAULT 0,
            id_ciclo INTEGER NOT NULL DEFAULT 0,
            id_n1 INTEGER NOT NULL DEFAULT 1,
            id_n2 INTEGER NOT NULL DEFAULT 0,
            id_n3 INTEGER NOT NULL DEFAULT 0,
            id_n4 INTEGER NOT NULL DEFAULT 0,
            id_n5 INTEGER NOT NULL DEFAULT 0,
            id_n6 INTEGER NOT NULL DEFAULT 0,
            id_n7 INTEGER NOT NULL DEFAULT 0,
            id_n8 INTEGER NOT NULL DEFAULT 0,
            id_n9 INTEGER NOT NULL DEFAULT 0,
            id_n10 INTEGER NOT NULL DEFAULT 0,
            descripcion_tarea TEXT NOT NULL DEFAULT '',
            observacion_tarea_ciclo TEXT NOT NULL DEFAULT '',
            estado TEXT NOT NULL DEFAULT '',
            duracion REAL NOT NULL DEFAULT 0,
            oficio TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_PersonalUtilizado (
            id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
            id_ref INTEGER NOT NULL DEFAULT 0,
            id_personal INTEGER NOT NULL DEFAULT 0,
            origen TEXT NOT NULL DEFAULT '',
            id_ciclo INTEGER NOT NULL DEFAULT 0,
            lugar INTEGER NOT NULL DEFAULT 0,
            observacion TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_Checklist (
            id_chk INTEGER PRIMARY KEY,
            fecha_inicio TEXT NOT NULL DEFAULT '',
            fecha_termino TEXT NOT NULL DEFAULT '',
            duracion REAL NOT NULL DEFAULT 0,
            id_n1 INTEGER NOT NULL DEFAULT 1,
            id_n2 INTEGER NOT NULL DEFAULT 0,
            id_n3 INTEGER NOT NULL DEFAULT 0,
            id_n4 INTEGER NOT NULL DEFAULT 0,
            id_n5 INTEGER NOT NULL DEFAULT 0,
            id_n6 INTEGER NOT NULL DEFAULT 0,
            id_n7 INTEGER NOT NULL DEFAULT 0,
            observacion TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_TareasChecklist (
            id_tarea INTEGER PRIMARY KEY,
            descripcion TEXT NOT NULL DEFAULT '',
            grupo TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_ChkTareas (
            id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
            indice_chk INTEGER NOT NULL DEFAULT 0,
            id_chk INTEGER NOT NULL DEFAULT 0,
            id_tarea INTEGER NOT NULL DEFAULT 0,
            estado TEXT NOT NULL DEFAULT '',
            observacion TEXT NOT NULL DEFAULT ''
        );

        CREATE TABLE IF NOT EXISTS Mant_PersonalChecklist (
            id_checklist INTEGER NOT NULL,
            id_personal INTEGER NOT NULL,
            PRIMARY KEY (id_checklist, id_personal)
        );

        CREATE TABLE IF NOT EXISTS Mant_UsuariosLegacy (
            id_usuario INTEGER PRIMARY KEY,
            desc_usuario TEXT NOT NULL,
            permisos_json TEXT NOT NULL DEFAULT ''
        );
        """;

    public static void EnsureInitialized(SqliteConnectionFactory factory, InitialAdminSeedOptions? initialAdmin = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = SchemaSql;
            cmd.ExecuteNonQuery();
        }

        MigrateUsuariosSchema(conn);
        MigrateActivosSchema(conn);
        EnsurePerformanceIndexes(conn);
        SeedLevels(conn);
        SeedInitialGroups(conn);
        SeedRootAsset(conn);
        InitialAdminProvisioner.EnsureInitialAdmin(conn, initialAdmin);
    }

    private static void MigrateActivosSchema(SqliteConnection conn)
    {
        HashSet<string> cols = new(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand infoCmd = conn.CreateCommand())
        {
            infoCmd.CommandText = "PRAGMA table_info(Activos);";
            using SqliteDataReader rdr = infoCmd.ExecuteReader();
            while (rdr.Read())
            {
                cols.Add(rdr.GetString(1));
            }
        }

        (string Name, string Ddl)[] extraColumns =
        [
            ("cod_sap", "TEXT NOT NULL DEFAULT ''"),
            ("fabricante", "TEXT NOT NULL DEFAULT ''"),
            ("serie", "TEXT NOT NULL DEFAULT ''"),
            ("modelo", "TEXT NOT NULL DEFAULT ''"),
            ("fecha_compra", "TEXT NOT NULL DEFAULT ''"),
            ("valor_compra", "REAL NOT NULL DEFAULT 0"),
            ("valor_actual", "REAL NOT NULL DEFAULT 0"),
            ("fecha_instalacion", "TEXT NOT NULL DEFAULT ''"),
            ("tipo_equipo", "TEXT NOT NULL DEFAULT ''"),
            ("estado_operativo", "TEXT NOT NULL DEFAULT ''"),
            ("vida_util", "TEXT NOT NULL DEFAULT ''"),
            ("observacion", "TEXT NOT NULL DEFAULT ''"),
            ("prioridad", "INTEGER NOT NULL DEFAULT 0"),
            ("material", "TEXT NOT NULL DEFAULT ''"),
            ("categoria", "TEXT NOT NULL DEFAULT ''"),
            ("ruta_plano", "TEXT NOT NULL DEFAULT ''"),
            ("nombre_plano", "TEXT NOT NULL DEFAULT ''"),
            ("ruta_foto", "TEXT NOT NULL DEFAULT ''"),
            ("nombre_foto", "TEXT NOT NULL DEFAULT ''"),
            ("info_tecnica_json", "TEXT NOT NULL DEFAULT ''")
        ];

        foreach ((string name, string ddl) in extraColumns)
        {
            if (!cols.Contains(name))
            {
                ExecuteNonQuery(conn, $"ALTER TABLE Activos ADD COLUMN {name} {ddl};");
            }
        }

        // Normalización idempotente de variantes históricas de categoría sin alterar jerarquía ni niveles
        ExecuteNonQuery(conn, "UPDATE Activos SET categoria = 'MAQUINA' WHERE categoria IN ('MÁQUINA', 'Máquina', 'Maquina', 'maquina');");
        ExecuteNonQuery(conn, "UPDATE Activos SET categoria = 'EMPRESA' WHERE parent_id IS NULL AND categoria = '';");

        bool hasParentIndex = false;
        using (SqliteCommand idxCmd = conn.CreateCommand())
        {
            idxCmd.CommandText = "PRAGMA index_list('Activos');";
            using SqliteDataReader idxRdr = idxCmd.ExecuteReader();
            while (idxRdr.Read())
            {
                string idxName = idxRdr.GetString(1);
                using SqliteCommand colCmd = conn.CreateCommand();
                colCmd.CommandText = $"PRAGMA index_info('{idxName.Replace("'", "''")}');";
                using SqliteDataReader colRdr = colCmd.ExecuteReader();
                if (colRdr.Read() && string.Equals(colRdr.GetString(2), "parent_id", StringComparison.OrdinalIgnoreCase))
                {
                    hasParentIndex = true;
                    break;
                }
            }
        }

        if (!hasParentIndex)
        {
            ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_Activos_ParentId ON Activos(parent_id);");
        }
    }

    private static void MigrateUsuariosSchema(SqliteConnection conn)
    {
        HashSet<string> cols = new(StringComparer.OrdinalIgnoreCase);
        using (SqliteCommand infoCmd = conn.CreateCommand())
        {
            infoCmd.CommandText = "PRAGMA table_info(Usuarios);";
            using SqliteDataReader rdr = infoCmd.ExecuteReader();
            while (rdr.Read())
            {
                cols.Add(rdr.GetString(1));
            }
        }

        if (!cols.Contains("codigo_id"))
        {
            ExecuteNonQuery(conn, "ALTER TABLE Usuarios ADD COLUMN codigo_id INTEGER;");
        }

        if (!cols.Contains("debe_cambiar_clave"))
        {
            ExecuteNonQuery(conn, "ALTER TABLE Usuarios ADD COLUMN debe_cambiar_clave INTEGER NOT NULL DEFAULT 0;");
        }

        if (!cols.Contains("fecha_modificacion"))
        {
            ExecuteNonQuery(conn, "ALTER TABLE Usuarios ADD COLUMN fecha_modificacion TEXT NOT NULL DEFAULT '';");
            ExecuteNonQuery(conn, """
                UPDATE Usuarios
                SET fecha_modificacion = COALESCE(NULLIF(fecha_creacion, ''), datetime('now','localtime'))
                WHERE fecha_modificacion = '' OR fecha_modificacion IS NULL;
                """);
        }

        if (!cols.Contains("id_grupo"))
        {
            ExecuteNonQuery(conn, "ALTER TABLE Usuarios ADD COLUMN id_grupo INTEGER REFERENCES Grupos(id_grupo) ON DELETE SET NULL;");
        }

        if (!cols.Contains("row_version"))
        {
            ExecuteNonQuery(conn, "ALTER TABLE Usuarios ADD COLUMN row_version INTEGER NOT NULL DEFAULT 1;");
        }

        // Backfill auto-increment numeric IDs for any existing legacy rows without codigo_id
        List<string> missingIds = [];
        using (SqliteCommand missingCmd = conn.CreateCommand())
        {
            missingCmd.CommandText = "SELECT id_usuario FROM Usuarios WHERE codigo_id IS NULL ORDER BY rowid;";
            using SqliteDataReader rdr = missingCmd.ExecuteReader();
            while (rdr.Read())
            {
                missingIds.Add(rdr.GetString(0));
            }
        }

        if (missingIds.Count > 0)
        {
            int nextCode;
            using (SqliteCommand maxCmd = conn.CreateCommand())
            {
                maxCmd.CommandText = "SELECT COALESCE(MAX(codigo_id), 0) + 1 FROM Usuarios;";
                nextCode = Convert.ToInt32(maxCmd.ExecuteScalar());
            }

            using SqliteTransaction tx = conn.BeginTransaction();
            foreach (string idUsuario in missingIds)
            {
                using SqliteCommand upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE Usuarios SET codigo_id = $code WHERE id_usuario = $id;";
                upd.Parameters.AddWithValue("$code", nextCode++);
                upd.Parameters.AddWithValue("$id", idUsuario);
                upd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        ExecuteNonQuery(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Usuarios_CodigoId ON Usuarios(codigo_id);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_Usuarios_Nivel_Activo ON Usuarios(id_nivel, activo);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_Usuarios_Grupo_Activo ON Usuarios(id_grupo, activo);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_Usuarios_FechaCreacion ON Usuarios(fecha_creacion DESC, codigo_id DESC);");
    }

    private static void EnsurePerformanceIndexes(SqliteConnection conn)
    {
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_OT_FechaDesc_IdDesc ON OrdenesTrabajo(fecha_ot DESC, id_ot DESC);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_Facturas_FechaDesc_IdDesc ON Facturas(fecha_fac DESC, id_factura DESC);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_MantOT_FechaDesc_IdDesc ON Mant_OT(fechai DESC, id DESC);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_MantOT_Oficio_Estado ON Mant_OT(oficio_ot, estado_ot);");
        ExecuteNonQuery(conn, "CREATE INDEX IF NOT EXISTS IX_AuditLog_FechaDesc_IdDesc ON AuditLog(fecha_utc DESC, id_log DESC);");
    }

    private static void ExecuteNonQuery(SqliteConnection conn, string sql)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void SeedLevels(SqliteConnection conn)
    {
        ExecuteNonQuery(conn, """
            INSERT OR IGNORE INTO Niveles(id_nivel, tipo_nivel) VALUES
            (1, 'Usuario'),
            (2, 'Supervisor'),
            (3, 'Jefatura'),
            (4, 'Administrador');
            """);
    }

    private static void SeedInitialGroups(SqliteConnection conn)
    {
        ExecuteNonQuery(conn, """
            INSERT OR IGNORE INTO Grupos(nombre, descripcion, activo) VALUES
            ('MECÁNICOS', 'Personal técnico especialista en mantención mecánica industrial.', 1),
            ('ELÉCTRICOS', 'Personal técnico especialista en sistemas eléctricos y potencia.', 1),
            ('ELECTRÓNICOS', 'Personal especialista en instrumentación, PLC y electrónica industrial.', 1),
            ('EXTERNOS', 'Contratistas, proveedores técnicos y servicios externos especializados.', 1),
            ('OPERADORES', 'Personal de operación de líneas productivas y equipos industriales.', 1);
            """);
    }

    private static void SeedRootAsset(SqliteConnection conn)
    {
        ExecuteNonQuery(conn, """
            INSERT OR IGNORE INTO Activos(id_activo, parent_id, nivel, descripcion, activo)
            VALUES (1, NULL, 1, 'CAMBIASO HNOS S.A.C.', 1);
            """);
    }
}
