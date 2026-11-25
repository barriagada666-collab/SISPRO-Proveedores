# Informe de Incidencias y Validación — SISPRO v4.6.3

**Fecha:** 25-11-2026  
**Versión validada:** `4.6.3` (`AssemblyVersion` / `FileVersion`: `4.6.3.0`)  
**Plataforma:** `.NET 10` (`C# 14` WinForms `net10.0-windows10.0.19041.0`) · SQLite 3 (WAL) · Inno Setup `7.1.1`  
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
| Total de incidencias detectadas durante el ciclo | **6** |
| Total de advertencias finales (`Debug` + `Release` + analizadores) | **0** |
| Vulnerabilidades NuGet (directas y transitivas en los 5 proyectos) | **0** |
| Supresiones artificiales (`NoWarn`, `#pragma warning disable`, `Skip=`) | **0** |
| Incidencias corregidas de causa raíz | **6** |
| Incidencias pendientes | **0** |
| Regresiones pendientes | **0** |

---

## 3. Detalle de Incidencias Detectadas y Corregidas

### INC-01 — Llamada a método inexistente `ExportSanitizer.SanitizeText` en `WordExportService` (`CS0117`)
- **Gravedad:** Alta (Error de compilación en `Sispro.Services`)
- **Módulo / Archivo / Método / Línea:** `Sispro.Services` · `src/Sispro.Services/WordExportService.cs` · `ComputeProportionalWidthPcts` · línea `373`
- **Causa raíz:** Invocación a `ExportSanitizer.SanitizeText` en lugar del método real `ExportSanitizer.SanitizeXmlText`.
- **Corrección aplicada:** Reemplazo directo por `ExportSanitizer.SanitizeXmlText(...)`.
- **Validación antes / después:** `Debug` pasó de `1` error `CS0117` a `0` errores en `Sispro.Services`.

### INC-02 — Uso de palabra clave contextual `field` de C# 14 dentro de descriptor `get` (`CS9273` / `CS9258` / `CS1503`)
- **Gravedad:** Alta (Error de compilación en `Sispro.UI`)
- **Módulo / Archivo / Propiedad / Línea:** `Sispro.UI` · `src/Sispro.UI/Theme/GridExportColumnHeaderController.cs` · `SelectedFields` · líneas `76-80`
- **Causa raíz:** En `.NET 10` / `C# 14` (`LangVersion=latest`), el identificador `field` dentro de un descriptor de propiedad referencia el campo de respaldo sintetizado.
- **Corrección aplicada:** Renombrada la variable de iteración a `exportField`.
- **Validación antes / después:** Eliminados los errores `CS9273`, `CS9258` y `CS1503` sin supresiones.

### INC-03 — Comparación de tipo incompatible `SortOrder` vs `ListSortDirection` (`CS0019`)
- **Gravedad:** Alta (Error de compilación en `Sispro.UI`)
- **Módulo / Archivo / Método / Línea:** `Sispro.UI` · `src/Sispro.UI/Theme/GridExportColumnHeaderController.cs` · `SortColumnProgrammatically` · línea `405`
- **Causa raíz:** `SortGlyphDirection` es de tipo `System.Windows.Forms.SortOrder`, mientras se comparaba con `ListSortDirection.Ascending`.
- **Corrección aplicada:** Corregida la comparación a `SortOrder.Ascending`.
- **Validación antes / después:** Eliminado `CS0019`; verificado ordenamiento ascendente y descendente en prueba STA UI.

### INC-04 — Captura temprana de `_searchExportColumns` en lambda de constructor (`CS8602`)
- **Gravedad:** Media (Error por nulabilidad estricta con `TreatWarningsAsErrors=true`)
- **Módulo / Archivo / Método / Línea:** `Sispro.UI` · `src/Sispro.UI/Forms/WorkOrdersForm.cs` · constructor `.ctor` · línea `469`
- **Causa raíz:** La suscripción en línea de `_btnExportSearch.Click` referenciaba `_searchExportColumns` antes de su asignación en el constructor.
- **Corrección aplicada:** Extraído el método privado `ExportSearchResultsList()` invocado desde el evento `Click`.
- **Validación antes / después:** `Sispro.UI` compiló con `0` errores y `0` advertencias en `Debug` y `Release`.

