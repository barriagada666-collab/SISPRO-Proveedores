# Informe de Entrega — SISPRO v4.6.3 (.NET 10)

**Fecha:** 25-11-2026  
**Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
**Plataforma:** `.NET 10` (`C#` WinForms `net10.0-windows10.0.19041.0`) · SQLite 3 (WAL) · Inno Setup `7.1.1`

---

## Novedades Principales en v4.6.3

### 1. Selección Dinámica de Campos a Exportar con CheckBoxes sobre Encabezados (Búsqueda de OT)
- **CheckBoxes vectoriales modernos directamente en los encabezados de columna (`GridExportColumnHeaderController`, `WorkOrdersForm`)**:
  - Cada una de las 10 columnas visibles de **Búsqueda de OT** (`N° OT`, `Fecha`, `Proveedor`, `Solicitante`, `Cant.`, `Detalle`, `Obs.`, `Valor Neto`, `Estado` y `Firmas`) incorpora un encabezado interactivo de dos zonas (`48 px`): título completo en la zona superior (para ordenar ascendente/descendente) y casilla redondeada interactiva en la zona inferior (para incluir o excluir la columna al exportar sin alterar el ordenamiento).
  - Retroalimentación visual inmediata: columnas incluidas en azul corporativo Cambiaso con casilla cyan y barra indicadora inferior; columnas excluidas en tono pizarra atenuado; tooltip dinámico por columna y cursor interactivo.
- **Botón rápido `Campos (N/10)`**:
  - Permite alternar perfiles predefinidos (**Marcar todas las columnas**, **Solo campos esenciales**), marcar/desmarcar columnas individuales e incorporar campos opcionales adicionales (**Tipo OT** y **Ruta de Activo**).
- **Integración total en exportaciones (`DocumentExportService`, `ExportUiHelper`)**:
  - Tanto **Exportar seleccionadas** (ahora en los 4 formatos: `.xlsx`, `.pdf`, `.docx`, `.csv`) como **Exportar Lista** respetan exactamente los campos marcados y validan que al menos una columna esté seleccionada.

### 2. Ajuste Automático Completo de Celdas y Textos sin Recortes (`...`) en Todos los Formatos
- **PDF Corporativo (`PdfExportService`)**:
  - Eliminado el truncamiento fijo de 1 línea con puntos suspensivos (`…`).
  - Nueva distribución inteligente de anchos (`ComputeSmartColumnWidths`): asigna ancho exacto a columnas estructuradas (`N° OT`, `Fecha`, `Cant.`, `Valor Neto`) y concentra el máximo espacio horizontal en `Detalle`, `Observaciones`, `Proveedor` y `Firmas`.
  - Ajuste multilínea completo (`WrapTextToWidth`) tanto en encabezados (`Valor Neto`, `Observaciones` siempre íntegros) como en celdas de datos, altura de fila calculada dinámicamente, divisores verticales suaves y paginación multipágina real (`Página X de Y`).
- **Excel (`.xlsx`, `ExcelExportService`)**:
  - Habilitado `WrapText = true` en encabezados y celdas de datos con alineación vertical superior, anchos calculados según contenido y altura automática por fila para visualizar íntegramente detalles largos y observaciones.
- **Word (`.docx`, `WordExportService`)**:
  - Tabla con `TableLayoutValues.Fixed` y anchos proporcionales exactos (`5000 Pct` = 100 %), soporte multilínea (`\n`) y alineación superior para lectura limpia sin recortes.

---

## Verificación de Calidad

- **Compilación `Debug` y `Release` (`--no-incremental`)**: **`0` errores, `0` advertencias** (`TreatWarningsAsErrors=true`, `WarningLevel=9999`, `0` supresiones).
- **Seguridad NuGet**: `0` paquetes vulnerables en los 5 proyectos (incluyendo transitivos).
- **Suite Automatizada (`Release`)**: **`86 / 86` pruebas `xUnit` aprobadas** (`0` fallidas, `0` omitidas).
- **Validación del Instalador (Inno Setup `7.1.1`)**: instalación silenciosa (`FileVersion 4.6.3.0`), preservación bit a bit de `Data\sispro.db` en actualización y desinstalación limpia verificadas con `0` errores.
- **Instalador Windows x64**: `SISPRO_Setup_v4.6.3_win-x64.exe` (`58.045.042` bytes).
- **Integridad `SHA-256`**: `FB77252A9A03774B965A9CA9A32C978857E26C93721248D0516C9C4DF86FFA00`.
