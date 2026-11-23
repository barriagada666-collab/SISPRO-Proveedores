# Guía de Compilación, Pruebas y Empaquetado — SISPRO v4.1.0

## 1. Requisitos del Entorno de Desarrollo

- **Sistema Operativo**: Windows 10 / Windows 11 (x64).
- **SDK**: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (`10.0.100` o superior; validado con `10.0.404`).
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

La suite `tests/Sispro.Tests` cubre 6 categorías:
- **Unit**: RUT chileno, PBKDF2 + compatibilidad VB6, IVA 19%, jerarquía de permisos.
- **Integration**: base SQLite real unificada (`7.284` OTs, `1.824` activos, `43.989` OTs mantención, `208.121` tareas MP, `2.462` insumos SAP), árbol 10 niveles y respaldos en caliente.
- **Functional**: flujo E2E (OT → VBS/VBJ → Factura → Excel → Seguridad/Lockout/SQLi), módulo CRUD `GESTIÓN DE USUARIOS` y `GESTIÓN DE GRUPOS` (eliminación lógica, lotes, movimientos), administración del Árbol de Activos (`AssetTreeAdministrationTests`: CRUD, mover subárbol, protección ciclos/OTs, RBAC estricto y normalización) y exportaciones multiformato (`MultiFormatExportTests`: Excel, PDF, Word, CSV).
- **UI**: auditoría en hilo STA de todos los formularios (incluyendo `MaintenanceHubForm`, `UsersForm` y `AssetTreeEditorView`) y diálogos (`AssetNodeEditorDialog`, `AssetNodeMoveDialog`, etc.), menús contextuales seguros (`ContextMenuHelperTests`), iconos Lucide en botones, ortografía y RBAC.
- **Regression**: validación explícita contra todos los defectos históricos de VB6.
- **Stress**: concurrencia multi-hilo (16 hilos), analítica Dashboard, consulta en `43.989` OTs internas, inserción masiva y exportación de `1.500` filas.

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
- `installer\Output\SISPRO_Setup_v4.1.0_win-x64.exe`
- `installer\Output\checksums_sha256.txt`

---

## 5. Re-ejecutar Migración Integral desde Bases Access (`.mdb`)

Si dispone de copias actualizadas de `OT_PROV.mdb`, `BD MANTENCION.mdb` y `PERSONAL_MANTENCION.mdb`:

```powershell
.\scripts\Migrate-MdbToSqlite.ps1 `
    -OtProvMdb "ruta\OT_PROV.mdb" `
    -MantencionMdb "ruta\BD MANTENCION.mdb" `
    -PersonalMdb "ruta\PERSONAL_MANTENCION.mdb" `
    -SqlitePath "data\sispro.db"

# Aprovisionar cuenta Administrador inicial de forma segura e idempotente (sin clave en código)
.\scripts\Initialize-AdminUser.ps1 -DatabasePath "data\sispro.db" -Username "barriagada"
```
