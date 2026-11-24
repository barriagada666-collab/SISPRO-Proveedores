# Informe de Entrega — SISPRO v4.3.0 (.NET 10)

> **Proyecto:** SISPRO — Sistema Integral de Gestión de Proveedores, Trabajos Externos, Solicitantes con Firmas Digitales y Usuarios/Grupos  
> **Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
> **Versión:** `4.3.0` (`v4.2.1 → v4.3.0`)  
> **Fecha de Entrega:** 24-11-2026  
> **Repositorio GitHub:** https://github.com/barriagada666-collab/SISPRO-Proveedores

---

## 1. Resumen Ejecutivo

**SISPRO v4.3.0** incorpora búsqueda y exportación masiva exclusiva de órdenes de trabajo seleccionadas, amplia el modelo de OT con **Observación**, **Valor Neto ($)** destacado y **persistencia de firmas al pie por orden**, extiende la selección por casillas y gestión masiva controlada a los **4 mantenedores** (`Proveedores`, `Solicitantes`, `Firmas` en pestaña dedicada y `Árbol de Activos`) e implementa un motor transaccional de **integridad referencial, reasignaciones y eliminación explícita con doble confirmación exclusiva para Administradores**.

---

## 2. Capacidades Implementadas y Verificadas en `v4.3.0`

| # | Requerimiento | Implementación Verificada |
|---|---|---|
| **1** | **Búsqueda y exportación masiva exclusiva de OT** | `WorkOrdersForm` + `GridCheckboxSelectionController<WorkOrderSearchRow>`: checkboxes por fila, casilla *"Seleccionar todos (página visible)"* con selección persistente al ordenar columnas o cambiar de página, contador en vivo, botón **Limpiar selección**, confirmación previa de cantidad y **Exportar seleccionadas** habilitado solo con selección activa. Exportación exclusiva sin duplicar ítems por OT en **Excel (`.xlsx`)**, **CSV (`.csv`)** y **PDF (`.pdf`)** mediante `DocumentExportService.BuildSelectedWorkOrdersReport` y `ExportUiHelper.ShowSelectedWorkOrdersExportMenu`. |
| **2** | **Observación de OT y Valor Neto ($) destacado** | Migración idempotente en `SchemaInitializer` agregando `observacion` y `valor_neto` en `OrdenesTrabajo`. Nuevos campos `TxtObservacionEntry` (bajo *Detalle*) y `TxtValorNetoEntry` (`15pt` Bold, `$ #,##0`, validación `>= 0` vía `WorkOrderService.TryParseNetValue`) reflejados en documentos individuales y exportación masiva. |
| **3** | **Selección persistente de firmas al pie** | Migración idempotente de `firmas_mask` (`SignatureOption` `[Flags]`, predeterminado `7`) sincronizado entre `Ingreso / Edición de OT`, `Generar / Imprimir OT`, base de datos SQLite y todos los exportadores (`.xlsx`, `.pdf`, `.docx`, `.csv`, impresión y vista previa). |
| **4** | **Checkboxes y acciones masivas en los 4 Mantenedores** | Pestañas **Proveedores**, **Solicitantes**, **Firmas** (`SignatureManagerControl` con vista previa `PNG`/`JPG`/`PDF`) y **Árbol de Activos** (`AssetTreePanel` / `AssetTreeEditorView`) equipadas con selección múltiple, **Consultar OT**, **Reasignar Rel.**, **Desactivar Sel.** y **Eliminar**, con actualización inmediata vía `SisproSyncHub`. |
| **5** | **Integridad referencial, reasignaciones y RBAC estricto** | `MaintainerDependenciesDialog` y `MaintainerBatchUiHelper`: consulta de OTs dependientes antes de modificar/eliminar, reasignación de proveedor, solicitante, firma, activo/subnodos (bloqueo de ciclos y tope de 10 niveles), desactivación preservando historial y borrado explícito de OTs asociadas con doble confirmación y restricción exclusiva para **Administradores** en interfaz y servicios (`AuthService.EnsureAdministrator`, `MaintainerService`, `AssetTreeService`, `WorkOrderService.DeleteManyExplicitly`). |

---

## 3. Verificaciones Reales y Métricas de Calidad

- **Compilación `Debug` y `Release` (`SISPRO.sln`):** **0 errores**, **0 advertencias** (`TreatWarningsAsErrors=true`, `Nullable=enable`).
- **Auditoría NuGet (`dotnet list SISPRO.sln package --vulnerable --include-transitive`):** **0 paquetes vulnerables** en los 5 proyectos (`Sispro.Core`, `Sispro.Data`, `Sispro.Services`, `Sispro.UI`, `Sispro.Tests`).
- **Suite Automatizada `xUnit` (`Release`):** **`65 / 65` aprobadas** (0 fallidas, 0 omitidas, 5 s), incluyendo [BulkOperationsAndExtendedWorkOrderTests.cs](file:///D:/archivos%20qlabel/Proveedores/tests/Sispro.Tests/Functional/BulkOperationsAndExtendedWorkOrderTests.cs) y auditoría STA ampliada en [WinFormsUiAuditTests.cs](file:///D:/archivos%20qlabel/Proveedores/tests/Sispro.Tests/UI/WinFormsUiAuditTests.cs), más inspección visual de todas las capturas actualizadas ([03_ordenes_trabajo.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/03_ordenes_trabajo.png), [12_busqueda_ot_masiva.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/12_busqueda_ot_masiva.png), [13_mantenedores_firmas.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/13_mantenedores_firmas.png), [09_editor_arbol.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/09_editor_arbol.png), [05_mantenedores.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/05_mantenedores.png)).
- **Instalador Inno Setup `6.7.3`:**
  - Artefacto: `installer/Output/SISPRO_Setup_v4.3.0_win-x64.exe` (**57.859.985 bytes**)
  - `SHA-256`: `C3F54BE3B15BE9D59670EC8C0A6A632346D6D09D8B7C5E128077A104F07F403E`
  - Prueba real silenciosa (`v4.2.1 → v4.3.0 → desinstalación` sobre `C:\Temp\SISPRO_QA_v430`):
    - Instalación `v4.2.1`: `INSTALL_421_EXIT=0`, `EXE_VER=4.2.1.0`, `DB_HASH=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`
    - Actualización `v4.3.0`: `UPGRADE_430_EXIT=0`, `EXE_VER=4.3.0.0`, `DB_HASH=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`, `DB_PRESERVED=True`
    - Desinstalación: `UNINSTALL_EXIT=0`, `EXE_EXISTS=False`, `DB_EXISTS=True`, `CLEANED_UP=True`.
