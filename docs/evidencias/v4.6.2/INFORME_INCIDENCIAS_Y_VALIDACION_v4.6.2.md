# Informe de Incidencias y Validación — SISPRO v4.6.2

**Fecha:** 25-11-2026  
**Versión validada:** `4.6.2` (`AssemblyVersion` / `FileVersion`: `4.6.2.0`)  
**Plataforma:** `.NET 10` (`C#` WinForms `net10.0-windows10.0.19041.0`) · SQLite 3 (WAL) · Inno Setup `7.1.1`  
**Política de compilación:** `TreatWarningsAsErrors=true`, `WarningLevel=9999`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`, `Nullable=enable`, **0 supresiones (`NoWarn` / `#pragma warning disable` / `SuppressMessage`)**.

---

## 1. Criterio de Aprobación Final

```text
ERRORES = 0
ADVERTENCIAS = 0
PRUEBAS FALLIDAS = 0
REGRESIONES PENDIENTES = 0
```

**Dictamen:** **APROBADO PARA PRODUCCIÓN Y PUBLICACIÓN** (todas las etapas respaldadas por registros de ejecución verificables).

---

## 2. Resumen Cuantitativo de Incidencias

| Métrica | Cantidad |
|---|---:|
| Total de errores detectados durante el ciclo | **2** |
| Total de advertencias detectadas (`Debug` + `Release` + analizadores) | **0** |
| Vulnerabilidades NuGet (directas y transitivas en los 5 proyectos) | **0** |
| Supresiones artificiales (`NoWarn`, `#pragma warning disable`, `Skip=`) | **0** |
| Incidencias corregidas de causa raíz | **2** |
| Incidencias pendientes | **0** |
| Regresiones pendientes | **0** |

---

## 3. Detalle de Incidencias Detectadas y Corregidas

### INC-01 — Pérdida de capitalización en alias de solicitantes deduplicados al importar MDB
- **Gravedad:** Media (Fallo funcional detectado por prueba automatizada)
- **Módulo / Archivo / Método / Línea:** `Sispro.Services` · `src/Sispro.Services/MdbImportService.cs` · `BuildAnalysis` · línea `609`
- **Causa raíz:** Al consolidar dos filas duplicadas de `Solicitantes` en el `.mdb` que diferían solo por mayúsculas/minúsculas (`"SolNuevo"` y `"solnuevo"`), el objeto fusionado asignaba `Alias = aliasKey` (del registro duplicado) en lugar de `prevReq.Alias` (del primer registro).
- **Corrección aplicada:** Se preservó `Alias = prevReq.Alias` en `MdbImportService.BuildAnalysis`.
- **Validación antes / después:**
  - Antes: `ApplyImport_RewriteAllFromMdb_ReplacesOperationalDataAtomicallyAndPreservesAdminUsersAndAssets` falló (`Expected: "SolNuevo", Actual: "solnuevo"`).
  - Después: `83 / 83` pruebas aprobadas (`0` fallos).

### INC-02 — Error `NETSDK1047` al publicar `win-x64` después de auditar paquetes NuGet
- **Gravedad:** Alta (Bloqueo de publicación autocontenida `win-x64`)
- **Módulo / Archivo / Línea:** `Sispro.UI` · `src/Sispro.UI/Sispro.UI.csproj` · líneas `2-5`
- **Causa raíz:** `dotnet list SISPRO.sln package --vulnerable --include-transitive` ejecuta una restauración implícita `AnyCPU` que sobrescribía `obj/project.assets.json` eliminando el destino `net10.0-windows10.0.19041.0/win-x64` cuando el proyecto no declaraba explícitamente `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`.
- **Corrección aplicada:** Se declaró `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` en `src/Sispro.UI/Sispro.UI.csproj`.
- **Validación antes / después:**
  - Antes: `dotnet publish ... -r win-x64 --no-restore` finalizó con `error NETSDK1047` (código `1`).
  - Después: `dotnet restore` + `dotnet list --vulnerable` + `Debug` + `Release` + `83` pruebas + `dotnet publish -r win-x64 --no-restore` + `ISCC.exe` completaron con código `0` y `0` advertencias.

