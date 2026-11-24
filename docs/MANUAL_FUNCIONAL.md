# Manual Funcional de Usuario — SISPRO v4.4.0

## 1. Acceso al Sistema

1. Ejecute **SISPRO** desde el Menú Inicio o el escritorio.
2. Ingrese su **Usuario (Alias)** y **Contraseña**.
   - El sistema valida las credenciales contra `sispro.db` mediante hash `PBKDF2-HMAC-SHA256`.
   - Si la cuenta tiene activo el cambio obligatorio de primer acceso (`debe_cambiar_clave = 1`, como la cuenta inicial de Administrador), se desplegará automáticamente la ventana de **Cambio Obligatorio de Contraseña** antes de acceder al panel principal.
   - Cuentas heredadas de la versión VB6 (`OT_PROV.mdb`) se actualizan automáticamente a `PBKDF2-HMAC-SHA256` en el primer ingreso exitoso.
   - Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea preventivamente durante 30 segundos.
3. Si requiere cambiar la ruta de base de datos antes de ingresar, utilice el botón **Config.**.

---

## 2. Panel de Control y Analítica (`Inicio / Analítica`)

Al iniciar sesión se despliega el **Panel de Control** enfocado íntegramente en trabajos externos:
- **Banner Hero Principal**: encabezado **Trabajos Externos — Crear y Gestionar Órdenes de Trabajo**, botón **Actualizar Panel**, **Buscar OTs** y botón dorado principal **Nueva OT Externa** para iniciar inmediatamente una nueva orden de trabajo.
- **KPIs Interactivos (5 tarjetas clicables)**: **OTs Externas** (`7.284`), **Proveedores** (`214`), **Activos Reales** (`1.823` activos), **OTs Año 2026** y **Solicitantes / Firmas** (con firma `PNG`/`JPG`/`PDF`).
- **Gráficas en vivo**: **Evolución Anual de OTs**, **Distribución por Planta** (Donut) y **Top 6 Proveedores**.
- En la tabla inferior se muestran las OTs más recientes con columnas **Valor Neto** y **Firmas**; haciendo **doble clic** se edita la orden y con **clic derecho → Visualizar OT (Vista Previa)** se abre el documento oficial al instante.

---

## 3. Módulo Trabajos Externos — Órdenes de Trabajo (`M1`)

En el panel izquierdo permanece visible el **Árbol de Activos (10 niveles)** con búsqueda rápida por nombre o ID y ruta completa (*breadcrumb*) en la parte inferior.

### 3.0 Barra Rápida "Nueva OT Externa" y Menús de Clic Derecho
- Tanto en la barra superior principal como en el banner Hero del Dashboard y en **Trabajos Externos** dispone del botón dorado **Nueva OT Externa**, que posiciona inmediatamente el módulo en modo de creación conservando el activo seleccionado y los catálogos sin interrupciones de sincronización.
- En el pie del ingreso dispone de **Guardar y Exportar** (guarda la nueva OT y abre al instante las opciones de exportación oficial) además del botón **Exportar / Imprimir OT** en la barra superior del módulo.
- Haciendo **clic derecho** sobre el árbol o cualquier grilla (resultados, ítems, proveedores, solicitantes, usuarios y grupos) se accede al menú contextual de edición, apertura, exportación o eliminación rápida.

### 3.1 Pestaña Ingreso / Edición de OT (`tab_0_1`)
- **Crear nueva OT**:
  1. Presione **Siguiente N°** (o ingrese el correlativo manual).
  2. Seleccione **Fecha**, **Proveedor** y **Solicitado por** (solo permite registros activos al crear nuevas órdenes).
  3. Seleccione en el árbol izquierdo la planta/sección/equipo destino (mínimo nivel 2).
  4. Ingrese **Cantidad** y **Descripción del trabajo**, y presione **Agregar Ítem** (puede agregar múltiples ítems; doble clic sobre la grilla permite editarlos y clic derecho abre **Visualizar OT (Vista Previa)**).
  5. Complete opcionalmente **Observación de la OT**, ingrese el **Valor Neto ($)** en formato peso chileno destacado (`$ >= 0`) y utilice las casillas + listas desplegables de **Firmas al Pie** (**Jefatura**, **Técnico**, **Supervisor**) para elegir cualquier firmante desde **Personal y Usuarios** (o desmarcar para omitir).
  6. Presione **Guardar OT**, **Guardar y Exportar** (siempre habilitado tanto en OT nueva como cargada) o **Vista Previa** para revisar el documento antes o después de guardar.