### INC-05 — Distribución de anchos PDF en 10 columnas y ergonomía visual de encabezados en 1280×800
- **Gravedad:** Media (Hallazgo funcional y de UX detectado por prueba automatizada e inspección visual de capturas)
- **Módulo / Archivo / Métodos:** `Sispro.Services/PdfExportService.cs` (`ComputeSmartColumnWidths`) y `Sispro.UI/Theme/GridExportColumnHeaderController.cs` (`OnCellPainting`, `HitTestHeaderCheckBox`) + `Sispro.UI/Forms/WorkOrdersForm.cs`
- **Causa raíz:**
  1. En PDF de 10 columnas, las columnas estructuradas (`N° OT`, `Fecha`, `Cant.`, `Valor Neto`) recibían ancho excedente innecesario restando espacio útil a `Detalle del Trabajo` y `Observaciones`.
  2. En la grilla de Búsqueda de OT a `1380×840`, un checkbox horizontal junto al título reducía el espacio de texto en columnas estrechas (`Cant.`, `Obs.`, `Valor Neto`).
- **Corrección aplicada:**
  1. `ComputeSmartColumnWidths` asigna ancho exacto acotado a columnas estructuradas (`Date`, `Integer`, `Currency`) y destina el máximo espacio horizontal a `Detalle`, `Observaciones` y `Proveedor`.
  2. `GridExportColumnHeaderController` adoptó diseño moderno de **dos zonas (`48 px`)**: título completo arriba (zona de ordenamiento) + checkbox redondeado cyan centrado abajo (zona de selección para exportar), y se ubicó `BtnSearchExportColumns` (`Campos (10/10)`) en la barra CRUD sin saltos de línea.
- **Validación antes / después:** Inspección visual confirmada de `docs/screenshots/12_busqueda_ot_masiva.png` (10/10 encabezados sin recorte) y de muestra PDF renderizada (`100 %` del detalle y observaciones visibles, `0` truncamientos).

### INC-06 — Alineación de etiqueta de perfil rápido `Solo campos esenciales` y verificación de texto PDF
- **Gravedad:** Baja (Inconsistencia de rótulo UX y falsos positivos de bytes JPEG en aserción de prueba)
- **Módulo / Archivo:** `src/Sispro.UI/Forms/WorkOrdersForm.cs` (`ShowExportColumnsQuickMenu`) y `tests/Sispro.Tests/Functional/MultiFormatExportTests.cs`
- **Causa raíz:** El texto del menú omitía mencionar `Solicitante` (aunque `EssentialFields` incluye los 7 campos) y la aserción de ausencia de `…` escaneaba el stream binario JPEG del logotipo incrustado en el PDF en vez de los operadores de texto `(...) Tj`.
- **Corrección aplicada:** Actualizado el rótulo del menú con los 7 campos reales y refinada la aserción sobre operadores `Tj` del PDF.
- **Validación antes / después:** Suite completa pasó de `85 / 86` a **`86 / 86` pruebas aprobadas (`0` fallidas, `0` omitidas)**.

---

## 4. Auditoría de Recursos, Seguridad y Dependencias

- **Paquetes NuGet vulnerables (5 proyectos, transitivos incluidos):** `0` paquetes vulnerables.
- **Disposición de recursos UI (`ToolTip` / GDI+):** `GridExportColumnHeaderController` utiliza recursos GDI+ con `using` locales y fuentes estáticas cacheadas (`CambiasoTheme.GridHeaderFont`) + desuscripción en `WorkOrdersForm.Dispose`; `0` fugas.
- **Métodos `async void` en `src/`:** `0` ocurrencias.
- **Secretos o credenciales en código/documentación:** `0` hallazgos.

---

## 5. Pruebas Ejecutadas y Resultados

