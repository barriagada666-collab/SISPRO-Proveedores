# Reporte Formal de Migración y Validación — `OT_PROV.mdb` (y Bases Complementarias)

> **Proyecto:** SISPRO — Sistema de Gestión de Proveedores y Órdenes de Trabajo (Cambiaso Hnos S.A.C.)  
> **Motor Destino:** SQLite 3 (WAL, `PRAGMA foreign_keys = ON`) — `data/sispro.db`  
> **Fecha de Verificación:** 23-11-2026  
> **Regla de Preservación:** Los archivos `.mdb` originales permanecen intactos en solo lectura (`Mode=Read`).

---

## 1. Huellas SHA-256 de Origen (Verificación de No Alteración)

| Archivo de Origen | Tamaño | SHA-256 | Estado |
|---|---:|---|---|
| `OT_PROV.mdb` | 5.935.104 bytes | `8748F95712DD2DAA5E3ECA51A4146F6956FF4F39DCAFD593A37750D0C607A379` | Intacto (Solo lectura) |
| `BD MANTENCION.mdb` | 87.949.312 bytes | `CCA7EB0D3F720DD08EF0A395359F197F6FCC101ACCAA9B1DEDD5C554BF2B6605` | Intacto (Solo lectura) |
| `PERSONAL_MANTENCION.mdb` | 217.088 bytes | `2F0C2D43131759DE6EA1862441CF7A89B4922CDD0A949FED5DC5F6CDEDDE8482` | Intacto (Solo lectura) |

---

## 2. Reconciliación Tabla por Tabla — `OT_PROV.mdb` (100% Contabilizado)

| Tabla Access (Origen) | Tabla SQLite (Destino) | Registros Originales | Registros Importados | Registros Rechazados | Duplicados Detectados | Errores | Resultado de Validación |
|---|---|---:|---:|---:|---:|---:|---|
| `OT_PROV` | `OrdenesTrabajo` | 7.285 | 7.284 | 1 *(nota 1)* | 27 grupos `n_ot` (30 filas extra preservadas) | 0 | **APROBADO (100% contabilizado)** |
| `Detalle_ot` | `DetalleOT` | 7.803 | 7.803 | 0 | 0 | 0 | **APROBADO (100%)** |
| `Proveedores` | `Proveedores` | 214 | 214 | 0 | 1 nombre duplicado *(nota 2)* | 0 | **APROBADO (100%)** |
| `Solicitante` | `Solicitantes` | 9 *(+2 rescatados)* | 11 *(nota 3)* | 0 | 0 | 0 | **APROBADO (100% + rescate histórico)** |
| `Nivel` | `Niveles` | 4 | 4 | 0 | 0 | 0 | **APROBADO (100%)** |
| `Usuarios` | `Usuarios` | 3 *(+1 admin inicial)* | 4 *(nota 4)* | 0 | 0 | 0 | **APROBADO (100% + provisión segura)** |
| `Permisos` | `Permisos` | 3 *(+1 admin inicial)* | 4 | 0 | 0 | 0 | **APROBADO (100%)** |
| `Facturas` | `Facturas` | 0 | 0 | 0 | 0 | 0 | **APROBADO (estructura lista)** |
| `Facturas_loc` | `FacturaDetalles` | 0 | 0 | 0 | 0 | 0 | **APROBADO (reemplazo transaccional)** |
| `VB` | `VistoBueno` | 0 | 0 | 0 | 0 | 0 | **APROBADO (estructura lista)** |
| `Errores de pegado` | *(Excluida)* | 0 | 0 | 0 | 0 | 0 | **DESCARTADA (artefacto vacío de Access)** |
| **TOTAL `OT_PROV.mdb`** | — | **15.321** | **15.320 (+3 nuevos)** | **1** | **28** | **0** | **100% CONTABILIZADO** |

### Notas de Trazabilidad e Integridad

1. **Registro corrupto rechazado en `OT_PROV` (`id_ot = 0`):**  
   Existe exactamente 1 fila corrupta en origen con valores `id_ot=0 | id_prov=0 | n_ot=0 | fecha_ot=NULL | activo=0 | guia_des_ot=0 | id_n1..id_n10=0 | id_nivel=0 | id_sol=7`. Al carecer de número de orden, proveedor y fecha, fue rechazada explícitamente para mantener la integridad referencial.  
   Adicionalmente, la OT `id_ot=68` (`n_ot=2514`, fecha `04-02-2011`) contenía `id_sol=0` en Access; se normalizó a `id_sol = NULL` en SQLite sin perder el registro ni sus detalles.  
   Rango cronológico validado en `OrdenesTrabajo`: **`29-12-2010` a `23-11-2026`** (7.284 registros).
