# SISPRO v4.5.0 — Sistema de Gestión de Proveedores, Trabajos Externos y Solicitantes

> **Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)**  
> Aplicación de escritorio corporativa desarrollada en **.NET 10 (`C#` WinForms · `net10.0-windows10.0.19041.0`)** sobre **SQLite 3 (WAL)**, con gestión integral de Trabajos Externos (OT) con **Observación**, **Valor Neto (CLP)** destacado, **Guardar y Exportar / Vista Previa directa**, **visualización de OT con clic derecho**, selector de **Firmas al Pie (`Jefatura`, `Técnico`, `Supervisor`) desde Personal y Usuarios**, **búsqueda/exportación masiva exclusiva de OTs seleccionadas** (`.xlsx`, `.csv`, `.pdf`), **Importación y Comparación Access (`.mdb`) -> SQLite**, **Solicitado Por** con **Firma Digital** (`PNG`, `JPG`, `PDF`), gestión masiva e integridad referencial en los 4 Mantenedores, **Gestión Dinámica de Perfiles de Usuarios**, Usuarios y Grupos e instalador **Inno Setup 7**.

---

## Características Principales

- **Panel de Control y Trabajos Externos en Primer Plano:** Banner Hero principal con el botón dorado **Nueva OT Externa**, **5 KPIs clicables** (**OTs Externas**, **Proveedores**, **Activos Reales**, **OTs Año 2026**, **Solicitantes / Firmas**), 3 gráficos vectoriales High-DPI y tabla reciente con columnas **Valor Neto** y **Firmas**, doble clic para editar y menú contextual de **clic derecho** con **Visualizar OT (Vista Previa)**.
- **Órdenes de Trabajo Externas (`M1`) + Búsqueda y Exportación Masiva:** Acceso rápido **Nueva OT Externa**, botón **Exportar / Imprimir OT**, **Guardar y Exportar** habilitado tanto para nuevas OTs como editadas, botón **Vista Previa** en `Ingreso / Edición de OT`, árbol industrial de 10 niveles, **Observación de la OT**, **Valor Neto ($)** destacado (`15pt` Bold, `>= 0`) y **casillas + listas desplegables de firmantes al pie (`Jefatura`, `Técnico`, `Supervisor`) desde Personal y Usuarios (`Solicitantes` + `Usuarios` + `Mant_Personal`)** persistidas por orden. En **Búsqueda de OT**, casillas por fila, **"Seleccionar todos (página visible)"**, exportación exclusiva en `.xlsx`, `.csv` y `.pdf` y **clic derecho → Visualizar OT (Vista Previa)**.
- **Gestión de "Solicitado Por" y Pestaña Dedicada de Firmas Digitales (`PNG`, `JPG`, `PDF`):** Subpestaña `Solicitados Por y Firmas Digitales` en **Usuarios y Solicitantes**, más pestañas **Solicitantes** y **Firmas** (`SignatureManagerControl` con vista previa, checkboxes, consulta de OTs y reasignación/desactivación/eliminación controlada por perfil Administrador) en **Mantenedores**. Archivos `PDF` se rasterizan automáticamente en su página 1 vía `Windows.Data.Pdf.PdfDocument` sin dependencias vulnerables.
- **Mantenedores con Checkboxes, Integridad y Reasignaciones (`M2`):** Los 4 mantenedores (**Proveedores**, **Solicitantes**, **Firmas**, **Árbol de Activos**) incorporan selección múltiple (`GridCheckboxSelectionController<T>` / `AssetTreePanel`), **Consultar OT**, **Reasignar Rel.**, **Desactivar Sel.** y **Eliminar** con `MaintainerDependenciesDialog` (reasignación de proveedor/solicitante/firma/activo/subnodos, preservación de historial, y eliminación explícita de OTs asociadas con doble confirmación restringida a Administradores en UI y backend).
- **Editor Especial del Árbol de Activos (Exclusivo Administrador):** Acceso dedicado en el menú lateral (**Editor de Árbol**) y menús de clic derecho sobre `AssetTreePanel` con checkboxes, **Consultar OT asociadas**, **Reasignar Rel.**, **Crear hijos**, **Editar ficha técnica y tipo estructural**, **Mover subárboles** (límite estricto 10 niveles y prevención de ciclos), **Activar / Desactivar**, **Normalizar Niveles** y **Exportar Rama** (`.xlsx`, `.pdf`, `.docx`, `.csv`). Protegido por RBAC estricto en BD (`AssetTreeService`) y auditoría de accesos denegados.
- **Menús Contextuales Universales de Clic Derecho (`ContextMenuHelper`):** Disponibles en Árbol de Activos, Trabajos Externos, Mantenedores (Proveedores/Solicitantes), Gestión de Usuarios y Grupos (grilla y grupos) y Panel de Control.
- **Mantenedores (`M2`):** Administración integral de **Proveedores**, **Solicitantes y Firmas** y consulta técnica del **Árbol de Activos** (edición restringida exclusivamente al perfil Administrador).
- **Gestión Dinámica de Perfiles, Usuarios y Grupos (Exclusivo Administrador):** Nueva pestaña **Perfiles de Usuarios** (`UserProfileManagerControl`) para crear, editar, activar/desactivar y eliminar perfiles personalizados con permisos por defecto; pestaña **Usuarios y Grupos** con panel de grupos Drag & Drop, grilla holgada de 12 columnas, CheckBoxes y operaciones por lotes (`Activar`, `Desactivar`, `Eliminar` lógico, `Mover`, `Cambiar Perfil`, `Exportar`, `Imprimir`, `Refrescar`).
- **Sincronización Inmediata y Concurrencia:** Bus reactivo post-commit (`SisproSyncHub`), detección entre sesiones vía `PRAGMA data_version` (cada 2,5 s) sin resetear selecciones activas, y concurrencia optimista (`row_version`).
- **Seguridad Empresarial:** Autenticación PBKDF2-HMAC-SHA256 (100.000 iteraciones), cambio obligatorio en primer acceso, bloqueo por intentos fallidos, RBAC estricto en capa de servicios y BD, consultas SQL 100% parametrizadas y bitácora de auditoría exportable.

