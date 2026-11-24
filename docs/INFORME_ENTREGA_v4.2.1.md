# Informe de Entrega — SISPRO v4.2.1 (.NET 10)

> **Proyecto:** SISPRO — Sistema Integral de Gestión de Proveedores, Trabajos Externos, Solicitantes con Firmas Digitales y Usuarios/Grupos  
> **Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
> **Versión:** `4.2.1` (`v4.2.0 → v4.2.1`)  
> **Fecha de Entrega:** 24-11-2026  
> **Repositorio GitHub:** https://github.com/barriagada666-collab/SISPRO-Proveedores

---

## 1. Resumen Ejecutivo

**SISPRO v4.2.1** rediseña de forma integral todos los documentos exportados e impresos de Órdenes de Trabajo (`PDF`, `Word`, `Excel`, `Vista Previa`, `Impresión` y `CSV`) y actualiza la pestaña `Generar / Imprimir OT` para permitir elegir libremente las firmas al pie de cada documento.

---

## 2. Mejoras Implementadas en `v4.2.1`

| # | Requerimiento | Implementación Verificada (`PDF`, `Word`, `Excel`, `Impresión`) |
|---|---|---|
| **1** | **N° de OT grande y destacado en parte superior derecha** | Tarjeta/insignia dorada Cambiaso (`OT N°` / `OT EXTERNA N°`) en la esquina superior derecha del banner/encabezado de los 4 formatos (`20pt`–`22pt` Bold en PDF/Word/Excel, `19pt` Bold en Impresión). |
| **2** | **Datos del Solicitante completos (`Nombre Apellido`, correo, teléfono)** | `WorkOrderRepository` carga `nombre_sol`, `ape_pat_sol`, `ape_mat_sol` exponiendo `WorkOrder.SolicitanteDisplayNombre` (`Nombre Apellido`), reflejado también en `WorkOrdersForm` y en los 4 exportadores + CSV. |
| **3** | **Mover `Activo` a Datos del Solicitante** | `ACTIVO` se retiró de `DATOS DEL PROVEEDOR` y pasó a `DATOS DEL SOLICITANTE` con ajuste automático de línea/alto para rutas largas sin truncamientos en los 4 formatos y en pantalla. |
| **4** | **Eliminar líneas `VB Supervisor` / `VB Jefatura` y columna `Guía / Estado` / `Valor / Ref.`** | Removidas por completo las líneas de Vistos Buenos al pie de la OT individual, `Estado:` en cabecera, `"Registrado"`, `"Estado"` / `"Guía / Estado"` y `"Valor / Ref."` en la tabla de ítems, ampliando el ancho útil del detalle del trabajo. |
| **5** | **Firmas al pie flexibles + Solicitante como `Técnico`** | `SignatureOption` convertido a `[Flags]` (`SignatureOptionExtensions`), compositor único `WorkOrderSignatureLayout` e interfaz con 3 casillas independientes (`Jefatura`, `Técnico (Solicitante)`, `Supervisor`) en `WorkOrdersForm`. La firma del solicitante se etiqueta como `Técnico` en medio de Jefatura y Supervisor; desmarcar todas las casillas deja el pie completamente vacío sin rayas ni cargos residuales. |
| **6** | **Alineación UI de `Generar / Imprimir OT`** | Reemplazo del layout apilado por `TableLayoutPanel` alineado (`CreatePrintFieldsGrid` + fila espaciadora final) mostrando sin recortes los 3 checkboxes y todos los campos incluyendo `Activo`. |

---

## 3. Verificaciones Reales y Métricas de Calidad

- **Compilación `Debug` y `Release` (`SISPRO.sln`):** **0 errores**, **0 advertencias**.
- **Auditoría NuGet (`dotnet list SISPRO.sln package --vulnerable --include-transitive`):** **0 paquetes vulnerables** en los 5 proyectos.
- **Suite Automatizada `xUnit` (`Release`):** **`62 / 62` aprobadas** (0 fallidas, 0 omitidas), incluyendo validaciones multi-formato sobre documentos generados con y sin firmas al pie, más verificación visual directa de `ot_14828_3firmas.pdf`, `ot_14828_sinfirmas.pdf`, `print_14828_3firmas.png` y [11_generar_imprimir_ot.png](file:///D:/archivos%20qlabel/Proveedores/docs/screenshots/11_generar_imprimir_ot.png).
- **Instalador Inno Setup `6.7.3`:**
  - Artefacto: `installer/Output/SISPRO_Setup_v4.2.1_win-x64.exe` (**57.722.069 bytes** / **55,05 MiB**)
  - `SHA-256`: `6C68EAC97006A10FEBC5CE1167A55BAA6572D86FB689286266723710B14802B5`
  - Prueba real silenciosa (`v4.2.0 → v4.2.1 → SISPRO.exe → desinstalación` en `C:\Temp\SISPRO_V421_VERIFY`):
    - Instalación `v4.2.0`: `V420_EXIT=0`, `FileVersion=4.2.0.0`
    - Actualización `v4.2.1`: `UPG_EXIT=0`, `FileVersion=4.2.1.0`, `DB_PRESERVED=True`, `SENTINEL=True`
    - Ejecución real post-instalación: `LAUNCH_ALIVE=True`, `RESPONDING=True`
    - Desinstalación: `UNINST_EXIT=0`, `EXE_REMOVED=True`, `DB_AFTER_UNINST=True`.
