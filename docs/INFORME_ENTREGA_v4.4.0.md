# Informe de Entrega — SISPRO v4.4.0 (.NET 10)

> **Proyecto:** SISPRO — Sistema Integral de Gestión de Proveedores, Trabajos Externos, Solicitantes con Firmas Digitales y Perfiles/Usuarios  
> **Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
> **Versión:** `4.4.0` (`v4.3.0 → v4.4.0`)  
> **Fecha de Entrega:** 24-11-2026  
> **Repositorio GitHub:** https://github.com/barriagada666-collab/SISPRO-Proveedores

---

## 1. Resumen Ejecutivo

**SISPRO v4.4.0** reorganiza y depura la interfaz operativa eliminando espacios recortados e incidencias GDI+ en `WorkOrdersForm`, retira `VB Supervisor` y `VB Jefatura` de formularios, grillas, filtros y reportes tabulares, habilita la selección de **Jefatura**, **Técnico** y **Supervisor** mediante CheckBox + lista desplegable conectada a **Personal y Usuarios**, habilita permanentemente **Guardar y Exportar** y **Vista Previa** en `Ingreso / Edición de OT`, agrega **"Visualizar OT (Vista Previa)"** al menú contextual de **clic derecho**, e incorpora un módulo CRUD dinámico completo para crear y administrar **Perfiles de Usuarios**.

---

## 2. Capacidades Implementadas y Verificadas en `v4.4.0`

| # | Requerimiento | Implementación Verificada |
|---|---|---|
| **1** | **Revisión de formularios, corrección de espacios y estabilidad GDI+** | Reemplazado `SplitContainer` por `TableLayoutPanel` de 2 columnas en [WorkOrdersForm.cs](file:///D:/archivos%20qlabel/Proveedores/src/Sispro.UI/Forms/WorkOrdersForm.cs), corregidos `WrapSignerSlot`, altos de cabecera en `Generar / Imprimir OT`, espaciado de botones en `Ingreso / Edición de OT`, alineación en [UserProfileManagerControl.cs](file:///D:/archivos%20qlabel/Proveedores/src/Sispro.UI/Controls/UserProfileManagerControl.cs) y ancho de `ID` + fila `"Cambiar clave 1er acceso"` en [UsersForm.cs](file:///D:/archivos%20qlabel/Proveedores/src/Sispro.UI/Forms/UsersForm.cs). |
| **2** | **Firmas con CheckBox + lista desde Personal y Usuarios** | `WorkOrderRepository.GetSignaturePersonnelCatalog()` combina y deduplica `Solicitantes` activos, `Usuarios` activos y `Mant_Personal` (134 registros en `data/sispro.db`). Los 3 firmantes seleccionados (`Jefatura`, `Técnico`, `Supervisor`) se persisten en `OrdenesTrabajo` (`firma_jefatura`, `firma_tecnico`, `firma_supervisor`) y se reflejan en PDF, Word, Excel, Impresión y Vista Previa (`WorkOrderSignatureLayout`). |
| **3** | **Eliminación de `VB Supervisor` y `VB Jefatura` de aprobaciones UI** | Retirados controles, filtros, banners, KPI y columnas `VBS` / `VBJ` en `WorkOrdersForm`, `UsersForm`, `MainForm`, `UserPermissionsDialog` y `DocumentExportService.BuildWorkOrdersReport`, preservando la tabla histórica `VistoBueno` en SQLite sin pérdida de datos. |
| **4** | **`Guardar y Exportar` siempre activo + `Vista Previa` en Ingreso/Edición** | `BtnSaveAndExportOt` permanece habilitado tanto para nueva OT como tras cargar una OT existente (actualiza y abre el menú de exportación), junto con el nuevo botón `BtnEntryPreview` (**Vista Previa**) directamente en el pie de `Ingreso / Edición de OT`. |
| **5** | **Visualización de OT con botón derecho del mouse** | Nueva acción **"Visualizar OT (Vista Previa)"** vía `ExportUiHelper.ShowWorkOrderPreview` en los menús contextuales de `MainForm` (Últimas OTs), `WorkOrdersForm` (`Búsqueda de OT` e ítems de `Ingreso / Edición de OT`) y `MaintainerDependenciesDialog`. |
| **6** | **Módulo CRUD Perfiles de Usuarios** | Nuevo servicio `UserProfileService`, repositorio `UserProfileRepository` (tabla `Niveles`), entidad `UserProfile` y vista `UserProfileManagerControl` en la tercera pestaña de `UsersForm`, con creación/edición/activación/eliminación de perfiles personalizados (`id_nivel >= 5`), protección de los 4 perfiles base del sistema y sincronización en vivo con asignación de usuarios y lotes (`BatchChangeProfile`). |

---

## 3. Verificaciones Reales y Métricas de Calidad

- **Compilación `Debug` y `Release` (`SISPRO.sln`):** **0 errores**, **0 advertencias** (`TreatWarningsAsErrors=true`, `Nullable=enable`).
- **Auditoría NuGet (`dotnet list SISPRO.sln package --vulnerable --include-transitive`):** **0 paquetes vulnerables** en los 5 proyectos (`Sispro.Core`, `Sispro.Data`, `Sispro.Services`, `Sispro.UI`, `Sispro.Tests`).
- **Suite Automatizada `xUnit` (`Release`):** **`68 / 68` aprobadas** (`0` fallidas, `0` omitidas, `9 s`), incluyendo [ProfilesSignersAndUiFeaturesTests.cs](file:///D:/archivos%20qlabel/Proveedores/tests/Sispro.Tests/Functional/ProfilesSignersAndUiFeaturesTests.cs) y protección ante excepciones UI en [TempTestDatabase.cs](file:///D:/archivos%20qlabel/Proveedores/tests/Sispro.Tests/TempTestDatabase.cs), más inspección visual directa de [02_dashboard.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/02_dashboard.png), [03_ordenes_trabajo.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/03_ordenes_trabajo.png), [11_generar_imprimir_ot.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/11_generar_imprimir_ot.png), [12_busqueda_ot_masiva.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/12_busqueda_ot_masiva.png), [06_usuarios_rbac.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/06_usuarios_rbac.png) y [14_perfiles_usuarios.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/14_perfiles_usuarios.png).
- **Instalador Inno Setup `6.7.3`:**
  - Artefacto: `installer/Output/SISPRO_Setup_v4.4.0_win-x64.exe` (**57.905.034 bytes**)
  - `SHA-256`: `AFD9B7605F593FCE458B041BFBF9442385B96D6A052B09A2685B977C42663876`
  - Prueba real silenciosa (`v4.3.0 → v4.4.0 → desinstalación` sobre `C:\Temp\SISPRO_QA_v440`):
    - Instalación `v4.3.0`: `V430_EXIT=0`, `EXE_VER=4.3.0.0`, `DB_HASH1=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`
    - Actualización `v4.4.0`: `V440_UPGRADE_EXIT=0`, `EXE_VER=4.4.0.0`, `DB_HASH2=F4123E08623EB1DDA8DAAE53073C26D764A525C577B5774A74268C4D47809654`, `DB_PRESERVED=True`
    - Desinstalación: `UNINS_EXIT=0`, `EXE_EXISTS=False`, `DB_EXISTS=True`, `QA_CLEANUP_DONE=True`.
