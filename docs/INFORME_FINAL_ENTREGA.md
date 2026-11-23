# Informe Final de Entrega — SISPRO v4.0.0 (.NET 10)

> **Proyecto:** SISPRO — Sistema de Gestión de Proveedores, Órdenes de Trabajo, Usuarios, Grupos y Mantención Industrial  
> **Organización:** Cambiaso Hnos S.A.C. — División Mantención Industrial (Placilla, Valparaíso)  
> **Versión Entregada:** `4.0.0` (LTS `.NET 10`)  
> **Fecha de Entrega:** 23-11-2026  
> **Repositorio GitHub:** https://github.com/barriagada666-collab/SISPRO-Proveedores

---

## A. Resumen Ejecutivo

Se ejecutó de extremo a extremo la auditoría integral, reingeniería controlada, modernización de interfaz, validación formal de migración de `OT_PROV.mdb`, implementación de gestión dinámica de usuarios y grupos de trabajo con operaciones por lotes y Drag & Drop, sincronización reactiva e inter-sesión, estandarización de Date Pickers (`dd-MM-yyyy`) con orden cronológico descendente global, motor multiformato de exportaciones corporativas (**Excel, PDF, Word y CSV**), regeneración de recursos gráficos e ícono multi-resolución, modernización del instalador Inno Setup (`C:\Temp`), verificación real del ciclo de instalación/actualización/desinstalación y publicación en GitHub (`v4.0.0`).

Todo el trabajo se realizó directamente sobre el proyecto existente (`SISPRO.sln`), preservando el 100% de las reglas de negocio y datos históricos (343.451+ registros sin pérdida ni corrupción), logrando **0 errores de compilación**, **0 advertencias**, **0 vulnerabilidades NuGet** y **44/44 pruebas automatizadas aprobadas**.

---

## B. Auditoría Inicial

Antes de modificar el código fuente se generó un respaldo completo en `backups/pre_v4_20261123_145003/` (excluido de Git) con huellas SHA-256 de `OT_PROV.mdb`, `BD MANTENCION.mdb`, `PERSONAL_MANTENCION.mdb` y `data/sispro.db`.

### Hallazgos Detectados en la Línea Base (`v3.0.0`)