---

## Tecnologías y Arquitectura

| Capa | Proyecto | Tecnología | Responsabilidad |
|---|---|---|---|
| Dominio | `src/Sispro.Core` | `.NET 10` (`net10.0`) | Entidades (`UserProfile`, `WorkOrder` con firmantes y neto/observación), DTOs (`SignaturePersonOption`, `WorkOrderSearchRow`), Enums (`SignatureOption`, `UserRoleLevel`), PBKDF2, presets RBAC y validadores. |
| Persistencia | `src/Sispro.Data` | `Microsoft.Data.Sqlite 10.0.15` + `SQLitePCLRaw.lib.e_sqlite3 2.1.14` | Esquema idempotente WAL, migración automática de `Solicitantes`, `Niveles` (perfiles) y `OrdenesTrabajo` (`observacion`, `valor_neto`, `firmas_mask`, `firma_jefatura`, `firma_tecnico`, `firma_supervisor`), y 8 repositorios parametrizados. |
| Negocio y Reportes | `src/Sispro.Services` | `ClosedXML 0.105.1` + `DocumentFormat.OpenXml 3.1.1` + `Windows.Data.Pdf` | Servicios transaccionales RBAC (`UserProfileService`, `AssetTreeService`, `SignatureFileService`, `WorkOrderService`, etc.), `SisproSyncHub`, `ExportSanitizer` y exportadores Excel/PDF/Word/CSV e impresión. |
| Interfaz | `src/Sispro.UI` | WinForms `net10.0-windows10.0.19041.0` (`PerMonitorV2`) | Identidad Cambiaso (`#0A263C`, `#0E4163`, `#0089B6`, `#C8A977`), 55 íconos Lucide (`IconCatalog` aislado por hilo STA), `ContextMenuHelper`, `GridCheckboxSelectionController<T>`, `UserProfileManagerControl`, `SignatureManagerControl`, `RequesterManagerControl`, `MaintainerDependenciesDialog`, `ExportUiHelper.ShowWorkOrderPreview` y vistas modulares. |
| Pruebas | `tests/Sispro.Tests` | `xUnit` | **74 pruebas** (Unit, Integration, Functional incl. Importación Access `.mdb` / Jet32 y análisis de diferencias, Perfiles de Usuarios, Catálogo de Firmantes, Exportación Masiva/Valor Neto/Firmas/Dependencias, Solicitantes/Firmas, Tree Admin, ContextMenu & Grid Sort/Pagination STA, UI Audit, Regression, Stress). |

---

## Requisitos e Instalación

