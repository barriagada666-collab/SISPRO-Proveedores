# Reporte de Pruebas y Validación — SISPRO v3.0 (.NET 10)

## 1. Resumen de Ejecución

| Métrica | Resultado |
|---|---|
| Framework de Pruebas | xUnit 2.9.3 sobre `.NET 10.0.15` (`net10.0-windows`) |
| Total de Pruebas Ejecutadas | **37** |
| Pruebas Exitosas | **37 (100%)** |
| Pruebas Fallidas | **0** |
| Pruebas Omitidas | **0** |
| Errores de Compilación (Debug & Release) | **0** |
| Advertencias de Compilación (Debug & Release) | **0** (`TreatWarningsAsErrors=true`, `WarningLevel=9999`) |
| Vulnerabilidades NuGet (`dotnet list --vulnerable`) | **0** |
| Integridad SQLite (`PRAGMA integrity_check` / `foreign_key_check`) | **`ok` / `0` violaciones** (`343.400+` filas reales) |

---

## 2. Cobertura por Categoría

### 2.1 Pruebas Unitarias (`tests/Sispro.Tests/Unit/CoreUnitTests.cs`)
- Validación de RUT chileno (módulo 11), cálculo de dígito verificador y formateo (`91.438.000-6`).
- Generación de hash `PBKDF2-HMAC-SHA256` con salt aleatorio único por ejecución y verificación en tiempo constante.
- Verificación de compatibilidad de contraseñas VB6 heredadas (`LEGACY_VB6$C381C0C9DED7D8` para `L Reyes`) y señalización de actualización automática (`needsUpgrade = true`).
- Cálculo de IVA chileno 19% y total con redondeo comercial (`MidpointRounding.AwayFromZero`) + rechazo de montos negativos.
- Principio de mínimo privilegio en plantillas de rol (`Usuario`, `Supervisor`, `Jefatura`, `Administrador`) y limpieza jerárquica de permisos huérfanos (`NormalizeHierarchy`).
- Validación de correos electrónicos y política de contraseñas.

### 2.2 Pruebas de Integración (`tests/Sispro.Tests/Integration/DatabaseIntegrationTests.cs`)
- Verificación íntegra sobre copia real de `data/sispro.db` unificada (`OT_PROV.mdb` + `BD MANTENCION.mdb` + `PERSONAL_MANTENCION.mdb`):
  - `7.284` Órdenes de Trabajo de proveedores (`7.803` detalles), `214` Proveedores activos, `9` Solicitantes activos (`11` totales), `4` Usuarios activos, `1.823` Activos activos (`1.824` totales).
  - `43.989` OTs de Mantención interna, `780` Planes MP, `208.121` Tareas MP, `2.462` Insumos SAP, `134` Técnicos, `43` Fichas RRHH, `56` OTs externas históricas, `41` Motivos de falla y `33` Oficios.
  - Recuperación visible de OT histórica `2514` con su ruta real resuelta desde `BD MANTENCION`: `CAMBIASO HNOS S.A.C. \ PLANTA PE \ RECUPERADO \ LINEA LAV. 3 \ Molino Rapid 400`.
  - Validación de `GetDashboardAnalytics()` (evolución anual, distribución por planta, top proveedores y oficios) y migración transparente de `L Reyes` a `PBKDF2$100000$` + verificación de `BORIS` y `barriagada` con `PBKDF2$100000$` y cambio obligatorio en primer acceso.
- Navegación y creación en árbol de activos hasta profundidad máxima de 10 niveles y bloqueo de nivel 11.
- Respaldo en caliente (`SqliteConnectionFactory.BackupTo`) y restauración verificada (`7.284` OTs íntegras).

