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

    // Solicitantes controls
    private readonly TextBox _txtSolSearch;
    private readonly TextBox _txtSolAlias;
    private readonly TextBox _txtSolNombre;
    private readonly TextBox _txtSolPat;
    private readonly TextBox _txtSolMat;
    private readonly TextBox _txtSolFono;
    private readonly TextBox _txtSolMail;
    private readonly CheckBox _chkSolActivo;
    private readonly DataGridView _gridRequesters;
    private IReadOnlyList<Requester> _requesters = [];
    private int _selectedSolId;

    // Asset Tree controls
    private readonly AssetTreePanel _assetTree;
    private readonly TextBox _txtAssetName;
    private readonly TextBox _txtNewChildName;
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
        BuildProvidersTab(tabProv);

        // --- TAB SOLICITANTES ---
        TabPage tabSol = new("Solicitantes") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtSolSearch = new TextBox { Width = 240, PlaceholderText = "Filtrar solicitante..." };
        _txtSolSearch.TextChanged += (_, _) => LoadRequesters();
        _txtSolAlias = new TextBox { Width = 180 };
        _txtSolNombre = new TextBox { Width = 180 };
        _txtSolPat = new TextBox { Width = 180 };
        _txtSolMat = new TextBox { Width = 180 };
        _txtSolFono = new TextBox { Width = 150 };
        _txtSolMail = new TextBox { Width = 240 };
        _chkSolActivo = new CheckBox { Text = "Solicitante activo", Checked = true, AutoSize = true };
        _gridRequesters = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridRequesters);
        _gridRequesters.CellClick += (_, _) => SelectRequesterFromGrid();
        BuildRequestersTab(tabSol);

        // --- TAB ÁRBOL DE ACTIVOS ---
        TabPage tabAssets = new("Árbol de Activos") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _assetTree = new AssetTreePanel(_ctx.MaintainerService) { Dock = DockStyle.Fill, IncludeInactive = true };
        _txtAssetName = new TextBox { Width = 340 };
        _txtNewChildName = new TextBox { Width = 340, PlaceholderText = "Nombre del nuevo subnivel (planta, línea, máquina...)" };
        _chkAssetActive = new CheckBox { Text = "Nodo activo", Checked = true, AutoSize = true };
        _lblAssetMeta = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, ForeColor = CambiasoTheme.TextMuted };
        _assetTree.SelectedAssetChanged += (_, node) => BindSelectedAsset(node);
        BuildAssetsTab(tabAssets);

        tabs.TabPages.AddRange([tabProv, tabSol, tabAssets]);
        Controls.Add(tabs);

        LoadProviders();
        LoadRequesters();
        BindSelectedAsset(_assetTree.SelectedAsset);
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
        Button btnClear = CambiasoTheme.CreateButton("Nuevo / Limpiar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Limpiar ficha para nuevo proveedor", width: 144);
        btnClear.Click += (_, _) => ClearProviderForm();
        Button btnExcel = CambiasoTheme.CreateButton("Exportar Excel", AppIcon.Excel, ButtonTone.Accent, _toolTip, "Exportar catálogo de proveedores a Excel", width: 142);
        btnExcel.Click += (_, _) => ExportProviders();

        bar.Controls.AddRange([btnSave, btnDel, btnClear, btnExcel, Wrap("Buscar:", _txtProvSearch)]);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Catálogo de Proveedores");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridProviders);

        layout.Controls.Add(grpForm, 0, 0);
        layout.Controls.Add(bar, 0, 1);
        layout.Controls.Add(grpGrid, 0, 2);
        page.Controls.Add(layout);
    }

    private void BuildRequestersTab(TabPage page)
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        GroupBox grpForm = CambiasoTheme.CreateGroupBox("Ficha de Solicitante");
        grpForm.Dock = DockStyle.Fill;
        FlowLayoutPanel f = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        FlowLayoutPanel r1 = new() { AutoSize = true, WrapContents = false };
        r1.Controls.AddRange([Wrap("Alias:", _txtSolAlias), Wrap("Nombres:", _txtSolNombre), Wrap("Ap. Paterno:", _txtSolPat), Wrap("Ap. Materno:", _txtSolMat)]);
        FlowLayoutPanel r2 = new() { AutoSize = true, WrapContents = false };
        r2.Controls.AddRange([Wrap("Teléfono:", _txtSolFono), Wrap("Correo:", _txtSolMail), _chkSolActivo]);
        f.Controls.AddRange([r1, r2]);
        grpForm.Controls.Add(f);

        FlowLayoutPanel bar = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 4) };
        Button btnSave = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar solicitante", width: 120);
        btnSave.Click += (_, _) => SaveRequester();
        Button btnDel = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar o desactivar solicitante", width: 118);
        btnDel.Click += (_, _) => DeleteRequester();
        Button btnClear = CambiasoTheme.CreateButton("Nuevo / Limpiar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Limpiar ficha para nuevo solicitante", width: 144);
        btnClear.Click += (_, _) => ClearRequesterForm();
        bar.Controls.AddRange([btnSave, btnDel, btnClear, Wrap("Buscar:", _txtSolSearch)]);

        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Listado de Solicitantes");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(_gridRequesters);

        layout.Controls.Add(grpForm, 0, 0);
        layout.Controls.Add(bar, 0, 1);
        layout.Controls.Add(grpGrid, 0, 2);
        page.Controls.Add(layout);
    }

    private void BuildAssetsTab(TabPage page)
    {
        SplitContainer split = new() { Size = new Size(1080, 680), Dock = DockStyle.Fill, Panel1MinSize = 320, SplitterDistance = 380 };
        split.Panel1.Controls.Add(_assetTree);

        Panel right = new() { Dock = DockStyle.Fill, Padding = new Padding(14), BackColor = CambiasoTheme.Surface };

        GroupBox grpEdit = CambiasoTheme.CreateGroupBox("Editar Nodo Seleccionado");
        grpEdit.Dock = DockStyle.Top;
        grpEdit.Height = 160;
        FlowLayoutPanel editFlow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Button btnUpdateNode = CambiasoTheme.CreateButton("Actualizar Nodo", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar nombre y estado del nodo seleccionado", width: 152);
        btnUpdateNode.Click += (_, _) => SaveSelectedAsset();
        editFlow.Controls.AddRange([_lblAssetMeta, Wrap("Descripción:", _txtAssetName), _chkAssetActive, btnUpdateNode]);
        grpEdit.Controls.Add(editFlow);

        GroupBox grpAdd = CambiasoTheme.CreateGroupBox("Agregar Subnivel (Hijo)");
        grpAdd.Dock = DockStyle.Top;
        grpAdd.Height = 130;
        FlowLayoutPanel addFlow = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        Button btnAddChild = CambiasoTheme.CreateButton("Agregar Hijo", AppIcon.Add, ButtonTone.Accent, _toolTip, "Crear un nuevo nodo hijo bajo el nodo seleccionado", width: 142);
        btnAddChild.Click += (_, _) => AddAssetChild();
        addFlow.Controls.AddRange([Wrap("Nuevo Activo:", _txtNewChildName), btnAddChild]);
        grpAdd.Controls.Add(addFlow);

        right.Controls.Add(grpAdd);
        right.Controls.Add(grpEdit);
        split.Panel2.Controls.Add(right);
        page.Controls.Add(split);
    }

    private void LoadProviders()
    {
        _providers = _ctx.MaintainerService.GetProviders(includeInactive: true, search: _txtProvSearch.Text);
        _gridProviders.DataSource = _providers.Select(p => new
        {
            p.IdProv,
            Proveedor = p.Nombre,
            Dirección = p.Direccion,
            Teléfono = p.Telefono,
            Correo = p.Correo,
            Contacto = p.Contacto,
            Estado = p.Activo ? "Activo" : "Inactivo"
        }).ToList();

        if (_gridProviders.Columns.Count >= 7)
        {
            _gridProviders.Columns[0].HeaderText = "ID";
            _gridProviders.Columns[0].FillWeight = 6;
            _gridProviders.Columns[1].FillWeight = 20;
            _gridProviders.Columns[2].FillWeight = 26;
            _gridProviders.Columns[3].FillWeight = 12;
            _gridProviders.Columns[4].FillWeight = 18;
            _gridProviders.Columns[5].FillWeight = 10;
            _gridProviders.Columns[6].FillWeight = 8;
        }
    }

    private void SelectProviderFromGrid()
    {
        if (_gridProviders.CurrentRow is null)
        {
            return;
        }

        int idx = _gridProviders.CurrentRow.Index;
        if (idx < 0 || idx >= _providers.Count)
        {
            return;
        }

        Provider p = _providers[idx];
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

    private void ExportProviders()
    {
        using SaveFileDialog dlg = new()
        {
            Title = "Exportar Proveedores a Excel",
            Filter = "Libro de Excel (*.xlsx)|*.xlsx",
            FileName = $"Proveedores_SISPRO_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _ctx.ExcelExportService.ExportProvidersGrid(_providers, dlg.FileName);
            CambiasoTheme.ShowInfo(this, $"Catálogo exportado a:\n{dlg.FileName}");
        }
    }

    private void LoadRequesters()
    {
        _requesters = _ctx.MaintainerService.GetRequesters(includeInactive: true, search: _txtSolSearch.Text);
        _gridRequesters.DataSource = _requesters.Select(r => new
        {
            r.IdSol,
            r.Alias,
            Nombre = r.NombreCompleto,
            Teléfono = r.Telefono,
            Correo = r.Correo,
            Estado = r.Activo ? "Activo" : "Inactivo"
        }).ToList();
    }

    private void SelectRequesterFromGrid()
    {
        if (_gridRequesters.CurrentRow is null)
        {
            return;
        }

        int idx = _gridRequesters.CurrentRow.Index;
        if (idx < 0 || idx >= _requesters.Count)
        {
            return;
        }

        Requester r = _requesters[idx];
        _selectedSolId = r.IdSol;
        _txtSolAlias.Text = r.Alias;
        _txtSolNombre.Text = r.Nombre;
        _txtSolPat.Text = r.ApellidoPaterno;
        _txtSolMat.Text = r.ApellidoMaterno;
        _txtSolFono.Text = r.Telefono;
        _txtSolMail.Text = r.Correo;
        _chkSolActivo.Checked = r.Activo;
    }

    private void SaveRequester()
    {
        try
        {
            Requester r = new()
            {
                IdSol = _selectedSolId,
                Alias = _txtSolAlias.Text,
                Nombre = _txtSolNombre.Text,
                ApellidoPaterno = _txtSolPat.Text,
                ApellidoMaterno = _txtSolMat.Text,
                Telefono = _txtSolFono.Text,
                Correo = _txtSolMail.Text,
                Activo = _chkSolActivo.Checked
            };

            _ctx.MaintainerService.SaveRequester(_session, r);
            CambiasoTheme.ShowInfo(this, $"Solicitante '{r.Alias}' guardado correctamente.");
            ClearRequesterForm();
            LoadRequesters();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void DeleteRequester()
    {
        if (_selectedSolId <= 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un solicitante.");
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Eliminar o desactivar el solicitante '{_txtSolAlias.Text}'?"))
        {
            return;
        }

        try
        {
            _ctx.MaintainerService.DeleteRequester(_session, _selectedSolId);
            CambiasoTheme.ShowInfo(this, "Solicitante actualizado correctamente.");
            ClearRequesterForm();
            LoadRequesters();
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void ClearRequesterForm()
    {
        _selectedSolId = 0;
        _txtSolAlias.Clear();
        _txtSolNombre.Clear();
        _txtSolPat.Clear();
        _txtSolMat.Clear();
        _txtSolFono.Clear();
        _txtSolMail.Clear();
        _chkSolActivo.Checked = true;
    }

    private void BindSelectedAsset(AssetNode? node)
    {
        if (node is null)
        {
            _lblAssetMeta.Text = "Seleccione un nodo del árbol.";
            _txtAssetName.Clear();
            return;
        }

        _lblAssetMeta.Text = $"ID #{node.IdActivo} · Nivel {node.Nivel} de 10";
        _txtAssetName.Text = node.Descripcion;
        _chkAssetActive.Checked = node.Activo;
    }

    private void SaveSelectedAsset()
    {
        AssetNode? node = _assetTree.SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo del árbol.");
            return;
        }

        try
        {
            _ctx.MaintainerService.RenameAsset(_session, node.IdActivo, _txtAssetName.Text, _chkAssetActive.Checked);
            CambiasoTheme.ShowInfo(this, "Nodo de activo actualizado.");
            IReadOnlyList<int> path = _assetTree.GetSelectedPathIds();
            _assetTree.LoadRoot();
            _assetTree.SelectNodeByPath(path);
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
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

        try
        {
            int newId = _ctx.MaintainerService.AddAssetChild(_session, node.IdActivo, _txtNewChildName.Text);
            _txtNewChildName.Clear();
            _assetTree.RefreshSelectedNode();
            CambiasoTheme.ShowInfo(this, $"Subnivel agregado correctamente (ID #{newId}).");
            CatalogsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
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
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
