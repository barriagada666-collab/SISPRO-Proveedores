# Reporte de Pruebas y Validación — SISPRO v4.3.0

> **Fecha:** 24-11-2026  
> **Framework:** xUnit + .NET 10 (`Release`, `net10.0-windows10.0.19041.0`)  
> **Resultado Global:** **65 aprobadas / 0 fallidas / 0 omitidas** (`0` errores y `0` advertencias en Debug y Release; `0` vulnerabilidades NuGet).

---

## 1. Resumen por Categoría

| Categoría | Archivo de Pruebas | Casos | Resultado | Cobertura Verificada |
|---|---|---:|---|---|
| **Unit Testing** | `Unit/CoreUnitTests.cs` | 23 | 23/23 OK | RUT chileno módulo 11, PBKDF2-HMAC-SHA256, migración hash VB6, jerarquía RBAC, validadores y normalización/sugerencia de tipos de nodo (`AssetNodeTypes`). |
| **Integration Testing** | `Integration/DatabaseIntegrationTests.cs` | 4 | 4/4 OK | Integridad de 7.284 OTs externas, 214 proveedores activos y 1.824 activos, árbol 10 niveles (bloqueo nivel 11 vía `AssetTreeService`), migración idempotente de esquema `Solicitantes` sin perder registros y respaldo en caliente `BackupTo`. |
| **Sync, Concurrency & Dates** | `Integration/SyncConcurrencyAndDatesTests.cs` | 3 | 3/3 OK | Bus reactivo `SisproSyncHub`, detección inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente global. |
| **Functional (OT/Seguridad)** | `Functional/EndToEndWorkflowTests.cs` | 2 | 2/2 OK | Flujo completo OT externa → aprobaciones VBS/VBJ → exportación Excel corporativo; lockout por fuerza bruta, prevención SQLi y protección último Admin. |
| **Functional (Solicitantes, Firmas y VB)** | `Functional/RequestersSignaturesAndApproversTests.cs` | 2 | 2/2 OK | Alta de usuario `Solicitante OT` + firma PDF rasterizada vía `Windows.Data.Pdf`, CRUD independiente de `Solicitado Por` en UI (`RequesterManagerControl`), directorio VBS/VBJ e incrustación en exportaciones. |
| **Functional (Exportación Masiva, Valor Neto, Firmas y Mantenedores)** | `Functional/BulkOperationsAndExtendedWorkOrderTests.cs` | 3 | 3/3 OK | Exportación masiva exclusiva de OTs seleccionadas (`.xlsx`/`.csv`/`.pdf` sin duplicar ítems), persistencia de Observación, Valor Neto CLP (`>= 0`) y `firmas_mask`, inspección de dependencias, reasignaciones de proveedor/solicitante/activo/subnodos, RBAC estricto solo Administrador en UI/servicios y eliminación explícita controlada. |
| **Functional (Usuarios)** | `Functional/UserManagementModuleTests.cs` | 5 | 5/5 OK | Aprovisionamiento seguro `barriagada`, CRUD completo, eliminación lógica, RBAC estricto y auditoría sin contraseñas en `src/`. |
| **Functional (Grupos y Lotes)** | `Functional/GroupsAndBatchOperationsTests.cs` | 3 | 3/3 OK | Semilla idempotente de 5 grupos, CRUD y protección de dependencias, movimientos preservando historial OT, lotes con protección de sesión y último Admin. |
| **Functional (CRUD Árbol de Activos)** | `Functional/AssetTreeAdministrationTests.cs` | 5 | 5/5 OK | CRUD Administrador, recálculo de niveles al mover subárbol, bloqueo de ciclos/profundidad > 10, RBAC estricto Supervisor con auditoría `ACCESO_NO_AUTORIZADO`, bloqueo eliminación con hijos/OTs, desactivación en cascada, ruta `depth DESC`, normalización real de anomalías y exportación 4 formatos. |
| **Functional (Exportaciones)** | `Functional/MultiFormatExportTests.cs` | 2 | 2/2 OK | Validación estructural real de archivos generados `.xlsx`, `.pdf` (`%PDF-1.4`, codificación `Windows-1252` + paginación multipágina de OTs extensas), `.docx` (OpenXML + footer) y `.csv` (UTF-8 BOM), además de saneamiento XML de caracteres de control vía `ExportSanitizer`. |
| **UI STA (Menús Contextuales)** | `UI/ContextMenuHelperTests.cs` | 1 | 1/1 OK | Ejecución diferida post-cierre en `ContextMenuStrip` sin `ObjectDisposedException`, `Dispose` completo tras invocación, normalización de separadores e integridad persistente de íconos cacheados en `IconCatalog`. |
| **UI STA (Auditoría y Capturas)** | `UI/WinFormsUiAuditTests.cs` + `ScreenshotCaptureTests.cs` | 4 | 4/4 OK | Verificación de íconos Lucide, textos, sin typos heredados, RBAC, selector multi-checkbox de firmas al pie, 4 pestañas de Mantenedores y `MaintainerDependenciesDialog` en STA + captura visual e inspección de pantallas (`00`..`13`, excluyendo módulos eliminados `04` y `07` reemplazada). |
| **UI STA (Ordenamiento y Paginación)** | `UI/GridSortingAndPaginationTests.cs` | 3 | 3/3 OK | Ordenamiento cronológico/numérico/texto con `SortableBindingList<T>`, mapeo exacto fila→usuario por `Tag` tras ordenar columnas en `UsersForm` bajo ejecución paralela STA y navegación `GridPagerBar`. |
| **Regression Testing** | `Regression/LegacyBugRegressionTests.cs` | 3 | 3/3 OK | Comparación cronológica real, actualización y eliminación de OT sin dependencias obsoletas de facturas, no inversión VBS/VBJ. |
| **Stress Testing** | `Stress/PerformanceStressTests.cs` | 2 | 2/2 OK | Inserción masiva indexada y lecturas concurrentes + exportación sobre BD real. |
| **TOTAL** | — | **65** | **100% OK** | **Ejecución `Build-Release.ps1` en `Release` (0 fallidas, 0 omitidas, 5 s).** |

---

## 2. Validación Real del Instalador Inno Setup (`v4.3.0`) y Upgrade desde `v4.2.1`

- **Compilación Inno Setup `6.7.3`:** Exitosa (`SISPRO_Setup_v4.3.0_win-x64.exe`, `57.859.985` bytes, SHA-256 `C3F54BE3B15BE9D59670EC8C0A6A632346D6D09D8B7C5E128077A104F07F403E`).
- **Instalación base `v4.2.1` en `C:\Temp\SISPRO_QA_v430`:** `INSTALL_421_EXIT=0`, `EXE_VER=4.2.1.0`, `DB_HASH=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`.
- **Actualización silenciosa `v4.2.1 → v4.3.0`:** `UPGRADE_430_EXIT=0`, `EXE_VER=4.3.0.0`, `DB_HASH=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`, `DB_PRESERVED=True`.
- **Desinstalación silenciosa:** `UNINSTALL_EXIT=0`, `EXE_EXISTS=False`, `DB_EXISTS=True`, `CLEANED_UP=True` (`Data\sispro.db` preservada íntegra por política `uninsneveruninstall`).
