# Informe de Entrega — SISPRO v4.1.0 (.NET 10)

> **Proyecto:** SISPRO — Sistema Integral de Gestión de Proveedores, Órdenes de Trabajo, Árbol de Activos, Usuarios y Mantención Industrial  
> **Organización:** Cambiaso Hnos S.A.C. — División Mantención Industrial (Placilla, Valparaíso)  
> **Versión:** `4.1.0` (`v4.0.0 → v4.1.0`)  
> **Fecha de Entrega:** 23-11-2026  
> **Repositorio GitHub:** https://github.com/barriagada666-collab/SISPRO-Proveedores

---

## 1. Resumen Ejecutivo

En **SISPRO v4.1.0** se resolvieron los defectos detectados en operación sobre la versión `v4.0.0` y se incorporaron capacidades solicitadas de productividad y administración avanzada sobre el proyecto existente (`SISPRO.sln`):

1. **Reparación crítica del error de exportación** (`Cannot access a disposed object. Object name: 'System.Windows.Forms.ContextMenuStrip'`) mediante el nuevo administrador centralizado `ContextMenuHelper`.
2. **Menús contextuales de clic derecho** en el Árbol de Activos y en todas las grillas y listados operativos del sistema.
3. **CRUD completo del Árbol de Activos (10 niveles) exclusivo para Administradores**, protegido por verificación RBAC contra la base de datos en `AssetTreeService` y expuesto en un nuevo módulo dedicado **Editor de Árbol** (`AssetTreeEditorView`).
4. **Botón visible y directo `+ Nueva OT Externa`** junto con flujo inmediato de **Exportar / Imprimir OT** y **Guardar y Exportar**.
5. **Estabilización de selección y corrección de jerarquía histórica**, preservando la selección del árbol y catálogos ante eventos de sincronización y corrigiendo el ordenamiento de rutas (`GetPathToRoot`) y filtrado cuando existen anomalías de nivel heredadas de Access.

---

## 2. Hallazgos Identificados y Correcciones Ejecutadas

| # | Hallazgo / Requerimiento | Causa Raíz / Situación Previa (`v4.0.0`) | Solución Implementada (`v4.1.0`) |
|---|---|---|---|
| **1** | Excepción al exportar (`ContextMenuStrip` eliminado) | `ExportUiHelper` ejecutaba `menu.Closed += (_, _) => menu.Dispose();`. En WinForms `Closed` se dispara antes que `Click`, liberando el menú prematuramente antes de ejecutar la acción/diálogo. | Creado `ContextMenuHelper.cs`: difiere tanto la ejecución de cada `Action` como el `Dispose()` del `ContextMenuStrip` vía `BeginInvoke`, y desvincula bitmaps cacheados en `IconCatalog` (`ClearImages`) antes de liberar. `ExportUiHelper` reescrito íntegramente sobre `ContextMenuHelper.ShowBelow`. |
| **2** | Reinicio del árbol y combos al sincronizar | `WorkOrdersForm.ReloadCatalogs` llamaba `_assetTree.LoadRoot()`, `BindCombo` reseteaba `SelectedIndex = 0` y `MainForm.OnSyncDataChanged` re-disparaba recargas adicionales sobre vistas ya suscritas. | Eliminada doble recarga en `MainForm`; `BindCombo` e `InvoicesForm.LoadProviders` preservan `SelectedValue`; `ClearEntryForm` conserva el nodo activo seleccionado; `ReloadCatalogs` usa `_assetTree.ReloadPreservingSelection()`. |
| **3** | Rutas erróneas en nodos con anomalías de nivel | 25 nodos importados tenían `nivel <> nivel_padre + 1` (6 con `nivel <= nivel_padre`). `GetPathToRoot` ordenaba por `nivel ASC` y `WorkOrdersForm.ExecuteSearch` filtraba usando `node.Nivel`. | `GetPathToRoot` ahora calcula `depth` recursiva desde el nodo hasta la raíz y ordena `ORDER BY depth DESC`. `WorkOrdersForm.ExecuteSearch` calcula `depth` real (`GetAssetPath(node.IdActivo).Count`). Botón **Normalizar Niveles** disponible en Editor de Árbol. |
| **4** | Escritura del árbol abierta a no-administradores | `MaintainerService.AddAssetChild` / `UpdateAssetDetails` solo requerían `MenuMantenedor`. | Movidas todas las escrituras al nuevo servicio `AssetTreeService` con validación `IsAdministrator` en BD y registro `ACCESO_NO_AUTORIZADO` en `AuditLog`. En `MaintainersForm`, usuarios no administradores solo ven ficha de solo lectura. |
| **5** | Falta de menús contextuales y CRUD completo del árbol | Sin menús de clic derecho en árbol ni grillas; imposible mover ramas, activar/desactivar en cascada o eliminar hojas sin usar SQL. | `ContextMenuHelper.AttachToGrid` y `AttachToListBox` integrados en todos los módulos; menú completo Admin en `AssetTreePanel`, diálogos `AssetNodeEditorDialog` / `AssetNodeMoveDialog`, y módulo `AssetTreeEditorView`. |
| **6** | Falta de acceso rápido para crear y exportar OT Externa | El usuario debía navegar manualmente por barras y pestañas para iniciar nueva OT externa y pasar a impresión/exportación. | Botón dorado `+ Nueva OT Externa` visible en `MainForm` (barra superior y Dashboard) y `WorkOrdersForm`, además de `Exportar / Imprimir OT` y `Guardar y Exportar` con apertura directa a `ShowPrintForOrder`. |

