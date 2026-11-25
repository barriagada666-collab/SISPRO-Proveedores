# Informe de Entrega — SISPRO v4.6.0 (.NET 10)

**Fecha:** 25-11-2026  
**Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
**Plataforma:** `.NET 10` (`C#` WinForms `net10.0-windows10.0.19041.0`) · SQLite 3 (WAL) · Inno Setup `7.1.1`

---

## Novedades Principales en v4.6.0

### 1. Reorganización del Menú Principal y Centralización en Mantenedores
- **Editor de Árbol**, **Configuración** y **Mi Clave** centralizados como pestañas dedicadas dentro de **Mantenedores** junto a `Proveedores`, `Solicitantes`, `Firmas` y `Árbol de Activos`.
- Retirado **Editor de Árbol** como acceso independiente de la barra lateral principal.
- Control estricto RBAC mantenido en UI y backend: **Mi Clave** accesible para cualquier usuario autenticado; catálogos según `MenuMantenedor` o `Administrador`; **Editor de Árbol** y **Configuración** exclusivos de `Administrador`.

### 2. Barra Superior Derecha Simplificada
- Retirados `Nueva OT`, `Configuración` y `Mi Clave` de la barra superior derecha, dejando únicamente **Cambiar usuario**, **Acerca de** y **Cerrar sesión**.
- **Cambiar usuario**: permite cambiar la cuenta activa sin cerrar la aplicación, solicitando credenciales y refrescando inmediatamente permisos, vistas en caché, cabecera, barra de estado y bitácora (`CAMBIO_USUARIO`).
- **Acerca de**: ventana modal informativa alimentada dinámicamente desde los metadatos centralizados del ensamblado (`AppMetadata` / `Directory.Build.props`): nombre del software, versión (`4.6.0`), fecha de versión (`25-11-2026`) y empresa desarrolladora.

### 3. Eliminación de `Generar / Imprimir OT` y `Exportar / Imprimir`
- Retirada la pestaña `Generar / Imprimir OT`, el botón `Exportar / Imprimir OT`, sus entradas de menú contextual y los permisos asociados (`ImpresionOt = false`), conservando intactos los servicios compartidos de exportación y Vista Previa.

### 4. Control de Visibilidad de Valor Neto en OT Externa
- Nuevo checkbox **Mostrar valor neto en OT externa** (`OrdenesTrabajo.mostrar_valor_neto`, migración idempotente no destructiva con valor por defecto `1`).
- Oculta/muestra `Valor Neto ($)` inmediatamente sin recargar, no exige ingreso cuando está desmarcado, **conserva intacto `valor_neto` en SQLite** sin borrar datos económicos, restaura automáticamente el estado guardado al abrir/editar OTs existentes y omite la sección `VALOR NETO` en plantillas PDF, Word, Excel, CSV e impresión/Vista Previa cuando está desmarcado.

### 5. Configuración del Sistema (`Probar`, `Aplicar`, `Guardar`) Persistida en BD
- Nueva tabla centralizada `ConfiguracionSistema` en SQLite (idempotente) cargada automáticamente al iniciar la aplicación.
- **Probar**: valida parámetros, RUT y compatibilidad/integridad SQLite (`PRAGMA quick_check`) sin alterar la configuración activa ni la BD.
- **Aplicar**: aplica temporalmente en la sesión en memoria (mostrando indicador `[Config. Temporal]` y botón `Revertir Temp.`) sin sobrescribir la BD.
- **Guardar**: solicita confirmación, persiste centralizadamente en `ConfiguracionSistema` y `settings.json`, registra auditoría (`GUARDAR_CONFIGURACION`), publica sincronización vía `SisproSyncHub` y sobrevive cierres de sesión y reinicios.

### 6. Búsqueda de OT — CRUD Completo (Externa e Interna)
- **Búsqueda de OT** como pestaña principal de `Trabajos Externos` con barra CRUD completa (**Nuevo**, **Consultar**, **Editar**, **Eliminar**).
- Soporte para OTs **Externa** e **Interna** (`OrdenesTrabajo.tipo_ot`, migración idempotente no destructiva con defecto `'Externa'`).
- **Consultar** (`WorkOrderDetailDialog`): visualización completa de datos generales, proveedor, solicitante, ruta de activo, ítems, observaciones, estado de visibilidad de valor neto, firmantes y acceso directo a Vista Previa.

---

## Verificación de Calidad

- **Compilación `Debug` y `Release`**: `0` errores, `0` advertencias (`TreatWarningsAsErrors=true`, `WarningLevel=9999`).
- **Suite Automatizada (`Release`)**: **`80 / 80` pruebas `xUnit` aprobadas** (`0` fallos, `0` omitidas), incluyendo `NavigationConfigNetValueAndCrudTests` y auditoría UI STA (`WinFormsUiAuditTests`).
- **Instalador Windows x64 (Inno Setup `7.1.1`)**: `SISPRO_Setup_v4.6.0_win-x64.exe` (`58.006.777` bytes).
- **Integridad `SHA-256`**: `34FDA6DE164035C25168837DFD55B63C53A442285DF36A65F6C29DF039472958`.
