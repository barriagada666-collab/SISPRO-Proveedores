# Arquitectura Técnica — SISPRO v4.2.1 (.NET 10)

## 1. Visión General

**SISPRO** (Sistema Integral de Gestión de Proveedores, Órdenes de Trabajo Externas, Solicitantes y Control de Activos de Cambiaso Hnos S.A.C.) está construido en **C# sobre .NET 10 (`net10.0` / `net10.0-windows10.0.19041.0`)** bajo una arquitectura limpia en capas con separación estricta de responsabilidades, tipado estricto (`Nullable=enable`, `TreatWarningsAsErrors=true`) y cero dependencias COM externas. Opera sobre SQLite 3 en modo WAL (`data/sispro.db`), preservando los datos históricos operativos de trabajos externos (`7.284` OTs externas, `7.803` ítems de detalle, `214` proveedores activos, `1.824` nodos de activos y `11` solicitantes históricos / `9` activos). En `v4.2.0` se eliminaron los módulos obsoletos de **Facturas y Guías** y **Mantención Industrial**, centralizando la operación en Trabajos Externos, Árbol de Activos, Proveedores, Solicitantes con Firma Digital, Aprobaciones `VBS`/`VBJ` y Gestión Dinámica de Usuarios y Grupos.

```mermaid
flowchart TD
    UI["Sispro.UI (WinForms .NET 10 · PerMonitorV2)"]
    SRV["Sispro.Services (Lógica de Negocio · ClosedXML · PrintDocument)"]
    DATA["Sispro.Data (Repositorios SQLite · Parametrizado · WAL)"]
    CORE["Sispro.Core (Entidades · DTOs · RBAC · PBKDF2 · Validadores)"]
    DB[("data/sispro.db (SQLite 3 · ACID + FK)")]

    UI --> SRV
    UI --> CORE
    SRV --> DATA
    SRV --> CORE
    DATA --> CORE
    DATA --> DB
```

---

## 2. Estructura de Proyectos (`SISPRO.sln`)

| Proyecto | Target | Responsabilidad Principal |
|---|---|---|
| `src/Sispro.Core` | `net10.0` | Entidades de dominio (`AssetNode`, `AssetNodeTypes`, `UserGroup`, `Requester`, `WorkOrder` con `NombreSolicitante`/`SolicitanteDisplayNombre`, `WorkOrderApproval`), enumeraciones (`SignatureOption` `[Flags]` con `SignatureOptionExtensions`, `UserRoleLevel`), DTOs (`CoreDtos`, `ExportDtos`, `ApproverDirectoryEntry`), interfaces de repositorio, seguridad (`PasswordSecurity`, `PermissionPresets`) y validadores (`RutValidator`, `DomainValidator`). |
| `src/Sispro.Data` | `net10.0` | Fábrica de conexiones SQLite (`SqliteConnectionFactory`), inicializador de esquema y migraciones idempotentes (`SchemaInitializer`, incluyendo columnas de firma en `Solicitantes`), aprovisionador seguro (`InitialAdminProvisioner`) y repositorios parametrizados (`AssetTreeRepository`, `UserRepository`, `GroupRepository`, `WorkOrderRepository`, `AuditRepository`, `ProviderRepository`, `RequesterRepository`). |
| `src/Sispro.Services` | `net10.0-windows10.0.19041.0` | Servicios transaccionales con RBAC (`AuthService`, `WorkOrderService`, `MaintainerService`, `AssetTreeService`, `UserManagementService`, `GroupManagementService`), gestor de firmas (`SignatureFileService` con renderizado nativo de página 1 PDF mediante `Windows.Data.Pdf.PdfDocument` sin dependencias vulnerables), compositor uniforme de firmas al pie (`WorkOrderSignatureLayout`), sincronización reactiva (`SisproSyncHub`), saneamiento compartido (`ExportSanitizer`), exportadores multiformato (`DocumentExportService`, `ExcelExportService` vía `ClosedXML`, `PdfExportService`, `WordExportService`, `CsvExportService`) e impresión (`PrintService`). |
| `src/Sispro.UI` | `net10.0-windows10.0.19041.0` | Interfaz Windows Forms con tema Cambiaso (`CambiasoTheme`, `ContextMenuHelper` seguro con ejecución diferida, `SortableBindingList<T>`, `GridPagerBar`, `ExportUiHelper`), gráficas analíticas (`ModernChartPanel`), íconos Lucide vectoriales (`IconCatalog` aislado por hilo STA), panel unificado `RequesterManagerControl`, árbol 10 niveles (`AssetTreePanel`, `AssetTreeEditorView`), módulo `UsersForm` (usuarios, solicitantes/firmas, aprobadores VB y grupos), `WorkOrdersForm` con acciones directas e identificación de aprobadores, `MaintainersForm` y 11 diálogos seguros. |
| `tests/Sispro.Tests` | `net10.0-windows10.0.19041.0` | Suite xUnit completa (**62 pruebas**): unitarias, integración (incluyendo migración de esquema legacy `Solicitantes`, árbol 10 niveles y respaldos), sincronización/concurrencia, administración CRUD del árbol, gestión de usuarios/grupos/lotes, solicitantes con firmas `PNG`/`JPG`/`PDF` y aprobadores `VBS`/`VBJ`, exportaciones multiformato, ordenamiento/paginación, auditoría UI/UX y menús contextuales en hilo STA, regresión VB6 y estrés/rendimiento. |

