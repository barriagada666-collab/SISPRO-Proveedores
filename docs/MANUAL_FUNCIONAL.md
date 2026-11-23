# Manual Funcional de Usuario — SISPRO v2.0

## 1. Acceso al Sistema

1. Ejecute **SISPRO** desde el Menú Inicio o el escritorio.
2. Ingrese su **Usuario (Alias)** y **Contraseña**.
   - El sistema valida las credenciales contra la base de datos local `sispro.db` mediante hash `PBKDF2-HMAC-SHA256`.
   - Cuentas heredadas de la versión VB6 (`OT_PROV.mdb`) se actualizan automáticamente a `PBKDF2-HMAC-SHA256` en el primer ingreso exitoso.
   - Tras **5 intentos fallidos consecutivos**, la cuenta se bloquea preventivamente durante 30 segundos.
3. Si requiere cambiar la ruta de base de datos antes de ingresar, utilice el botón **Config.**.

---

## 2. Panel de Control (Inicio)

Al iniciar sesión se despliega el **Panel de Control** con cuatro indicadores en vivo:
- **Órdenes de Trabajo**: total histórico y vigente de OTs registradas.
- **Pendientes VB Jefatura**: OTs que aún no cuentan con aprobación de Jefatura.
- **Proveedores Activos**: cantidad de proveedores habilitados en catálogo.
- **Facturas / Guías**: total de documentos tributarios y guías asociadas.

En la tabla inferior se muestran las 40 OTs más recientes; haciendo **doble clic** sobre cualquier fila se abre directamente en el módulo de Trabajos Externos.

---

## 3. Módulo Trabajos Externos — Órdenes de Trabajo (`M1`)

En el panel izquierdo permanece visible el **Árbol de Activos (10 niveles)** con búsqueda rápida por nombre o ID y ruta completa (*breadcrumb*) en la parte inferior.

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
  3. Realice cambios y presione **Modificar**, o elimine con **Eliminar** (bloqueado si la OT ya posee facturas asociadas).
- **Visto Bueno (`VB Supervisor` / `VB Jefatura`)**:
  - Solo visibles para usuarios con permiso `tab_0_VBS` o `tab_0_VBJ` respectivamente.
  - Al marcar/desmarcar la casilla sobre una OT cargada, la aprobación se registra inmediatamente con usuario y fecha en la bitácora.

### 3.2 Pestaña Búsqueda de OT (`tab_0_2`)
- Permite combinar filtros por **Rango de fechas**, **Proveedor**, **Palabra clave / N° OT**, **Nodo seleccionado del árbol** y **Solo pendientes de VB**.
- **Doble clic** sobre un resultado abre la OT para edición o impresión.
- **Exportar Excel** genera un archivo `.xlsx` con formato corporativo y barra de progreso sin requerir Microsoft Excel instalado.

### 3.3 Pestaña Generar / Imprimir OT (`tab_0_3`)
- Cargue una OT existente por su número (o componga ítems manualmente).
- Seleccione la **Firma Digital** deseada (`Ambas Firmas JR + MS`, `Juan Ramos Moyano`, `Marcelo Silva Morgado` o `Sin firma digital`).
- Utilice **Generar Excel** (formato oficial Cambiaso `.xlsx`), **Vista Previa** o **Imprimir**.

---

## 4. Módulo Facturas y Guías de Despacho (`M2`)

### 4.1 Pestaña Guías de Despacho / Facturas (`tab_1_0`)
1. Ingrese **N° Documento**, **Fecha**, **Proveedor**, **Recibido por** y **Detalle**.
2. En el panel derecho busque una OT por su número (**Buscar OT**).
3. Seleccione un ítem disponible, indique el **Valor Trabajo ($)** y presione **Asociar Ítem**.
   - El sistema impide asociar OTs de un proveedor distinto o ítems ya facturados anteriormente.
4. El **Neto**, **IVA (19%)** y **Total c/IVA** se calculan automáticamente.
5. Presione **Localizar / Guardar** para registrar el documento y actualizar el estado de la OT.

### 4.2 Pestaña Buscar Facturas y Guías (`tab_1_1`)
- Filtre por **Proveedor**, **N° Documento** o **N° OT**.
- Al seleccionar una fila se despliegan las OTs asociadas en la grilla inferior.
- Permite **Eliminar** (revirtiendo el estado facturado de los ítems de OT) y **Exportar Excel**.

---

## 5. Módulo Mantenedores (`M3`)

- **Proveedores**: alta, edición, búsqueda instantánea, exportación Excel y desactivación segura (si tiene OTs históricas, se marca inactivo sin romper integridad referencial).
- **Solicitantes**: gestión completa del catálogo de solicitantes de mantención.
- **Árbol de Activos**: edición de nombres, activación/desactivación y creación de subniveles hasta **10 niveles de profundidad**.

---

## 6. Módulo Usuarios y Permisos (`M4`)

- **Ingreso / Edición de Usuarios (`tab_2_0`)**:
  - Validación automática de **RUT chileno** (módulo 11) y correo electrónico.
  - Botón **Aplicar Perfil** carga los permisos recomendados según nivel (`Usuario`, `Supervisor`, `Jefatura`, `Administrador`), permitiendo ajuste fino posterior.
  - Si marca **Es solicitante de trabajos**, se sincroniza automáticamente en la tabla `Solicitantes`.
- **Listado de Usuarios (`tab_2_1`)**: búsqueda en vivo, doble clic para editar, **Vista Previa / Imprimir** y **Exportar Excel**.

---

## 7. Perfil, Respaldos y Auditoría

- **Mi Clave**: cada usuario puede cambiar su propia contraseña verificando su clave actual.
- **Configuración** (Administradores): cambiar ubicación de `sispro.db`, editar datos corporativos y generar respaldos en caliente (**Respaldar BD**).
- **Auditoría** (Administradores): consulta de los últimos 300 eventos de seguridad y operación.
