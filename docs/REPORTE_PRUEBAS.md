# Reporte de Pruebas y Validación — SISPRO v4.1.0

> **Fecha:** 23-11-2026  
> **Framework:** xUnit + .NET 10 (`Release`)  
> **Resultado Global:** **59 aprobadas / 0 fallidas / 0 omitidas** (`0` errores y `0` advertencias en Debug y Release; `0` vulnerabilidades NuGet).

---

## 1. Resumen por Categoría

| Categoría | Archivo de Pruebas | Casos | Resultado | Cobertura Verificada |
|---|---|---:|---|---|
| **Unit Testing** | `Unit/CoreUnitTests.cs` | 26 | 26/26 OK | RUT chileno módulo 11, IVA 19%, PBKDF2-HMAC-SHA256, migración hash VB6, jerarquía RBAC, validadores y normalización/sugerencia de tipos de nodo (`AssetNodeTypes`). |
| **Integration Testing** | `Integration/DatabaseIntegrationTests.cs` | 3 | 3/3 OK | Integridad de 7.284 OTs y 343K+ filas, árbol 10 niveles (bloqueo nivel 11 vía `AssetTreeService`), respaldo en caliente `BackupTo`. |
| **Sync, Concurrency & Dates** | `Integration/SyncConcurrencyAndDatesTests.cs` | 3 | 3/3 OK | Bus reactivo `SisproSyncHub`, detección inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente global. |
| **Functional (OT/Facturas/Seguridad)** | `Functional/EndToEndWorkflowTests.cs` | 2 | 2/2 OK | Flujo completo OT → VBS/VBJ → Factura IVA → Excel corporativo → reversa; lockout, SQLi, protección último Admin. |
| **Functional (Usuarios)** | `Functional/UserManagementModuleTests.cs` | 5 | 5/5 OK | Aprovisionamiento seguro `barriagada`, CRUD completo, eliminación lógica, RBAC estricto y auditoría sin contraseñas en `src/`. |
| **Functional (Grupos y Lotes)** | `Functional/GroupsAndBatchOperationsTests.cs` | 3 | 3/3 OK | Semilla idempotente de 5 grupos, CRUD y protección de dependencias, movimientos preservando historial OT, lotes con protección de sesión y último Admin. |
| **Functional (CRUD Árbol de Activos)** | `Functional/AssetTreeAdministrationTests.cs` | 5 | 5/5 OK | CRUD Administrador, recálculo de niveles al mover subárbol, bloqueo de ciclos/profundidad > 10, RBAC estricto Supervisor con auditoría `ACCESO_NO_AUTORIZADO`, bloqueo eliminación con hijos/OTs, desactivación en cascada, ruta `depth DESC`, normalización real de anomalías y exportación 4 formatos. |
| **Functional (Exportaciones)** | `Functional/MultiFormatExportTests.cs` | 1 | 1/1 OK | Validación estructural real de archivos generados `.xlsx` (fórmulas `=SUM`), `.pdf` (`%PDF-1.4` + paginación), `.docx` (OpenXML + footer) y `.csv` (UTF-8 BOM) en listados y OT oficial. |
| **UI STA (Menús Contextuales)** | `UI/ContextMenuHelperTests.cs` | 1 | 1/1 OK | Ejecución diferida post-cierre en `ContextMenuStrip` sin `ObjectDisposedException`, `Dispose` completo tras invocación, normalización de separadores e integridad persistente de íconos cacheados en `IconCatalog`. |
| **UI STA (Auditoría y Capturas)** | `UI/WinFormsUiAuditTests.cs` + `ScreenshotCaptureTests.cs` | 4 | 4/4 OK | Verificación de íconos Lucide, textos, sin typos heredados y RBAC en todos los formularios (`MainForm`, `AssetTreeEditorView`, `WorkOrdersForm`, etc.) y los 11 diálogos (`AssetNodeEditorDialog`, `AssetNodeMoveDialog`, etc.) + captura visual de 11 pantallas (`00`..`10`). |
| **Regression Testing** | `Regression/LegacyBugRegressionTests.cs` | 3 | 3/3 OK | Comparación cronológica real, prevención de doble facturación, no inversión VBS/VBJ. |
| **Stress Testing** | `Stress/PerformanceStressTests.cs` | 3 | 3/3 OK | Inserción masiva indexada y lecturas concurrentes + exportación sobre BD real de 343K+ registros. |
| **TOTAL** | — | **59** | **100% OK** | — |

---

## 2. Validación Real del Instalador Inno Setup (`v4.1.0`) y Upgrade desde `v4.0.0`

- **Compilación Inno Setup `6.7.3`:** Exitosa (`SISPRO_Setup_v4.1.0_win-x64.exe`, `48.632.424` bytes / `46,38 MiB`, SHA-256 `AFC74492105CCC49990B3885F52A077DFD3974C668696BA28690FD4ADD1E3F3E`).
- **Instalación base `v4.0.0` + registro centinela:** `INSTALL_40_EXIT=0`, `SENTINEL_BEFORE=1` en `C:\Temp\SISPRO_V41_VERIFY\Data\sispro.db`.
- **Actualización silenciosa `v4.0.0 → v4.1.0`:** `UPGRADE_41_EXIT=0`, `SISPRO.exe` actualizado a `FileVersion = 4.1.0.0` (`Cambiaso Hnos S.A.C.`), registro centinela preservado 100% (`SENTINEL_AFTER_UPGRADE=1`), intactos `1.824` activos y `7.284` OTs.
- **Desinstalación silenciosa:** `UNINSTALL_EXIT=0` — `SISPRO.exe` eliminado limpiamente, `Data\sispro.db` preservada y archivo externo de usuario (`user_notes.txt`) intacto.