2. **Desambiguación de proveedor duplicado (`Proveedores`):**  
   `BATERIAS TUBULAR S.A` existía en dos registros con IDs `140` y `191`. Para satisfacer `UNIQUE COLLATE NOCASE` sin fusionar destructivamente sus historiales de OTs, el registro `191` se preservó como `BATERIAS TUBULAR S.A (2)`.
3. **Rescate de solicitantes históricos eliminados (`Solicitantes`):**  
   1.506 OTs históricas referenciaban a `id_sol = 5` (1.488 OTs) y `id_sol = 6` (18 OTs), eliminados de la tabla activa `Solicitante` en `OT_PROV.mdb`. Se recuperaron sus identidades reales desde la copia histórica de respaldo (`5: J Maureira` y `6: L Fuentes`) y se insertaron marcados como `activo = 0`, logrando **0 referencias huérfanas** (`PRAGMA foreign_key_check = 0`).
4. **Migración segura de credenciales (`Usuarios`):**  
   Los 3 usuarios legados (`L Reyes`, `L Reyes M`, `BORIS`) fueron migrados preservando sus `id_usuario` y permisos. La cuenta `BORIS`, cuya contraseña existía en texto plano en Access, fue recifrada automáticamente con **PBKDF2-HMAC-SHA256 (100.000 iteraciones)** y marcada con `debe_cambiar_clave = 1`. Además se aprovisionó idempotentemente el administrador inicial `barriagada` (`USR-0004`, `codigo_id = 4`, `debe_cambiar_clave = 1`).

---

## 3. Análisis de Lógica, Relaciones y Consultas Guardadas en `OT_PROV.mdb`

| Objeto Access | Tipo | Definición / Regla en Access | Adaptación en SISPRO .NET 10 |
|---|---|---|---|
| `Detalle_otOT_PROV` | FK | `Detalle_ot.id_ot -> OT_PROV.id_ot` | `FOREIGN KEY(id_ot) REFERENCES OrdenesTrabajo(id_ot) ON DELETE CASCADE` |
| `ProveedoresOT_PROV` | FK | `OT_PROV.id_prov -> Proveedores.id_prov` | `FOREIGN KEY(id_prov) REFERENCES Proveedores(id_prov)` |
| `SolicitanteOT_PROV` | FK | `OT_PROV.id_sol -> Solicitante.id_sol` | `FOREIGN KEY(id_sol) REFERENCES Solicitantes(id_sol)` |
| `OT_PROVNivel` | FK | `OT_PROV.id_nivel -> Nivel.id_nivel` | `id_nivel` preservado + `Niveles` normalizado |
| `Permisos Consulta` | Vista | Selección de 13 banderas de permisos | Integrado en `UserRepository.SelectBase` vía `LEFT JOIN Permisos` |
| `Proveedores Consulta` | Vista | Listado `nom_prov, id_prov` | `ProviderRepository.GetAll` con búsqueda y ordenación |
| `Solicitante Consulta` | Vista | Listado `alias` | `RequesterRepository.GetAll` |
| `OT_PROV Consulta` | Query | Join `OT_PROV` + `Proveedores` con filtro fijo `01-01-2011`..`28-02-2011` | Reemplazado por `WorkOrderRepository.Search` con **Date Pickers dinámicos** y `ORDER BY fecha_ot DESC, id_ot DESC` |
| `Usuarios Consulta` | Query | `alias_usuario, pass_usuario, Nivel` | Reemplazado por `AuthService` (verificación PBKDF2 sin exposición de contraseñas) |

> **Tablas vinculadas, macros y formularios en `OT_PROV.mdb`:** No existen tablas vinculadas externas (`LINK`), macros ni módulos VBA activos en `OT_PROV.mdb` (el archivo operaba exclusivamente como backend de datos del aplicativo heredado VB.NET).

---

## 4. Reconciliación Complementaria (`BD MANTENCION.mdb` y `PERSONAL_MANTENCION.mdb`)

| Base Origen | Tablas Migradas | Registros en SQLite | Observaciones |
|---|---:|---:|---|
| `BD MANTENCION.mdb` | 21 (`Activos`, `Mant_OT`, `Mant_MP`, `Mant_CicloMP`, `Mant_TareasMP`, `Mant_Insumos`, `Mant_Personal`, etc.) | 328.081 | 1.824 activos reales (10 niveles + ficha técnica), 43.989 OTs internas (5 IDs duplicados deduplicados), 208.121 tareas MP, 58.847 asignaciones de personal. |
| `PERSONAL_MANTENCION.mdb` | 1 (`Mant_PersonalRRHH`) | 43 | Dotación RRHH completa con centro de costo y puesto. |
| **Total Global en `data/sispro.db`** | **33 tablas base (+2 en v4.0.0: `Grupos`, `MovimientosGrupo`)** | **343.451 registros** | **`PRAGMA integrity_check = ok`**, **`PRAGMA foreign_key_check = 0`**. |