- **Consultar / Modificar / Eliminar OT existente**:
  1. Ingrese el **N° OT** y presione **Buscar OT** (o tecla `Enter`).
  2. El árbol se expande automáticamente al activo correspondiente y se restauran **Observación**, **Valor Neto ($)**, casillas de **Firmas** y firmantes seleccionados.
  3. Realice cambios y presione **Modificar**, **Guardar y Exportar**, **Vista Previa** o **Eliminar** cuando corresponda.

### 3.2 Pestaña Búsqueda de OT (`tab_0_2`) y Exportación Masiva Exclusiva
- Permite combinar filtros por **Rango de fechas**, **Proveedor**, **Solicitante**, **Palabra clave / N° OT** y **Nodo seleccionado del árbol**.
- **Selección individual y masiva por Checkboxes**:
  - Marque cada orden deseada en la columna `Sel.` o use **Seleccionar todos (página visible)**.
  - La selección **se conserva** al ordenar columnas o navegar entre páginas de resultados (`OT seleccionadas: N`).
  - **Exportar seleccionadas** (habilitado solo con 1+ selección) solicita confirmación indicando la cantidad exacta de OTs a exportar y genera **exclusivamente las órdenes seleccionadas** (sin duplicar ítems por OT, incluyendo número, fecha, proveedor, solicitante, cantidad total, detalle resumido, observaciones, valor neto, estado y firmas seleccionadas) en **Excel (`.xlsx`)**, **CSV (`.csv`)** o **PDF (`.pdf`)**.
  - **Limpiar selección** desmarca toda la selección acumulada.
- Dispone de **ordenamiento por clic en cualquier columna** y **barra de paginación inferior** (`50`, `100`, `200`, `500` registros por página).
- **Doble clic** o menú de **clic derecho** sobre un resultado permite **Visualizar OT (Vista Previa)**, abrir para edición, imprimir o eliminar.
- **Exportar Lista** exporta el listado completo filtrado actual (`.xlsx`, `.pdf`, `.docx` o `.csv`).

### 3.3 Pestaña Generar / Imprimir OT (`tab_0_3`)
- Cargue una OT existente por su número (o componga ítems manualmente).
- Revise las tarjetas alineadas de **Datos del Proveedor** (`Proveedor`, `Dirección`, `Correo`) y **Datos del Solicitante y Firma Técnico** (`Solicitante`, `Nombre`, `Correo`, `Teléfono`, `Activo` junto con la vista previa de firma digital).
- Seleccione libremente las **Firmas al pie** mediante casillas independientes (**Jefatura**, **Técnico (Solicitante)**, **Supervisor**) — se sincronizan con `OrdenesTrabajo.firmas_mask` al cargar una OT y permiten combinar cualquiera o **desmarcar todas** para dejar el pie de página sin firmas.
- Todas las exportaciones/impresiones individuales de OT (`.xlsx`, `.pdf`, `.docx`, impresión directa y vista previa) destacan el **N° de OT grande en la esquina superior derecha**, muestran **Nombre Apellido**, correo, teléfono y **Activo** en *Datos del Solicitante*, presentan la firma del solicitante como **Técnico**, incluyen **Observaciones** y **Valor Neto ($)** cuando aplica, omiten líneas de `VB` y columnas `Guía / Estado` o `Valor / Ref.`, y solo imprimen las firmas seleccionadas.
- Utilice **Exportar OT** (`.xlsx`, `.pdf`, `.docx`, `.csv`), **Vista Previa** o **Imprimir**.

