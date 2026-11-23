using System.Drawing;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo para seleccionar el nuevo nodo padre de destino al mover una rama del árbol de activos.
/// </summary>
public sealed class AssetNodeMoveDialog : Form
{
    private readonly AssetNode _nodeToMove;
    private readonly AssetTreePanel _targetTree;
    private readonly Label _lblTargetInfo;

    public int SelectedParentId { get; private set; }

    public AssetNodeMoveDialog(MaintainerService maintainerService, AssetNode node, string nodePath)
    {
        _nodeToMove = node;

        Text = $"Mover Nodo #{node.IdActivo} — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(560, 560);
        MinimumSize = new Size(460, 440);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(16, 10, 16, 10)
        };
        header.Controls.Add(new Label
        {
            Text = $"MOVER NODO: {node.Descripcion}",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });

        Panel infoBanner = new()
        {
            Dock = DockStyle.Top,
            Height = 68,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(12, 8, 12, 8)
        };
        Label lblSource = new()
        {
            Text = $"Origen actual: {(string.IsNullOrWhiteSpace(nodePath) ? node.Descripcion : nodePath)}",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary,
            Dock = DockStyle.Top,
            Height = 22
        };
        _lblTargetInfo = new Label
        {
            Text = "Seleccione el nuevo nodo padre en el árbol inferior.",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        infoBanner.Controls.Add(_lblTargetInfo);
        infoBanner.Controls.Add(lblSource);

        _targetTree = new AssetTreePanel(maintainerService)
        {
            Dock = DockStyle.Fill,
            IncludeInactive = false
        };
        _targetTree.SelectedAssetChanged += (_, target) =>
        {
            _lblTargetInfo.Text = target is null
                ? "Seleccione un nodo de destino válido."
                : $"Destino seleccionado: {target.Descripcion} (ID #{target.IdActivo} · Nivel {target.Nivel})";
        };

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(12, 9, 12, 8)
        };

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, width: 112, height: 32);
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Button btnMove = CambiasoTheme.CreateButton("Mover Aquí", AppIcon.MoveRight, ButtonTone.Primary, width: 136, height: 32);
        btnMove.Click += (_, _) => ConfirmSelection();

        footer.Controls.AddRange([btnCancel, btnMove]);
        AcceptButton = btnMove;
        CancelButton = btnCancel;

        Controls.Add(_targetTree);
        Controls.Add(infoBanner);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private void ConfirmSelection()
    {
        AssetNode? target = _targetTree.SelectedAsset;
        if (target is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un nodo padre de destino.");
            return;
        }

        if (target.IdActivo == _nodeToMove.IdActivo)
        {
            CambiasoTheme.ShowWarning(this, "No puede mover un nodo dentro de sí mismo.");
            return;
        }

        SelectedParentId = target.IdActivo;
        DialogResult = DialogResult.OK;
    }
}
