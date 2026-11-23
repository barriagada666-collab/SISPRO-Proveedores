# Guía de Compilación, Pruebas y Empaquetado — SISPRO v2.0

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
- **Integration**: base SQLite real migrada (1.514 OTs), árbol 10 niveles y respaldos en caliente.
- **Functional**: flujo E2E (OT → VBS/VBJ → Factura → Excel → Seguridad/Lockout/SQLi).
- **UI**: auditoría en hilo STA de todos los formularios, iconos en botones, ortografía y RBAC.
- **Regression**: validación explícita contra todos los defectos históricos de VB6.
- **Stress**: concurrencia multi-hilo (16 hilos), inserción masiva y exportación de 1.726 filas.

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
- `installer\Output\SISPRO_Setup_v2.0.0_win-x64.exe`
- `installer\Output\checksums_sha256.txt`

---

## 5. Re-ejecutar Migración de Datos desde `OT_PROV.mdb`

Si se dispone de una copia actualizada de `OT_PROV.mdb`:

```powershell
.\scripts\Migrate-MdbToSqlite.ps1 -MdbPath "ruta\OT_PROV.mdb" -SqlitePath "data\sispro.db"
```