| Etapa | Resultado | Detalle |
|---|---|---|
| Compilación `Debug` (`--no-incremental`) | **APROBADA** | `0` errores, `0` advertencias |
| Compilación `Release` (`--no-incremental`) | **APROBADA** | `0` errores, `0` advertencias |
| Suite automatizada `xUnit` (`Release`) | **86 / 86 APROBADAS** | `0` fallidas, `0` omitidas (`31,4 s`) |
| Inspección visual UI y muestra PDF exportada | **APROBADA** | 10/10 columnas completas con checkboxes; PDF multipágina sin truncamiento |
| Publicación autocontenida `win-x64` (ReadyToRun) | **APROBADA** | `SISPRO.exe` (`FileVersion 4.6.3.0`) |
| Compilación instalador Inno Setup `7.1.1` | **APROBADA** | `SISPRO_Setup_v4.6.3_win-x64.exe` (`58.045.042` bytes) |
| QA aislado del instalador (Instalación silenciosa) | **APROBADA** | `exit=0`, `SISPRO.exe` `4.6.3.0` y `Data\sispro.db` presentes |
| QA aislado del instalador (Actualización) | **APROBADA** | `exit=0`, `Data\sispro.db` preservada bit a bit (`SHA-256` idéntico) |
| QA aislado del instalador (Desinstalación limpia) | **APROBADA** | `exit=0`, `SISPRO.exe` removido y `Data\sispro.db` conservada bit a bit |

---

## 6. Archivos Modificados en el Ciclo v4.6.3

- `Directory.Build.props` (versión `4.6.3`)
- `src/Sispro.Core/Dtos/ExportDtos.cs` (`WorkOrderExportField`, `WorkOrderExportFieldCatalog`)
- `src/Sispro.Services/DocumentExportService.cs` (selección dinámica de columnas en `BuildWorkOrdersReport` y `BuildSelectedWorkOrdersReport`, retrocompatible)
- `src/Sispro.Services/PdfExportService.cs` (ajuste multilínea completo sin `…`, encabezados completos, anchos inteligentes y paginación dinámica)
- `src/Sispro.Services/ExcelExportService.cs` (`WrapText`, anchos inteligentes y cálculo automático de altura de filas)
- `src/Sispro.Services/WordExportService.cs` (tabla de anchos proporcionales fijos `5000 Pct`, alineación superior y soporte multilínea `\n`)
- `src/Sispro.UI/Theme/CambiasoTheme.cs` (`GridHeaderFont`)
- `src/Sispro.UI/Theme/GridExportColumnHeaderController.cs` (controlador moderno de checkboxes en encabezados de columnas)
- `src/Sispro.UI/Theme/ExportUiHelper.cs` (soporte de campos dinámicos y exportación Word `.docx` en seleccionadas)
- `src/Sispro.UI/Forms/WorkOrdersForm.cs` (integración de checkboxes en encabezados de Búsqueda de OT y botón rápido `Campos (N/10)`)
- `tests/Sispro.Tests/Functional/MultiFormatExportTests.cs` (3 nuevas pruebas integrales: selección dinámica, ajuste completo sin recortes y UI STA de checkboxes en encabezados)
- `installer/Setup_SISPRO.iss`, `installer/Assets/LICENCIA_SISPRO_ES.txt`, `installer/Assets/INFO_INSTALADOR_ES.txt`, `installer/Assets/wizard_banner.bmp`
- `docs/screenshots/*`, `docs/INFORME_ENTREGA_v4.6.3.md`, `docs/evidencias/v4.6.3/*`, `CHANGELOG.md`, `README.md`

---

## 7. Integridad del Artefacto de Producción

- **Instalador:** `SISPRO_Setup_v4.6.3_win-x64.exe` (`58.045.042` bytes)
- **SHA-256:** `FB77252A9A03774B965A9CA9A32C978857E26C93721248D0516C9C4DF86FFA00`
- **Nota de privacidad:** los logs crudos de ejecución con rutas locales del equipo se conservaron exclusivamente fuera del repositorio y del instalador; en esta carpeta se incluyen únicamente los registros sanitizados y verificables.