| Categoría | Hallazgo Detectado | Acción Ejecutada |
|---|---|---|
| Migración `OT_PROV.mdb` | Faltaba informe formal tabla por tabla con reconciliación 100%, detalle de consultas guardadas y registro corrupto `id_ot=0`. | Generado `docs/REPORTE_MIGRACION_OT_PROV.md` con conteo exacto de las 11 tablas y 5 vistas/queries de Access. |
| Gestión de Grupos | No existían tablas `Grupos` ni `MovimientosGrupo`, ni las 5 categorías iniciales (`MECÁNICOS`, `ELÉCTRICOS`, `ELECTRÓNICOS`, `EXTERNOS`, `OPERADORES`). | Creadas tablas, índices, semilla idempotente, repositorio, servicio RBAC y diálogos completos. |
| Interfaz de Usuarios | `UsersForm` usaba 2 secciones verticales sin panel de grupos, sin CheckBoxes multi-selección, sin acciones por lotes ni Drag & Drop. | Reingeniería completa a arquitectura de **3 Áreas** con selección múltiple, lotes y Drag & Drop. |
| Sincronización y Concurrencia | Las pantallas requerían recarga manual; no existía detección de cambios externos ni control de concurrencia optimista. | Implementado `SisproSyncHub` (eventos post-commit + sondeo `PRAGMA data_version` cada 2,5 s) y `row_version` en `Usuarios` y `Grupos`. |
| Fechas y Ordenamiento | `UserRepository` ordenaba ascendente por ID; `InvoicesForm`, `MaintenanceHubForm`, `UsersForm` y `AuditLogDialog` carecían de Date Pickers de rango estandarizados en `dd-MM-yyyy`. | Creado `DateRangeFilterControl` (`dd-MM-yyyy` + atajos + validación `Desde <= Hasta`) y aplicado `ORDER BY fecha DESC, id DESC` en todos los listados históricos. |
| Exportaciones | Solo existía exportación Excel básica; faltaban PDF, Word (`.docx`) y CSV (`.csv`), además de logo/fórmulas de totales en grillas Excel y columna `Grupo` en impresión. | Implementados `PdfExportService`, `WordExportService`, `CsvExportService`, `DocumentExportService`, banner corporativo + fórmulas `=SUM(...)` en Excel y menú unificado en todos los módulos. |
| Recursos Gráficos | `src/Sispro.UI/Assets/sispro.ico` era un ícono antiguo de 2.238 bytes (32×32, 8 bits); faltaban marca SVG y banners corporativos de Inno Setup. | Regenerado `sispro.ico` (54 KB, 7 frames 16–256 px en 32-bit RGBA), `logo_cambiaso_badge.png`, `sispro_mark.svg`, `wizard_banner.bmp` (164×314) y `wizard_small.bmp` (55×58). |
| Instalador Inno Setup | `Setup_SISPRO.iss` apuntaba a `{autopf}\Cambiaso\SISPRO` y carecía de `AppCopyright`, `AppComments`, `UninstallDisplayName`, `WizardImageFile` y `WizardSmallImageFile`. | Actualizado a `v4.0.0`, `DefaultDirName=C:\Temp` (editable), metadatos completos y banners corporativos. |
| Bug WinForms detectado en pruebas | `ComboBox.SelectedIndex` asignado antes de crear el handle cuando se usaba `DataSource` en diálogos de grupo. | Corregido usando carga determinística con `Items.Add` en todos los diálogos (`GroupMovementDialog`, `GroupEditorDialog`, `GroupHistoryDialog`, `BatchConfirmDialog`). |

---

## C. Arquitectura y Código

La solución mantiene su arquitectura limpia en 4 capas sobre **.NET 10 (`net10.0` / `net10.0-windows`)** con `TreatWarningsAsErrors=true`, `WarningLevel=9999`, `Nullable=enable` y `AnalysisLevel=latest-recommended`:

- **`src/Sispro.Core`**: Entidades (`User`, `UserGroup`, `GroupMovementEntry`, `WorkOrder`, `Invoice`, `Provider`, `Requester`, `AssetNode`, `AuditEntry`), DTOs (`ExportDtos`, `BatchOperationResult`, `ConcurrencyConflictException`, filtros con rangos de fecha), enums (`DataChangeKind`, `DateRangePreset`, `ExportFormat`), seguridad (`PasswordSecurity`, `PermissionPresets`) y validadores (`RutValidator`, `TaxCalculator`, `DomainValidator`).
- **`src/Sispro.Data`**: `SchemaInitializer` (migraciones idempotentes no destructivas + 14 índices de rendimiento), `InitialAdminProvisioner`, `SqliteConnectionFactory` (WAL + `PRAGMA foreign_keys = ON`) y 9 repositorios SQL 100% parametrizados (`GroupRepository`, `UserRepository`, `WorkOrderRepository`, `InvoiceRepository`, `AuditRepository`, `MaintenanceRepository`, `ProviderRepository`, `RequesterRepository`, `AssetTreeRepository`).
- **`src/Sispro.Services`**: `SisproSyncHub`, `GroupManagementService`, `UserManagementService`, `AuthService`, `WorkOrderService`, `InvoiceService`, `MaintainerService`, `MaintenanceService`, `ExcelExportService`, `PdfExportService`, `WordExportService`, `CsvExportService`, `DocumentExportService`, `PrintService` y `SisproApplicationContext` (`IDisposable`).
- **`src/Sispro.UI`**: `MainForm`, `UsersForm` (3 áreas), `WorkOrdersForm`, `InvoicesForm`, `MaintainersForm`, `MaintenanceHubForm`, `LoginForm`, `SplashForm`, controles (`DateRangeFilterControl`, `AssetTreePanel`, `ModernChartPanel`), diálogos (`GroupEditorDialog`, `GroupMovementDialog`, `GroupHistoryDialog`, `BatchConfirmDialog`, `AuditLogDialog`, `SettingsDialog`, `ChangePasswordDialog`, `AdminResetPasswordDialog`, `UserPermissionsDialog`) y tema (`CambiasoTheme`, `IconCatalog`, `ExportUiHelper`).

