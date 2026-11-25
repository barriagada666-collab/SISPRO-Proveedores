# Reporte de Pruebas y Validación — SISPRO v4.5.0

> **Fecha:** 24-11-2026  
> **Framework:** xUnit + .NET 10 (`Release`, `net10.0-windows10.0.19041.0`)  
> **Resultado Global:** **74 aprobadas / 0 fallidas / 0 omitidas** (`0` errores y `0` advertencias en Debug y Release; `0` vulnerabilidades NuGet en los 5 proyectos).

---

## 1. Resumen por Categoría

| Categoría | Archivo de Pruebas | Casos | Resultado | Cobertura Verificada |
|---|---|---:|---|---|
| **Unit Testing** | `Unit/CoreUnitTests.cs` | 23 | 23/23 OK | RUT chileno módulo 11, PBKDF2-HMAC-SHA256, migración hash VB6, jerarquía RBAC, validadores y `AssetNodeTypes`. |
| **Integration Testing** | `Integration/DatabaseIntegrationTests.cs` | 4 | 4/4 OK | Integridad de 7.284 OTs externas, 214 proveedores y 1.824 activos, árbol 10 niveles (bloqueo nivel 11), migración idempotente de esquema y respaldo en caliente `BackupTo`. |
| **Sync, Concurrency & Dates** | `Integration/SyncConcurrencyAndDatesTests.cs` | 3 | 3/3 OK | Bus reactivo `SisproSyncHub`, detección inter-sesión `PRAGMA data_version`, concurrencia optimista `row_version`, rangos de fecha y orden descendente global. |
| **Functional (Importación Access `.mdb`)** | `Functional/MdbImportTests.cs` | 6 | 6/6 OK | Cálculo exacto de diferencias (`Nuevo` / `Actualizar` / `Sin cambios`, campos modificados y OTs/solicitantes nuevos), inserción/actualización transaccional preservando campos SISPRO y firmas digitales, opción sobrescribir vacíos, bloqueo RBAC a no Administradores, auditoría UI STA de `MdbImportDialog` y análisis real de `OT_PROV.mdb` cuando está presente. |
| **Functional (OT/Seguridad)** | `Functional/EndToEndWorkflowTests.cs` | 2 | 2/2 OK | Flujo E2E OT externa → exportación Excel; bloqueo por fuerza bruta, prevención SQLi y protección último Admin. |
| **Functional (Solicitantes y Firmas)** | `Functional/RequestersSignaturesAndApproversTests.cs` | 2 | 2/2 OK | Alta `Solicitante OT` + firma PDF rasterizada vía `Windows.Data.Pdf`, CRUD independiente `RequesterManagerControl` e incrustación en exportaciones. |
| **Functional (Exportación Masiva, Valor Neto, Firmas y Mantenedores)** | `Functional/BulkOperationsAndExtendedWorkOrderTests.cs` | 3 | 3/3 OK | Exportación masiva exclusiva de OTs seleccionadas (`.xlsx`/`.csv`/`.pdf` sin duplicar ítems), persistencia de Observación/Valor Neto/firmas, dependencias, reasignaciones y RBAC Administrador. |
| **Functional (Usuarios)** | `Functional/UserManagementModuleTests.cs` | 5 | 5/5 OK | Aprovisionamiento seguro `barriagada`, CRUD completo, eliminación lógica, RBAC estricto y auditoría sin credenciales en `src/`. |
| **Functional (Grupos y Lotes)** | `Functional/GroupsAndBatchOperationsTests.cs` | 3 | 3/3 OK | Semilla idempotente de 5 grupos, CRUD y protección de dependencias, movimientos preservando historial OT y lotes seguros. |
| **Functional (CRUD Árbol de Activos)** | `Functional/AssetTreeAdministrationTests.cs` | 5 | 5/5 OK | CRUD Administrador, recálculo de niveles, bloqueo ciclos/>10 niveles, RBAC Supervisor con auditoría, desactivación en cascada, normalización y exportación 4 formatos. |
| **Functional (Exportaciones)** | `Functional/MultiFormatExportTests.cs` | 2 | 2/2 OK | Validación estructural `.xlsx`, `.pdf` multipágina, `.docx` OpenXML, `.csv` UTF-8 BOM y saneamiento XML (`ExportSanitizer`). |
| **Functional (Perfiles, Firmantes Personal y UI STA)** | `Functional/ProfilesSignersAndUiFeaturesTests.cs` | 3 | 3/3 OK | CRUD de Perfiles de Usuarios con protección de perfiles base; catálogo unificado de firmantes (`Solicitantes` + `Usuarios` + `Mant_Personal`); auditoría STA de `WorkOrdersForm` y `UsersForm`. |
| **UI STA (Menús Contextuales)** | `UI/ContextMenuHelperTests.cs` | 1 | 1/1 OK | Ejecución diferida post-cierre en `ContextMenuStrip` sin `ObjectDisposedException`, `Dispose` completo e integridad de `IconCatalog`. |
| **UI STA (Auditoría y Capturas)** | `UI/WinFormsUiAuditTests.cs` + `ScreenshotCaptureTests.cs` | 4 | 4/4 OK | Verificación de íconos Lucide, textos, sin typos, RBAC, `MdbImportDialog`, `AuditLogDialog` (incl. `Perfiles` e `Importación MDB`), versiones dinámicas `v4.5` en `MainForm`/`SplashForm` y capturas (`00`..`15`). |
| **UI STA (Ordenamiento y Paginación)** | `UI/GridSortingAndPaginationTests.cs` | 3 | 3/3 OK | Ordenamiento `SortableBindingList<T>`, mapeo exacto fila→usuario tras ordenar en `UsersForm` y navegación `GridPagerBar`. |
| **Regression Testing** | `Regression/LegacyBugRegressionTests.cs` | 3 | 3/3 OK | Comparación cronológica real, actualización/eliminación de OT y corrección de defectos heredados VB6. |
| **Stress Testing** | `Stress/PerformanceStressTests.cs` | 2 | 2/2 OK | Inserción masiva indexada, lecturas concurrentes y exportación sobre BD real. |
| **TOTAL** | — | **74** | **100% OK** | **Ejecución `Build-Release.ps1` en `Release` (0 fallidas, 0 omitidas, 9 s).** |

---

## 2. Validación Real del Instalador Inno Setup `7.1.1` (`v4.5.0`) y Upgrade desde `v4.4.0`

- **Compilación Inno Setup `7.1.1`:** Exitosa (`SISPRO_Setup_v4.5.0_win-x64.exe`, `57.968.544` bytes, SHA-256 `13E206BFAFA1569AD1810129502AA80D3454C3595614A9E442DB5DECDD6435DA`).
- **Instalación base `v4.4.0` en `C:\Temp\SISPRO_QA_v450`:** `V440_INSTALL_EXIT=0`, `EXE_VER=4.4.0.0`, `DB_HASH1=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`.
- **Actualización silenciosa `v4.4.0 → v4.5.0`:** `V450_UPGRADE_EXIT=0`, `EXE_VER=4.5.0.0`, `DB_HASH2=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`, `DB_PRESERVED=True`.
- **Desinstalación silenciosa:** `UNINS_EXIT=0`, `EXE_EXISTS=False`, `DB_EXISTS=True`, `QA_CLEANUP_DONE=True` (`Data\sispro.db` preservada íntegra por política `uninsneveruninstall`).
