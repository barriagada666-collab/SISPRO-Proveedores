import base64
import csv
import hashlib
import json
import os
import secrets
import sqlite3
import sys

csv.field_size_limit(10 * 1024 * 1024)

SCHEMA_SQL = """
PRAGMA foreign_keys = OFF;
PRAGMA synchronous = OFF;
PRAGMA temp_store = MEMORY;
PRAGMA journal_mode = WAL;

-- =====================================================================
-- 1. NÚCLEO SISPRO (Proveedores, Órdenes de Trabajo, Facturas, RBAC)
-- =====================================================================
CREATE TABLE IF NOT EXISTS Niveles (
    id_nivel INTEGER PRIMARY KEY,
    tipo_nivel TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Usuarios (
    id_usuario TEXT PRIMARY KEY,
    codigo_id INTEGER,
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
    debe_cambiar_clave INTEGER NOT NULL DEFAULT 0,
    fecha_creacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
    fecha_modificacion TEXT NOT NULL DEFAULT (datetime('now','localtime')),
    ultimo_acceso TEXT
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_Usuarios_CodigoId ON Usuarios(codigo_id);
CREATE INDEX IF NOT EXISTS IX_Usuarios_Nivel_Activo ON Usuarios(id_nivel, activo);

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

-- =====================================================================
-- 2. MÓDULO MANTENCIÓN INDUSTRIAL (Importado de BD MANTENCION.mdb)
-- =====================================================================
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

CREATE INDEX IF NOT EXISTS IX_MantPersonal_Rut ON Mant_Personal(rut_personal);
CREATE INDEX IF NOT EXISTS IX_MantPersonal_Oficio ON Mant_Personal(oficio);

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

CREATE INDEX IF NOT EXISTS IX_MantInsumos_Sap ON Mant_Insumos(cod_sap);
CREATE INDEX IF NOT EXISTS IX_MantInsumos_Desc ON Mant_Insumos(desc_item);

CREATE TABLE IF NOT EXISTS Mant_InsumosUtilizados (
    id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
    id_ref INTEGER NOT NULL DEFAULT 0,
    id_item INTEGER NOT NULL DEFAULT 0,
    origen TEXT NOT NULL DEFAULT '',
    id_ciclo INTEGER NOT NULL DEFAULT 0,
    cantidad REAL NOT NULL DEFAULT 0,
    costo_unidad REAL NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS IX_MantInsUtil_Item ON Mant_InsumosUtilizados(id_item);
CREATE INDEX IF NOT EXISTS IX_MantInsUtil_Ref ON Mant_InsumosUtilizados(id_ref, origen);

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

CREATE INDEX IF NOT EXISTS IX_MantOT_FechaI ON Mant_OT(fechai);
CREATE INDEX IF NOT EXISTS IX_MantOT_Estado ON Mant_OT(estado_ot);
CREATE INDEX IF NOT EXISTS IX_MantOT_Oficio ON Mant_OT(oficio_ot);
CREATE INDEX IF NOT EXISTS IX_MantOT_Arbol ON Mant_OT(id_n2, id_n3, id_n4);
CREATE INDEX IF NOT EXISTS IX_MantOT_Numero ON Mant_OT(n_ot);

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

CREATE INDEX IF NOT EXISTS IX_MantMP_Arbol ON Mant_MP(id_n2, id_n3, id_n4);
CREATE INDEX IF NOT EXISTS IX_MantMP_Estado ON Mant_MP(estadomp);

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

CREATE INDEX IF NOT EXISTS IX_MantCicloMP_Mp ON Mant_CicloMP(id_mp, id_ciclo);

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

CREATE INDEX IF NOT EXISTS IX_MantTareasMP_MpCiclo ON Mant_TareasMP(id_mp, id_ciclo);

CREATE TABLE IF NOT EXISTS Mant_PersonalUtilizado (
    id_reg INTEGER PRIMARY KEY AUTOINCREMENT,
    id_ref INTEGER NOT NULL DEFAULT 0,
    id_personal INTEGER NOT NULL DEFAULT 0,
    origen TEXT NOT NULL DEFAULT '',
    id_ciclo INTEGER NOT NULL DEFAULT 0,
    lugar INTEGER NOT NULL DEFAULT 0,
    observacion TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS IX_MantPersUtil_Ref ON Mant_PersonalUtilizado(id_ref, origen);
CREATE INDEX IF NOT EXISTS IX_MantPersUtil_Pers ON Mant_PersonalUtilizado(id_personal);

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
"""


def s(val):
    if val is None:
        return ""
    return str(val).strip()


def clean_date(val):
    v = s(val)
    if v.startswith("1899-12-30") or v.startswith("1899-12-29"):
        return ""
    return v


def i(val, default=0):
    v = s(val)
    if not v:
        return default
    try:
        return int(float(v))
    except ValueError:
        return default


def f(val, default=0.0):
    v = s(val)
    if not v:
        return default
    try:
        return float(v)
    except ValueError:
        return default


