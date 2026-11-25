# Guía de Despliegue, Actualización y Respaldo — SISPRO v4.5.0

## 1. Requisitos del Equipo de Producción

- **Sistema Operativo**: Windows 10 (19041 / 2004+) o Windows 11 de 64 bits (`x64`).
- **Memoria RAM**: mínimo 2 GB (recomendado 4 GB).
- **Espacio en disco**: 250 MB libres para aplicación + espacio para respaldos de base de datos.
- **Dependencias externas**: **Ninguna** (el instalador incluye el runtime `.NET 10` autocontenido, motor SQLite embebido, OpenXML, renderizado PDF nativo vía `Windows.Data.Pdf` y generador PDF vectorial; la función opcional de importación `.mdb` utiliza el host Windows `SysWOW64` integrado en el sistema operativo).

---

## 2. Instalación

### Verificación de Integridad (SHA-256)
Verifique el instalador `SISPRO_Setup_v4.5.0_win-x64.exe` (`57.968.544` bytes) contra `checksums_sha256.txt`:

```powershell
Get-FileHash SISPRO_Setup_v4.5.0_win-x64.exe -Algorithm SHA256
# Esperado: 13E206BFAFA1569AD1810129502AA80D3454C3595614A9E442DB5DECDD6435DA
```

### Instalación Interactiva
1. Ejecute `SISPRO_Setup_v4.5.0_win-x64.exe`.
2. **Ruta predeterminada:** `C:\Temp` (editable por el usuario durante el asistente).
3. Elija instalación para el usuario actual (no requiere elevación) o para todos los usuarios.
4. Complete el asistente y, si desea, cree el acceso directo en el escritorio.

### Instalación Desatendida (Despliegue Masivo)
```powershell
.\SISPRO_Setup_v4.5.0_win-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR="C:\Temp"
```

---

## 3. Ubicación de Datos, Red Compartida y Sincronización MDB

- **Base de datos activa**: `%LOCALAPPDATA%\Cambiaso\SISPRO\sispro.db`
- **Configuración local**: `%LOCALAPPDATA%\Cambiaso\SISPRO\settings.json`
- **Respaldos automáticos pre-importación MDB**: `%LOCALAPPDATA%\Cambiaso\SISPRO\backups\sispro_pre_mdb_*.db`

Para operar con una base SQLite compartida en red local:
1. Ubique `sispro.db` en una carpeta compartida con lectura/escritura.
2. En SISPRO abra **Configuración → Examinar → Guardar**.

Para actualizar proveedores u órdenes desde una base Access histórica (`.mdb`):
1. Ingrese con perfil **Administrador**.
2. Abra **Mantenedores → Proveedores → `Importar MDB`** (o **Configuración → `Importar MDB`**).
3. Seleccione el `.mdb`, presione **Analizar MDB**, revise el resumen de cambios y presione **Actualizar BD desde MDB** (manteniendo marcada la opción de respaldo automático previo).

---

## 4. Actualización (Upgrade)

1. Cierre todas las instancias abiertas de `SISPRO.exe`.
2. Ejecute `SISPRO_Setup_v4.5.0_win-x64.exe` sobre la carpeta existente.
3. El instalador mantiene el `AppId` corporativo, actualiza únicamente los binarios y **nunca sobrescribe** `Data\sispro.db` (`Flags: onlyifdoesntexist uninsneveruninstall`).

---

## 5. Desinstalación

- Desde **Configuración de Windows → Aplicaciones → SISPRO → Desinstalar**, o silenciosamente:
  ```powershell
  & "<RutaInstalacion>\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  ```
- Por seguridad operativa, la base de datos SQLite (`Data\sispro.db` y `%LOCALAPPDATA%\Cambiaso\SISPRO\sispro.db`) se conserva intacta.

---

## 6. Respaldo y Recuperación

- **Respaldo manual en caliente**: **Configuración → Respaldar BD** (`SqliteConnection.BackupDatabase`).
- **Recuperación**: con la aplicación cerrada, reemplace `sispro.db` por el respaldo deseado o seleccione el respaldo desde **Configuración → Examinar**.
