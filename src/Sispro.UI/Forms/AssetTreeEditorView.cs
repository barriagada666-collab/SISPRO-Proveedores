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
/// Módulo especial para la administración integral del Árbol de Activos (10 niveles),
/// reservado exclusivamente para cuentas con perfil Administrador.
/// </summary>
public sealed class AssetTreeEditorView : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();
    private readonly AssetTreePanel _treePanel;
    private readonly Label _lblAnomalies;

    private readonly Label _lblIdVal;
    private readonly Label _lblTipoVal;
    private readonly Label _lblNivelVal;
    private readonly Label _lblEstadoVal;
    private readonly Label _lblRutaVal;
    private readonly Label _lblHijosVal;
    private readonly Label _lblRefsVal;
    private readonly Label _lblSapVal;
    private readonly Label _lblEquipoVal;
    private readonly TextBox _txtObsVal;

    private bool _syncSubscribed;
    private bool _suppressSyncRefresh;

    internal AssetTreePanel TreePanel => _treePanel;
    internal Button BtnAddChild { get; }
    internal Button BtnEdit { get; }
    internal Button BtnMove { get; }
    internal Button BtnInspectOts { get; }
    internal Button BtnReassign { get; }
    internal Button BtnActivate { get; }
    internal Button BtnDeactivate { get; }
    internal Button BtnDelete { get; }
    internal Button BtnClearChecks { get; }
    internal Button BtnNormalize { get; }
    internal Button BtnExport { get; }
    internal Button BtnRefresh { get; }

    public AssetTreeEditorView(SisproApplicationContext ctx, UserSession session)
    {
        if (!ctx.AssetTreeService.IsAdministrator(session))
        {
            throw new UnauthorizedAccessException("Acceso denegado: el Editor del Árbol de Activos es exclusivo del perfil Administrador.");
        }

        _ctx = ctx;
        _session = session;
        SuspendLayout();
        Size = new Size(1120, 680);
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        // --- ENCABEZADO ADMINISTRATIVO ---
        Panel headerBanner = new()
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(14, 8, 14, 8)
        };

        PictureBox picTree = new()
        {
            Image = IconCatalog.Get(AppIcon.Tree, CambiasoTheme.GoldAccent, 22),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Width = 28,
            Dock = DockStyle.Left
        };

        PictureBox picShield = new()
        {
            Image = IconCatalog.Get(AppIcon.Shield, CambiasoTheme.GoldAccent, 18),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Width = 28,
            Dock = DockStyle.Right
        };

        Label lblBannerTitle = new()
        {
            Text = "EDITOR DEL ÁRBOL DE ACTIVOS — EXCLUSIVO ADMINISTRADOR",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };

        headerBanner.Controls.Add(lblBannerTitle);
        headerBanner.Controls.Add(picTree);
        headerBanner.Controls.Add(picShield);

        // --- CONTENEDOR DIVIDIDO ---
        SplitContainer split = new()
        {
            Size = new Size(1120, 680),
            Dock = DockStyle.Fill,
            Panel1MinSize = 310,
            SplitterDistance = 400,
            BackColor = CambiasoTheme.BorderColor
        };
        ((System.ComponentModel.ISupportInitialize)split).BeginInit();
        split.SuspendLayout();
        Load += (_, _) =>
        {
            if (split.Width > 720)
            {
                split.SplitterDistance = 400;
            }
        };

        _treePanel = new AssetTreePanel(
            _ctx.MaintainerService,
            _ctx.AssetTreeService,
            _session,
            showCheckBoxes: true,
            includeInactive: true)
        {
            Dock = DockStyle.Fill
        };
        _treePanel.SelectedAssetChanged += (_, node) => RefreshNodeDetails(node);
        _treePanel.TreeModified += (_, _) => RefreshAnomaliesAndDetails();
        _treePanel.CheckedAssetsChanged += (_, _) => UpdateSelectionActionStates();
        _treePanel.InspectDependenciesRequested += (_, ids) => RunTreeAction(() => OpenDependencies(ids));
        split.Panel1.Controls.Add(_treePanel);

        // --- PANEL DERECHO ---
        Panel rightPanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = CambiasoTheme.Surface,
            Padding = new Padding(10)
        };

        // Botones de barra superior
        BtnAddChild = CambiasoTheme.CreateButton("Agregar Hijo", AppIcon.Add, ButtonTone.Primary, _toolTip, "Crear nuevo subnodo bajo el nodo seleccionado", width: 142, height: 30);
        BtnAddChild.Click += (_, _) => RunTreeAction(_treePanel.AddChildToSelected);

        BtnEdit = CambiasoTheme.CreateButton("Editar", AppIcon.Edit, ButtonTone.Accent, _toolTip, "Editar propiedades y ficha técnica del nodo seleccionado", width: 88, height: 30);
        BtnEdit.Click += (_, _) => RunTreeAction(_treePanel.EditSelected);

        BtnMove = CambiasoTheme.CreateButton("Mover", AppIcon.MoveRight, ButtonTone.Secondary, _toolTip, "Reubicar este nodo y su subárbol bajo otro padre", width: 88, height: 30);
        BtnMove.Click += (_, _) => RunTreeAction(_treePanel.MoveSelected);

        BtnInspectOts = CambiasoTheme.CreateButton("Consultar OT", AppIcon.Search, ButtonTone.Accent, _toolTip, "Consultar órdenes de trabajo asociadas a los nodos seleccionados o marcados", width: 122, height: 30);
        BtnInspectOts.Click += (_, _) => RunTreeAction(() => OpenDependencies(GetActionTargetIds()));

        BtnReassign = CambiasoTheme.CreateButton("Reasignar Rel.", AppIcon.MoveRight, ButtonTone.Secondary, _toolTip, "Reasignar OTs, proveedor o subnodos dependientes hacia otro activo o proveedor", width: 128, height: 30);
        BtnReassign.Click += (_, _) => RunTreeAction(() => OpenDependencies(GetActionTargetIds()));

        BtnActivate = CambiasoTheme.CreateButton("Activar", AppIcon.Power, ButtonTone.Primary, _toolTip, "Activar nodo seleccionado o nodos marcados", width: 92, height: 30);
        BtnActivate.Click += (_, _) => RunTreeAction(ActivateCheckedOrSelected);

        BtnDeactivate = CambiasoTheme.CreateButton("Desactivar", AppIcon.Ban, ButtonTone.Secondary, _toolTip, "Verificar dependencias y desactivar nodo(s) conservando historial", width: 108, height: 30);
        BtnDeactivate.Click += (_, _) => RunTreeAction(DeactivateCheckedOrSelected);

        BtnDelete = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Verificar dependencias y eliminar nodos compatibles sin romper OTs", width: 98, height: 30);
        BtnDelete.Click += (_, _) => RunTreeAction(DeleteCheckedOrSelected);

        BtnClearChecks = CambiasoTheme.CreateButton("Limpiar Sel.", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Desmarcar casillas del árbol", width: 132, height: 30);
        BtnClearChecks.Click += (_, _) => _treePanel.ClearChecked();

        BtnNormalize = CambiasoTheme.CreateButton("Normalizar", AppIcon.Check, ButtonTone.Secondary, _toolTip, "Recalcular columna nivel según profundidad real desde la raíz", width: 112, height: 30);
        BtnNormalize.Click += (_, _) => NormalizeLevels();

        BtnExport = CambiasoTheme.CreateButton("Exportar Rama", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar subárbol seleccionado a Excel, PDF, Word o CSV", width: 148, height: 30);
        BtnExport.Click += (_, _) =>
        {
            int rootId = _treePanel.SelectedAsset?.IdActivo ?? 1;
            string crumb = string.IsNullOrWhiteSpace(_treePanel.CurrentBreadcrumb) ? "CAMBIASO HNOS S.A.C." : _treePanel.CurrentBreadcrumb;
            ExportUiHelper.ShowReportExportMenu(
                BtnExport,
                this,
                _ctx.DocumentExportService,
                "SISPRO_ArbolActivos",
                () => DocumentExportService.BuildAssetTreeReport(
                    _ctx.AssetTreeService.GetSubtreeForExport(rootId, includeInactive: true),
                    $"Rama exportada: {crumb} (incluye inactivos)"),
                file => _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Árbol de Activos", "EXPORTAR_ARBOL", $"Exportado subárbol #{rootId} a {Path.GetFileName(file)}."));
        };

        BtnRefresh = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip, "Recargar árbol conservando la selección actual", width: 104, height: 30);
        BtnRefresh.Click += (_, _) =>
        {
            _treePanel.ReloadPreservingSelection();
            RefreshAnomaliesAndDetails();
        };

        GroupBox grpActions = CambiasoTheme.CreateGroupBox("Acciones del Administrador sobre el Árbol (soporta selección múltiple y clic derecho)");
        grpActions.Dock = DockStyle.Top;
        grpActions.Height = 146;
        FlowLayoutPanel actionFlow = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = new Padding(2, 2, 2, 2)
        };
        foreach (Button b in new[] { BtnAddChild, BtnEdit, BtnMove, BtnInspectOts, BtnReassign, BtnActivate, BtnDeactivate, BtnDelete, BtnClearChecks, BtnNormalize, BtnExport, BtnRefresh })
        {
            b.Margin = new Padding(0, 2, 6, 4);
        }
        actionFlow.Controls.AddRange([BtnAddChild, BtnEdit, BtnMove, BtnInspectOts, BtnReassign, BtnActivate, BtnDeactivate, BtnDelete, BtnClearChecks, BtnNormalize, BtnExport, BtnRefresh]);
        grpActions.Controls.Add(actionFlow);

        // Panel estado anomalías
        Panel statusStrip = new()
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(10, 6, 10, 4),
            Margin = new Padding(0, 6, 0, 6)
        };
        _lblAnomalies = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft
        };
        statusStrip.Controls.Add(_lblAnomalies);

        // Leyenda de tipos de nodo
        GroupBox grpLegend = CambiasoTheme.CreateGroupBox("Tipos de Nodo y Jerarquía Estructural");
        grpLegend.Dock = DockStyle.Bottom;
        grpLegend.Height = 82;
        FlowLayoutPanel legendFlow = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            Padding = new Padding(4, 4, 4, 2)
        };
        AddLegendItem(legendFlow, AppIcon.Factory, CambiasoTheme.GoldAccent, "Empresa");
        AddLegendItem(legendFlow, AppIcon.Factory, CambiasoTheme.Primary, "Planta");
        AddLegendItem(legendFlow, AppIcon.Layers, CambiasoTheme.Accent, "Sección / Área");
        AddLegendItem(legendFlow, AppIcon.Cpu, CambiasoTheme.Primary, "Máquina");
        AddLegendItem(legendFlow, AppIcon.Package, CambiasoTheme.TextPrimary, "Módulo");
        AddLegendItem(legendFlow, AppIcon.Gauge, CambiasoTheme.TextMuted, "Componente");
        AddLegendItem(legendFlow, AppIcon.Tree, Color.Gray, "Sin clasificar");
        grpLegend.Controls.Add(legendFlow);

        // Ficha del nodo seleccionado
        GroupBox grpDetails = CambiasoTheme.CreateGroupBox("Nodo Seleccionado — Información Estructural y Ficha Técnica");
        grpDetails.Dock = DockStyle.Fill;

        TableLayoutPanel infoGrid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 7,
            Padding = new Padding(8, 6, 8, 6)
        };
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
        infoGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 6; r++)
        {
            infoGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        }
        infoGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblIdVal = CreateValueLabel(bold: true);
        _lblTipoVal = CreateValueLabel(bold: true);
        _lblNivelVal = CreateValueLabel(bold: true);
        _lblEstadoVal = CreateValueLabel(bold: true);
        _lblRutaVal = CreateValueLabel(bold: false);
        _lblHijosVal = CreateValueLabel(bold: false);
        _lblRefsVal = CreateValueLabel(bold: true);
        _lblSapVal = CreateValueLabel(bold: false);
        _lblEquipoVal = CreateValueLabel(bold: false);
        _txtObsVal = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = CambiasoTheme.Surface,
            ForeColor = CambiasoTheme.TextPrimary
        };

        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("ID del Nodo:"), 0, 0);
        infoGrid.Controls.Add(_lblIdVal, 1, 0);
        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Tipo Estructural:"), 2, 0);
        infoGrid.Controls.Add(_lblTipoVal, 3, 0);

        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Nivel Jerárquico:"), 0, 1);
        infoGrid.Controls.Add(_lblNivelVal, 1, 1);
        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Estado:"), 2, 1);
        infoGrid.Controls.Add(_lblEstadoVal, 3, 1);

        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Subnodos:"), 0, 2);
        infoGrid.Controls.Add(_lblHijosVal, 1, 2);
        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("OTs Asociadas:"), 2, 2);
        infoGrid.Controls.Add(_lblRefsVal, 3, 2);

        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Código SAP:"), 0, 3);
        infoGrid.Controls.Add(_lblSapVal, 1, 3);
        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Equipo / Marca:"), 2, 3);
        infoGrid.Controls.Add(_lblEquipoVal, 3, 3);

        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Ruta Completa:"), 0, 4);
        infoGrid.Controls.Add(_lblRutaVal, 1, 4);
        infoGrid.SetColumnSpan(_lblRutaVal, 3);
        infoGrid.SetRowSpan(_lblRutaVal, 2);

        infoGrid.Controls.Add(CambiasoTheme.CreateFieldLabel("Observaciones:"), 0, 6);
        infoGrid.Controls.Add(_txtObsVal, 1, 6);
        infoGrid.SetColumnSpan(_txtObsVal, 3);

        grpDetails.Controls.Add(infoGrid);

        rightPanel.Controls.Add(grpDetails);
        rightPanel.Controls.Add(statusStrip);
        rightPanel.Controls.Add(grpActions);
        rightPanel.Controls.Add(grpLegend);
        split.Panel2.Controls.Add(rightPanel);

        Controls.Add(split);
        Controls.Add(headerBanner);
        ((System.ComponentModel.ISupportInitialize)split).EndInit();
        split.ResumeLayout(false);
        ResumeLayout(false);

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;

        RefreshAnomaliesAndDetails();
    }

    private void RunTreeAction(Action action)
    {
        _suppressSyncRefresh = true;
        try
        {
            action();
        }
        finally
        {
            _suppressSyncRefresh = false;
        }
    }

    private void NormalizeLevels()
    {
        int anomalies = _ctx.AssetTreeService.CountLevelAnomalies();
        string question = anomalies > 0
            ? $"Se detectaron {anomalies} nodo(s) con nivel guardado distinto a su profundidad real.\n\n¿Desea recalcular automáticamente la columna nivel de todo el árbol?"
            : "No se detectaron anomalías, ¿desea ejecutar de todas formas la verificación/normalización de niveles?";

        if (!CambiasoTheme.Confirm(this, question))
        {
            return;
        }

        _suppressSyncRefresh = true;
        try
        {
            int updated = _ctx.AssetTreeService.NormalizeLevels(_session);
            _treePanel.ReloadPreservingSelection();
            RefreshAnomaliesAndDetails();
            CambiasoTheme.ShowInfo(this, $"Normalización completada: {updated} nodo(s) actualizados.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
        finally
        {
            _suppressSyncRefresh = false;
        }
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || _suppressSyncRefresh || (e.Kind & DataChangeKind.Catalogs) == 0)
        {
            return;
        }

        void RefreshView()
        {
            if (IsDisposed)
            {
                return;
            }

            _treePanel.ReloadPreservingSelection();
            RefreshAnomaliesAndDetails();
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke(RefreshView);
        }
        else if (!InvokeRequired)
        {
            RefreshView();
        }
    }

    private void RefreshAnomaliesAndDetails()
    {
        int anomalies = _ctx.AssetTreeService.CountLevelAnomalies();
        if (anomalies > 0)
        {
            _lblAnomalies.Text = $"Anomalías de nivel detectadas: {anomalies} nodo(s) — Use 'Normalizar Niveles' para corregir su profundidad registrada.";
            _lblAnomalies.ForeColor = CambiasoTheme.Warning;
        }
        else
        {
            _lblAnomalies.Text = "Anomalías de nivel detectadas: 0 — Jerarquía de niveles 100% consistente.";
            _lblAnomalies.ForeColor = CambiasoTheme.Success;
        }

        RefreshNodeDetails(_treePanel.SelectedAsset);
    }

    private void RefreshNodeDetails(AssetNode? node)
    {
        if (node is null)
        {
            _lblIdVal.Text = "-";
            _lblTipoVal.Text = "-";
            _lblNivelVal.Text = "-";
            _lblEstadoVal.Text = "-";
            _lblRutaVal.Text = "Seleccione un nodo en el árbol izquierdo.";
            _lblHijosVal.Text = "-";
            _lblRefsVal.Text = "-";
            _lblSapVal.Text = "-";
            _lblEquipoVal.Text = "-";
            _txtObsVal.Clear();

            BtnAddChild.Enabled = false;
            BtnEdit.Enabled = false;
            BtnMove.Enabled = false;
            UpdateSelectionActionStates();
            return;
        }

        AssetNodeInfo info;
        try
        {
            info = _ctx.AssetTreeService.GetNodeInfo(node.IdActivo);
        }
        catch
        {
            info = new AssetNodeInfo { Node = node, Depth = Math.Max(1, node.Nivel), Path = _treePanel.CurrentBreadcrumb };
        }

        _lblIdVal.Text = $"#{info.Node.IdActivo}";
        _lblTipoVal.Text = AssetNodeTypes.GetLabel(info.Node.Categoria);

        bool levelMismatch = info.Node.Nivel != info.Depth;
        _lblNivelVal.Text = levelMismatch
            ? $"Nivel {info.Depth} de 10 (guardado: {info.Node.Nivel})"
            : $"Nivel {info.Depth} de 10";
        _lblNivelVal.ForeColor = levelMismatch ? CambiasoTheme.Warning : CambiasoTheme.Primary;

        _lblEstadoVal.Text = info.Node.Activo ? "Activo" : "Inactivo";
        _lblEstadoVal.ForeColor = info.Node.Activo ? CambiasoTheme.Success : CambiasoTheme.Danger;
        _lblRutaVal.Text = info.Path;
        _lblHijosVal.Text = $"{info.ActiveChildren} activos / {info.TotalChildren} totales";
        _lblRefsVal.Text = $"{info.WorkOrderReferences:N0}";
        _lblRefsVal.ForeColor = info.WorkOrderReferences > 0 ? CambiasoTheme.Warning : CambiasoTheme.TextPrimary;
        _lblSapVal.Text = string.IsNullOrWhiteSpace(info.Node.CodSap) ? "(Sin código)" : info.Node.CodSap;

        string eqSummary = string.Join(" · ", new[] { info.Node.Fabricante, info.Node.Modelo, info.Node.Serie }.Where(s => !string.IsNullOrWhiteSpace(s)));
        _lblEquipoVal.Text = string.IsNullOrWhiteSpace(eqSummary) ? "(Sin datos)" : eqSummary;
        _txtObsVal.Text = info.Node.Observacion;

        BtnAddChild.Enabled = info.Node.Activo && info.Depth < 10;
        BtnEdit.Enabled = true;
        BtnMove.Enabled = info.Node.ParentId.HasValue;
        UpdateSelectionActionStates();
    }

    private IReadOnlyList<int> GetActionTargetIds()
    {
        if (_treePanel.CheckedAssetIds.Count > 0)
        {
            return _treePanel.CheckedAssetIds;
        }

        return _treePanel.SelectedAsset is not null ? [_treePanel.SelectedAsset.IdActivo] : [];
    }

    private void OpenDependencies(IReadOnlyCollection<int> targetIds)
    {
        if (MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Activo, targetIds, RefreshAfterBatch))
        {
            RefreshAnomaliesAndDetails();
        }
    }

    private void ActivateCheckedOrSelected()
    {
        if (_treePanel.CheckedAssetIds.Count > 0)
        {
            MaintainerBatchResult res = _ctx.AssetTreeService.SetActiveBatch(_session, _treePanel.CheckedAssetIds, true, includeDescendants: true);
            CambiasoTheme.ShowInfo(this, res.BuildSummary());
            RefreshAfterBatch();
            return;
        }

        _treePanel.SetSelectedActive(true);
    }

    private void DeactivateCheckedOrSelected()
    {
        if (MaintainerBatchUiHelper.DeactivateChecked(this, _ctx, _session, MaintainerEntityKind.Activo, GetActionTargetIds(), RefreshAfterBatch))
        {
            RefreshAnomaliesAndDetails();
        }
    }

    private void DeleteCheckedOrSelected()
    {
        if (MaintainerBatchUiHelper.DeleteChecked(this, _ctx, _session, MaintainerEntityKind.Activo, GetActionTargetIds(), RefreshAfterBatch))
        {
            RefreshAnomaliesAndDetails();
        }
    }

    private void RefreshAfterBatch()
    {
        _treePanel.ClearChecked();
        _treePanel.ReloadPreservingSelection();
        RefreshAnomaliesAndDetails();
    }

    private void UpdateSelectionActionStates()
    {
        int checkedCount = _treePanel.CheckedAssetIds.Count;
        AssetNode? sel = _treePanel.SelectedAsset;
        bool hasTarget = checkedCount > 0 || sel is not null;

        BtnInspectOts.Enabled = hasTarget;
        BtnReassign.Enabled = hasTarget;
        BtnClearChecks.Enabled = checkedCount > 0;
        BtnActivate.Enabled = checkedCount > 0 || (sel is not null && !sel.Activo);
        BtnDeactivate.Enabled = checkedCount > 0 || (sel is not null && sel.Activo && sel.ParentId.HasValue);
        BtnDelete.Enabled = checkedCount > 0 || (sel is not null && sel.ParentId.HasValue);
    }

    private static Label CreateValueLabel(bool bold) => new()
    {
        Dock = DockStyle.Fill,
        Font = bold ? CambiasoTheme.LabelFont : CambiasoTheme.BodyFont,
        ForeColor = CambiasoTheme.TextPrimary,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static void AddLegendItem(FlowLayoutPanel panel, AppIcon icon, Color color, string text)
    {
        FlowLayoutPanel chip = new()
        {
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0, 2, 12, 2)
        };
        chip.Controls.Add(new PictureBox
        {
            Image = IconCatalog.Get(icon, color, 16),
            Size = new Size(18, 18),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Margin = new Padding(0, 1, 4, 0)
        });
        chip.Controls.Add(new Label
        {
            Text = text,
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextPrimary,
            Margin = new Padding(0, 2, 0, 0)
        });
        panel.Controls.Add(chip);
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
