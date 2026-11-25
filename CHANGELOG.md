# Changelog — SISPRO

Todos los cambios relevantes del proyecto **SISPRO (Sistema de Gestión de Proveedores y Órdenes de Trabajo — Cambiaso Hnos S.A.C.)** se documentan en este archivo siguiendo el estándar [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [Semantic Versioning](https://semver.org/).

## [4.4.0] — 2026-11-24

### Agregado y Mejorado
- **Selector de Firmantes con CheckBox + Lista Desplegable desde Personal y Usuarios (`WorkOrdersForm`, `WorkOrderRepository`, `WorkOrderService`, `WorkOrderSignatureLayout`)**:
  - Agregados CheckBox + `ComboBox` combinables para **Jefatura**, **Técnico** y **Supervisor** tanto en `Ingreso / Edición de OT` como en `Generar / Imprimir OT`, poblados y deduplicados desde `Solicitantes` activos, `Usuarios` activos y la nómina histórica `Mant_Personal` de `sispro.db` (`GetSignaturePersonnelCatalog`).
  - Migración idempotente de `OrdenesTrabajo` agregando `firma_jefatura`, `firma_tecnico` y `firma_supervisor` (`SchemaInitializer.MigrateOrdenesTrabajoSchema`), reflejando los nombres seleccionados (y vista previa digital si corresponde) en Excel, PDF, Word, Impresión y Vista Previa.
- **Perfiles de Usuarios Dinámicos (`UserProfile`, `UserProfileRepository`, `UserProfileService`, `UserProfileManagerControl`, `UsersForm`)**:
  - Nueva pestaña **Perfiles de Usuarios** en `UsersForm` con CRUD completo para que el Administrador cree, edite, active, desactive y elimine perfiles personalizados (`id_nivel >= 5`) definiendo sus permisos por defecto y protegiendo los 4 perfiles base del sistema (`Usuario`, `Supervisor`, `Jefatura`, `Administrador`) y perfiles con usuarios asignados.
  - Combos de perfil, filtros, cambio masivo (`BatchChangeProfile`, `BatchConfirmDialog`) y visualización (`User.PerfilDisplay`) sincronizados en tiempo real mediante `SisproSyncHub`.
- **Acciones Directas de OT (`WorkOrdersForm`, `ExportUiHelper`, `MainForm`, `MaintainerDependenciesDialog`)**:
  - Botón **Guardar y Exportar** habilitado permanentemente en `Ingreso / Edición de OT` (tanto al guardar una OT nueva como al actualizar y exportar una OT cargada).
  - Nuevo botón **Vista Previa** directamente en `Ingreso / Edición de OT`.
  - Nueva acción **"Visualizar OT (Vista Previa)"** en los menús contextuales de **clic derecho** de las grillas de OT (Dashboard, Búsqueda de OT, Ingreso/Edición de OT y diálogo de dependencias `MaintainerDependenciesDialog`).
- **Limpieza de `VB Supervisor` / `VB Jefatura` en la UI y Estabilización de Espaciados WinForms**:
  - Retiradas las aprobaciones, banners, filtros y columnas `VB Supervisor` (`VBS`) y `VB Jefatura` (`VBJ`) de `MainForm`, `WorkOrdersForm`, `UsersForm`, `UserPermissionsDialog` y `DocumentExportService.BuildWorkOrdersReport` (conservando la tabla histórica `VistoBueno` en SQLite sin pérdida de datos).
  - Reorganizados espaciados y layouts (`WorkOrdersForm`, `UsersForm`, `UserProfileManagerControl`) y reemplazado `SplitContainer` por `TableLayoutPanel` en `WorkOrdersForm` para eliminar `ExternalException (GDI+)` de `RepaintSplitterRect` y evitar todo recorte de textos o botones.

### Verificado
- **Compilación y Seguridad NuGet**: `Debug` y `Release` `0` errores, `0` advertencias; `0` paquetes vulnerables en los 5 proyectos (`dotnet list SISPRO.sln package --vulnerable --include-transitive`).
- **Suite Automatizada (`Release`)**: `68 / 68` pruebas `xUnit` aprobadas (`ProfilesSignersAndUiFeaturesTests` + suite previa, `9 s`) e inspección visual directa de las 14 capturas regeneradas en `docs/screenshots/` (incluyendo `14_perfiles_usuarios.png`).
- **Instalador `SISPRO_Setup_v4.4.0_win-x64.exe`**: `57.901.866` bytes (`SHA-256`: `279BAA4EB257363AAF046F692FE8B89DC1DAED0FFDE711F88C15AC560144A702`), validada actualización silenciosa `v4.3.0 → v4.4.0` (preservación íntegra de `Data\sispro.db` con hash `F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`) y desinstalación limpia; automatizado además en [`.github/workflows/release.yml`](.github/workflows/release.yml).

---

## [4.3.0] — 2026-11-24

### Agregado y Mejorado
- **Búsqueda y Exportación Masiva Exclusiva de Órdenes de Trabajo (`WorkOrdersForm`, `GridCheckboxSelectionController`, `ExportUiHelper`, `DocumentExportService`)**:
  - Columna de checkboxes por fila en `Búsqueda de OT`, casilla `"Seleccionar todos (página visible)"`, contador en vivo (`OT seleccionadas: N`), botón **Exportar seleccionadas** (deshabilitado cuando no hay selección) y **Limpiar selección**.
  - Selección persistente al ordenar columnas o cambiar de página/filtros de resultado, con confirmación previa indicando la cantidad exacta de OTs a exportar.
  - Nuevo generador `DocumentExportService.BuildSelectedWorkOrdersReport` que consolida exclusivamente las OTs seleccionadas sin duplicar ítems por orden, exportable en **Excel (`.xlsx`)**, **CSV (`.csv`)** y **PDF (`.pdf`)** vía `ExportUiHelper.ShowSelectedWorkOrdersExportMenu`.
- **Observación, Valor Neto ($) CLP Destacado y Persistencia de Firmas al Pie (`SchemaInitializer`, `WorkOrderService`, `WorkOrdersForm`)**:
  - Migración idempotente de `OrdenesTrabajo` agregando `observacion` (`TEXT NOT NULL DEFAULT ''`), `valor_neto` (`REAL NOT NULL DEFAULT 0`) y `firmas_mask` (`INTEGER NOT NULL DEFAULT 7`).
  - Nuevos campos en `Ingreso / Edición de OT`: **Observación de la OT** (inmediatamente bajo *Detalle*), **Valor Neto ($)** con tipografía destacada (`15pt` Bold), formateo `$ #,##0` y validación `>= 0` (`WorkOrderService.TryParseNetValue`), además de casillas de firma (`Jefatura`, `Técnico (Sol.)`, `Supervisor`) persistidas en `firmas_mask` y reflejadas tanto en documentos individuales como en la exportación masiva.
- **Gestión Masiva y Control de Integridad en 4 Mantenedores (`MaintainersForm`, `SignatureManagerControl`, `RequesterManagerControl`, `AssetTreePanel`, `AssetTreeEditorView`)**:
  - **Proveedores**, **Solicitantes**, **Firmas** (nueva pestaña dedicada con vista previa PNG/JPG/PDF) y **Árbol de Activos** equipados con checkboxes por registro, acciones masivas (**Consultar OT**, **Reasignar Rel.**, **Activar / Desactivar**, **Eliminar**) y sincronización inmediata.
  - Restricción estricta de operaciones destructivas, reasignaciones y cambios de estado exclusivamente a **Administradores** tanto en interfaz como en capa de servicios (`AuthService.EnsureAdministrator`, `MaintainerService`, `AssetTreeService`, `WorkOrderService.DeleteManyExplicitly`).
  - Nuevo diálogo modal `MaintainerDependenciesDialog` y asistente `MaintainerBatchUiHelper`: consulta de OTs dependientes, reasignación de proveedor, solicitante, firma, activo/subnodos (con protección contra ciclos y tope de 10 niveles), desactivación conservando historial, eliminación compatible sin romper historial y eliminación explícita de OTs asociadas con doble confirmación exclusiva para Administradores.

### Verificado
- **Compilación y Seguridad NuGet**: `Debug` y `Release` `0` errores, `0` advertencias; `0` paquetes vulnerables en los 5 proyectos (`dotnet list SISPRO.sln package --vulnerable --include-transitive`).
- **Suite Automatizada (`Release`)**: `65 / 65` pruebas `xUnit` aprobadas (`BulkOperationsAndExtendedWorkOrderTests` + suite previa) e inspección visual directa de las capturas regeneradas en `docs/screenshots/`.
- **Instalador `SISPRO_Setup_v4.3.0_win-x64.exe`**: `57.859.985` bytes (`SHA-256`: `C3F54BE3B15BE9D59670EC8C0A6A632346D6D09D8B7C5E128077A104F07F403E`), validada actualización silenciosa `v4.2.1 → v4.3.0` (preservación íntegra de `Data\sispro.db` con hash `F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`) y desinstalación limpia.

---

## [4.2.1] — 2026-11-24

### Mejorado y Rediseñado
- **Rediseño integral de exportaciones e impresión de OT (`Excel`, `PDF`, `Word`, `Impresión` / `Vista Previa` y `CSV`)**:
  - **N° OT destacado en parte superior derecha**: bloque dorado institucional Cambiaso (`OT N°` / `OT EXTERNA N°`) con tipografía grande (`19pt`–`22pt` Bold) en los 4 formatos visuales.
  - **Datos del Solicitante completos y `Activo` reubicado**: `WorkOrderRepository` hidrata `nombre_sol`, `ape_pat_sol`, `ape_mat_sol` (`WorkOrder.SolicitanteDisplayNombre`) mostrando **Nombre Apellido**, correo y teléfono en *Datos del Solicitante*, y moviendo **Activo** desde *Datos del Proveedor* hacia *Datos del Solicitante* con ajuste multi-línea sin truncamientos.
  - **Limpieza de columnas y líneas obsoletas**: eliminadas al exportar/imprimir la línea `VB Supervisor: ... | VB Jefatura: ...`, `Estado:` en cabecera y las columnas `Guía / Estado`, `Estado` / `"Registrado"` y `Valor / Ref.` de la tabla de trabajos.
  - **Selector libre de firmas al pie + firma del solicitante como `Técnico`**: convertido `SignatureOption` a `[Flags]` (`SignatureOptionExtensions`), creado compositor compartido `WorkOrderSignatureLayout` y sustituidos los presets cerrados en `WorkOrdersForm` por 3 casillas independientes (`Jefatura`, `Técnico (Solicitante)`, `Supervisor`). Permite cualquier combinación con la firma del solicitante etiquetada como **Técnico** en medio, o desmarcar todas las casillas para dejar el pie vacío sin rayas ni textos residuales.
  - **Alineación UI de `Generar / Imprimir OT`**: campos alineados en grilla fija (`CreatePrintFieldsGrid`) mostrando todos los campos del solicitante (`Solicitante`, `Nombre`, `Correo`, `Teléfono`, `Activo`) y los 3 checkboxes de firma sin recortes.

### Verificado
- **Compilación y Pruebas**: `Debug` y `Release` `0` errores, `0` advertencias; `0` vulnerabilidades NuGet; `62 / 62` pruebas `xUnit` aprobadas + inspección visual de `11_generar_imprimir_ot.png`, PDF e Impresión reales.
- **Instalador `SISPRO_Setup_v4.2.1_win-x64.exe`**: `57.722.069` bytes (`SHA-256`: `6C68EAC97006A10FEBC5CE1167A55BAA6572D86FB689286266723710B14802B5`), validada actualización silenciosa `v4.2.0 → v4.2.1`, preservación de `Data\sispro.db`, arranque vivo de `SISPRO.exe` y desinstalación limpia.

---

## [4.2.0] — 2026-11-24

### Agregado
- **Gestión integral de "Solicitado Por" y Firma Digital (`PNG`, `JPG`, `PDF`)**:
  - Subpestaña dedicada `Solicitados Por y Firmas Digitales` en **Usuarios y Solicitantes** (`UsersForm`) y pestaña compartida `Solicitantes y Firmas` en **Mantenedores** (`MaintainersForm`) mediante el control unificado `RequesterManagerControl`.
  - Adjunto y vista previa de firma digital al crear/editar un usuario marcado con `Solicitante OT` en `UsersForm`, con sincronización automática sobre la tabla `Solicitantes`.
  - Nuevo servicio `SignatureFileService` (`net10.0-windows10.0.19041.0`) para validar `PNG`/`JPG`/`PDF` (máx. 10 MB) y rasterizar automáticamente la página 1 de PDFs a miniatura PNG mediante `Windows.Data.Pdf.PdfDocument` sin dependencias NuGet vulnerables.
  - Migración idempotente de esquema en `SchemaInitializer` (`firma_bytes`, `firma_nombre`, `firma_tipo`, `firma_preview_png`) preservando todos los solicitantes históricos (`11` totales / `9` activos).
  - Nuevas opciones de exportación e impresión en `WorkOrdersForm` (`Ambas Firmas + Solicitante` y `Firma del Solicitante`) con renderizado de la firma en `.xlsx`, `.pdf`, `.docx` e impresión directa.
- **Identificación visible de aprobadores `VB Supervisor` y `VB Jefatura`**:
  - Franja superior `APROBADORES OT VIGENTES` y columnas `VBS` / `VBJ` / `Solicitante / Firma` en `UsersForm`.
  - Resumen de aprobadores autorizados en Dashboard (`MainForm`), etiquetas detalladas con usuario y fecha/hora (`✓ Aprobado por <Usuario> (<fecha>)` o lista de autorizados si está `Pendiente`) en `WorkOrdersForm`, y columnas `VBS`/`VBJ` enriquecidas en grillas y exportaciones.
- **Dashboard centrado en Trabajos Externos**:
  - Nuevo banner Hero con el botón dorado principal **Nueva OT Externa** (`BtnHeroNewExternalOt`) y 5 KPIs clicables (`OTs Externas`, `Proveedores`, `Activos Reales`, `Pendientes VB`, `Solicitantes / Firmas`).

### Eliminado
- Eliminación integral de **Facturas y Guías** (`InvoicesForm`, `InvoiceService`, `InvoiceRepository`, `TaxCalculator`, tablas/DTOs/permisos/filtros asociados) y **Mantención Industrial** (`MaintenanceHubForm`, `MaintenanceService`, `MaintenanceRepository`, `MaintenanceDtos`, botón `"Explorar BD Mantención"` y KPIs internos), conservando intactos todos los datos de OTs externas (`7.284`), proveedores (`214`), activos (`1.824` / `1.823` activos), usuarios, grupos y solicitantes.

### Corregido y Verificado
- **Aislamiento GDI+ por hilo en `IconCatalog`**: caché de íconos Lucide, ícono de aplicación y logotipo indexada por `ManagedThreadId` para eliminar condiciones de carrera `InvalidOperationException: Object is currently in use elsewhere` en formularios e hilos STA concurrentes.
- **Validación en `Build-Release.ps1`**: verificación explícita de `$LASTEXITCODE` por etapa y restauración explícita `-r win-x64`.
- **Compilación y Pruebas**: Debug/Release `0` errores, `0` advertencias; `0` vulnerabilidades NuGet; `62 / 62` pruebas `xUnit` aprobadas.
- **Instalador `SISPRO_Setup_v4.2.0_win-x64.exe`**: `57.695.222` bytes (`SHA-256`: `28A697DDB61E263D845DECBC7CD00BA3CD825403231BF43BEAF33CC77C4D2ECE`), validada actualización silenciosa `v4.1.0 → v4.2.0`, preservación de `Data\sispro.db`, arranque vivo de `SISPRO.exe` y desinstalación limpia.

---

## [4.1.0] — 2026-11-23

### Correcciones Críticas, Menús Contextuales de Clic Derecho, CRUD del Árbol (Solo Administrador) y Botón Visible de OT Externa
- **Reparación crítica de exportaciones (`ContextMenuHelper` + `ExportUiHelper`)**: eliminado `ObjectDisposedException` (`Cannot access a disposed object. Object name: 'System.Windows.Forms.ContextMenuStrip'`) ocurrido al hacer clic en opciones de exportación. En WinForms, `Closed` se dispara antes que `Click`, por lo que ahora tanto la acción elegida como el `Dispose()` del menú se difieren vía `BeginInvoke` y las imágenes cacheadas de `IconCatalog` se desvinculan (`ClearImages`) antes de liberar el menú.
- **Menús de clic derecho en todo el sistema (`ContextMenuHelper.AttachToGrid` / `AttachToListBox` / `AssetTreePanel`)**:
  - **Árbol de Activos**: menú completo para **Administradores** (*Agregar nodo hijo…*, *Editar nodo…*, *Mover a otro nodo padre…*, *Activar nodo*, *Desactivar nodo*, *Eliminar nodo…*, separador, *Expandir rama*, *Contraer rama*, *Copiar ruta*, *Actualizar árbol*) y modo lectura segura para demás perfiles (+ *Buscar OTs de este nodo* en Trabajos Externos).
  - **Usuarios y Grupos (`UsersForm`)**: acciones contextuales en la grilla de usuarios (*Editar*, *Activar/Desactivar*, *Mover a grupo…*, *Cambiar perfil…*, *Restablecer contraseña…*, *Permisos…*, *Eliminar*) y en el panel de grupos (*Nuevo grupo…*, *Editar grupo…*, *Activar/Desactivar grupo*, *Historial de movimientos…*, *Eliminar grupo…*).
  - **Trabajos Externos, Facturas, Mantenedores y Dashboard**: menús contextuales rápidos sobre grillas de OTs recientes, resultados de búsqueda, detalles de OT ingreso/impresión, facturas/ítems asociados, proveedores y solicitantes (incluyendo *Activar/Desactivar* directo).
- **Módulo Especial `Editor de Árbol` (Exclusivo Administrador)**:
  - Nuevo servicio `AssetTreeService` (verificación RBAC estricta en BD, auditoría de intentos `ACCESO_NO_AUTORIZADO` y publicación en `SisproSyncHub`), catálogo estructural normalizado `AssetNodeTypes` (`Empresa`, `Planta`, `Sección / Área`, `Máquina`, `Módulo`, `Componente`), diálogos `AssetNodeEditorDialog` y `AssetNodeMoveDialog`, vista dedicada `AssetTreeEditorView` con leyenda de íconos, contador de anomalías, botón **Normalizar Niveles** y **Exportar Rama** (Excel, PDF, Word, CSV).
  - Bloqueo de eliminación para nodos con hijos o referencias en OTs/Mantención (`OrdenesTrabajo`, `Mant_OT`, `Mant_MP`, `Mant_TareasMP`, `Mant_Checklist`), prevención de ciclos al mover, límite estricto de 10 niveles y desactivación en cascada.
  - Removidos métodos de escritura sobre el árbol desde `MaintainerService` y bloqueada edición visual del árbol para perfiles no administradores en `MaintainersForm`.
- **Botón visible `+ Nueva OT Externa` y flujo rápido de exportación/impresión**:
  - Nuevos botones dorados visibles (`ButtonTone.Gold`) **+ Nueva OT Externa** en la barra superior de `MainForm`, en el Dashboard y en la franja superior de `WorkOrdersForm`, junto con **Exportar / Imprimir OT** y **Guardar y Exportar** en el pie del ingreso de OT.
- **Preservación de selecciones durante captura y sincronización**:
  - Corregido el reinicio involuntario del árbol y de los combos de proveedor/solicitante ante eventos `SyncHub` o al limpiar formularios (`BindCombo`, `InvoicesForm.LoadProviders`, `ClearEntryForm`, `ReloadPreservingSelection`), eliminando la doble recarga en `MainForm.OnSyncDataChanged`.
- **Corrección de jerarquía y búsquedas del árbol (`Activos`)**:
  - `AssetTreeRepository.GetPathToRoot` ahora ordena por profundidad recursiva real (`depth DESC`) en lugar de `nivel ASC`, evitando rutas corruptas en nodos heredados con anomalías de nivel; `WorkOrdersForm.ExecuteSearch` filtra por la profundidad real del nodo seleccionado y `SchemaInitializer` normaliza variantes tipográficas históricas de categoría (`MAQUINA`, `EMPRESA`).
- **Calidad, Ordenamiento, Paginación, Empaquetado y Pruebas**:
  - Catálogo `IconCatalog` ampliado a 55 íconos Lucide (`Copy`, `Expand`, `Collapse`, `ChevronLeft`, `ChevronRight`, `ChevronsLeft`, `ChevronsRight`) y metadato `Description` agregado en `Directory.Build.props`.
  - **Ordenamiento por columnas y Paginación Real (`SortableBindingList<T>` + `GridPagerBar`)**: todas las grillas ordenan fechas, montos, enteros y textos al hacer clic en encabezados resolviendo entidades vía `IGridRowSource<T>` (inmune al reordenamiento visual); corregido en `UsersForm` el mapeo fila→usuario (`Row.Tag` + `SortCompare` + sincronización en `Sorted` / `CurrentCellChanged` con `colSel` no ordenable). Paginación real SQL `COUNT(*) + LIMIT/OFFSET` (`SearchInternalWorkOrdersPaged` sobre 43.989 OTs y `SearchSuppliesPaged` sobre 2.462 insumos SAP) más paginación UI en `WorkOrdersForm`, `InvoicesForm`, `AuditLogDialog` y `GroupHistoryDialog`.
  - Endurecimiento integral de exportaciones (`ExportSanitizer`): limpieza centralizada de caracteres de control XML, codificación `Windows-1252` nativa en PDF (preservando `€`, `—`, `–`, tildes y `ñ`), paginación multipágina en documentos PDF de OT extensa, ancho explícito de encabezado en Word con IDs determinísticos de imagen, creación de directorio destino en Excel de OT, y eliminación de 5 envoltorios muertos en `ExcelExportService`.
  - Endurecimiento UI/Diálogos: protección `!anchor.IsDisposed` en acciones diferidas de `ContextMenuHelper`, filtros `"Árbol de Activos"` / `"Configuración"` + resumen de búsqueda + menú clic derecho en `AuditLogDialog`, validación de empresa y RUT con auditoría y `AcceptButton`/`CancelButton` en `SettingsDialog`, e ícono estándar `AppIcon.Exit` al cerrar `GroupHistoryDialog`.
  - Debug y Release compilando con **0 errores, 0 advertencias y 0 vulnerabilidades NuGet**; suite ampliada a **64/64 pruebas xUnit aprobadas**; ciclo completo de instalación `v4.0.0 → v4.1.0`, ejecución real de `SISPRO.exe` y desinstalación verificado en `C:\Temp`.

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
