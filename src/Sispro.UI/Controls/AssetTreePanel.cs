using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Árbol jerárquico de activos de 10 niveles con carga diferida (lazy loading), búsqueda rápida,
/// íconos vectoriales por tipo de nodo, selección programática sin parpadeos y menú contextual
/// completo con operaciones CRUD exclusivas para perfil Administrador.
/// </summary>
public sealed class AssetTreePanel : UserControl
{
    private const string DummyKey = "__DUMMY__";

    private readonly MaintainerService _maintainerService;
    private readonly AssetTreeService? _treeAdmin;
    private readonly UserSession? _session;
    private readonly ToolTip _toolTip = new();
    private readonly ImageList _nodeIcons;
    private readonly TextBox _txtSearch;
    private readonly Button _btnSearch;
    private readonly Button _btnReset;
    private readonly Panel _checkBar;
    private readonly CheckBox _chkSelectAllVisible;
    private readonly Label _lblCheckedCount;
    private readonly Button _btnClearChecks;
    private readonly TreeView _treeView;
    private readonly Label _lblBreadcrumb;
    private readonly HashSet<int> _checkedIds = [];
    private bool _includeInactive;
    private bool _suppressCheckEvents;

    public bool CanEdit { get; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Func<AssetNode, IEnumerable<ToolStripItem>>? ExtraMenuItemsProvider { get; set; }

    public event EventHandler<AssetNode?>? SelectedAssetChanged;
    public event EventHandler<int?>? TreeModified;
    public event EventHandler? CheckedAssetsChanged;
    public event EventHandler<IReadOnlyList<int>>? InspectDependenciesRequested;

    public AssetNode? SelectedAsset => _treeView.SelectedNode?.Tag as AssetNode;

    public IReadOnlyList<int> CheckedAssetIds => _checkedIds.ToList();

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ShowCheckBoxes
    {
        get => _treeView.CheckBoxes;
        set
        {
            if (_treeView.CheckBoxes == value)
            {
                return;
            }

            _treeView.CheckBoxes = value;
            _checkBar.Visible = value;
            if (value)
            {
                SyncNodeChecksRecursive(_treeView.Nodes);
            }
            else
            {
                _checkedIds.Clear();
            }

            UpdateCheckedCountLabel();
        }
    }

    public string CurrentBreadcrumb => _lblBreadcrumb.Text;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IncludeInactive
    {
        get => _includeInactive;
        set
        {
            if (_includeInactive == value)
            {
                return;
            }

            _includeInactive = value;
            LoadRoot();
        }
    }

    public AssetTreePanel(
        MaintainerService maintainerService,
        AssetTreeService? treeAdmin = null,
        UserSession? session = null,
        bool showCheckBoxes = false,
        bool includeInactive = false)
    {
        _maintainerService = maintainerService;
        _treeAdmin = treeAdmin;
        _session = session;
        _includeInactive = includeInactive;
        CanEdit = treeAdmin is not null && treeAdmin.IsAdministrator(session);

        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.CardBackground;
        Padding = new Padding(8);
        MinimumSize = new Size(250, 300);

        _nodeIcons = BuildNodeImageList();

        Label lblHeader = new()
        {
            Text = "ÁRBOL DE ACTIVOS (10 NIVELES)",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Dock = DockStyle.Top,
            Height = 24
        };

        Panel searchPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(0, 2, 0, 4)
        };

        _txtSearch = new TextBox
        {
            PlaceholderText = "Buscar planta, equipo o ID...",
            Font = CambiasoTheme.SmallFont,
            Dock = DockStyle.Fill
        };
        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                ExecuteSearch();
            }
        };

        _btnSearch = CambiasoTheme.CreateButton(string.Empty, AppIcon.Search, ButtonTone.Primary, _toolTip, "Buscar activo en el árbol", width: 32, height: 26);
        _btnSearch.Dock = DockStyle.Right;
        _btnSearch.Padding = Padding.Empty;
        _btnSearch.ImageAlign = ContentAlignment.MiddleCenter;
        _btnSearch.Click += (_, _) => ExecuteSearch();

        _btnReset = CambiasoTheme.CreateButton(string.Empty, AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Restablecer árbol completo", width: 32, height: 26);
        _btnReset.Dock = DockStyle.Right;
        _btnReset.Padding = Padding.Empty;
        _btnReset.ImageAlign = ContentAlignment.MiddleCenter;
        _btnReset.Click += (_, _) =>
        {
            _txtSearch.Clear();
            LoadRoot();
        };

        searchPanel.Controls.Add(_txtSearch);
        searchPanel.Controls.Add(_btnSearch);
        searchPanel.Controls.Add(_btnReset);

        _treeView = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.SmallFont,
            BorderStyle = BorderStyle.FixedSingle,
            HideSelection = false,
            FullRowSelect = true,
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            ShowNodeToolTips = true,
            CheckBoxes = showCheckBoxes,
            ImageList = _nodeIcons,
            ItemHeight = 22
        };
        _treeView.BeforeExpand += OnBeforeExpand;
        _treeView.AfterSelect += OnAfterSelect;
        _treeView.AfterCheck += OnAfterCheck;
        _treeView.NodeMouseClick += OnTreeNodeMouseClick;

        _checkBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            Visible = showCheckBoxes,
            Padding = new Padding(0, 2, 0, 2),
            BackColor = CambiasoTheme.SubtleFill
        };
        _chkSelectAllVisible = new CheckBox
        {
            Name = "chkSelectAllAssets",
            Text = "Marcar visibles",
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            Location = new Point(4, 5)
        };
        _chkSelectAllVisible.CheckedChanged += (_, _) =>
        {
            if (!_suppressCheckEvents)
            {
                SetAllVisibleChecked(_chkSelectAllVisible.Checked);
            }
        };
        _lblCheckedCount = new Label
        {
            Text = "Sel.: 0",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Location = new Point(116, 6)
        };
        _btnClearChecks = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip,
            "Desmarcar todos los nodos seleccionados", width: 96, height: 24);
        _btnClearChecks.Dock = DockStyle.Right;
        _btnClearChecks.Font = CambiasoTheme.SmallFont;
        _btnClearChecks.Padding = new Padding(4, 0, 4, 0);
        _btnClearChecks.Click += (_, _) => ClearChecked();
        _checkBar.Controls.AddRange([_chkSelectAllVisible, _lblCheckedCount, _btnClearChecks]);

        _lblBreadcrumb = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            BackColor = CambiasoTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(6, 4, 6, 4),
            Text = "CAMBIASO HNOS S.A.C."
        };

        Controls.Add(_treeView);
        Controls.Add(_checkBar);
        Controls.Add(searchPanel);
        Controls.Add(lblHeader);
        Controls.Add(_lblBreadcrumb);

        LoadRoot();
    }

    public void LoadRoot()
    {
        _treeView.BeginUpdate();
        try
        {
            _treeView.Nodes.Clear();
            IReadOnlyList<AssetNode> roots = _maintainerService.GetAssetChildren(null, _includeInactive);
            foreach (AssetNode root in roots)
            {
                TreeNode tn = CreateTreeNode(root);
                _treeView.Nodes.Add(tn);
                LoadChildrenInto(tn);
                tn.Expand();
            }

            if (_treeView.Nodes.Count > 0)
            {
                _treeView.SelectedNode = _treeView.Nodes[0];
            }
        }
        finally
        {
            _treeView.EndUpdate();
        }

        UpdateCheckedCountLabel();
    }

    public void SetAssetChecked(int idActivo, bool isChecked)
    {
        if (idActivo <= 0)
        {
            return;
        }

        bool changed = isChecked ? _checkedIds.Add(idActivo) : _checkedIds.Remove(idActivo);
        SyncNodeChecksRecursive(_treeView.Nodes);
        UpdateCheckedCountLabel();
        if (changed)
        {
            CheckedAssetsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void SetAllVisibleChecked(bool isChecked)
    {
        SetVisibleCheckedRecursive(_treeView.Nodes, isChecked);
        SyncNodeChecksRecursive(_treeView.Nodes);
        UpdateCheckedCountLabel();
        CheckedAssetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearChecked()
    {
        if (_checkedIds.Count == 0 && !_chkSelectAllVisible.Checked)
        {
            return;
        }

        _checkedIds.Clear();
        SyncNodeChecksRecursive(_treeView.Nodes);
        UpdateCheckedCountLabel();
        CheckedAssetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetVisibleCheckedRecursive(TreeNodeCollection nodes, bool isChecked)
    {
        foreach (TreeNode tn in nodes)
        {
            if (tn.Tag is AssetNode a)
            {
                if (isChecked) _checkedIds.Add(a.IdActivo);
                else _checkedIds.Remove(a.IdActivo);
            }

            if (tn.Nodes.Count > 0 && !(tn.Nodes.Count == 1 && tn.Nodes[0].Name == DummyKey))
            {
                SetVisibleCheckedRecursive(tn.Nodes, isChecked);
            }
        }
    }

    private void SyncNodeChecksRecursive(TreeNodeCollection nodes)
    {
        _suppressCheckEvents = true;
        try
        {
            foreach (TreeNode tn in nodes)
            {
                if (tn.Tag is AssetNode a)
                {
                    bool shouldCheck = _treeView.CheckBoxes && _checkedIds.Contains(a.IdActivo);
                    if (tn.Checked != shouldCheck)
                    {
                        tn.Checked = shouldCheck;
                    }
                }

                if (tn.Nodes.Count > 0)
                {
                    SyncNodeChecksRecursive(tn.Nodes);
                }
            }
        }
        finally
        {
            _suppressCheckEvents = false;
        }
    }

    private void OnAfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_suppressCheckEvents || e.Node?.Tag is not AssetNode asset)
        {
            return;
        }

        if (e.Node.Checked)
        {
            _checkedIds.Add(asset.IdActivo);
        }
        else
        {
            _checkedIds.Remove(asset.IdActivo);
        }

        UpdateCheckedCountLabel();
        CheckedAssetsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCheckedCountLabel()
    {
        if (_lblCheckedCount is null)
        {
            return;
        }

        _lblCheckedCount.Text = $"Sel.: {_checkedIds.Count}";
        _btnClearChecks.Enabled = _checkedIds.Count > 0;
        _suppressCheckEvents = true;
        try
        {
            int visible = 0;
            int checkedVis = 0;
            CountVisibleChecks(_treeView.Nodes, ref visible, ref checkedVis);
            _chkSelectAllVisible.Checked = visible > 0 && visible == checkedVis;
        }
        finally
        {
            _suppressCheckEvents = false;
        }
    }

    private void CountVisibleChecks(TreeNodeCollection nodes, ref int visible, ref int checkedVis)
    {
        foreach (TreeNode tn in nodes)
        {
            if (tn.Tag is AssetNode a)
            {
                visible++;
                if (_checkedIds.Contains(a.IdActivo))
                {
                    checkedVis++;
                }
            }

            if (tn.IsExpanded && tn.Nodes.Count > 0 && !(tn.Nodes.Count == 1 && tn.Nodes[0].Name == DummyKey))
            {
                CountVisibleChecks(tn.Nodes, ref visible, ref checkedVis);
            }
        }
    }

    public void ReloadPreservingSelection()
    {
        IReadOnlyList<int> pathIds = SelectedAsset is not null ? GetSelectedPathIds() : [];
        _txtSearch.Clear();
        LoadRoot();
        if (pathIds.Count > 0)
        {
            SelectNodeByPath(pathIds);
        }
    }

    public IReadOnlyList<int> GetSelectedPathIds()
    {
        if (SelectedAsset is null)
        {
            return [1];
        }

        IReadOnlyList<AssetNode> path = _maintainerService.GetAssetPath(SelectedAsset.IdActivo);
        return path.Count > 0 ? path.Select(n => n.IdActivo).ToList() : [SelectedAsset.IdActivo];
    }

    public void SelectNodeByPath(IReadOnlyList<int> levelIds)
    {
        if (levelIds.Count == 0)
        {
            LoadRoot();
            return;
        }

        _treeView.BeginUpdate();
        try
        {
            if (_treeView.Nodes.Count == 0)
            {
                LoadRoot();
            }

            TreeNodeCollection currentLevel = _treeView.Nodes;
            TreeNode? lastFound = null;

            foreach (int id in levelIds.Where(i => i > 0))
            {
                TreeNode? match = currentLevel.Cast<TreeNode>().FirstOrDefault(n => n.Tag is AssetNode a && a.IdActivo == id);
                if (match is null)
                {
                    break;
                }

                lastFound = match;
                LoadChildrenInto(match);
                match.Expand();
                currentLevel = match.Nodes;
            }

            if (lastFound is not null)
            {
                _treeView.SelectedNode = lastFound;
                lastFound.EnsureVisible();
            }
        }
        finally
        {
            _treeView.EndUpdate();
        }
    }

    public void RefreshSelectedNode()
    {
        TreeNode? sel = _treeView.SelectedNode;
        if (sel?.Tag is not AssetNode)
        {
            LoadRoot();
            return;
        }

        _treeView.BeginUpdate();
        try
        {
            sel.Nodes.Clear();
            LoadChildrenInto(sel);
            sel.Expand();
        }
        finally
        {
            _treeView.EndUpdate();
        }
    }

    public void AddChildToSelected()
    {
        if (!EnsureEditable() || _treeAdmin is null || _session is null)
        {
            return;
        }

        AssetNode? parent = SelectedAsset;
        if (parent is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione el nodo padre donde desea agregar el subnivel.");
            return;
        }

        IReadOnlyList<AssetNode> pathNodes = _maintainerService.GetAssetPath(parent.IdActivo);
        string parentPath = pathNodes.Count > 0
            ? string.Join(" \\ ", pathNodes.Select(n => n.Descripcion))
            : parent.Descripcion;

        using AssetNodeEditorDialog dlg = new(existing: null, parent, parentPath);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            int newId = _treeAdmin.AddChild(_session, parent.IdActivo, dlg.Result);
            ReloadAndSelect(newId);
            TreeModified?.Invoke(this, newId);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    public void EditSelected()
    {
        if (!EnsureEditable() || _treeAdmin is null || _session is null)
        {
            return;
        }

        AssetNode? node = SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo del árbol para editar.");
            return;
        }

        IReadOnlyList<AssetNode> pathNodes = _maintainerService.GetAssetPath(node.IdActivo);
        AssetNode parent = pathNodes.Count > 1 ? pathNodes[^2] : node;
        string parentPath = pathNodes.Count > 1
            ? string.Join(" \\ ", pathNodes.Take(pathNodes.Count - 1).Select(n => n.Descripcion))
            : string.Empty;

        using AssetNodeEditorDialog dlg = new(node, parent, parentPath);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _treeAdmin.UpdateNode(_session, dlg.Result);
            ReloadAndSelect(node.IdActivo);
            TreeModified?.Invoke(this, node.IdActivo);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    public void MoveSelected()
    {
        if (!EnsureEditable() || _treeAdmin is null || _session is null)
        {
            return;
        }

        AssetNode? node = SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo del árbol para mover.");
            return;
        }

        if (!node.ParentId.HasValue)
        {
            CambiasoTheme.ShowWarning(this, "No es posible mover el nodo raíz de la empresa.");
            return;
        }

        using AssetNodeMoveDialog dlg = new(_maintainerService, node, CurrentBreadcrumb);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _treeAdmin.MoveNode(_session, node.IdActivo, dlg.SelectedParentId);
            ReloadAndSelect(node.IdActivo);
            TreeModified?.Invoke(this, node.IdActivo);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    public void SetSelectedActive(bool active)
    {
        if (!EnsureEditable() || _treeAdmin is null || _session is null)
        {
            return;
        }

        AssetNode? node = SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo del árbol.");
            return;
        }

        try
        {
            bool includeDescendants = true;
            if (!active)
            {
                AssetNodeInfo info = _treeAdmin.GetNodeInfo(node.IdActivo);
                string childNotice = info.ActiveChildren > 0
                    ? $"\n\nSe desactivarán este nodo y sus subnodos activos ({info.ActiveChildren} hijo(s) directo(s))."
                    : string.Empty;

                if (!CambiasoTheme.Confirm(this, $"¿Desea desactivar el nodo '{node.Descripcion}'?{childNotice}"))
                {
                    return;
                }
            }
            else
            {
                AssetNodeInfo info = _treeAdmin.GetNodeInfo(node.IdActivo);
                if (info.TotalChildren > 0)
                {
                    DialogResult ans = MessageBox.Show(
                        this,
                        $"El nodo '{node.Descripcion}' tiene subnodos.\n\n¿Desea activar también todos sus subnodos descendientes?",
                        "SISPRO — Activar Nodo",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question,
                        MessageBoxDefaultButton.Button1);

                    if (ans == DialogResult.Cancel)
                    {
                        return;
                    }

                    includeDescendants = ans == DialogResult.Yes;
                }
            }

            int affected = _treeAdmin.SetActive(_session, node.IdActivo, active, includeDescendants);
            ReloadAndSelect(node.IdActivo);
            CambiasoTheme.ShowInfo(this, $"Estado actualizado ({affected} nodo(s) {(active ? "activados" : "desactivados")}).");
            TreeModified?.Invoke(this, node.IdActivo);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    public void DeleteSelected()
    {
        if (!EnsureEditable() || _treeAdmin is null || _session is null)
        {
            return;
        }

        AssetNode? node = SelectedAsset;
        if (node is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione el nodo que desea eliminar.");
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Eliminar definitivamente el nodo '{node.Descripcion}' (ID #{node.IdActivo})?"))
        {
            return;
        }

        int fallbackId = node.ParentId ?? 1;
        try
        {
            _treeAdmin.DeleteNode(_session, node.IdActivo);
            ReloadAndSelect(fallbackId);
            CambiasoTheme.ShowInfo(this, $"Nodo '{node.Descripcion}' eliminado correctamente.");
            TreeModified?.Invoke(this, fallbackId);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    public void ExpandSelected()
    {
        TreeNode? sel = _treeView.SelectedNode;
        if (sel is null)
        {
            return;
        }

        Cursor? previous = Cursor;
        Cursor = Cursors.WaitCursor;
        _treeView.BeginUpdate();
        try
        {
            sel.ExpandAll();
            sel.EnsureVisible();
        }
        finally
        {
            _treeView.EndUpdate();
            Cursor = previous;
        }
    }

    public void CollapseSelected()
    {
        TreeNode? sel = _treeView.SelectedNode;
        if (sel is null)
        {
            return;
        }

        _treeView.BeginUpdate();
        try
        {
            sel.Collapse(ignoreChildren: false);
        }
        finally
        {
            _treeView.EndUpdate();
        }
    }

    private void CopySelectedPath()
    {
        string text = string.IsNullOrWhiteSpace(CurrentBreadcrumb)
            ? (SelectedAsset?.Descripcion ?? string.Empty)
            : CurrentBreadcrumb;

        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
        }
    }

    private void ReloadAndSelect(int nodeId)
    {
        IReadOnlyList<AssetNode> path = _maintainerService.GetAssetPath(nodeId);
        List<int> pathIds = path.Count > 0 ? path.Select(n => n.IdActivo).ToList() : [1];
        _txtSearch.Clear();
        LoadRoot();
        SelectNodeByPath(pathIds);
    }

    private bool EnsureEditable()
    {
        if (CanEdit)
        {
            return true;
        }

        CambiasoTheme.ShowWarning(this, "La modificación del árbol de activos es exclusiva para usuarios con perfil Administrador.");
        return false;
    }

    private void OnTreeNodeMouseClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.Node is null)
        {
            return;
        }

        _treeView.SelectedNode = e.Node;
        if (e.Node.Tag is not AssetNode asset)
        {
            return;
        }

        List<ToolStripItem> items = [];

        if (CanEdit)
        {
            items.Add(ContextMenuHelper.Item("Agregar nodo hijo…", AppIcon.Add, CambiasoTheme.Success, AddChildToSelected));
            items.Add(ContextMenuHelper.Item("Editar nodo…", AppIcon.Edit, CambiasoTheme.Primary, EditSelected));
            items.Add(ContextMenuHelper.Item("Mover a otro nodo padre…", AppIcon.MoveRight, CambiasoTheme.Accent, MoveSelected, enabled: asset.ParentId.HasValue));
            items.Add(ContextMenuHelper.Item("Activar nodo", AppIcon.Power, CambiasoTheme.Success, () => SetSelectedActive(true), enabled: !asset.Activo));
            items.Add(ContextMenuHelper.Item("Desactivar nodo", AppIcon.Ban, CambiasoTheme.Warning, () => SetSelectedActive(false), enabled: asset.Activo && asset.ParentId.HasValue));
            items.Add(ContextMenuHelper.Item("Eliminar nodo…", AppIcon.Delete, CambiasoTheme.Danger, DeleteSelected, enabled: asset.ParentId.HasValue));
            items.Add(ContextMenuHelper.Separator());
        }

        if (InspectDependenciesRequested is not null)
        {
            List<int> targetIds = _checkedIds.Count > 0 ? _checkedIds.ToList() : [asset.IdActivo];
            items.Add(ContextMenuHelper.Item("Consultar OT asociadas…", AppIcon.Search, CambiasoTheme.Accent,
                () => InspectDependenciesRequested.Invoke(this, targetIds)));
            if (CanEdit)
            {
                items.Add(ContextMenuHelper.Item("Reasignar OT / Subnodos…", AppIcon.MoveRight, CambiasoTheme.Primary,
                    () => InspectDependenciesRequested.Invoke(this, targetIds)));
            }

            items.Add(ContextMenuHelper.Separator());
        }

        if (ExtraMenuItemsProvider is not null)
        {
            List<ToolStripItem> extra = ExtraMenuItemsProvider(asset).ToList();
            if (extra.Count > 0)
            {
                items.AddRange(extra);
                items.Add(ContextMenuHelper.Separator());
            }
        }

        items.Add(ContextMenuHelper.Item("Expandir rama", AppIcon.Expand, CambiasoTheme.Primary, ExpandSelected));
        items.Add(ContextMenuHelper.Item("Contraer rama", AppIcon.Collapse, CambiasoTheme.TextMuted, CollapseSelected));
        items.Add(ContextMenuHelper.Item("Copiar ruta", AppIcon.Copy, CambiasoTheme.Accent, CopySelectedPath));
        items.Add(ContextMenuHelper.Item("Actualizar árbol", AppIcon.Refresh, CambiasoTheme.Primary, ReloadPreservingSelection));

        ContextMenuHelper.Show(_treeView, e.Location, items);
    }

    private void ExecuteSearch()
    {
        string q = _txtSearch.Text.Trim();
        if (string.IsNullOrEmpty(q))
        {
            LoadRoot();
            return;
        }

        IReadOnlyList<AssetNode> matches = _maintainerService.SearchAssets(q, _includeInactive);
        _treeView.BeginUpdate();
        bool prevSuppress = _suppressCheckEvents;
        _suppressCheckEvents = true;
        try
        {
            _treeView.Nodes.Clear();
            if (matches.Count == 0)
            {
                _lblBreadcrumb.Text = $"Sin resultados para \"{q}\".";
                SelectedAssetChanged?.Invoke(this, null);
                return;
            }

            foreach (AssetNode node in matches)
            {
                IReadOnlyList<AssetNode> path = _maintainerService.GetAssetPath(node.IdActivo);
                int realDepth = Math.Max(1, path.Count);
                string inactiveSuffix = node.Activo ? string.Empty : " (inactivo)";
                string label = $"{node.Descripcion}{inactiveSuffix}  [N{realDepth} · #{node.IdActivo}]";
                string imageKey = ResolveImageKey(node.Categoria);
                TreeNode tn = new(label)
                {
                    Tag = node,
                    ImageKey = imageKey,
                    SelectedImageKey = imageKey,
                    Checked = _treeView.CheckBoxes && _checkedIds.Contains(node.IdActivo),
                    ToolTipText = $"{string.Join(" \\ ", path.Select(p => p.Descripcion))}  |  ID #{node.IdActivo} · {AssetNodeTypes.GetLabel(node.Categoria)} · Nivel {realDepth}",
                    ForeColor = node.Activo ? CambiasoTheme.TextPrimary : Color.Gray
                };
                _treeView.Nodes.Add(tn);
            }

            _treeView.SelectedNode = _treeView.Nodes[0];
        }
        finally
        {
            _suppressCheckEvents = prevSuppress;
            _treeView.EndUpdate();
        }

        UpdateCheckedCountLabel();
    }

    private void OnBeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node is null)
        {
            return;
        }

        if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Name == DummyKey)
        {
            LoadChildrenInto(e.Node);
            UpdateCheckedCountLabel();
        }
    }

    private void OnAfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is AssetNode asset)
        {
            IReadOnlyList<AssetNode> path = _maintainerService.GetAssetPath(asset.IdActivo);
            _lblBreadcrumb.Text = path.Count > 0
                ? string.Join(" \\ ", path.Select(p => p.Descripcion))
                : asset.Descripcion;
            SelectedAssetChanged?.Invoke(this, asset);
        }
        else
        {
            _lblBreadcrumb.Text = string.Empty;
            SelectedAssetChanged?.Invoke(this, null);
        }
    }

    private void LoadChildrenInto(TreeNode parentTreeNode)
    {
        if (parentTreeNode.Tag is not AssetNode parentAsset)
        {
            return;
        }

        bool prevSuppress = _suppressCheckEvents;
        _suppressCheckEvents = true;
        try
        {
            parentTreeNode.Nodes.Clear();
            IReadOnlyList<AssetNode> children = _maintainerService.GetAssetChildren(parentAsset.IdActivo, _includeInactive);
            foreach (AssetNode child in children)
            {
                parentTreeNode.Nodes.Add(CreateTreeNode(child));
            }
        }
        finally
        {
            _suppressCheckEvents = prevSuppress;
        }
    }

    private TreeNode CreateTreeNode(AssetNode node)
    {
        string displayText = node.Activo ? node.Descripcion : $"{node.Descripcion} (inactivo)";
        string imageKey = ResolveImageKey(node.Categoria);
        TreeNode tn = new(displayText)
        {
            Tag = node,
            ImageKey = imageKey,
            SelectedImageKey = imageKey,
            Checked = _treeView.CheckBoxes && _checkedIds.Contains(node.IdActivo),
            ToolTipText = $"ID #{node.IdActivo} · {AssetNodeTypes.GetLabel(node.Categoria)} · Nivel {node.Nivel}",
            ForeColor = node.Activo ? CambiasoTheme.TextPrimary : Color.Gray
        };

        if (node.TieneHijos)
        {
            tn.Nodes.Add(new TreeNode("Cargando...") { Name = DummyKey, ImageKey = imageKey, SelectedImageKey = imageKey });
        }

        return tn;
    }

    private static ImageList BuildNodeImageList()
    {
        ImageList list = new()
        {
            ColorDepth = ColorDepth.Depth32Bit,
            ImageSize = new Size(16, 16)
        };

        list.Images.Add(AssetNodeTypes.Empresa, IconCatalog.Get(AppIcon.Factory, CambiasoTheme.GoldAccent, 16));
        list.Images.Add(AssetNodeTypes.Planta, IconCatalog.Get(AppIcon.Factory, CambiasoTheme.Primary, 16));
        list.Images.Add(AssetNodeTypes.Seccion, IconCatalog.Get(AppIcon.Layers, CambiasoTheme.Accent, 16));
        list.Images.Add(AssetNodeTypes.Maquina, IconCatalog.Get(AppIcon.Cpu, CambiasoTheme.Primary, 16));
        list.Images.Add(AssetNodeTypes.Modulo, IconCatalog.Get(AppIcon.Package, CambiasoTheme.TextPrimary, 16));
        list.Images.Add(AssetNodeTypes.Componente, IconCatalog.Get(AppIcon.Gauge, CambiasoTheme.TextMuted, 16));
        list.Images.Add(AssetNodeTypes.SinClasificar, IconCatalog.Get(AppIcon.Tree, Color.Gray, 16));
        return list;
    }

    private string ResolveImageKey(string? categoria)
    {
        string normalized = AssetNodeTypes.Normalize(categoria);
        return _nodeIcons.Images.ContainsKey(normalized) ? normalized : AssetNodeTypes.SinClasificar;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _nodeIcons.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