def b(val):
    v = s(val).lower()
    return 1 if v in ("1", "true", "-1", "si", "sí") else 0


def read_csv(dump_dir, filename):
    path = os.path.join(dump_dir, filename)
    if not os.path.exists(path):
        return []
    with open(path, "r", encoding="utf-8-sig", newline="") as fh:
        return list(csv.DictReader(fh))


def get_ci(row, *keys, default=""):
    for k in keys:
        if k in row and row[k] is not None:
            return row[k]
    lower_map = {rk.lower(): rv for rk, rv in row.items()}
    for k in keys:
        if k.lower() in lower_map and lower_map[k.lower()] is not None:
            return lower_map[k.lower()]
    return default


def main():
    dump_dir, db_path = sys.argv[1], sys.argv[2]

    if os.path.exists(db_path):
        os.remove(db_path)
    os.makedirs(os.path.dirname(db_path), exist_ok=True)

    conn = sqlite3.connect(db_path)
    cur = conn.cursor()
    cur.executescript(SCHEMA_SQL)

    # 1. Niveles
    niveles = read_csv(dump_dir, "ot_Nivel.csv")
    cur.executemany(
        "INSERT INTO Niveles(id_nivel, tipo_nivel) VALUES (?, ?)",
        [(i(r["id_nivel"]), s(r["tipo_nivel"])) for r in niveles],
    )

    # 2. Proveedores (handle exact duplicate nom_prov safely without dropping any id_prov)
    provs = read_csv(dump_dir, "ot_Proveedores.csv")
    valid_provs = set()
    seen_names = {}
    prov_rows = []
    for r in provs:
        pid = i(r["id_prov"])
        valid_provs.add(pid)
        nom = s(r["nom_prov"]) or f"PROVEEDOR #{pid}"
        key = nom.lower()
        if key in seen_names:
            seen_names[key] += 1
            nom = f"{nom} ({seen_names[key]})"
        else:
            seen_names[key] = 1
        prov_rows.append((pid, nom, s(r["dir_prov"]), s(r["fono_prov"]), s(r["fax_prov"]), s(r["mail_prov"])))
    cur.executemany(
        "INSERT INTO Proveedores(id_prov, nom_prov, dir_prov, fono_prov, fax_prov, mail_prov) VALUES (?, ?, ?, ?, ?, ?)",
        prov_rows,
    )

    # 3. Solicitantes (current + historical inactive rescue from legacy OT_PROV.mdb for id_sol 5 & 6)
    sols = read_csv(dump_dir, "ot_Solicitante.csv")
    valid_sols = set()
    seen_sol_aliases = set()
    sol_rows = []
    for r in sols:
        sid = i(r["id_sol"])
        alias = s(r["alias"]) or f"Sol #{sid}"
        valid_sols.add(sid)
        seen_sol_aliases.add(alias.lower())
        sol_rows.append((sid, alias, s(r["nombre_sol"]), s(r["ape_pat_sol"]), s(r["ape_mat_sol"]), s(r["fono_sol"]), s(r["mail_sol"]), 1))

    # Rescue deleted historical requesters (e.g. id_sol=5 J Maureira, id_sol=6 L Fuentes) as inactive so 1,506 OTs preserve real names!
    for r in read_csv(dump_dir, "legacy_Solicitante.csv"):
        sid = i(r["id_sol"])
        if sid not in valid_sols:
            alias = s(r["alias"]) or f"Hist #{sid}"
            if alias.lower() in seen_sol_aliases:
                alias = f"{alias} (Hist)"
            valid_sols.add(sid)
            seen_sol_aliases.add(alias.lower())
            sol_rows.append((sid, alias, s(r["nombre_sol"]), s(r["ape_pat_sol"]), s(r["ape_mat_sol"]), s(r["fono_sol"]), s(r["mail_sol"]), 0))

    cur.executemany(
        """INSERT INTO Solicitantes(id_sol, alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, activo)
           VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
        sol_rows,
    )

    # 4. Usuarios & Permisos (from OT_PROV.mdb)
    perms_by_user = {}
    for perm in read_csv(dump_dir, "ot_Permisos.csv"):
        uid = s(get_ci(perm, "Id_usuario", "id_usuario"))
        perms_by_user[uid] = perm

    users_raw = read_csv(dump_dir, "ot_Usuarios.csv")
    code_seq = 1
    for u in users_raw:
        uid = s(u["id_usuario"])
        perm = perms_by_user.get(uid, {})
        is_admin = b(get_ci(perm, "M4")) and b(get_ci(perm, "M1")) and b(get_ci(perm, "M2"))
        is_jef = b(get_ci(perm, "tab_0_VBJ"))
        is_sup = b(get_ci(perm, "tab_0_VBS"))
        nivel_id = 4 if is_admin else (3 if is_jef else (2 if is_sup else 1))
        pass_hex = s(u.get("pass_hex"))
        alias_u = s(u["alias_usuario"])
        raw_bytes = bytes.fromhex(pass_hex) if pass_hex else b""
        # If legacy Access stored an unencrypted plaintext password equal to alias (e.g. BORIS),
        # upgrade it directly to PBKDF2-HMAC-SHA256 and require change on first login.
        if raw_bytes and raw_bytes.decode("cp1252", errors="ignore").strip().lower() == alias_u.lower():
            salt = secrets.token_bytes(16)
            dk = hashlib.pbkdf2_hmac("sha256", raw_bytes.decode("cp1252").encode("utf-8"), salt, 100000, dklen=32)
            pass_hash = f"PBKDF2$100000${base64.b64encode(salt).decode('ascii')}${base64.b64encode(dk).decode('ascii')}"
            force_change = 1
        else:
            pass_hash = f"LEGACY_VB6${pass_hex}"
            force_change = 0

        cur.execute(
            """INSERT INTO Usuarios(
                id_usuario, codigo_id, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
                ape_mat_usuario, id_nivel, mail_usuario, fono_usuario, es_solicitante,
                activo, debe_cambiar_clave, fecha_creacion, fecha_modificacion
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, ?, datetime('now','localtime'), datetime('now','localtime'))""",
            (
                uid,
                code_seq,
                alias_u,
                pass_hash,
                s(get_ci(u, "Nombre_usuario")),
                s(get_ci(u, "Ape_pat_usuario")),
                s(get_ci(u, "ape_mat_usuario")),
                nivel_id,
                s(get_ci(u, "mail_usuario")),
                s(get_ci(u, "fono_usuario")),
                b(get_ci(u, "Solicitante")),
                force_change,
            ),
        )
        code_seq += 1

        cur.execute(
            """INSERT INTO Permisos(
                id_usuario, m1_ot, tab_0_1_ingreso_ot, tab_0_vbj, tab_0_vbs,
                tab_0_2_busqueda_ot, tab_0_3_impresion_ot, m2_facturas,
                tab_1_0_ingreso_fac, tab_1_1_busqueda_fac, m3_mantenedor,
                m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
            (
                uid,
                b(get_ci(perm, "M1")),
                b(get_ci(perm, "tab_0_1")),
                b(get_ci(perm, "tab_0_VBJ")),
                b(get_ci(perm, "tab_0_VBS")),
                b(get_ci(perm, "tab_0_2")),
                b(get_ci(perm, "tab_0_3")),
                b(get_ci(perm, "M2")),
                b(get_ci(perm, "tab_1_0")),
                b(get_ci(perm, "tab_1_1")),
                1 if nivel_id == 4 else b(get_ci(perm, "M3")),
                1 if nivel_id == 4 else 0,
                1 if nivel_id == 4 else 0,
                1 if nivel_id == 4 else 0,
            ),
        )

    # 5. Real Asset Tree from BD MANTENCION.mdb (ACTIVO + N1_N2..N1_N10)
    parent_of = {}
    edge_active = {}
    for lvl in range(2, 11):
        rows = read_csv(dump_dir, f"mant_N1_N{lvl}.csv")
        parent_col = f"ID_N{lvl - 1}"
        child_col = f"ID_N{lvl}"
        for r in rows:
            cid = i(get_ci(r, child_col))
            pid = i(get_ci(r, parent_col), 1)
            est = b(get_ci(r, "ESTADO", "estado", default="1"))
            if cid > 0:
                parent_of[cid] = pid if pid > 0 else 1
                edge_active[cid] = est

    activos_map = {
        1: {
            "id": 1,
            "parent": None,
            "nivel": 1,
            "desc": "CAMBIASO HNOS S.A.C.",
            "activo": 1,
            "extra": ("", "", "", "", "", 0.0, 0.0, "", "Empresa", "Operativa", "", "", 0, "", "", "", "", "", "", ""),
        }
    }

    for r in read_csv(dump_dir, "mant_ACTIVO.csv"):
        aid = i(r["ID"])
        if aid <= 0:
            continue
        lvl = max(1, min(10, i(r["NIVEL"], 2)))
        desc = s(r["DESC_ACTIVO"]) or f"Activo #{aid}"
        estado_op = s(r["ESTADO"])
        is_act = edge_active.get(aid, 0 if estado_op.lower() in ("no operativa", "inactivo", "baja", "false", "0") else 1)

        tech = {}
        for k in range(1, 11):
            dk = s(r.get(f"DESC_INF{k}"))
            vk = s(r.get(f"INF{k}"))
            if dk or vk:
                tech[dk or f"Campo {k}"] = vk
        tech_json = json.dumps(tech, ensure_ascii=False) if tech else ""

        activos_map[aid] = {
            "id": aid,
            "parent": None if aid == 1 else parent_of.get(aid, 1),
            "nivel": lvl,
            "desc": desc,
            "activo": is_act,
            "extra": (
                s(r["COD_SAP"]),
                s(r["FABRICANTE"]),
                s(r["SERIE"]),
                s(r["MODELO"]),
                clean_date(r["FECHA_COMPRA"]),
                f(r["VALOR_COMPRA"]),
                f(r["VALOR_ACTUAL"]),
                clean_date(r["FECHA_INSTALACION"]),
                s(r["TIPO_EQUIPO"]),
                estado_op,
                s(r["VIDA_UTIL"]),
                s(r["OBSERVACION"]),
                i(r["PRIORIDAD"]),
                s(r["MATERIAL"]),
                s(r["CATEGORIA"]),
                s(r["RUTA_CARPETA_PLANO"]),
                s(r["NOMBRE_PLANO"]),
                s(r["RUTA_CARPETA_FOTO"]),
                s(r["NOMBRE_FOTO"]),
                tech_json,
            ),
        }

    # Ensure any referenced parent in OT_PROV that isn't in ACTIVO exists safely
    ots_raw = read_csv(dump_dir, "ot_OT_PROV.csv")
    valid_ots = []
    skipped_ots = 0
    for o in ots_raw:
        id_ot = i(o["id_ot"])
        id_prov = i(o["id_prov"])
        n_ot = i(o["n_ot"])
        fecha = clean_date(o.get("fecha_ot"))
        if id_ot <= 0 or id_prov <= 0 or n_ot <= 0 or not fecha or id_prov not in valid_provs:
            skipped_ots += 1
            continue
        valid_ots.append(o)
        chain = [i(o.get(f"id_n{k}")) for k in range(1, 11)]
        parent = 1
        for lvl in range(2, 11):
            nid = chain[lvl - 1]
            if nid <= 0:
                break
            if nid not in activos_map:
                activos_map[nid] = {
                    "id": nid,
                    "parent": parent,
                    "nivel": lvl,
                    "desc": f"Activo Histórico #{nid}",
                    "activo": 0,
                    "extra": ("", "", "", "", "", 0.0, 0.0, "", "", "Histórico", "", "", 0, "", "", "", "", "", "", ""),
                }
            parent = nid

    # Guarantee every parent_id points to an existing id_activo
    for aid, info in activos_map.items():
        if aid != 1 and (info["parent"] is None or info["parent"] not in activos_map or info["parent"] == aid):
            info["parent"] = 1

    # Insert Activos sorted by nivel then id
    activo_rows = []
    for aid, info in sorted(activos_map.items(), key=lambda kv: (kv[1]["nivel"], kv[0])):
        activo_rows.append((aid, info["parent"], info["nivel"], info["desc"], info["activo"], *info["extra"]))

    cur.executemany(
        """INSERT INTO Activos(
            id_activo, parent_id, nivel, descripcion, activo,
            cod_sap, fabricante, serie, modelo, fecha_compra, valor_compra, valor_actual,
            fecha_instalacion, tipo_equipo, estado_operativo, vida_util, observacion,
            prioridad, material, categoria, ruta_plano, nombre_plano, ruta_foto, nombre_foto, info_tecnica_json
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        activo_rows,
    )

    def build_path(chain):
        parts = []
        for nid in chain:
            if nid > 0 and nid in activos_map:
                parts.append(activos_map[nid]["desc"])
        return " \\ ".join(parts)

    # 6. OrdenesTrabajo (ALL 7,284 valid real OTs with real Cambiaso asset paths!)
    migrated_ot_ids = set()
    ot_rows = []
    for o in valid_ots:
        id_ot = i(o["id_ot"])
        migrated_ot_ids.add(id_ot)
        id_sol_raw = i(o.get("id_sol"))
        id_sol = id_sol_raw if id_sol_raw in valid_sols else None
        chain = [i(o.get(f"id_n{k}")) for k in range(1, 11)]
        if chain[0] == 0:
            chain[0] = 1
        ruta = build_path(chain)
        id_nivel = max(1, min(10, i(o.get("id_nivel"), 1)))
        ot_rows.append((
            id_ot,
            i(o["n_ot"]),
            i(o["id_prov"]),
            id_sol,
            clean_date(o["fecha_ot"]),
            i(o.get("activo")),
            i(o.get("guia_des_ot")),
            *chain,
            id_nivel,
            ruta,
        ))

    cur.executemany(
        """INSERT INTO OrdenesTrabajo(
            id_ot, n_ot, id_prov, id_sol, fecha_ot, estado, guia_des_ot,
            id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
            id_nivel, ruta_activo
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        ot_rows,
    )

    # 7. DetalleOT (ALL 7,803 detail rows)
    det_rows = []
    for d in read_csv(dump_dir, "ot_Detalle_ot.csv"):
        id_ot = i(d.get("id_ot"))
        if id_ot not in migrated_ot_ids:
            continue
        cant = max(1, i(d.get("cantidad_det"), 1))
        det_text = s(d.get("detalle_ot")) or "(Sin detalle)"
        det_rows.append((i(d["Id_Det"]), id_ot, cant, det_text, b(d.get("Estado_det"))))
    cur.executemany(
        "INSERT INTO DetalleOT(id_det, id_ot, cantidad_det, detalle_ot, estado_det) VALUES (?, ?, ?, ?, ?)",
        det_rows,
    )

    # =====================================================================
    # 8. IMPORTACIÓN COMPLETA DE TABLAS DE BD MANTENCION.mdb & PERSONAL_MANTENCION.mdb
    # =====================================================================
    mant_counts = {}

    rows = read_csv(dump_dir, "mant_OFICIOS.csv")
    cur.executemany(
        "INSERT OR REPLACE INTO Mant_Oficios(id_oficio, desc_oficio) VALUES (?, ?)",
        [(i(r["ID_OFICIO"]), s(r["DESC_OFICIO"])) for r in rows],
    )
    mant_counts["Mant_Oficios"] = len(rows)

    rows = read_csv(dump_dir, "mant_MOTIVO.csv")
    cur.executemany(
        "INSERT OR REPLACE INTO Mant_Motivos(id_motivo, desc_motivo, considerado) VALUES (?, ?, ?)",
        [(i(r["ID_MOTIVO"]), s(r["DESC_MOTIVO"]), b(r["CONSIDERADO"])) for r in rows],
    )
    mant_counts["Mant_Motivos"] = len(rows)

    rows = read_csv(dump_dir, "mant_EMPRESA.csv")
    cur.executemany(
        "INSERT OR REPLACE INTO Mant_Empresas(id_empresa, desc_empresa, direccion, telefono, email, oficio, fax) VALUES (?, ?, ?, ?, ?, ?, ?)",
        [(i(r["ID_EMPRESA"]), s(r["DESC_EMPRESA"]), s(r["DIRECCION"]), s(r["TELEFONO"]), s(r["E-MAIL"]), s(r["OFICIO"]), s(r["FAX"])) for r in rows],
    )
    mant_counts["Mant_Empresas"] = len(rows)

    rows = read_csv(dump_dir, "mant_DESC_INFORMES.csv")
    cur.executemany(
        "INSERT OR REPLACE INTO Mant_DescInformes(id_desc, desc_inf) VALUES (?, ?)",
        [(i(r["ID_DESC"]), s(r["DESC_INF"])) for r in rows],
    )
    mant_counts["Mant_DescInformes"] = len(rows)

    rows = read_csv(dump_dir, "mant_PERSONAL.csv")
    cur.executemany(
        """INSERT OR REPLACE INTO Mant_Personal(
            id_personal, cod_personal, rut_personal, nombres, apellidos, desc_personal,
            oficio, email, celular, fono, direccion, fecha_ingreso, departamento, centro_trabajo
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        [(
            i(r["ID_PERSONAL"]), s(r["COD_PERSONAL"]), s(r["RUT_PERSONAL"]), s(r["NOMBRES"]),
            s(r["APELLIDOS"]), s(r["DESC_PERSONAL"]), s(r["OFICIO"]), s(r["EMAIL"]),
            s(r["CELULAR"]), s(r["FONO"]), s(r["DIRECCION"]), clean_date(r["FECHA_INGRESO"]),
            s(r["DEPARTAMENTO"]), s(r["CENTRO_TRABAJO"]),
        ) for r in rows],
    )
    mant_counts["Mant_Personal"] = len(rows)

    rows = read_csv(dump_dir, "rrhh_PERSONAL.csv")
    cur.executemany(
        """INSERT OR REPLACE INTO Mant_PersonalRRHH(
            id_empleado, fec_alta_empleado, nombre, apellido_p, apellido_m,
            n_centro_costo, n_puesto, n_departamento, n_centro_trabajo
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        [(
            s(r["ID_EMPLEADO"]), clean_date(r["FEC_ALTA_EMPLEADO"]), s(r["NOMBRE"]),
            s(r["APELLIDO_P"]), s(r["APELLIDO_M"]), s(r["N_CENTRO_COSTO"]),
            s(r["N_PUESTO"]), s(r["N_DEPARTAMENTO"]), s(r["N_CENTRO_TRABAJO"]),
        ) for r in rows if s(r.get("ID_EMPLEADO"))],
    )
    mant_counts["Mant_PersonalRRHH"] = len(rows)

    rows = read_csv(dump_dir, "mant_ROPA.csv")
    cur.executemany(
        "INSERT INTO Mant_Ropa(id_personal, id_item, descripcion, talla, color, cantidad) VALUES (?, ?, ?, ?, ?, ?)",
        [(i(r["ID_PERSONAL"]), i(r["ID"]), s(r["DESCRIPCION"]), s(r["TALLA"]), s(r["COLOR"]), i(r["CANTIDAD"])) for r in rows],
    )
    mant_counts["Mant_Ropa"] = len(rows)

    rows = read_csv(dump_dir, "mant_INSUMOS2.csv")
    cur.executemany(
        """INSERT OR REPLACE INTO Mant_Insumos(
            id_item, cod_sap, cod_proveedor, desc_item, stock_unidad, dias_entrega, prioridad_item
        ) VALUES (?, ?, ?, ?, ?, ?, ?)""",
        [(i(r["ID_ITEM"]), s(r["COD_SAP"]), s(r["COD_PROVEEDOR"]), s(r["DESC_ITEM"]), f(r["D_STOCK_UNIDAD"]), i(r["DIAS_ENTREGA"]), i(r["PRIORIDAD_ITEM"])) for r in rows],
    )
    mant_counts["Mant_Insumos"] = len(rows)

    rows = read_csv(dump_dir, "mant_INSUMOS_UTILIZADOS.csv")
    cur.executemany(
        "INSERT INTO Mant_InsumosUtilizados(id_ref, id_item, origen, id_ciclo, cantidad, costo_unidad) VALUES (?, ?, ?, ?, ?, ?)",
        [(i(r["ID"]), i(r["ID_ITEM"]), s(r["ORIGEN"]), i(r["ID_CICLO"]), f(r["CANTIDAD"]), f(r["COSTO_UNIDAD"])) for r in rows],
    )
    mant_counts["Mant_InsumosUtilizados"] = len(rows)

    rows = read_csv(dump_dir, "mant_OT.csv")
    cur.executemany(
        """INSERT OR REPLACE INTO Mant_OT(
            id, n_ot, fecha_recepcion_ot, fechai, fechaf, fecha_pm, turno, id_motivo,
            detencion, oficio_ot, id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
            nivel, solicitada_por, recibido_por, entregado_a, descripcion_breve, descripcion_tecnica, estado_ot, prioridad_ot
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        [(
            i(r["ID"]), s(r["N_OT"]), clean_date(r["FECHA_RECEPCION_OT"]), clean_date(r["FECHAI"]),
            clean_date(r["FECHAF"]), clean_date(r["FECHA_PM"]), s(r["TURNO"]), i(r["ID_MOTIVO"]),
            s(r["DETENCION"]), s(r["OFICIO_OT"]),
            i(r["ID_N1"], 1), i(r["ID_N2"]), i(r["ID_N3"]), i(r["ID_N4"]), i(r["ID_N5"]),
            i(r["ID_N6"]), i(r["ID_N7"]), i(r["ID_N8"]), i(r["ID_N9"]), i(r["ID_N10"]),
            i(r["NIVEL"], 1), s(r["SOLICITADA_POR"]), s(r["RECIBIDO_POR"]), s(r["ENTREGADO_A"]),
            s(r["DESCRIPCION_BREVE"]), s(r["DESCRIPCION_TECNICA"]), s(r["ESTADO_OT"]), i(r["PRIORIDAD_OT"]),
        ) for r in rows],
    )
    mant_counts["Mant_OT"] = len(rows)

    rows = read_csv(dump_dir, "mant_OT_EXTERNA.csv")
    cur.executemany(
        "INSERT OR REPLACE INTO Mant_OTExterna(id_ote, n_ote, fecha_pedido, fecha_entrega, id_empresa, descripcion_ote, costo, garantia) VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
        [(i(r["ID_OTE"]), i(r["N_OTE"]), clean_date(r["FECHA_PEDIDO"]), clean_date(r["FECHA_ENTREGA"]), i(r["ID_EMPRESA"]), s(r["DESCRIPCION_OTE"]), f(r["COSTO"]), s(r["GARANTIA"])) for r in rows],
    )
    mant_counts["Mant_OTExterna"] = len(rows)

    rows = read_csv(dump_dir, "mant_OT_EXTERNA_MP.csv")
    cur.executemany(
        "INSERT INTO Mant_OTExternaMP(id_mp, id_ote, id_ciclo) VALUES (?, ?, ?)",
        [(i(r["ID"]), i(r["ID_OTE"]), i(r["ID_CICLO"])) for r in rows],
    )
    mant_counts["Mant_OTExternaMP"] = len(rows)

    rows = read_csv(dump_dir, "mant_MP.csv")
    cur.executemany(
        """INSERT OR REPLACE INTO Mant_MP(
            id, id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
            nivel, fechai_mp, fecha_cierremp, descripcionmp, estadomp, prioridad,
            ufrecuenciamp, frecuenciamp, diasmp, tiempo_previo_aviso, tipo_mant
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        [(
            i(r["ID"]),
            i(r["ID_N1"], 1), i(r["ID_N2"]), i(r["ID_N3"]), i(r["ID_N4"]), i(r["ID_N5"]),
            i(r["ID_N6"]), i(r["ID_N7"]), i(r["ID_N8"]), i(r["ID_N9"]), i(r["ID_N10"]),
            i(r["NIVEL"], 1), clean_date(r["FECHAI_MP"]), clean_date(r["FECHA_CIERREMP"]),
            s(r["DESCRIPCIONMP"]), s(r["ESTADOMP"]), i(r["PRIORIDAD"]),
            s(r["UFRECUENCIAMP"]), i(r["FRECUENCIAMP"]), i(r["DIASMP"]), i(r["TIEMPO_PREVIO_AVISO"]), s(r["TIPO_MANT"]),
        ) for r in rows],
    )
    mant_counts["Mant_MP"] = len(rows)

    rows = read_csv(dump_dir, "mant_CICLOMP.csv")
    cur.executemany(
        """INSERT INTO Mant_CicloMP(
            id_mp, id_ciclo, fecha_inicio_ciclo, fecha_cierre_ciclo, tiempo, observacion_ciclo_mp, estado_ciclo
        ) VALUES (?, ?, ?, ?, ?, ?, ?)""",
        [(i(r["ID"]), i(r["ID_CICLO"]), clean_date(r["FECHA_INICIO_CICLO"]), clean_date(r["FECHA_CIERRE_CICLO"]), f(r["TIEMPO"]), s(r["OBSERVACION_CICLO_MP"]), s(r["ESTADO_CICLO"])) for r in rows],
    )
    mant_counts["Mant_CicloMP"] = len(rows)

    # Stream TAREAS_MP (208,121 rows) in chunks for minimal memory usage
    tareas_path = os.path.join(dump_dir, "mant_TAREAS_MP.csv")
    tareas_count = 0
    if os.path.exists(tareas_path):
        with open(tareas_path, "r", encoding="utf-8-sig", newline="") as fh:
            reader = csv.DictReader(fh)
            batch = []
            for r in reader:
                batch.append((
                    i(r["ID"]), i(r["ID_TAREA"]), i(r["ID_CICLO"]),
                    i(r["ID_N1"], 1), i(r["ID_N2"]), i(r["ID_N3"]), i(r["ID_N4"]), i(r["ID_N5"]),
                    i(r["ID_N6"]), i(r["ID_N7"]), i(r["ID_N8"]), i(r["ID_N9"]), i(r["ID_N10"]),
                    s(r["DESCRIPCION_TAREA"]), s(r["OBSERVACION_TAREA_CICLO"]), s(r["ESTADO"]), f(r["DURACION"]), s(r["OFICIO"]),
                ))
                if len(batch) >= 10000:
                    cur.executemany(
                        """INSERT INTO Mant_TareasMP(
                            id_mp, id_tarea, id_ciclo, id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
                            descripcion_tarea, observacion_tarea_ciclo, estado, duracion, oficio
                        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                        batch,
                    )
                    tareas_count += len(batch)
                    batch.clear()
            if batch:
                cur.executemany(
                    """INSERT INTO Mant_TareasMP(
                        id_mp, id_tarea, id_ciclo, id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10,
                        descripcion_tarea, observacion_tarea_ciclo, estado, duracion, oficio
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
                    batch,
                )
                tareas_count += len(batch)
    mant_counts["Mant_TareasMP"] = tareas_count

    pers_util_path = os.path.join(dump_dir, "mant_PERSONAL_UTILIZADO.csv")
    pu_count = 0
    if os.path.exists(pers_util_path):
        with open(pers_util_path, "r", encoding="utf-8-sig", newline="") as fh:
            reader = csv.DictReader(fh)
            batch = []
            for r in reader:
                batch.append((i(r["ID"]), i(r["ID_PERSONAL"]), s(r["ORIGEN"]), i(r["ID_CICLO"]), i(r["LUGAR"]), s(r["OBSERVACION"])))
                if len(batch) >= 10000:
                    cur.executemany(
                        "INSERT INTO Mant_PersonalUtilizado(id_ref, id_personal, origen, id_ciclo, lugar, observacion) VALUES (?, ?, ?, ?, ?, ?)",
                        batch,
                    )
                    pu_count += len(batch)
                    batch.clear()
            if batch:
                cur.executemany(
                    "INSERT INTO Mant_PersonalUtilizado(id_ref, id_personal, origen, id_ciclo, lugar, observacion) VALUES (?, ?, ?, ?, ?, ?)",
                    batch,
                )
                pu_count += len(batch)
    mant_counts["Mant_PersonalUtilizado"] = pu_count

    rows = read_csv(dump_dir, "mant_CHECKLIST.csv")
    cur.executemany(
        """INSERT OR REPLACE INTO Mant_Checklist(
            id_chk, fecha_inicio, fecha_termino, duracion, id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, observacion
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)""",
        [(i(r["ID_CHK"]), clean_date(r["FECHA_INICIO"]), clean_date(r["FECHA_TERMINO"]), f(r["DURACION"]), i(r["ID_N1"], 1), i(r["ID_N2"]), i(r["ID_N3"]), i(r["ID_N4"]), i(r["ID_N5"]), i(r["ID_N6"]), i(r["ID_N7"]), s(r["OBSERVACION"])) for r in rows],
    )
    mant_counts["Mant_Checklist"] = len(rows)

    rows = read_csv(dump_dir, "mant_TAREAS_CHECKLIST.csv")
    cur.executemany(
        "INSERT OR REPLACE INTO Mant_TareasChecklist(id_tarea, descripcion, grupo) VALUES (?, ?, ?)",
        [(i(r["ID_TAREA"]), s(r["DESCRIPCION"]), s(r["GRUPO"])) for r in rows],
    )
    mant_counts["Mant_TareasChecklist"] = len(rows)

    chk_rows = []
    for r in read_csv(dump_dir, "mant_CHK_TAREAS_PENDIENTES.csv"):
        chk_rows.append((i(r["INDICE_CHK"]), i(r["ID_CHK"]), i(r["ID_TAREA"]), "Pendiente", s(r["OBSERVACION"])))
    for r in read_csv(dump_dir, "mant_CHK_TAREAS_REALIZADAS.csv"):
        chk_rows.append((i(r["INDICE_CHK"]), i(r["ID_CHK"]), i(r["ID_TAREA"]), "Realizada", s(r["OBSERVACION"])))
    cur.executemany(
        "INSERT INTO Mant_ChkTareas(indice_chk, id_chk, id_tarea, estado, observacion) VALUES (?, ?, ?, ?, ?)",
        chk_rows,
    )
    mant_counts["Mant_ChkTareas"] = len(chk_rows)

    rows = read_csv(dump_dir, "mant_PERSONAL_CHECKLIST.csv")
    cur.executemany(
        "INSERT OR IGNORE INTO Mant_PersonalChecklist(id_checklist, id_personal) VALUES (?, ?)",
        [(i(r["ID_CHECKLIST"]), i(r["ID_PERSONAL"])) for r in rows],
    )
    mant_counts["Mant_PersonalChecklist"] = len(rows)

    # Legacy maintenance users (strictly without plaintext passwords!)
    mant_perms = {s(r["ID_USUARIO"]): r for r in read_csv(dump_dir, "mant_PERMISOS_USUARIO.csv")}
    mu_rows = read_csv(dump_dir, "mant_USUARIOS.csv")
    for r in mu_rows:
        uid = i(r["ID_USUARIO"])
        pjson = json.dumps(mant_perms.get(str(uid), {}), ensure_ascii=False)
        cur.execute(
            "INSERT OR REPLACE INTO Mant_UsuariosLegacy(id_usuario, desc_usuario, permisos_json) VALUES (?, ?, ?)",
            (uid, s(r["DESC_USUARIO"]), pjson),
        )
    mant_counts["Mant_UsuariosLegacy"] = len(mu_rows)

    # Record complete migration audit entry
    cur.execute(
        """INSERT INTO AuditLog(usuario, modulo, accion, detalle)
           VALUES ('SISTEMA', 'Migración', 'MIGRACION_INTEGRAL', ?)""",
        (
            f"Importación unificada OT_PROV.mdb + BD MANTENCION.mdb + PERSONAL_MANTENCION.mdb: "
            f"{len(valid_provs)} proveedores, {len(valid_sols)} solicitantes, {len(migrated_ot_ids)} OTs externas "
            f"({skipped_ots} corruptas omitidas), {len(det_rows)} detalles OT, {len(activos_map)} activos reales, "
            f"{mant_counts['Mant_OT']} OTs mantención, {mant_counts['Mant_MP']} planes MP, "
            f"{mant_counts['Mant_TareasMP']} tareas MP, {mant_counts['Mant_Insumos']} insumos, {mant_counts['Mant_Personal']} técnicos.",
        ),
    )

    conn.commit()
    cur.execute("PRAGMA foreign_keys = ON;")
    cur.execute("PRAGMA foreign_key_check;")
    fk_issues = cur.fetchall()
    cur.execute("PRAGMA integrity_check;")
    integrity = cur.fetchone()[0]
    cur.execute("PRAGMA wal_checkpoint(TRUNCATE);")
    cur.execute("VACUUM;")
    cur.execute("ANALYZE;")
    conn.close()

    size_mb = os.path.getsize(db_path) / (1024 * 1024)
    print(f"SQLite Integrity Check: {integrity}")
    print(f"Foreign Key Violations: {len(fk_issues)}")
    print(f"Database Size: {size_mb:.2f} MB ({db_path})")
    print(
        f"SISPRO Core => Niveles: {len(niveles)}, Proveedores: {len(valid_provs)}, "
        f"Solicitantes: {len(valid_sols)}, Usuarios: {len(users_raw)}, "
        f"Activos: {len(activos_map)}, OTs: {len(migrated_ot_ids)} (skipped {skipped_ots}), Detalles: {len(det_rows)}"
    )
    print("BD Mantencion => " + ", ".join(f"{k}: {v}" for k, v in mant_counts.items()))
    if integrity != "ok" or len(fk_issues) > 0:
        print("FK ISSUES:", fk_issues[:10])
        sys.exit(1)


if __name__ == "__main__":
    main()