---

## D. Migración `OT_PROV.mdb`

Informe exhaustivo y huellas SHA-256 disponibles en [REPORTE_MIGRACION_OT_PROV.md](REPORTE_MIGRACION_OT_PROV.md).

- **Archivos originales intactos:** `OT_PROV.mdb` (`8748F957...`), `BD MANTENCION.mdb` (`CCA7EB0D...`), `PERSONAL_MANTENCION.mdb` (`2F0C2D43...`) no fueron modificados ni eliminados.
- **Contabilización 100% de `OT_PROV.mdb`:**
  - `OT_PROV` → `OrdenesTrabajo`: **7.285** origen = **7.284** importadas + **1** fila corrupta `id_ot=0` rechazada (27 grupos `n_ot` duplicados preservados; `id_ot=68` con `id_sol=0` normalizado a `NULL`; rango `29-12-2010` a `23-11-2026`).
  - `Detalle_ot` → `DetalleOT`: **7.803 / 7.803** (100%).
  - `Proveedores` → `Proveedores`: **214 / 214** (1 nombre duplicado desambiguado como `BATERIAS TUBULAR S.A (2)`).
  - `Solicitante` → `Solicitantes`: **9** activos + **2** históricos rescatados inactivos (`5: J Maureira`, `6: L Fuentes`) = **11** (0 huérfanos).
  - `Nivel` → `Niveles`: **4 / 4**.
  - `Usuarios` / `Permisos`: **3** legados (`BORIS` recifrado con PBKDF2 + cambio forzado) + **1** administrador inicial `barriagada` = **4**.
  - `Facturas` (0), `Facturas_loc` → `FacturaDetalles` (0), `VB` → `VistoBueno` (0), `Errores de pegado` (0, excluida).
- **Integridad verificada:** `PRAGMA integrity_check = ok` y `PRAGMA foreign_key_check = 0` sobre las 35 tablas y 343.456 registros totales de `data/sispro.db`.

---

## E. Gestión de Usuarios

- **Administrador inicial operativo:** `barriagada` (`USR-0004`, `codigo_id = 4`, perfil `Administrador`, acceso total 13/13 permisos, estado `Activo`, hash `PBKDF2-HMAC-SHA256` con 100.000 iteraciones, `debe_cambiar_clave = 1`, aprovisionado idempotentemente sin credenciales en texto plano en `src/`).
- **CRUD completo:** Crear, Consultar (con búsqueda por texto, perfil, grupo, estado y fecha de creación), Editar (con carga automática y control de concurrencia `row_version`) y Eliminación lógica (preserva OTs e historial).
- **Campos completos:** ID automático, Usuario, Nombre, Apellido, Correo, Teléfono, Perfil, Contraseña, Confirmación, Grupo, Estado, Fecha de creación, Fecha de modificación y Último acceso.
- **Protección del último Administrador:** Imposible eliminar, desactivar o degradar al último Administrador activo ni a la propia sesión en curso (validado tanto en UI como en capa de servicios).

---

## F. Gestión de Grupos