---

## 4. Módulo Mantenedores (`M2`) y Gestión Masiva con Integridad

Los **4 mantenedores** operan sobre pestañas integradas con **casillas de verificación por fila**, botón **Limpiar Sel.**, consulta de dependencias y sincronización instantánea entre módulos:

- **Proveedores**: alta, edición, checkboxes por fila, **Consultar OT**, **Reasignar Rel.**, **Activar / Desactivar Sel.**, **Eliminar** y exportación multiformato (Excel, PDF, Word, CSV).
- **Solicitantes**: catálogo unificado (`RequesterManagerControl`) sincronizado con `Usuarios y Solicitantes` para administrar **Solicitado Por** con checkboxes, **Consultar OT**, **Reasignar**, **Activar / Desactivar** y **Eliminar**.
- **Firmas**: pestaña dedicada (`SignatureManagerControl`) con vista previa en vivo (`PNG`/`JPG`/`PDF`), filtros (`Todos los solicitantes`, `Con firma digital (todas)`, `Con firma y activos`, `Sin firma adjunta`, `Solo inactivos`), adjunto/reemplazo y checkboxes para **Consultar OT**, **Reasignar Firma**, **Desactivar Sel.** y **Eliminar Firma(s)**.
- **Árbol de Activos**: árbol industrial de 10 niveles con checkboxes por nodo, menú de clic derecho (*Consultar OT asociadas…*, *Reasignar OT / Subnodos…*, CRUD completo para Administradores) e íconos por tipo estructural (`Empresa`, `Planta`, `Sección / Área`, `Máquina`, `Módulo`, `Componente`).

### Reglas de Integridad, RBAC y Diálogo de Dependencias (`MaintainerDependenciesDialog`)
- **Exclusivo Administrador (UI y Backend)**: solo perfiles **Administrador** pueden desactivar/reactivar, eliminar, reasignar o borrar OTs relacionadas en Proveedores, Solicitantes, Firmas o Árbol de Activos; demás perfiles solo consultan.
- **Sin borrados silenciosos de historial**: ninguna operación sobre un mantenedor borra órdenes de trabajo automáticamente. Si existen OTs asociadas, `MaintainerDependenciesDialog` exhibe la grilla de OTs vinculadas y ofrece:
  1. **Reasignar proveedor / solicitante / firma / activo / subnodos** hacia otro registro activo destino (respetando jerarquía máx. 10 niveles y bloqueo de ciclos en el árbol).
  2. **Desactivar conservando historial** (oculta el registro para nuevas OTs preservando 100% los registros pasados).
  3. **Eliminar seleccionados (compatibles sin OTs)**.
  4. **Eliminar explícitamente OTs asociadas** (opción destacada en peligro, únicamente mediante **doble confirmación explícita** exclusiva para Administradores).

---

## 5.1 Módulo Especial Editor del Árbol de Activos (Exclusivo Administrador)

Accesible desde **Editor de Árbol** en el menú lateral izquierdo o mediante **Abrir Editor de Árbol** en Mantenedores (visible y habilitado únicamente para cuentas con perfil **Administrador**, revalidado en base de datos):
- **Menú de Clic Derecho en el Árbol**:
  - *Administradores*: **Agregar nodo hijo…**, **Editar nodo…**, **Mover a otro nodo padre…**, **Activar nodo**, **Desactivar nodo**, **Eliminar nodo…**, separador, **Expandir rama**, **Contraer rama**, **Copiar ruta** y **Actualizar árbol**.
  - *Operadores y otros perfiles*: vista de solo lectura con **Expandir rama**, **Contraer rama**, **Copiar ruta**, **Buscar OTs de este nodo** (en Trabajos Externos) y **Actualizar árbol**.
