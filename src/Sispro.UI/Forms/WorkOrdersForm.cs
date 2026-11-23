using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo de Trabajos Externos / Órdenes de Trabajo (reemplaza tab_ot.frm, TAB_0.bas, TAB_2.bas y exp_excel.bas).
/// Incluye Ingreso/Edición con VB Supervisor y VB Jefatura, Búsqueda avanzada con exportación Excel asíncrona,
/// y Generación/Vista Previa/Impresión corporativa.
/// </summary>
public sealed class WorkOrdersForm : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly AssetTreePanel _assetTree;
    private readonly TabControl _tabs;
    private readonly TabPage _tabEntry;
    private readonly TabPage _tabSearch;
    private readonly TabPage _tabPrint;
    private readonly Label _lblNextOtHeader;

    internal Button BtnNewExternalOt { get; }
    internal Button BtnExportPrintOt { get; }

    // Tab 0 (Ingreso / Edición OT)
    private readonly TextBox _txtNumeroOt;
    private readonly Button _btnLookupOt;
    private readonly Button _btnNextOt;
    private readonly ComboBox _cmbProviderEntry;
    private readonly ComboBox _cmbRequesterEntry;
    private readonly DateTimePicker _dtFechaOt;
    private readonly CheckBox _chkVbs;
    private readonly CheckBox _chkVbj;
    private readonly NumericUpDown _numCantidad;
    private readonly TextBox _txtDetalle;
    private readonly Button _btnAddDetail;
    private readonly Button _btnRemoveDetail;
    private readonly DataGridView _gridEntryDetails;
    private readonly Button _btnSaveOt;
    private readonly Button _btnSaveAndExportOt;
    private readonly Button _btnUpdateOt;
    private readonly Button _btnDeleteOt;
    private readonly Button _btnClearEntry;
    private readonly Label _lblEntryStatus;
    private readonly List<WorkOrderDetail> _entryDetails = [];
    private WorkOrder? _loadedOrder;
    private bool _suppressApprovalEvents;

    // Tab 1 (Búsqueda de OT)
    private readonly DateRangeFilterControl _searchDateRange;
    private readonly CheckBox _chkFilterProvider;
    private readonly ComboBox _cmbProviderSearch;
    private readonly CheckBox _chkFilterRequester;
    private readonly ComboBox _cmbRequesterSearch;
    private readonly CheckBox _chkFilterText;
    private readonly TextBox _txtSearchKeyword;
    private readonly CheckBox _chkFilterTree;
    private readonly CheckBox _chkPendingVb;
    private readonly Button _btnExecuteSearch;
    private readonly Button _btnClearSearch;
    private readonly Button _btnExportSearch;
    private readonly DataGridView _gridSearchResults;
    private readonly ProgressBar _exportProgress;
    private readonly Label _lblSearchSummary;
    private IReadOnlyList<WorkOrderSearchRow> _lastSearchResults = [];
    private bool _syncSubscribed;

    // Tab 2 (Generar / Imprimir OT)
    private readonly TextBox _txtPrintOtNumber;
    private readonly Button _btnLookupPrintOt;
    private readonly ComboBox _cmbProviderPrint;
    private readonly TextBox _txtPrintProvDir;
    private readonly TextBox _txtPrintProvMail;
    private readonly ComboBox _cmbRequesterPrint;
    private readonly TextBox _txtPrintSolMail;
    private readonly TextBox _txtPrintSolFono;
    private readonly TextBox _txtPrintSolDir;
    private readonly ComboBox _cmbSignature;
    private readonly NumericUpDown _numPrintCantidad;
    private readonly TextBox _txtPrintWork;
    private readonly Button _btnAddPrintItem;
    private readonly Button _btnRemovePrintItem;
    private readonly DataGridView _gridPrintItems;
    private readonly Button _btnGenerateExcel;
    private readonly Button _btnPrintPreview;
    private readonly Button _btnPrintDirect;
    private readonly Button _btnClearPrint;
    private readonly List<WorkOrderDetail> _printDetails = [];
    private WorkOrder? _printOrder;

    public WorkOrdersForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        SplitContainer split = new()
        {
            Size = new Size(1080, 700),
            Dock = DockStyle.Fill,
            Panel1MinSize = 260,
            SplitterDistance = 290,
            FixedPanel = FixedPanel.Panel1,
            BackColor = CambiasoTheme.BorderColor
        };
        Load += (_, _) =>
        {
            if (split.Width > 540)
            {
                split.SplitterDistance = 290;
            }
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
        split.Panel1.Controls.Add(_assetTree);

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

        BtnNewExternalOt = CambiasoTheme.CreateButton("Nueva OT Externa", AppIcon.Add, ButtonTone.Gold, _toolTip, "Iniciar inmediatamente una nueva Orden de Trabajo externa", width: 176, height: 36);
        BtnNewExternalOt.Enabled = _session.Permisos.IngresoOt;
        BtnNewExternalOt.Click += (_, _) => StartNewExternalOrder();

        BtnExportPrintOt = CambiasoTheme.CreateButton("Exportar / Imprimir OT", AppIcon.Print, ButtonTone.Accent, _toolTip, "Abrir documento oficial de OT para exportar (Excel, PDF, Word, CSV) o imprimir", width: 192, height: 36);
        BtnExportPrintOt.Enabled = _session.Permisos.ImpresionOt;
        BtnExportPrintOt.Click += (_, _) => OpenPrintFromHeader();

        _lblNextOtHeader = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Margin = new Padding(14, 9, 4, 0)
        };

        FlowLayoutPanel actionFlow = new() { Dock = DockStyle.Fill, WrapContents = false };
        actionFlow.Controls.AddRange([BtnNewExternalOt, BtnExportPrintOt, _lblNextOtHeader]);
        topActionStrip.Controls.Add(actionFlow);

        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.LabelFont,
            Padding = new Point(14, 6)
        };

        // --- TAB 0: INGRESO / EDICIÓN ---
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
        _btnLookupOt = CambiasoTheme.CreateButton("Buscar OT", AppIcon.Search, ButtonTone.Secondary, _toolTip, "Cargar orden de trabajo existente por número", width: 112, height: 28);
        _btnLookupOt.Click += (_, _) => LookupEntryOrder();

        _btnNextOt = CambiasoTheme.CreateButton("Siguiente N°", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Sugerir el siguiente correlativo disponible", width: 122, height: 28);
        _btnNextOt.Click += (_, _) => _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString();

        _cmbProviderEntry = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 195 };
        _cmbRequesterEntry = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 155 };
        _dtFechaOt = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 120, Value = DateTime.Today };

        _chkVbs = new CheckBox
        {
            Text = "VB Supervisor",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Visible = _session.Permisos.VistoBuenoSupervisor
        };
        _chkVbs.CheckedChanged += (_, _) => ToggleApproval(ApprovalType.Supervisor, _chkVbs.Checked);

        _chkVbj = new CheckBox
        {
            Text = "VB Jefatura",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Visible = _session.Permisos.VistoBuenoJefatura
        };
        _chkVbj.CheckedChanged += (_, _) => ToggleApproval(ApprovalType.Jefatura, _chkVbj.Checked);

        _numCantidad = new NumericUpDown { Minimum = 1, Maximum = 99999, Value = 1, Width = 76 };
        _txtDetalle = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        _btnAddDetail = CambiasoTheme.CreateButton("Agregar Ítem", AppIcon.Add, ButtonTone.Accent, _toolTip, "Agregar trabajo al detalle de la OT", width: 132, height: 32);
        _btnAddDetail.Click += (_, _) => AddEntryDetail();

        _btnRemoveDetail = CambiasoTheme.CreateButton("Quitar Ítem", AppIcon.Remove, ButtonTone.Secondary, _toolTip, "Quitar fila seleccionada del detalle", width: 124, height: 32);
        _btnRemoveDetail.Click += (_, _) => RemoveSelectedEntryDetail();

        _gridEntryDetails = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridEntryDetails);
        _gridEntryDetails.CellDoubleClick += (_, _) => EditSelectedEntryDetail();
        ContextMenuHelper.AttachToGrid(_gridEntryDetails, idx =>
            idx >= 0 && idx < _entryDetails.Count
                ?
                [
                    ContextMenuHelper.Item("Editar ítem", AppIcon.Edit, CambiasoTheme.Primary, EditSelectedEntryDetail, enabled: !_entryDetails[idx].AsociadoAFactura),
                    ContextMenuHelper.Item("Quitar ítem", AppIcon.Remove, CambiasoTheme.Danger, RemoveSelectedEntryDetail, enabled: !_entryDetails[idx].AsociadoAFactura)
                ]
                : []);

        _btnSaveOt = CambiasoTheme.CreateButton("Guardar Nueva", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar nueva orden de trabajo en base de datos", width: 146);
        _btnSaveOt.Click += (_, _) => SaveNewOrder();

        _btnSaveAndExportOt = CambiasoTheme.CreateButton("Guardar y Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Guardar la nueva OT y abrir inmediatamente el menú de exportación", width: 174);
        _btnSaveAndExportOt.Enabled = _session.Permisos.ImpresionOt;
        _btnSaveAndExportOt.Click += (_, _) => SaveAndExportNewOrder();

        _btnUpdateOt = CambiasoTheme.CreateButton("Modificar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Guardar cambios en la orden cargada", width: 124);
        _btnUpdateOt.Enabled = false;
        _btnUpdateOt.Click += (_, _) => UpdateLoadedOrder();

        _btnDeleteOt = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar orden de trabajo cargada (si no tiene facturas)", width: 118);
        _btnDeleteOt.Enabled = false;
        _btnDeleteOt.Click += (_, _) => DeleteLoadedOrder();

        _btnClearEntry = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Limpiar formulario para nueva OT", width: 112);
        _btnClearEntry.Click += (_, _) => ClearEntryForm();

        _lblEntryStatus = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, ForeColor = CambiasoTheme.TextMuted };

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
        _txtSearchKeyword = new TextBox { Width = 185, PlaceholderText = "Ej. motor, MAI, 2815..." };
        _txtSearchKeyword.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ExecuteSearch();
            }
        };
        _chkFilterText.CheckedChanged += (_, _) => _txtSearchKeyword.Enabled = _chkFilterText.Checked;

        _chkFilterTree = new CheckBox { Text = "Nodo árbol", AutoSize = true };
        _chkPendingVb = new CheckBox { Text = "Pendientes VB", AutoSize = true };

        _btnExecuteSearch = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Ejecutar búsqueda combinada de órdenes de trabajo", width: 102, height: 30);
        _btnExecuteSearch.Click += (_, _) => ExecuteSearch();

        _btnClearSearch = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Restablecer filtros y resultados", width: 98, height: 30);
        _btnClearSearch.Click += (_, _) => ClearSearchTab();

        _btnExportSearch = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar resultados filtrados a Excel, PDF, Word o CSV", width: 112, height: 30);
        _btnExportSearch.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            _btnExportSearch,
            this,
            _ctx.DocumentExportService,
            "SISPRO_OrdenesTrabajo",
            () => DocumentExportService.BuildWorkOrdersReport(_lastSearchResults, BuildSearchFilterSummary()),
            file => _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Órdenes de Trabajo", "EXPORTAR_OT_LISTA", $"Exportados {_lastSearchResults.Count} registros a {Path.GetFileName(file)}."));

        _gridSearchResults = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridSearchResults);
        _gridSearchResults.CellDoubleClick += (_, _) => OpenSelectedSearchOrder();
        ContextMenuHelper.AttachToGrid(_gridSearchResults, idx =>
        {
            if (idx < 0 || idx >= _lastSearchResults.Count)
            {
                return [];
            }

            int numOt = _lastSearchResults[idx].NumeroOt;
            return
            [
                ContextMenuHelper.Item("Abrir / Editar OT", AppIcon.Edit, CambiasoTheme.Primary, () => OpenOrderByNumber(numOt), enabled: _session.Permisos.IngresoOt),
                ContextMenuHelper.Item("Exportar / Imprimir OT", AppIcon.Print, CambiasoTheme.Accent, () => ShowPrintForOrder(numOt), enabled: _session.Permisos.ImpresionOt),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Eliminar OT", AppIcon.Delete, CambiasoTheme.Danger, () => DeleteSearchOrder(numOt), enabled: _session.Permisos.IngresoOt)
            ];
        });

        _exportProgress = new ProgressBar { Width = 220, Height = 20, Visible = false, Minimum = 0, Maximum = 100 };
        _lblSearchSummary = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, ForeColor = CambiasoTheme.TextMuted, Text = "Use los filtros y presione Buscar." };

        BuildSearchTabLayout();

        // --- TAB 2: GENERAR / IMPRIMIR OT ---
        _tabPrint = new TabPage("Generar / Imprimir OT") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtPrintOtNumber = new TextBox { Width = 100, MaxLength = 8 };
        _txtPrintOtNumber.KeyPress += DigitsOnly_KeyPress;
        _txtPrintOtNumber.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LookupPrintOrder();
            }
        };
        _btnLookupPrintOt = CambiasoTheme.CreateButton("Cargar OT", AppIcon.Search, ButtonTone.Primary, _toolTip, "Cargar datos de OT registrada para generar documento", width: 120, height: 28);
        _btnLookupPrintOt.Click += (_, _) => LookupPrintOrder();

        _cmbProviderPrint = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
        _cmbProviderPrint.SelectedIndexChanged += (_, _) => SyncPrintProviderInfo();
        _txtPrintProvDir = new TextBox { ReadOnly = true, Width = 320, BackColor = CambiasoTheme.Surface };
        _txtPrintProvMail = new TextBox { ReadOnly = true, Width = 220, BackColor = CambiasoTheme.Surface };

        _cmbRequesterPrint = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        _cmbRequesterPrint.SelectedIndexChanged += (_, _) => SyncPrintRequesterInfo();
        _txtPrintSolMail = new TextBox { ReadOnly = true, Width = 210, BackColor = CambiasoTheme.Surface };
        _txtPrintSolFono = new TextBox { ReadOnly = true, Width = 150, BackColor = CambiasoTheme.Surface };
        _txtPrintSolDir = new TextBox { ReadOnly = true, Width = 320, BackColor = CambiasoTheme.Surface, Text = _ctx.Settings.CompanyAddress };

        _cmbSignature = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
        _cmbSignature.Items.AddRange(["Ambas Firmas (JR + MS)", "Juan Ramos Moyano (JR)", "Marcelo Silva Morgado (MS)", "Sin firma digital"]);
        _cmbSignature.SelectedIndex = 0;

        _numPrintCantidad = new NumericUpDown { Minimum = 1, Maximum = 99999, Value = 1, Width = 72 };
        _txtPrintWork = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
        _btnAddPrintItem = CambiasoTheme.CreateButton("Agregar", AppIcon.Add, ButtonTone.Accent, _toolTip, "Agregar línea de trabajo al documento", width: 112, height: 30);
        _btnAddPrintItem.Click += (_, _) => AddPrintDetail();
        _btnRemovePrintItem = CambiasoTheme.CreateButton("Quitar", AppIcon.Remove, ButtonTone.Secondary, _toolTip, "Quitar línea seleccionada", width: 102, height: 30);
        _btnRemovePrintItem.Click += (_, _) => RemovePrintDetail();

        _gridPrintItems = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridPrintItems);
        _gridPrintItems.CellDoubleClick += (_, _) => EditSelectedPrintDetail();
        ContextMenuHelper.AttachToGrid(_gridPrintItems, idx =>
            idx >= 0 && idx < _printDetails.Count
                ?
                [
                    ContextMenuHelper.Item("Editar línea", AppIcon.Edit, CambiasoTheme.Primary, EditSelectedPrintDetail),
                    ContextMenuHelper.Item("Quitar línea", AppIcon.Remove, CambiasoTheme.Danger, RemovePrintDetail)
                ]
                : []);

        _btnGenerateExcel = CambiasoTheme.CreateButton("Exportar OT", AppIcon.Download, ButtonTone.Primary, _toolTip, "Exportar documento oficial de OT a Excel, PDF, Word o CSV", width: 136);
        _btnGenerateExcel.Click += (_, _) => OpenWorkOrderExportMenu();

        _btnPrintPreview = CambiasoTheme.CreateButton("Vista Previa", AppIcon.Preview, ButtonTone.Accent, _toolTip, "Abrir vista previa de impresión de la OT", width: 136);
        _btnPrintPreview.Click += (_, _) => ShowPrintPreview();

        _btnPrintDirect = CambiasoTheme.CreateButton("Imprimir", AppIcon.Print, ButtonTone.Secondary, _toolTip, "Enviar orden de trabajo a la impresora", width: 118);
        _btnPrintDirect.Click += (_, _) => PrintOrderDirect();

        _btnClearPrint = CambiasoTheme.CreateButton("Limpiar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Limpiar plantilla de impresión", width: 112);
        _btnClearPrint.Click += (_, _) => ClearPrintTab();

        BuildPrintTabLayout();

        // Enforce strict RBAC tab visibility
        if (_session.Permisos.IngresoOt)
        {
            _tabs.TabPages.Add(_tabEntry);
        }

        if (_session.Permisos.BusquedaOt)
        {
            _tabs.TabPages.Add(_tabSearch);
        }

        if (_session.Permisos.ImpresionOt)
        {
            _tabs.TabPages.Add(_tabPrint);
        }

        split.Panel2.Controls.Add(_tabs);
        split.Panel2.Controls.Add(topActionStrip);
        Controls.Add(split);

        LoadCatalogs();
        RefreshNextOtBadge();
        _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString(CultureInfo.InvariantCulture);
        RefreshEntryDetailsGrid();
        RefreshPrintDetailsGrid();

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & DataChangeKind.Catalogs) == 0)
        {
            return;
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(ReloadCatalogs);
        else if (!InvokeRequired) ReloadCatalogs();
    }

    public void OpenOrderByNumber(int numeroOt)
    {
        if (_tabs.TabPages.Contains(_tabEntry))
        {
            _tabs.SelectedTab = _tabEntry;
            _txtNumeroOt.Text = numeroOt.ToString(CultureInfo.InvariantCulture);
            LookupEntryOrder();
        }
        else if (_tabs.TabPages.Contains(_tabPrint))
        {
            _tabs.SelectedTab = _tabPrint;
            _txtPrintOtNumber.Text = numeroOt.ToString(CultureInfo.InvariantCulture);
            LookupPrintOrder();
        }
    }

    public void StartNewExternalOrder()
    {
        if (_tabs.TabPages.Contains(_tabEntry))
        {
            _tabs.SelectedTab = _tabEntry;
            ClearEntryForm();
            _cmbProviderEntry.Focus();
        }
    }

    public void ShowPrintForOrder(int numeroOt)
    {
        if (_tabs.TabPages.Contains(_tabPrint))
        {
            _tabs.SelectedTab = _tabPrint;
            _txtPrintOtNumber.Text = numeroOt.ToString(CultureInfo.InvariantCulture);
            LookupPrintOrder();
        }
    }

    private void OpenPrintFromHeader()
    {
        if (_loadedOrder is not null)
        {
            ShowPrintForOrder(_loadedOrder.NumeroOt);
        }
        else if (_tabs.TabPages.Contains(_tabPrint))
        {
            _tabs.SelectedTab = _tabPrint;
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

        OpenOrderByNumber(numeroOt);
        DeleteLoadedOrder();
        ExecuteSearch();
    }

    private void BuildEntryTabLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 144));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 134));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        GroupBox grpHeader = CambiasoTheme.CreateGroupBox("Datos Generales de la Orden de Trabajo");
        grpHeader.Dock = DockStyle.Fill;

        FlowLayoutPanel row1 = new() { Dock = DockStyle.Top, Height = 38, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        row1.Controls.AddRange(
        [
            WrapLabel("N° OT:", _txtNumeroOt),
            _btnLookupOt,
            _btnNextOt,
            WrapLabel("Fecha:", _dtFechaOt)
        ]);

        FlowLayoutPanel row2 = new() { Dock = DockStyle.Top, Height = 38, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        row2.Controls.AddRange(
        [
            WrapLabel("Proveedor:", _cmbProviderEntry),
            WrapLabel("Solicitado por:", _cmbRequesterEntry)
        ]);

        FlowLayoutPanel row3 = new() { Dock = DockStyle.Top, Height = 32, WrapContents = false, Padding = new Padding(6, 4, 4, 0) };
        _chkVbs.Margin = new Padding(0, 2, 20, 0);
        _chkVbj.Margin = new Padding(0, 2, 12, 0);
        Label lblVb = CambiasoTheme.CreateFieldLabel("Aprobaciones:");
        lblVb.Margin = new Padding(0, 3, 12, 0);
        lblVb.Visible = _chkVbs.Visible || _chkVbj.Visible;
        row3.Controls.AddRange([lblVb, _chkVbs, _chkVbj]);

        grpHeader.Controls.Add(row3);
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

        FlowLayoutPanel detBtns = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(4, 12, 0, 0) };
        detBtns.Controls.Add(_btnAddDetail);
        detBtns.Controls.Add(_btnRemoveDetail);

        detTbl.Controls.Add(cantPanel, 0, 0);
        detTbl.Controls.Add(descPanel, 1, 0);
        detTbl.Controls.Add(detBtns, 2, 0);
        grpDetail.Controls.Add(detTbl);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Ítems de la OT (doble clic para editar)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridEntryDetails);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnSaveOt, _btnSaveAndExportOt, _btnUpdateOt, _btnDeleteOt, _btnClearEntry, _lblEntryStatus]);

        layout.Controls.Add(grpHeader, 0, 0);
        layout.Controls.Add(grpDetail, 0, 1);
        layout.Controls.Add(grpGrid, 0, 2);
        layout.Controls.Add(footer, 0, 3);
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
        f3.Controls.AddRange([_chkFilterText, _txtSearchKeyword, _chkFilterTree, _chkPendingVb, _btnExecuteSearch]);

        grpFilters.Controls.Add(f3);
        grpFilters.Controls.Add(f2);
        grpFilters.Controls.Add(f1);

        GroupBox grpResults = CambiasoTheme.CreateGroupBox("Resultados (orden cronológico descendente — doble clic para abrir)");
        grpResults.Dock = DockStyle.Fill;
        grpResults.Controls.Add(_gridSearchResults);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnExportSearch, _btnClearSearch, _exportProgress, _lblSearchSummary]);

        layout.Controls.Add(grpFilters, 0, 0);
        layout.Controls.Add(grpResults, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        _tabSearch.Controls.Add(layout);
    }

    private void BuildPrintTabLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 184));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        Panel topPanel = new() { Dock = DockStyle.Fill };
        FlowLayoutPanel otBar = new() { Dock = DockStyle.Top, Height = 38, Padding = new Padding(2, 2, 2, 2) };
        otBar.Controls.AddRange([WrapLabel("N° OT:", _txtPrintOtNumber), _btnLookupPrintOt, WrapLabel("Firma Digital:", _cmbSignature)]);

        TableLayoutPanel cards = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        GroupBox grpProv = CambiasoTheme.CreateGroupBox("Datos Proveedor");
        grpProv.Dock = DockStyle.Fill;
        FlowLayoutPanel provFlow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        provFlow.Controls.Add(WrapLabel("Proveedor:", _cmbProviderPrint));
        provFlow.Controls.Add(WrapLabel("Dirección:", _txtPrintProvDir));
        provFlow.Controls.Add(WrapLabel("Correo:", _txtPrintProvMail));
        grpProv.Controls.Add(provFlow);

        GroupBox grpSol = CambiasoTheme.CreateGroupBox("Datos Solicitante");
        grpSol.Dock = DockStyle.Fill;
        FlowLayoutPanel solFlow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        solFlow.Controls.Add(WrapLabel("Solicitante:", _cmbRequesterPrint));
        solFlow.Controls.Add(WrapLabel("Correo:", _txtPrintSolMail));
        solFlow.Controls.Add(WrapLabel("Teléfono:", _txtPrintSolFono));
        solFlow.Controls.Add(WrapLabel("Planta:", _txtPrintSolDir));
        grpSol.Controls.Add(solFlow);

        cards.Controls.Add(grpProv, 0, 0);
        cards.Controls.Add(grpSol, 1, 0);

        topPanel.Controls.Add(cards);
        topPanel.Controls.Add(otBar);

        GroupBox grpAdd = CambiasoTheme.CreateGroupBox("Agregar Trabajo al Documento");
        grpAdd.Dock = DockStyle.Fill;
        TableLayoutPanel addTbl = new() { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        addTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        addTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 232));
        addTbl.Controls.Add(_numPrintCantidad, 0, 0);
        addTbl.Controls.Add(_txtPrintWork, 1, 0);
        FlowLayoutPanel addBtns = new() { Dock = DockStyle.Fill };
        addBtns.Controls.AddRange([_btnAddPrintItem, _btnRemovePrintItem]);
        addTbl.Controls.Add(addBtns, 2, 0);
        grpAdd.Controls.Add(addTbl);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Detalle a Imprimir / Exportar");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridPrintItems);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnGenerateExcel, _btnPrintPreview, _btnPrintDirect, _btnClearPrint]);

        layout.Controls.Add(topPanel, 0, 0);
        layout.Controls.Add(grpAdd, 0, 1);
        layout.Controls.Add(grpGrid, 0, 2);
        layout.Controls.Add(footer, 0, 3);
        _tabPrint.Controls.Add(layout);
    }

    private void LoadCatalogs()
    {
        IReadOnlyList<Provider> providers = _ctx.MaintainerService.GetProviders();
        IReadOnlyList<Requester> requesters = _ctx.MaintainerService.GetRequesters();

        BindCombo(_cmbProviderEntry, providers, nameof(Provider.Nombre), nameof(Provider.IdProv));
        BindCombo(_cmbProviderSearch, providers, nameof(Provider.Nombre), nameof(Provider.IdProv));
        BindCombo(_cmbProviderPrint, providers, nameof(Provider.Nombre), nameof(Provider.IdProv));

        BindCombo(_cmbRequesterEntry, requesters, nameof(Requester.Alias), nameof(Requester.IdSol));
        BindCombo(_cmbRequesterSearch, requesters, nameof(Requester.Alias), nameof(Requester.IdSol));
        BindCombo(_cmbRequesterPrint, requesters, nameof(Requester.Alias), nameof(Requester.IdSol));
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
            _btnSaveOt.Enabled = true;
            _btnSaveAndExportOt.Enabled = _session.Permisos.ImpresionOt;
            _btnUpdateOt.Enabled = false;
            _btnDeleteOt.Enabled = false;
            return;
        }

        _loadedOrder = order;
        _cmbProviderEntry.SelectedValue = order.IdProv;
        if (order.IdSol.HasValue)
        {
            _cmbRequesterEntry.SelectedValue = order.IdSol.Value;
        }

        _dtFechaOt.Value = order.FechaOt;
        _entryDetails.Clear();
        _entryDetails.AddRange(order.Detalles);
        RefreshEntryDetailsGrid();

        _assetTree.SelectNodeByPath(order.GetLevelArray());

        _suppressApprovalEvents = true;
        _chkVbs.Checked = order.TieneVbs;
        _chkVbj.Checked = order.TieneVbj;
        _suppressApprovalEvents = false;

        _btnSaveOt.Enabled = false;
        _btnSaveAndExportOt.Enabled = false;
        _btnUpdateOt.Enabled = true;
        _btnDeleteOt.Enabled = true;
        _lblEntryStatus.Text = $"OT N° {order.NumeroOt} cargada (ID #{order.IdOt}) · Solicitante: {order.AliasSolicitante} · {order.Detalles.Count} ítem(s) · VBS: {(order.TieneVbs ? "Sí" : "No")} · VBJ: {(order.TieneVbj ? "Sí" : "No")}";
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
            if (_entryDetails[idx].AsociadoAFactura)
            {
                CambiasoTheme.ShowWarning(this, "No se puede quitar un ítem ya asociado a una guía/factura.");
                return;
            }

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
            if (!item.AsociadoAFactura)
            {
                _entryDetails.RemoveAt(idx);
                RefreshEntryDetailsGrid();
            }
        }
    }

    private void RefreshEntryDetailsGrid()
    {
        _gridEntryDetails.DataSource = null;
        _gridEntryDetails.DataSource = _entryDetails.Select((d, i) => new
        {
            Ítem = i + 1,
            d.Cantidad,
            Descripción = d.Descripcion,
            Estado = d.AsociadoAFactura ? "Facturado" : "Pendiente"
        }).ToList();

        if (_gridEntryDetails.Columns.Count >= 4)
        {
            _gridEntryDetails.Columns[0].FillWeight = 8;
            _gridEntryDetails.Columns[1].HeaderText = "Cant.";
            _gridEntryDetails.Columns[1].FillWeight = 10;
            _gridEntryDetails.Columns[2].FillWeight = 68;
            _gridEntryDetails.Columns[3].FillWeight = 14;
        }
    }

    private void SaveNewOrder()
    {
        SaveNewOrderCore(promptToPrint: true);
    }

    private void SaveAndExportNewOrder()
    {
        int? savedNum = SaveNewOrderCore(promptToPrint: false);
        if (savedNum.HasValue && _session.Permisos.ImpresionOt)
        {
            ShowPrintForOrder(savedNum.Value);
            BeginInvoke(OpenWorkOrderExportMenu);
        }
    }

    private int? SaveNewOrderCore(bool promptToPrint)
    {
        try
        {
            WorkOrder order = BuildOrderFromEntryForm();
            int id = _ctx.WorkOrderService.Create(_session, order);
            CambiasoTheme.ShowInfo(this, $"Orden de Trabajo N° {order.NumeroOt} guardada correctamente (ID #{id}).");
            ClearEntryForm();

            if (promptToPrint &&
                _session.Permisos.ImpresionOt &&
                CambiasoTheme.Confirm(this, $"¿Desea abrir la OT N° {order.NumeroOt} para exportar o imprimir ahora?"))
            {
                ShowPrintForOrder(order.NumeroOt);
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
            updated.Estado = _loadedOrder.Estado;
            updated.GuiaDespacho = _loadedOrder.GuiaDespacho;
            _ctx.WorkOrderService.Update(_session, updated);
            CambiasoTheme.ShowInfo(this, $"Orden de Trabajo N° {updated.NumeroOt} actualizada correctamente.");
            LookupEntryOrder();
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

        if (!CambiasoTheme.Confirm(this, $"¿Confirma que desea eliminar la OT N° {_loadedOrder.NumeroOt}? Esta acción no se puede deshacer."))
        {
            return;
        }

        try
        {
            _ctx.WorkOrderService.Delete(_session, _loadedOrder.IdOt);
            CambiasoTheme.ShowInfo(this, $"OT N° {_loadedOrder.NumeroOt} eliminada correctamente.");
            ClearEntryForm();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void ToggleApproval(ApprovalType type, bool approved)
    {
        if (_suppressApprovalEvents || _loadedOrder is null)
        {
            return;
        }

        try
        {
            _ctx.WorkOrderService.SetApproval(_session, _loadedOrder.IdOt, type, approved);
            _loadedOrder = _ctx.WorkOrderService.GetById(_loadedOrder.IdOt);
            _lblEntryStatus.Text = $"Visto Bueno actualizado · VBS: {(_loadedOrder?.TieneVbs == true ? "Sí" : "No")} · VBJ: {(_loadedOrder?.TieneVbj == true ? "Sí" : "No")}";
        }
        catch (Exception ex)
        {
            _suppressApprovalEvents = true;
            if (type == ApprovalType.Supervisor)
            {
                _chkVbs.Checked = !approved;
            }
            else
            {
                _chkVbj.Checked = !approved;
            }

            _suppressApprovalEvents = false;
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private WorkOrder BuildOrderFromEntryForm()
    {
        if (!int.TryParse(_txtNumeroOt.Text.Trim(), out int numOt) || numOt <= 0)
        {
            throw new ArgumentException("Debe ingresar un número de OT válido.");
        }

        int idProv = _cmbProviderEntry.SelectedValue is int p ? p : 0;
        int? idSol = _cmbRequesterEntry.SelectedValue is int s ? s : null;
        IReadOnlyList<int> pathIds = _assetTree.GetSelectedPathIds();

        WorkOrder order = new()
        {
            NumeroOt = numOt,
            IdProv = idProv,
            IdSol = idSol,
            FechaOt = _dtFechaOt.Value.Date,
            RutaActivo = _assetTree.CurrentBreadcrumb,
            Detalles = _entryDetails.Select(d => new WorkOrderDetail
            {
                IdDet = d.IdDet,
                Cantidad = d.Cantidad,
                Descripcion = d.Descripcion,
                AsociadoAFactura = d.AsociadoAFactura
            }).ToList()
        };
        order.SetLevels(pathIds);
        return order;
    }

    private void ClearEntryForm()
    {
        _loadedOrder = null;
        _entryDetails.Clear();
        _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString(CultureInfo.InvariantCulture);
        RefreshNextOtBadge();
        _dtFechaOt.Value = DateTime.Today;
        _numCantidad.Value = 1;
        _txtDetalle.Clear();
        _suppressApprovalEvents = true;
        _chkVbs.Checked = false;
        _chkVbj.Checked = false;
        _suppressApprovalEvents = false;
        _btnSaveOt.Enabled = true;
        _btnSaveAndExportOt.Enabled = _session.Permisos.ImpresionOt;
        _btnUpdateOt.Enabled = false;
        _btnDeleteOt.Enabled = false;
        _lblEntryStatus.Text = $"Modo ingreso de nueva OT · Activo actual: {_assetTree.SelectedAsset?.Descripcion ?? "CAMBIASO HNOS S.A.C."}";
        RefreshEntryDetailsGrid();
    }

    private void ExecuteSearch()
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
                TextoBusqueda = _chkFilterText.Checked ? _txtSearchKeyword.Text.Trim() : null,
                SoloPendientesVb = _chkPendingVb.Checked
            };

            if (_chkFilterTree.Checked)
            {
                AssetNode? node = _assetTree.SelectedAsset;
                int realDepth = node is null ? 0 : _ctx.MaintainerService.GetAssetPath(node.IdActivo).Count;
                if (node is null || realDepth < 2)
                {
                    CambiasoTheme.ShowWarning(this, "Seleccione al menos una planta (nivel 2 o superior) en el árbol izquierdo.");
                    return;
                }

                filter.AssetLevel = realDepth;
                filter.AssetNodeId = node.IdActivo;
            }

            Cursor = Cursors.WaitCursor;
            _lastSearchResults = _ctx.WorkOrderService.Search(_session, filter);
            _gridSearchResults.DataSource = _lastSearchResults.Select(r => new
            {
                r.NumeroOt,
                Fecha = r.Fecha.ToString("dd-MM-yyyy"),
                r.Proveedor,
                r.Solicitante,
                r.Cantidad,
                r.Detalle,
                VBS = r.TieneVbs ? "Sí" : "No",
                VBJ = r.TieneVbj ? "Sí" : "No",
                Facturado = r.AsociadoAFactura ? "Sí" : "No"
            }).ToList();

            if (_gridSearchResults.Columns.Count >= 9)
            {
                _gridSearchResults.Columns[0].HeaderText = "N° OT";
                _gridSearchResults.Columns[0].FillWeight = 8;
                _gridSearchResults.Columns[1].FillWeight = 10;
                _gridSearchResults.Columns[2].FillWeight = 16;
                _gridSearchResults.Columns[3].FillWeight = 11;
                _gridSearchResults.Columns[4].FillWeight = 6;
                _gridSearchResults.Columns[5].FillWeight = 34;
                _gridSearchResults.Columns[6].FillWeight = 5;
                _gridSearchResults.Columns[7].FillWeight = 5;
                _gridSearchResults.Columns[8].FillWeight = 7;
            }

            _lblSearchSummary.Text = $"{_lastSearchResults.Count:N0} registro(s) encontrados (más recientes primero).";
            if (_lastSearchResults.Count == 0)
            {
                CambiasoTheme.ShowInfo(this, "No se encontraron órdenes de trabajo con los criterios indicados.");
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void ClearSearchTab()
    {
        _searchDateRange.Clear();
        _chkFilterProvider.Checked = false;
        _chkFilterRequester.Checked = false;
        _chkFilterText.Checked = true;
        _chkFilterTree.Checked = false;
        _chkPendingVb.Checked = false;
        _txtSearchKeyword.Clear();
        _lastSearchResults = [];
        _gridSearchResults.DataSource = null;
        _lblSearchSummary.Text = "Filtros restablecidos.";
    }

    private string BuildSearchFilterSummary()
    {
        string prov = _chkFilterProvider.Checked && _cmbProviderSearch.SelectedItem is Provider p ? p.Nombre : "Todos";
        string sol = _chkFilterRequester.Checked && _cmbRequesterSearch.SelectedItem is Requester r ? r.Alias : "Todos";
        return $"{_searchDateRange.BuildFilterDescription()} | Proveedor: {prov} | Solicitante: {sol}";
    }

    private void OpenSelectedSearchOrder()
    {
        if (_gridSearchResults.CurrentRow is null)
        {
            return;
        }

        int idx = _gridSearchResults.CurrentRow.Index;
        if (idx >= 0 && idx < _lastSearchResults.Count)
        {
            OpenOrderByNumber(_lastSearchResults[idx].NumeroOt);
        }
    }

    private void LookupPrintOrder()
    {
        if (!int.TryParse(_txtPrintOtNumber.Text.Trim(), out int numOt) || numOt <= 0)
        {
            CambiasoTheme.ShowWarning(this, "Ingrese un N° de OT válido.");
            return;
        }

        WorkOrder? order = _ctx.WorkOrderService.GetByNumber(numOt);
        if (order is null)
        {
            CambiasoTheme.ShowWarning(this, $"No se encontró la OT N° {numOt}.");
            return;
        }

        _printOrder = order;
        _cmbProviderPrint.SelectedValue = order.IdProv;
        if (order.IdSol.HasValue)
        {
            _cmbRequesterPrint.SelectedValue = order.IdSol.Value;
        }

        SyncPrintProviderInfo();
        SyncPrintRequesterInfo();
        _printDetails.Clear();
        _printDetails.AddRange(order.Detalles);
        RefreshPrintDetailsGrid();
        _assetTree.SelectNodeByPath(order.GetLevelArray());
    }

    private void SyncPrintProviderInfo()
    {
        if (_cmbProviderPrint.SelectedItem is Provider p)
        {
            _txtPrintProvDir.Text = p.Direccion;
            _txtPrintProvMail.Text = p.Correo;
        }
    }

    private void SyncPrintRequesterInfo()
    {
        if (_cmbRequesterPrint.SelectedItem is Requester r)
        {
            _txtPrintSolMail.Text = r.Correo;
            _txtPrintSolFono.Text = r.Telefono;
            _txtPrintSolDir.Text = _ctx.Settings.CompanyAddress;
        }
    }

    private void AddPrintDetail()
    {
        if (string.IsNullOrWhiteSpace(_txtPrintWork.Text))
        {
            CambiasoTheme.ShowWarning(this, "Ingrese la descripción del trabajo.");
            return;
        }

        _printDetails.Add(new WorkOrderDetail
        {
            Cantidad = (int)_numPrintCantidad.Value,
            Descripcion = _txtPrintWork.Text.Trim()
        });
        _numPrintCantidad.Value = 1;
        _txtPrintWork.Clear();
        RefreshPrintDetailsGrid();
    }

    private void EditSelectedPrintDetail()
    {
        if (_gridPrintItems.CurrentRow is null)
        {
            return;
        }

        int idx = _gridPrintItems.CurrentRow.Index;
        if (idx >= 0 && idx < _printDetails.Count)
        {
            WorkOrderDetail item = _printDetails[idx];
            _numPrintCantidad.Value = Math.Clamp(item.Cantidad, 1, 99999);
            _txtPrintWork.Text = item.Descripcion;
            _printDetails.RemoveAt(idx);
            RefreshPrintDetailsGrid();
        }
    }

    private void RemovePrintDetail()
    {
        if (_gridPrintItems.CurrentRow is null)
        {
            return;
        }

        int idx = _gridPrintItems.CurrentRow.Index;
        if (idx >= 0 && idx < _printDetails.Count)
        {
            _printDetails.RemoveAt(idx);
            RefreshPrintDetailsGrid();
        }
    }

    private void RefreshPrintDetailsGrid()
    {
        _gridPrintItems.DataSource = null;
        _gridPrintItems.DataSource = _printDetails.Select((d, i) => new
        {
            N = i + 1,
            d.Cantidad,
            Trabajo = d.Descripcion
        }).ToList();

        if (_gridPrintItems.Columns.Count >= 3)
        {
            _gridPrintItems.Columns[0].HeaderText = "N°";
            _gridPrintItems.Columns[0].FillWeight = 8;
            _gridPrintItems.Columns[1].FillWeight = 12;
            _gridPrintItems.Columns[2].HeaderText = "Detalle de piezas en reparación y trabajo";
            _gridPrintItems.Columns[2].FillWeight = 80;
        }
    }

    private WorkOrder BuildPrintableOrder()
    {
        if (_printDetails.Count == 0)
        {
            throw new InvalidOperationException("Debe cargar una OT o agregar al menos un detalle antes de generar o imprimir.");
        }

        int numOt = int.TryParse(_txtPrintOtNumber.Text.Trim(), out int n) && n > 0 ? n : _ctx.WorkOrderService.GetNextWorkOrderNumber();
        Provider? prov = _cmbProviderPrint.SelectedItem as Provider;
        Requester? sol = _cmbRequesterPrint.SelectedItem as Requester;

        return new WorkOrder
        {
            NumeroOt = numOt,
            FechaOt = _printOrder?.FechaOt ?? DateTime.Today,
            NombreProveedor = prov?.Nombre ?? string.Empty,
            DireccionProveedor = _txtPrintProvDir.Text,
            CorreoProveedor = _txtPrintProvMail.Text,
            AliasSolicitante = sol?.Alias ?? string.Empty,
            CorreoSolicitante = _txtPrintSolMail.Text,
            TelefonoSolicitante = _txtPrintSolFono.Text,
            RutaActivo = _printOrder?.RutaActivo ?? _assetTree.CurrentBreadcrumb,
            GuiaDespacho = _printOrder?.GuiaDespacho ?? 0,
            Detalles = [.. _printDetails],
            VistosBuenos = _printOrder?.VistosBuenos ?? []
        };
    }

    private SignatureOption GetSelectedSignature() => _cmbSignature.SelectedIndex switch
    {
        0 => SignatureOption.Ambas,
        1 => SignatureOption.JuanRamos,
        2 => SignatureOption.MarceloSilva,
        _ => SignatureOption.Ninguna
    };

    private void OpenWorkOrderExportMenu()
    {
        try
        {
            WorkOrder order = BuildPrintableOrder();
            ExportUiHelper.ShowWorkOrderExportMenu(_btnGenerateExcel, this, _ctx.DocumentExportService, order, GetSelectedSignature());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ShowPrintPreview()
    {
        try
        {
            WorkOrder order = BuildPrintableOrder();
            using PrintDocument doc = _ctx.PrintService.CreateWorkOrderPrintDocument(order, GetSelectedSignature());
            using PrintPreviewDialog preview = new()
            {
                Document = doc,
                Width = 960,
                Height = 760,
                StartPosition = FormStartPosition.CenterParent
            };
            preview.ShowDialog(this);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void PrintOrderDirect()
    {
        try
        {
            WorkOrder order = BuildPrintableOrder();
            using PrintDocument doc = _ctx.PrintService.CreateWorkOrderPrintDocument(order, GetSelectedSignature());
            using PrintDialog dlg = new() { Document = doc, UseEXDialog = true };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                doc.Print();
                _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Órdenes de Trabajo", "IMPRIMIR_OT", $"OT N° {order.NumeroOt} enviada a impresora.");
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, $"Error al imprimir la OT: {ex.Message}");
        }
    }

    private void ClearPrintTab()
    {
        _printOrder = null;
        _txtPrintOtNumber.Clear();
        _printDetails.Clear();
        _txtPrintWork.Clear();
        _numPrintCantidad.Value = 1;
        RefreshPrintDetailsGrid();
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

            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
