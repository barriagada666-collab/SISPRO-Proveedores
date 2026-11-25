# Guía de Compilación, Pruebas y Empaquetado — SISPRO v4.5.0

## 1. Requisitos del Entorno de Desarrollo

- **Sistema Operativo**: Windows 10 (19041+) / Windows 11 (x64).
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.100` o superior).
- **Instalador**: [Inno Setup 7](https://jrsoftware.org/isinfo.php) (`7.1.1` recomendado; retrocompatible con Inno Setup 6).
- **Lectura nativa de `.mdb` en tiempo de ejecución**: Windows PowerShell x86 (`SysWOW64`, incluido de fábrica en Windows 10/11) con proveedor `Microsoft.Jet.OLEDB.4.0`.

---

## 2. Compilación (`0` errores, `0` advertencias)

`Directory.Build.props` habilita `TreatWarningsAsErrors=true`, `WarningLevel=9999`, `Nullable=enable` y `AnalysisLevel=latest-recommended`.

```powershell
# Restaurar dependencias
dotnet restore SISPRO.sln

# Compilar Debug
dotnet build SISPRO.sln -c Debug --no-restore

# Compilar Release
dotnet build SISPRO.sln -c Release --no-restore
```

---

## 3. Suite de Pruebas Automatizadas (`74` casos `xUnit`)

```powershell
dotnet test SISPRO.sln -c Release --no-build --verbosity normal
```

Cobertura por categoría:
- **Unit (`23`)**: RUT chileno, PBKDF2 + compatibilidad VB6, jerarquía RBAC y `AssetNodeTypes`.
- **Integration (`7`)**: integridad real sobre `data/sispro.db`, migraciones idempotentes, árbol 10 niveles, respaldo en caliente, sincronización `SisproSyncHub` y concurrencia optimista.
- **Functional (`29`)**:
  - `MdbImportTests` (`6`): análisis comparativo exacto, inserción/actualización preservando campos SISPRO y firmas digitales, sobrescritura de vacíos opcional, RBAC solo Administrador, auditoría UI STA de `MdbImportDialog` y validación real contra `OT_PROV.mdb` cuando existe.
  - `ProfilesSignersAndUiFeaturesTests` (`3`), `BulkOperationsAndExtendedWorkOrderTests` (`3`), `RequestersSignaturesAndApproversTests` (`2`), `AssetTreeAdministrationTests` (`5`), `UserManagementModuleTests` (`5`), `GroupsAndBatchOperationsTests` (`3`), `EndToEndWorkflowTests` (`2`), `MultiFormatExportTests` (`2` — incluido en el total).
- **UI STA (`8`)**: auditoría completa de formularios, diálogos, botones, íconos, versiones dinámicas y módulos de auditoría (`WinFormsUiAuditTests`), menús contextuales (`ContextMenuHelperTests`), ordenamiento/paginación (`GridSortingAndPaginationTests`) y capturas (`ScreenshotCaptureTests`).
- **Regression (`3`)** y **Stress (`2`)**.

---

## 4. Publicación y Generación del Instalador Inno Setup 7

### Script integral automatizado
Restaura paquetes (incluyendo RID `win-x64` + `ReadyToRun`), compila `Debug` y `Release`, ejecuta las `74` pruebas, publica binario autocontenido `win-x64`, compila el instalador con Inno Setup `7.1.1` y genera `checksums_sha256.txt`:

```powershell
.\scripts\Build-Release.ps1
```

### Artefactos generados
- `installer\Output\SISPRO_Setup_v4.5.0_win-x64.exe`
- `installer\Output\checksums_sha256.txt`

---

## 5. Aprovisionamiento de Administrador Inicial (Opcional)

```powershell
.\scripts\Initialize-AdminUser.ps1 -DatabasePath "data\sispro.db" -Username "barriagada"
```