- **Reglas de Integridad Estructural**:
  - Profundidad máxima estricta de **10 niveles** (validada tanto al crear como al mover subárboles).
  - Prevención automática de nombres duplicados entre nodos hermanos y prevención de ciclos al mover nodos.
  - Desactivación en cascada de subnodos, bloqueo de reactivación si el nodo padre está inactivo y protección absoluta del nodo raíz de la empresa.
  - Eliminación física bloqueada si el nodo posee subnodos o referencias históricas en OTs externas.
- **Normalización y Exportación**:
  - Botón **Normalizar Niveles** recalcula la columna `nivel` según la profundidad jerárquica real desde la raíz.
  - Botón **Exportar Rama** exporta el subárbol seleccionado en Excel (`.xlsx`), PDF (`.pdf`), Word (`.docx`) o CSV (`.csv`).

---

## 6. Módulo Usuarios, Solicitantes y Perfiles (Exclusivo Administrador)

Disponible únicamente para usuarios con perfil **Administrador** (validado en interfaz y backend) bajo tres pestañas integradas:

### 6.1 Pestaña `Usuarios y Grupos`
- **Sección `DATOS DEL USUARIO Y SOLICITANTE`**: campos completos (ID automático, Usuario, Nombre, Apellido, Correo, Teléfono, Perfil dinámico, Grupo, Estado, Contraseña, Confirmar contraseña, cambio obligatorio de clave `Cambiar clave 1er acceso`, Fecha de creación, Último acceso) + botón **Permisos**.
- **Integración directa `Solicitante OT` + Firma Digital**: al marcar `Solicitante OT` durante la creación o edición de un usuario, se habilita el adjunto y vista previa de su firma digital (`PNG`, `JPG` o `PDF`, máx. 10 MB), sincronizándose automáticamente en la tabla `Solicitantes`.
- **Operaciones CRUD, Grupos y Lotes**: `Nuevo`, `Guardar`, `Editar`, `Actualizar`, `Eliminar` (eliminación lógica protegiendo sesión propia y último Administrador activo), `Cambiar Contraseña`, botones masivos (`Activar`, `Desactivar`, `Mover`, `Cambiar Perfil`), filtros por perfil dinámico y estado, búsqueda, impresión y exportación Excel.
- **Listado de Usuarios (12 columnas)**: incluye columnas `Solicitante / Firma`, `Perfil`, `Grupo`, `Estado`, `Creación` y `Últ. Acceso`.

### 6.2 Pestaña `Solicitados Por y Firmas Digitales`
- Panel dedicado para administrar de forma independiente cualquier registro de **Solicitado Por** (tenga o no usuario del sistema), con carga/reemplazo/eliminación de firma digital en **PNG**, **JPG** o **PDF** (página 1 renderizada a vista previa PNG nítida), filtros rápidos, menú de clic derecho y exportación multiformato.

### 6.3 Pestaña `Perfiles de Usuarios`
- Catálogo dinámico conectado a la tabla `Niveles`: permite al Administrador **crear**, **editar**, **activar/desactivar** y **eliminar** perfiles personalizados (`ID >= 5`) definiendo sus permisos por defecto sobre Trabajos Externos y Mantenedores.
- Los 4 perfiles base del sistema (`Usuario`, `Supervisor`, `Jefatura`, `Administrador`) permanecen protegidos contra borrado o desactivación, y todo perfil con usuarios asignados bloquea su eliminación hasta reasignarlos.
- Los cambios en perfiles se sincronizan inmediatamente con los combos de asignación, filtros y cambio masivo de perfil en `Usuarios y Grupos`.

---

## 7. Perfil, Respaldos y Auditoría

- **Mi Clave**: cada usuario puede cambiar su propia contraseña verificando su clave actual.
- **Configuración** (Administradores): cambiar ubicación de `sispro.db`, editar datos corporativos y generar respaldos en caliente (**Respaldar BD**).
- **Auditoría** (Administradores): consulta de los últimos 300 eventos de seguridad y operación.
