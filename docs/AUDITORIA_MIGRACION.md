# Informe de Auditoría Técnica y Migración VB6 → .NET 10

## 1. Resumen Ejecutivo

Se realizó una auditoría exhaustiva archivo por archivo del proyecto heredado **SISPRO / Proveedores** (Visual Basic 6, compilado en 2011) y su base de datos `OT_PROV.mdb`. Se identificaron **24 defectos críticos y medios** en seguridad, permisos RBAC, integridad de datos, funcionalidad incompleta, manejo de fechas, exportación a Excel y calidad de interfaz. Todos fueron corregidos en la migración completa a **C# sobre .NET 10 (`net10.0-windows`)** con base de datos **SQLite embebida**.

---

## 2. Inventario de Archivos Heredados y Limpieza

| Categoría | Archivos Originales | Disposición en .NET 10 |
|---|---|---|
| Proyecto y formularios activos VB6 | `Proveedores.vbp`, `index.frm`, `MDI.frm`, `tab_ot.frm`, `tab_fac.frm`, `form_usuario.frm` | Migrados a `Sispro.UI` (`LoginForm`, `MainForm`, `WorkOrdersForm`, `InvoicesForm`, `UsersForm`) + nuevo `MaintainersForm`. |
| Módulos VB6 activos | `conexion.bas`, `Utilitarios.bas`, `Transacciones.bas`, `Carga_nodos.bas`, `TAB_0.bas`, `TAB_1.bas`, `TAB_2.bas`, `TAB_0_FAC.bas`, `TAB_US_0.bas`, `exp_excel.bas`, `API_IMPRESORA.bas`, `Variables.bas` | Refactorizados en capas limpias `Sispro.Core`, `Sispro.Data` y `Sispro.Services`. |
| Código muerto / formularios huérfanos | `Form1.frm/.frx/.log`, `form_genra_ot.frm/.frx`, `frm_impresion.frm/.frx`, `frm_op.frm/.frx`, `frm_prov.frm/.frx`, `sel_imp.frm`, `Module1.bas`, `MDI.log`, `objetos provee.txt`, `TAB_1.bas` (vacío) | Eliminados del repositorio. La funcionalidad pendiente de `frm_prov` fue implementada completamente en `MaintainersForm`. |
| Binarios y dependencias heredadas | `Proveedores.exe`, `TABCTL32.OCX`, `Datos Exportados.xls`, `Proveedores.vbw`, `dbacceso.ini`, `db_acceso_arbol.ini` | Eliminados; reemplazados por compilación `.NET 10` nativa, configuración JSON (`settings.json`) y ClosedXML. |
| Recursos corporativos vigentes | `imagenes/logo mediano.jpg`, `imagenes/sispro.ico`, `Firmas/JR.jpg`, `Firmas/MS.JPG` | Incorporados en `src/Sispro.UI/Assets/`. |
| Base de datos histórica | `OT_PROV.mdb` | Respaldada localmente en `data/legacy/OT_PROV.mdb` (excluida de Git) y migrada íntegramente a `data/sispro.db`. |

---

## 3. Matriz de Hallazgos y Correcciones

### 3.1 Seguridad y Control de Acceso (RBAC)

| ID | Archivo VB6 | Hallazgo | Severidad | Solución en .NET 10 |
|---|---|---|---|---|
| SEC-01 | `index.frm` L44, L61 | Credenciales hardcodeadas por defecto en los campos de login (`L Reyes` / `wanderes`). | Crítica | Campos de inicio vacíos por defecto; verificado por prueba automatizada UI. |
| SEC-02 | `form_usuario.frm` | Datos personales y contraseñas de prueba precargados en todos los `TextBox`. | Alta | Campos limpios con validación de RUT chileno módulo 11 y política de contraseña. |
| SEC-03 | `Utilitarios.bas` L200-247 | Algoritmo rotulado "MD5" que en realidad era un desplazamiento ASCII reversible truncado al largo del alias. | Crítica | `PasswordSecurity` con **PBKDF2-HMAC-SHA256** (100.000 iteraciones + salt aleatorio 128-bit) y actualización automática en el primer login de cuentas migradas. |
| SEC-04 | `Transacciones.bas`, `TAB_0.bas`, `index.frm` | Concatenación directa de strings en SQL (inyección SQL en login, búsquedas e inserciones). | Crítica | 100% consultas SQL parametrizadas con `Microsoft.Data.Sqlite`. |
| SEC-05 | `Utilitarios.bas` L263-264 | **Permisos invertidos**: `VBSUP` leía `tab_0_VBJ` y `VBJEF` leía `tab_0_VBS`. | Crítica | Mapeo corregido y validado en `UserRepository`, `WorkOrderService` y pruebas de regresión. |
| SEC-06 | `TAB_US_0.bas` L41-46 + `Transacciones.bas` L52 | **Desplazamiento de columnas de permisos**: al crear usuarios, `Ingreso OT` se guardaba en `tab_0_VBJ`, `VB Jefatura` en `tab_0_VBS` y `VB Supervisor` en `tab_0_1`. | Crítica | Mapeo nominal explícito por columna y pruebas de regresión automatizadas. |
| SEC-07 | `form_usuario.frm` L1424-1462 | Al desmarcar un menú padre, solo se ocultaban los sub-checkboxes pero quedaban marcados y se guardaban activos. | Media | `UserPermissions.NormalizeHierarchy()` y sincronización automática en UI. |
| SEC-08 | `Sispro.Data` (NuGet Audit) | `SQLitePCLRaw.lib.e_sqlite3 2.1.11` reportado con vulnerabilidad alta `GHSA-2m69-gcr7-jv3q`. | Alta | Actualizado a `Microsoft.Data.Sqlite 10.0.15` + `SQLitePCLRaw.lib.e_sqlite3 2.1.14` (0 vulnerabilidades). |