- **Categorías iniciales creadas idempotentemente:** `MECÁNICOS`, `ELÉCTRICOS`, `ELECTRÓNICOS`, `EXTERNOS` y `OPERADORES` (editables, activables/desactivables y extensibles con nuevas categorías personalizadas).
- **Decisión de modelado (1-a-N):** Tras revisar el dominio de mantenimiento de Cambiaso (donde cada técnico en `Mant_Personal` posee una especialidad/oficio principal único y las reglas del módulo exigen mover un usuario de "grupo origen" a "grupo destino" actualizando contadores de origen y destino), se implementó una relación coherente **1-a-N** (`Usuarios.id_grupo REFERENCES Grupos(id_grupo) ON DELETE SET NULL`), complementada con la tabla auditable `MovimientosGrupo` que conserva trazabilidad histórica completa de cada traslado.
- **Protección de dependencias:** `GroupManagementService.DeleteGroup` bloquea la eliminación física de cualquier grupo que tenga usuarios asignados o registros históricos en `MovimientosGrupo`, orientando a su desactivación.
- **Movimiento de usuarios:** Individual y masivo mediante el diálogo `GroupMovementDialog` y mediante **Drag & Drop** directo desde la grilla (Área 2) hacia el listado de grupos (Área 1), preservando la autoría histórica de órdenes de trabajo previas.

---

## G. Operaciones Masivas

- CheckBoxes en la grilla de usuarios (`colSel`), botones `Todos` / `Ninguno`, y contador en tiempo real `Sel: N`.
- Barra de acciones masivas: **Activar**, **Desactivar**, **Eliminar** (lógico), **Mover**, **Cambiar Perfil**, **Exportar**, **Imprimir** y **Refrescar**.
- **Confirmación detallada (`BatchConfirmDialog`):** Antes de ejecutar muestra cantidad seleccionada, lista de usuarios que serán afectados, lista resaltada de **cuentas protegidas** (sesión activa y último Administrador activo) y campo de motivo auditable.
- **Ejecución segura y trazable:** Retorna `BatchOperationResult` con resultado individual por usuario y registra una entrada de auditoría por cada usuario procesado.

---

## H. Sincronización Inmediata

- **Actualización reactiva en proceso (`SisproSyncHub`):** Cada servicio publica eventos `DataChangeKind` **exclusivamente tras confirmar el `COMMIT` en SQLite**. Los formularios abiertos (`MainForm`, `UsersForm`, `WorkOrdersForm`, `InvoicesForm`) refrescan automáticamente sus grillas, contadores de grupos, combos y KPIs mediante `BeginInvoke` sin reiniciar ni cerrar ventanas.
- **Sincronización entre sesiones concurrentes:** `MainForm` ejecuta un temporizador ligero (2,5 s) que consulta `PRAGMA data_version` sobre una conexión vigía en `SisproSyncHub`. Cuando otra instancia/sesión escribe en la misma base SQLite WAL, SQLite incrementa `data_version` y la aplicación propaga automáticamente `DataChangeKind.All` a las vistas abiertas.
- **Manejo de conflictos concurrentes:** `Usuarios.row_version` y `Grupos.row_version` aplican concurrencia optimista (`WHERE ... AND row_version = $ver`); si otra sesión modificó el registro simultáneamente se lanza `ConcurrencyConflictException`, se informa al usuario y se recarga el estado vigente desde la BD.

---

## I. Seguridad y Permisos

- **RBAC estricto en doble capa (UI + Servicios):** `UserManagementService` y `GroupManagementService` verifican en cada operación el perfil `Administrador` directamente contra la base de datos (no confían solo en ocultar controles), bloqueando intentos de elevación de privilegios y registrando `ACCESO_NO_AUTORIZADO` en `AuditLog`.
- **Cifrado de contraseñas:** `PBKDF2-HMAC-SHA256` (100.000 iteraciones, salt aleatorio de 16 bytes, comparación en tiempo constante `CryptographicOperations.FixedTimeEquals`), bloqueo temporal por intentos fallidos en `AuthService`, y cero contraseñas en código fuente, logs o GitHub (verificado por prueba automatizada `SourceCodeSecurityAudit_ContainsNoHardcodedPlaintextPasswordsInSrc`).
- **Consultas SQL:** 100% parametrizadas (`SqliteParameter`) en todos los repositorios, previniendo inyección SQL.

