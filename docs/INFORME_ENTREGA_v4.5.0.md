# Informe de Entrega — SISPRO v4.5.0 (.NET 10)

> **Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)**  
> **Fecha:** 24-11-2026 · **Versión:** `4.5.0` · **Instalador:** Inno Setup `7.1.1` (`win-x64`)

---

## 1. Resumen Ejecutivo

La versión **`v4.5.0`** incorpora un subsistema completo de **análisis comparativo e importación desde Microsoft Access (`.mdb` / `.accdb`) hacia SQLite (`sispro.db`)**, ejecuta una auditoría integral de código, memoria GDI+, UI/UX y seguridad sobre todos los módulos del proyecto, actualiza las gráficas y descripciones simplificadas del instalador Inno Setup `7.1.1` y valida `74 / 74` pruebas automatizadas con **`0` errores, `0` advertencias y `0` paquetes vulnerables**.

---

## 2. Principales Mejoras Entregadas

1. **Importador y Comparador Access (`.mdb`) → SQLite (`MdbImportService`, `Jet32MdbTableReader`, `MdbImportDialog`)**:
   - Exclusivo para perfil **Administrador** (validado en UI y backend), accesible desde **Mantenedores → Proveedores → `Importar MDB`** y **Configuración → `Importar MDB`**.
   - Compatible con aplicaciones `win-x64` mediante lectura nativa `SysWOW64` (`Microsoft.Jet.OLEDB.4.0`), sin paquetes NuGet externos ni vulnerabilidades.
   - **Previsualización exacta antes de aplicar**: 5 tarjetas KPI y grilla comparativa muestran cuántos proveedores existen en el `.mdb` vs `sispro.db`, cuántos son **Nuevos**, cuántos están **Por Actualizar** (detallando `Dirección`, `Teléfono`, `Fax`, `Correo` y vista `Actual → Nuevo`), cuántos están **Sin Cambios**, y cuántos **Solicitantes** u **OTs nuevas** existen en el archivo `.mdb`.
   - **Actualización transaccional segura**: consolida duplicados del `.mdb`, preserva por defecto campos enriquecidos en SISPRO (`Contacto`, `Correo Contacto`, `Observaciones`, `Activo` y firmas digitales), ofrece respaldo en caliente previo (`backups/sispro_pre_mdb_*.db`), audita (`Importación MDB / IMPORTAR_MDB`) y sincroniza vistas al instante vía `SisproSyncHub`.
2. **Optimización de Recursos GDI+, Rendimiento y Estabilidad**:
   - Eliminadas todas las asignaciones transitorias `new Font(...)` en `CambiasoTheme`, `MdbImportDialog`, `MainForm`, `SplashForm` y `UsersForm` mediante fuentes estáticas cacheadas en `CambiasoTheme`.
   - Añadido `Dispose` explícito de `ToolTip` en `MdbImportDialog` y eliminado el único método no-evento `async void` (`BrowseMdbFileAsync`).
   - Versionado dinámico leído desde metadatos de ensamblado en `MainForm`, `SplashForm` y `PrintService`.
3. **Auditoría y Consistencia de Interfaz**:
   - Incorporados los módulos **`Perfiles`** e **`Importación MDB`** al selector de filtros de `AuditLogDialog`.
   - Verificados botones, íconos vectoriales Lucide (`56` íconos incl. `AppIcon.Upload`), layouts sin recortes y capturas actualizadas (`docs/screenshots/00`..`15`).
4. **Gráficas y Descripciones Simplificadas del Instalador (`v4.5`)**:
   - Regenerado `installer/Assets/wizard_banner.bmp` (`164×314`, 24 bpp) con versión **`Versión 4.5`** y descripción corporativa simplificada.
   - Simplificadas descripciones internas en `Directory.Build.props` e `installer/Setup_SISPRO.iss` y corregida la detección de versión de Inno Setup 7 en `scripts/Build-Release.ps1` y `.github/workflows/release.yml`.

---

## 3. Verificación Técnica y QA

- **Compilación (`TreatWarningsAsErrors=true`)**: `Debug` (`0` errores, `0` advertencias) · `Release` (`0` errores, `0` advertencias).
- **Seguridad NuGet**: `0` vulnerabilidades en los 5 proyectos (`dotnet list SISPRO.sln package --vulnerable --include-transitive`).
- **Pruebas Automatizadas (`Release`)**: **`74 / 74` aprobadas**, `0` fallidas, `0` omitidas (`9 s`).
- **Instalador `SISPRO_Setup_v4.5.0_win-x64.exe` (Inno Setup `7.1.1`)**:
  - Tamaño: `57.968.544` bytes
  - SHA-256: `13E206BFAFA1569AD1810129502AA80D3454C3595614A9E442DB5DECDD6435DA`
- **Prueba real de ciclo de vida (`C:\Temp\SISPRO_QA_v450`)**:
  - Instalación base `v4.4.0`: Exit `0` (`EXE_VER=4.4.0.0`, `DB_HASH=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`).
  - Actualización silenciosa `v4.4.0 → v4.5.0`: Exit `0` (`EXE_VER=4.5.0.0`, `DB_PRESERVED=True`).
  - Desinstalación silenciosa: Exit `0` (`EXE_EXISTS=False`, `DB_EXISTS=True`, limpieza QA completa).
