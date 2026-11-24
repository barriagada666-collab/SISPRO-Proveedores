# Manual Funcional de Usuario — SISPRO v4.2.1

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
- **Banner Hero Principal**: encabezado **Trabajos Externos — Crear y Gestionar Órdenes de Trabajo**, resumen en vivo de aprobadores vigentes **VB Supervisor** y **VB Jefatura**, y botón dorado principal **Nueva OT Externa** para iniciar inmediatamente una nueva orden de trabajo.
- **KPIs Interactivos (5 tarjetas clicables)**: **OTs Externas** (`7.284`), **Proveedores** (`214`), **Activos Reales** (`1.823` activos), **Pendientes VB** (sin VBS o sin VBJ) y **Solicitantes / Firmas** (con firma `PNG`/`JPG`/`PDF`).
- **Gráficas en vivo**: **Evolución Anual de OTs**, **Distribución por Planta** (Donut) y **Top 6 Proveedores**.
- En la tabla inferior se muestran las OTs más recientes con columnas **VBS** y **VBJ** identificando al aprobador (`Sí (<alias>)` o `Pendiente`); haciendo **doble clic** sobre cualquier fila se abre directamente en el módulo de **Trabajos Externos**.

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
  2. Seleccione **Fecha**, **Proveedor** y **Solicitado por**.
  3. Seleccione en el árbol izquierdo la planta/sección/equipo destino (mínimo nivel 2).
  4. Ingrese **Cantidad** y **Descripción del trabajo**, y presione **Agregar Ítem** (puede agregar múltiples ítems; doble clic sobre la grilla permite editarlos).
  5. Presione **Guardar Nueva**.
- **Consultar / Modificar / Eliminar OT existente**:
  1. Ingrese el **N° OT** y presione **Buscar OT** (o tecla `Enter`).
  2. El árbol se expande automáticamente al activo correspondiente.
  3. Realice cambios y presione **Modificar**, o elimine con **Eliminar** cuando corresponda.
- **Visto Bueno (`VB Supervisor` / `VB Jefatura`)**:
  - Solo visibles/habilitados para usuarios con permiso `tab_0_VBS` o `tab_0_VBJ` respectivamente.
  - Junto a cada casilla se muestra en tiempo real **quién aprobó y cuándo** (`✓ Aprobado por <Usuario> (<fecha>)`) o los **usuarios autorizados vigentes** cuando está `Pendiente`.
  - Al marcar/desmarcar la casilla sobre una OT cargada, la aprobación se registra inmediatamente con usuario y fecha en la base y en la bitácora.

### 3.2 Pestaña Búsqueda de OT (`tab_0_2`)
- Permite combinar filtros por **Rango de fechas**, **Proveedor**, **Palabra clave / N° OT**, **Nodo seleccionado del árbol** y **Solo pendientes de VB**.
- Muestra las columnas `VBS` y `VBJ` con el alias de quien aprobó o `Pendiente`.
- Dispone de **ordenamiento por clic en cualquier columna** y **barra de paginación inferior** (`50`, `100`, `200`, `500` registros por página).
- **Doble clic** o menú de **clic derecho** sobre un resultado abre la OT para edición o impresión.
- **Exportar** genera archivos `.xlsx`, `.pdf`, `.docx` o `.csv` con todos los resultados filtrados e identidad de aprobadores VB.

### 3.3 Pestaña Generar / Imprimir OT (`tab_0_3`)
- Cargue una OT existente por su número (o componga ítems manualmente).
- Revise las tarjetas alineadas de **Datos del Proveedor** (`Proveedor`, `Dirección`, `Correo`) y **Datos del Solicitante y Firma Técnico** (`Solicitante`, `Nombre`, `Correo`, `Teléfono`, `Activo` junto con la vista previa de firma digital).
- Seleccione libremente las **Firmas al pie** mediante casillas independientes (**Jefatura**, **Técnico (Solicitante)**, **Supervisor**) — puede combinar cualquiera o **desmarcar todas** para dejar el pie de página sin firmas.
- Todas las exportaciones/impresiones individuales de OT (`.xlsx`, `.pdf`, `.docx`, impresión directa y vista previa) destacan el **N° de OT grande en la esquina superior derecha**, muestran **Nombre Apellido**, correo, teléfono y **Activo** en *Datos del Solicitante*, presentan la firma del solicitante como **Técnico**, omiten líneas de `VB` y columnas `Guía / Estado` o `Valor / Ref.`, y solo imprimen las firmas seleccionadas.
- Utilice **Exportar OT** (`.xlsx`, `.pdf`, `.docx`, `.csv`), **Vista Previa** o **Imprimir**.

