# Guía de Despliegue, Actualización y Respaldo — SISPRO v4.0

## 1. Requisitos del Equipo de Producción

- **Sistema Operativo**: Windows 10 (1809+) o Windows 11 de 64 bits (`x64`).
- **Memoria RAM**: mínimo 2 GB (recomendado 4 GB).
- **Espacio en disco**: 250 MB libres para aplicación + espacio para respaldos de base de datos.
- **Dependencias externas requeridas**: **Ninguna** (el instalador oficial incluye el runtime `.NET 10` autocontenido, motor SQLite embebido, OpenXML y generador PDF vectorial; no requiere Microsoft Access, Excel ni Word).

---

## 2. Instalación

### Instalación Interactiva
1. Verifique la integridad del instalador comparando el hash SHA-256 con `checksums_sha256.txt`:
   ```powershell
   Get-FileHash SISPRO_Setup_v4.0.0_win-x64.exe -Algorithm SHA256
   ```
2. Ejecute `SISPRO_Setup_v4.0.0_win-x64.exe`.
3. **Ruta predeterminada:** `C:\Temp` (editable libremente por el usuario durante la instalación).
4. Seleccione instalación para el usuario actual (no requiere privilegios de administrador) o para todos los usuarios.
5. Complete el asistente y opcionalmente cree acceso directo en el escritorio.

### Instalación Desatendida (Despliegue Corporativo)
```powershell
.\SISPRO_Setup_v4.0.0_win-x64.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR="C:\Temp"
```

---

## 3. Ubicación de Datos y Configuración

Para evitar conflictos de permisos en `Program Files`, al iniciarse por primera vez SISPRO copia la base inicial `Data\sispro.db` al directorio de datos local del usuario:

- **Base de datos activa**: `%LOCALAPPDATA%\Cambiaso\SISPRO\sispro.db`
- **Archivo de configuración**: `%LOCALAPPDATA%\Cambiaso\SISPRO\settings.json`

### Uso en Red Compartida (Múltiples Estaciones)
Para compartir una misma base SQLite entre estaciones de trabajo en red local:
1. Ubique `sispro.db` en una carpeta compartida con permisos de lectura/escritura (ej. `\\Servidor\Mantencion\SISPRO\sispro.db`).
2. Abra **Configuración** en SISPRO → **Examinar** → seleccione la ruta de red → **Guardar**.

---

## 4. Proceso de Actualización (Upgrade)

1. Cierre todas las instancias abiertas de `SISPRO.exe`.
2. (Recomendado) Genere un respaldo desde **Configuración → Respaldar BD**.
3. Ejecute el nuevo instalador `SISPRO_Setup_vX.Y.Z_win-x64.exe` sobre la instalación existente.
4. El instalador conserva el mismo `AppId`, actualiza únicamente los binarios de programa y **nunca sobrescribe** la base de datos existente (`Flags: onlyifdoesntexist uninsneveruninstall`).

---

## 5. Desinstalación

- Desde **Configuración de Windows → Aplicaciones → SISPRO → Desinstalar**, o ejecutando silenciosamente:
  ```powershell
  & "<RutaInstalacion>\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
  ```
- Por seguridad operativa, la base de datos de producción en `%LOCALAPPDATA%\Cambiaso\SISPRO\sispro.db` se conserva para evitar pérdida accidental de registros históricos.

---

## 6. Política de Respaldos y Recuperación

- **Respaldo manual en caliente**: **Configuración → Respaldar BD** (usa la API nativa `SqliteConnection.BackupDatabase`, consistente incluso con sesiones activas).
- **Recuperación**: reemplace `sispro.db` con el archivo respaldado (con la aplicación cerrada) o apunte al respaldo desde **Configuración → Examinar**.
