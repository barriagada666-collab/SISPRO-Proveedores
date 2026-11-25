using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo Mantenedores (M3): Proveedores, Solicitantes, Firmas Digitales y Árbol de Activos (10 niveles),
/// con selección múltiple, consulta de OT relacionadas, reasignación y control de permisos Admin.
/// </summary>
public sealed class MaintainersForm : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();
    private readonly bool _isAdmin;

    public event EventHandler? CatalogsChanged;
    public event EventHandler? OpenTreeEditorRequested;
    private bool _syncSubscribed;
    private bool _suppressTreeSync;

    internal TabControl TabControlMain { get; }

    // Proveedores controls
    private readonly TextBox _txtProvSearch;
    private readonly TextBox _txtProvNombre;
    private readonly TextBox _txtProvDir;
    private readonly TextBox _txtProvFono;
    private readonly TextBox _txtProvFax;
    private readonly TextBox _txtProvMail;
    private readonly TextBox _txtProvContacto;
    private readonly TextBox _txtProvMailContacto;
    private readonly TextBox _txtProvObs;
    private readonly CheckBox _chkProvActivo;
    private readonly CheckBox _chkSelectAllProv;
    private readonly Label _lblProvSelectedCount;
    private readonly DataGridView _gridProviders;
    private readonly GridCheckboxSelectionController<Provider> _provSelection;
    private readonly Button _btnProvConsultOt;
    private readonly Button _btnProvReassign;
    private readonly Button _btnProvActivate;
    private readonly Button _btnProvDeactivate;
    private readonly Button _btnProvDelete;
    private readonly Button _btnProvImportMdb;
    private readonly Button _btnProvClearSel;
    private IReadOnlyList<Provider> _providers = [];
    private int _selectedProvId;

    // Solicitantes y Firmas
    private readonly RequesterManagerControl _requestersControl;
    private readonly SignatureManagerControl _signaturesControl;

    // Asset Tree controls
    private readonly AssetTreePanel _assetTree;
    private readonly TextBox _txtAssetName;
    private readonly TextBox _txtAssetSap;
    private readonly TextBox _txtAssetFabricante;
    private readonly TextBox _txtAssetModelo;
    private readonly TextBox _txtAssetSerie;
    private readonly TextBox _txtAssetTipo;
    private readonly TextBox _txtAssetEstadoOp;
    private readonly ComboBox _cmbAssetTipoNodo;
    private readonly TextBox _txtAssetObs;
    private readonly TextBox _txtNewChildName;
    private readonly ComboBox _cmbNewChildTipo;
    private readonly CheckBox _chkAssetActive;
    private readonly Label _lblAssetMeta;
    private readonly Button _btnAssetInspectOt;
    private readonly Button _btnAssetReassign;
    private readonly Button _btnAssetDeactivateSel;
    private readonly Button _btnAssetDeleteSel;

    internal DataGridView GridProviders => _gridProviders;
    internal GridCheckboxSelectionController<Provider> ProviderSelection => _provSelection;
    internal RequesterManagerControl RequestersControl => _requestersControl;
    internal SignatureManagerControl SignaturesControl => _signaturesControl;
    internal AssetTreePanel AssetTreePanelControl => _assetTree;

    public MaintainersForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        _isAdmin = _ctx.MaintainerService.IsAdministrator(_session);

        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        TabControlMain = new TabControl { Dock = DockStyle.Fill, Font = CambiasoTheme.LabelFont, Padding = new Point(14, 6) };

        // --- TAB 1: PROVEEDORES ---
        TabPage tabProv = new("Proveedores") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtProvSearch = new TextBox { Width = 220, PlaceholderText = "Filtrar proveedor por nombre, dirección o correo..." };
        _txtProvSearch.TextChanged += (_, _) => LoadProviders();
        _txtProvNombre = new TextBox { Width = 240 };
        _txtProvDir = new TextBox { Width = 320 };
        _txtProvFono = new TextBox { Width = 140 };
        _txtProvFax = new TextBox { Width = 140 };
        _txtProvMail = new TextBox { Width = 240 };
        _txtProvContacto = new TextBox { Width = 220 };
        _txtProvMailContacto = new TextBox { Width = 240 };
        _txtProvObs = new TextBox { Width = 460 };
        _chkProvActivo = new CheckBox { Text = "Proveedor activo", Checked = true, AutoSize = true };

        _chkSelectAllProv = new CheckBox
        {
            Name = "chkSelectAllProveedores",
            Text = "Seleccionar todos visibles",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            Margin = new Padding(0, 6, 10, 0)
        };
        _lblProvSelectedCount = new Label
        {
            Text = "Seleccionados: 0",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Margin = new Padding(0, 7, 12, 0)
        };

        _btnProvConsultOt = CreateActionBtn("Consultar OT", AppIcon.Search, ButtonTone.Accent, "Consultar órdenes de trabajo asociadas a los proveedores marcados o seleccionados");
        _btnProvConsultOt.Click += (_, _) => InspectProviders();

        _btnProvReassign = CreateActionBtn("Reasignar Rel.", AppIcon.MoveRight, ButtonTone.Secondary, "Reasignar OTs de los proveedores seleccionados hacia otro activo (solo Administrador)");
        _btnProvReassign.Click += (_, _) => InspectProviders();

        _btnProvActivate = CreateActionBtn("Activar Sel.", AppIcon.Power, ButtonTone.Primary, "Activar proveedores seleccionados (solo Administrador)");
        _btnProvActivate.Click += (_, _) => SetCheckedProvidersActive(true);

        _btnProvDeactivate = CreateActionBtn("Desactivar Sel.", AppIcon.Ban, ButtonTone.Secondary, "Verificar OT y desactivar proveedores seleccionados conservando historial (solo Administrador)");
        _btnProvDeactivate.Click += (_, _) => SetCheckedProvidersActive(false);

        _btnProvDelete = CreateActionBtn("Eliminar", AppIcon.Delete, ButtonTone.Danger, "Verificar dependencias y eliminar proveedores compatibles (solo Administrador)");
        _btnProvDelete.Click += (_, _) => DeleteProvider();

        _btnProvImportMdb = CreateActionBtn("Importar MDB", AppIcon.Upload, ButtonTone.Secondary, "Comparar e importar proveedores desde base Access (.mdb) (solo Administrador)");
        _btnProvImportMdb.Visible = _isAdmin;
        _btnProvImportMdb.Enabled = _isAdmin;
        _btnProvImportMdb.Click += (_, _) => OpenMdbImportDialog();

        _btnProvClearSel = CreateActionBtn("Limpiar Sel.", AppIcon.Eraser, ButtonTone.Secondary, "Limpiar casillas marcadas");

        _gridProviders = new DataGridView { Name = "gridProveedores", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridProviders);
        _provSelection = new GridCheckboxSelectionController<Provider>(
            _gridProviders,
            p => p.IdProv,
            _chkSelectAllProv,
            UpdateProviderActionStates);
        _btnProvClearSel.Click += (_, _) => _provSelection.ClearSelection();

        _gridProviders.CellClick += (_, _) => SelectProviderFromGrid();
        _gridProviders.SelectionChanged += (_, _) => UpdateProviderActionStates();
        _gridProviders.CellFormatting += OnProviderCellFormatting;

        ContextMenuHelper.AttachToGrid(_gridProviders, idx =>
        {
            Provider? prov = GridBindingHelper.GetRowSource<Provider>(_gridProviders, idx);
            if (prov is null)
            {
                return [];
            }

            List<int> targets = _provSelection.SelectedCount > 0 ? _provSelection.SelectedIds.ToList() : [prov.IdProv];
            return
            [
                ContextMenuHelper.Item("Editar", AppIcon.Edit, CambiasoTheme.Primary, () => { SelectProviderFromGrid(idx); _txtProvNombre.Focus(); }),
                ContextMenuHelper.Item("Consultar OT asociadas…", AppIcon.Search, CambiasoTheme.Accent, () => RunProviderInspect(targets)),
                ContextMenuHelper.Item("Reasignar relaciones en OT…", AppIcon.MoveRight, CambiasoTheme.Primary, () => RunProviderInspect(targets), enabled: _isAdmin),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Activar", AppIcon.Power, CambiasoTheme.Success, () => SetProviderActive(prov, true), enabled: _isAdmin && !prov.Activo),
                ContextMenuHelper.Item("Desactivar / Verificar OT…", AppIcon.Ban, CambiasoTheme.Warning, () => RunProviderDeactivate(targets), enabled: _isAdmin && (prov.Activo || targets.Count > 1)),
                ContextMenuHelper.Item("Eliminar / Verificar OT…", AppIcon.Delete, CambiasoTheme.Danger, () => RunProviderDelete(targets), enabled: _isAdmin),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Nuevo", AppIcon.Add, CambiasoTheme.Accent, () => { ClearProviderForm(); _txtProvNombre.Focus(); })
            ];
        });
        BuildProvidersTab(tabProv);

        // --- TAB 2: SOLICITANTES ---
        TabPage tabSol = new("Solicitantes") { BackColor = CambiasoTheme.Surface, Padding = new Padding(6) };
        _requestersControl = new RequesterManagerControl(_ctx, _session) { Dock = DockStyle.Fill };
        tabSol.Controls.Add(_requestersControl);

        // --- TAB 3: FIRMAS DIGITALES ---
        TabPage tabFirmas = new("Firmas") { BackColor = CambiasoTheme.Surface, Padding = new Padding(6) };
        _signaturesControl = new SignatureManagerControl(_ctx, _session) { Dock = DockStyle.Fill };
        tabFirmas.Controls.Add(_signaturesControl);

        _requestersControl.RequestersChanged += (_, _) =>
        {
            _signaturesControl.Reload();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        };
        _signaturesControl.SignaturesChanged += (_, _) =>
        {
            _requestersControl.ReloadRequesters();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        };

        // --- TAB 4: ÁRBOL DE ACTIVOS ---
        TabPage tabAssets = new("Árbol de Activos") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _assetTree = new AssetTreePanel(
            _ctx.MaintainerService,
            _ctx.AssetTreeService,
            _session,
            showCheckBoxes: true,
            includeInactive: true)
        {
            Dock = DockStyle.Fill
        };
        bool canEditTree = _assetTree.CanEdit;
        _txtAssetName = new TextBox { Dock = DockStyle.Fill, ReadOnly = !canEditTree };
        _txtAssetSap = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Código SAP", ReadOnly = !canEditTree };
        _txtAssetFabricante = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Fabricante / Marca", ReadOnly = !canEditTree };
        _txtAssetModelo = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Modelo", ReadOnly = !canEditTree };
        _txtAssetSerie = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "N° Serie", ReadOnly = !canEditTree };
        _txtAssetTipo = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Tipo de equipo", ReadOnly = !canEditTree };
        _txtAssetEstadoOp = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Ej. Operativa / No Operativa", ReadOnly = !canEditTree };
        _cmbAssetTipoNodo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = canEditTree };
        foreach ((string code, string label) in AssetNodeTypes.All)
        {
            _cmbAssetTipoNodo.Items.Add(new AssetTypeItem(code, label));
        }
        _txtAssetObs = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Observaciones técnicas del activo...", ReadOnly = !canEditTree };
        _txtNewChildName = new TextBox { Width = 280, PlaceholderText = "Nombre del nuevo subnivel...", Enabled = canEditTree };
        _cmbNewChildTipo = new ComboBox { Width = 148, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = canEditTree };
        foreach ((string code, string label) in AssetNodeTypes.All)
        {
            _cmbNewChildTipo.Items.Add(new AssetTypeItem(code, label));
        }
        _chkAssetActive = new CheckBox { Text = "Nodo activo en árbol", Checked = true, AutoSize = true, Enabled = canEditTree };
        _lblAssetMeta = new Label { AutoSize = true, Font = CambiasoTheme.LabelFont, ForeColor = CambiasoTheme.Primary };

        _btnAssetInspectOt = CreateActionBtn("Consultar OT", AppIcon.Search, ButtonTone.Accent, "Consultar OTs asociadas a los activos marcados o seleccionados");
        _btnAssetInspectOt.Click += (_, _) => ManageAssetDependencies(GetAssetTargetIds());

        _btnAssetReassign = CreateActionBtn("Reasignar Rel.", AppIcon.MoveRight, ButtonTone.Secondary, "Reasignar OTs, proveedor o subnodos dependientes (solo Administrador)");
        _btnAssetReassign.Visible = canEditTree;
        _btnAssetReassign.Click += (_, _) => ManageAssetDependencies(GetAssetTargetIds());

        _btnAssetDeactivateSel = CreateActionBtn("Desactivar Sel.", AppIcon.Ban, ButtonTone.Secondary, "Verificar OTs y desactivar nodos seleccionados conservando historial (solo Administrador)");
        _btnAssetDeactivateSel.Visible = canEditTree;
        _btnAssetDeactivateSel.Click += (_, _) => DeactivateAssetTargets();

        _btnAssetDeleteSel = CreateActionBtn("Eliminar Sel.", AppIcon.Delete, ButtonTone.Danger, "Verificar dependencias y eliminar nodos compatibles (solo Administrador)");
        _btnAssetDeleteSel.Visible = canEditTree;
        _btnAssetDeleteSel.Click += (_, _) => DeleteAssetTargets();

        _assetTree.SelectedAssetChanged += (_, node) => { BindSelectedAsset(node); UpdateAssetBatchButtonsState(); };
        _assetTree.CheckedAssetsChanged += (_, _) => UpdateAssetBatchButtonsState();
        _assetTree.InspectDependenciesRequested += (_, ids) => ManageAssetDependencies(ids);
        _assetTree.TreeModified += (_, _) =>
        {
            BindSelectedAsset(_assetTree.SelectedAsset);
            UpdateAssetBatchButtonsState();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        };
        BuildAssetsTab(tabAssets);

        TabControlMain.TabPages.AddRange([tabProv, tabSol, tabFirmas, tabAssets]);
        Controls.Add(TabControlMain);

        LoadProviders();
        BindSelectedAsset(_assetTree.SelectedAsset);
        UpdateProviderActionStates();
        UpdateAssetBatchButtonsState();

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;
    }

    private Button CreateActionBtn(string text, AppIcon icon, ButtonTone tone, string tooltip)
    {
        Button btn = CambiasoTheme.CreateButton(text, icon, tone, _toolTip, tooltip, width: 92, height: 30);
        btn.AutoSize = true;
        btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btn.MinimumSize = new Size(0, 30);
        btn.Padding = new Padding(6, 0, 8, 0);
        btn.Margin = new Padding(0, 2, 5, 2);
        return btn;
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || _suppressTreeSync)
        {
            return;
        }

        if ((e.Kind & (DataChangeKind.Catalogs | DataChangeKind.WorkOrders)) == 0)
        {
            return;
        }

        void RefreshCatalogs()
        {
            if (IsDisposed) return;
            LoadProviders();
            if ((e.Kind & DataChangeKind.Catalogs) != 0)
            {
                _assetTree.ReloadPreservingSelection();
                BindSelectedAsset(_assetTree.SelectedAsset);
                UpdateAssetBatchButtonsState();
            }
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshCatalogs);
        else if (!InvokeRequired) RefreshCatalogs();
    }

    private void BuildProvidersTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 186));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        GroupBox grpForm = CambiasoTheme.CreateGroupBox("Ficha de Proveedor");
        grpForm.Dock = DockStyle.Fill;
        FlowLayoutPanel f = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        FlowLayoutPanel r1 = new() { AutoSize = true, WrapContents = false };
        r1.Controls.AddRange([Wrap("Nombre:", _txtProvNombre), Wrap("Dirección:", _txtProvDir), _chkProvActivo]);
        FlowLayoutPanel r2 = new() { AutoSize = true, WrapContents = false };
        r2.Controls.AddRange([Wrap("Teléfono:", _txtProvFono), Wrap("Fax:", _txtProvFax), Wrap("Correo:", _txtProvMail)]);
        FlowLayoutPanel r3 = new() { AutoSize = true, WrapContents = false };
        r3.Controls.AddRange([Wrap("Contacto:", _txtProvContacto), Wrap("Correo Contacto:", _txtProvMailContacto)]);
        FlowLayoutPanel r4 = new() { AutoSize = true, WrapContents = false };
        r4.Controls.Add(Wrap("Observaciones:", _txtProvObs));
        f.Controls.AddRange([r1, r2, r3, r4]);
        grpForm.Controls.Add(f);

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(0, 4, 0, 2) };
        Button btnSave = CreateActionBtn("Guardar", AppIcon.Save, ButtonTone.Primary, "Guardar proveedor nuevo o modificado");
        btnSave.Click += (_, _) => SaveProvider();
        Button btnClear = CreateActionBtn("Nuevo / Limpiar", AppIcon.Eraser, ButtonTone.Secondary, "Limpiar ficha para nuevo proveedor");
        btnClear.Click += (_, _) => ClearProviderForm();
        Button btnExcel = CreateActionBtn("Exportar", AppIcon.Download, ButtonTone.Accent, "Exportar catálogo de proveedores a Excel, PDF, Word o CSV");
        btnExcel.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            btnExcel,
            this,
            _ctx.DocumentExportService,
            "SISPRO_Proveedores",
            () => DocumentExportService.BuildProvidersReport(_providers, string.IsNullOrWhiteSpace(_txtProvSearch.Text) ? "Catálogo completo" : $"Filtro: {_txtProvSearch.Text}"));

        bar.Controls.AddRange([btnSave, btnClear, _btnProvConsultOt, _btnProvReassign, _btnProvActivate, _btnProvDeactivate, _btnProvDelete, btnExcel, _btnProvImportMdb]);

        FlowLayoutPanel filterBar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 2) };
        filterBar.Controls.AddRange([_chkSelectAllProv, _lblProvSelectedCount, _btnProvClearSel, Wrap("Buscar:", _txtProvSearch)]);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Catálogo de Proveedores (Marque casillas para consultar OTs o acciones masivas)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridProviders);

        layout.Controls.Add(grpForm, 0, 0);
        layout.Controls.Add(bar, 0, 1);
        layout.Controls.Add(filterBar, 0, 2);
        layout.Controls.Add(grpGrid, 0, 3);
        page.Controls.Add(layout);
    }

    private void BuildAssetsTab(TabPage page)
    {
        bool canEdit = _assetTree.CanEdit;
        SplitContainer split = new() { Size = new Size(1080, 680), Dock = DockStyle.Fill, Panel1MinSize = 320, SplitterDistance = 380 };
        split.Panel1.Controls.Add(_assetTree);

        Panel right = new() { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = CambiasoTheme.Surface };

        GroupBox grpEdit = CambiasoTheme.CreateGroupBox("Ficha Técnica del Activo Seleccionado");
        grpEdit.Dock = DockStyle.Top;
        grpEdit.Height = 376;

        TableLayoutPanel techGrid = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7, Padding = new Padding(6) };
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 6; r++) techGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        techGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

        techGrid.Controls.Add(_lblAssetMeta, 0, 0);
        techGrid.SetColumnSpan(_lblAssetMeta, 2);
        techGrid.Controls.Add(_chkAssetActive, 2, 0);
        techGrid.SetColumnSpan(_chkAssetActive, 2);

        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Descripción:"), 0, 1);
        techGrid.Controls.Add(_txtAssetName, 1, 1);
        techGrid.SetColumnSpan(_txtAssetName, 3);

        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Código SAP:"), 0, 2);
        techGrid.Controls.Add(_txtAssetSap, 1, 2);
        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Estado Operativo:"), 2, 2);
        techGrid.Controls.Add(_txtAssetEstadoOp, 3, 2);

        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Fabricante:"), 0, 3);
        techGrid.Controls.Add(_txtAssetFabricante, 1, 3);
        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Modelo:"), 2, 3);
        techGrid.Controls.Add(_txtAssetModelo, 3, 3);

        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("N° Serie:"), 0, 4);
        techGrid.Controls.Add(_txtAssetSerie, 1, 4);
        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Tipo Eq. / Nodo:"), 2, 4);
        TableLayoutPanel tipoCat = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        tipoCat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tipoCat.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        tipoCat.Controls.Add(_txtAssetTipo, 0, 0);
        tipoCat.Controls.Add(_cmbAssetTipoNodo, 1, 0);
        techGrid.Controls.Add(tipoCat, 3, 4);

        techGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Observación:"), 0, 5);
        techGrid.Controls.Add(_txtAssetObs, 1, 5);
        techGrid.SetColumnSpan(_txtAssetObs, 3);

        FlowLayoutPanel editButtons = new() { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 4, 0, 0) };
        Button btnUpdateNode = CreateActionBtn("Guardar Ficha del Activo", AppIcon.Cpu, ButtonTone.Primary, "Guardar descripción, estado y ficha técnica del activo");
        btnUpdateNode.Visible = canEdit;
        btnUpdateNode.Click += (_, _) => SaveSelectedAsset();

        Button btnOpenTreeEditor = CreateActionBtn("Editor de Árbol", AppIcon.Tree, ButtonTone.Accent, "Abrir módulo completo de edición estructural del árbol (solo Administrador)");
        btnOpenTreeEditor.Visible = canEdit;
        btnOpenTreeEditor.Click += (_, _) => OpenTreeEditorRequested?.Invoke(this, EventArgs.Empty);

        Label lblReadOnlyHint = new()
        {
            Text = "Solo lectura — la edición/eliminación del árbol es exclusiva del perfil Administrador.",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Warning,
            AutoSize = true,
            Visible = !canEdit,
            Margin = new Padding(4, 8, 4, 0)
        };

        editButtons.Controls.AddRange([btnUpdateNode, btnOpenTreeEditor, _btnAssetInspectOt, _btnAssetReassign, _btnAssetDeactivateSel, _btnAssetDeleteSel, lblReadOnlyHint]);
        techGrid.Controls.Add(editButtons, 0, 6);
        techGrid.SetColumnSpan(editButtons, 4);
        grpEdit.Controls.Add(techGrid);

        GroupBox grpAdd = CambiasoTheme.CreateGroupBox("Agregar Subnivel (Hijo)");
        grpAdd.Dock = DockStyle.Top;
        grpAdd.Height = 122;
        grpAdd.Visible = canEdit;
        FlowLayoutPanel addFlow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        FlowLayoutPanel addInputs = new() { AutoSize = true, WrapContents = false };
        addInputs.Controls.AddRange([Wrap("Nuevo Activo:", _txtNewChildName), Wrap("Tipo:", _cmbNewChildTipo)]);
        Button btnAddChild = CreateActionBtn("Agregar Hijo", AppIcon.Add, ButtonTone.Accent, "Crear un nuevo nodo hijo bajo el nodo seleccionado");
        btnAddChild.Visible = canEdit;
        btnAddChild.Click += (_, _) => AddAssetChild();
        addFlow.Controls.AddRange([addInputs, btnAddChild]);
        grpAdd.Controls.Add(addFlow);

        right.Controls.Add(grpAdd);
        right.Controls.Add(grpEdit);
        split.Panel2.Controls.Add(right);
        page.Controls.Add(split);
    }

    private void LoadProviders()
    {
        _providers = _ctx.MaintainerService.GetProviders(includeInactive: true, search: _txtProvSearch.Text);
        _gridProviders.DataSource = new SortableBindingList<ProviderGridRow>(
            _providers.Select(p => new ProviderGridRow(p)));

        if (_gridProviders.Columns.Count >= 8)
        {
            _provSelection.ConfigureDataColumn(0, "ID", 6);
            _provSelection.ConfigureDataColumn(1, "Proveedor", 20);
            _provSelection.ConfigureDataColumn(2, "Dirección", 25);
            _provSelection.ConfigureDataColumn(3, "Teléfono", 11);
            _provSelection.ConfigureDataColumn(4, "Correo", 18);
            _provSelection.ConfigureDataColumn(5, "Contacto", 11);
            _provSelection.ConfigureDataColumn(6, "Estado", 9);
        }

        _provSelection.EnsureSelectColumnAndSync();
        UpdateProviderActionStates();
    }

    private void OnProviderCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        Provider? p = GridBindingHelper.GetRowSource<Provider>(_gridProviders, e.RowIndex);
        if (p is null || e.CellStyle is null || e.ColumnIndex < 0 || e.ColumnIndex >= _gridProviders.Columns.Count)
        {
            return;
        }

        if (string.Equals(_gridProviders.Columns[e.ColumnIndex].DataPropertyName, nameof(ProviderGridRow.Estado), StringComparison.Ordinal))
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = p.Activo ? CambiasoTheme.Success : CambiasoTheme.Danger;
        }
    }

    private IReadOnlyCollection<int> GetProviderTargetIds()
    {
        if (_provSelection.SelectedCount > 0)
        {
            return _provSelection.SelectedIds;
        }

        return _selectedProvId > 0 ? [_selectedProvId] : [];
    }

    private void UpdateProviderActionStates()
    {
        int checkedCount = _provSelection.SelectedCount;
        bool hasAnyTarget = checkedCount > 0 || _selectedProvId > 0;
        _lblProvSelectedCount.Text = $"Seleccionados: {checkedCount}";

        _btnProvConsultOt.Enabled = hasAnyTarget;
        _btnProvReassign.Visible = _isAdmin;
        _btnProvActivate.Visible = _isAdmin;
        _btnProvDeactivate.Visible = _isAdmin;
        _btnProvDelete.Visible = _isAdmin;

        _btnProvReassign.Enabled = _isAdmin && hasAnyTarget;
        _btnProvActivate.Enabled = _isAdmin && hasAnyTarget;
        _btnProvDeactivate.Enabled = _isAdmin && hasAnyTarget;
        _btnProvDelete.Enabled = _isAdmin && hasAnyTarget;
        _btnProvClearSel.Enabled = checkedCount > 0;
    }

    private void InspectProviders() => RunProviderInspect(GetProviderTargetIds());

    private void RunProviderInspect(IReadOnlyCollection<int> targets)
    {
        if (MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Proveedor, targets, OnProvidersBatchUpdated))
        {
            _provSelection.ClearSelection();
        }
    }

    private void SetCheckedProvidersActive(bool active)
    {
        IReadOnlyCollection<int> targets = GetProviderTargetIds();
        if (targets.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione o marque al menos un proveedor.");
            return;
        }

        if (!active)
        {
            RunProviderDeactivate(targets);
            return;
        }

        try
        {
            MaintainerBatchResult res = _ctx.MaintainerService.SetProvidersActiveBatch(_session, targets, true);
            CambiasoTheme.ShowInfo(this, res.BuildSummary());
            _provSelection.ClearSelection();
            LoadProviders();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void RunProviderDeactivate(IReadOnlyCollection<int> targets)
    {
        if (MaintainerBatchUiHelper.DeactivateChecked(this, _ctx, _session, MaintainerEntityKind.Proveedor, targets, OnProvidersBatchUpdated))
        {
            _provSelection.ClearSelection();
        }
    }

    private void DeleteProvider() => RunProviderDelete(GetProviderTargetIds());

    private void RunProviderDelete(IReadOnlyCollection<int> targets)
    {
        if (MaintainerBatchUiHelper.DeleteChecked(this, _ctx, _session, MaintainerEntityKind.Proveedor, targets, OnProvidersBatchUpdated))
        {
            _provSelection.ClearSelection();
        }
    }

    private void OnProvidersBatchUpdated()
    {
        ClearProviderForm();
        LoadProviders();
        CatalogsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SelectProviderFromGrid(int? rowIndex = null)
    {
        Provider? p = GridBindingHelper.GetRowSource<Provider>(_gridProviders, rowIndex);
        if (p is null)
        {
            return;
        }

        _selectedProvId = p.IdProv;
        _txtProvNombre.Text = p.Nombre;
        _txtProvDir.Text = p.Direccion;
        _txtProvFono.Text = p.Telefono;
        _txtProvFax.Text = p.Fax;
        _txtProvMail.Text = p.Correo;
        _txtProvContacto.Text = p.Contacto;
        _txtProvMailContacto.Text = p.CorreoContacto;
        _txtProvObs.Text = p.Observaciones;
        _chkProvActivo.Checked = p.Activo;
        UpdateProviderActionStates();
    }

    private void SaveProvider()
    {
        try
        {
            Provider p = new()
            {
                IdProv = _selectedProvId,
                Nombre = _txtProvNombre.Text,
                Direccion = _txtProvDir.Text,
                Telefono = _txtProvFono.Text,
                Fax = _txtProvFax.Text,
                Correo = _txtProvMail.Text,
                Contacto = _txtProvContacto.Text,
                CorreoContacto = _txtProvMailContacto.Text,
                Observaciones = _txtProvObs.Text,
                Activo = _chkProvActivo.Checked
            };

            _ctx.MaintainerService.SaveProvider(_session, p);
            CambiasoTheme.ShowInfo(this, $"Proveedor '{p.Nombre}' guardado correctamente.");
            ClearProviderForm();
            LoadProviders();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void SetProviderActive(Provider p, bool active)
    {
        try
        {
            _ctx.MaintainerService.SetProviderActive(_session, p.IdProv, active);
            LoadProviders();
            if (_selectedProvId == p.IdProv)
            {
                _chkProvActivo.Checked = active;
            }
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ClearProviderForm()
    {
        _selectedProvId = 0;
        _txtProvNombre.Clear();
        _txtProvDir.Clear();
        _txtProvFono.Clear();
        _txtProvFax.Clear();
        _txtProvMail.Clear();
        _txtProvContacto.Clear();
        _txtProvMailContacto.Clear();
        _txtProvObs.Clear();
        _chkProvActivo.Checked = true;
        UpdateProviderActionStates();
    }

    private IReadOnlyList<int> GetAssetTargetIds()
    {
        if (_assetTree.CheckedAssetIds.Count > 0)
        {
            return _assetTree.CheckedAssetIds;
        }

        return _assetTree.SelectedAsset is not null ? [_assetTree.SelectedAsset.IdActivo] : [];
    }

    private void UpdateAssetBatchButtonsState()
    {
        bool hasTarget = _assetTree.CheckedAssetIds.Count > 0 || _assetTree.SelectedAsset is not null;
        _btnAssetInspectOt.Enabled = hasTarget;
        _btnAssetReassign.Enabled = _assetTree.CanEdit && hasTarget;
        _btnAssetDeactivateSel.Enabled = _assetTree.CanEdit && hasTarget;
        _btnAssetDeleteSel.Enabled = _assetTree.CanEdit && hasTarget;
    }

    private void ManageAssetDependencies(IReadOnlyCollection<int> ids)
    {
        if (MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Activo, ids, RefreshAssetTreeAfterBatch))
        {
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DeactivateAssetTargets()
    {
        if (MaintainerBatchUiHelper.DeactivateChecked(this, _ctx, _session, MaintainerEntityKind.Activo, GetAssetTargetIds(), RefreshAssetTreeAfterBatch))
        {
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DeleteAssetTargets()
    {
        if (MaintainerBatchUiHelper.DeleteChecked(this, _ctx, _session, MaintainerEntityKind.Activo, GetAssetTargetIds(), RefreshAssetTreeAfterBatch))
        {
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshAssetTreeAfterBatch()
    {
        _assetTree.ClearChecked();
        _assetTree.ReloadPreservingSelection();
        BindSelectedAsset(_assetTree.SelectedAsset);
        UpdateAssetBatchButtonsState();
    }

    private void BindSelectedAsset(AssetNode? node)
    {
        if (node is null)
        {
            _lblAssetMeta.Text = "Seleccione un nodo del árbol.";
            _txtAssetName.Clear();
            _txtAssetSap.Clear();
            _txtAssetFabricante.Clear();
            _txtAssetModelo.Clear();
            _txtAssetSerie.Clear();
            _txtAssetTipo.Clear();
            _txtAssetEstadoOp.Clear();
            SelectNodeTypeCombo(_cmbAssetTipoNodo, AssetNodeTypes.SinClasificar);
            SelectNodeTypeCombo(_cmbNewChildTipo, AssetNodeTypes.SinClasificar);
            _txtAssetObs.Clear();
            return;
        }

        IReadOnlyList<AssetNode> path = _ctx.MaintainerService.GetAssetPath(node.IdActivo);
        int realDepth = Math.Max(1, path.Count);
        _lblAssetMeta.Text = $"ID #{node.IdActivo}  ·  Nivel {realDepth} de 10";
        _txtAssetName.Text = node.Descripcion;
        _txtAssetSap.Text = node.CodSap;
        _txtAssetFabricante.Text = node.Fabricante;
        _txtAssetModelo.Text = node.Modelo;
        _txtAssetSerie.Text = node.Serie;
        _txtAssetTipo.Text = node.TipoEquipo;
        _txtAssetEstadoOp.Text = node.EstadoOperativo;
        SelectNodeTypeCombo(_cmbAssetTipoNodo, AssetNodeTypes.Normalize(node.Categoria));
        SelectNodeTypeCombo(_cmbNewChildTipo, AssetNodeTypes.SuggestChildType(node.Categoria));
        _txtAssetObs.Text = node.Observacion;
        _chkAssetActive.Checked = node.Activo;
    }

    private static void SelectNodeTypeCombo(ComboBox combo, string code)
    {
        string normalized = AssetNodeTypes.Normalize(code);
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is AssetTypeItem item && string.Equals(item.Code, normalized, StringComparison.Ordinal))
            {
                combo.SelectedIndex = i;
                return;
            }
        }

        if (combo.Items.Count > 0)
        {
            combo.SelectedIndex = combo.Items.Count - 1;
        }
    }

    private void SaveSelectedAsset()
    {
        AssetNode? node = _assetTree.SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo del árbol.");
            return;
        }

        _suppressTreeSync = true;
        try
        {
            bool targetActive = _chkAssetActive.Checked;
            bool activeChanged = node.Activo != targetActive;

            node.Descripcion = _txtAssetName.Text;
            node.CodSap = _txtAssetSap.Text;
            node.Fabricante = _txtAssetFabricante.Text;
            node.Modelo = _txtAssetModelo.Text;
            node.Serie = _txtAssetSerie.Text;
            node.TipoEquipo = _txtAssetTipo.Text;
            node.EstadoOperativo = _txtAssetEstadoOp.Text;
            node.Categoria = (_cmbAssetTipoNodo.SelectedItem as AssetTypeItem)?.Code ?? AssetNodeTypes.SinClasificar;
            node.Observacion = _txtAssetObs.Text;

            _ctx.AssetTreeService.UpdateNode(_session, node);
            if (activeChanged)
            {
                _ctx.AssetTreeService.SetActive(_session, node.IdActivo, targetActive, includeDescendants: true);
            }

            CambiasoTheme.ShowInfo(this, "Ficha técnica del activo actualizada correctamente.");
            _assetTree.ReloadPreservingSelection();
            BindSelectedAsset(_assetTree.SelectedAsset);
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
        finally
        {
            _suppressTreeSync = false;
        }
    }

    private void AddAssetChild()
    {
        AssetNode? node = _assetTree.SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo padre en el árbol.");
            return;
        }

        _suppressTreeSync = true;
        try
        {
            string childType = (_cmbNewChildTipo.SelectedItem as AssetTypeItem)?.Code ?? AssetNodeTypes.SinClasificar;
            int newId = _ctx.AssetTreeService.AddChild(_session, node.IdActivo, _txtNewChildName.Text, childType);
            _txtNewChildName.Clear();
            _assetTree.RefreshSelectedNode();
            CambiasoTheme.ShowInfo(this, $"Subnivel agregado correctamente (ID #{newId}).");
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
        finally
        {
            _suppressTreeSync = false;
        }
    }

    private void OpenMdbImportDialog()
    {
        if (!_isAdmin)
        {
            CambiasoTheme.ShowWarning(this, "La importación desde bases Access (.mdb) requiere perfil Administrador.");
            return;
        }

        using MdbImportDialog dlg = new(_ctx, _session);
        dlg.ShowDialog(FindForm());
        if (dlg.LastResult is not null)
        {
            LoadProviders();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static FlowLayoutPanel Wrap(string label, Control ctl)
    {
        FlowLayoutPanel p = new() { AutoSize = true, Margin = new Padding(4, 2, 10, 2), WrapContents = false };
        Label lbl = CambiasoTheme.CreateFieldLabel(label);
        lbl.Margin = new Padding(0, 5, 6, 0);
        p.Controls.Add(lbl);
        p.Controls.Add(ctl);
        return p;
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

    private sealed record AssetTypeItem(string Code, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class ProviderGridRow(Provider source) : IGridRowSource<Provider>
    {
        [Browsable(false)]
        public Provider Source { get; } = source;
        public int IdProv => Source.IdProv;
        public string Proveedor => Source.Nombre;
        public string Direccion => Source.Direccion;
        public string Telefono => Source.Telefono;
        public string Correo => Source.Correo;
        public string Contacto => Source.Contacto;
        public string Estado => Source.Activo ? "Activo" : "Inactivo";
    }
}
