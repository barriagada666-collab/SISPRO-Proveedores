# Reporte de Pruebas y Validación — SISPRO v4.0.0

> **Fecha:** 23-11-2026  
> **Framework:** xUnit + .NET 10 (`Release`)  
> **Resultado Global:** **44 aprobadas / 0 fallidas / 0 omitidas** (`0` errores y `0` advertencias en Debug y Release; `0` vulnerabilidades NuGet).

---

## 1. Resumen por Categoría

| Categoría | Archivo de Pruebas | Casos | Resultado | Cobertura Verificada |
|---|---|---:|---|---|
| **Unit Testing** | `Unit/CoreUnitTests.cs` | 17 | 17/17 OK | RUT chileno módulo 11, IVA 19%, PBKDF2-HMAC-SHA256, migración hash VB6, jerarquía RBAC, validadores. |
| **Integration Testing** | `Integration/DatabaseIntegrationTests.cs` | 3 | 3/3 OK | Integridad de 7.284 OTs y 343K+ filas, árbol 10 niveles (bloqueo nivel 11), respaldo en caliente `BackupTo`. |
| **Sync, Concurrency & Dates** | `Integration/SyncConcurrencyAndDatesTests.cs` | 3 | 3/3 OK | Bus reactivo `SisproSyncHub`, detección inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente global. |
| **Functional (OT/Facturas/Seguridad)** | `Functional/EndToEndWorkflowTests.cs` | 2 | 2/2 OK | Flujo completo OT → VBS/VBJ → Factura IVA → Excel corporativo → reversa; lockout, SQLi, protección último Admin. |
| **Functional (Usuarios)** | `Functional/UserManagementModuleTests.cs` | 5 | 5/5 OK | Aprovisionamiento seguro `barriagada`, CRUD completo, eliminación lógica, RBAC estricto y auditoría sin contraseñas en `src/`. |
| **Functional (Grupos y Lotes)** | `Functional/GroupsAndBatchOperationsTests.cs` | 3 | 3/3 OK | Semilla idempotente de 5 grupos, CRUD y protección de dependencias, movimientos preservando historial OT, lotes con protección de sesión y último Admin. |
| **Functional (Exportaciones)** | `Functional/MultiFormatExportTests.cs` | 1 | 1/1 OK | Validación estructural real de archivos generados `.xlsx` (fórmulas `=SUM`), `.pdf` (`%PDF-1.4` + paginación), `.docx` (OpenXML + footer) y `.csv` (UTF-8 BOM) en listados y OT oficial. |
| **UI STA Testing** | `UI/WinFormsUiAuditTests.cs` + `ScreenshotCaptureTests.cs` | 4 | 4/4 OK | Verificación de íconos Lucide, textos, sin typos heredados y RBAC en todos los formularios y los 9 diálogos + captura real de 9 pantallas. |
| **Regression Testing** | `Regression/LegacyBugRegressionTests.cs` | 3 | 3/3 OK | Comparación cronológica real, prevención de doble facturación, no inversión VBS/VBJ. |
| **Stress Testing** | `Stress/PerformanceStressTests.cs` | 3 | 3/3 OK | Inserción masiva indexada y lecturas concurrentes + exportación sobre BD real de 343K+ registros. |
| **TOTAL** | — | **44** | **100% OK** | — |

---

## 2. Validación Real del Instalador Inno Setup (`v4.0.0`)

- **Compilación Inno Setup `6.7.3`:** Exitosa (`SISPRO_Setup_v4.0.0_win-x64.exe`, `46,29 MB`, SHA-256 `085B96CDE4E34E93D9CAD24B4855E19C85678F35543E1AEAFF39C839ACF7F3B5`).
- **Instalación silenciosa:** `EXIT=0` — instaló `SISPRO.exe` (`v4.0.0.0`), `Assets\sispro.ico` (7 resoluciones), `Data\sispro.db` (5 grupos + 7.284 OTs) y `unins000.exe`.
- **Actualización silenciosa:** `EXIT=0` — registro centinela en `Data\sispro.db` preservado 100% (`SENTINEL_AFTER_UPGRADE=1`).
- **Desinstalación silenciosa:** `EXIT=0` — `SISPRO.exe` eliminado limpiamente, `Data\sispro.db` preservada y archivo externo preexistente en destino intacto.