---

## 4. Módulo Mantenedores (`M2`)

- **Proveedores**: alta, edición, activación/desactivación rápida mediante menú de clic derecho, búsqueda instantánea, exportación multiformato (Excel, PDF, Word, CSV) y eliminación lógica automática si existen OTs históricas.
- **Solicitantes y Firmas**: catálogo compartido (`RequesterManagerControl`) sincronizado al instante con `Usuarios y Solicitantes` para gestionar **Solicitado Por** y adjuntar su **Firma Digital** en `PNG`, `JPG` o `PDF`.
- **Árbol de Activos**: visualización del árbol industrial de 10 niveles con íconos por tipo estructural (`Empresa`, `Planta`, `Sección / Área`, `Máquina`, `Módulo`, `Componente`). Solo usuarios con perfil **Administrador** pueden modificar la ficha técnica, crear hijos o acceder al módulo dedicado **Editor de Árbol**.

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

## 6. Módulo Usuarios y Solicitantes (Exclusivo Administrador)

Disponible únicamente para usuarios con perfil **Administrador** (validado en interfaz y backend) bajo dos pestañas integradas:

### 6.1 Pestaña `Usuarios, Aprobadores VB y Grupos`
- **Franja `APROBADORES OT VIGENTES`**: muestra en tiempo real qué usuarios activos tienen asignado el visto bueno **VB Supervisor** (`tab_0_VBS`) y **VB Jefatura** (`tab_0_VBJ`).
- **Sección `DATOS DEL USUARIO`**: campos completos (ID automático, Usuario, Nombre, Apellido, Correo, Teléfono, Perfil, Grupo, Estado, Contraseña, Confirmar contraseña, cambio obligatorio de clave, Fecha de creación, Último acceso) + botón **Permisos RBAC**.
- **Integración directa `Solicitante OT` + Firma Digital**: al marcar `Solicitante OT` durante la creación o edición de un usuario, se habilita el adjunto y vista previa de su firma digital (`PNG`, `JPG` o `PDF`, máx. 10 MB), sincronizándose automáticamente en la tabla `Solicitantes` para quedar seleccionable en el campo *Solicitado por* de las OTs.
- **Operaciones CRUD, Grupos y Lotes**: `Nuevo`, `Guardar`, `Editar`, `Actualizar`, `Eliminar` (eliminación lógica protegiendo sesión propia y último Administrador activo), `Cambiar Contraseña`, botones masivos (`Activar`, `Desactivar`, `Mover`, `Cambiar Perfil`), filtros, búsqueda, impresión y exportación Excel.
- **Listado de Usuarios (14 columnas)**: incluye columnas explícitas **VBS**, **VBJ** y **Solicitante / Firma** además de estado (`● Activo` / `● Inactivo`) y grupo.

### 6.2 Pestaña `Solicitados Por y Firmas Digitales`
- Panel dedicado para administrar de forma independiente cualquier registro de **Solicitado Por** (tenga o no usuario del sistema), con carga/reemplazo/eliminación de firma digital en **PNG**, **JPG** o **PDF** (página 1 renderizada a vista previa PNG nítida), filtros rápidos (`Todos`, `Activos`, `Inactivos`, `Con Firma Digital`, `Sin Firma Digital`), menú de clic derecho y exportación multiformato.

---

## 7. Perfil, Respaldos y Auditoría

- **Mi Clave**: cada usuario puede cambiar su propia contraseña verificando su clave actual.
- **Configuración** (Administradores): cambiar ubicación de `sispro.db`, editar datos corporativos y generar respaldos en caliente (**Respaldar BD**).
- **Auditoría** (Administradores): consulta de los últimos 300 eventos de seguridad y operación.
