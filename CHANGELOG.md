# Changelog — SISPRO

Todos los cambios relevantes del proyecto **SISPRO (Sistema de Gestión de Proveedores y Órdenes de Trabajo — Cambiaso Hnos S.A.C.)** se documentan en este archivo siguiendo el estándar [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [Semantic Versioning](https://semver.org/).

## [2.0.0] — 2026-11-23

### Migración Completa a .NET 10
- Migración íntegra desde Visual Basic 6 (`.vbp`, `.frm`, `.bas`, OCX ActiveX de 32 bits) hacia **C# sobre .NET 10 (`net10.0-windows`)** con Windows Forms nativo y soporte High-DPI (`PerMonitorV2`).
- Arquitectura profesional en capas:
  - `Sispro.Core`: Entidades de dominio, DTOs, validadores (RUT chileno módulo 11, IVA 19%, correo, políticas de contraseña) y seguridad criptográfica.
  - `Sispro.Data`: Motor SQLite embebido (`Microsoft.Data.Sqlite` 10.0.15 + `SQLitePCLRaw.lib.e_sqlite3` 2.1.14 parcheado contra GHSA-2m69-gcr7-jv3q) con modo WAL, claves foráneas estrictas, índices y consultas 100% parametrizadas.
  - `Sispro.Services`: Servicios de negocio, exportación Excel sin dependencia de Office COM (`ClosedXML` 0.105.1), impresión nativa `.NET 10` (`PrintDocument`) y bitácora de auditoría.
  - `Sispro.UI`: Interfaz corporativa Cambiaso con iconografía vectorial consistente en todos los botones, grillas con doble buffer y árbol jerárquico de 10 niveles con carga diferida.
  - `Sispro.Tests`: Suite automatizada xUnit (Unit, Integration, Functional, UI STA, Regression y Stress).

### Agregado
- **Migración completa de datos históricos** desde `OT_PROV.mdb` a `data/sispro.db`: 1.514 órdenes de trabajo válidas, 1.726 ítems de detalle, 73 proveedores, 7 solicitantes, 4 niveles, 2 usuarios y 272 nodos del árbol de activos (`PRAGMA integrity_check = ok`, 0 violaciones FK).
- **Panel de Control (Dashboard)** con indicadores en vivo (OTs totales, pendientes de VB Jefatura, proveedores activos, facturas/guías) y acceso rápido a las últimas OTs registradas.
- **Módulo Mantenedores (M3)** completo: CRUD de Proveedores (con datos de contacto y observaciones), CRUD de Solicitantes y administración completa del Árbol de Activos de 10 niveles.
- **Flujo completo de Guías de Despacho y Facturas (M2)**: asociación validada por proveedor, prevención de ítems duplicados, cálculo automático de IVA 19%, búsqueda avanzada, eliminación con reversa transaccional y exportación Excel.
- **Flujo completo de Visto Bueno (VBS / VBJ)** con registro auditable de usuario y fecha por aprobación.
- **Impresión y Vista Previa nativa** para Órdenes de Trabajo (con logotipo Cambiaso y selección de firmas digitales `JR` / `MS`) y Listado de Usuarios.
- **Seguridad reforzada**: hash `PBKDF2-HMAC-SHA256` (100.000 iteraciones + salt aleatorio) con actualización transparente desde contraseñas VB6 heredadas, bloqueo temporal ante fuerza bruta (5 intentos), protección del último administrador y bitácora de auditoría (`AuditLog`).
- **Configuración y Respaldo en caliente** (`SettingsDialog`) para cambiar base de datos, datos de empresa y generar respaldos consistentes de SQLite.
- **Instalador profesional Inno Setup 6** (`SISPRO_Setup_v2.0.0_win-x64.exe`) autocontenido (no requiere instalar .NET ni drivers Access), con preservación de datos en actualizaciones y desinstalación limpia.

### Corregido (Defectos críticos detectados en VB6)
- **[Seguridad]** Eliminadas credenciales hardcodeadas en `index.frm` y datos personales de prueba en `form_usuario.frm`.
- **[Seguridad]** Eliminada inyección SQL en todas las consultas y reemplazada por parámetros SQLite.
- **[Seguridad / RBAC]** Corregida inversión de permisos `VBSUP` / `VBJEF` en `Utilitarios.bas` (líneas 263-264) y corrimiento de columnas `tab_0_VBJ` / `tab_0_VBS` / `tab_0_1` al crear usuarios en `Transacciones.bas`.
- **[Funcional]** Corregida comparación de fechas por texto (`dd-mm-yyyy`) e inversión `MM/dd` en consultas Access (`TAB_0.bas`), usando fechas ISO `yyyy-MM-dd` y comparación cronológica real.
- **[Funcional]** Recuperada visibilidad de OT histórica `2514` (sin solicitante asignado) mediante `LEFT JOIN Solicitantes`.
- **[Funcional]** Corregido bucle roto de exportación a Excel (`exp_excel.bas` líneas 110-122) y barra de progreso.
- **[Funcional]** Implementadas todas las acciones antes vacías o rotas: Modificar OT, Eliminar OT, Imprimir OT, Localizar/Guardar Facturas, Buscar Facturas, Modificar/Eliminar Usuarios y Mantenedor de Proveedores.
- **[UI/UX]** Corregidas cabeceras erróneas en grilla de usuarios (`TAB_US_0.bas`), desbordamientos de índice al limpiar grillas (`TAB_2.bas`), botones superpuestos (`CommandButton2` sobre `cmd_guardar`) y errores ortográficos (`IINGRESO`, `Envíar`, `vualva`, `Solictante`).

### Eliminado
- Eliminados archivos obsoletos, binarios ActiveX (`TABCTL32.OCX`, `Proveedores.exe`), formularios huérfanos sin uso (`Form1.frm`, `frm_op.frm`, `frm_impresion.frm`, `form_genra_ot.frm`, `sel_imp.frm`, `Module1.bas`), archivos de log antiguos (`.log`) y dependencias COM de Microsoft Excel / Access Jet 4.0.

## [1.0.0] — 2011-11-03
- Versión original en Visual Basic 6 (`Proveedores.exe`) con base de datos Microsoft Access (`OT_PROV.mdb`) y árbol externo (`BD MANTENCION.mdb`).