---

## J. Formularios e Interfaz

- **Arquitectura de 3 Áreas en Gestión de Usuarios y Grupos (`UsersForm`):**
  - **Área 1 (Izquierda):** Panel de grupos con íconos Lucide específicos por especialidad (`Wrench`, `Zap`, `Cpu`, `Globe`, `Gauge`, `Layers`), contadores vivos, barra CRUD de grupos y zona destino Drag & Drop con resaltado visual.
  - **Área 2 (Inferior Derecha):** Filtros combinados + Date Pickers + Grilla de 11 columnas con CheckBox, `Grupo`, colores de estado y origen Drag & Drop.
  - **Área 3 (Central Derecha):** Barra de acciones individuales y por lotes con contador de selección.
- **Auditoría automatizada de botones (`WinFormsUiAuditTests`):** Verifica que el 100% de los botones en todos los formularios y diálogos tengan ícono Lucide válido, texto sin espacios residuales ni errores ortográficos heredados, y acción funcional real.
- **Inspección visual directa:** Las 9 capturas actualizadas en `docs/screenshots/` fueron revisadas visualmente verificando alineación, legibilidad y ausencia de recortes.

---

## K. Date Pickers y Filtros

- Componente reutilizable `DateRangeFilterControl` con formato estandarizado **`dd-MM-yyyy`**, calendario desplegable con navegación por mes y año, atajos rápidos (`Todos`, `Hoy`, `Últ. Semana`, `Últ. Mes`, `Este Año`, `Rango`), limpieza rápida y validación automática `Fecha desde <= Fecha hasta`.
- Integrado en: **Órdenes de Trabajo** (ingreso + búsqueda combinada con proveedor, solicitante, texto y árbol), **Facturas y Guías** (registro + búsqueda), **Mantención Industrial** (OTs internas), **Gestión de Usuarios** (fecha de creación), **Historial de Movimientos de Grupos** y **Bitácora de Auditoría**.
- **Orden cronológico descendente desde SQLite:** Todas las consultas históricas aplican `ORDER BY <fecha> DESC, <id> DESC` (con día final inclusivo `23:59:59` cuando existen marcas horarias) y las exportaciones preservan exactamente los filtros y el orden activo.

---

## L. Identidad Visual

- **Paleta institucional oficial centralizada (`CambiasoTheme.cs`):** Azul Petrol Profundo (`#0A263C`), Azul Industrial (`#0E4163`), Cyan Corporativo (`#0089B6`), Dorado Cambiaso (`#C8A977`), Superficie (`#F2F6F9`), Bordes (`#C5D6E2`), Éxito (`#15803D`), Advertencia (`#C67A00`) y Error (`#C0392B`).
- **Recursos gráficos centralizados y auditados:**
  - `src/Sispro.UI/Assets/sispro.ico` (54 KB, 7 resoluciones 16/24/32/48/64/128/256 px, 32-bit RGBA).
  - `src/Sispro.UI/Assets/logo_cambiaso.png` (logotipo oficial transparente dorado).
  - `src/Sispro.UI/Assets/logo_cambiaso_badge.png` (emblema corporativo 256×256) y `sispro_mark.svg`.
  - `src/Sispro.UI/Assets/Firmas/JR.jpg` y `MS.jpg` (firmas digitales en OTs impresas/exportadas).
  - Catálogo vectorial Lucide (`IconCatalog.cs`, 48 íconos 24×24 con antialiasing).

---

## M. Instalador Inno Setup

