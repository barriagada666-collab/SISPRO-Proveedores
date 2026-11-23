<div align="center">

# SISPRO — Sistema de Gestión de Proveedores y Órdenes de Trabajo

**Cambiaso Hnos S.A.C. · Departamento de Mantención**

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14-239120?style=for-the-badge&logo=csharp)](https://learn.microsoft.com/dotnet/csharp/)
[![SQLite](https://img.shields.io/badge/SQLite-3%20(WAL)-003B57?style=for-the-badge&logo=sqlite)](https://www.sqlite.org/)
[![Tests](https://img.shields.io/badge/Tests-32%2F32%20Passing-2E7D32?style=for-the-badge)](docs/REPORTE_PRUEBAS.md)
[![Warnings](https://img.shields.io/badge/Errors%20%7C%20Warnings-0%20%7C%200-0E4163?style=for-the-badge)](docs/GUIA_COMPILACION.md)
[![License](https://img.shields.io/badge/License-MIT-blue?style=for-the-badge)](LICENSE)

*Modernización integral a **.NET 10** del sistema corporativo **SISPRO (Proveedores)** para la gestión de órdenes de trabajo externas, árbol jerárquico de activos industriales, aprobaciones de visto bueno (Supervisor/Jefatura), guías de despacho, facturas, mantenedores y seguridad RBAC.*

</div>

---

## Características Principales

- **Panel de Control en Vivo**: KPIs instantáneos (Órdenes de Trabajo, Pendientes de VB Jefatura, Proveedores Activos, Facturas/Guías) y acceso rápido por doble clic a las últimas 40 OTs.
- **Órdenes de Trabajo Externas (`M1`)**:
  - Árbol jerárquico de **Activos de 10 niveles** con carga diferida (*lazy loading*), búsqueda rápida y sincronización automática al abrir cualquier OT histórica.
  - Ingreso, modificación, eliminación protegida y control de aprobaciones **VB Supervisor (`VBS`)** y **VB Jefatura (`VBJ`)** con trazabilidad de usuario y fecha.
  - Búsqueda combinada por rango de fechas, proveedor, palabra clave/N° OT, nodo del árbol y estado de VB.
  - Generación de documentos oficiales de OT en **Excel (`.xlsx`)** con logotipo corporativo y firmas digitales (`JR` / `MS`) sin requerir Microsoft Office instalado (**ClosedXML**), además de **Vista Previa e Impresión nativa**.
- **Facturas y Guías de Despacho (`M2`)**:
  - Asociación de ítems de OT a Guías de Despacho y Facturas con validación estricta de proveedor y prevención de ítems duplicados.
  - Cálculo automático de **Neto, IVA 19% y Total c/IVA**, búsqueda maestro-detalle, eliminación con reversa transaccional y exportación Excel.
- **Mantenedores (`M3`)**:
  - Gestión completa (CRUD) de **Proveedores**, **Solicitantes** y **Árbol de Activos (hasta 10 niveles)** con protección de integridad referencial.
- **Usuarios y Seguridad RBAC (`M4`)**:
  - Control de acceso granular por módulo, pestaña y acción en capas UI + Servicios.
  - Plantillas de permisos por perfil (`Usuario`, `Supervisor`, `Jefatura`, `Administrador`) y validación de **RUT chileno (módulo 11)**.
  - Cifrado **PBKDF2-HMAC-SHA256** (100.000 iteraciones + salt aleatorio), migración transparente desde contraseñas heredadas de VB6, bloqueo ante fuerza bruta, cambio de clave personal y **Bitácora de Auditoría (`AuditLog`)**.
- **Base de Datos Embebida y Respaldos**:
  - SQLite 3 en modo WAL con claves foráneas activas, consultas 100% parametrizadas (inmune a inyección SQL), migración completa de los **1.514 registros históricos** de `OT_PROV.mdb` y herramienta integrada de respaldo en caliente.

---

## Capturas de Pantalla

### Arranque Corporativo, Acceso y Panel de Control
| Pantalla de Arranque Corporativa | Acceso al Sistema |
| :---: | :---: |
| ![Pantalla de Arranque](docs/screenshots/00_splash.png) | ![Acceso al Sistema](docs/screenshots/01_login.png) |

| Panel de Control (Dashboard) | Órdenes de Trabajo y Árbol de Activos |
| :---: | :---: |
| ![Panel de Control](docs/screenshots/02_dashboard.png) | ![Órdenes de Trabajo](docs/screenshots/03_ordenes_trabajo.png) |

### Facturación, Mantenedores y Seguridad RBAC
| Facturas y Guías de Despacho | Mantenedores (Proveedores, Solicitantes y Activos) |
| :---: | :---: |
| ![Facturas y Guías](docs/screenshots/04_facturas_guias.png) | ![Mantenedores](docs/screenshots/05_mantenedores.png) |

| Administración de Usuarios y Permisos RBAC |
| :---: |
| ![Usuarios y Permisos](docs/screenshots/06_usuarios_rbac.png) |

---

## Arquitectura del Proyecto

```text
SISPRO/
├── SISPRO.sln                       # Solución principal .NET 10
├── Directory.Build.props            # Configuración global (C# latest, Nullable, 0 Warnings)
├── src/
│   ├── Sispro.Core/                 # Entidades, DTOs, RBAC, PBKDF2, RUT y Validadores
│   ├── Sispro.Data/                 # SQLite WAL, SchemaInitializer y Repositorios Parametrizados
│   ├── Sispro.Services/             # Lógica de negocio, ClosedXML Excel y PrintDocument
│   └── Sispro.UI/                   # Aplicación WinForms .NET 10 (PerMonitorV2, IconCatalog)
├── tests/
│   └── Sispro.Tests/                # Suite xUnit (Unit, Integration, Functional, UI, Regression, Stress)
├── data/
│   └── sispro.db                    # Base SQLite migrada desde OT_PROV.mdb (1.514 OTs, 73 Prov.)
├── installer/
│   └── Setup_SISPRO.iss             # Script Inno Setup 6 (instalador autocontenido win-x64)
├── scripts/
│   ├── Build-Release.ps1            # Automatización: Debug + Release + Tests + Publish + Setup
│   ├── Migrate-MdbToSqlite.ps1      # Migración desde Microsoft Access OT_PROV.mdb
│   └── build_sqlite.py              # Constructor e integrador SQLite
└── docs/
    ├── AUDITORIA_MIGRACION.md       # Auditoría completa VB6 y matriz de correcciones
    ├── ARQUITECTURA_TECNICA.md      # Diseño en capas, diagrama ER y modelo de seguridad
    ├── MANUAL_FUNCIONAL.md          # Guía de usuario módulo por módulo
    ├── GUIA_COMPILACION.md          # Compilación, pruebas y empaquetado
    ├── GUIA_DESPLIEGUE.md           # Instalación, actualización, red compartida y respaldos
    └── REPORTE_PRUEBAS.md           # Resultados detallados de la suite de 32 pruebas
```

---

## Requisitos

### Para Usuarios Finales (Instalador)
- **Windows 10 (1809+) o Windows 11 (x64)**.
- **No requiere instalar .NET, Microsoft Access ni Microsoft Excel** (instalador autocontenido).

### Para Desarrollo / Compilación
- **.NET 10 SDK** (`10.0.100+`).
- **Inno Setup 6** (solo para compilar el instalador `.exe`).

---

## Instalación y Uso

### Instalación Rápida (Producción)
1. Descargue `SISPRO_Setup_v2.0.0_win-x64.exe` desde la sección de **Releases** o compílelo con `.\scripts\Build-Release.ps1`.
2. Ejecute el instalador (compatible con instalación sin permisos de administrador o para todos los usuarios).
3. Inicie **SISPRO** desde el Menú Inicio.
   - Si utiliza la base migrada `data/sispro.db`, las cuentas históricas de `OT_PROV.mdb` funcionan directamente y actualizan su cifrado a PBKDF2 en el primer inicio de sesión.
   - En una base nueva vacía se crea automáticamente el administrador inicial `admin` / `Admin2026!` (se recomienda cambiar la clave en **Mi Clave** tras el primer ingreso).

### Compilación y Pruebas desde Código Fuente
```powershell
# Restaurar, compilar Debug y Release con 0 errores y 0 advertencias
dotnet restore SISPRO.sln
dotnet build SISPRO.sln -c Debug --no-restore
dotnet build SISPRO.sln -c Release --no-restore

# Ejecutar las 32 pruebas automatizadas
dotnet test SISPRO.sln -c Release --no-build --verbosity normal

# Generar compilación autocontenida e instalador Inno Setup 6
.\scripts\Build-Release.ps1
```

---

## Configuración

El archivo de configuración se guarda en `%LOCALAPPDATA%\Cambiaso\SISPRO\settings.json` y se edita gráficamente desde **Configuración**:
- **Base de datos activa**: por defecto `%LOCALAPPDATA%\Cambiaso\SISPRO\sispro.db` (copiada automáticamente desde `Data\sispro.db` al primer arranque; puede apuntar a una ruta de red compartida para operación multiusuario).
- **Datos corporativos**: Razón Social, RUT, Departamento y Dirección de Planta usados en reportes Excel e impresión.
- **Respaldo en caliente**: botón **Respaldar BD** que genera copias consistentes sin detener el sistema.

---

## Documentación Técnica y Funcional

- [Auditoría Completa y Migración VB6 → .NET 10](docs/AUDITORIA_MIGRACION.md)
- [Arquitectura Técnica y Modelo de Datos](docs/ARQUITECTURA_TECNICA.md)
- [Manual Funcional de Usuario](docs/MANUAL_FUNCIONAL.md)
- [Guía de Compilación y Empaquetado](docs/GUIA_COMPILACION.md)
- [Guía de Despliegue, Actualización y Respaldo](docs/GUIA_DESPLIEGUE.md)
- [Reporte de Pruebas Automatizadas](docs/REPORTE_PRUEBAS.md)

---

## Versionado e Historial de Cambios

Este proyecto sigue **Semantic Versioning (`MAJOR.MINOR.PATCH`)**. Consulte el detalle completo en [CHANGELOG.md](CHANGELOG.md).

- **v2.0.0** *(2026-11-23)* — Migración completa a **.NET 10 (C# WinForms)**, motor **SQLite WAL** con 1.514 OTs históricas migradas, corrección total de permisos RBAC y seguridad PBKDF2, exportación Excel sin Office (`ClosedXML`), impresión nativa, 32 pruebas automatizadas (0 errores / 0 advertencias) e instalador Inno Setup 6.
- **v1.0.0** *(2011-11-03)* — Versión original en Visual Basic 6 (`Proveedores.exe`) con Microsoft Access `OT_PROV.mdb`.

---

## Créditos

- **Organización**: Cambiaso Hnos S.A.C. — Departamento de Mantención, Placilla, Valparaíso, Chile.
- **Mantenimiento y Modernización .NET 10**: Equipo de Ingeniería / `barriagada666-collab`.
- **Licencia**: [MIT](LICENSE).
