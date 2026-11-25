# Informe de Entrega — SISPRO v4.6.1 (.NET 10)

**Fecha:** 25-11-2026  
**Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
**Plataforma:** `.NET 10` (`C#` WinForms `net10.0-windows10.0.19041.0`) · SQLite 3 (WAL) · Inno Setup `7.1.1`

---

## Novedades Principales en v4.6.1

### 1. Catálogo Completo de 6 Estados de Órdenes de Trabajo
- Ampliación de `WorkOrderStatus` conservando compatibilidad total con los registros históricos de SQLite:
  - **Abierta** (`0`)
  - **En Curso** (`1`)
  - **Terminada** (`2`)
  - **Anulada** (`3`)
  - **Modificada** (`4`)
  - **Cerrada** (`5`)
- Presentación uniforme y colores semánticos en Búsqueda de OT, Dashboard, Consulta Completa de OT (`WorkOrderDetailDialog`) y exportaciones (`.xlsx`, `.pdf`, `.docx`, `.csv`).

### 2. Cambio Rápido de Estado con Clic Derecho (Individual y por Lotes)
- Nuevo submenú contextual **`Cambiar estado`** (`WorkOrderStatusUi` + `ContextMenuHelper.SubMenu`) con las 6 opciones, íconos vectoriales, color semántico y marca `(Actual)` sobre el estado vigente.
- Disponible mediante **clic derecho** en:
  - **Trabajos Externos → Búsqueda de OT** (incluye actualización masiva atómica cuando hay varias OTs marcadas con checkbox).
  - **Panel de Control (Dashboard) → Últimas Órdenes de Trabajo Registradas** (se añade además la columna `Estado`).
- Protegido por RBAC (`MenuOrdenesTrabajo && IngresoOt`), trazado en auditoría (`CAMBIAR_ESTADO_OT` / `CAMBIAR_ESTADO_OT_LOTE`) y sincronizado inmediatamente entre vistas mediante `SisproSyncHub`.

### 3. Selector de Estado al Ingresar y Editar OT + Filtro en Búsqueda
- Nuevo selector **`Estado:`** (`CmbEstadoEntry`) en **Ingreso / Edición de OT**, disponible tanto al crear una nueva orden (predeterminado en `Abierta`) como al cargar y modificar una orden existente (**Modificar** y **Guardar y Exportar**).
- Nuevo filtro combinable por **Estado** en **Búsqueda de OT**.

### 4. Información y Licencia Corporativa en Español en el Instalador
- Reemplazada la licencia en inglés por el **Acuerdo de Licencia Corporativo en español** (`installer/Assets/LICENCIA_SISPRO_ES.txt`) de Cambiaso Hnos S.A.C. — Departamento Mantención.
- Incorporada página previa de **Información de la Versión** (`installer/Assets/INFO_INSTALADOR_ES.txt`), textos actualizados en `Setup_SISPRO.iss` y banner `Versión 4.6.1` (`wizard_banner.bmp`).

---

## Verificación de Calidad

- **Compilación `Debug` y `Release`**: `0` errores, `0` advertencias (`TreatWarningsAsErrors=true`, `WarningLevel=9999`).
- **Suite Automatizada (`Release`)**: **`81 / 81` pruebas `xUnit` aprobadas** (`0` fallos, `0` omitidas), incluyendo `WorkOrderStatus_CreateEditRightClickMenuAndBatchUpdate_SupportsAllSixStatusesWithRbacAndAudit` y auditoría visual de pantallas.
- **Instalador Windows x64 (Inno Setup `7.1.1`)**: `SISPRO_Setup_v4.6.1_win-x64.exe` (`57.994.042` bytes).
- **Integridad `SHA-256`**: `0AFB5E7D908CF5B614886FAF226A57A957E34310C4BC259BF2C44FE59412D605`.