- Archivo modernizado: `installer/Setup_SISPRO.iss` (compilado con Inno Setup `6.7.3`).
- **Ruta predeterminada:** `C:\Temp` (`DisableDirPage=no`, editable por el usuario; `DirExistsWarning=no`).
- **Propiedades corporativas completas:** `AppName`, `AppVersion=4.0.0`, `AppPublisher=Cambiaso Hnos S.A.C.`, `AppPublisherURL`, `AppSupportURL`, `AppUpdatesURL`, `AppCopyright`, `AppComments`, `UninstallDisplayName`, `UninstallDisplayIcon`, `SetupIconFile`, `WizardImageFile=Assets\wizard_banner.bmp` (164×314), `WizardSmallImageFile=Assets\wizard_small.bmp` (55×58), `ArchitecturesInstallIn64BitMode=x64compatible`, `PrivilegesRequired=lowest`.
- **Artefacto generado:** `installer/Output/SISPRO_Setup_v4.0.0_win-x64.exe` (**46,29 MB**, SHA-256 `085B96CDE4E34E93D9CAD24B4855E19C85678F35543E1AEAFF39C839ACF7F3B5`).
- **Pruebas reales ejecutadas:**
  - Instalación silenciosa: `EXIT=0` (ejecutable, íconos, 5 grupos y 7.284 OTs verificadas).
  - Actualización silenciosa: `EXIT=0` (registro centinela en `Data\sispro.db` 100% preservado).
  - Desinstalación silenciosa: `EXIT=0` (`SISPRO.exe` eliminado, `Data\sispro.db` preservada, archivo externo de usuario en el mismo directorio intacto).

---

## N. Exportaciones

Fachada unificada `DocumentExportService` + menú desplegable `ExportUiHelper` disponible en Órdenes de Trabajo (listado y documento oficial OT), Facturas y Guías, Proveedores, Usuarios y Grupos, Historial de Movimientos, Mantención Industrial (OTs internas e Insumos SAP) y Auditoría:

- **Excel (`.xlsx`):** ClosedXML con banner corporativo Cambiaso, logotipo incrustado, filtros aplicados, AutoFilter, fila de encabezados inmovilizada, formatos chilenos (`dd-MM-yyyy`, `$#,##0`), fórmulas reales `=SUM(...)` en totales y pie de página con numeración.
- **PDF (`.pdf`):** Generador vectorial nativo PDF 1.4 (`PdfExportService`) con logotipo JPEG embebido, encabezado Petrol/Dorado, tablas multipágina con repetición de encabezados, acentos en español (WinAnsi/Latin-1), totales, numeración `Página X de Y`, y plantilla completa de OT con firmas digitales `JR.jpg`/`MS.jpg`.
- **Word (`.docx`):** OpenXML SDK (`WordExportService`) con logotipo embebido, tablas estilizadas corporativas, repetición de encabezado, pie con campos `PAGE`/`NUMPAGES` y plantilla completa de OT.
- **CSV (`.csv`):** UTF-8 con BOM (`CsvExportService`), delimitador `;` RFC-4180, sin elementos gráficos, preservando exactamente filtros y orden descendente.
- **Impresión (`PrintService`):** Actualizada a v4.0 con columna `Grupo` y paleta institucional.

---

## O. Pruebas

Suite automatizada xUnit ejecutada sobre `Release` (`dotnet test SISPRO.sln -c Release`):

- **Resultado total:** **44 aprobadas / 0 fallidas / 0 omitidas** (duración ~5 s).
- **Cobertura por tipo:**
  - *Unit:* `CoreUnitTests` (RUT módulo 11, IVA 19%, PBKDF2, permisos jerárquicos, validadores).
  - *Integration:* `DatabaseIntegrationTests` (migración real 7.284 OTs, árbol 10 niveles, backup en caliente), `SyncConcurrencyAndDatesTests` (sincronización in-app, sondeo inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente).
  - *Functional:* `EndToEndWorkflowTests`, `UserManagementModuleTests`, `GroupsAndBatchOperationsTests` (CRUD grupos, protección dependencias, movimientos, lotes, protección último Admin, preservación historial OT), `MultiFormatExportTests` (verificación real de archivos `.xlsx`, `.pdf`, `.docx` y `.csv`).
  - *UI STA:* `WinFormsUiAuditTests` (auditoría de íconos, botones, textos y RBAC en todos los formularios y los 9 diálogos), `ScreenshotCaptureTests` (captura en vivo de las 9 pantallas).
  - *Regression & Stress:* `LegacyBugRegressionTests`, `PerformanceStressTests` (carga masiva y lecturas concurrentes sobre 343K+ filas).

