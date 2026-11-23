using Microsoft.Data.Sqlite;
using Sispro.Core.Enums;
using Sispro.Core.Security;

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

        CREATE TABLE IF NOT EXISTS Usuarios (
            id_usuario TEXT PRIMARY KEY,
            alias_usuario TEXT NOT NULL UNIQUE COLLATE NOCASE,
            pass_hash TEXT NOT NULL,
            nombre_usuario TEXT NOT NULL,
            ape_pat_usuario TEXT NOT NULL,
            ape_mat_usuario TEXT NOT NULL DEFAULT '',
            id_nivel INTEGER NOT NULL DEFAULT 1 REFERENCES Niveles(id_nivel),
            mail_usuario TEXT NOT NULL DEFAULT '',
            fono_usuario TEXT NOT NULL DEFAULT '',
            es_solicitante INTEGER NOT NULL DEFAULT 0,
            activo INTEGER NOT NULL DEFAULT 1,
            fecha_creacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
            ultimo_acceso TEXT
        );

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
            activo INTEGER NOT NULL DEFAULT 1
        );

        CREATE INDEX IF NOT EXISTS IX_Activos_Parent ON Activos(parent_id, activo, descripcion);

        CREATE TABLE IF NOT EXISTS OrdenesTrabajo (
            id_ot INTEGER PRIMARY KEY AUTOINCREMENT,
            n_ot INTEGER NOT NULL UNIQUE,
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
        """;

    public static void EnsureInitialized(SqliteConnectionFactory factory, bool seedDefaultAdminIfEmpty = true)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using (SqliteCommand cmd = conn.CreateCommand())
        {
            cmd.CommandText = SchemaSql;
            cmd.ExecuteNonQuery();
        }

        SeedLevels(conn);
        SeedRootAsset(conn);

        if (seedDefaultAdminIfEmpty)
        {
            SeedAdminIfEmpty(conn);
        }
    }

    private static void SeedLevels(SqliteConnection conn)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO Niveles(id_nivel, tipo_nivel) VALUES
            (1, 'Usuario'),
            (2, 'Supervisor'),
            (3, 'Jefatura'),
            (4, 'Administrador');
            """;
        cmd.ExecuteNonQuery();
    }

    private static void SeedRootAsset(SqliteConnection conn)
    {
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO Activos(id_activo, parent_id, nivel, descripcion, activo)
            VALUES (1, NULL, 1, 'CAMBIASO HNOS S.A.C.', 1);
            """;
        cmd.ExecuteNonQuery();
    }

    private static void SeedAdminIfEmpty(SqliteConnection conn)
    {
        using SqliteCommand countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM Usuarios;";
        long count = (long)(countCmd.ExecuteScalar() ?? 0L);
        if (count > 0)
        {
            return;
        }

        string hash = PasswordSecurity.HashPassword("Admin2026!");
        using SqliteTransaction tx = conn.BeginTransaction();

        using (SqliteCommand userCmd = conn.CreateCommand())
        {
            userCmd.Transaction = tx;
            userCmd.CommandText = """
                INSERT INTO Usuarios(
                    id_usuario, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
                    ape_mat_usuario, id_nivel, mail_usuario, fono_usuario, es_solicitante, activo
                ) VALUES (
                    '111111111', 'admin', $hash, 'Administrador', 'Sistema',
                    'SISPRO', $nivel, 'soporte@cambiaso.cl', '', 0, 1
                );
                """;
            userCmd.Parameters.AddWithValue("$hash", hash);
            userCmd.Parameters.AddWithValue("$nivel", (int)UserRoleLevel.Administrador);
            userCmd.ExecuteNonQuery();
        }

        using (SqliteCommand permCmd = conn.CreateCommand())
        {
            permCmd.Transaction = tx;
            permCmd.CommandText = """
                INSERT INTO Permisos(
                    id_usuario, m1_ot, tab_0_1_ingreso_ot, tab_0_vbj, tab_0_vbs,
                    tab_0_2_busqueda_ot, tab_0_3_impresion_ot, m2_facturas,
                    tab_1_0_ingreso_fac, tab_1_1_busqueda_fac, m3_mantenedor,
                    m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
                ) VALUES ('111111111', 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
                """;
            permCmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
