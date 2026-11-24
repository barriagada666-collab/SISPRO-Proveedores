using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo Mantenedores (M3): Proveedores, Solicitantes y Árbol de Activos (10 niveles).
/// Implementa de forma completa lo que en VB6 estaba incompleto/comentado (frm_prov.frm + BD MANTENCION).
/// </summary>
public sealed class MaintainersForm : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    public event EventHandler? CatalogsChanged;
    public event EventHandler? OpenTreeEditorRequested;
    private bool _syncSubscribed;
    private bool _suppressTreeSync;

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
    private readonly DataGridView _gridProviders;
    private IReadOnlyList<Provider> _providers = [];
    private int _selectedProvId;

    // Solicitantes control unificado
    private readonly RequesterManagerControl _requestersControl;

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

    public MaintainersForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        TabControl tabs = new() { Dock = DockStyle.Fill, Font = CambiasoTheme.LabelFont, Padding = new Point(14, 6) };

        // --- TAB PROVEEDORES ---
        TabPage tabProv = new("Proveedores") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtProvSearch = new TextBox { Width = 240, PlaceholderText = "Filtrar proveedor por nombre, dirección o correo..." };
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
        _gridProviders = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridProviders);
        _gridProviders.CellClick += (_, _) => SelectProviderFromGrid();
        ContextMenuHelper.AttachToGrid(_gridProviders, idx =>
        {
            Provider? prov = GridBindingHelper.GetRowSource<Provider>(_gridProviders, idx);
            if (prov is null)
            {
                return [];
            }

            return
            [
                ContextMenuHelper.Item("Editar", AppIcon.Edit, CambiasoTheme.Primary, () => { SelectProviderFromGrid(idx); _txtProvNombre.Focus(); }),
                ContextMenuHelper.Item("Activar", AppIcon.Power, CambiasoTheme.Success, () => SetProviderActive(prov, true), enabled: !prov.Activo),
                ContextMenuHelper.Item("Desactivar", AppIcon.Ban, CambiasoTheme.Warning, () => SetProviderActive(prov, false), enabled: prov.Activo),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Eliminar", AppIcon.Delete, CambiasoTheme.Danger, () => { SelectProviderFromGrid(idx); DeleteProvider(); }),
                ContextMenuHelper.Item("Nuevo", AppIcon.Add, CambiasoTheme.Accent, () => { ClearProviderForm(); _txtProvNombre.Focus(); })
            ];
        });
        BuildProvidersTab(tabProv);

        // --- TAB SOLICITANTES ---
        TabPage tabSol = new("Solicitantes y Firmas") { BackColor = CambiasoTheme.Surface, Padding = new Padding(6) };
        _requestersControl = new RequesterManagerControl(_ctx, _session) { Dock = DockStyle.Fill };
        _requestersControl.RequestersChanged += (_, _) => CatalogsChanged?.Invoke(this, EventArgs.Empty);
        tabSol.Controls.Add(_requestersControl);

        // --- TAB ÁRBOL DE ACTIVOS ---
        TabPage tabAssets = new("Árbol de Activos") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _assetTree = new AssetTreePanel(_ctx.MaintainerService, _ctx.AssetTreeService, _session) { Dock = DockStyle.Fill, IncludeInactive = true };
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
        _assetTree.SelectedAssetChanged += (_, node) => BindSelectedAsset(node);
        _assetTree.TreeModified += (_, _) =>
        {
            BindSelectedAsset(_assetTree.SelectedAsset);
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        };
        BuildAssetsTab(tabAssets);

        tabs.TabPages.AddRange([tabProv, tabSol, tabAssets]);
        Controls.Add(tabs);

        LoadProviders();
        BindSelectedAsset(_assetTree.SelectedAsset);

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || _suppressTreeSync || (e.Kind & Sispro.Core.Enums.DataChangeKind.Catalogs) == 0)
        {
            return;
        }

        void RefreshCatalogs()
        {
            if (IsDisposed) return;
            _assetTree.ReloadPreservingSelection();
            BindSelectedAsset(_assetTree.SelectedAsset);
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshCatalogs);
        else if (!InvokeRequired) RefreshCatalogs();
    }

    private void BuildProvidersTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 198));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
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

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 4) };
        Button btnSave = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar proveedor nuevo o modificado", width: 120);
        btnSave.Click += (_, _) => SaveProvider();
        Button btnDel = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar o desactivar proveedor seleccionado", width: 118);
        btnDel.Click += (_, _) => DeleteProvider();
        Button btnClear = CambiasoTheme.CreateButton("Nuevo / Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Limpiar ficha para nuevo proveedor", width: 144);
        btnClear.Click += (_, _) => ClearProviderForm();
        Button btnExcel = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar catálogo de proveedores a Excel, PDF, Word o CSV", width: 118);
        btnExcel.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            btnExcel,
            this,
            _ctx.DocumentExportService,
            "SISPRO_Proveedores",
            () => DocumentExportService.BuildProvidersReport(_providers, string.IsNullOrWhiteSpace(_txtProvSearch.Text) ? "Catálogo completo" : $"Filtro: {_txtProvSearch.Text}"));

        bar.Controls.AddRange([btnSave, btnDel, btnClear, btnExcel, Wrap("Buscar:", _txtProvSearch)]);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Catálogo de Proveedores");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridProviders);

        layout.Controls.Add(grpForm, 0, 0);
        layout.Controls.Add(bar, 0, 1);
        layout.Controls.Add(grpGrid, 0, 2);
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
        grpEdit.Height = 338;

        TableLayoutPanel techGrid = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 7, Padding = new Padding(6) };
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 122));
        techGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 6; r++) techGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        techGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

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

        FlowLayoutPanel editButtons = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
        Button btnUpdateNode = CambiasoTheme.CreateButton("Guardar Ficha del Activo", AppIcon.Cpu, ButtonTone.Primary, _toolTip, "Guardar descripción, estado y ficha técnica del activo", width: 204);
        btnUpdateNode.Visible = canEdit;
        btnUpdateNode.Click += (_, _) => SaveSelectedAsset();

        Button btnOpenTreeEditor = CambiasoTheme.CreateButton("Abrir Editor de Árbol", AppIcon.Tree, ButtonTone.Accent, _toolTip, "Abrir módulo completo de edición estructural del árbol (solo Administrador)", width: 196);
        btnOpenTreeEditor.Visible = canEdit;
        btnOpenTreeEditor.Click += (_, _) => OpenTreeEditorRequested?.Invoke(this, EventArgs.Empty);

        Label lblReadOnlyHint = new()
        {
            Text = "Solo lectura — la modificación del árbol es exclusiva del perfil Administrador.",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Warning,
            AutoSize = true,
            Visible = !canEdit,
            Margin = new Padding(4, 8, 4, 0)
        };

        editButtons.Controls.AddRange([btnUpdateNode, btnOpenTreeEditor, lblReadOnlyHint]);
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
        Button btnAddChild = CambiasoTheme.CreateButton("Agregar Hijo", AppIcon.Add, ButtonTone.Accent, _toolTip, "Crear un nuevo nodo hijo bajo el nodo seleccionado", width: 146);
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

        if (_gridProviders.Columns.Count >= 7)
        {
            GridBindingHelper.ConfigureColumn(_gridProviders, 0, "ID", 6);
            GridBindingHelper.ConfigureColumn(_gridProviders, 1, "Proveedor", 20);
            GridBindingHelper.ConfigureColumn(_gridProviders, 2, "Dirección", 26);
            GridBindingHelper.ConfigureColumn(_gridProviders, 3, "Teléfono", 12);
            GridBindingHelper.ConfigureColumn(_gridProviders, 4, "Correo", 18);
            GridBindingHelper.ConfigureColumn(_gridProviders, 5, "Contacto", 10);
            GridBindingHelper.ConfigureColumn(_gridProviders, 6, "Estado", 8);
        }
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

    private void DeleteProvider()
    {
        if (_selectedProvId <= 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un proveedor de la lista.");
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Eliminar o desactivar el proveedor '{_txtProvNombre.Text}'?"))
        {
            return;
        }

        try
        {
            _ctx.MaintainerService.DeleteProvider(_session, _selectedProvId);
            CambiasoTheme.ShowInfo(this, "Operación completada correctamente.");
            ClearProviderForm();
            LoadProviders();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
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
