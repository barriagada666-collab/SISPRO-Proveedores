# Guía de Compilación, Pruebas y Empaquetado — SISPRO v4.2.0

## 1. Requisitos del Entorno de Desarrollo

- **Sistema Operativo**: Windows 10 (1809+ / SDK 10.0.19041.0) / Windows 11 (x64).
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.100` o superior).
- **Empaquetado de Instalador (opcional)**: [Inno Setup 6](https://jrsoftware.org/isinfo.php) (`ISCC.exe`).
- **Migración desde Access heredado (opcional)**: PowerShell 32-bit + Python 3.10+ (solo si requiere re-importar `OT_PROV.mdb`).

---

## 2. Compilación desde Línea de Comandos

El proyecto tiene activado `TreatWarningsAsErrors=true`, `Nullable=enable` y `AnalysisLevel=latest-recommended` en `Directory.Build.props`, garantizando **0 errores y 0 advertencias**.

### Restaurar paquetes NuGet
```powershell
dotnet restore SISPRO.sln
```

### Compilar en Debug
```powershell
dotnet build SISPRO.sln -c Debug --no-restore
```

### Compilar en Release
```powershell
dotnet build SISPRO.sln -c Release --no-restore
```

---

## 3. Ejecución de Pruebas Automatizadas

La suite `tests/Sispro.Tests` cubre 6 categorías (**62 casos automatizados**):
- **Unit**: RUT chileno, PBKDF2 + compatibilidad VB6, jerarquía de permisos RBAC y normalización de tipos de nodos (`AssetNodeTypes`).
- **Integration**: base SQLite real (`7.284` OTs externas, `1.824` activos, `214` proveedores activos), migración idempotente de esquema legado en `Solicitantes` sin perder registros, árbol 10 niveles, sincronización/concurrencia y respaldos en caliente.
- **Functional**: flujo E2E (OT Externa → VBS/VBJ con identidad → Excel → Seguridad/Lockout/SQLi), gestión de `Solicitado Por` con firma `PNG`/`JPG`/`PDF` y renderizado PDF en exportaciones (`RequestersSignaturesAndApproversTests`), módulo CRUD `GESTIÓN DE USUARIOS` y `GESTIÓN DE GRUPOS` (eliminación lógica, lotes, movimientos), administración del Árbol de Activos (`AssetTreeAdministrationTests`) y exportaciones multiformato (`MultiFormatExportTests`: Excel, PDF, Word, CSV).
- **UI**: auditoría en hilo STA de formularios y diálogos (`WinFormsUiAuditTests`, incluyendo confirmación explícita de ausencia de módulos de Facturas y Mantención Industrial), capturas visuales (`ScreenshotCaptureTests`), ordenamiento/paginación (`GridSortingAndPaginationTests`), menús contextuales seguros (`ContextMenuHelperTests`) e iconos Lucide aislados por hilo STA (`IconCatalog`).
- **Regression**: validación explícita contra defectos históricos de VB6 (sin inversión VBS/VBJ, actualización/eliminación de OT sin dependencias obsoletas de facturas).
- **Stress**: concurrencia multi-hilo, búsqueda indexada y exportación sobre la base real `data/sispro.db`.

```powershell
dotnet test SISPRO.sln -c Release --no-build --verbosity normal
```

---

## 4. Publicación y Generación de Instalador

### Opción A: Script integral automatizado
Ejecuta compilación Debug y Release, pruebas automatizadas, publicación autocontenida `win-x64` (`ReadyToRun`), compilación de Inno Setup 6 y generación de checksum SHA-256:

```powershell
.\scripts\Build-Release.ps1
```

### Opción B: Paso a paso manual
```powershell
# 1. Publicar binario autocontenido (no requiere .NET 10 instalado en el equipo destino)
dotnet publish src\Sispro.UI\Sispro.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o publish\win-x64

# 2. Compilar instalador Inno Setup 6
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\Setup_SISPRO.iss
```

El instalador resultante se genera en:
- `installer\Output\SISPRO_Setup_v4.2.0_win-x64.exe`
- `installer\Output\checksums_sha256.txt`

---

## 5. Re-ejecutar Migración Base desde `OT_PROV.mdb` (Opcional)

Si requiere reconstruir o inicializar una base vacía y aprovisionar la cuenta Administrador inicial de forma segura e idempotente (sin clave en código):

```powershell
# Aprovisionar cuenta Administrador inicial de forma segura e idempotente (sin clave en código)
.\scripts\Initialize-AdminUser.ps1 -DatabasePath "data\sispro.db" -Username "barriagada"
```
