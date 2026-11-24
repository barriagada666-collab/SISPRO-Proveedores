# Reporte de Pruebas y Validación — SISPRO v4.1.0

> **Fecha:** 23-11-2026  
> **Framework:** xUnit + .NET 10 (`Release`)  
> **Resultado Global:** **64 aprobadas / 0 fallidas / 0 omitidas** (`0` errores y `0` advertencias en Debug y Release; `0` vulnerabilidades NuGet).

---

## 1. Resumen por Categoría

| Categoría | Archivo de Pruebas | Casos | Resultado | Cobertura Verificada |
|---|---|---:|---|---|
| **Unit Testing** | `Unit/CoreUnitTests.cs` | 26 | 26/26 OK | RUT chileno módulo 11, IVA 19%, PBKDF2-HMAC-SHA256, migración hash VB6, jerarquía RBAC, validadores y normalización/sugerencia de tipos de nodo (`AssetNodeTypes`). |
| **Integration Testing** | `Integration/DatabaseIntegrationTests.cs` | 4 | 4/4 OK | Integridad de 7.284 OTs y 343K+ filas, paginación SQL `COUNT(*)+LIMIT/OFFSET` sobre 43.989 `Mant_OT` y 2.462 insumos SAP con RBAC, árbol 10 niveles (bloqueo nivel 11 vía `AssetTreeService`), respaldo en caliente `BackupTo`. |
| **Sync, Concurrency & Dates** | `Integration/SyncConcurrencyAndDatesTests.cs` | 3 | 3/3 OK | Bus reactivo `SisproSyncHub`, detección inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente global. |
| **Functional (OT/Facturas/Seguridad)** | `Functional/EndToEndWorkflowTests.cs` | 2 | 2/2 OK | Flujo completo OT → VBS/VBJ → Factura IVA → Excel corporativo → reversa; lockout, SQLi, protección último Admin. |
| **Functional (Usuarios)** | `Functional/UserManagementModuleTests.cs` | 5 | 5/5 OK | Aprovisionamiento seguro `barriagada`, CRUD completo, eliminación lógica, RBAC estricto y auditoría sin contraseñas en `src/`. |
| **Functional (Grupos y Lotes)** | `Functional/GroupsAndBatchOperationsTests.cs` | 3 | 3/3 OK | Semilla idempotente de 5 grupos, CRUD y protección de dependencias, movimientos preservando historial OT, lotes con protección de sesión y último Admin. |
| **Functional (CRUD Árbol de Activos)** | `Functional/AssetTreeAdministrationTests.cs` | 5 | 5/5 OK | CRUD Administrador, recálculo de niveles al mover subárbol, bloqueo de ciclos/profundidad > 10, RBAC estricto Supervisor con auditoría `ACCESO_NO_AUTORIZADO`, bloqueo eliminación con hijos/OTs, desactivación en cascada, ruta `depth DESC`, normalización real de anomalías y exportación 4 formatos. |
| **Functional (Exportaciones)** | `Functional/MultiFormatExportTests.cs` | 2 | 2/2 OK | Validación estructural real de archivos generados `.xlsx` (fórmulas `=SUM`), `.pdf` (`%PDF-1.4`, codificación `Windows-1252` + paginación multipágina de OTs extensas), `.docx` (OpenXML + footer) y `.csv` (UTF-8 BOM), además de saneamiento XML de caracteres de control vía `ExportSanitizer`. |
| **UI STA (Menús Contextuales)** | `UI/ContextMenuHelperTests.cs` | 1 | 1/1 OK | Ejecución diferida post-cierre en `ContextMenuStrip` sin `ObjectDisposedException`, `Dispose` completo tras invocación, normalización de separadores e integridad persistente de íconos cacheados en `IconCatalog`. |
| **UI STA (Auditoría y Capturas)** | `UI/WinFormsUiAuditTests.cs` + `ScreenshotCaptureTests.cs` | 4 | 4/4 OK | Verificación de íconos Lucide, textos, sin typos heredados y RBAC en todos los formularios (`MainForm`, `AssetTreeEditorView`, `WorkOrdersForm`, etc.) y los 11 diálogos (`AssetNodeEditorDialog`, `AssetNodeMoveDialog`, `AuditLogDialog`, `SettingsDialog`, etc.) + captura visual de 11 pantallas (`00`..`10`). |
| **UI STA (Ordenamiento y Paginación)** | `UI/GridSortingAndPaginationTests.cs` | 3 | 3/3 OK | Ordenamiento cronológico/numérico/texto con `SortableBindingList<T>`, mapeo exacto fila→usuario por `Tag` tras ordenar columnas en `UsersForm` y navegación `GridPagerBar`. |
| **Regression Testing** | `Regression/LegacyBugRegressionTests.cs` | 3 | 3/3 OK | Comparación cronológica real, prevención de doble facturación, no inversión VBS/VBJ. |
| **Stress Testing** | `Stress/PerformanceStressTests.cs` | 3 | 3/3 OK | Inserción masiva indexada y lecturas concurrentes + exportación sobre BD real de 343K+ registros. |
| **TOTAL** | — | **64** | **100% OK** | — |

---

## 2. Validación Real del Instalador Inno Setup (`v4.1.0`) y Upgrade desde `v4.0.0`

- **Compilación Inno Setup `6.7.3`:** Exitosa (`SISPRO_Setup_v4.1.0_win-x64.exe`, `48.622.478` bytes / `46,37 MiB`, SHA-256 `E5AA9191D55431DFF66F16B5E55B18BFB5273B91A8156CB280CFD5F74BE70D91`).
- **Instalación base `v4.0.0` + archivo centinela:** `V40_EXIT=0`, `FileVersion=4.0.0.0` en `C:\Temp\SISPRO_V41_VERIFY`.
- **Actualización silenciosa `v4.0.0 → v4.1.0`:** `UPG_EXIT=0`, `SISPRO.exe` actualizado a `FileVersion = 4.1.0.0` (`Cambiaso Hnos S.A.C.`), hash de `Data\sispro.db` idéntico (`DB_PRESERVED=True`) y centinela preservado (`SENTINEL=True`).
- **Ejecución real post-instalación (`SISPRO.exe`):** `LAUNCH_ALIVE=True`, `RESPONDING=True` (arranque limpio con splash/login sin errores ni dependencias faltantes).
- **Desinstalación silenciosa:** `UNINST_EXIT=0` — `SISPRO.exe` eliminado limpiamente, `Data\sispro.db` preservada (`uninsneveruninstall`).
