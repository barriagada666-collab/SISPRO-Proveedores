using System.ComponentModel;
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
    private readonly Label _lblVbsDetail;
    private readonly Label _lblVbjDetail;
    private readonly Label _lblAuthorizedApprovers;
    private readonly NumericUpDown _numCantidad;
    private readonly TextBox _txtDetalle;
    private readonly Button _btnAddDetail;
    private readonly Button _btnRemoveDetail;
    private readonly DataGridView _gridEntryDetails;
    private readonly Font _valorNetoFont = new("Segoe UI", 15f, FontStyle.Bold);
    private readonly Label _lblEntrySigHint;
    private readonly Button _btnSaveOt;
    private readonly Button _btnSaveAndExportOt;
    private readonly Button _btnUpdateOt;
    private readonly Button _btnDeleteOt;
    private readonly Button _btnClearEntry;
    private readonly Label _lblEntryStatus;
    private readonly List<WorkOrderDetail> _entryDetails = [];
    private WorkOrder? _loadedOrder;
    private bool _suppressApprovalEvents;

    internal TextBox TxtObservacionEntry { get; }
    internal TextBox TxtValorNetoEntry { get; }
    internal CheckBox ChkEntrySigJefatura { get; }
    internal CheckBox ChkEntrySigTecnico { get; }
    internal CheckBox ChkEntrySigSupervisor { get; }

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
    private readonly GridPagerBar _searchPager;
    private readonly ProgressBar _exportProgress;
    private readonly Label _lblSearchSummary;
    private readonly GridCheckboxSelectionController<WorkOrderSearchRow> _searchSelection;
    private IReadOnlyList<WorkOrderSearchRow> _lastSearchResults = [];
    private bool _syncSubscribed;
    private bool _suppressPrintSignaturePersist;

    internal CheckBox ChkSelectAllSearchVisible { get; }
    internal Label LblSearchSelectedCount { get; }
    internal Button BtnExportSelectedSearch { get; }
    internal Button BtnClearSearchSelection { get; }
    internal DataGridView GridSearchResults => _gridSearchResults;
    internal GridCheckboxSelectionController<WorkOrderSearchRow> SearchSelection => _searchSelection;

    // Tab 2 (Generar / Imprimir OT)
    private readonly TextBox _txtPrintOtNumber;
    private readonly Button _btnLookupPrintOt;
    private readonly ComboBox _cmbProviderPrint;
    private readonly TextBox _txtPrintProvDir;
    private readonly TextBox _txtPrintProvMail;
    private readonly ComboBox _cmbRequesterPrint;
    private readonly TextBox _txtPrintSolNombre;
    private readonly TextBox _txtPrintSolMail;
    private readonly TextBox _txtPrintSolFono;
    private readonly TextBox _txtPrintSolActivo;
    private readonly PictureBox _picPrintSolSig;
    private readonly Label _lblPrintSolSigStatus;
    private readonly Label _lblPrintMetaSummary;
    internal CheckBox ChkSigJefatura { get; }
    internal CheckBox ChkSigTecnico { get; }
    internal CheckBox ChkSigSupervisor { get; }
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
        _cmbRequesterEntry.SelectedIndexChanged += (_, _) => UpdateEntrySignatureHint();
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
        _lblVbsDetail = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(4, 3, 18, 0)
        };

        _chkVbj = new CheckBox
        {
            Text = "VB Jefatura",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Visible = _session.Permisos.VistoBuenoJefatura
        };
        _chkVbj.CheckedChanged += (_, _) => ToggleApproval(ApprovalType.Jefatura, _chkVbj.Checked);
        _lblVbjDetail = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(4, 3, 10, 0)
        };

        _lblAuthorizedApprovers = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Accent,
            Margin = new Padding(0, 1, 0, 0)
        };

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
        _toolTip.SetToolTip(TxtValorNetoEntry, "Valor neto de la orden en pesos chilenos ($ >= 0)");

        ChkEntrySigJefatura = new CheckBox
        {
            Name = "chkEntrySigJefatura",
            Text = "Jefatura",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 4, 6, 2)
        };
        ChkEntrySigTecnico = new CheckBox
        {
            Name = "chkEntrySigTecnico",
            Text = "Técnico (Sol.)",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 4, 6, 2)
        };
        ChkEntrySigSupervisor = new CheckBox
        {
            Name = "chkEntrySigSupervisor",
            Text = "Supervisor",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 4, 2, 2)
        };
        ChkEntrySigJefatura.CheckedChanged += (_, _) => UpdateEntrySignatureHint();
        ChkEntrySigTecnico.CheckedChanged += (_, _) => UpdateEntrySignatureHint();
        ChkEntrySigSupervisor.CheckedChanged += (_, _) => UpdateEntrySignatureHint();

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
                    ContextMenuHelper.Item("Editar ítem", AppIcon.Edit, CambiasoTheme.Primary, EditSelectedEntryDetail),
                    ContextMenuHelper.Item("Quitar ítem", AppIcon.Remove, CambiasoTheme.Danger, RemoveSelectedEntryDetail)
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

        _btnDeleteOt = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar orden de trabajo cargada", width: 118);
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

        _btnExportSearch = CambiasoTheme.CreateButton("Exportar Lista", AppIcon.Download, ButtonTone.Secondary, _toolTip, "Exportar todos los resultados filtrados a Excel, PDF, Word o CSV", width: 138, height: 30);
        _btnExportSearch.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            _btnExportSearch,
            this,
            _ctx.DocumentExportService,
            "SISPRO_OrdenesTrabajo",
            () => DocumentExportService.BuildWorkOrdersReport(_lastSearchResults, BuildSearchFilterSummary()),
            file => _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Órdenes de Trabajo", "EXPORTAR_OT_LISTA", $"Exportados {_lastSearchResults.Count} registros a {Path.GetFileName(file)}."));

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
            "Exportar únicamente las órdenes de trabajo seleccionadas a Excel (.xlsx), CSV (.csv) o PDF (.pdf)", width: 188, height: 30);
        BtnExportSelectedSearch.Enabled = false;
        BtnExportSelectedSearch.Click += (_, _) => ExportSelectedWorkOrders();

        BtnClearSearchSelection = CambiasoTheme.CreateButton("Limpiar selección", AppIcon.Eraser, ButtonTone.Secondary, _toolTip,
            "Desmarcar todas las órdenes de trabajo seleccionadas", width: 168, height: 30);
        BtnClearSearchSelection.Enabled = false;

        _gridSearchResults = new DataGridView { Name = "gridSearchOtResults", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridSearchResults);
        _searchSelection = new GridCheckboxSelectionController<WorkOrderSearchRow>(
            _gridSearchResults,
            r => r.IdOt,
            ChkSelectAllSearchVisible,
            UpdateSearchSelectionState);
        BtnClearSearchSelection.Click += (_, _) => _searchSelection.ClearSelection();

        _gridSearchResults.CellDoubleClick += (_, _) => OpenSelectedSearchOrder();
        _gridSearchResults.CellFormatting += (_, e) =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults, e.RowIndex);
            if (row is null || e.CellStyle is null || e.ColumnIndex < 0 || e.ColumnIndex >= _gridSearchResults.Columns.Count) return;

            string prop = _gridSearchResults.Columns[e.ColumnIndex].DataPropertyName;
            if (string.Equals(prop, nameof(WorkOrderGridRow.VBS), StringComparison.Ordinal))
            {
                e.CellStyle.Font = CambiasoTheme.LabelFont;
                e.CellStyle.ForeColor = row.TieneVbs ? CambiasoTheme.Success : CambiasoTheme.Warning;
            }
            else if (string.Equals(prop, nameof(WorkOrderGridRow.VBJ), StringComparison.Ordinal))
            {
                e.CellStyle.Font = CambiasoTheme.LabelFont;
                e.CellStyle.ForeColor = row.TieneVbj ? CambiasoTheme.Success : CambiasoTheme.Warning;
            }
            else if (string.Equals(prop, nameof(WorkOrderGridRow.Estado), StringComparison.Ordinal))
            {
                e.CellStyle.Font = CambiasoTheme.LabelFont;
                e.CellStyle.ForeColor = row.Estado == WorkOrderStatus.Completada
                    ? CambiasoTheme.Success
                    : (row.Estado == WorkOrderStatus.Anulada ? CambiasoTheme.Danger : CambiasoTheme.Primary);
            }
        };
        ContextMenuHelper.AttachToGrid(_gridSearchResults, idx =>
        {
            WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults, idx);
            if (row is null)
            {
                return [];
            }

            int numOt = row.NumeroOt;
            return
            [
                ContextMenuHelper.Item("Abrir / Editar OT", AppIcon.Edit, CambiasoTheme.Primary, () => OpenOrderByNumber(numOt), enabled: _session.Permisos.IngresoOt),
                ContextMenuHelper.Item("Exportar / Imprimir OT", AppIcon.Print, CambiasoTheme.Accent, () => ShowPrintForOrder(numOt), enabled: _session.Permisos.ImpresionOt),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Eliminar OT", AppIcon.Delete, CambiasoTheme.Danger, () => DeleteSearchOrder(numOt), enabled: _session.Permisos.IngresoOt)
            ];
        });

        _searchPager = new GridPagerBar(100);
        _searchPager.PageChanged += (_, _) => RenderSearchResultsPage();

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

        _cmbRequesterPrint = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 175 };
        _cmbRequesterPrint.SelectedIndexChanged += (_, _) => SyncPrintRequesterInfo();
        _txtPrintSolNombre = new TextBox { ReadOnly = true, Width = 240, BackColor = CambiasoTheme.Surface };
        _txtPrintSolMail = new TextBox { ReadOnly = true, Width = 240, BackColor = CambiasoTheme.Surface };
        _txtPrintSolFono = new TextBox { ReadOnly = true, Width = 150, BackColor = CambiasoTheme.Surface };
        _txtPrintSolActivo = new TextBox { ReadOnly = true, Width = 240, BackColor = CambiasoTheme.Surface };
        _picPrintSolSig = new PictureBox { Size = new Size(124, 56), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
        _lblPrintSolSigStatus = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, ForeColor = CambiasoTheme.TextMuted, Text = "Sin firma" };
        _lblPrintMetaSummary = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Margin = new Padding(12, 8, 4, 2),
            Text = "Valor Neto: $ 0"
        };

        ChkSigJefatura = new CheckBox
        {
            Text = "Jefatura",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(4, 5, 10, 2)
        };
        ChkSigTecnico = new CheckBox
        {
            Text = "Técnico (Solicitante)",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 5, 10, 2)
        };
        ChkSigSupervisor = new CheckBox
        {
            Text = "Supervisor",
            Checked = true,
            AutoSize = true,
            Font = CambiasoTheme.BodyFont,
            Margin = new Padding(0, 5, 4, 2)
        };
        ChkSigJefatura.CheckedChanged += (_, _) => PersistPrintSignatureSelection();
        ChkSigTecnico.CheckedChanged += (_, _) => PersistPrintSignatureSelection();
        ChkSigSupervisor.CheckedChanged += (_, _) => PersistPrintSignatureSelection();
        _toolTip.SetToolTip(ChkSigJefatura, "Incluir firma de Jefatura Mantención al pie de la exportación/impresión");
        _toolTip.SetToolTip(ChkSigTecnico, "Incluir firma del Solicitante como Técnico al pie de la exportación/impresión");
        _toolTip.SetToolTip(ChkSigSupervisor, "Incluir firma de Supervisor Mantención al pie de la exportación/impresión");

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
        SyncPrintProviderInfo();
        SyncPrintRequesterInfo();
        _assetTree.SelectedAssetChanged += (_, _) =>
        {
            if (_printOrder is null)
            {
                _txtPrintSolActivo.Text = _assetTree.CurrentBreadcrumb;
            }
        };
        RefreshNextOtBadge();
        RefreshApprovalStatusBadges();
        _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString(CultureInfo.InvariantCulture);
        RefreshEntryDetailsGrid();
        RefreshPrintDetailsGrid();

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
            if ((e.Kind & DataChangeKind.WorkOrders) != 0 && _tabs.SelectedTab == _tabSearch && _lastSearchResults.Count > 0)
            {
                ExecuteSearchCore(showEmptyMessage: false);
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

    public void OpenSearchTab()
    {
        if (_tabs.TabPages.Contains(_tabSearch))
        {
            _tabs.SelectedTab = _tabSearch;
            _chkPendingVb.Checked = false;
            ExecuteSearch();
        }
    }

    public void OpenSearchPendingVb()
    {
        if (_tabs.TabPages.Contains(_tabSearch))
        {
            _tabs.SelectedTab = _tabSearch;
            _chkPendingVb.Checked = true;
            ExecuteSearch();
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
        SyncPrintRequesterInfo();
        RefreshApprovalStatusBadges();
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 152));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 134));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        GroupBox grpHeader = CambiasoTheme.CreateGroupBox("Datos Generales de la Orden de Trabajo");
        grpHeader.Dock = DockStyle.Fill;

        FlowLayoutPanel row1 = new() { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        row1.Controls.AddRange(
        [
            WrapLabel("N° OT:", _txtNumeroOt),
            _btnLookupOt,
            _btnNextOt,
            WrapLabel("Fecha:", _dtFechaOt)
        ]);

        FlowLayoutPanel row2 = new() { Dock = DockStyle.Top, Height = 36, WrapContents = false, Padding = new Padding(4, 2, 4, 0) };
        row2.Controls.AddRange(
        [
            WrapLabel("Proveedor:", _cmbProviderEntry),
            WrapLabel("Solicitado por:", _cmbRequesterEntry)
        ]);

        FlowLayoutPanel row3 = new() { Dock = DockStyle.Top, Height = 28, WrapContents = false, Padding = new Padding(6, 2, 4, 0) };
        _chkVbs.Margin = new Padding(0, 2, 4, 0);
        _chkVbj.Margin = new Padding(0, 2, 4, 0);
        Label lblVb = CambiasoTheme.CreateFieldLabel("Aprobaciones:");
        lblVb.Margin = new Padding(0, 3, 10, 0);
        row3.Controls.AddRange([lblVb, _chkVbs, _lblVbsDetail, _chkVbj, _lblVbjDetail]);

        FlowLayoutPanel row4 = new() { Dock = DockStyle.Top, Height = 22, WrapContents = false, Padding = new Padding(6, 0, 4, 0) };
        row4.Controls.Add(_lblAuthorizedApprovers);

        grpHeader.Controls.Add(row4);
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

        FlowLayoutPanel detBtns = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(4, 2, 0, 0) };
        detBtns.Controls.Add(_btnAddDetail);
        detBtns.Controls.Add(_btnRemoveDetail);

        detTbl.Controls.Add(cantPanel, 0, 0);
        detTbl.Controls.Add(descPanel, 1, 0);
        detTbl.Controls.Add(detBtns, 2, 0);
        grpDetail.Controls.Add(detTbl);

        // Fila 2: Observación (inmediatamente después de Detalle) + Valor Neto destacado + Firmas al pie
        TableLayoutPanel obsAndMoneySplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        obsAndMoneySplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        obsAndMoneySplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        GroupBox grpObs = CambiasoTheme.CreateGroupBox("Observación de la OT");
        grpObs.Dock = DockStyle.Fill;
        Panel obsPad = new() { Dock = DockStyle.Fill, Padding = new Padding(4, 2, 4, 4) };
        obsPad.Controls.Add(TxtObservacionEntry);
        grpObs.Controls.Add(obsPad);

        GroupBox grpNetAndSig = CambiasoTheme.CreateGroupBox("Valor Neto ($) y Firmas al Pie");
        grpNetAndSig.Dock = DockStyle.Fill;
        TableLayoutPanel netSigTbl = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4, 2, 4, 2) };
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        netSigTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel moneyRow = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        Label lblNetTitle = new()
        {
            Text = "Valor Neto ($):",
            Font = CambiasoTheme.SectionFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            AutoSize = true,
            Margin = new Padding(0, 7, 8, 0)
        };
        moneyRow.Controls.AddRange([lblNetTitle, TxtValorNetoEntry]);

        FlowLayoutPanel sigChecksRow = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        Label lblSigPick = CambiasoTheme.CreateFieldLabel("Firmas:");
        lblSigPick.Margin = new Padding(0, 6, 6, 0);
        sigChecksRow.Controls.AddRange([lblSigPick, ChkEntrySigJefatura, ChkEntrySigTecnico, ChkEntrySigSupervisor]);

        netSigTbl.Controls.Add(moneyRow, 0, 0);
        netSigTbl.Controls.Add(sigChecksRow, 0, 1);
        netSigTbl.Controls.Add(_lblEntrySigHint, 0, 2);
        grpNetAndSig.Controls.Add(netSigTbl);

        obsAndMoneySplit.Controls.Add(grpObs, 0, 0);
        obsAndMoneySplit.Controls.Add(grpNetAndSig, 1, 0);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Ítems de la OT (doble clic para editar)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridEntryDetails);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnSaveOt, _btnSaveAndExportOt, _btnUpdateOt, _btnDeleteOt, _btnClearEntry, _lblEntryStatus]);

        layout.Controls.Add(grpHeader, 0, 0);
        layout.Controls.Add(grpDetail, 0, 1);
        layout.Controls.Add(obsAndMoneySplit, 0, 2);
        layout.Controls.Add(grpGrid, 0, 3);
        layout.Controls.Add(footer, 0, 4);
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

        GroupBox grpResults = CambiasoTheme.CreateGroupBox("Resultados (marque casillas para exportar seleccionadas · doble clic para abrir OT)");
        grpResults.Dock = DockStyle.Fill;

        TableLayoutPanel resultsTbl = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        resultsTbl.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        FlowLayoutPanel selBar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 40),
            BackColor = CambiasoTheme.SubtleFill,
            WrapContents = true,
            Padding = new Padding(6, 4, 6, 4)
        };
        _toolTip.SetToolTip(ChkSelectAllSearchVisible, "Selecciona todos los registros de la página visible; la selección se conserva al ordenar o cambiar de página.");
        _toolTip.SetToolTip(LblSearchSelectedCount, "Cantidad total de órdenes de trabajo marcadas (la selección se conserva al ordenar o cambiar de página).");
        selBar.Controls.AddRange([ChkSelectAllSearchVisible, LblSearchSelectedCount, BtnExportSelectedSearch, BtnClearSearchSelection]);

        resultsTbl.Controls.Add(selBar, 0, 0);
        resultsTbl.Controls.Add(_gridSearchResults, 0, 1);
        resultsTbl.Controls.Add(_searchPager, 0, 2);
        grpResults.Controls.Add(resultsTbl);

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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 224));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        Panel topPanel = new() { Dock = DockStyle.Fill };
        FlowLayoutPanel otBar = new() { Dock = DockStyle.Top, Height = 38, Padding = new Padding(2, 2, 2, 2), WrapContents = false };
        Label lblFirmas = CambiasoTheme.CreateFieldLabel("Firmas al pie:");
        lblFirmas.Margin = new Padding(10, 7, 4, 2);
        _toolTip.SetToolTip(lblFirmas, "Elija qué firmas se mostrarán al pie (desmarque todas para omitir firmas)");
        otBar.Controls.AddRange([WrapLabel("N° OT:", _txtPrintOtNumber), _btnLookupPrintOt, lblFirmas, ChkSigJefatura, ChkSigTecnico, ChkSigSupervisor, _lblPrintMetaSummary]);

        TableLayoutPanel cards = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));

        GroupBox grpProv = CambiasoTheme.CreateGroupBox("Datos del Proveedor");
        grpProv.Dock = DockStyle.Fill;
        TableLayoutPanel provGrid = CreatePrintFieldsGrid(3);
        AddPrintFieldRow(provGrid, 0, "Proveedor:", _cmbProviderPrint);
        AddPrintFieldRow(provGrid, 1, "Dirección:", _txtPrintProvDir);
        AddPrintFieldRow(provGrid, 2, "Correo:", _txtPrintProvMail);
        grpProv.Controls.Add(provGrid);

        GroupBox grpSol = CambiasoTheme.CreateGroupBox("Datos del Solicitante y Firma Técnico");
        grpSol.Dock = DockStyle.Fill;
        TableLayoutPanel solTbl = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        solTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        solTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));

        TableLayoutPanel solGrid = CreatePrintFieldsGrid(5);
        AddPrintFieldRow(solGrid, 0, "Solicitante:", _cmbRequesterPrint);
        AddPrintFieldRow(solGrid, 1, "Nombre:", _txtPrintSolNombre);
        AddPrintFieldRow(solGrid, 2, "Correo:", _txtPrintSolMail);
        AddPrintFieldRow(solGrid, 3, "Teléfono:", _txtPrintSolFono);
        AddPrintFieldRow(solGrid, 4, "Activo:", _txtPrintSolActivo);

        FlowLayoutPanel sigBox = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(6, 4, 2, 2) };
        sigBox.Controls.AddRange([_picPrintSolSig, _lblPrintSolSigStatus]);

        solTbl.Controls.Add(solGrid, 0, 0);
        solTbl.Controls.Add(sigBox, 1, 0);
        grpSol.Controls.Add(solTbl);

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
        SelectOrBindHistoricalProvider(_cmbProviderEntry, order.IdProv);
        if (order.IdSol.HasValue)
        {
            SelectOrBindHistoricalRequester(_cmbRequesterEntry, order.IdSol.Value);
        }

        _dtFechaOt.Value = order.FechaOt;
        TxtObservacionEntry.Text = order.Observacion;
        TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
        TxtValorNetoEntry.Text = $"$ {order.ValorNeto:N0}";
        ApplyEntrySelectedSignature(order.FirmasSeleccionadas);

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
        RefreshApprovalStatusBadges();
        _lblEntryStatus.Text = $"OT N° {order.NumeroOt} cargada (ID #{order.IdOt}) · Neto: $ {order.ValorNeto:N0} · Firmas: {order.FirmasSeleccionadas.FormatResumen()}";
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
            RefreshApprovalStatusBadges();
            _lblEntryStatus.Text = $"Visto Bueno actualizado · VBS: {_loadedOrder?.VbsResumen ?? "Pendiente"} · VBJ: {_loadedOrder?.VbjResumen ?? "Pendiente"}";
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

        if (!WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out decimal valorNeto) || valorNeto < 0m)
        {
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 232, 232);
            TxtValorNetoEntry.Focus();
            throw new ArgumentException("Debe ingresar un Valor Neto numérico válido mayor o igual a cero.");
        }

        TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
        int idProv = _cmbProviderEntry.SelectedValue is int p ? p : 0;
        int? idSol = _cmbRequesterEntry.SelectedValue is int s ? s : null;
        IReadOnlyList<int> pathIds = _assetTree.GetSelectedPathIds();

        WorkOrder order = new()
        {
            NumeroOt = numOt,
            IdProv = idProv,
            IdSol = idSol,
            FechaOt = _dtFechaOt.Value.Date,
            Observacion = TxtObservacionEntry.Text.Trim(),
            ValorNeto = valorNeto,
            FirmasSeleccionadas = GetEntrySelectedSignature(),
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
        Requester? sol = _cmbRequesterEntry?.SelectedItem as Requester;
        string techNote = ChkEntrySigTecnico.Checked && sol is not null
            ? (sol.TieneFirma ? $" · Firma Técnico ({sol.Alias}): OK" : $" · {sol.Alias} sin imagen de firma (solo línea)")
            : string.Empty;
        _lblEntrySigHint.Text = $"Pie OT: {sigText}{techNote}";
        _lblEntrySigHint.ForeColor = sel == SignatureOption.Ninguna ? CambiasoTheme.TextMuted : CambiasoTheme.Primary;
    }

    private void FormatAndValidateNetValueInput()
    {
        if (WorkOrderService.TryParseNetValue(TxtValorNetoEntry.Text, out decimal valor) && valor >= 0m)
        {
            TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
            TxtValorNetoEntry.Text = $"$ {valor:N0}";
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
    }

    private void RefreshApprovalStatusBadges()
    {
        IReadOnlyList<ApproverDirectoryEntry> approvers = _ctx.WorkOrderService.GetApproverDirectory();
        List<string> vbsAll = approvers.Where(a => a.Vbs).Select(a => a.AliasUsuario).ToList();
        List<string> vbjAll = approvers.Where(a => a.Vbj).Select(a => a.AliasUsuario).ToList();
        string vbsAuth = vbsAll.Count == 0 ? "Sin asignar" : (vbsAll.Count <= 2 ? string.Join(", ", vbsAll) : $"{string.Join(", ", vbsAll.Take(2))} +{vbsAll.Count - 2}");
        string vbjAuth = vbjAll.Count == 0 ? "Sin asignar" : (vbjAll.Count <= 2 ? string.Join(", ", vbjAll) : $"{string.Join(", ", vbjAll.Take(2))} +{vbjAll.Count - 2}");

        _lblAuthorizedApprovers.Text =
            $"Aprobadores vigentes — VB Supervisor: {vbsAuth}   |   VB Jefatura: {vbjAuth}";
        _toolTip.SetToolTip(_lblAuthorizedApprovers,
            $"Autorizados VB Supervisor: {string.Join(", ", vbsAll)}\nAutorizados VB Jefatura: {string.Join(", ", vbjAll)}");

        WorkOrderApproval? vbs = _loadedOrder?.VbsApproval;
        if (vbs is not null)
        {
            _lblVbsDetail.Text = $"✓ Aprobado por {vbs.AprobadorDisplay} ({vbs.Fecha:dd-MM-yyyy HH:mm})";
            _lblVbsDetail.ForeColor = CambiasoTheme.Success;
        }
        else
        {
            _lblVbsDetail.Text = vbsAll.Count == 0 ? "Pendiente" : $"Pendiente (Aut.: {vbsAuth})";
            _lblVbsDetail.ForeColor = CambiasoTheme.Warning;
        }
        _toolTip.SetToolTip(_lblVbsDetail, $"Autorizados VBS: {string.Join(", ", vbsAll)}");

        WorkOrderApproval? vbj = _loadedOrder?.VbjApproval;
        if (vbj is not null)
        {
            _lblVbjDetail.Text = $"✓ Aprobado por {vbj.AprobadorDisplay} ({vbj.Fecha:dd-MM-yyyy HH:mm})";
            _lblVbjDetail.ForeColor = CambiasoTheme.Success;
        }
        else
        {
            _lblVbjDetail.Text = vbjAll.Count == 0 ? "Pendiente" : $"Pendiente (Aut.: {vbjAuth})";
            _lblVbjDetail.ForeColor = CambiasoTheme.Warning;
        }
        _toolTip.SetToolTip(_lblVbjDetail, $"Autorizados VBJ: {string.Join(", ", vbjAll)}");
    }

    private void ClearEntryForm()
    {
        _loadedOrder = null;
        _entryDetails.Clear();
        LoadCatalogs();
        _txtNumeroOt.Text = _ctx.WorkOrderService.GetNextWorkOrderNumber().ToString(CultureInfo.InvariantCulture);
        RefreshNextOtBadge();
        _dtFechaOt.Value = DateTime.Today;
        _numCantidad.Value = 1;
        _txtDetalle.Clear();
        TxtObservacionEntry.Clear();
        TxtValorNetoEntry.BackColor = Color.FromArgb(255, 250, 236);
        TxtValorNetoEntry.Text = "$ 0";
        ApplyEntrySelectedSignature(SignatureOption.AmbasConSolicitante);
        _suppressApprovalEvents = true;
        _chkVbs.Checked = false;
        _chkVbj.Checked = false;
        _suppressApprovalEvents = false;
        _btnSaveOt.Enabled = true;
        _btnSaveAndExportOt.Enabled = _session.Permisos.ImpresionOt;
        _btnUpdateOt.Enabled = false;
        _btnDeleteOt.Enabled = false;
        RefreshApprovalStatusBadges();
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
                TextoBusqueda = _chkFilterText.Checked ? _txtSearchKeyword.Text.Trim() : null,
                SoloPendientesVb = _chkPendingVb.Checked
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

        if (_gridSearchResults.Columns.Count >= 13)
        {
            _searchSelection.ConfigureDataColumn(0, "N° OT", 9);
            _searchSelection.ConfigureDataColumn(1, "Fecha", 11, "dd-MM-yyyy");
            _searchSelection.ConfigureDataColumn(2, "Proveedor", 14);
            _searchSelection.ConfigureDataColumn(3, "Solicitante", 10);
            _searchSelection.ConfigureDataColumn(4, "Cant.", 6);
            _searchSelection.ConfigureDataColumn(5, "Detalle", 16);
            _searchSelection.ConfigureDataColumn(6, "Obs.", 8);
            _searchSelection.ConfigureDataColumn(7, "Valor Neto", 10, "$#,##0", DataGridViewContentAlignment.MiddleRight);
            _searchSelection.ConfigureDataColumn(8, "Estado", 7);
            _searchSelection.ConfigureDataColumn(9, "Firmas", 9);
            _searchSelection.ConfigureDataColumn(10, "VBS", 6);
            _searchSelection.ConfigureDataColumn(11, "VBJ", 6);
        }

        _searchSelection.EnsureSelectColumnAndSync();
        UpdateSearchSelectionState();
    }

    private void UpdateSearchSelectionState()
    {
        int count = _searchSelection.SelectedCount;
        LblSearchSelectedCount.Text = $"OT seleccionadas: {count:N0}";
        BtnExportSelectedSearch.Enabled = count > 0;
        BtnClearSearchSelection.Enabled = count > 0;
    }

    private void ExportSelectedWorkOrders()
    {
        IReadOnlyList<int> selectedIds = _searchSelection.SelectedIds;
        if (selectedIds.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Debe seleccionar al menos una orden de trabajo mediante su casilla de verificación.");
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
                $"Exportadas {count} OT(s) seleccionadas a {Path.GetFileName(file)}."));
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
        return $"{_searchDateRange.BuildFilterDescription()} | Proveedor: {prov} | Solicitante: {sol}";
    }

    private void OpenSelectedSearchOrder()
    {
        WorkOrderSearchRow? row = GridBindingHelper.GetRowSource<WorkOrderSearchRow>(_gridSearchResults);
        if (row is not null)
        {
            OpenOrderByNumber(row.NumeroOt);
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
        SelectOrBindHistoricalProvider(_cmbProviderPrint, order.IdProv);
        if (order.IdSol.HasValue)
        {
            SelectOrBindHistoricalRequester(_cmbRequesterPrint, order.IdSol.Value);
        }

        _suppressPrintSignaturePersist = true;
        try
        {
            ChkSigJefatura.Checked = order.FirmasSeleccionadas.IncludeJefatura();
            ChkSigSupervisor.Checked = order.FirmasSeleccionadas.IncludeSupervisor();
            ChkSigTecnico.Checked = order.FirmasSeleccionadas.IncludeTecnico();
        }
        finally
        {
            _suppressPrintSignaturePersist = false;
        }

        SyncPrintProviderInfo();
        SyncPrintRequesterInfo();
        _txtPrintSolNombre.Text = order.SolicitanteDisplayNombre;
        _txtPrintSolActivo.Text = !string.IsNullOrWhiteSpace(order.RutaActivo) ? order.RutaActivo : _assetTree.CurrentBreadcrumb;
        _lblPrintMetaSummary.Text = $"Valor Neto: $ {order.ValorNeto:N0}";
        _toolTip.SetToolTip(_lblPrintMetaSummary,
            string.IsNullOrWhiteSpace(order.Observacion) ? "Sin observaciones adicionales" : $"Observación: {order.Observacion}");
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
            _txtPrintSolNombre.Text = string.IsNullOrWhiteSpace(r.NombreCompleto) ? r.Alias : r.NombreCompleto;
            _txtPrintSolMail.Text = r.Correo;
            _txtPrintSolFono.Text = r.Telefono;
            if (_printOrder is null)
            {
                _txtPrintSolActivo.Text = _assetTree.CurrentBreadcrumb;
            }

            Image? old = _picPrintSolSig.Image;
            if (r.FirmaPreviewPng is { Length: > 0 })
            {
                _picPrintSolSig.Image = SignatureFileService.CreateBitmapFromBytes(r.FirmaPreviewPng);
                _lblPrintSolSigStatus.Text = $"Firma: {r.FirmaBadge}";
                _lblPrintSolSigStatus.ForeColor = CambiasoTheme.Success;
            }
            else
            {
                _picPrintSolSig.Image = IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 24);
                _lblPrintSolSigStatus.Text = "Sin firma";
                _lblPrintSolSigStatus.ForeColor = CambiasoTheme.TextMuted;
            }

            if (old is not null && !ReferenceEquals(old, IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 24)))
            {
                old.Dispose();
            }
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
            IdSol = sol?.IdSol,
            AliasSolicitante = sol?.Alias ?? _printOrder?.AliasSolicitante ?? string.Empty,
            NombreSolicitante = sol is not null
                ? (string.IsNullOrWhiteSpace(sol.NombreCompleto) ? sol.Alias : sol.NombreCompleto)
                : (_printOrder?.NombreSolicitante ?? string.Empty),
            CorreoSolicitante = _txtPrintSolMail.Text,
            TelefonoSolicitante = _txtPrintSolFono.Text,
            FirmaSolicitantePreviewPng = sol?.FirmaPreviewPng ?? _printOrder?.FirmaSolicitantePreviewPng,
            FirmaSolicitanteTipo = !string.IsNullOrWhiteSpace(sol?.FirmaTipo) ? sol.FirmaTipo : (_printOrder?.FirmaSolicitanteTipo ?? string.Empty),
            FirmaSolicitanteNombre = !string.IsNullOrWhiteSpace(sol?.FirmaNombre) ? sol.FirmaNombre : (_printOrder?.FirmaSolicitanteNombre ?? string.Empty),
            Observacion = _printOrder?.Observacion ?? string.Empty,
            ValorNeto = _printOrder?.ValorNeto ?? 0m,
            Estado = _printOrder?.Estado ?? WorkOrderStatus.Abierta,
            FirmasSeleccionadas = GetSelectedSignature(),
            RutaActivo = !string.IsNullOrWhiteSpace(_txtPrintSolActivo.Text) ? _txtPrintSolActivo.Text : (_printOrder?.RutaActivo ?? _assetTree.CurrentBreadcrumb),
            Detalles = [.. _printDetails],
            VistosBuenos = _printOrder?.VistosBuenos ?? []
        };
    }

    internal SignatureOption SelectedSignature => GetSelectedSignature();

    private SignatureOption GetSelectedSignature() =>
        SignatureOptionExtensions.FromFlags(ChkSigJefatura.Checked, ChkSigSupervisor.Checked, ChkSigTecnico.Checked);

    private void PersistPrintSignatureSelection()
    {
        if (_suppressPrintSignaturePersist || _printOrder is null)
        {
            return;
        }

        try
        {
            SignatureOption sel = GetSelectedSignature();
            _ctx.WorkOrderService.UpdateSignatures(_session, _printOrder.IdOt, sel);
            _printOrder.FirmasSeleccionadas = sel;
            if (_loadedOrder is not null && _loadedOrder.IdOt == _printOrder.IdOt)
            {
                _loadedOrder.FirmasSeleccionadas = sel;
                ApplyEntrySelectedSignature(sel);
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

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
        LoadCatalogs();
        _txtPrintOtNumber.Clear();
        _printDetails.Clear();
        _txtPrintWork.Clear();
        _numPrintCantidad.Value = 1;
        _lblPrintMetaSummary.Text = "Valor Neto: $ 0";
        _toolTip.SetToolTip(_lblPrintMetaSummary, "Valor neto de la orden");
        _suppressPrintSignaturePersist = true;
        try
        {
            ChkSigJefatura.Checked = true;
            ChkSigTecnico.Checked = true;
            ChkSigSupervisor.Checked = true;
        }
        finally
        {
            _suppressPrintSignaturePersist = false;
        }

        SyncPrintProviderInfo();
        SyncPrintRequesterInfo();
        RefreshPrintDetailsGrid();
    }

    private static TableLayoutPanel CreatePrintFieldsGrid(int rows)
    {
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = rows + 1,
            Padding = new Padding(4, 2, 4, 2)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int r = 0; r < rows; r++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
        }

        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return grid;
    }

    private static void AddPrintFieldRow(TableLayoutPanel grid, int row, string labelText, Control control)
    {
        Label lbl = CambiasoTheme.CreateFieldLabel(labelText);
        lbl.AutoSize = false;
        lbl.Dock = DockStyle.Fill;
        lbl.TextAlign = ContentAlignment.MiddleLeft;
        lbl.Margin = new Padding(2, 1, 4, 1);

        control.Dock = DockStyle.None;
        control.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        control.Margin = new Padding(0, 2, 4, 2);

        grid.Controls.Add(lbl, 0, row);
        grid.Controls.Add(control, 1, row);
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

            if (_picPrintSolSig.Image is not null &&
                !ReferenceEquals(_picPrintSolSig.Image, IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 24)))
            {
                _picPrintSolSig.Image.Dispose();
                _picPrintSolSig.Image = null;
            }

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
        public string VBS => Source.VbsDisplay;
        public string VBJ => Source.VbjDisplay;
    }
}
