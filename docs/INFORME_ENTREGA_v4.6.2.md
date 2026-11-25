# Informe de Entrega — SISPRO v4.6.2 (.NET 10)

**Fecha:** 25-11-2026  
**Organización:** Cambiaso Hnos S.A.C. — Departamento Mantención (Placilla, Valparaíso)  
**Plataforma:** `.NET 10` (`C#` WinForms `net10.0-windows10.0.19041.0`) · SQLite 3 (WAL) · Inno Setup `7.1.1`

---

## Novedades Principales en v4.6.2

### 1. Importación Access (`.mdb`): Reescribir Todo desde Cero y Limpiar `sispro.db`
- **Modo `Reescribir todo desde cero al importar` (`MdbImportOptions.RewriteAllFromMdb`)**:
  - Permite limpiar atómicamente las tablas operativas de `sispro.db` (`VistoBueno`, `DetalleOT`, `OrdenesTrabajo`, `Solicitantes` y `Proveedores`, reiniciando sus secuencias autoincrement) e importar desde cero todo el contenido del archivo `.mdb` dentro de **una sola transacción SQLite** (rollback automático ante cualquier falla).
  - Deduplicación segura de proveedores por nombre y solicitantes por alias (preservando mayúsculas originales y remapeando todos los IDs legacy hacia las OTs importadas).
  - Respaldo automático previo (`backups/sispro_pre_mdb_rewrite_*.db`), confirmación explícita con conteos exactos, auditoría `REESCRIBIR_BD_DESDE_MDB` y refresco en vivo vía `SisproSyncHub`.
- **Acción independiente `Limpiar sispro.db` (`MdbImportService.CleanOperationalDatabase`)**:
  - Disponible exclusivamente para perfil **Administrador** desde el diálogo **Importar MDB**, con respaldo previo automático (`backups/sispro_pre_limpieza_*.db`), confirmación detallada y auditoría `LIMPIAR_BD_OPERATIVA`.
  - **Conserva intactos**: `Usuarios`, `Permisos`, `Niveles`, `Grupos`, `MovimientosGrupo`, `Activos`, `ConfiguracionSistema` y `AuditLog`.

### 2. Estabilización de Compilación `win-x64` y Auditoría Integral (`0` Errores / `0` Advertencias)
- Declarado `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` en `src/Sispro.UI/Sispro.UI.csproj` para prevenir definitivamente `NETSDK1047` en flujos combinados de auditoría NuGet y publicación `win-x64`.
- Actualización del instalador corporativo en español (`LICENCIA_SISPRO_ES.txt`, `INFO_INSTALADOR_ES.txt`, `Setup_SISPRO.iss` y banner `Versión 4.6.2`).
- Carpeta de evidencias de validación incorporada en `docs/evidencias/v4.6.2/`.

---

## Verificación de Calidad

- **Compilación `Debug` y `Release` (`--no-incremental`)**: **`0` errores, `0` advertencias** (`TreatWarningsAsErrors=true`, `WarningLevel=9999`, `0` supresiones).
- **Seguridad NuGet**: `0` paquetes vulnerables en los 5 proyectos (incluyendo transitivos).
- **Suite Automatizada (`Release`)**: **`83 / 83` pruebas `xUnit` aprobadas** (`0` fallidas, `0` omitidas).
- **Validación del Instalador (Inno Setup `7.1.1`)**: instalación silenciosa (`FileVersion 4.6.2.0`), preservación exacta de `Data\sispro.db` en actualización y desinstalación limpia verificadas con `0` errores.
- **Instalador Windows x64**: `SISPRO_Setup_v4.6.2_win-x64.exe` (`58.014.333` bytes).
- **Integridad `SHA-256`**: `D36400B36B971CC5599E5D8BCEB97E7A1309EB40BFBC554F1FA2EA0BC00B63DF`.