---

## 3. Modelo de Datos Relacional (`sispro.db`)

Motor: **SQLite 3** (`PRAGMA foreign_keys = ON`, `PRAGMA journal_mode = WAL`, `busy_timeout = 5000ms`).

```mermaid
erDiagram
    Niveles ||--o{ Usuarios : "define perfil"
    Usuarios ||--|| Permisos : "posee permisos"
    Proveedores ||--o{ OrdenesTrabajo : "recibe"
    Solicitantes ||--o{ OrdenesTrabajo : "solicita"
    Activos ||--o{ Activos : "jerarquía 10 niveles"
    OrdenesTrabajo ||--|{ DetalleOT : "contiene"
    OrdenesTrabajo ||--o{ VistoBueno : "aprobaciones VBS/VBJ"
    Usuarios ||--o{ VistoBueno : "aprueba"
    Grupos ||--o{ Usuarios : "agrupa"
    Grupos ||--o{ MovimientosGrupo : "traza historial"
```

### Columnas Agregadas en `Solicitantes` (Migración Idempotente `v4.2.0`)
- `firma_bytes` (`BLOB`) — contenido binario original validado (`PNG`, `JPG` o `PDF`, máx. 10 MB).
- `firma_nombre` (`TEXT`) — nombre de archivo original adjunto.
- `firma_tipo` (`TEXT`) — tipo MIME/extensión normalizada (`png`, `jpg`, `pdf`).
- `firma_preview_png` (`BLOB`) — miniatura PNG rasterizada (pág. 1 vía `Windows.Data.Pdf` para archivos PDF) lista para vista previa e incrustación en documentos.

### Índices de Rendimiento
- `IX_Activos_Parent(parent_id, activo, descripcion)` — expansión instantánea de nodos en `AssetTreePanel`.
- `IX_OT_Fecha(fecha_ot)` — búsquedas por rango cronológico en milisegundos.
- `IX_OT_Prov(id_prov)` y `IX_OT_Sol(id_sol)` — filtros por proveedor y solicitante.
- `IX_OT_Arbol(id_n2, id_n3, id_n4, id_n5)` — filtrado por ubicación en planta.
- `IX_DetalleOT_IdOt(id_ot)` — carga inmediata de detalles por OT.
- `IX_AuditLog_Fecha(fecha_utc DESC)` — consulta ordenada de auditoría.

---

## 4. Modelo de Seguridad y Control de Acceso (RBAC)

1. **Autenticación**:
   - Hash criptográfico `PBKDF2-HMAC-SHA256` con 100.000 iteraciones y salt aleatorio de 16 bytes por usuario.
   - Comparación en tiempo constante (`CryptographicOperations.FixedTimeEquals`).
   - Compatibilidad transitoria de lectura para cuentas migradas desde `OT_PROV.mdb` (`LEGACY_VB6$...`), con actualización automática e irreversible a PBKDF2 tras el primer inicio de sesión válido.
   - Protección contra fuerza bruta: bloqueo temporal de 30 segundos tras 5 intentos fallidos consecutivos por alias.
2. **Autorización en Doble Capa**:
   - **Capa UI**: Menús, pestañas y botones sensibles (VB Supervisor, VB Jefatura, Configuración, Auditoría, Editor de Árbol, Usuarios y Solicitantes) se habilitan/ocultan estrictamente según `UserPermissions` y perfil.
   - **Capa Services**: Cada operación crítica valida `AuthService.EnsurePermission(...)` o revalida perfil Administrador en base de datos, impidiendo escalación de privilegios incluso mediante invocación directa.
3. **Resguardo Operacional**:
   - Imposibilidad de eliminar la sesión propia o degradar/desactivar al último administrador activo.
   - Registro automático en `AuditLog` de accesos, cambios de clave, CRUD de OTs, aprobaciones VBS/VBJ con usuario y fecha, solicitantes/firmas, mantenedores y respaldos.

---

## 5. Optimizaciones de Rendimiento y UI

- **Doble buffer (`DoubleBuffered = true`)** en grillas, formularios y contenedor de módulos para eliminar parpadeos.
- **Instanciación diferida y reutilización de vistas** en `MainForm` para cambio instantáneo entre módulos.
- **Árbol de activos con carga diferida (Lazy Loading)** por nodo padre + búsqueda indexada directa.
- **Exportación Excel asíncrona** en `Task.Run` con reporte de progreso (`IProgress<int>`) sin congelar el hilo de UI.
- **Soporte High-DPI `PerMonitorV2`** con layouts fluidos (`TableLayoutPanel`, `SplitContainer`, `FlowLayoutPanel`).