---

## P. Compilaciones

| Configuración | Errores | Advertencias | Vulnerabilidades NuGet | Estado |
|---|---:|---:|---:|---|
| `Debug` (`SISPRO.sln`) | **0** | **0** | **0** | Aprobada |
| `Release` (`SISPRO.sln`) | **0** | **0** | **0** | Aprobada |
| `Publish win-x64` (self-contained ReadyToRun) | **0** | **0** | **0** | Aprobada |
| `Inno Setup 6.7.3` (`Setup_SISPRO.iss`) | **0** | **0** | N/A | Aprobada |

---

## Q. GitHub

- Repositorio: `https://github.com/barriagada666-collab/SISPRO-Proveedores` (rama `main`).
- Sincronización limpia: sin archivos `.mdb`, sin respaldos `backups/`, sin `USUARIOS_Y_CLAVES.md` y sin tokens ni contraseñas en el historial.
- Etiqueta y Release oficial: **`v4.0.0`** con instalador `SISPRO_Setup_v4.0.0_win-x64.exe` y `checksums_sha256.txt` adjuntos.

---

## R. Documentación

Documentación completa actualizada en el repositorio:

- [README.md](../README.md) — Guía integral de producto, arquitectura, instalación (`C:\Temp`), usuarios/grupos, migración, fechas, exportaciones y capturas.
- [CHANGELOG.md](../CHANGELOG.md) — Historial detallado de versiones (`v1.0.0` → `v4.0.0`).
- [docs/REPORTE_MIGRACION_OT_PROV.md](REPORTE_MIGRACION_OT_PROV.md) — Reconciliación verificable 100% tabla por tabla de `OT_PROV.mdb` y bases complementarias.
- [docs/INFORME_FINAL_ENTREGA.md](INFORME_FINAL_ENTREGA.md) — Presente informe de entrega (secciones A–S).
- [docs/REPORTE_PRUEBAS.md](REPORTE_PRUEBAS.md), [docs/ARQUITECTURA_TECNICA.md](ARQUITECTURA_TECNICA.md), [docs/MANUAL_FUNCIONAL.md](MANUAL_FUNCIONAL.md), [docs/GUIA_COMPILACION.md](GUIA_COMPILACION.md), [docs/GUIA_DESPLIEGUE.md](GUIA_DESPLIEGUE.md).

---

## S. Pendientes y Limitaciones Técnicas Conocidas

1. **Concurrencia multiusuario en red local (SQLite WAL):** SQLite WAL con sondeo `PRAGMA data_version` (cada 2,5 s) y concurrencia optimista (`row_version`) soporta adecuadamente múltiples sesiones simultáneas locales o en volúmenes compartidos estables de baja/media concurrencia. Para despliegues futuros sobre WAN o decenas de escritores simultáneos en red SMB, se recomienda migrar la capa `Sispro.Data` a **PostgreSQL** o **SQL Server** (facilitado por la separación actual de interfaces `I*Repository`).
2. **Firma digital Authenticode (Windows SmartScreen):** El instalador `SISPRO_Setup_v4.0.0_win-x64.exe` no cuenta con certificado comercial Authenticode (requiere certificado corporativo emitido por una CA para Cambiaso Hnos S.A.C.); al ejecutarse por primera vez en equipos nuevos puede mostrar el aviso estándar de SmartScreen.
3. **Resguardo de credenciales iniciales:** El archivo local `USUARIOS_Y_CLAVES.md` se mantiene excluido del repositorio Git por seguridad y se recomienda cambiar la contraseña inicial del administrador `barriagada` en el primer inicio de sesión (forzado automáticamente por el sistema).