- **Sistema Operativo:** Windows 10 (19041+) / Windows 11 (64 bits, `win-x64`).
- **Instalador oficial (autocontenido — no requiere instalar .NET previamente):**
  1. Descargar `SISPRO_Setup_v4.5.0_win-x64.exe` desde la sección [Releases](https://github.com/barriagada666-collab/SISPRO-Proveedores/releases).
  2. Ejecutar el asistente de instalación. **Ruta predeterminada:** `C:\Temp` (modificable por el usuario durante la instalación).
  3. El instalador configura accesos directos, ícono corporativo multi-resolución y copia la base de datos inicial solo si no existe (las actualizaciones y desinstalaciones preservan los datos).

---

## Configuración y Primer Acceso

- Al iniciar, SISPRO resuelve automáticamente la base SQLite en `%LOCALAPPDATA%\Cambiaso\SISPRO\sispro.db` (copiándola desde `Data\sispro.db` en el primer arranque para operar sin privilegios de administrador).
- Desde el botón **Configuración** (exclusivo Administrador) se puede cambiar la ruta de la base de datos, generar respaldos en caliente (`BackupTo`) y ajustar los datos de la empresa.
- **Cuenta Administrador inicial:** `barriagada` (aprovisionada de forma segura con PBKDF2-HMAC-SHA256; exige cambio obligatorio de contraseña en el primer inicio de sesión). *Las credenciales iniciales nunca se almacenan en código fuente ni en este repositorio.*

---

## Gestión de Usuarios, Grupos y Permisos (RBAC)

- **Acceso exclusivo `Administrador`:** Crear, editar, activar, desactivar, eliminar lógicamente, cambiar perfiles, asignar permisos, administrar solicitantes/firmas, administrar grupos, mover usuarios, ejecutar operaciones masivas y revisar auditorías.
- **Perfiles disponibles (`10` permisos granulares):** `Administrador` (acceso total 10/10), `Jefatura` (OT + VBJ + Mantenedores), `Supervisor` (OT + VBS), `Usuario` (consulta/ingreso según asignación).
- **Grupos base:** `MECÁNICOS`, `ELÉCTRICOS`, `ELECTRÓNICOS`, `EXTERNOS`, `OPERADORES` (extensibles y administrables con responsable, estado y protección contra borrado si existen integrantes o historial de movimientos).

---

## Base de Datos SQLite (`data/sispro.db`)

La base operativa `data/sispro.db` mantiene íntegramente los datos migrados de trabajos externos desde `OT_PROV.mdb` (detalle completo en [docs/REPORTE_MIGRACION_OT_PROV.md](docs/REPORTE_MIGRACION_OT_PROV.md)) con migración automática de esquema en el arranque para firmas digitales de solicitantes:

| Entidad Operativa | Registros en `data/sispro.db` | Notas |
|---|---:|---|
| `OrdenesTrabajo` / `DetalleOT` | `7.284` / `7.803` | Histórico completo de trabajos externos (`PRAGMA integrity_check = ok`). |
| `Proveedores` | `214` | Catálogo 100% activo para asociación de OTs. |
| `Activos` | `1.824` (`1.823` activos) | Árbol jerárquico de 10 niveles. |
| `Solicitantes` | `11` (`9` activos) | Solicitantes históricos preservados + soporte `PNG`/`JPG`/`PDF`. |
| `Usuarios` / `Grupos` | `4` / `5` | Cuentas migradas + aprovisionamiento seguro y 5 grupos base. |

---

## Fechas, Búsquedas y Exportaciones

- Todos los campos temporales utilizan **Date Pickers** en formato **`dd-MM-yyyy`** con atajos rápidos (`Todos`, `Hoy`, `Últ. Semana`, `Últ. Mes`, `Este Año`, `Rango`), validación `Desde <= Hasta` y día final inclusivo (`23:59:59`).
- Todos los listados históricos se ordenan en **orden cronológico descendente desde SQLite** (`ORDER BY fecha DESC, id DESC`).
- Todos los módulos principales exportan el resultado filtrado y ordenado en **4 formatos corporativos**:
  - **Excel (`.xlsx`):** Logo, encabezado Petrol/Dorado, AutoFilter, formatos chilenos y firmas digitales (incluyendo firma de solicitante).
  - **PDF (`.pdf`):** Documento vectorial PDF 1.4 multipágina con logo, identidades VBS/VBJ, firma de solicitante rasterizada y numeración `Página X de Y`.
  - **Word (`.docx`):** Documento OpenXML estilizado con logo, tablas corporativas, firma incrustada y pie numerado.
  - **CSV (`.csv`):** Texto UTF-8 con BOM delimitado por `;` (sin elementos gráficos).

---

## Capturas de Pantalla

| Pantalla de Arranque (`v4.4`) | Inicio de Sesión |
|---|---|
| ![Splash](docs/screenshots/00_splash.png) | ![Login](docs/screenshots/01_login.png) |

| Panel de Control (`Nueva OT Externa` Hero) | Trabajos Externos (Firmantes y Vista Previa) |
|---|---|
| ![Dashboard](docs/screenshots/02_dashboard.png) | ![Trabajos Externos](docs/screenshots/03_ordenes_trabajo.png) |

| Órdenes de Trabajo y Botón OT Externa | Generar / Imprimir OT (Firmantes desde Personal) |
|---|---|
| ![OTs](docs/screenshots/10_ot_externa.png) | ![Imprimir OT](docs/screenshots/11_generar_imprimir_ot.png) |

| Usuarios, Grupos y Perfiles | Gestión de Solicitado Por y Firmas Digitales |
|---|---|
| ![Usuarios y Grupos](docs/screenshots/06_usuarios_rbac.png) | ![Solicitantes y Firmas](docs/screenshots/07_solicitantes_firmas.png) |

| Perfiles de Usuarios (CRUD Administrador) | Editor de Árbol (Administrador con Selección Múltiple) |
|---|---|
| ![Perfiles de Usuarios](docs/screenshots/14_perfiles_usuarios.png) | ![Editor de Árbol](docs/screenshots/09_editor_arbol.png) |

| Mantenedores (Proveedores, Solicitantes, Firmas y Árbol) | Búsqueda y Exportación Masiva de OT |
|---|---|
| ![Mantenedores](docs/screenshots/05_mantenedores.png) | ![Búsqueda Masiva OT](docs/screenshots/12_busqueda_ot_masiva.png) |

| Catálogo y Vista Previa de Firmas Digitales | Auditoría y Fechas |
|---|---|
| ![Mantenedores](docs/screenshots/05_mantenedores.png) | ![Usuarios RBAC](docs/screenshots/06_usuarios_rbac.png) |
| ![Mantenedores Firmas](docs/screenshots/13_mantenedores_firmas.png) | ![Importar Access MDB](docs/screenshots/15_importar_mdb.png) |
| ![Auditoría](docs/screenshots/08_auditoria_fechas.png) | ![Generar Imprimir OT](docs/screenshots/11_generar_imprimir_ot.png) |

---

## Compilación y Despliegue

```powershell
# Compilar Debug y Release (0 errores, 0 advertencias)
dotnet build SISPRO.sln -c Debug
dotnet build SISPRO.sln -c Release

# Ejecutar todas las pruebas automatizadas
dotnet test SISPRO.sln -c Release --no-build

# Compilar publicación autocontenida win-x64 + instalador Inno Setup v4.5.0 + SHA-256
.\scripts\Build-Release.ps1
```

---

## Documentación, Versionado e Historial

- [Informe de Entrega `v4.4.0`](docs/INFORME_ENTREGA_v4.4.0.md)
- [Informe de Entrega `v4.3.0`](docs/INFORME_ENTREGA_v4.3.0.md)
- [Informe de Entrega `v4.2.1`](docs/INFORME_ENTREGA_v4.2.1.md)
- [Informe de Entrega `v4.2.0`](docs/INFORME_ENTREGA_v4.2.0.md)
- [Informe de Entrega `v4.1.0`](docs/INFORME_ENTREGA_v4.1.0.md)
- [Informe Final de Entrega Base `v4.0.0` (Secciones A–S)](docs/INFORME_FINAL_ENTREGA.md)
- [Reporte de Migración `OT_PROV.mdb`](docs/REPORTE_MIGRACION_OT_PROV.md)
- [Historial de Cambios (`CHANGELOG.md`)](CHANGELOG.md)
- [Arquitectura Técnica](docs/ARQUITECTURA_TECNICA.md) · [Manual Funcional](docs/MANUAL_FUNCIONAL.md) · [Reporte de Pruebas](docs/REPORTE_PRUEBAS.md) · [Guía de Compilación](docs/GUIA_COMPILACION.md) · [Guía de Despliegue](docs/GUIA_DESPLIEGUE.md)

---

## Créditos

Desarrollado para **Cambiaso Hnos S.A.C. — Departamento Mantención**, Placilla, Valparaíso, Chile.  
Licencia: [MIT](LICENSE).
