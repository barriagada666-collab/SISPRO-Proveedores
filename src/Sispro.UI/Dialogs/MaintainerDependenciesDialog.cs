using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo integral para consultar órdenes de trabajo asociadas a registros seleccionados en
/// Proveedores, Solicitantes, Firmas o Árbol de Activos, y ejecutar acciones administrativas seguras:
/// desactivar conservando historial, reasignar relaciones, reasignar subnodos, eliminar compatibles
/// y eliminar OTs relacionadas únicamente bajo acción explícita con confirmación doble.
/// </summary>
public sealed class MaintainerDependenciesDialog : Form
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly bool _isAdmin;
    private readonly Label _lblSelectionSummary;
    private readonly Label _lblRelationStats;
    private readonly DataGridView _gridRelations;

    private readonly ComboBox? _cmbTargetProvider;
    private readonly ComboBox? _cmbTargetRequester;
    private readonly Button? _btnDeactivate;
    private readonly Button? _btnDeleteCompatible;
    private readonly Button? _btnReassignMain;
    private readonly Button? _btnReassignProviderAsset;
    private readonly Button? _btnReassignChildNodes;
    private readonly Button? _btnDeleteRelatedOts;

    private MaintainerDependencyReport _report;

    public bool ChangesApplied { get; private set; }

    public MaintainerDependenciesDialog(
        SisproApplicationContext ctx,
        UserSession session,
        MaintainerDependencyReport initialReport)
    {
        _ctx = ctx;
        _session = session;
        _report = initialReport;
        _isAdmin = initialReport.EntityKind == MaintainerEntityKind.Activo
            ? ctx.AssetTreeService.IsAdministrator(session)
            : ctx.MaintainerService.IsAdministrator(session);

        string entityTitle = GetEntityTitle(initialReport.EntityKind);
        Text = $"Gestión de Relaciones y Dependencias ({entityTitle}) — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(920, 640);
        MinimumSize = new Size(780, 520);
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        // Encabezado
        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = CambiasoTheme.PrimaryDark,
            Padding = new Padding(16, 10, 16, 10)
        };
        header.Controls.Add(new Label
        {
            Text = $"CONSULTA DE OT ASOCIADAS Y ACCIONES SEGURAS — {entityTitle.ToUpperInvariant()}",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });

        // Resumen superior
        Panel summaryPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(14, 8, 14, 8)
        };
        _lblSelectionSummary = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            AutoEllipsis = true
        };
        _lblRelationStats = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.BodyFont,
            ForeColor = CambiasoTheme.TextPrimary
        };
        summaryPanel.Controls.Add(_lblRelationStats);
        summaryPanel.Controls.Add(_lblSelectionSummary);

        // Grilla de OTs asociadas
        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Órdenes de Trabajo Relacionadas (Conservación de Historial)");
        grpGrid.Dock = DockStyle.Fill;
        _gridRelations = new DataGridView { Name = "gridDependenciasOt", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridRelations);
        grpGrid.Controls.Add(_gridRelations);

        // Panel inferior de acciones administrativas
        GroupBox grpActions = CambiasoTheme.CreateGroupBox(
            _isAdmin
                ? "Acciones Administrativas (Historial, Reasignación e Integridad)"
                : "Solo consulta — Las acciones de modificación/eliminación/reasignación son exclusivas del Administrador");
        grpActions.Dock = DockStyle.Bottom;
        grpActions.Height = initialReport.EntityKind == MaintainerEntityKind.Activo ? 156 : 122;

        TableLayoutPanel actionLayout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4)
        };
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        actionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));

        FlowLayoutPanel reassignRow = new() { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 2, 0, 2) };
        FlowLayoutPanel actionRow = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };

        if (initialReport.EntityKind == MaintainerEntityKind.Proveedor)
        {
            _cmbTargetProvider = BuildActiveProvidersCombo();
            _btnReassignMain = CambiasoTheme.CreateButton("Reasignar Proveedor en OTs", AppIcon.MoveRight, ButtonTone.Accent, null,
                "Cambiar el proveedor en todas las OT asociadas hacia el proveedor activo elegido", width: 220, height: 30);
            _btnReassignMain.Click += (_, _) => ExecuteReassignProvider();
            reassignRow.Controls.Add(new Label { Text = "Proveedor destino:", AutoSize = true, Font = CambiasoTheme.LabelFont, Margin = new Padding(2, 7, 6, 0) });
            reassignRow.Controls.Add(_cmbTargetProvider);
            reassignRow.Controls.Add(_btnReassignMain);
        }
        else if (initialReport.EntityKind is MaintainerEntityKind.Solicitante or MaintainerEntityKind.Firma)
        {
            bool onlyWithSig = initialReport.EntityKind == MaintainerEntityKind.Firma;
            _cmbTargetRequester = BuildActiveRequestersCombo(onlyWithSig);
            _btnReassignMain = CambiasoTheme.CreateButton(
                onlyWithSig ? "Reasignar Firma en OTs" : "Reasignar Solicitante en OTs",
                AppIcon.MoveRight,
                ButtonTone.Accent,
                null,
                "Reasignar las OT relacionadas hacia otro registro activo",
                width: 224,
                height: 30);
            _btnReassignMain.Click += (_, _) => ExecuteReassignRequester(onlyWithSig);
            reassignRow.Controls.Add(new Label
            {
                Text = onlyWithSig ? "Firma / Solicitante destino:" : "Solicitante destino:",
                AutoSize = true,
                Font = CambiasoTheme.LabelFont,
                Margin = new Padding(2, 7, 6, 0)
            });
            reassignRow.Controls.Add(_cmbTargetRequester);
            reassignRow.Controls.Add(_btnReassignMain);
        }
        else if (initialReport.EntityKind == MaintainerEntityKind.Activo)
        {
            _btnReassignMain = CambiasoTheme.CreateButton("Cambiar Activo en OTs…", AppIcon.Tree, ButtonTone.Accent, null,
                "Seleccionar otro nodo activo en el árbol para reasignar las OTs asociadas", width: 196, height: 30);
            _btnReassignMain.Click += (_, _) => ExecuteReassignAssetNodeWorkOrders();

            _btnReassignChildNodes = CambiasoTheme.CreateButton("Reasignar Subnodos…", AppIcon.MoveRight, ButtonTone.Secondary, null,
                "Mover subnodos dependientes hacia otro nodo padre activo", width: 174, height: 30);
            _btnReassignChildNodes.Click += (_, _) => ExecuteReassignChildNodes();

            _cmbTargetProvider = BuildActiveProvidersCombo();
            _cmbTargetProvider.Width = 220;
            _btnReassignProviderAsset = CambiasoTheme.CreateButton("Cambiar Proveedor OTs", AppIcon.Edit, ButtonTone.Secondary, null,
                "Reasignar el proveedor de las OT asociadas a los activos seleccionados", width: 186, height: 30);
            _btnReassignProviderAsset.Click += (_, _) => ExecuteReassignAssetProvider();

            reassignRow.Controls.AddRange([_btnReassignMain, _btnReassignChildNodes, _cmbTargetProvider, _btnReassignProviderAsset]);
        }

        _btnDeactivate = CambiasoTheme.CreateButton("Desactivar Seleccionados", AppIcon.Ban, ButtonTone.Secondary, null,
            "Desactivar conservando todo el historial y bloqueando uso en nuevas OTs", width: 198, height: 30);
        _btnDeactivate.Click += (_, _) => ExecuteDeactivate();

        string delText = initialReport.EntityKind == MaintainerEntityKind.Firma
            ? "Eliminar Firma(s) Compatibles"
            : "Eliminar Registros Compatibles";
        _btnDeleteCompatible = CambiasoTheme.CreateButton(delText, AppIcon.Delete, ButtonTone.Primary, null,
            "Eliminar registros sin conflictos de integridad (omite los que aún tienen OTs/subnodos)", width: 230, height: 30);
        _btnDeleteCompatible.Click += (_, _) => ExecuteDeleteCompatible();

        _btnDeleteRelatedOts = CambiasoTheme.CreateButton("Eliminar OTs Asociadas…", AppIcon.Delete, ButtonTone.Danger, null,
            "Acción explícita irreversible: requiere doble confirmación del Administrador", width: 205, height: 30);
        _btnDeleteRelatedOts.Click += (_, _) => ExecuteDeleteRelatedWorkOrdersExplicitly();

        Button btnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Cancel, ButtonTone.Secondary, null, "Cerrar ventana", width: 100, height: 30);
        btnClose.Click += (_, _) => Close();

        actionRow.Controls.AddRange([_btnDeactivate, _btnDeleteCompatible, _btnDeleteRelatedOts, btnClose]);
        actionLayout.Controls.Add(reassignRow, 0, 0);
        actionLayout.Controls.Add(actionRow, 0, 1);
        grpActions.Controls.Add(actionLayout);

        Controls.Add(grpGrid);
        Controls.Add(grpActions);
        Controls.Add(summaryPanel);
        Controls.Add(header);

        BindReport();
    }

    private void BindReport()
    {
        string selectedPreview = _report.SelectedLabels.Count == 0
            ? "(Ninguno)"
            : string.Join(", ", _report.SelectedLabels.Take(4)) + (_report.SelectedLabels.Count > 4 ? $" (+{_report.SelectedLabels.Count - 4} más)" : string.Empty);

        _lblSelectionSummary.Text = $"Registros seleccionados ({_report.SelectedIds.Count}): {selectedPreview}";

        string subInfo = _report.EntityKind == MaintainerEntityKind.Activo
            ? $"   ·   Subnodos descendientes: {_report.DependentChildNodesCount:N0}"
            : string.Empty;
        _lblRelationStats.Text = _report.RelatedWorkOrders.Count > 0
            ? $"ATENCIÓN: Existen {_report.RelatedWorkOrders.Count:N0} orden(es) de trabajo relacionada(s){subInfo}. No se eliminarán OTs automáticamente."
            : $"Sin órdenes de trabajo asociadas directamente{subInfo}. Puede desactivar o eliminar registros compatibles con seguridad.";
        _lblRelationStats.ForeColor = _report.RelatedWorkOrders.Count > 0 ? CambiasoTheme.Warning : CambiasoTheme.Success;

        _gridRelations.DataSource = new SortableBindingList<RelatedWorkOrderGridRow>(
            _report.RelatedWorkOrders.Select(r => new RelatedWorkOrderGridRow(r)));
        if (_gridRelations.Columns.Count >= 6)
        {
            GridBindingHelper.ConfigureColumn(_gridRelations, 0, "N° OT", 10);
            GridBindingHelper.ConfigureColumn(_gridRelations, 1, "Fecha", 11, "dd-MM-yyyy", DataGridViewContentAlignment.MiddleCenter);
            GridBindingHelper.ConfigureColumn(_gridRelations, 2, "Estado", 10);
            GridBindingHelper.ConfigureColumn(_gridRelations, 3, "Relación", 20);
            GridBindingHelper.ConfigureColumn(_gridRelations, 4, "Registro", 24);
            GridBindingHelper.ConfigureColumn(_gridRelations, 5, "Detalle / Info", 25);
        }

        bool hasIds = _report.SelectedIds.Count > 0;
        bool hasOts = _report.RelatedWorkOrders.Count > 0;

        if (_btnDeactivate is not null) _btnDeactivate.Enabled = _isAdmin && hasIds;
        if (_btnDeleteCompatible is not null) _btnDeleteCompatible.Enabled = _isAdmin && hasIds;
        if (_btnReassignMain is not null)
        {
            _btnReassignMain.Enabled = _isAdmin && hasIds && (hasOts || _report.EntityKind == MaintainerEntityKind.Activo);
        }
        if (_btnReassignProviderAsset is not null) _btnReassignProviderAsset.Enabled = _isAdmin && hasIds && hasOts;
        if (_btnReassignChildNodes is not null) _btnReassignChildNodes.Enabled = _isAdmin && _report.DependentChildNodesCount > 0;
        if (_btnDeleteRelatedOts is not null)
        {
            _btnDeleteRelatedOts.Visible = _isAdmin;
            _btnDeleteRelatedOts.Enabled = _isAdmin && hasOts;
        }
    }

    private void ReloadDependencies()
    {
        _report = _report.EntityKind == MaintainerEntityKind.Activo
            ? _ctx.AssetTreeService.InspectDependencies(_session, _report.SelectedIds)
            : _ctx.MaintainerService.InspectDependencies(_session, _report.EntityKind, _report.SelectedIds);
        BindReport();
    }

    private void ExecuteDeactivate()
    {
        if (!CambiasoTheme.Confirm(this, "¿Desactivar los registros seleccionados conservando todo su historial de OTs?"))
        {
            return;
        }

        try
        {
            MaintainerBatchResult res = _report.EntityKind switch
            {
                MaintainerEntityKind.Proveedor => _ctx.MaintainerService.SetProvidersActiveBatch(_session, _report.SelectedIds, false),
                MaintainerEntityKind.Solicitante or MaintainerEntityKind.Firma => _ctx.MaintainerService.SetRequestersActiveBatch(_session, _report.SelectedIds, false),
                MaintainerEntityKind.Activo => _ctx.AssetTreeService.SetActiveBatch(_session, _report.SelectedIds, false, includeDescendants: true),
                _ => throw new InvalidOperationException("Tipo no soportado.")
            };

            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, res.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteDeleteCompatible()
    {
        string confirmMsg = _report.EntityKind == MaintainerEntityKind.Firma
            ? "¿Eliminar las firmas digitales de los registros compatibles (sin OTs pendientes)?"
            : "¿Eliminar los registros seleccionados que no posean conflictos de integridad?\nLos registros con OTs o subnodos serán omitidos de forma segura.";

        if (!CambiasoTheme.Confirm(this, confirmMsg))
        {
            return;
        }

        try
        {
            MaintainerBatchResult res = _report.EntityKind switch
            {
                MaintainerEntityKind.Proveedor => _ctx.MaintainerService.DeleteProvidersBatch(_session, _report.SelectedIds),
                MaintainerEntityKind.Solicitante => _ctx.MaintainerService.DeleteRequestersBatch(_session, _report.SelectedIds),
                MaintainerEntityKind.Firma => _ctx.MaintainerService.ClearSignaturesBatch(_session, _report.SelectedIds, skipWithWorkOrders: true),
                MaintainerEntityKind.Activo => _ctx.AssetTreeService.DeleteNodesBatch(_session, _report.SelectedIds),
                _ => throw new InvalidOperationException("Tipo no soportado.")
            };

            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, res.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteReassignProvider()
    {
        if (_cmbTargetProvider?.SelectedItem is not IdNameItem target)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un proveedor activo de destino.");
            return;
        }

        if (!CambiasoTheme.Confirm(this, $"¿Reasignar todas las OTs relacionadas hacia '{target.Label}'?"))
        {
            return;
        }

        try
        {
            int count = _ctx.MaintainerService.ReassignProviderWorkOrders(_session, _report.SelectedIds, target.Id);
            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, $"Se reasignaron {count:N0} orden(es) de trabajo hacia '{target.Label}'.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteReassignRequester(bool requireSignature)
    {
        if (_cmbTargetRequester?.SelectedItem is not IdNameItem target)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un registro destino activo.");
            return;
        }

        string noun = requireSignature ? "la firma y solicitante" : "el solicitante";
        if (!CambiasoTheme.Confirm(this, $"¿Reasignar {noun} de las OTs relacionadas hacia '{target.Label}'?"))
        {
            return;
        }

        try
        {
            int count = _ctx.MaintainerService.ReassignRequesterWorkOrders(_session, _report.SelectedIds, target.Id, requireSignature);
            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, $"Se reasignaron {count:N0} orden(es) de trabajo hacia '{target.Label}'.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteReassignAssetNodeWorkOrders()
    {
        using AssetNodePickerDialog picker = new(_ctx.MaintainerService, "Seleccionar Activo Destino para Reasignar OTs");
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedNode is null)
        {
            return;
        }

        try
        {
            int count = _ctx.AssetTreeService.ReassignWorkOrdersNode(_session, _report.SelectedIds, picker.SelectedNode.IdActivo, includeDescendants: true);
            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, $"Se reasignaron {count:N0} orden(es) de trabajo al activo '{picker.SelectedNode.Descripcion}'.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteReassignAssetProvider()
    {
        if (_cmbTargetProvider?.SelectedItem is not IdNameItem target)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un proveedor activo de destino.");
            return;
        }

        try
        {
            int count = _ctx.AssetTreeService.ReassignWorkOrdersProvider(_session, _report.SelectedIds, target.Id, includeDescendants: true);
            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, $"Se actualizó el proveedor a '{target.Label}' en {count:N0} OT(s) asociada(s).");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteReassignChildNodes()
    {
        using AssetNodePickerDialog picker = new(_ctx.MaintainerService, "Seleccionar Nuevo Nodo Padre para los Subnodos");
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedNode is null)
        {
            return;
        }

        try
        {
            MaintainerBatchResult res = _ctx.AssetTreeService.ReassignChildrenAndMove(_session, _report.SelectedIds, picker.SelectedNode.IdActivo);
            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, res.BuildSummary());
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ExecuteDeleteRelatedWorkOrdersExplicitly()
    {
        List<int> distinctOtIds = _report.RelatedWorkOrders.Select(o => o.IdOt).Where(id => id > 0).Distinct().ToList();
        if (distinctOtIds.Count == 0)
        {
            return;
        }

        string otSample = string.Join(", ", _report.RelatedWorkOrders.Take(8).Select(o => $"#{o.NumeroOt}"));
        if (_report.RelatedWorkOrders.Count > 8)
        {
            otSample += $" (+{_report.RelatedWorkOrders.Count - 8} más)";
        }

        // Primera confirmación: advertencia explícita
        if (!CambiasoTheme.Confirm(
                this,
                $"ADVERTENCIA CRÍTICA:\nEstá por ELIMINAR PERMANENTEMENTE {distinctOtIds.Count:N0} orden(es) de trabajo asociada(s) ({otSample}).\n\n¿Desea continuar al paso de confirmación final?"))
        {
            return;
        }

        // Segunda confirmación explícita reforzada con botón predeterminado = No
        DialogResult finalConfirm = MessageBox.Show(
            this,
            $"CONFIRMACIÓN FINAL IRREVERSIBLE:\n\nSe eliminarán definitivamente {distinctOtIds.Count:N0} OT(s), sus detalles y vistos buenos.\nEsta operación quedará registrada en Auditoría y NO puede deshacerse.\n\n¿Confirma eliminar estas órdenes de trabajo?",
            "SISPRO — Confirmación Doble Obligatoria",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (finalConfirm != DialogResult.Yes)
        {
            return;
        }

        try
        {
            string desc = $"{_report.EntityKind}: {string.Join(", ", _report.SelectedLabels.Take(3))}";
            int deleted = _report.EntityKind == MaintainerEntityKind.Activo
                ? _ctx.AssetTreeService.DeleteRelatedWorkOrdersExplicitly(_session, distinctOtIds, desc)
                : _ctx.MaintainerService.DeleteRelatedWorkOrdersExplicitly(_session, distinctOtIds, desc);

            ChangesApplied = true;
            ReloadDependencies();
            CambiasoTheme.ShowInfo(this, $"Se eliminaron explícitamente {deleted:N0} orden(es) de trabajo.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private ComboBox BuildActiveProvidersCombo()
    {
        ComboBox cmb = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, Margin = new Padding(0, 2, 8, 2) };
        HashSet<int> excluded = _report.EntityKind == MaintainerEntityKind.Proveedor ? [.. _report.SelectedIds] : [];
        foreach (Provider p in _ctx.MaintainerService.GetProviders(includeInactive: false).Where(p => !excluded.Contains(p.IdProv)))
        {
            cmb.Items.Add(new IdNameItem(p.IdProv, p.Nombre));
        }

        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        return cmb;
    }

    private ComboBox BuildActiveRequestersCombo(bool onlyWithSignature)
    {
        ComboBox cmb = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280, Margin = new Padding(0, 2, 8, 2) };
        HashSet<int> excluded = [.. _report.SelectedIds];
        IEnumerable<Requester> candidates = _ctx.MaintainerService.GetRequesters(includeInactive: false)
            .Where(r => !excluded.Contains(r.IdSol) && (!onlyWithSignature || r.TieneFirma));
        foreach (Requester r in candidates)
        {
            string sigMark = r.TieneFirma ? " [Firma OK]" : string.Empty;
            cmb.Items.Add(new IdNameItem(r.IdSol, $"{r.Alias} — {r.NombreCompleto}{sigMark}"));
        }

        if (cmb.Items.Count > 0) cmb.SelectedIndex = 0;
        return cmb;
    }

    private static string GetEntityTitle(MaintainerEntityKind kind) => kind switch
    {
        MaintainerEntityKind.Proveedor => "Proveedores",
        MaintainerEntityKind.Solicitante => "Solicitantes",
        MaintainerEntityKind.Firma => "Firmas Digitales",
        MaintainerEntityKind.Activo => "Árbol de Activos",
        _ => kind.ToString()
    };

    private sealed record IdNameItem(int Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class RelatedWorkOrderGridRow(RelatedWorkOrderInfo source) : IGridRowSource<RelatedWorkOrderInfo>
    {
        [Browsable(false)]
        public RelatedWorkOrderInfo Source { get; } = source;
        public int NumeroOt => Source.NumeroOt;
        public DateTime FechaOt => Source.FechaOt;
        public string Estado => Source.Estado.ToString();
        public string TipoRelacion => Source.TipoRelacion;
        public string RegistroAsociado => Source.RegistroAsociado;
        public string InfoAdicional => Source.InfoAdicional;
    }

    private sealed class AssetNodePickerDialog : Form
    {
        private readonly AssetTreePanel _tree;
        public AssetNode? SelectedNode { get; private set; }

        public AssetNodePickerDialog(MaintainerService maintainerService, string title)
        {
            Text = $"{title} — SISPRO";
            Icon = IconCatalog.GetAppIcon();
            Size = new Size(520, 500);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = CambiasoTheme.Surface;
            Font = CambiasoTheme.BodyFont;

            _tree = new AssetTreePanel(maintainerService) { Dock = DockStyle.Fill, IncludeInactive = false };
            FlowLayoutPanel footer = new() { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 8, 10, 8) };
            Button btnOk = CambiasoTheme.CreateButton("Seleccionar Destino", AppIcon.Check, ButtonTone.Primary, null, "Confirmar nodo destino", width: 174);
            btnOk.Click += (_, _) =>
            {
                if (_tree.SelectedAsset is null)
                {
                    CambiasoTheme.ShowWarning(this, "Seleccione un nodo del árbol.");
                    return;
                }

                SelectedNode = _tree.SelectedAsset;
                DialogResult = DialogResult.OK;
                Close();
            };
            Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, null, "Cancelar selección", width: 110);
            btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

            footer.Controls.AddRange([btnOk, btnCancel]);
            Controls.Add(_tree);
            Controls.Add(footer);
        }
    }
}