---

## 4. Auditoría de Recursos, Seguridad y Dependencias

- **Paquetes NuGet vulnerables (5 proyectos, transitivos incluidos):** `0` paquetes vulnerables.
- **Disposición de recursos UI (`ToolTip` / GDI+):** `21 / 21` controles y diálogos con `ToolTip` verificados con `_toolTip.Dispose()` en `override void Dispose`.
- **Métodos `async void` en `src/`:** `0` ocurrencias.
- **Control RBAC en limpieza y reescritura MDB:** verificado por pruebas (`UnauthorizedAccessException` para no administradores en `AnalyzeMdb`, `ApplyImport`, `GetOperationalCounts` y `CleanOperationalDatabase`).

---

## 5. Pruebas Ejecutadas y Resultados

| Etapa | Resultado | Detalle |
|---|---|---|
| Compilación `Debug` (`--no-incremental`) | **APROBADA** | `0` errores, `0` advertencias |
| Compilación `Release` (`--no-incremental`) | **APROBADA** | `0` errores, `0` advertencias |
| Suite automatizada `xUnit` (`Release`) | **83 / 83 APROBADAS** | `0` fallidas, `0` omitidas (`13,1 s`) |
| Publicación autocontenida `win-x64` (ReadyToRun) | **APROBADA** | `SISPRO.exe` (`FileVersion 4.6.2.0`) |
| Compilación instalador Inno Setup `7.1.1` | **APROBADA** | `SISPRO_Setup_v4.6.2_win-x64.exe` (`58.014.333` bytes) |
| QA aislado del instalador (Instalación silenciosa) | **APROBADA** | `exit=0`, `SISPRO.exe` `4.6.2.0` y `Data\sispro.db` presentes |
| QA aislado del instalador (Actualización) | **APROBADA** | `exit=0`, `Data\sispro.db` preservada íntegra (`SHA-256` idéntico) |
| QA aislado del instalador (Desinstalación limpia) | **APROBADA** | `exit=0`, `SISPRO.exe` removido y `Data\sispro.db` conservada |

---

## 6. Archivos Modificados en el Ciclo v4.6.2

- `Directory.Build.props` (versión `4.6.2`)
- `src/Sispro.Core/Dtos/MdbImportDtos.cs` (`RewriteAllFromMdb`, `OperationalDatabaseCounts`, `MdbDatabaseCleanResult`, métricas de reescritura)
- `src/Sispro.Services/MdbImportService.cs` (`GetOperationalCounts`, `CleanOperationalDatabase`, modo transaccional `RewriteAllFromMdb`, preservación de casing en deduplicación)
- `src/Sispro.UI/Dialogs/MdbImportDialog.cs` (checkbox `Reescribir todo desde cero`, botón `Limpiar sispro.db`, confirmaciones explícitas y respaldo previo)
- `src/Sispro.UI/Sispro.UI.csproj` (`<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`)
- `tests/Sispro.Tests/Functional/MdbImportTests.cs` (pruebas integrales de limpieza, reescritura atómica, RBAC y auditoría UI STA)
- `installer/Setup_SISPRO.iss`, `installer/Assets/LICENCIA_SISPRO_ES.txt`, `installer/Assets/INFO_INSTALADOR_ES.txt`, `installer/Assets/wizard_banner.bmp`
- `docs/screenshots/*`, `docs/INFORME_ENTREGA_v4.6.2.md`, `docs/evidencias/v4.6.2/*`, `CHANGELOG.md`, `README.md`

---

## 7. Integridad del Artefacto de Producción

- **Instalador:** `SISPRO_Setup_v4.6.2_win-x64.exe` (`58.014.333` bytes)
- **SHA-256:** `D36400B36B971CC5599E5D8BCEB97E7A1309EB40BFBC554F1FA2EA0BC00B63DF`
- **Nota de privacidad:** los logs crudos de ejecución con rutas locales del equipo se conservaron exclusivamente fuera del repositorio y del instalador; en esta carpeta se incluyen únicamente los registros sanitizados y verificables.
