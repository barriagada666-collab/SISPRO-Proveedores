# Manual Funcional de Usuario — SISPRO v4.5.0

## 1. Acceso al Sistema

1. Ejecute **SISPRO** desde el Menú Inicio o el acceso directo del escritorio.
2. Ingrese su **Usuario (Alias)** y **Contraseña**.
   - El sistema valida credenciales contra `sispro.db` mediante `PBKDF2-HMAC-SHA256`.
   - Si la cuenta requiere cambio obligatorio en el primer ingreso (`debe_cambiar_clave = 1`), se mostrará automáticamente la ventana de cambio de contraseña.
   - Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea preventivamente durante 30 segundos.
3. Para cambiar la ruta de base de datos antes de ingresar, utilice el botón **Config.**.

---

## 2. Panel de Control y Analítica (`Inicio / Analítica`)

- **Banner Hero Principal**: acceso directo al botón dorado **Nueva OT Externa**, **Buscar OTs** y **Actualizar Panel**.
- **KPIs Interactivos (5 tarjetas clicables)**: **OTs Externas**, **Proveedores**, **Activos Reales**, **OTs Año 2026** y **Solicitantes / Firmas**.
- **Gráficas vectoriales en vivo**: Evolución Anual de OTs, Distribución por Planta y Top 6 Proveedores.
- **Tabla reciente**: muestra **Valor Neto** y **Firmas**; doble clic edita la orden y **clic derecho → Visualizar OT (Vista Previa)** abre el documento oficial al instante.

---

## 3. Módulo Trabajos Externos — Órdenes de Trabajo (`M1`)

### 3.1 Ingreso / Edición de OT
1. Presione **Siguiente N°** o ingrese un correlativo manual.
2. Seleccione **Fecha**, **Proveedor**, **Solicitado por** y el nodo destino en el **Árbol de Activos (10 niveles)**.
3. Agregue uno o más ítems (Cantidad y Descripción), complete opcionalmente **Observación** y **Valor Neto ($)**.
4. En **Firmas al Pie**, marque las casillas deseadas (**Jefatura**, **Técnico**, **Supervisor**) y seleccione los firmantes desde la lista combinada de **Personal y Usuarios**.
5. Utilice **Guardar OT**, **Guardar y Exportar** (siempre habilitado para nuevas OTs o modificaciones) o **Vista Previa** directamente desde la ficha.

### 3.2 Búsqueda de OT y Exportación Masiva Exclusiva
- Filtre por rango de fechas, proveedor, solicitante, texto/N° OT o rama del árbol.
- Marque filas mediante checkboxes o **Seleccionar todos (página visible)**; la selección persiste entre ordenamientos y páginas.
- Presione **Exportar seleccionadas** para generar exclusivamente las OTs marcadas en **Excel (`.xlsx`)**, **CSV (`.csv`)** o **PDF (`.pdf`)**.
- Con **clic derecho** sobre cualquier fila puede ejecutar **Visualizar OT (Vista Previa)**, editar, imprimir o eliminar.

### 3.3 Generar / Imprimir OT
- Cargue una OT por número, ajuste las firmas al pie y genere documentos oficiales en **Excel**, **PDF**, **Word**, **CSV**, **Vista Previa** o **Imprimir**.

---

## 4. Módulo Mantenedores (`M2`) e Importación desde Access (`.mdb`)

Los 4 mantenedores (**Proveedores**, **Solicitantes**, **Firmas**, **Árbol de Activos**) incluyen selección múltiple por checkboxes, consulta de OTs relacionadas (`MaintainerDependenciesDialog`), reasignación sin pérdida de historial y exportación multiformato.

### 4.1 Importar y Actualizar Base de Datos desde Microsoft Access (`.mdb`) (Exclusivo Administrador)
Accesible desde **Mantenedores → Proveedores → `Importar MDB`** o desde **Configuración → `Importar MDB`**:
1. Presione **Examinar...** para seleccionar un archivo `.mdb` / `.accdb` (si existe `OT_PROV.mdb` en el Escritorio, se sugiere automáticamente).
2. Presione **Analizar MDB**. Sin modificar aún su base de datos, el sistema calcula y exhibe:
   - **5 tarjetas KPI**: **Proveedores en MDB** (vs BD actual), **Nuevos a Agregar**, **A Actualizar**, **Sin Cambios** y **OTs / Solicitantes nuevos detectados**.
   - **Grilla comparativa**: detalla cada proveedor, los campos con cambio (`Dirección`, `Teléfono`, `Fax`, `Correo`) y la vista **Actual → Nuevo**, filtrable por estado o búsqueda libre.
3. Configure las opciones deseadas:
   - *Agregar proveedores nuevos* (activado por defecto).
   - *Actualizar datos de existentes* (activado por defecto; preserva campos propios de SISPRO como Contacto, Observaciones y estado Activo).
   - *Sobrescribir con vacíos del MDB* (desactivado por defecto para no borrar información enriquecida en SISPRO).
   - *Sincronizar solicitantes* (conserva firmas digitales existentes).
   - *Importar también OTs nuevas detectadas* (opcional; agrega OTs ausentes en `sispro.db` junto con sus ítems de detalle y ruta de activo).
   - *Respaldar sispro.db antes de aplicar* (activado por defecto; crea copia en `backups/sispro_pre_mdb_*.db`).
4. Presione **Actualizar BD desde MDB** y confirme. Al finalizar se informa el conteo exacto aplicado, la ruta del respaldo y se actualizan automáticamente todos los catálogos abiertos.

---

## 5. Editor de Árbol de Activos (Exclusivo Administrador)

- Gestión completa de hasta **10 niveles** jerárquicos con menú contextual de clic derecho, prevención de ciclos, desactivación en cascada, **Normalizar Niveles** y **Exportar Rama** (`.xlsx`, `.pdf`, `.docx`, `.csv`).

---

## 6. Usuarios, Solicitantes y Perfiles (Exclusivo Administrador)

- **Usuarios y Grupos**: CRUD completo, integración directa de `Solicitante OT` + Firma Digital (`PNG`/`JPG`/`PDF`), gestión de grupos Drag & Drop y operaciones por lotes (`Activar`, `Desactivar`, `Eliminar` lógico, `Mover`, `Cambiar Perfil`).
- **Solicitados Por y Firmas Digitales**: administración centralizada de solicitantes y firmas rasterizadas (incluyendo página 1 de archivos PDF).
- **Perfiles de Usuarios**: CRUD de perfiles personalizados (`ID >= 5`) con permisos predeterminados, protegiendo los 4 perfiles base del sistema y cualquier perfil con usuarios asignados.

---

## 7. Auditoría y Configuración

- **Auditoría**: registro cronológico descendente con búsqueda por texto, rango de fechas y módulo (`Usuarios`, `Perfiles`, `Grupos`, `Seguridad`, `Órdenes de Trabajo`, `Mantenedores`, `Importación MDB`, `Árbol de Activos`, `Configuración`), exportable en 4 formatos.
- **Configuración**: cambio de ruta de `sispro.db` (local o red compartida), respaldo en caliente (**Respaldar BD**), **Importar MDB** y datos corporativos de cabecera.
