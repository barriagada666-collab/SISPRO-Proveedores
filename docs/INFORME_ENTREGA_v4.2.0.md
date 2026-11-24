# Informe de Entrega — SISPRO v4.2.0 (.NET 10)

> **Proyecto:** SISPRO — Sistema Integral de Gestión de Proveedores, Órdenes de Trabajo Externas, Solicitantes con Firmas Digitales, Aprobadores VB y Usuarios/Grupos  
> **Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
> **Versión:** `4.2.0` (`v4.1.0 → v4.2.0`)  
> **Fecha de Entrega:** 24-11-2026  
> **Repositorio GitHub:** https://github.com/barriagada666-collab/SISPRO-Proveedores

---

## 1. Resumen Ejecutivo

En **SISPRO v4.2.0** se implementó la gestión integral de **Solicitado Por** con **Firma Digital** (`PNG`, `JPG` o `PDF`), la identificación visible en tiempo real de quién posee y ejerce los vistos buenos **VB Supervisor (`VBS`)** y **VB Jefatura (`VBJ`)**, la focalización del **Dashboard** en **Nueva OT Externa**, y la eliminación completa de los módulos **Facturas y Guías** y **Mantención Industrial** conservando intactos todos los datos históricos de Trabajos Externos, Proveedores, Activos, Solicitantes, Usuarios y Grupos.

Además, durante la auditoría y validación en Release se detectó y resolvió una condición de carrera GDI+ en `IconCatalog` cuando múltiples hilos STA acceden de forma simultánea a bitmaps de íconos Lucide (`Object is currently in use elsewhere`), verificándose estabilidad total en 3 corridas consecutivas (`62/62`).

---

## 2. Requerimientos Cumplidos y Mejoras Arquitectónicas

| # | Requerimiento | Solución Implementada (`v4.2.0`) |
|---|---|---|
| **1** | **Gestión de "Solicitado Por" y Firma Digital (`PNG`, `JPG`, `PDF`)** | Control compartido `RequesterManagerControl` expuesto en pestaña dedicada `Solicitados Por y Firmas Digitales` de `UsersForm` y en `Solicitantes y Firmas` de `MaintainersForm`; adjunto y vista previa en vivo al crear/editar un usuario marcado `Solicitante OT`; validación `PNG`/`JPG`/`PDF` (máx. 10 MB) y renderizado de página 1 PDF a miniatura PNG mediante `Windows.Data.Pdf.PdfDocument` en `SignatureFileService` (`net10.0-windows10.0.19041.0`) sin paquetes vulnerables; migración idempotente en `SchemaInitializer` (`firma_bytes`, `firma_nombre`, `firma_tipo`, `firma_preview_png`) e incrustación en Excel/PDF/Word e impresión. |
| **2** | **Identificación visible de aprobadores `VB Supervisor` y `VB Jefatura`** | Franja `APROBADORES OT VIGENTES` y columnas `VBS` / `VBJ` / `Solicitante / Firma` en `UsersForm`; resumen de aprobadores vigentes y columnas `VBS` / `VBJ` en Dashboard (`MainForm`); etiquetas detalladas `✓ Aprobado por <Usuario> (<fecha>)` o lista de autorizados en `WorkOrdersForm`; columnas enriquecidas en Búsqueda OT y exportaciones. |
| **3** | **Eliminación de Facturas y Guías y Mantención Industrial** | Eliminados módulos UI (`InvoicesForm`, `MaintenanceHubForm`), servicios (`InvoiceService`, `MaintenanceService`), repositorios (`InvoiceRepository`, `MaintenanceRepository`), DTOs (`MaintenanceDtos`), permisos UI y filtros obsoletos, conservando intactos `data/sispro.db` (`7.284` OTs externas, `214` proveedores activos, `1.824` nodos / `1.823` activos, `11` solicitantes históricos / `9` activos, `4` usuarios, `5` grupos). |
| **4** | **Dashboard centrado en Nueva OT Externa y modernización UI** | Eliminados botón `"Explorar BD Mantención"` y KPIs internos; incorporado banner Hero degradado con botón dorado principal `Nueva OT Externa` (`BtnHeroNewExternalOt`) y 5 KPIs externos clicables (`OTs Externas`, `Proveedores`, `Activos Reales`, `Pendientes VB`, `Solicitantes / Firmas`); estabilidad multihilo GDI+ en `IconCatalog` por `ManagedThreadId`. |

---

## 3. Verificaciones Reales y Métricas de Calidad

- **Compilación `Debug` (`SISPRO.sln`):** **0 errores**, **0 advertencias** (`TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`).
- **Compilación `Release` (`SISPRO.sln`):** **0 errores**, **0 advertencias**.
- **Auditoría NuGet (`dotnet list SISPRO.sln package --vulnerable --include-transitive`):** **0 paquetes vulnerables** en los 5 proyectos (`Sispro.Core`, `Sispro.Data`, `Sispro.Services`, `Sispro.UI`, `Sispro.Tests`).
- **Suite Automatizada `xUnit` (`dotnet test SISPRO.sln -c Release --no-build`):** **62 / 62 pruebas aprobadas** (0 fallidas, 0 omitidas), confirmadas además en **3 corridas consecutivas** sin flakiness.
- **Capturas UI verificadas:** 10 capturas actualizadas en [docs/screenshots](../README.md#galer%C3%ADa-visual) incluyendo `02_dashboard.png`, `03_ordenes_trabajo.png`, `06_usuarios_rbac.png`, `07_solicitantes_firmas.png` y `10_ot_externa.png` (sin `04_facturas_guias.png` ni `07_mantencion_industrial.png`).
- **Instalador Inno Setup `6.7.3`:**
  - Artefacto: `installer/Output/SISPRO_Setup_v4.2.0_win-x64.exe` (**57.695.222 bytes** / **55,02 MiB**)
  - `SHA-256`: `28A697DDB61E263D845DECBC7CD00BA3CD825403231BF43BEAF33CC77C4D2ECE`
  - Prueba real silenciosa (`v4.1.0 → v4.2.0 → arranque SISPRO.exe → desinstalación` en `C:\Temp\SISPRO_V42_VERIFY`):
    - Instalación `v4.1.0`: `V41_EXIT=0`, `FileVersion=4.1.0.0`
    - Actualización `v4.2.0`: `UPG_EXIT=0`, `FileVersion=4.2.0.0`, `DB_PRESERVED=True`, `SENTINEL=True`
    - Ejecución real post-instalación: `LAUNCH_ALIVE=True`, `RESPONDING=True`
    - Desinstalación: `UNINST_EXIT=0`, `EXE_REMOVED=True`, `DB_AFTER_UNINST=True` (`uninsneveruninstall`).
