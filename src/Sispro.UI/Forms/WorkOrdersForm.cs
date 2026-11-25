using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo de Trabajos Externos / Órdenes de Trabajo.
/// Centraliza la Búsqueda de OT con CRUD completo (Nuevo, Consultar, Editar, Eliminar)
/// e Ingreso / Edición con Tipo de OT (Externa/Interna), control de visibilidad de Valor Neto,
/// Observaciones, selección de firmantes desde Personal/Usuarios y Vista Previa.
/// </summary>
public sealed class WorkOrdersForm : UserControl
{
    private static readonly CultureInfo ClCulture = new("es-CL");
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly AssetTreePanel _assetTree;
    private readonly TabControl _tabs;
    private readonly TabPage _tabEntry;
    private readonly TabPage _tabSearch;
    private readonly Label _lblNextOtHeader;

    internal Button BtnNewExternalOt { get; }

    // Tab Ingreso / Edición OT
    private readonly TextBox _txtNumeroOt;
    private readonly ComboBox _cmbTipoOrdenEntry;
    private readonly ComboBox _cmbEstadoEntry;
    private readonly Button _btnLookupOt;
    private readonly Button _btnNextOt;
    private readonly ComboBox _cmbProviderEntry;
    private readonly ComboBox _cmbRequesterEntry;
    private readonly DateTimePicker _dtFechaOt;
    private readonly NumericUpDown _numCantidad;
    private readonly TextBox _txtDetalle;
    private readonly Button _btnAddDetail;
    private readonly Button _btnRemoveDetail;
    private readonly DataGridView _gridEntryDetails;
    private readonly Font _valorNetoFont = new("Segoe UI", 15f, FontStyle.Bold);
    private readonly Label _lblNetTitle;
    private readonly Label _lblEntrySigHint;
    private readonly Button _btnSaveOt;
    private readonly Button _btnSaveAndExportOt;
    private readonly Button _btnEntryPreview;
    private readonly Button _btnUpdateOt;
    private readonly Button _btnDeleteOt;
    private readonly Button _btnClearEntry;
    private readonly Label _lblEntryStatus;
    private readonly List<WorkOrderDetail> _entryDetails = [];
    private WorkOrder? _loadedOrder;
    private decimal _cachedValorNeto;
    private bool _suppressEntrySignerSync;
    private bool _entryCatalogHasHistorical;
    private IReadOnlyList<SignaturePersonOption> _signatureCatalog = [];

    internal TextBox TxtObservacionEntry { get; }
    internal CheckBox ChkMostrarValorNetoEntry { get; }
    internal TextBox TxtValorNetoEntry { get; }
    internal ComboBox CmbTipoOrdenEntry => _cmbTipoOrdenEntry;
    internal ComboBox CmbEstadoEntry => _cmbEstadoEntry;
    internal CheckBox ChkEntrySigJefatura { get; }
    internal CheckBox ChkEntrySigTecnico { get; }
    internal CheckBox ChkEntrySigSupervisor { get; }
    internal ComboBox CmbEntrySigJefatura { get; }
    internal ComboBox CmbEntrySigTecnico { get; }
    internal ComboBox CmbEntrySigSupervisor { get; }
    internal Button BtnSaveAndExportOt => _btnSaveAndExportOt;
    internal Button BtnEntryPreview => _btnEntryPreview;
    internal SignatureOption SelectedSignature => GetEntrySelectedSignature();
    internal TabControl TabsMain => _tabs;

    // Tab Búsqueda de OT (CRUD completo)
    private readonly DateRangeFilterControl _searchDateRange;
    private readonly CheckBox _chkFilterProvider;
    private readonly ComboBox _cmbProviderSearch;
    private readonly CheckBox _chkFilterRequester;
    private readonly ComboBox _cmbRequesterSearch;
    private readonly CheckBox _chkFilterText;
    private readonly TextBox _txtSearchKeyword;
    private readonly CheckBox _chkFilterStatus;
    private readonly ComboBox _cmbStatusSearch;
    private readonly CheckBox _chkFilterTree;
    private readonly Button _btnExecuteSearch;
    private readonly Button _btnClearSearch;
    private readonly Button _btnExportSearch;
    private readonly DataGridView _gridSearchResults;
    private readonly GridPagerBar _searchPager;
    private readonly ProgressBar _exportProgress;
    private readonly Label _lblSearchSummary;
    private readonly GridCheckboxSelectionController<WorkOrderSearchRow> _searchSelection;
    private readonly GridExportColumnHeaderController _searchExportColumns;
    private IReadOnlyList<WorkOrderSearchRow> _lastSearchResults = [];
    private bool _syncSubscribed;

    internal Button BtnSearchNewOt { get; }
    internal Button BtnSearchConsultOt { get; }
    internal Button BtnSearchEditOt { get; }
    internal Button BtnSearchDeleteOt { get; }
    internal CheckBox ChkSelectAllSearchVisible { get; }
    internal Label LblSearchSelectedCount { get; }
    internal Button BtnExportSelectedSearch { get; }
    internal Button BtnClearSearchSelection { get; }
    internal Button BtnSearchExportColumns { get; }
    internal DataGridView GridSearchResults => _gridSearchResults;
    internal GridCheckboxSelectionController<WorkOrderSearchRow> SearchSelection => _searchSelection;
    internal GridExportColumnHeaderController SearchExportColumns => _searchExportColumns;