---

## 3. Verificaciones Reales y Métricas de Calidad

- **Compilación `Debug` (`SISPRO.sln`):** **0 errores**, **0 advertencias** (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`).
- **Compilación `Release` (`SISPRO.sln`):** **0 errores**, **0 advertencias**.
- **Auditoría de dependencias NuGet (`dotnet list SISPRO.sln package --vulnerable --include-transitive`):** **0 paquetes vulnerables**.
- **Suite Automatizada `xUnit` (`dotnet test -c Release --no-build`):** **64 / 64 pruebas aprobadas** (0 fallidas, 0 omitidas), incluyendo:
  - `AssetTreeAdministrationTests` (5 pruebas): CRUD completo, movimiento con recálculo de subárbol, protección ciclos y nivel 11, RBAC estricto Supervisor con auditoría, bloqueo por hijos y referencias OTs/Mantención, desactivación en cascada, ruta `depth DESC`, normalización de los 28 nodos anómalos reales y exportación `.xlsx`/`.pdf`/`.docx`/`.csv`.
  - `MultiFormatExportTests` (2 pruebas): saneamiento XML compartido (`ExportSanitizer`), codificación `Windows-1252` nativa en PDF (`€`, `—`, tildes, `ñ`) y paginación multipágina de OTs extensas (`25` ítems) sin solapar firmas.
  - `DatabaseIntegrationTests` (4 pruebas): preservación integral de 343K+ filas y paginación real `COUNT(*) + LIMIT/OFFSET` sobre 43.989 `Mant_OT` y 2.462 insumos SAP con RBAC.
  - `ContextMenuHelperTests` (1 prueba STA) y `GridSortingAndPaginationTests` (3 pruebas STA): ejecución diferida segura, ordenamiento por columnas `SortableBindingList<T>` preservando entidad fuente, ordenamiento cronológico/numérico en `UsersForm` resolviendo checkboxes/selección vía `Row.Tag`, y navegación completa de `GridPagerBar`.
  - `WinFormsUiAuditTests` + `ScreenshotCaptureTests`: auditoría completa de íconos Lucide (55 íconos)/botones/RBAC/diálogos + capturas actualizadas en [docs/screenshots](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots) e inspección visual de `07_mantencion_industrial.png`, `08_auditoria_fechas.png`, `09_editor_arbol.png` y `10_ot_externa.png`.
- **Instalador Inno Setup `6.7.3`:**
  - Artefacto: `installer/Output/SISPRO_Setup_v4.1.0_win-x64.exe` (**48.622.478 bytes** / **46,37 MiB**)
  - Ruta predeterminada: `C:\Temp` (editable por el usuario)
  - `SHA-256`: `E5AA9191D55431DFF66F16B5E55B18BFB5273B91A8156CB280CFD5F74BE70D91`
  - Prueba real silenciosa (`v4.0.0 → v4.1.0 → arranque SISPRO.exe → desinstalación` en `C:\Temp\SISPRO_V41_VERIFY`):
    - Instalación `v4.0.0`: `EXIT=0`, `FileVersion=4.0.0.0`
    - Actualización `v4.1.0`: `EXIT=0`, `FileVersion=4.1.0.0`, `Data\sispro.db` preservada sin alteración (`SHA-256` idéntico) y archivo centinela conservado
    - Ejecución real post-instalación: `LAUNCH_ALIVE=True`, `RESPONDING=True`
    - Desinstalación: `EXIT=0`, ejecutable eliminado, `Data\sispro.db` preservada (`uninsneveruninstall`).

---

## 4. Limitaciones Técnicas Conocidas

1. **Concurrencia en red compartida (SQLite WAL):** Adecuado para operación local o red LAN de baja/media concurrencia mediante `PRAGMA data_version` (2,5 s) y concurrencia optimista (`row_version`). Escenarios WAN o alta escritura simultánea masiva requieren migrar los repositorios `Sispro.Data` a PostgreSQL o SQL Server.
2. **Firma Digital Authenticode (Windows SmartScreen):** El instalador no incorpora certificado comercial Authenticode emitido por una CA corporativa; puede requerir confirmación estándar de SmartScreen en la primera ejecución en equipos nuevos.
3. **Resguardo de credenciales en repositorio:** La inicialización segura mantiene `barriagada` sin credenciales en texto plano en código/Git; si se compartió un token personal de GitHub durante la conversación, se recomienda revocarlo y rotarlo tras validar el Release `v4.1.0`.
