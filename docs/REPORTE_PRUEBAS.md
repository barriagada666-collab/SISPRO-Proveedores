# Reporte de Pruebas y Validación — SISPRO v4.2.1

> **Fecha:** 24-11-2026  
> **Framework:** xUnit + .NET 10 (`Release`, `net10.0-windows10.0.19041.0`)  
> **Resultado Global:** **62 aprobadas / 0 fallidas / 0 omitidas** (`0` errores y `0` advertencias en Debug y Release; `0` vulnerabilidades NuGet).

---

## 1. Resumen por Categoría

| Categoría | Archivo de Pruebas | Casos | Resultado | Cobertura Verificada |
|---|---|---:|---|---|
| **Unit Testing** | `Unit/CoreUnitTests.cs` | 23 | 23/23 OK | RUT chileno módulo 11, PBKDF2-HMAC-SHA256, migración hash VB6, jerarquía RBAC, validadores y normalización/sugerencia de tipos de nodo (`AssetNodeTypes`). |
| **Integration Testing** | `Integration/DatabaseIntegrationTests.cs` | 4 | 4/4 OK | Integridad de 7.284 OTs externas, 214 proveedores activos y 1.824 activos, árbol 10 niveles (bloqueo nivel 11 vía `AssetTreeService`), migración idempotente de esquema `Solicitantes` sin perder registros y respaldo en caliente `BackupTo`. |
| **Sync, Concurrency & Dates** | `Integration/SyncConcurrencyAndDatesTests.cs` | 3 | 3/3 OK | Bus reactivo `SisproSyncHub`, detección inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente global. |
| **Functional (OT/Seguridad)** | `Functional/EndToEndWorkflowTests.cs` | 2 | 2/2 OK | Flujo completo OT externa → aprobaciones VBS/VBJ → exportación Excel corporativo; lockout por fuerza bruta, prevención SQLi y protección último Admin. |
| **Functional (Solicitantes, Firmas y VB)** | `Functional/RequestersSignaturesAndApproversTests.cs` | 2 | 2/2 OK | Alta de usuario `Solicitante OT` + firma PDF rasterizada vía `Windows.Data.Pdf`, CRUD independiente de `Solicitado Por` en UI (`RequesterManagerControl`), directorio VBS/VBJ e incrustación en exportaciones. |
| **Functional (Usuarios)** | `Functional/UserManagementModuleTests.cs` | 5 | 5/5 OK | Aprovisionamiento seguro `barriagada`, CRUD completo, eliminación lógica, RBAC estricto y auditoría sin contraseñas en `src/`. |
| **Functional (Grupos y Lotes)** | `Functional/GroupsAndBatchOperationsTests.cs` | 3 | 3/3 OK | Semilla idempotente de 5 grupos, CRUD y protección de dependencias, movimientos preservando historial OT, lotes con protección de sesión y último Admin. |
| **Functional (CRUD Árbol de Activos)** | `Functional/AssetTreeAdministrationTests.cs` | 5 | 5/5 OK | CRUD Administrador, recálculo de niveles al mover subárbol, bloqueo de ciclos/profundidad > 10, RBAC estricto Supervisor con auditoría `ACCESO_NO_AUTORIZADO`, bloqueo eliminación con hijos/OTs, desactivación en cascada, ruta `depth DESC`, normalización real de anomalías y exportación 4 formatos. |
| **Functional (Exportaciones)** | `Functional/MultiFormatExportTests.cs` | 2 | 2/2 OK | Validación estructural real de archivos generados `.xlsx`, `.pdf` (`%PDF-1.4`, codificación `Windows-1252` + paginación multipágina de OTs extensas), `.docx` (OpenXML + footer) y `.csv` (UTF-8 BOM), además de saneamiento XML de caracteres de control vía `ExportSanitizer`. |
| **UI STA (Menús Contextuales)** | `UI/ContextMenuHelperTests.cs` | 1 | 1/1 OK | Ejecución diferida post-cierre en `ContextMenuStrip` sin `ObjectDisposedException`, `Dispose` completo tras invocación, normalización de separadores e integridad persistente de íconos cacheados en `IconCatalog`. |
| **UI STA (Auditoría y Capturas)** | `UI/WinFormsUiAuditTests.cs` + `ScreenshotCaptureTests.cs` | 4 | 4/4 OK | Verificación de íconos Lucide, textos, sin typos heredados, RBAC, selector multi-checkbox de firmas al pie (`Jefatura`, `Técnico`, `Supervisor` y pie vacío `Ninguna`) y ausencia explícita de módulos Facturas/Mantención en todos los formularios y los 11 diálogos + captura visual de 11 pantallas vigentes (`00`..`11`, excluyendo módulos eliminados). |
| **UI STA (Ordenamiento y Paginación)** | `UI/GridSortingAndPaginationTests.cs` | 3 | 3/3 OK | Ordenamiento cronológico/numérico/texto con `SortableBindingList<T>`, mapeo exacto fila→usuario por `Tag` tras ordenar columnas en `UsersForm` bajo ejecución paralela STA y navegación `GridPagerBar`. |
| **Regression Testing** | `Regression/LegacyBugRegressionTests.cs` | 3 | 3/3 OK | Comparación cronológica real, actualización y eliminación de OT sin dependencias obsoletas de facturas, no inversión VBS/VBJ. |
| **Stress Testing** | `Stress/PerformanceStressTests.cs` | 2 | 2/2 OK | Inserción masiva indexada y lecturas concurrentes + exportación sobre BD real. |
| **TOTAL** | — | **62** | **100% OK** | **Confirmado en 3 corridas consecutivas + Build-Release integral.** |

---

## 2. Validación Real del Instalador Inno Setup (`v4.2.1`) y Upgrade desde `v4.2.0`

- **Compilación Inno Setup `6.7.3`:** Exitosa (`SISPRO_Setup_v4.2.1_win-x64.exe`, `57.722.069` bytes / `55,05 MiB`, SHA-256 `6C68EAC97006A10FEBC5CE1167A55BAA6572D86FB689286266723710B14802B5`).
- **Instalación base `v4.2.0` + archivo centinela:** `V420_EXIT=0`, `FileVersion=4.2.0.0` en `C:\Temp\SISPRO_V421_VERIFY`.
- **Actualización silenciosa `v4.2.0 → v4.2.1`:** `UPG_EXIT=0`, `SISPRO.exe` actualizado a `FileVersion = 4.2.1.0`, hash de `Data\sispro.db` idéntico (`DB_PRESERVED=True`) y centinela preservado (`SENTINEL=True`).
- **Ejecución real post-instalación (`SISPRO.exe`):** `LAUNCH_ALIVE=True`, `RESPONDING=True`.
- **Desinstalación silenciosa:** `UNINST_EXIT=0` — `SISPRO.exe` eliminado limpiamente (`EXE_REMOVED=True`), `Data\sispro.db` preservada (`DB_AFTER_UNINST=True`, `uninsneveruninstall`).