    public WorkOrdersForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        TableLayoutPanel mainSplit = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = CambiasoTheme.Surface,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 296));
        mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Panel leftPane = new()
        {
            Dock = DockStyle.Fill,
            BackColor = CambiasoTheme.BorderColor,
            Padding = new Padding(0, 0, 1, 0),
            Margin = Padding.Empty
        };

        Panel rightPane = new()
        {
            Dock = DockStyle.Fill,
            BackColor = CambiasoTheme.Surface,
            Margin = Padding.Empty
        };

        _assetTree = new AssetTreePanel(_ctx.MaintainerService, _ctx.AssetTreeService, _session) { Dock = DockStyle.Fill };
        _assetTree.ExtraMenuItemsProvider = node =>
        [
            ContextMenuHelper.Item(
                "Buscar OTs de este nodo",
                AppIcon.Search,
                CambiasoTheme.Accent,
                () => SearchByTreeNode(node),
                enabled: _session.Permisos.BusquedaOt)
        ];
        leftPane.Controls.Add(_assetTree);

        Panel topActionStrip = new()
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(10, 7, 10, 6)
        };
        topActionStrip.Paint += (_, e) =>
        {
            using Pen borderPen = new(CambiasoTheme.BorderColor, 1);
            e.Graphics.DrawLine(borderPen, 0, topActionStrip.Height - 1, topActionStrip.Width, topActionStrip.Height - 1);
        };

        BtnNewExternalOt = CambiasoTheme.CreateButton("Nueva OT", AppIcon.Add, ButtonTone.Gold, _toolTip, "Iniciar inmediatamente una nueva Orden de Trabajo (externa o interna)", width: 138, height: 36);
        BtnNewExternalOt.Enabled = _session.Permisos.IngresoOt;
        BtnNewExternalOt.Click += (_, _) => StartNewExternalOrder();

        _lblNextOtHeader = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Margin = new Padding(14, 9, 4, 0)
        };

        FlowLayoutPanel actionFlow = new() { Dock = DockStyle.Fill, WrapContents = false };
        actionFlow.Controls.AddRange([BtnNewExternalOt, _lblNextOtHeader]);
        topActionStrip.Controls.Add(actionFlow);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.LabelFont,
            Padding = new Point(14, 6)
        };

        // --- TAB INGRESO / EDICIÓN ---
        _tabEntry = new TabPage("Ingreso / Edición de OT") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtNumeroOt = new TextBox { Width = 100, MaxLength = 8 };
        _txtNumeroOt.KeyPress += DigitsOnly_KeyPress;
        _txtNumeroOt.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LookupEntryOrder();
            }
        };
        _cmbTipoOrdenEntry = new ComboBox
        {
            Name = "cmbTipoOrdenEntry",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 108
        };
        _cmbTipoOrdenEntry.Items.AddRange(["Externa", "Interna"]);
        _cmbTipoOrdenEntry.SelectedIndex = 0;
        _btnLookupOt = CambiasoTheme.CreateButton("Buscar OT", AppIcon.Search, ButtonTone.Secondary, _toolTip, "Cargar orden de trabajo existente por número", width: 112, height: 28);
        _btnLookupOt.Click += (_, _) => LookupEntryOrder();

        _btnNextOt = CambiasoTheme.CreateButton("Siguiente N°", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Sugerir el siguiente correlativo disponible", width: 122, height: 28);
        _btnNextOt.Click += (_, _) => _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString();

        _cmbProviderEntry = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        _cmbRequesterEntry = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 142 };
        _cmbEstadoEntry = new ComboBox
        {
            Name = "cmbEstadoOtEntry",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 112
        };
        _cmbEstadoEntry.Items.AddRange(WorkOrderStatusExtensions.CreateComboOptions().Cast<object>().ToArray());
        _cmbEstadoEntry.SelectedIndex = 0;
        _toolTip.SetToolTip(_cmbEstadoEntry, "Estado de la Orden de Trabajo (Abierta, En Curso, Modificada, Terminada, Cerrada o Anulada)");
        _cmbRequesterEntry.SelectedIndexChanged += (_, _) =>
        {
            if (!_suppressEntrySignerSync &&
                _loadedOrder is null &&
                CmbEntrySigTecnico is not null &&
                _cmbRequesterEntry.SelectedItem is Requester req)
            {
                string reqName = string.IsNullOrWhiteSpace(req.NombreCompleto) ? req.Alias : req.NombreCompleto;
                SelectSignerInCombo(CmbEntrySigTecnico, reqName);
            }
            UpdateEntrySignatureHint();
        };
        _dtFechaOt = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 120, Value = DateTime.Today };

        _numCantidad = new NumericUpDown { Minimum = 1, Maximum = 99999, Value = 1, Width = 76 };
        _txtDetalle = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        _btnAddDetail = CambiasoTheme.CreateButton("Agregar Ítem", AppIcon.Add, ButtonTone.Accent, _toolTip, "Agregar trabajo al detalle de la OT", width: 132, height: 32);
        _btnAddDetail.Click += (_, _) => AddEntryDetail();

        _btnRemoveDetail = CambiasoTheme.CreateButton("Quitar Ítem", AppIcon.Remove, ButtonTone.Secondary, _toolTip, "Quitar fila seleccionada del detalle", width: 124, height: 32);
        _btnRemoveDetail.Click += (_, _) => RemoveSelectedEntryDetail();

        TxtObservacionEntry = new TextBox
        {
            Name = "txtOtObservacion",
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            PlaceholderText = "Observaciones, condiciones de entrega, alcance adicional..."
        };

        ChkMostrarValorNetoEntry = new CheckBox
        {
            Name = "chkMostrarValorNetoOt",
            Text = "Mostrar valor neto en OT externa",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Margin = new Padding(0, 4, 8, 2)
        };
        _toolTip.SetToolTip(ChkMostrarValorNetoEntry, "Controla si el Valor Neto se muestra en el formulario y en la OT externa generada sin borrar los datos económicos guardados.");

        _lblNetTitle = new Label
        {
            Text = "Valor Neto ($):",
            Font = CambiasoTheme.SectionFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            AutoSize = true,
            Margin = new Padding(0, 6, 8, 0)
        };

        TxtValorNetoEntry = new TextBox
        {
            Name = "txtOtValorNeto",
            Font = _valorNetoFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            BackColor = Color.FromArgb(255, 250, 236),
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Right,
            Width = 175,
            Text = "$ 0"
        };
        TxtValorNetoEntry.Leave += (_, _) => FormatAndValidateNetValueInput();
        TxtValorNetoEntry.TextChanged += (_, _) =>
        {
            if (ChkMostrarValorNetoEntry.Checked && WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out decimal v) && v >= 0m)
            {
                _cachedValorNeto = v;
            }
        };
        _toolTip.SetToolTip(TxtValorNetoEntry, "Valor neto de la orden en pesos chilenos ($ >= 0)");
        ChkMostrarValorNetoEntry.CheckedChanged += (_, _) => ApplyNetValueVisibilityImmediate();

        ChkEntrySigJefatura = new CheckBox
        {
            Name = "chkEntrySigJefatura",
            Text = "Jefatura",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 4, 4, 2)
        };
        ChkEntrySigTecnico = new CheckBox
        {
            Name = "chkEntrySigTecnico",
            Text = "Técnico",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 4, 4, 2)
        };
        ChkEntrySigSupervisor = new CheckBox
        {
            Name = "chkEntrySigSupervisor",
            Text = "Supervisor",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 4, 4, 2)
        };
        CmbEntrySigJefatura = CreateSignerCombo("cmbEntrySigJefatura");
        CmbEntrySigTecnico = CreateSignerCombo("cmbEntrySigTecnico");
        CmbEntrySigSupervisor = CreateSignerCombo("cmbEntrySigSupervisor");

        ChkEntrySigJefatura.CheckedChanged += (_, _) =>
        {
            CmbEntrySigJefatura.Enabled = ChkEntrySigJefatura.Checked;
            UpdateEntrySignatureHint();
        };
        ChkEntrySigTecnico.CheckedChanged += (_, _) =>
        {
            CmbEntrySigTecnico.Enabled = ChkEntrySigTecnico.Checked;
            UpdateEntrySignatureHint();
        };
        ChkEntrySigSupervisor.CheckedChanged += (_, _) =>
        {
            CmbEntrySigSupervisor.Enabled = ChkEntrySigSupervisor.Checked;
            UpdateEntrySignatureHint();
        };
        CmbEntrySigJefatura.SelectedIndexChanged += (_, _) => UpdateEntrySignatureHint();
        CmbEntrySigTecnico.SelectedIndexChanged += (_, _) => UpdateEntrySignatureHint();
        CmbEntrySigSupervisor.SelectedIndexChanged += (_, _) => UpdateEntrySignatureHint();

        _lblEntrySigHint = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            AutoEllipsis = true
        };

        _gridEntryDetails = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridEntryDetails);
        _gridEntryDetails.CellDoubleClick += (_, _) => EditSelectedEntryDetail();
        ContextMenuHelper.AttachToGrid(_gridEntryDetails, idx =>
            idx >= 0 && idx < _entryDetails.Count
                ?
                [
                    ContextMenuHelper.Item("Visualizar OT (Vista Previa)", AppIcon.Preview, CambiasoTheme.Primary, ShowEntryPreview),
                    ContextMenuHelper.Separator(),
                    ContextMenuHelper.Item("Editar ítem", AppIcon.Edit, CambiasoTheme.Accent, EditSelectedEntryDetail),
                    ContextMenuHelper.Item("Quitar ítem", AppIcon.Remove, CambiasoTheme.Danger, RemoveSelectedEntryDetail)
                ]
                :
                [
                    ContextMenuHelper.Item("Visualizar OT (Vista Previa)", AppIcon.Preview, CambiasoTheme.Primary, ShowEntryPreview)
                ]);

        _btnSaveOt = CambiasoTheme.CreateButton("Guardar OT", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar nueva orden de trabajo en base de datos", width: 122);
        _btnSaveOt.Click += (_, _) => SaveNewOrder();

        _btnSaveAndExportOt = CambiasoTheme.CreateButton("Guardar y Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Guardar (nueva o cambios) y exportar documento oficial", width: 158);
        _btnSaveAndExportOt.Enabled = _session.Permisos.IngresoOt;
        _btnSaveAndExportOt.Click += (_, _) => SaveAndExportOrder();

        _btnEntryPreview = CambiasoTheme.CreateButton("Vista Previa", AppIcon.Preview, ButtonTone.Secondary, _toolTip, "Visualizar vista previa de impresión de la OT actual", width: 122);
        _btnEntryPreview.Enabled = _session.Permisos.MenuOrdenesTrabajo;
        _btnEntryPreview.Click += (_, _) => ShowEntryPreview();

        _btnUpdateOt = CambiasoTheme.CreateButton("Modificar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Guardar cambios en la orden cargada", width: 108);
        _btnUpdateOt.Enabled = false;
        _btnUpdateOt.Click += (_, _) => UpdateLoadedOrder();

        _btnDeleteOt = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar orden de trabajo cargada", width: 102);
        _btnDeleteOt.Enabled = false;
        _btnDeleteOt.Click += (_, _) => DeleteLoadedOrder();

        _btnClearEntry = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Limpiar formulario para nueva OT", width: 98);
        _btnClearEntry.Click += (_, _) => ClearEntryForm();

        _lblEntryStatus = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0),
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted
        };

        BuildEntryTabLayout();

        // --- TAB 1: BÚSQUEDA DE OT ---
        _tabSearch = new TabPage("Búsqueda de OT") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _searchDateRange = new DateRangeFilterControl(compact: true);

        _chkFilterProvider = new CheckBox { Text = "Proveedor", AutoSize = true };
        _cmbProviderSearch = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175, Enabled = false };
        _chkFilterProvider.CheckedChanged += (_, _) => _cmbProviderSearch.Enabled = _chkFilterProvider.Checked;

        _chkFilterRequester = new CheckBox { Text = "Solicitante", AutoSize = true };
        _cmbRequesterSearch = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Enabled = false };
        _chkFilterRequester.CheckedChanged += (_, _) => _cmbRequesterSearch.Enabled = _chkFilterRequester.Checked;

        _chkFilterText = new CheckBox { Text = "Palabra clave / N° OT", AutoSize = true, Checked = true };
        _txtSearchKeyword = new TextBox { Width = 152, PlaceholderText = "Ej. motor, MAI, 2815..." };
        _txtSearchKeyword.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ExecuteSearch();
            }
        };
        _chkFilterText.CheckedChanged += (_, _) => _txtSearchKeyword.Enabled = _chkFilterText.Checked;

        _chkFilterStatus = new CheckBox { Text = "Estado", AutoSize = true };
        _cmbStatusSearch = new ComboBox
        {
            Name = "cmbEstadoOtSearch",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 114,
            Enabled = false
        };
        _cmbStatusSearch.Items.AddRange(WorkOrderStatusExtensions.CreateComboOptions().Cast<object>().ToArray());
        _cmbStatusSearch.SelectedIndex = 0;
        _chkFilterStatus.CheckedChanged += (_, _) => _cmbStatusSearch.Enabled = _chkFilterStatus.Checked;

        _chkFilterTree = new CheckBox { Text = "Nodo árbol", AutoSize = true };

        _btnExecuteSearch = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Ejecutar búsqueda combinada de órdenes de trabajo", width: 102, height: 30);
        _btnExecuteSearch.Click += (_, _) => ExecuteSearch();

        _btnClearSearch = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Restablecer filtros y resultados", width: 98, height: 30);
        _btnClearSearch.Click += (_, _) => ClearSearchTab();

        _btnExportSearch = CambiasoTheme.CreateButton("Exportar Lista", AppIcon.Download, ButtonTone.Secondary, _toolTip, "Exportar todos los resultados filtrados con las columnas marcadas a Excel, PDF, Word o CSV", width: 138, height: 30);
        _btnExportSearch.Click += (_, _) => ExportSearchResultsList();

        BtnSearchNewOt = CambiasoTheme.CreateButton("Nuevo", AppIcon.Add, ButtonTone.Primary, _toolTip, "Crear una nueva Orden de Trabajo (externa o interna)", width: 96, height: 30);
        BtnSearchNewOt.Enabled = _session.Permisos.IngresoOt;
        BtnSearchNewOt.Click += (_, _) => StartNewExternalOrder();

        BtnSearchConsultOt = CambiasoTheme.CreateButton("Consultar", AppIcon.Preview, ButtonTone.Secondary, _toolTip, "Consultar todos los datos completos de la OT seleccionada", width: 112, height: 30);
        BtnSearchConsultOt.Enabled = false;
        BtnSearchConsultOt.Click += (_, _) => ConsultSelectedSearchOrder();

        BtnSearchEditOt = CambiasoTheme.CreateButton("Editar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Abrir la OT seleccionada para editar sus datos", width: 96, height: 30);
        BtnSearchEditOt.Enabled = false;
        BtnSearchEditOt.Click += (_, _) => OpenSelectedSearchOrder();

        BtnSearchDeleteOt = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar la(s) orden(es) de trabajo seleccionada(s) con confirmación y auditoría", width: 104, height: 30);
        BtnSearchDeleteOt.Enabled = false;
        BtnSearchDeleteOt.Click += (_, _) => DeleteSelectedSearchOrders();

        ChkSelectAllSearchVisible = new CheckBox
        {
            Name = "chkSelectAllSearchOt",
            Text = "Seleccionar todos (página visible)",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            Margin = new Padding(2, 7, 8, 0)
        };

        LblSearchSelectedCount = new Label
        {
            Name = "lblSearchSelectedCount",
            Text = "OT seleccionadas: 0",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Margin = new Padding(0, 8, 8, 0)
        };

        BtnExportSelectedSearch = CambiasoTheme.CreateButton("Exportar seleccionadas", AppIcon.Download, ButtonTone.Primary, _toolTip,
            "Exportar únicamente las órdenes de trabajo marcadas (y las columnas seleccionadas) a Excel, PDF, Word o CSV", width: 184, height: 30);
        BtnExportSelectedSearch.Enabled = false;
        BtnExportSelectedSearch.Click += (_, _) => ExportSelectedWorkOrders();

        BtnClearSearchSelection = CambiasoTheme.CreateButton("Limpiar selección", AppIcon.Eraser, ButtonTone.Secondary, _toolTip,
            "Desmarcar todas las órdenes de trabajo seleccionadas", width: 150, height: 30);
        BtnClearSearchSelection.Enabled = false;

        BtnSearchExportColumns = CambiasoTheme.CreateButton("Campos (10/10)", AppIcon.Layers, ButtonTone.Secondary, _toolTip,
            "Elegir qué columnas y campos adicionales se incluirán al exportar (también puede marcar/desmarcar directamente sobre los encabezados de la tabla)", width: 150, height: 30);
        BtnSearchExportColumns.Name = "btnSearchExportColumns";
        BtnSearchExportColumns.Margin = new Padding(14, 3, 3, 3);
        BtnSearchExportColumns.Click += (_, _) => ShowExportColumnsQuickMenu();

        _gridSearchResults = new DataGridView { Name = "gridSearchOtResults", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridSearchResults);
        _gridSearchResults.ColumnHeadersHeight = 48;
        _searchSelection = new GridCheckboxSelectionController<WorkOrderSearchRow>(
            _gridSearchResults,
            r => r.IdOt,
            ChkSelectAllSearchVisible,
            UpdateSearchSelectionState);
        _searchExportColumns = new GridExportColumnHeaderController(
            _gridSearchResults,
            [
                (nameof(WorkOrderGridRow.NumeroOt), WorkOrderExportField.NumeroOt),
                (nameof(WorkOrderGridRow.Fecha), WorkOrderExportField.Fecha),
                (nameof(WorkOrderGridRow.Proveedor), WorkOrderExportField.Proveedor),
                (nameof(WorkOrderGridRow.Solicitante), WorkOrderExportField.Solicitante),
                (nameof(WorkOrderGridRow.Cantidad), WorkOrderExportField.Cantidad),
                (nameof(WorkOrderGridRow.Detalle), WorkOrderExportField.Detalle),
                (nameof(WorkOrderGridRow.Observacion), WorkOrderExportField.Observacion),
                (nameof(WorkOrderGridRow.ValorNeto), WorkOrderExportField.ValorNeto),
                (nameof(WorkOrderGridRow.Estado), WorkOrderExportField.Estado),
                (nameof(WorkOrderGridRow.Firmas), WorkOrderExportField.Firmas)
            ],
            UpdateExportColumnsButtonState);
        BtnClearSearchSelection.Click += (_, _) => _searchSelection.ClearSelection();
        UpdateExportColumnsButtonState();

        _gridSearchResults.CellDoubleClick += (_, _) => OpenSelectedSearchOrder();
        _gridSearchResults.SelectionChanged += (_, _) => UpdateSearchSelectionState();
        _gridSearchResults.CellFormatting += (_, e) =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults, e.RowIndex);
            if (row is null || e.CellStyle is null || e.ColumnIndex < 0 || e.ColumnIndex >= _gridSearchResults.Columns.Count) return;

            string prop = _gridSearchResults.Columns[e.ColumnIndex].DataPropertyName;
            if (string.Equals(prop, nameof(WorkOrderGridRow.Estado), StringComparison.Ordinal))
            {
                e.CellStyle.Font = CambiasoTheme.LabelFont;
                e.CellStyle.ForeColor = WorkOrderStatusUi.GetColor(row.Estado);
            }
            else if (string.Equals(prop, nameof(WorkOrderGridRow.ValorNeto), StringComparison.Ordinal))
            {
                if (!row.MostrarValorNeto)
                {
                    e.Value = "Oculto";
                    e.FormattingApplied = true;
                    e.CellStyle.Font = CambiasoTheme.SmallItalicFont;
                    e.CellStyle.ForeColor = CambiasoTheme.TextMuted;
                }
                else
                {
                    e.CellStyle.Font = CambiasoTheme.LabelFont;
                    e.CellStyle.ForeColor = CambiasoTheme.PrimaryDark;
                }
            }
        };
        ContextMenuHelper.AttachToGrid(_gridSearchResults, BuildSearchContextMenuItems);

        _searchPager = new GridPagerBar(100);
        _searchPager.PageChanged += (_, _) => RenderSearchResultsPage();

        _exportProgress = new ProgressBar { Width = 220, Height = 20, Visible = false, Minimum = 0, Maximum = 100 };
        _lblSearchSummary = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, ForeColor = CambiasoTheme.TextMuted, Text = "Use los filtros y presione Buscar." };

        BuildSearchTabLayout();

        // Enforce strict RBAC tab visibility (Búsqueda de OT centralizada con CRUD como pestaña principal)
        if (_session.Permisos.BusquedaOt)
        {
            _tabs.TabPages.Add(_tabSearch);
        }

        if (_session.Permisos.IngresoOt)
        {
            _tabs.TabPages.Add(_tabEntry);
        }

        rightPane.Controls.Add(_tabs);
        rightPane.Controls.Add(topActionStrip);
        mainSplit.Controls.Add(leftPane, 0, 0);
        mainSplit.Controls.Add(rightPane, 1, 0);
        Controls.Add(mainSplit);

        LoadCatalogs();
        ApplyDefaultSignersForDraft();
        RefreshNextOtBadge();
        _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString(CultureInfo.InvariantCulture);
        RefreshEntryDetailsGrid();

        if (_tabs.TabPages.Contains(_tabSearch))
        {
            ExecuteSearchCore(showEmptyMessage: false);
        }

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & (DataChangeKind.Catalogs | DataChangeKind.WorkOrders)) == 0)
        {
            return;
        }

        void RefreshView()
        {
            if (IsDisposed)
            {
                return;
            }

            if ((e.Kind & DataChangeKind.Catalogs) != 0)
            {
                ReloadCatalogs();
            }

            RefreshNextOtBadge();
            if ((e.Kind & DataChangeKind.WorkOrders) != 0)
            {
                if (_loadedOrder is not null)
                {
                    WorkOrder? freshLoaded = _ctx.WorkOrderService.GetById(_loadedOrder.IdOt);
                    if (freshLoaded is not null && freshLoaded.Estado != _loadedOrder.Estado)
                    {
                        _loadedOrder.Estado = freshLoaded.Estado;
                        SelectEntryStatus(freshLoaded.Estado);
                    }
                }

                if (_tabs.SelectedTab == _tabSearch && _lastSearchResults.Count > 0)
                {
                    ExecuteSearchCore(showEmptyMessage: false);
                }
            }
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshView);
        else if (!InvokeRequired) RefreshView();
    }

    public void OpenOrderByNumber(int numeroOt)
    {
        if (_tabs.TabPages.Contains(_tabEntry))
        {
            _tabs.SelectedTab = _tabEntry;
            _txtNumeroOt.Text = numeroOt.ToString(CultureInfo.InvariantCulture);
            LookupEntryOrder();
        }
        else
        {
            ConsultOrderByNumber(numeroOt);
        }
    }

    public void StartNewExternalOrder()
    {
        if (!_session.Permisos.IngresoOt || !_tabs.TabPages.Contains(_tabEntry))
        {
            CambiasoTheme.ShowWarning(this, "No posee permisos para crear órdenes de trabajo.");
            return;
        }

        _tabs.SelectedTab = _tabEntry;
        ClearEntryForm();
        _cmbProviderEntry.Focus();
    }

    public void OpenSearchTab()
    {
        if (_tabs.TabPages.Contains(_tabSearch))
        {
            _tabs.SelectedTab = _tabSearch;
            ExecuteSearchCore(showEmptyMessage: false);
        }
    }

    public void ReloadCatalogs()
    {
        LoadCatalogs();
        _assetTree.ReloadPreservingSelection();
    }

    private void RefreshNextOtBadge()
    {
        int next = _ctx.WorkOrderService.GetNextWorkOrderNumber();
        _lblNextOtHeader.Text = $"Siguiente N° OT disponible: {next.ToString(CultureInfo.InvariantCulture)}";
    }

    private void SearchByTreeNode(AssetNode node)
    {
        if (!_session.Permisos.BusquedaOt || !_tabs.TabPages.Contains(_tabSearch))
        {
            return;
        }

        _chkFilterTree.Checked = true;
        _tabs.SelectedTab = _tabSearch;
        int realDepth = _ctx.MaintainerService.GetAssetPath(node.IdActivo).Count;
        if (realDepth < 2)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione al menos una planta (nivel 2 o superior) para filtrar por rama.");
            return;
        }

        ExecuteSearch();
    }

    private void DeleteSearchOrder(int numeroOt)
    {
        if (!_session.Permisos.IngresoOt)
        {
            return;
        }

        WorkOrder? target = _ctx.WorkOrderService.GetByNumber(numeroOt);
        OpenOrderByNumber(numeroOt);
        DeleteLoadedOrder();
        if (target is not null)
        {
            _searchSelection.SetSelected(target.IdOt, false);
        }

        ExecuteSearch();
    }

    private void BuildEntryTabLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 198));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        GroupBox grpHeader = CambiasoTheme.CreateGroupBox("Datos Generales de la Orden de Trabajo");
        grpHeader.Dock = DockStyle.Fill;

        FlowLayoutPanel row1 = new() { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        row1.Controls.AddRange(
        [
            WrapLabel("N° OT:", _txtNumeroOt),
            _btnLookupOt,
            _btnNextOt,
            WrapLabel("Tipo OT:", _cmbTipoOrdenEntry),
            WrapLabel("Fecha:", _dtFechaOt)
        ]);

        FlowLayoutPanel row2 = new() { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        row2.Controls.AddRange(
        [
            WrapLabel("Proveedor:", _cmbProviderEntry),
            WrapLabel("Solicitado por:", _cmbRequesterEntry),
            WrapLabel("Estado:", _cmbEstadoEntry)
        ]);

        grpHeader.Controls.Add(row2);
        grpHeader.Controls.Add(row1);

        GroupBox grpDetail = CambiasoTheme.CreateGroupBox("Detalle de Piezas en Reparación y Trabajo");
        grpDetail.Dock = DockStyle.Fill;
        TableLayoutPanel detTbl = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        detTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        detTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));

        Panel cantPanel = new() { Dock = DockStyle.Fill };
        Label lblCant = CambiasoTheme.CreateFieldLabel("Cantidad");
        lblCant.Location = new Point(4, 4);
        _numCantidad.Location = new Point(4, 26);
        cantPanel.Controls.AddRange([lblCant, _numCantidad]);

        Panel descPanel = new() { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 8, 2) };
        Label lblDesc = CambiasoTheme.CreateFieldLabel("Descripción del trabajo");
        lblDesc.Dock = DockStyle.Top;
        descPanel.Controls.Add(_txtDetalle);
        descPanel.Controls.Add(lblDesc);

        FlowLayoutPanel detBtns = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(4, 0, 0, 0) };
        _btnAddDetail.Margin = new Padding(0, 2, 0, 2);
        _btnRemoveDetail.Margin = new Padding(0, 2, 0, 2);
        detBtns.Controls.Add(_btnAddDetail);
        detBtns.Controls.Add(_btnRemoveDetail);

        detTbl.Controls.Add(cantPanel, 0, 0);
        detTbl.Controls.Add(descPanel, 1, 0);
        detTbl.Controls.Add(detBtns, 2, 0);
        grpDetail.Controls.Add(detTbl);

        // Fila 2: Observación (inmediatamente después de Detalle) + Valor Neto destacado + Selector de Firmas desde Personal/Usuarios
        TableLayoutPanel obsAndMoneySplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        obsAndMoneySplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        obsAndMoneySplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));

        GroupBox grpObs = CambiasoTheme.CreateGroupBox("Observación de la OT");
        grpObs.Dock = DockStyle.Fill;
        Panel obsPad = new() { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 4, 4) };
        obsPad.Controls.Add(TxtObservacionEntry);
        grpObs.Controls.Add(obsPad);

        GroupBox grpNetAndSig = CambiasoTheme.CreateGroupBox("Valor Neto ($) y Firmas al Pie (Personal / Usuarios)");
        grpNetAndSig.Dock = DockStyle.Fill;
        TableLayoutPanel netSigTbl = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(4, 2, 4, 2) };
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel moneyToggleRow = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0) };
        moneyToggleRow.Controls.Add(ChkMostrarValorNetoEntry);

        FlowLayoutPanel moneyRow = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 1, 0, 0) };
        moneyRow.Controls.AddRange([_lblNetTitle, TxtValorNetoEntry]);

        TableLayoutPanel sigPickTbl = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Margin = Padding.Empty };
        sigPickTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        sigPickTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sigPickTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sigPickTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sigPickTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        sigPickTbl.Controls.Add(ChkEntrySigJefatura, 0, 0);
        sigPickTbl.Controls.Add(CmbEntrySigJefatura, 1, 0);
        sigPickTbl.Controls.Add(ChkEntrySigTecnico, 0, 1);
        sigPickTbl.Controls.Add(CmbEntrySigTecnico, 1, 1);
        sigPickTbl.Controls.Add(ChkEntrySigSupervisor, 0, 2);
        sigPickTbl.Controls.Add(CmbEntrySigSupervisor, 1, 2);

        netSigTbl.Controls.Add(moneyToggleRow, 0, 0);
        netSigTbl.Controls.Add(moneyRow, 0, 1);
        netSigTbl.Controls.Add(sigPickTbl, 0, 2);
        netSigTbl.Controls.Add(_lblEntrySigHint, 0, 3);
        grpNetAndSig.Controls.Add(netSigTbl);

        obsAndMoneySplit.Controls.Add(grpObs, 0, 0);
        obsAndMoneySplit.Controls.Add(grpNetAndSig, 1, 0);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Ítems de la OT (doble clic para editar · clic derecho para vista previa)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridEntryDetails);

        TableLayoutPanel footerHost = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        footerHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        footerHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));

        FlowLayoutPanel footerButtons = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 2, 2, 0), WrapContents = false };
        footerButtons.Controls.AddRange([_btnSaveOt, _btnSaveAndExportOt, _btnEntryPreview, _btnUpdateOt, _btnDeleteOt, _btnClearEntry]);

        footerHost.Controls.Add(footerButtons, 0, 0);
        footerHost.Controls.Add(_lblEntryStatus, 0, 1);

        layout.Controls.Add(grpHeader, 0, 0);
        layout.Controls.Add(grpDetail, 0, 1);
        layout.Controls.Add(obsAndMoneySplit, 0, 2);
        layout.Controls.Add(grpGrid, 0, 3);
        layout.Controls.Add(footerHost, 0, 4);
        _tabEntry.Controls.Add(layout);
    }

    private void BuildSearchTabLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 146));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        GroupBox grpFilters = CambiasoTheme.CreateGroupBox("Criterios de Búsqueda Combinables (orden más reciente primero)");
        grpFilters.Dock = DockStyle.Fill;

        FlowLayoutPanel f1 = new() { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        f1.Controls.Add(_searchDateRange);

        FlowLayoutPanel f2 = new() { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        f2.Controls.AddRange([_chkFilterProvider, _cmbProviderSearch, _chkFilterRequester, _cmbRequesterSearch]);

        FlowLayoutPanel f3 = new() { Dock = DockStyle.Top, Height = 40, WrapContents = false, Padding = new Padding(4, 3, 4, 0) };
        f3.Controls.AddRange([_chkFilterText, _txtSearchKeyword, _chkFilterStatus, _cmbStatusSearch, _chkFilterTree, _btnExecuteSearch]);

        grpFilters.Controls.Add(f3);
        grpFilters.Controls.Add(f2);
        grpFilters.Controls.Add(f1);

        GroupBox grpResults = CambiasoTheme.CreateGroupBox("Gestión y Resultados de OT (marque columnas a exportar en el encabezado · doble clic editar · clic derecho estado)");
        grpResults.Dock = DockStyle.Fill;

        TableLayoutPanel resultsTbl = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        FlowLayoutPanel crudBar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 38),
            BackColor = CambiasoTheme.CardBackground,
            WrapContents = true,
            Padding = new Padding(6, 4, 6, 4)
        };
        crudBar.Controls.AddRange([BtnSearchNewOt, BtnSearchConsultOt, BtnSearchEditOt, BtnSearchDeleteOt, BtnSearchExportColumns]);

        FlowLayoutPanel selBar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 38),
            BackColor = CambiasoTheme.SubtleFill,
            WrapContents = true,
            Padding = new Padding(6, 4, 6, 4)
        };
        _toolTip.SetToolTip(ChkSelectAllSearchVisible, "Selecciona todos los registros de la página visible; la selección se conserva al ordenar o cambiar de página.");
        _toolTip.SetToolTip(LblSearchSelectedCount, "Cantidad total de órdenes de trabajo marcadas (la selección se conserva al ordenar o cambiar de página).");
        selBar.Controls.AddRange([ChkSelectAllSearchVisible, LblSearchSelectedCount, BtnExportSelectedSearch, BtnClearSearchSelection]);

        resultsTbl.Controls.Add(crudBar, 0, 0);
        resultsTbl.Controls.Add(selBar, 0, 1);
        resultsTbl.Controls.Add(_gridSearchResults, 0, 2);
        resultsTbl.Controls.Add(_searchPager, 0, 3);
        grpResults.Controls.Add(resultsTbl);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnExportSearch, _btnClearSearch, _exportProgress, _lblSearchSummary]);

        layout.Controls.Add(grpFilters, 0, 0);
        layout.Controls.Add(grpResults, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        _tabSearch.Controls.Add(layout);
    }

    private void LoadCatalogs()
    {
        IReadOnlyList<Provider> providers = _ctx.MaintainerService.GetProviders();
        IReadOnlyList<Requester> requesters = _ctx.MaintainerService.GetRequesters();
        _signatureCatalog = _ctx.WorkOrderService.GetSignaturePersonnelCatalog();

        BindCombo(_cmbProviderEntry, providers, nameof(Provider.Nombre), nameof(Provider.IdProv));
        BindCombo(_cmbProviderSearch, providers, nameof(Provider.Nombre), nameof(Provider.IdProv));

        BindCombo(_cmbRequesterEntry, requesters, nameof(Requester.Alias), nameof(Requester.IdSol));
        BindCombo(_cmbRequesterSearch, requesters, nameof(Requester.Alias), nameof(Requester.IdSol));

        BindSignerCombo(CmbEntrySigJefatura, _signatureCatalog);
        BindSignerCombo(CmbEntrySigTecnico, _signatureCatalog);
        BindSignerCombo(CmbEntrySigSupervisor, _signatureCatalog);
    }

    private static void BindCombo<T>(ComboBox combo, IReadOnlyList<T> items, string display, string value)
    {
        object? previousValue = combo.SelectedValue;
        combo.DataSource = null;
        combo.DisplayMember = display;
        combo.ValueMember = value;
        combo.DataSource = items.ToList();

        if (previousValue is not null)
        {
            combo.SelectedValue = previousValue;
            if (combo.SelectedIndex >= 0)
            {
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = 0;
        }
    }

    private void LookupEntryOrder()
    {
        if (!int.TryParse(_txtNumeroOt.Text.Trim(), out int numOt) || numOt <= 0)
        {
            CambiasoTheme.ShowWarning(this, "Ingrese un número de OT válido para buscar.");
            return;
        }

        WorkOrder? order = _ctx.WorkOrderService.GetByNumber(numOt);
        if (order is null)
        {
            CambiasoTheme.ShowInfo(this, $"La OT N° {numOt} no existe aún. Puede ingresarla como nueva.");
            _loadedOrder = null;
            _btnSaveOt.Enabled = _session.Permisos.IngresoOt;
            _btnSaveAndExportOt.Enabled = _session.Permisos.IngresoOt;
            _btnUpdateOt.Enabled = false;
            _btnDeleteOt.Enabled = false;
            return;
        }

        _loadedOrder = order;
        _suppressEntrySignerSync = true;
        try
        {
            SelectOrBindHistoricalProvider(_cmbProviderEntry, order.IdProv);
            if (order.IdSol.HasValue)
            {
                SelectOrBindHistoricalRequester(_cmbRequesterEntry, order.IdSol.Value);
            }

            _cmbTipoOrdenEntry.SelectedItem = string.Equals(order.TipoOrden, "Interna", StringComparison.OrdinalIgnoreCase)
                ? "Interna"
                : "Externa";
            SelectEntryStatus(order.Estado);
            _dtFechaOt.Value = order.FechaOt;
            TxtObservacionEntry.Text = order.Observacion;
            _cachedValorNeto = Math.Max(0m, order.ValorNeto);
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
            TxtValorNetoEntry.Text = $"$ {_cachedValorNeto.ToString("N0", ClCulture)}";
            ChkMostrarValorNetoEntry.Checked = order.MostrarValorNeto;
            ApplyNetValueVisibilityImmediate();
            ApplyEntrySelectedSignature(order.FirmasSeleccionadas);
            SelectOrderSignersInEntryCombos(order);
        }
        finally
        {
            _suppressEntrySignerSync = false;
        }

        _entryDetails.Clear();
        _entryDetails.AddRange(order.Detalles);
        RefreshEntryDetailsGrid();

        _assetTree.SelectNodeByPath(order.GetLevelArray());

        _btnSaveOt.Enabled = false;
        _btnSaveAndExportOt.Enabled = _session.Permisos.IngresoOt;
        _btnUpdateOt.Enabled = _session.Permisos.IngresoOt;
        _btnDeleteOt.Enabled = _session.Permisos.IngresoOt;
        UpdateEntrySignatureHint();
        string netLabel = order.MostrarValorNeto ? $"$ {order.ValorNeto.ToString("N0", ClCulture)}" : "Oculto en OT externa";
        _lblEntryStatus.Text = $"OT {order.TipoOrden} N° {order.NumeroOt} cargada · Estado: {order.Estado.ToDisplayName()} · Neto: {netLabel} · Firmas: {order.FirmasSeleccionadas.FormatResumen()}";
    }

    private void AddEntryDetail()
    {
        if (string.IsNullOrWhiteSpace(_txtDetalle.Text))
        {
            CambiasoTheme.ShowWarning(this, "Debe ingresar la descripción del trabajo antes de agregarlo.");
            _txtDetalle.Focus();
            return;
        }

        _entryDetails.Add(new WorkOrderDetail
        {
            Cantidad = (int)_numCantidad.Value,
            Descripcion = _txtDetalle.Text.Trim()
        });

        _numCantidad.Value = 1;
        _txtDetalle.Clear();
        _txtDetalle.Focus();
        RefreshEntryDetailsGrid();
    }

    private void RemoveSelectedEntryDetail()
    {
        if (_gridEntryDetails.CurrentRow is null)
        {
            return;
        }

        int idx = _gridEntryDetails.CurrentRow.Index;
        if (idx >= 0 && idx < _entryDetails.Count)
        {
            _entryDetails.RemoveAt(idx);
            RefreshEntryDetailsGrid();
        }
    }

    private void EditSelectedEntryDetail()
    {
        if (_gridEntryDetails.CurrentRow is null)
        {
            return;
        }

        int idx = _gridEntryDetails.CurrentRow.Index;
        if (idx >= 0 && idx < _entryDetails.Count)
        {
            WorkOrderDetail item = _entryDetails[idx];
            _numCantidad.Value = Math.Clamp(item.Cantidad, 1, 99999);
            _txtDetalle.Text = item.Descripcion;
            _entryDetails.RemoveAt(idx);
            RefreshEntryDetailsGrid();
        }
    }

    private void RefreshEntryDetailsGrid()
    {
        _gridEntryDetails.DataSource = null;
        _gridEntryDetails.DataSource = _entryDetails.Select((d, i) => new
        {
            Ítem = i + 1,
            d.Cantidad,
            Descripción = d.Descripcion
        }).ToList();

        if (_gridEntryDetails.Columns.Count >= 3)
        {
            _gridEntryDetails.Columns[0].FillWeight = 8;
            _gridEntryDetails.Columns[1].HeaderText = "Cant.";
            _gridEntryDetails.Columns[1].FillWeight = 12;
            _gridEntryDetails.Columns[2].FillWeight = 80;
        }
    }

    private void SaveNewOrder()
    {
        SaveNewOrderCore();
    }

    private void SaveAndExportOrder()
    {
        try
        {
            if (_loadedOrder is not null)
            {
                WorkOrder updated = BuildOrderFromEntryForm();
                updated.IdOt = _loadedOrder.IdOt;
                _ctx.WorkOrderService.Update(_session, updated);
                WorkOrder? fresh = _ctx.WorkOrderService.GetByNumber(updated.NumeroOt) ?? updated;
                _loadedOrder = fresh;
                _lblEntryStatus.Text = $"OT N° {fresh.NumeroOt} actualizada (Estado: {fresh.Estado.ToDisplayName()}) — lista para exportar.";
                if (_tabs.TabPages.Contains(_tabSearch))
                {
                    ExecuteSearchCore(showEmptyMessage: false);
                }

                ExportUiHelper.ShowWorkOrderExportMenu(_btnSaveAndExportOt, this, _ctx.DocumentExportService, fresh, fresh.FirmasSeleccionadas);
                return;
            }

            int? savedNum = SaveNewOrderCore();
            if (savedNum.HasValue)
            {
                WorkOrder? savedOrder = _ctx.WorkOrderService.GetByNumber(savedNum.Value);
                if (savedOrder is not null)
                {
                    ExportUiHelper.ShowWorkOrderExportMenu(_btnSaveAndExportOt, this, _ctx.DocumentExportService, savedOrder, savedOrder.FirmasSeleccionadas);
                }
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ShowEntryPreview()
    {
        try
        {
            if (_entryDetails.Count == 0 && _loadedOrder is null)
            {
                CambiasoTheme.ShowWarning(this, "Agregue al menos un ítem de trabajo o busque una OT antes de abrir la vista previa.");
                return;
            }

            WorkOrder previewOrder = BuildPreviewOrderFromEntryForm();
            ExportUiHelper.ShowWorkOrderPreview(this, _ctx, previewOrder, GetEntrySelectedSignature());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private int? SaveNewOrderCore()
    {
        try
        {
            WorkOrder order = BuildOrderFromEntryForm();
            int id = _ctx.WorkOrderService.Create(_session, order);
            CambiasoTheme.ShowInfo(this, $"Orden de Trabajo {order.TipoOrden} N° {order.NumeroOt} guardada correctamente (ID #{id}).");
            ClearEntryForm();
            if (_tabs.TabPages.Contains(_tabSearch))
            {
                ExecuteSearchCore(showEmptyMessage: false);
            }

            return order.NumeroOt;
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
            return null;
        }
    }

    private void UpdateLoadedOrder()
    {
        if (_loadedOrder is null)
        {
            return;
        }

        try
        {
            WorkOrder updated = BuildOrderFromEntryForm();
            updated.IdOt = _loadedOrder.IdOt;
            _ctx.WorkOrderService.Update(_session, updated);
            CambiasoTheme.ShowInfo(this, $"Orden de Trabajo N° {updated.NumeroOt} actualizada correctamente (Estado: {updated.Estado.ToDisplayName()}).");
            LookupEntryOrder();
            if (_tabs.TabPages.Contains(_tabSearch))
            {
                ExecuteSearchCore(showEmptyMessage: false);
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void DeleteLoadedOrder()
    {
        if (_loadedOrder is null)
        {
            return;
        }

        if (!CambiasoTheme.Confirm(
            this,
            $"¿Confirma que desea eliminar la OT {_loadedOrder.TipoOrden} N° {_loadedOrder.NumeroOt}?\n\n" +
            $"• Proveedor: {_loadedOrder.NombreProveedor}\n" +
            $"• Fecha: {_loadedOrder.FechaOt:dd-MM-yyyy}\n" +
            $"• Estado: {_loadedOrder.Estado.ToDisplayName()}\n\n" +
            "Esta acción quedará registrada en la auditoría y no se puede deshacer.",
            "Confirmar Eliminación de OT"))
        {
            return;
        }

        try
        {
            _ctx.WorkOrderService.Delete(_session, _loadedOrder.IdOt);
            CambiasoTheme.ShowInfo(this, $"OT N° {_loadedOrder.NumeroOt} eliminada correctamente.");
            ClearEntryForm();
            if (_tabs.TabPages.Contains(_tabSearch))
            {
                ExecuteSearchCore(showEmptyMessage: false);
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void ApplyNetValueVisibilityImmediate()
    {
        bool show = ChkMostrarValorNetoEntry.Checked;
        if (!show)
        {
            if (WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out decimal parsed) && parsed >= 0m)
            {
                _cachedValorNeto = parsed;
            }
            else if (_loadedOrder is not null)
            {
                _cachedValorNeto = Math.Max(0m, _loadedOrder.ValorNeto);
            }
        }
        else
        {
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
            TxtValorNetoEntry.Text = $"$ {_cachedValorNeto.ToString("N0", ClCulture)}";
        }

        _lblNetTitle.Visible = show;
        TxtValorNetoEntry.Visible = show;
        TxtValorNetoEntry.Enabled = show;
    }

    private WorkOrder BuildOrderFromEntryForm()
    {
        if (!int.TryParse(_txtNumeroOt.Text.Trim(), out int numOt) || numOt <= 0)
        {
            throw new ArgumentException("Debe ingresar un número de OT válido.");
        }

        bool mostrarNeto = ChkMostrarValorNetoEntry.Checked;
        decimal valorNeto;
        if (mostrarNeto)
        {
            if (!WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out valorNeto) || valorNeto < 0m)
            {
                TxtValorNetoEntry.BackColor = Color.FromArgb(255, 232, 232);
                TxtValorNetoEntry.Focus();
                throw new ArgumentException("Debe ingresar un Valor Neto numérico válido mayor o igual a cero.");
            }

            _cachedValorNeto = valorNeto;
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
        }
        else
        {
            // Ocultar valor neto no exige ingreso ni elimina/borra el dato económico guardado en BD
            valorNeto = _cachedValorNeto > 0m ? _cachedValorNeto : (_loadedOrder?.ValorNeto ?? 0m);
        }

        int idProv = _cmbProviderEntry.SelectedValue is int p ? p : 0;
        int? idSol = _cmbRequesterEntry.SelectedValue is int s ? s : null;
        IReadOnlyList<int> pathIds = _assetTree.GetSelectedPathIds();
        string tipoOrden = _cmbTipoOrdenEntry.SelectedItem?.ToString() ?? "Externa";

        WorkOrder order = new()
        {
            NumeroOt = numOt,
            TipoOrden = tipoOrden,
            Estado = GetSelectedEntryStatus(),
            IdProv = idProv,
            IdSol = idSol,
            FechaOt = _dtFechaOt.Value.Date,
            Observacion = TxtObservacionEntry.Text.Trim(),
            ValorNeto = Math.Max(0m, valorNeto),
            MostrarValorNeto = mostrarNeto,
            FirmasSeleccionadas = GetEntrySelectedSignature(),
            FirmaJefaturaNombre = GetSignerName(CmbEntrySigJefatura),
            FirmaTecnicoNombre = GetSignerName(CmbEntrySigTecnico),
            FirmaSupervisorNombre = GetSignerName(CmbEntrySigSupervisor),
            RutaActivo = _assetTree.CurrentBreadcrumb,
            Detalles = _entryDetails.Select(d => new WorkOrderDetail
            {
                IdDet = d.IdDet,
                Cantidad = d.Cantidad,
                Descripcion = d.Descripcion
            }).ToList()
        };
        order.SetLevels(pathIds);
        return order;
    }

    private WorkOrder BuildPreviewOrderFromEntryForm()
    {
        int numOt = int.TryParse(_txtNumeroOt.Text.Trim(), out int parsedOt) && parsedOt > 0
            ? parsedOt
            : (_loadedOrder?.NumeroOt ?? _ctx.WorkOrderService.GetNextWorkOrderNumber());

        bool mostrarNeto = ChkMostrarValorNetoEntry.Checked;
        decimal valorNeto = mostrarNeto && WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out decimal parsedNeto)
            ? Math.Max(0m, parsedNeto)
            : (_cachedValorNeto > 0m ? _cachedValorNeto : (_loadedOrder?.ValorNeto ?? 0m));
        Provider? prov = _cmbProviderEntry.SelectedItem as Provider;
        Requester? sol = _cmbRequesterEntry.SelectedItem as Requester;

        return new WorkOrder
        {
            IdOt = _loadedOrder?.IdOt ?? 0,
            NumeroOt = numOt,
            TipoOrden = _cmbTipoOrdenEntry.SelectedItem?.ToString() ?? _loadedOrder?.TipoOrden ?? "Externa",
            IdProv = prov?.IdProv ?? _loadedOrder?.IdProv ?? 0,
            NombreProveedor = prov?.Nombre ?? _loadedOrder?.NombreProveedor ?? string.Empty,
            DireccionProveedor = prov?.Direccion ?? _loadedOrder?.DireccionProveedor ?? string.Empty,
            CorreoProveedor = prov?.Correo ?? _loadedOrder?.CorreoProveedor ?? string.Empty,
            IdSol = sol?.IdSol ?? _loadedOrder?.IdSol,
            AliasSolicitante = sol?.Alias ?? _loadedOrder?.AliasSolicitante ?? string.Empty,
            NombreSolicitante = sol is not null
                ? (string.IsNullOrWhiteSpace(sol.NombreCompleto) ? sol.Alias : sol.NombreCompleto)
                : (_loadedOrder?.NombreSolicitante ?? string.Empty),
            CorreoSolicitante = sol?.Correo ?? _loadedOrder?.CorreoSolicitante ?? string.Empty,
            TelefonoSolicitante = sol?.Telefono ?? _loadedOrder?.TelefonoSolicitante ?? string.Empty,
            FirmaSolicitantePreviewPng = sol?.FirmaPreviewPng ?? _loadedOrder?.FirmaSolicitantePreviewPng,
            FirmaSolicitanteTipo = !string.IsNullOrWhiteSpace(sol?.FirmaTipo) ? sol.FirmaTipo : (_loadedOrder?.FirmaSolicitanteTipo ?? string.Empty),
            FirmaSolicitanteNombre = !string.IsNullOrWhiteSpace(sol?.FirmaNombre) ? sol.FirmaNombre : (_loadedOrder?.FirmaSolicitanteNombre ?? string.Empty),
            FechaOt = _dtFechaOt.Value.Date,
            Estado = GetSelectedEntryStatus(),
            Observacion = TxtObservacionEntry.Text.Trim(),
            ValorNeto = Math.Max(0m, valorNeto),
            MostrarValorNeto = mostrarNeto,
            FirmasSeleccionadas = GetEntrySelectedSignature(),
            FirmaJefaturaNombre = GetSignerName(CmbEntrySigJefatura),
            FirmaTecnicoNombre = GetSignerName(CmbEntrySigTecnico),
            FirmaSupervisorNombre = GetSignerName(CmbEntrySigSupervisor),
            RutaActivo = !string.IsNullOrWhiteSpace(_assetTree.CurrentBreadcrumb)
                ? _assetTree.CurrentBreadcrumb
                : (_loadedOrder?.RutaActivo ?? string.Empty),
            Detalles = _entryDetails.Count > 0
                ? _entryDetails.Select(d => new WorkOrderDetail { IdDet = d.IdDet, Cantidad = d.Cantidad, Descripcion = d.Descripcion }).ToList()
                : (_loadedOrder?.Detalles ?? [])
        };
    }

    private SignatureOption GetEntrySelectedSignature() =>
        SignatureOptionExtensions.FromFlags(ChkEntrySigJefatura.Checked, ChkEntrySigSupervisor.Checked, ChkEntrySigTecnico.Checked);

    private void ApplyEntrySelectedSignature(SignatureOption option)
    {
        ChkEntrySigJefatura.Checked = option.IncludeJefatura();
        ChkEntrySigSupervisor.Checked = option.IncludeSupervisor();
        ChkEntrySigTecnico.Checked = option.IncludeTecnico();
        UpdateEntrySignatureHint();
    }

    private void UpdateEntrySignatureHint()
    {
        if (_lblEntrySigHint is null)
        {
            return;
        }

        SignatureOption sel = GetEntrySelectedSignature();
        string sigText = sel.FormatResumen();
        string? tecSel = ChkEntrySigTecnico.Checked ? GetSignerName(CmbEntrySigTecnico) : null;
        string tecInfo = !string.IsNullOrWhiteSpace(tecSel) ? $" · Técnico: {tecSel}" : string.Empty;
        _lblEntrySigHint.Text = $"Pie OT: {sigText}{tecInfo}";
        _lblEntrySigHint.ForeColor = sel == SignatureOption.Ninguna ? CambiasoTheme.TextMuted : CambiasoTheme.Primary;
    }

    private void FormatAndValidateNetValueInput()
    {
        if (WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out decimal valor) && valor >= 0m)
        {
            _cachedValorNeto = valor;
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
            TxtValorNetoEntry.Text = $"$ {valor.ToString("N0", ClCulture)}";
        }
        else
        {
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 232, 232);
            _lblEntryStatus.Text = "Advertencia: Valor Neto inválido. Ingrese un monto numérico mayor o igual a cero.";
        }
    }

    private void SelectOrBindHistoricalProvider(ComboBox combo, int idProv)
    {
        combo.SelectedValue = idProv;
        if (combo.SelectedIndex >= 0)
        {
            return;
        }

        IReadOnlyList<Provider> all = _ctx.MaintainerService.GetProviders(includeInactive: true);
        Provider? match = all.FirstOrDefault(p => p.IdProv == idProv);
        if (match is null)
        {
            return;
        }

        List<Provider> list = (combo.DataSource as IEnumerable<Provider>)?.ToList() ?? [];
        list.Add(match);
        combo.DataSource = list;
        combo.SelectedValue = idProv;
        if (ReferenceEquals(combo, _cmbProviderEntry))
        {
            _entryCatalogHasHistorical = true;
        }
    }

    private void SelectOrBindHistoricalRequester(ComboBox combo, int idSol)
    {
        combo.SelectedValue = idSol;
        if (combo.SelectedIndex >= 0)
        {
            return;
        }

        IReadOnlyList<Requester> all = _ctx.MaintainerService.GetRequesters(includeInactive: true);
        Requester? match = all.FirstOrDefault(r => r.IdSol == idSol);
        if (match is null)
        {
            return;
        }

        List<Requester> list = (combo.DataSource as IEnumerable<Requester>)?.ToList() ?? [];
        list.Add(match);
        combo.DataSource = list;
        combo.SelectedValue = idSol;
        if (ReferenceEquals(combo, _cmbRequesterEntry))
        {
            _entryCatalogHasHistorical = true;
        }
    }

    private void ClearEntryForm()
    {
        _loadedOrder = null;
        _entryDetails.Clear();
        _suppressEntrySignerSync = true;
        try
        {
            if (_entryCatalogHasHistorical)
            {
                LoadCatalogs();
                _entryCatalogHasHistorical = false;
            }
            _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString(CultureInfo.InvariantCulture);
            _cmbTipoOrdenEntry.SelectedIndex = 0;
            SelectEntryStatus(WorkOrderStatus.Abierta);
            RefreshNextOtBadge();
            _dtFechaOt.Value = DateTime.Today;
            _numCantidad.Value = 1;
            _txtDetalle.Clear();
            TxtObservacionEntry.Clear();
            _cachedValorNeto = 0m;
            ChkMostrarValorNetoEntry.Checked = true;
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
            TxtValorNetoEntry.Text = "$ 0";
            ApplyNetValueVisibilityImmediate();
            ApplyEntrySelectedSignature(SignatureOption.AmbasConSolicitante);
            ApplyDefaultSignersForDraft();
        }
        finally
        {
            _suppressEntrySignerSync = false;
        }

        _btnSaveOt.Enabled = _session.Permisos.IngresoOt;
        _btnSaveAndExportOt.Enabled = _session.Permisos.IngresoOt;
        _btnUpdateOt.Enabled = false;
        _btnDeleteOt.Enabled = false;
        UpdateEntrySignatureHint();
        _lblEntryStatus.Text = $"Modo ingreso de nueva OT · Activo actual: {_assetTree.SelectedAsset?.Descripcion ?? "CAMBIASO HNOS S.A.C."}";
        RefreshEntryDetailsGrid();
    }

    internal void ExecuteSearchForTest() => ExecuteSearch();

    private void ExecuteSearch() => ExecuteSearchCore(showEmptyMessage: true);

    private void ExecuteSearchCore(bool showEmptyMessage)
    {
        if (!_searchDateRange.TryValidate(this, out _))
        {
            return;
        }

        try
        {
            WorkOrderSearchFilter filter = new()
            {
                FechaInicio = _searchDateRange.FechaInicio,
                FechaFin = _searchDateRange.FechaFin,
                IdProv = _chkFilterProvider.Checked && _cmbProviderSearch.SelectedValue is int pid ? pid : null,
                IdSol = _chkFilterRequester.Checked && _cmbRequesterSearch.SelectedValue is int sid ? sid : null,
                Estado = _chkFilterStatus.Checked && _cmbStatusSearch.SelectedItem is WorkOrderStatusOption stOpt ? stOpt.Value : null,
                TextoBusqueda = _chkFilterText.Checked ? _txtSearchKeyword.Text.Trim() : null
            };

            if (_chkFilterTree.Checked)
            {
                AssetNode? node = _assetTree.SelectedAsset;
                int realDepth = node is null ? 0 : _ctx.MaintainerService.GetAssetPath(node.IdActivo).Count;
                if (node is null || realDepth < 2)
                {
                    if (showEmptyMessage)
                    {
                        CambiasoTheme.ShowWarning(this, "Seleccione al menos una planta (nivel 2 o superior) en el árbol izquierdo.");
                    }
                    return;
                }

                filter.AssetLevel = realDepth;
                filter.AssetNodeId = node.IdActivo;
            }

            Cursor = Cursors.WaitCursor;
            _lastSearchResults = _ctx.WorkOrderService.Search(_session, filter);
            _searchPager.ResetPageIndex();
            RenderSearchResultsPage();

            int uniqueOts = _lastSearchResults.Select(r => r.IdOt).Distinct().Count();
            _lblSearchSummary.Text = $"{_lastSearchResults.Count:N0} fila(s) / {uniqueOts:N0} OT única(s) (más recientes primero).";
            if (showEmptyMessage && _lastSearchResults.Count == 0)
            {
                CambiasoTheme.ShowInfo(this, "No se encontraron órdenes de trabajo con los criterios indicados.");
            }
        }
        catch (Exception ex)
        {
            if (showEmptyMessage)
            {
                CambiasoTheme.ShowWarning(this, ex.Message);
            }
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void RenderSearchResultsPage()
    {
        IReadOnlyList<WorkOrderSearchRow> slice = GridBindingHelper.SlicePage(_lastSearchResults, _searchPager.PageIndex, _searchPager.PageSize, out int safePage);
        _gridSearchResults.DataSource = new SortableBindingList<WorkOrderGridRow>(
            slice.Select(r => new WorkOrderGridRow(r)));
        _searchPager.SetState(safePage, _searchPager.PageSize, _lastSearchResults.Count);

        if (_gridSearchResults.Columns.Count >= 11)
        {
            _searchSelection.ConfigureDataColumn(0, "N° OT", 8.5f);
            _searchSelection.ConfigureDataColumn(1, "Fecha", 9.5f, "dd-MM-yyyy");
            _searchSelection.ConfigureDataColumn(2, "Proveedor", 13.5f);
            _searchSelection.ConfigureDataColumn(3, "Solicitante", 11.5f);
            _searchSelection.ConfigureDataColumn(4, "Cant.", 7.5f);
            _searchSelection.ConfigureDataColumn(5, "Detalle", 16.0f);
            _searchSelection.ConfigureDataColumn(6, "Obs.", 7.5f);
            _searchSelection.ConfigureDataColumn(7, "Valor Neto", 11.5f, "$#,##0", DataGridViewContentAlignment.MiddleRight);
            _searchSelection.ConfigureDataColumn(8, "Estado", 10.0f);
            _searchSelection.ConfigureDataColumn(9, "Firmas", 9.5f);
        }

        _searchSelection.EnsureSelectColumnAndSync();
        _searchExportColumns.ApplyToGrid();
        UpdateSearchSelectionState();
    }

    private void UpdateSearchSelectionState()
    {
        int count = _searchSelection.SelectedCount;
        bool hasCurrentRow = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults) is not null;

        LblSearchSelectedCount.Text = $"OT seleccionadas: {count:N0}";
        BtnExportSelectedSearch.Enabled = count > 0;
        BtnClearSearchSelection.Enabled = count > 0;

        BtnSearchNewOt.Enabled = _session.Permisos.IngresoOt;
        BtnSearchConsultOt.Enabled = hasCurrentRow && _session.Permisos.MenuOrdenesTrabajo;
        BtnSearchEditOt.Enabled = hasCurrentRow && _session.Permisos.IngresoOt;
        BtnSearchDeleteOt.Enabled = (count > 0 || hasCurrentRow) && _session.Permisos.IngresoOt;
    }

    private void ConsultSelectedSearchOrder()
    {
        WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults);
        if (row is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione una orden de trabajo de la lista para consultar.");
            return;
        }

        ConsultOrderByNumber(row.NumeroOt);
    }

    public void ConsultOrderByNumber(int numeroOt)
    {
        WorkOrder? order = _ctx.WorkOrderService.GetByNumber(numeroOt);
        if (order is null)
        {
            CambiasoTheme.ShowWarning(this, $"No se encontró la OT N° {numeroOt}.");
            return;
        }

        using WorkOrderDetailDialog dlg = new(_ctx, order);
        dlg.ShowDialog(FindForm());
    }

    private void DeleteSelectedSearchOrders()
    {
        if (!_session.Permisos.IngresoOt)
        {
            CambiasoTheme.ShowWarning(this, "No posee permisos para eliminar órdenes de trabajo.");
            return;
        }

        IReadOnlyList<int> checkedIds = _searchSelection.SelectedIds;
        if (checkedIds.Count > 1)
        {
            IReadOnlyList<WorkOrder> orders = _ctx.WorkOrderService.GetByIds(_session, checkedIds);
            if (orders.Count == 0)
            {
                return;
            }

            string previewNums = string.Join(", ", orders.Take(8).Select(o => $"#{o.NumeroOt}"));
            if (orders.Count > 8) previewNums += $" (+{orders.Count - 8} más)";

            if (!CambiasoTheme.Confirm(
                this,
                $"¿Confirma que desea eliminar las {orders.Count} órdenes de trabajo seleccionadas?\n\n" +
                $"Órdenes: {previewNums}\n\n" +
                "Cada eliminación quedará registrada en la auditoría del sistema y no se puede deshacer.",
                "Confirmar Eliminación de OTs"))
            {
                return;
            }

            try
            {
                int deleted = 0;
                foreach (WorkOrder ot in orders)
                {
                    _ctx.WorkOrderService.Delete(_session, ot.IdOt);
                    _searchSelection.SetSelected(ot.IdOt, false);
                    deleted++;
                }

                CambiasoTheme.ShowInfo(this, $"{deleted} orden(es) de trabajo eliminada(s) correctamente.");
                ExecuteSearchCore(showEmptyMessage: false);
            }
            catch (Exception ex)
            {
                CambiasoTheme.ShowError(this, ex.Message);
            }

            return;
        }

        WorkOrderSearchRow? singleRow = checkedIds.Count == 1
            ? _lastSearchResults.FirstOrDefault(r => r.IdOt == checkedIds[0])
            : GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults);

        if (singleRow is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione una orden de trabajo para eliminar.");
            return;
        }

        DeleteSearchOrder(singleRow.NumeroOt);
    }

    private void ExportSearchResultsList()
    {
        if (_searchExportColumns.SelectedFields.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Debe marcar al menos una columna en el encabezado de la tabla para exportar.");
            return;
        }

        ExportUiHelper.ShowReportExportMenu(
            _btnExportSearch,
            this,
            _ctx.DocumentExportService,
            "SISPRO_OrdenesTrabajo",
            () => DocumentExportService.BuildWorkOrdersReport(_lastSearchResults, BuildSearchFilterSummary(), _searchExportColumns.SelectedFields),
            file => _ctx.Audit.Log(
                _session.Usuario.AliasUsuario,
                "Órdenes de Trabajo",
                "EXPORTAR_OT_LISTA",
                $"Exportados {_lastSearchResults.Count} registros ({_searchExportColumns.SelectedFields.Count} campos) a {Path.GetFileName(file)}."));
    }

    private void ExportSelectedWorkOrders()
    {
        IReadOnlyList<int> selectedIds = _searchSelection.SelectedIds;
        if (selectedIds.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Debe seleccionar al menos una orden de trabajo mediante su casilla de verificación.");
            return;
        }

        if (_searchExportColumns.SelectedFields.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Debe marcar al menos una columna en el encabezado de la tabla para exportar.");
            return;
        }

        ExportUiHelper.ShowSelectedWorkOrdersExportMenu(
            BtnExportSelectedSearch,
            this,
            _ctx.DocumentExportService,
            () => _ctx.WorkOrderService.GetByIds(_session, _searchSelection.SelectedIds),
            (file, count) => _ctx.Audit.Log(
                _session.Usuario.AliasUsuario,
                "Órdenes de Trabajo",
                "EXPORTAR_OT_SELECCIONADAS",
                $"Exportadas {count} OT(s) seleccionadas ({_searchExportColumns.SelectedFields.Count} campos) a {Path.GetFileName(file)}."),
            () => _searchExportColumns.SelectedFields);
    }

    private void UpdateExportColumnsButtonState()
    {
        int gridSel = _searchExportColumns.SelectedGridFieldCount;
        int gridTotal = _searchExportColumns.GridFieldCount;
        int extraSel = _searchExportColumns.SelectedExtraFieldCount;

        BtnSearchExportColumns.Text = extraSel > 0
            ? $"Campos ({gridSel}+{extraSel})"
            : $"Campos ({gridSel}/{gridTotal})";

        string names = _searchExportColumns.SelectedFields.Count > 0
            ? string.Join(", ", _searchExportColumns.SelectedFields.Select(WorkOrderExportFieldCatalog.GetDisplayName))
            : "Ninguno marcado";
        _toolTip.SetToolTip(
            BtnSearchExportColumns,
            $"Campos activos para exportar ({_searchExportColumns.SelectedFields.Count}): {names}.\nTambién puede marcar o desmarcar directamente sobre los encabezados de la tabla.");
    }

    private void ShowExportColumnsQuickMenu()
    {
        List<ToolStripItem> items =
        [
            ContextMenuHelper.Item(
                $"Marcar todas las columnas de la tabla ({_searchExportColumns.GridFieldCount})",
                AppIcon.Check,
                CambiasoTheme.Success,
                () => _searchExportColumns.SelectAllGridFields()),
            ContextMenuHelper.Item(
                "Solo campos esenciales (N° OT, Fecha, Proveedor, Solicitante, Detalle, Neto, Estado)",
                AppIcon.ClipboardCheck,
                CambiasoTheme.Primary,
                () => _searchExportColumns.SelectEssentialFields()),
            ContextMenuHelper.Separator()
        ];

        foreach ((_, WorkOrderExportField field) in _searchExportColumns.GridMappings)
        {
            bool active = _searchExportColumns.IsFieldSelected(field);
            string display = WorkOrderExportFieldCatalog.GetDisplayName(field);
            items.Add(ContextMenuHelper.Item(
                active ? $"✓  {display}" : $"    {display}",
                active ? AppIcon.Check : AppIcon.Remove,
                active ? CambiasoTheme.Accent : CambiasoTheme.TextMuted,
                () => _searchExportColumns.ToggleField(field)));
        }

        items.Add(ContextMenuHelper.Separator());

        WorkOrderExportField[] extraFields = [WorkOrderExportField.TipoOrden, WorkOrderExportField.RutaActivo];
        foreach (WorkOrderExportField extra in extraFields)
        {
            bool active = _searchExportColumns.IsFieldSelected(extra);
            string display = WorkOrderExportFieldCatalog.GetDisplayName(extra);
            items.Add(ContextMenuHelper.Item(
                active ? $"✓  Campo adicional: {display}" : $"    Campo adicional: {display}",
                active ? AppIcon.Check : AppIcon.Add,
                active ? CambiasoTheme.Primary : CambiasoTheme.TextMuted,
                () => _searchExportColumns.ToggleField(extra)));
        }

        ContextMenuHelper.ShowBelow(BtnSearchExportColumns, items);
    }

    private void ClearSearchTab()
    {
        _searchDateRange.Clear();
        _chkFilterProvider.Checked = false;
        _chkFilterRequester.Checked = false;
        _chkFilterStatus.Checked = false;
        _chkFilterText.Checked = true;
        _chkFilterTree.Checked = false;
        _txtSearchKeyword.Clear();
        _lastSearchResults = [];
        _searchSelection.ClearSelection();
        _gridSearchResults.DataSource = null;
        _searchPager.SetState(0, _searchPager.PageSize, 0);
        _lblSearchSummary.Text = "Filtros y selección restablecidos.";
        UpdateSearchSelectionState();
    }

    private string BuildSearchFilterSummary()
    {
        string prov = _chkFilterProvider.Checked && _cmbProviderSearch.SelectedItem is Provider p ? p.Nombre : "Todos";
        string sol = _chkFilterRequester.Checked && _cmbRequesterSearch.SelectedItem is Requester r ? r.Alias : "Todos";
        string est = _chkFilterStatus.Checked && _cmbStatusSearch.SelectedItem is WorkOrderStatusOption st ? st.DisplayName : "Todos";
        return $"{_searchDateRange.BuildFilterDescription()} | Proveedor: {prov} | Solicitante: {sol} | Estado: {est}";
    }

    internal ToolStripItem[] BuildSearchContextMenuItems(int rowIndex)
    {
        WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults, rowIndex);
        if (row is null)
        {
            return [];
        }

        int idOt = row.IdOt;
        int numOt = row.NumeroOt;
        bool canEdit = _session.Permisos.IngresoOt;

        IReadOnlyList<int> selectedIds = _searchSelection.SelectedIds;
        bool applyBatchStatus = selectedIds.Count > 1 && selectedIds.Contains(idOt);
        ToolStripMenuItem statusSubMenu = applyBatchStatus
            ? WorkOrderStatusUi.BuildChangeStatusMenu(
                null,
                canEdit,
                newStatus => ChangeWorkOrdersStatusBatch(selectedIds, newStatus),
                $"Cambiar estado ({selectedIds.Count} seleccionadas)")
            : WorkOrderStatusUi.BuildChangeStatusMenu(
                row.Estado,
                canEdit,
                newStatus => ChangeWorkOrderStatusSingle(idOt, numOt, newStatus));

        return
        [
            ContextMenuHelper.Item("Consultar OT (Detalle Completo)", AppIcon.FileText, CambiasoTheme.Primary, () => ConsultOrderByNumber(numOt), enabled: _session.Permisos.MenuOrdenesTrabajo),
            ContextMenuHelper.Item("Visualizar OT (Vista Previa)", AppIcon.Preview, CambiasoTheme.Primary, () => ExportUiHelper.ShowWorkOrderPreview(this, _ctx, numOt), enabled: _session.Permisos.MenuOrdenesTrabajo),
            ContextMenuHelper.Item("Editar OT", AppIcon.Edit, CambiasoTheme.Accent, () => OpenOrderByNumber(numOt), enabled: canEdit),
            ContextMenuHelper.Separator(),
            statusSubMenu,
            ContextMenuHelper.Separator(),
            ContextMenuHelper.Item("Eliminar OT", AppIcon.Delete, CambiasoTheme.Danger, () => DeleteSearchOrder(numOt), enabled: canEdit)
        ];
    }

    private void ChangeWorkOrderStatusSingle(int idOt, int numeroOt, WorkOrderStatus newStatus)
    {
        try
        {
            _ctx.WorkOrderService.UpdateStatus(_session, idOt, newStatus);
            if (_loadedOrder is not null && _loadedOrder.IdOt == idOt)
            {
                _loadedOrder.Estado = newStatus;
                SelectEntryStatus(newStatus);
            }

            ExecuteSearchCore(showEmptyMessage: false);
            _lblSearchSummary.Text = $"OT N° {numeroOt} actualizada a estado '{newStatus.ToDisplayName()}'.";
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, $"No se pudo cambiar el estado de la OT N° {numeroOt}: {ex.Message}");
        }
    }

    private void ChangeWorkOrdersStatusBatch(IReadOnlyList<int> idOts, WorkOrderStatus newStatus)
    {
        try
        {
            int updated = _ctx.WorkOrderService.UpdateStatusMany(_session, idOts, newStatus);
            if (_loadedOrder is not null && idOts.Contains(_loadedOrder.IdOt))
            {
                _loadedOrder.Estado = newStatus;
                SelectEntryStatus(newStatus);
            }

            ExecuteSearchCore(showEmptyMessage: false);
            _lblSearchSummary.Text = $"{updated} OT(s) actualizadas a estado '{newStatus.ToDisplayName()}'.";
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, $"No se pudo cambiar el estado de las OTs seleccionadas: {ex.Message}");
        }
    }

    private WorkOrderStatus GetSelectedEntryStatus() =>
        _cmbEstadoEntry.SelectedItem is WorkOrderStatusOption opt ? opt.Value : WorkOrderStatus.Abierta;

    private void SelectEntryStatus(WorkOrderStatus status)
    {
        for (int i = 0; i < _cmbEstadoEntry.Items.Count; i++)
        {
            if (_cmbEstadoEntry.Items[i] is WorkOrderStatusOption opt && opt.Value == status)
            {
                _cmbEstadoEntry.SelectedIndex = i;
                return;
            }
        }

        if (_cmbEstadoEntry.Items.Count > 0)
        {
            _cmbEstadoEntry.SelectedIndex = 0;
        }
    }

    private void OpenSelectedSearchOrder()
    {
        WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults);
        if (row is null)
        {
            return;
        }

        if (_session.Permisos.IngresoOt)
        {
            OpenOrderByNumber(row.NumeroOt);
        }
        else
        {
            ConsultOrderByNumber(row.NumeroOt);
        }
    }

    private ComboBox CreateSignerCombo(string name)
    {
        ComboBox cmb = new()
        {
            Name = name,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill,
            Margin = new Padding(2, 2, 2, 2)
        };
        _toolTip.SetToolTip(cmb, "Seleccionar firmante desde Personal y Usuarios");
        return cmb;
    }

    private static void BindSignerCombo(ComboBox combo, IReadOnlyList<SignaturePersonOption> items)
    {
        string? previousName = combo.SelectedValue as string ?? (combo.SelectedItem as SignaturePersonOption)?.Nombre;
        combo.DataSource = null;
        combo.DisplayMember = nameof(SignaturePersonOption.Etiqueta);
        combo.ValueMember = nameof(SignaturePersonOption.Nombre);
        combo.DataSource = items.ToList();

        if (!string.IsNullOrWhiteSpace(previousName))
        {
            SelectSignerInCombo(combo, previousName);
        }
    }

    private static void SelectSignerInCombo(ComboBox combo, string? fullName)
    {
        if (combo.DataSource is not IList<SignaturePersonOption> list || list.Count == 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            string target = fullName.Trim();
            int idx = -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Nombre, target, StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }

            if (idx < 0)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].Nombre.Contains(target, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = i;
                        break;
                    }
                }
            }

            if (idx >= 0)
            {
                combo.SelectedIndex = idx;
                return;
            }

            List<SignaturePersonOption> extended = [.. list, new SignaturePersonOption(target, target, "Histórico")];
            combo.DataSource = null;
            combo.DisplayMember = nameof(SignaturePersonOption.Etiqueta);
            combo.ValueMember = nameof(SignaturePersonOption.Nombre);
            combo.DataSource = extended;
            combo.SelectedIndex = extended.Count - 1;
            return;
        }

        combo.SelectedIndex = 0;
    }

    private static string GetSignerName(ComboBox combo) =>
        (combo.SelectedItem as SignaturePersonOption)?.Nombre ?? string.Empty;

    private void ApplyDefaultSignersForDraft()
    {
        SelectSignerInCombo(CmbEntrySigJefatura, "Juan Ramos");
        Requester? entryReq = _cmbRequesterEntry.SelectedItem as Requester;
        string tecName = entryReq is not null && !string.IsNullOrWhiteSpace(entryReq.NombreCompleto)
            ? entryReq.NombreCompleto
            : "Técnico";
        SelectSignerInCombo(CmbEntrySigTecnico, entryReq is not null ? tecName : null);
        SelectSignerInCombo(CmbEntrySigSupervisor, "Marcelo Silva");
    }

    private void SelectOrderSignersInEntryCombos(WorkOrder order)
    {
        SelectSignerInCombo(CmbEntrySigJefatura,
            string.IsNullOrWhiteSpace(order.FirmaJefaturaNombre) ? "Juan Ramos" : order.FirmaJefaturaNombre);
        SelectSignerInCombo(CmbEntrySigTecnico,
            !string.IsNullOrWhiteSpace(order.FirmaTecnicoNombre) ? order.FirmaTecnicoNombre : order.SolicitanteDisplayNombre);
        SelectSignerInCombo(CmbEntrySigSupervisor,
            string.IsNullOrWhiteSpace(order.FirmaSupervisorNombre) ? "Marcelo Silva" : order.FirmaSupervisorNombre);
    }

    private static FlowLayoutPanel WrapLabel(string labelText, Control control)
    {
        FlowLayoutPanel p = new() { AutoSize = true, Margin = new Padding(4, 2, 10, 2), WrapContents = false };
        Label lbl = CambiasoTheme.CreateFieldLabel(labelText);
        lbl.Margin = new Padding(0, 5, 6, 0);
        p.Controls.Add(lbl);
        p.Controls.Add(control);
        return p;
    }

    private static void DigitsOnly_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_syncSubscribed)
            {
                _ctx.SyncHub.DataChanged -= OnSyncChanged;
                _syncSubscribed = false;
            }

            _searchExportColumns.Detach();
            _valorNetoFont.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class WorkOrderGridRow(WorkOrderSearchRow source) : IGridRowSource<WorkOrderSearchRow>
    {
        [Browsable(false)]
        public WorkOrderSearchRow Source { get; } = source;
        public int NumeroOt => Source.NumeroOt;
        public DateTime Fecha => Source.Fecha;
        public string Proveedor => Source.Proveedor;
        public string Solicitante => Source.Solicitante;
        public int Cantidad => Source.Cantidad;
        public string Detalle => Source.Detalle;
        public string Observacion => Source.Observacion;
        public decimal ValorNeto => Source.ValorNeto;
        public string Estado => Source.EstadoDisplay;
        public string Firmas => Source.FirmasDisplay;
    }
}
