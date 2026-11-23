# Changelog — SISPRO

Todos los cambios relevantes del proyecto **SISPRO (Sistema de Gestión de Proveedores y Órdenes de Trabajo — Cambiaso Hnos S.A.C.)** se documentan en este archivo siguiendo el estándar [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [Semantic Versioning](https://semver.org/).

## [4.0.0] — 2026-11-23

### Reingeniería Integral, Gestión Dinámica de Usuarios y Grupos, Exportaciones Multiformato y Preparación para Producción
- **Validación formal 100% de migración `OT_PROV.mdb`**: informe reconciliado tabla por tabla (`docs/REPORTE_MIGRACION_OT_PROV.md`) contabilizando las 11 tablas, 5 consultas/vistas guardadas, 4 claves foráneas y las huellas SHA-256 de los `.mdb` originales intactos.
- **Módulo `GESTIÓN DE GRUPOS` y 5 categorías iniciales**: tablas `Grupos` y `MovimientosGrupo`, semilla idempotente (`MECÁNICOS`, `ELÉCTRICOS`, `ELECTRÓNICOS`, `EXTERNOS`, `OPERADORES`), categorías personalizadas, asignación de responsable, protección ante borrado con dependencias históricas y visor `GroupHistoryDialog` con Date Pickers y exportación.
- **Interfaz moderna de 3 Áreas en `GESTIÓN DE USUARIOS Y GRUPOS` (`UsersForm`)**:
  - **Área 1**: Panel de grupos con íconos Lucide por especialidad, contadores vivos, acciones CRUD y zona destino **Drag & Drop**.
  - **Área 2**: Grilla con columna de **CheckBoxes**, columna **Grupo**, filtros combinados + **Date Pickers (`dd-MM-yyyy`)**, orden cronológico descendente y origen **Drag & Drop**.
  - **Área 3**: Barra de operaciones individuales y por lotes (`Activar`, `Desactivar`, `Eliminar`, `Mover`, `Cambiar Perfil`, `Exportar`, `Imprimir`, `Refrescar`) con confirmación previa detallada (`BatchConfirmDialog`) que resalta cuentas protegidas (sesión activa y último Administrador) y registra auditoría por cada ítem.
- **Sincronización inmediata y concurrencia optimista**: bus reactivo `SisproSyncHub` disparado post-commit, detección de escrituras concurrentes entre sesiones vía `PRAGMA data_version` (cada 2,5 s) y control de conflictos `row_version` (`ConcurrencyConflictException`) en `Usuarios` y `Grupos`.
- **Date Pickers estandarizados (`dd-MM-yyyy`) y orden descendente global**: nuevo control `DateRangeFilterControl` (`Todos`, `Hoy`, `Últ. Semana`, `Últ. Mes`, `Este Año`, `Rango`) con validación `Desde <= Hasta` y día final inclusivo (`23:59:59`) aplicado a Órdenes de Trabajo, Facturas/Guías, Mantención Industrial, Gestión de Usuarios, Historial de Grupos y Auditoría, con `ORDER BY fecha DESC, id DESC` desde SQLite.
- **Motor corporativo de exportaciones multiformato (`DocumentExportService`)**:
  - **Excel (`.xlsx`)**: banner Cambiaso con logotipo, filtros aplicados, formatos chilenos y fórmulas reales `=SUM(...)`.
  - **PDF (`.pdf`)**: generador vectorial PDF 1.4 nativo multipágina con logo, totales, numeración `Página X de Y` y firmas de OT.
  - **Word (`.docx`)**: documentos OpenXML estilizados con logo, tablas corporativas y pie numerado.
  - **CSV (`.csv`)**: UTF-8 con BOM RFC-4180 sin elementos gráficos.
- **Recursos gráficos e Instalador Inno Setup `v4.0.0`**: regeneración de `sispro.ico` multi-resolución (7 tamaños 16–256 px en 32-bit RGBA), `logo_cambiaso_badge.png`, `sispro_mark.svg`, `wizard_banner.bmp` (164×314) y `wizard_small.bmp` (55×58); `Setup_SISPRO.iss` con ruta predeterminada `C:\Temp` (editable), metadatos corporativos completos y pruebas reales verificadas de instalación, actualización y desinstalación.
- **Calidad y pruebas**: compilaciones Debug y Release con **0 errores, 0 advertencias y 0 vulnerabilidades NuGet**, **44/44 pruebas automatizadas xUnit aprobadas** e Informe Final de Entrega (`docs/INFORME_FINAL_ENTREGA.md`, secciones A–S).

## [3.0.0] — 2026-11-23

### Migración Integral de Datos Industriales + Modernización Visual Analítica
- **Importación completa de `OT_PROV.mdb` (producción 2010–2026) + `BD MANTENCION.mdb` + `PERSONAL_MANTENCION.mdb`** en base SQLite unificada `data/sispro.db` (`36,79 MB`, `PRAGMA integrity_check = ok`, `0` violaciones FK, más de **343.400 registros reales**):
  - **SISPRO Proveedores**: `7.284` órdenes de trabajo externas, `7.803` ítems de detalle, `214` proveedores activos, `11` solicitantes (`9` activos + `2` históricos rescatados para `1.506` OTs), `4` usuarios (`L Reyes`, `L Reyes M`, `BORIS` migrado a PBKDF2 con cambio obligatorio, y `barriagada` Administrador inicial).
  - **Árbol de Activos Industrial Real (`1.824` nodos)**: importado íntegramente desde `ACTIVO` + `N1_N2..N1_N10` con ficha técnica completa (`Código SAP`, `Fabricante`, `Modelo`, `Serie`, `Tipo Equipo`, `Estado Operativo`, `Categoría`, `Prioridad`, `Observación`), resolviendo rutas reales (ej. `PLANTA PE \ RECUPERADO \ LINEA LAV. 3 \ Molino Rapid 400`).
  - **Tablas de Mantención Industrial (`Mant_*`)**: `43.989` OTs internas (`Mant_OT`), `780` planes preventivos (`Mant_MP`), `7.704` ciclos MP (`Mant_CicloMP`), `208.121` tareas MP (`Mant_TareasMP`), `2.462` insumos SAP (`Mant_Insumos`), `3.886` consumos (`Mant_InsumosUtilizados`), `134` técnicos (`Mant_Personal`), `43` fichas RRHH (`Mant_PersonalRRHH`), `58.847` asignaciones técnicas, `99` registros EPP, `56` OTs externas históricas, `33` oficios, `41` motivos de falla y `48` tareas checklist.
- **Nuevo Módulo `Mantención Industrial`** (`MaintenanceHubForm`): explorador de 5 pestañas con búsquedas indexadas, visor técnico detallado (técnicos e insumos por OT), drill-down de ciclos/tareas MP y exportación a Excel (`.xlsx`).
- **Panel de Control Modernizado con Gráficas Vectoriales en Vivo** (`ModernChartPanel.cs`):
  - 5 tarjetas KPI con insignias vectoriales Lucide (`OTs Proveedores`, `Proveedores`, `Activos Reales`, `OTs Mantención`, `Planes MP / Insumos`).
  - 3 gráficas GDI+ High-DPI con antialiasing: **Evolución Anual de OTs** (barras verticales con gradiente), **Distribución por Planta** (gráfico de anillo/Donut) y **Top 6 Proveedores** (barras horizontales de ranking).
- **Ficha Técnica Editable de Activos** integrada en `Mantenedores → Árbol de Activos` (`MaintainersForm`).
- **Catálogo Lucide ampliado** (`Wrench`, `BarChart`, `Package`, `HardHat`, `ClipboardCheck`, `Factory`, `TrendingUp`, `Cpu`, `UserPlus`, `Cancel`, `Eraser`) y consistencia visual en todo el proyecto.
- **Optimización de rendimiento**: consultas limitadas (`MaxRows`) en Dashboard (`< 5 ms`), índices compuestos en `Mant_OT`, `Mant_Insumos` y `OrdenesTrabajo`, manteniendo **37/37 pruebas xUnit aprobadas** (incluyendo estrés sobre los 7.803 detalles y 43.989 OTs internas) con **0 errores y 0 advertencias**.

## [2.1.0] — 2026-11-23

### Agregado — Módulo Profesional GESTIÓN DE USUARIOS (CRUD + RBAC Estricto)
- **Formulario rediseñado `GESTIÓN DE USUARIOS`** distribuido en dos secciones (`DATOS DEL USUARIO` y `LISTADO DE USUARIOS`) con los 12 campos completos (ID automático, Usuario, Nombre, Apellido, Correo electrónico, Teléfono, Perfil, Contraseña, Confirmar contraseña, Estado, Fecha de creación y Último acceso).
- **10 botones operativos con iconografía vectorial Lucide**: `Nuevo`, `Guardar`, `Editar`, `Actualizar`, `Eliminar` (eliminación lógica), `Cambiar Contraseña`, `Cancelar`, `Limpiar`, `Buscar` y `Refrescar`, además de `Permisos RBAC`, `Imprimir` y `Excel`.
- **Grilla con búsqueda, filtros combinados e indicadores visuales**: filtrado simultáneo por texto (nombre, apellido, usuario, correo o ID), por perfil y por estado (`Activo` / `Inactivo`) con insignias de color diferenciadas.
- **Eliminación lógica (`activo = 0`)**: desactiva usuarios preservando íntegros todos los registros históricos (`VistoBueno`, `AuditLog`) e impide desactivar la propia cuenta o al último Administrador activo.
- **Gestión segura de contraseñas**: diálogo administrativo `AdminResetPasswordDialog` y flujo de **cambio obligatorio de contraseña en el primer inicio de sesión** (`debe_cambiar_clave = 1`, `ChangePasswordDialog` en modo forzado).
- **Aprovisionamiento seguro e idempotente de Administrador inicial**: `InitialAdminProvisioner` + script `scripts/Initialize-AdminUser.ps1` (hash `PBKDF2-HMAC-SHA256` de 100.000 iteraciones con salt aleatorio CSPRNG y cero credenciales hardcodeadas en el código fuente).
- **Control de acceso exclusivo para Administrador**: validación en UI (`MainForm`, `UsersForm`) y revalidación contra base de datos en `UserManagementService` (bloqueando llamadas directas de otros perfiles y registrando `ACCESO_NO_AUTORIZADO`).
- **Auditoría ampliada**: registro de `CREAR_USUARIO`, `MODIFICAR_USUARIO`, `CAMBIO_PERFIL`, `ACTIVAR_USUARIO`, `DESACTIVAR_USUARIO`, `CAMBIO_CLAVE_ADMIN`, `CAMBIO_CLAVE_PRIMER_ACCESO`, `CAMBIO_CLAVE_CANCELADO`, `INIT_ADMIN_PROVISIONED` y `ACCESO_NO_AUTORIZADO` sin almacenar secretos.
- **Esquema SQLite evolucionado sin duplicar tablas**: migración no destructiva de `Usuarios` con `codigo_id` (ID numérico único automático), `debe_cambiar_clave` y `fecha_modificacion`.
- **Suite de pruebas ampliada a 37/37 pruebas xUnit**, incluyendo auditoría automatizada de ausencia de contraseñas en texto plano en `src/`.

## [2.0.0] — 2026-11-23

### Migración Completa a .NET 10
- Migración íntegra desde Visual Basic 6 (`.vbp`, `.frm`, `.bas`, OCX ActiveX de 32 bits) hacia **C# sobre .NET 10 (`net10.0-windows`)** con Windows Forms nativo y soporte High-DPI (`PerMonitorV2`).
- Arquitectura profesional en capas:
  - `Sispro.Core`: Entidades de dominio, DTOs, validadores (RUT chileno módulo 11, IVA 19%, correo, políticas de contraseña) y seguridad criptográfica.
  - `Sispro.Data`: Motor SQLite embebido (`Microsoft.Data.Sqlite` 10.0.15 + `SQLitePCLRaw.lib.e_sqlite3` 2.1.14 parcheado contra GHSA-2m69-gcr7-jv3q) con modo WAL, claves foráneas estrictas, índices y consultas 100% parametrizadas.
  - `Sispro.Services`: Servicios de negocio, exportación Excel sin dependencia de Office COM (`ClosedXML` 0.105.1), impresión nativa `.NET 10` (`PrintDocument`) y bitácora de auditoría.
  - `Sispro.UI`: Interfaz corporativa División Mantención Industrial Cambiaso (paleta Azul Petrol `#0A263C` / `#0E4163`, Cyan `#0089B6` y Dorado `#C8A977`), logotipo dorado transparente oficial, pantalla de arranque (`SplashForm`), iconografía vectorial Lucide 24×24 en todos los botones, grillas con doble buffer y árbol jerárquico de 10 niveles con carga diferida.
  - `Sispro.Tests`: Suite automatizada xUnit (Unit, Integration, Functional, UI STA, Regression y Stress).

### Agregado
- **Migración completa de datos históricos** desde `OT_PROV.mdb` a `data/sispro.db`: 1.514 órdenes de trabajo válidas, 1.726 ítems de detalle, 73 proveedores, 7 solicitantes, 4 niveles, 2 usuarios y 272 nodos del árbol de activos (`PRAGMA integrity_check = ok`, 0 violaciones FK).
- **Panel de Control (Dashboard)** con indicadores en vivo (OTs totales, pendientes de VB Jefatura, proveedores activos, facturas/guías) y acceso rápido a las últimas OTs registradas.
- **Módulo Mantenedores (M3)** completo: CRUD de Proveedores (con datos de contacto y observaciones), CRUD de Solicitantes y administración completa del Árbol de Activos de 10 niveles.
- **Flujo completo de Guías de Despacho y Facturas (M2)**: asociación validada por proveedor, prevención de ítems duplicados, cálculo automático de IVA 19%, búsqueda avanzada, eliminación con reversa transaccional y exportación Excel.
- **Flujo completo de Visto Bueno (VBS / VBJ)** con registro auditable de usuario y fecha por aprobación.
- **Impresión y Vista Previa nativa** para Órdenes de Trabajo (con logotipo Cambiaso y selección de firmas digitales `JR` / `MS`) y Listado de Usuarios.
- **Seguridad reforzada**: hash `PBKDF2-HMAC-SHA256` (100.000 iteraciones + salt aleatorio) con actualización transparente desde contraseñas VB6 heredadas, bloqueo temporal ante fuerza bruta (5 intentos), protección del último administrador y bitácora de auditoría (`AuditLog`).
- **Configuración y Respaldo en caliente** (`SettingsDialog`) para cambiar base de datos, datos de empresa y generar respaldos consistentes de SQLite.
- **Instalador profesional Inno Setup 6** (`SISPRO_Setup_v2.0.0_win-x64.exe`) autocontenido (no requiere instalar .NET ni drivers Access), con preservación de datos en actualizaciones y desinstalación limpia.

### Corregido (Defectos críticos detectados en VB6)
- **[Seguridad]** Eliminadas credenciales hardcodeadas en `index.frm` y datos personales de prueba en `form_usuario.frm`.
- **[Seguridad]** Eliminada inyección SQL en todas las consultas y reemplazada por parámetros SQLite.
- **[Seguridad / RBAC]** Corregida inversión de permisos `VBSUP` / `VBJEF` en `Utilitarios.bas` (líneas 263-264) y corrimiento de columnas `tab_0_VBJ` / `tab_0_VBS` / `tab_0_1` al crear usuarios en `Transacciones.bas`.
- **[Funcional]** Corregida comparación de fechas por texto (`dd-mm-yyyy`) e inversión `MM/dd` en consultas Access (`TAB_0.bas`), usando fechas ISO `yyyy-MM-dd` y comparación cronológica real.
- **[Funcional]** Recuperada visibilidad de OT histórica `2514` (sin solicitante asignado) mediante `LEFT JOIN Solicitantes`.
- **[Funcional]** Corregido bucle roto de exportación a Excel (`exp_excel.bas` líneas 110-122) y barra de progreso.
- **[Funcional]** Implementadas todas las acciones antes vacías o rotas: Modificar OT, Eliminar OT, Imprimir OT, Localizar/Guardar Facturas, Buscar Facturas, Modificar/Eliminar Usuarios y Mantenedor de Proveedores.
- **[UI/UX]** Corregidas cabeceras erróneas en grilla de usuarios (`TAB_US_0.bas`), desbordamientos de índice al limpiar grillas (`TAB_2.bas`), botones superpuestos (`CommandButton2` sobre `cmd_guardar`) y errores ortográficos (`IINGRESO`, `Envíar`, `vualva`, `Solictante`).

### Eliminado
- Eliminados archivos obsoletos, binarios ActiveX (`TABCTL32.OCX`, `Proveedores.exe`), formularios huérfanos sin uso (`Form1.frm`, `frm_op.frm`, `frm_impresion.frm`, `form_genra_ot.frm`, `sel_imp.frm`, `Module1.bas`), archivos de log antiguos (`.log`) y dependencias COM de Microsoft Excel / Access Jet 4.0.

## [1.0.0] — 2011-11-03
- Versión original en Visual Basic 6 (`Proveedores.exe`) con base de datos Microsoft Access (`OT_PROV.mdb`) y árbol externo (`BD MANTENCION.mdb`).