### 2.3 Pruebas Funcionales End-to-End (`EndToEndWorkflowTests.cs` y `UserManagementModuleTests.cs`)
- Ciclo completo: Login Admin → Creación de Proveedores, Solicitantes y Activos → Creación de usuarios `Supervisor` (con sincronización automática a `Solicitantes`) y `Jefatura` → Creación de OT con 2 ítems → Aprobación `VBS` por Supervisor + bloqueo `UnauthorizedAccessException` al intentar `VBJ` → Aprobación `VBJ` por Jefatura → Validación de rechazo por proveedor distinto en factura → Registro de factura con cálculo automático de IVA 19% → Bloqueo de eliminación de OT facturada → Exportación de OT a `.xlsx` con verificación real de celdas en `ClosedXML` → Exportación de resultados de búsqueda con progreso → Eliminación de factura con reversa de estado y eliminación final de OT.
- **Módulo GESTIÓN DE USUARIOS (`UserManagementModuleTests.cs`)**:
  - Aprovisionamiento idempotente de Administrador inicial con `PBKDF2-HMAC-SHA256`, cambio obligatorio en primer inicio de sesión, rechazo de reutilización de clave inicial y rotación confirmada.
  - Verificación en base real `data/sispro.db` de la cuenta `barriagada` (Administrador activo, 13/13 permisos, hash PBKDF2, `debe_cambiar_clave = 1`).
  - Ciclo CRUD completo: validaciones obligatorias (usuario único, correo, contraseñas), creación con ID automático, búsqueda multicampo, filtros por perfil y estado, edición y cambio de perfil, restablecimiento seguro de contraseña, eliminación lógica preservando `VistoBueno` histórico, reactivación y auditoría sin fuga de secretos.
  - Bloqueo exclusivo para perfil Administrador (en UI y backend contra BD) para perfiles `Usuario`, `Supervisor` y `Jefatura` con registro `ACCESO_NO_AUTORIZADO` y protección del último Administrador activo.
  - Auditoría automatizada sobre `src/**/*.cs` garantizando cero contraseñas en texto plano en el código fuente.
- Pruebas de seguridad: rechazo de inyección SQL (`admin' OR '1'='1`), bloqueo por fuerza bruta tras 5 intentos fallidos, bloqueo de auto-eliminación y protección contra degradación del último administrador activo.

### 2.4 Pruebas de Interfaz de Usuario (`tests/Sispro.Tests/UI/WinFormsUiAuditTests.cs` y `ScreenshotCaptureTests.cs`)
- Ejecución real en hilo STA (`ApartmentState.STA`) de todos los formularios y diálogos (`LoginForm`, `MainForm`, `WorkOrdersForm`, `InvoicesForm`, `MaintainersForm`, `UsersForm`, `SettingsDialog`, `ChangePasswordDialog`, `AuditLogDialog`).
- Auditoría automática del 100% de botones: verificación de ícono no nulo, dimensiones válidas y textos limpios sin espacios artificiales ni errores ortográficos heredados.
- Verificación de ausencia total de credenciales precargadas en `LoginForm`.
- Verificación de habilitación RBAC en `MainForm` para rol `Usuario` vs rol `Administrador`.
- Inspección visual pixel a pixel mediante capturas PNG reales de los 6 módulos principales.

### 2.5 Pruebas de Regresión VB6 (`tests/Sispro.Tests/Regression/LegacyBugRegressionTests.cs`)
- Confirmación de no-inversión ni desplazamiento de columnas `VBS` / `VBJ` / `IngresoOt` al guardar y recargar permisos.
- Confirmación de comparación cronológica real en búsqueda por rango de fechas (`31-01-2026` a `02-02-2026` incluyendo `01-02-2026`).
- Confirmación de bloqueo ante doble asociación de un mismo ítem de OT a dos facturas distintas.

### 2.6 Pruebas de Estrés y Rendimiento (`tests/Sispro.Tests/Stress/PerformanceStressTests.cs`)
- Consulta completa de 1.726 ítems reales en `< 1.500 ms` (típicamente `< 80 ms`).
- Lectura concurrente desde **16 hilos paralelos** sin errores de bloqueo SQLite (modo WAL + `busy_timeout`).
- Exportación masiva a `.xlsx` de los 1.726 registros reales validada en tiempo y tamaño de salida.
- Inserción masiva de 250 OTs / 500 ítems y búsqueda combinada indexada en `< 500 ms`.

---

## 3. Defectos Detectados y Resueltos Durante el Ciclo de Corrección Continua

1. **Dependencia vulnerable `SQLitePCLRaw.lib.e_sqlite3 2.1.11` (`NU1903` / `GHSA-2m69-gcr7-jv3q`)**: resuelto actualizando a `Microsoft.Data.Sqlite 10.0.15` y fijando `SQLitePCLRaw.lib.e_sqlite3 2.1.14`.
2. **Bucle infinito de layout WinForms en `InvoicesForm` (`AutoScroll` + `FlowLayoutPanel` con `Dock=Fill`)**: detectado durante la captura visual en vivo y resuelto migrando `InvoicesForm` y `UsersForm` a `TableLayoutPanel` deterministas y optimizando `MainForm.HostControl` con visibilidad conmutada.
3. **Recorte de botones inferiores en `LoginForm` y colapso inicial de `SplitContainer` en `WorkOrdersForm`**: detectados en la inspección visual de capturas y corregidos ajustando `ClientSize`, `Panel1MinSize`, `SplitterDistance` y fila dedicada de aprobaciones VBS/VBJ.
4. **Contraste de botones deshabilitados y cabeceras de grilla**: añadida transición automática de color e ícono en `EnabledChanged` y estilo fijo en cabeceras seleccionadas.
