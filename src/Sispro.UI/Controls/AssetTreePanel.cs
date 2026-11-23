using System.Drawing;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Árbol jerárquico de activos de 10 niveles con carga diferida (lazy loading), búsqueda rápida
/// y selección programática de rutas de OT sin parpadeos.
/// </summary>
public sealed class AssetTreePanel : UserControl
{
    private const string DummyKey = "__DUMMY__";

    private readonly MaintainerService _maintainerService;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtSearch;
    private readonly Button _btnSearch;
    private readonly Button _btnReset;
    private readonly TreeView _treeView;
    private readonly Label _lblBreadcrumb;
    private bool _includeInactive;

    public event EventHandler<AssetNode?>? SelectedAssetChanged;

    public AssetNode? SelectedAsset => _treeView.SelectedNode?.Tag as AssetNode;

    public string CurrentBreadcrumb => _lblBreadcrumb.Text;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool IncludeInactive
    {
        get => _includeInactive;
        set
        {
            _includeInactive = value;
            LoadRoot();
        }
    }

    public AssetTreePanel(MaintainerService maintainerService)
    {
        _maintainerService = maintainerService;
        CambiasoTheme.EnableDoubleBuffering(this);

        BackColor = CambiasoTheme.CardBackground;
        Padding = new Padding(8);
        MinimumSize = new Size(250, 300);

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
            ItemHeight = 22
        };
        _treeView.BeforeExpand += OnBeforeExpand;
        _treeView.AfterSelect += OnAfterSelect;

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
    }

    public IReadOnlyList<int> GetSelectedPathIds()
    {
        if (SelectedAsset is null)
        {
            return [1];
        }

        IReadOnlyList<AssetNode> path = _maintainerService.GetAssetPath(SelectedAsset.IdActivo);
        return path.Select(n => n.IdActivo).ToList();
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
                string label = $"{node.Descripcion}  [N{node.Nivel} · #{node.IdActivo}]";
                TreeNode tn = new(label)
                {
                    Tag = node,
                    ToolTipText = string.Join(" \\ ", path.Select(p => p.Descripcion)),
                    ForeColor = node.Activo ? CambiasoTheme.TextPrimary : Color.Gray
                };
                _treeView.Nodes.Add(tn);
            }

            _treeView.SelectedNode = _treeView.Nodes[0];
        }
        finally
        {
            _treeView.EndUpdate();
        }
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
        }
    }

    private void OnAfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is AssetNode asset)
        {
            IReadOnlyList<AssetNode> path = _maintainerService.GetAssetPath(asset.IdActivo);
            _lblBreadcrumb.Text = string.Join(" \\ ", path.Select(p => p.Descripcion));
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

        parentTreeNode.Nodes.Clear();
        IReadOnlyList<AssetNode> children = _maintainerService.GetAssetChildren(parentAsset.IdActivo, _includeInactive);
        foreach (AssetNode child in children)
        {
            parentTreeNode.Nodes.Add(CreateTreeNode(child));
        }
    }

    private static TreeNode CreateTreeNode(AssetNode node)
    {
        TreeNode tn = new(node.Descripcion)
        {
            Tag = node,
            ForeColor = node.Activo ? CambiasoTheme.TextPrimary : Color.Gray
        };

        if (node.TieneHijos)
        {
            tn.Nodes.Add(new TreeNode("Cargando...") { Name = DummyKey });
        }

        return tn;
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