### 3.2 Defectos Funcionales y de Integridad de Datos

| ID | Archivo VB6 | Hallazgo | Solución en .NET 10 |
|---|---|---|---|
| FUN-01 | `TAB_0.bas` L274-288 | Comparación de fechas como texto `"dd-mm-yyyy"` (`"31-01" > "01-02"` fallaba) e interpretación US `#MM-dd-yyyy#` en Access. | Almacenamiento ISO `yyyy-MM-dd` y comparación tipada `DateTime`. |
| FUN-02 | `exp_excel.bas` L110-122 | Bucle `For` interno faltante en `Exportar_grilla` (doble `Next` para un solo `For`, variable `c` sin iterar). | Exportación asíncrona completa con `ClosedXML`, estilos, filtros y progreso real. |
| FUN-03 | `Carga_nodos.bas` L57-64 | `Case 9` duplicaba llamadas `Bparent` y omitía el nivel 10. | Árbol recursivo (`WITH RECURSIVE`) de 10 niveles con carga diferida y búsqueda instantánea. |
| FUN-04 | `OT_PROV.mdb` | Registro corrupto `id_ot=0` (sin proveedor ni fecha) y OT `2514` con `id_sol=0` que desaparecía en `INNER JOIN`. | Registro corrupto depurado en migración; `LEFT JOIN Solicitantes` preserva OT 2514 intacta. |
| FUN-05 | `tab_ot.frm` | Botones `Modificar`, `Eliminar`, `Imprimir`, `img_buscar` y checkboxes `VBJ`/`VBS` sin código implementado. | Todas las acciones implementadas con transacciones ACID y auditoría. |
| FUN-06 | `tab_fac.frm` | Botones `LOCALIZAR`, `Eliminar` y pestaña `BUSCAR FACTURAS` sin implementar (tablas `Facturas` y `Facturas_loc` vacías). | Módulo completo de Guías/Facturas con IVA 19%, control de duplicados, búsqueda y eliminación con reversa. |
| FUN-07 | `MDI.frm` | Menú `Mantenedor` (`M3`) sin manejador de evento. | Nuevo módulo `MaintainersForm` con CRUD completo de Proveedores, Solicitantes y Árbol de Activos. |
| FUN-08 | `form_usuario.frm` | Botones `Modificar`, `Eliminar` y pestaña de listado/impresión incompletos; no sincronizaba `Solicitante`. | CRUD completo con protección del último administrador, auto-sincronización de solicitante, impresión y Excel. |
| FUN-09 | `conexion.bas` | `closeBD()` nunca cerraba conexiones ADO (`cnt.Close` comentado) provocando fugas de locks `.ldb`. | Conexiones gestionadas con `using` + pool SQLite en modo WAL. |

### 3.3 Interfaz de Usuario (UI/UX)

| ID | Archivo VB6 | Hallazgo | Solución en .NET 10 |
|---|---|---|---|
| UX-01 | `TAB_US_0.bas` L61-71 | Cabeceras de grilla de usuarios copiadas de OT (`ID OT`, `N° OT`, `Proveedor`, `Fecha`, `Cantidad`) y acceso fuera de rango en columna 5. | Cabeceras reales de usuarios (`RUT`, `Alias`, `Nombre`, `Nivel`, `Teléfono`, `Correo`, `Solicitante`, `Estado`). |
| UX-02 | `TAB_2.bas` L182-188 / `tab_ot.frm` L1942 | Error de índice fuera de rango al limpiar grilla (`Rows = 1` seguido de `TextMatrix(1, 1..4)`). | Limpieza tipada sobre `List<WorkOrderDetail>` enlazada a `DataGridView`. |
| UX-03 | `tab_ot.frm` L1140-1188 | Botón `CommandButton2` ("Modificar") superpuesto exactamente encima de `cmd_guardar`. | Barra de acciones ordenada con `FlowLayoutPanel`, iconos consistentes y habilitación contextual. |
| UX-04 | Múltiples formularios | Errores ortográficos: `"IINGRESO DE USUARIOS"`, `"Crear/Envíar OT"`, `"vualva"`, `"Solictante"`, títulos `"Form1"`. | Revisión ortográfica integral y prueba automatizada que prohíbe regresiones tipográficas. |
| UX-05 | Múltiples botones | Iconos faltantes o desalineados con espacios manuales en captions (`"   Salir"`). | `IconCatalog` vectorial 16×16 uniforme en el 100% de botones, verificado por prueba UI automatizada. |
