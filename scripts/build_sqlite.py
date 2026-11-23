import json
import os
import sqlite3
import sys


SCHEMA_SQL = """
PRAGMA foreign_keys = OFF;
PRAGMA journal_mode = WAL;

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
"""


def s(val):
    if val is None:
        return ""
    return str(val).strip()


def b(val):
    if isinstance(val, bool):
        return 1 if val else 0
    if val is None:
        return 0
    return 1 if str(val).strip().lower() in ("1", "true", "-1") else 0


def main():
    json_path, db_path = sys.argv[1], sys.argv[2]
    with open(json_path, "r", encoding="utf-8-sig") as f:
        data = json.load(f)

    if os.path.exists(db_path):
        os.remove(db_path)
    os.makedirs(os.path.dirname(db_path), exist_ok=True)

    conn = sqlite3.connect(db_path)
    cur = conn.cursor()
    cur.executescript(SCHEMA_SQL)

    # 1. Niveles
    niveles = data.get("Nivel") or []
    for n in niveles:
        cur.execute(
            "INSERT INTO Niveles(id_nivel, tipo_nivel) VALUES (?, ?)",
            (int(n["id_nivel"]), s(n["tipo_nivel"])),
        )

    # 2. Proveedores
    valid_provs = set()
    for p in data.get("Proveedores") or []:
        pid = int(p["id_prov"])
        valid_provs.add(pid)
        cur.execute(
            """INSERT INTO Proveedores(id_prov, nom_prov, dir_prov, fono_prov, fax_prov, mail_prov)
               VALUES (?, ?, ?, ?, ?, ?)""",
            (pid, s(p["nom_prov"]), s(p["dir_prov"]), s(p["fono_prov"]), s(p["fax_prov"]), s(p["mail_prov"])),
        )

    # 3. Solicitantes
    valid_sols = set()
    for sol in data.get("Solicitante") or []:
        sid = int(sol["id_sol"])
        valid_sols.add(sid)
        cur.execute(
            """INSERT INTO Solicitantes(id_sol, alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol)
               VALUES (?, ?, ?, ?, ?, ?, ?)""",
            (
                sid,
                s(sol["alias"]),
                s(sol["nombre_sol"]),
                s(sol["ape_pat_sol"]),
                s(sol["ape_mat_sol"]),
                s(sol["fono_sol"]),
                s(sol["mail_sol"]),
            ),
        )

    # 4. Usuarios & Permisos
    perms_by_user = {}
    for perm in data.get("Permisos") or []:
        uid = s(perm.get("Id_usuario") or perm.get("id_usuario"))
        perms_by_user[uid] = perm

    for u in data.get("Usuarios") or []:
        uid = s(u["id_usuario"])
        perm = perms_by_user.get(uid, {})
        # Infer role level from permissions
        is_admin = b(perm.get("M4")) and b(perm.get("M1")) and b(perm.get("M2"))
        is_jef = b(perm.get("tab_0_VBJ"))
        is_sup = b(perm.get("tab_0_VBS"))
        nivel_id = 4 if is_admin else (3 if is_jef else (2 if is_sup else 1))
        pass_hex = s(u.get("pass_hex"))
        pass_hash = f"LEGACY_VB6${pass_hex}"

        cur.execute(
            """INSERT INTO Usuarios(
                id_usuario, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
                ape_mat_usuario, id_nivel, mail_usuario, fono_usuario, es_solicitante, activo
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1)""",
            (
                uid,
                s(u["alias_usuario"]),
                pass_hash,
                s(u["Nombre_usuario"]),
                s(u["Ape_pat_usuario"]),
                s(u["ape_mat_usuario"]),
                nivel_id,
                s(u["mail_usuario"]),
                s(u["fono_usuario"]),
                b(u["Solicitante"]),
            ),
        )

        # Note: For legacy user 128498745 (main admin Luis Reyes), grant M3 Mantenedor too if admin, or preserve exact legacy flags cleanly:
        # Wait: preserve exact legacy flags from Permisos table (with fixed VBJ/VBS mapping!)
        cur.execute(
            """INSERT INTO Permisos(
                id_usuario, m1_ot, tab_0_1_ingreso_ot, tab_0_vbj, tab_0_vbs,
                tab_0_2_busqueda_ot, tab_0_3_impresion_ot, m2_facturas,
                tab_1_0_ingreso_fac, tab_1_1_busqueda_fac, m3_mantenedor,
                m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                uid,
                b(perm.get("M1")),
                b(perm.get("tab_0_1")),
                b(perm.get("tab_0_VBJ")),
                b(perm.get("tab_0_VBS")),
                b(perm.get("tab_0_2")),
                b(perm.get("tab_0_3")),
                b(perm.get("M2")),
                b(perm.get("tab_1_0")),
                b(perm.get("tab_1_1")),
                1 if uid == "128498745" else b(perm.get("M3")),  # Enable Mantenedor for main administrator
                b(perm.get("M4")),
                b(perm.get("tab_2_0")),
                b(perm.get("TAB_2_1")),
            ),
        )

    # 5. Reconstruct Asset Tree (Activos) from historical OT_PROV hierarchy IDs
    activos = {
        1: (None, 1, "CAMBIASO HNOS S.A.C."),
        2: (1, 2, "PLANTA PLÁSTICOS"),
        3: (1, 2, "LOGÍSTICA Y GRÚAS HORQUILLA"),
        4: (1, 2, "PLANTA TÉ"),
    }

    ots_raw = data.get("OT_PROV") or []
    valid_ots = []
    skipped_ots = 0
    for o in ots_raw:
        id_ot = int(o["id_ot"])
        id_prov = int(o["id_prov"] or 0)
        n_ot = int(o["n_ot"] or 0)
        fecha = s(o.get("fecha_ot"))
        if id_ot == 0 or id_prov == 0 or n_ot == 0 or not fecha or id_prov not in valid_provs:
            skipped_ots += 1
            continue
        valid_ots.append(o)

        # Register referenced tree nodes
        chain = [int(o.get(f"id_n{i}") or 0) for i in range(1, 11)]
        parent = 1
        for lvl in range(2, 11):
            node_id = chain[lvl - 1]
            if node_id <= 0:
                break
            if node_id not in activos:
                prefix = {2: "Planta", 3: "Área / Sección", 4: "Línea / Equipo", 5: "Componente"}.get(lvl, f"Nivel {lvl}")
                activos[node_id] = (parent, lvl, f"{prefix} #{node_id}")
            parent = node_id

    # Insert Activos sorted by level then id
    for aid, (pid, lvl, desc) in sorted(activos.items(), key=lambda kv: (kv[1][1], kv[0])):
        cur.execute(
            "INSERT OR IGNORE INTO Activos(id_activo, parent_id, nivel, descripcion, activo) VALUES (?, ?, ?, ?, 1)",
            (aid, pid, lvl, desc),
        )

    def build_path(chain):
        parts = []
        for nid in chain:
            if nid > 0 and nid in activos:
                parts.append(activos[nid][2])
        return " \\ ".join(parts)

    # 6. OrdenesTrabajo
    migrated_ot_ids = set()
    for o in valid_ots:
        id_ot = int(o["id_ot"])
        migrated_ot_ids.add(id_ot)
        id_sol_raw = int(o.get("id_sol") or 0)
        id_sol = id_sol_raw if id_sol_raw in valid_sols else None
        chain = [int(o.get(f"id_n{i}") or 0) for i in range(1, 11)]
        if chain[0] == 0:
            chain[0] = 1
        ruta = build_path(chain)
        id_nivel = max(1, int(o.get("id_nivel") or 1))
        cur.execute(
            """INSERT INTO OrdenesTrabajo(
                id_ot, n_ot, id_prov, id_sol, fecha_ot, estado, guia_des_ot,
                id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
                id_nivel, ruta_activo
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                id_ot,
                int(o["n_ot"]),
                int(o["id_prov"]),
                id_sol,
                s(o["fecha_ot"]),
                int(o.get("activo") or 0),
                int(o.get("guia_des_ot") or 0),
                *chain,
                id_nivel,
                ruta,
            ),
        )

    # 7. DetalleOT
    migrated_det = 0
    for d in data.get("Detalle_ot") or []:
        id_ot = int(d.get("id_ot") or 0)
        if id_ot not in migrated_ot_ids:
            continue
        cant = int(d.get("cantidad_det") or 1)
        if cant <= 0:
            cant = 1
        det_text = s(d.get("detalle_ot"))
        if not det_text:
            det_text = "(Sin detalle)"
        cur.execute(
            """INSERT INTO DetalleOT(id_det, id_ot, cantidad_det, detalle_ot, estado_det)
               VALUES (?, ?, ?, ?, ?)""",
            (int(d["Id_Det"]), id_ot, cant, det_text, b(d.get("Estado_det"))),
        )
        migrated_det += 1

    # Record initial migration audit entry
    cur.execute(
        """INSERT INTO AuditLog(usuario, modulo, accion, detalle)
           VALUES ('SISTEMA', 'Migración', 'MIGRACION_INICIAL', ?)""",
        (
            f"Migración desde OT_PROV.mdb completada: {len(valid_provs)} proveedores, "
            f"{len(valid_sols)} solicitantes, {len(migrated_ot_ids)} OTs ({skipped_ots} corruptas omitidas), "
            f"{migrated_det} detalles, {len(activos)} nodos de activos.",
        ),
    )

    conn.commit()
    cur.execute("PRAGMA foreign_key_check;")
    fk_issues = cur.fetchall()
    cur.execute("PRAGMA integrity_check;")
    integrity = cur.fetchone()[0]
    cur.execute("PRAGMA wal_checkpoint(TRUNCATE);")
    conn.close()

    print(f"SQLite Integrity Check: {integrity}")
    print(f"Foreign Key Violations: {len(fk_issues)}")
    print(
        f"Migrated => Niveles: {len(niveles)}, Proveedores: {len(valid_provs)}, "
        f"Solicitantes: {len(valid_sols)}, Usuarios: {len(data.get('Usuarios') or [])}, "
        f"Activos: {len(activos)}, OTs: {len(migrated_ot_ids)} (skipped {skipped_ots}), Detalles: {migrated_det}"
    )
    if integrity != "ok" or len(fk_issues) > 0:
        sys.exit(1)


if __name__ == "__main__":
    main()
