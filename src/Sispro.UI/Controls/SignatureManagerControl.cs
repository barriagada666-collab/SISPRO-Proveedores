using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Pestaña dedicada a la administración de Firmas Digitales asociadas a solicitantes OT:
/// vista previa (PNG/JPG/PDF), carga/reemplazo, selección múltiple por checkboxes, consulta de OT,
/// reasignación de firma entre solicitantes, desactivación y eliminación controlada por perfil Admin.
/// </summary>
public sealed class SignatureManagerControl : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();
    private readonly bool _isAdmin;

    private IReadOnlyList<Requester> _allRequesters = [];
    private int _selectedIdSol;
    private bool _syncSubscribed;
    private bool _suppressSelection;

    public event EventHandler? SignaturesChanged;

    internal CheckBox ChkSelectAllVisible { get; }
    internal Label LblSelectedCount { get; }
    internal Label LblStats { get; }
    internal TextBox TxtSearch { get; }
    internal ComboBox CmbFilter { get; }
    internal DataGridView GridSignatures { get; }
    internal GridCheckboxSelectionController<Requester> SelectionController { get; }

    internal PictureBox PicPreview { get; }
    internal Label LblPreviewTitle { get; }
    internal Label LblPreviewMeta { get; }

    internal Button BtnAttachReplace { get; }
    internal Button BtnInspectOts { get; }
    internal Button BtnReassign { get; }
    internal Button BtnDeactivateSelected { get; }
    internal Button BtnDeleteSelected { get; }
    internal Button BtnClearSelection { get; }
    internal Button BtnRefresh { get; }

    public SignatureManagerControl(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        _isAdmin = ctx.MaintainerService.IsAdministrator(session);

        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        ChkSelectAllVisible = new CheckBox
        {
            Name = "chkSelectAllFirmas",
            Text = "Seleccionar todas visibles",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            Margin = new Padding(0, 6, 10, 2)
        };
        LblSelectedCount = new Label
        {
            Text = "Seleccionadas: 0",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Margin = new Padding(0, 7, 12, 2)
        };
        LblStats = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(8, 7, 0, 2)
        };

        TxtSearch = new TextBox { Width = 220, PlaceholderText = "Buscar firma, alias o archivo..." };
        TxtSearch.TextChanged += (_, _) => ApplyFilterAndBind();

        CmbFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 195 };
        CmbFilter.Items.AddRange(["Todos los solicitantes", "Con firma digital (todas)", "Con firma y activos", "Sin firma adjunta", "Solo inactivos"]);
        CmbFilter.SelectedIndex = 0;
        CmbFilter.SelectedIndexChanged += (_, _) => ApplyFilterAndBind();

        BtnAttachReplace = CambiasoTheme.CreateButton("Adjuntar / Cambiar", AppIcon.FileText, ButtonTone.Primary, _toolTip,
            "Adjuntar o reemplazar la firma digital (PNG, JPG o PDF) del registro seleccionado", width: 158, height: 30);
        BtnAttachReplace.Click += (_, _) => AttachSignatureToCurrent();

        BtnInspectOts = CambiasoTheme.CreateButton("Consultar OT", AppIcon.Search, ButtonTone.Accent, _toolTip,
            "Consultar órdenes de trabajo asociadas a las firmas seleccionadas", width: 128, height: 30);
        BtnInspectOts.Click += (_, _) => InspectChecked();

        BtnReassign = CambiasoTheme.CreateButton("Reasignar Firma", AppIcon.MoveRight, ButtonTone.Secondary, _toolTip,
            "Reasignar OTs asociadas hacia otro solicitante con firma activa (solo Administrador)", width: 142, height: 30);
        BtnReassign.Click += (_, _) => InspectChecked();

        BtnDeactivateSelected = CambiasoTheme.CreateButton("Desactivar Sel.", AppIcon.Ban, ButtonTone.Secondary, _toolTip,
            "Desactivar solicitante/firma seleccionado conservando historial (solo Administrador)", width: 136, height: 30);
        BtnDeactivateSelected.Click += (_, _) => DeactivateChecked();

        BtnDeleteSelected = CambiasoTheme.CreateButton("Eliminar Firma(s)", AppIcon.Delete, ButtonTone.Danger, _toolTip,
            "Eliminar firma(s) digital(es) compatibles sin afectar OTs asociadas (solo Administrador)", width: 148, height: 30);
        BtnDeleteSelected.Click += (_, _) => DeleteChecked();

        BtnClearSelection = CambiasoTheme.CreateButton("Limpiar Sel.", AppIcon.Eraser, ButtonTone.Secondary, _toolTip,
            "Limpiar casillas marcadas", width: 122, height: 30);

        BtnRefresh = CambiasoTheme.CreateButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, _toolTip,
            "Recargar catálogo de firmas", width: 108, height: 30);
        BtnRefresh.Click += (_, _) => Reload();

        GridSignatures = new DataGridView { Name = "gridFirmas", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(GridSignatures);
        SelectionController = new GridCheckboxSelectionController<Requester>(
            GridSignatures,
            r => r.IdSol,
            ChkSelectAllVisible,
            UpdateBatchButtonsState);
        BtnClearSelection.Click += (_, _) => SelectionController.ClearSelection();

        GridSignatures.SelectionChanged += (_, _) => OnSelectionChanged();
        GridSignatures.CellFormatting += OnCellFormatting;

        ContextMenuHelper.AttachToGrid(GridSignatures, idx =>
        {
            Requester? r = GridBindingHelper.GetRowSource<Requester>(GridSignatures, idx);
            if (r is null)
            {
                return [];
            }

            List<int> targets = SelectionController.SelectedCount > 0 ? SelectionController.SelectedIds.ToList() : [r.IdSol];
            return
            [
                ContextMenuHelper.Item("Adjuntar / Reemplazar firma (PNG/JPG/PDF)…", AppIcon.FileText, CambiasoTheme.Primary, () => { _selectedIdSol = r.IdSol; AttachSignatureToCurrent(); }),
                ContextMenuHelper.Item("Consultar OT asociadas…", AppIcon.Search, CambiasoTheme.Accent, () => RunInspect(targets)),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Reasignar firma en OT…", AppIcon.MoveRight, CambiasoTheme.Primary, () => RunInspect(targets), enabled: _isAdmin),
                ContextMenuHelper.Item("Desactivar seleccionados…", AppIcon.Ban, CambiasoTheme.Warning, () => RunDeactivate(targets), enabled: _isAdmin),
                ContextMenuHelper.Item("Eliminar firma(s) seleccionadas…", AppIcon.Delete, CambiasoTheme.Danger, () => RunDelete(targets), enabled: _isAdmin)
            ];
        });

        PicPreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        LblPreviewTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Text = "Seleccione un registro para visualizar su firma",
            TextAlign = ContentAlignment.MiddleLeft
        };
        LblPreviewMeta = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Formatos admitidos: PNG, JPG, PDF"
        };

        BuildLayout();

        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;

        Reload();
    }

    public void Reload(int? selectIdSol = null)
    {
        _allRequesters = _ctx.MaintainerService.GetRequesters(includeInactive: true);
        ApplyFilterAndBind(selectIdSol ?? (_selectedIdSol > 0 ? _selectedIdSol : null));
        UpdateBatchButtonsState();
    }

    private IReadOnlyCollection<int> GetTargetIds()
    {
        if (SelectionController.SelectedCount > 0)
        {
            return SelectionController.SelectedIds;
        }

        return _selectedIdSol > 0 ? [_selectedIdSol] : [];
    }

    private void InspectChecked() => RunInspect(GetTargetIds());

    private void DeactivateChecked() => RunDeactivate(GetTargetIds());

    private void DeleteChecked() => RunDelete(GetTargetIds());

    private void RunInspect(IReadOnlyCollection<int> ids)
    {
        if (MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Firma, ids, OnBatchCompleted))
        {
            SelectionController.ClearSelection();
        }
    }

    private void RunDeactivate(IReadOnlyCollection<int> ids)
    {
        if (MaintainerBatchUiHelper.DeactivateChecked(this, _ctx, _session, MaintainerEntityKind.Firma, ids, OnBatchCompleted))
        {
            SelectionController.ClearSelection();
        }
    }

    private void RunDelete(IReadOnlyCollection<int> ids)
    {
        if (MaintainerBatchUiHelper.DeleteChecked(this, _ctx, _session, MaintainerEntityKind.Firma, ids, OnBatchCompleted))
        {
            SelectionController.ClearSelection();
        }
    }

    private void OnBatchCompleted()
    {
        Reload();
        SignaturesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void AttachSignatureToCurrent()
    {
        if (_selectedIdSol <= 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione primero un solicitante en la grilla.");
            return;
        }

        Requester? current = _allRequesters.FirstOrDefault(r => r.IdSol == _selectedIdSol);
        if (current is null)
        {
            return;
        }

        using OpenFileDialog dlg = new()
        {
            Title = $"Adjuntar Firma Digital — {current.Alias}",
            Filter = "Firmas (PNG, JPG, PDF)|*.png;*.jpg;*.jpeg;*.pdf|Imagen PNG (*.png)|*.png|Imagen JPG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Documento PDF (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            ProcessedSignature processed = SignatureFileService.ProcessFile(dlg.FileName);
            current.FirmaBytes = processed.RawBytes;
            current.FirmaNombre = processed.FileName;
            current.FirmaTipo = processed.FileType;
            current.FirmaPreviewPng = processed.PreviewPngBytes;
            _ctx.MaintainerService.SaveRequester(_session, current);

            Reload(current.IdSol);
            SignaturesChanged?.Invoke(this, EventArgs.Empty);
            CambiasoTheme.ShowInfo(this, $"Firma actualizada para '{current.Alias}' ({processed.FileType}).");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private List<Requester> GetFiltered()
    {
        string q = TxtSearch.Text.Trim();
        int idx = CmbFilter.SelectedIndex;

        return _allRequesters.Where(r =>
        {
            if (idx == 1 && !r.TieneFirma) return false;
            if (idx == 2 && (!r.TieneFirma || !r.Activo)) return false;
            if (idx == 3 && r.TieneFirma) return false;
            if (idx == 4 && r.Activo) return false;

            if (!string.IsNullOrWhiteSpace(q))
            {
                bool match = r.Alias.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.NombreCompleto.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.FirmaNombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.FirmaTipo.Contains(q, StringComparison.OrdinalIgnoreCase);
                if (!match) return false;
            }

            return true;
        }).ToList();
    }

    private void ApplyFilterAndBind(int? selectIdSol = null)
    {
        _suppressSelection = true;
        try
        {
            List<Requester> rows = GetFiltered();
            GridSignatures.DataSource = new SortableBindingList<SignatureGridRow>(
                rows.Select(r => new SignatureGridRow(r, _ctx.MaintainerService)));

            if (GridSignatures.Columns.Count >= 9)
            {
                SelectionController.ConfigureDataColumn(0, "ID", 6);
                SelectionController.ConfigureDataColumn(1, "Alias", 13);
                SelectionController.ConfigureDataColumn(2, "Nombre Completo", 23);
                SelectionController.ConfigureDataColumn(3, "Formato", 9);
                SelectionController.ConfigureDataColumn(4, "Archivo Firma", 18);
                SelectionController.ConfigureDataColumn(5, "Tamaño", 9);
                SelectionController.ConfigureDataColumn(6, "OTs Asoc.", 9);
                SelectionController.ConfigureDataColumn(7, "Estado", 9);
            }

            SelectionController.EnsureSelectColumnAndSync();

            int totalWithSig = _allRequesters.Count(r => r.TieneFirma);
            LblStats.Text = $"Firmas registradas: {totalWithSig} de {_allRequesters.Count} solicitante(s) · Mostrando {rows.Count}";

            if (selectIdSol.HasValue)
            {
                for (int i = 0; i < GridSignatures.Rows.Count; i++)
                {
                    if (GridBindingHelper.GetRowSource<Requester>(GridSignatures, i)?.IdSol == selectIdSol.Value)
                    {
                        GridSignatures.ClearSelection();
                        GridSignatures.Rows[i].Selected = true;
                        GridSignatures.CurrentCell = GridSignatures.Rows[i].Cells[1];
                        break;
                    }
                }
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        UpdatePreview();
    }

    private void OnSelectionChanged()
    {
        if (_suppressSelection)
        {
            return;
        }

        Requester? current = GridBindingHelper.GetRowSource<Requester>(GridSignatures);
        _selectedIdSol = current?.IdSol ?? 0;
        UpdatePreview();
        UpdateBatchButtonsState();
    }

    private void UpdatePreview()
    {
        Requester? r = _allRequesters.FirstOrDefault(x => x.IdSol == _selectedIdSol);
        Image? oldImg = PicPreview.Image;

        if (r is null)
        {
            LblPreviewTitle.Text = "Seleccione un registro para visualizar su firma";
            LblPreviewMeta.Text = "Sin selección activa.";
            PicPreview.Image = IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 36);
            BtnAttachReplace.Enabled = false;
        }
        else if (r.FirmaPreviewPng is { Length: > 0 })
        {
            LblPreviewTitle.Text = $"{r.Alias} — {r.NombreCompleto}";
            int kb = Math.Max(1, (r.FirmaBytes?.Length ?? r.FirmaPreviewPng.Length) / 1024);
            LblPreviewMeta.Text = $"Archivo: {r.FirmaNombre} · Formato: {r.FirmaTipo} · Peso: {kb:N0} KB · Estado: {(r.Activo ? "Activo" : "Inactivo")}";
            PicPreview.Image = SignatureFileService.CreateBitmapFromBytes(r.FirmaPreviewPng);
            BtnAttachReplace.Enabled = true;
        }
        else
        {
            LblPreviewTitle.Text = $"{r.Alias} — {r.NombreCompleto}";
            LblPreviewMeta.Text = $"Sin firma digital adjunta · Estado: {(r.Activo ? "Activo" : "Inactivo")}";
            PicPreview.Image = IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 36);
            BtnAttachReplace.Enabled = true;
        }

        if (oldImg is not null && !ReferenceEquals(oldImg, IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 36)))
        {
            oldImg.Dispose();
        }
    }

    private void UpdateBatchButtonsState()
    {
        int checkedCount = SelectionController.SelectedCount;
        LblSelectedCount.Text = $"Seleccionadas: {checkedCount}";
        bool hasAnyTarget = checkedCount > 0 || _selectedIdSol > 0;

        BtnInspectOts.Enabled = hasAnyTarget;
        BtnReassign.Visible = _isAdmin;
        BtnReassign.Enabled = _isAdmin && hasAnyTarget;
        BtnDeactivateSelected.Visible = _isAdmin;
        BtnDeactivateSelected.Enabled = _isAdmin && hasAnyTarget;
        BtnDeleteSelected.Visible = _isAdmin;
        BtnDeleteSelected.Enabled = _isAdmin && hasAnyTarget;
        BtnClearSelection.Enabled = checkedCount > 0;
    }

    private void OnCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        Requester? r = GridBindingHelper.GetRowSource<Requester>(GridSignatures, e.RowIndex);
        if (r is null || e.CellStyle is null)
        {
            return;
        }

        // Columna Formato (index 4 con checkbox col 0) y Estado (index 8)
        if (e.ColumnIndex == 4)
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = r.TieneFirma ? CambiasoTheme.Success : CambiasoTheme.TextMuted;
        }
        else if (e.ColumnIndex == 8)
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = r.Activo ? CambiasoTheme.Success : CambiasoTheme.Danger;
        }
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & (DataChangeKind.Catalogs | DataChangeKind.WorkOrders)) == 0)
        {
            return;
        }

        void RefreshSafe()
        {
            if (!IsDisposed)
            {
                Reload();
            }
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshSafe);
        else if (!InvokeRequired) RefreshSafe();
    }

    private void BuildLayout()
    {
        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        FlowLayoutPanel actionBar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2, 4, 2, 2) };
        actionBar.Controls.AddRange([BtnAttachReplace, BtnInspectOts, BtnReassign, BtnDeactivateSelected, BtnDeleteSelected, BtnClearSelection, BtnRefresh]);

        FlowLayoutPanel filterBar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2, 4, 2, 2) };
        filterBar.Controls.AddRange([
            ChkSelectAllVisible,
            LblSelectedCount,
            new Label { Text = "Buscar:", AutoSize = true, Margin = new Padding(8, 6, 4, 0), Font = CambiasoTheme.LabelFont },
            TxtSearch,
            new Label { Text = "Filtro:", AutoSize = true, Margin = new Padding(10, 6, 4, 0), Font = CambiasoTheme.LabelFont },
            CmbFilter,
            LblStats
        ]);

        SplitContainer split = new() { Dock = DockStyle.Fill, Size = new Size(1060, 560), Panel1MinSize = 540, SplitterDistance = 720 };
        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Catálogo de Firmas Digitales (Marque casillas para acciones masivas)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(GridSignatures);
        split.Panel1.Controls.Add(grpGrid);

        GroupBox grpPreview = CambiasoTheme.CreateGroupBox("Vista Previa de Firma Seleccionada");
        grpPreview.Dock = DockStyle.Fill;
        TableLayoutPanel previewInner = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(6) };
        previewInner.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        previewInner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewInner.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        LblPreviewTitle.Dock = DockStyle.Fill;
        LblPreviewMeta.Dock = DockStyle.Fill;
        previewInner.Controls.Add(LblPreviewTitle, 0, 0);
        previewInner.Controls.Add(PicPreview, 0, 1);
        previewInner.Controls.Add(LblPreviewMeta, 0, 2);
        grpPreview.Controls.Add(previewInner);
        split.Panel2.Controls.Add(grpPreview);

        root.Controls.Add(actionBar, 0, 0);
        root.Controls.Add(filterBar, 0, 1);
        root.Controls.Add(split, 0, 2);
        Controls.Add(root);
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

            if (PicPreview.Image is not null && !ReferenceEquals(PicPreview.Image, IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 36)))
            {
                PicPreview.Image.Dispose();
                PicPreview.Image = null;
            }

            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class SignatureGridRow(Requester source, MaintainerService svc) : IGridRowSource<Requester>
    {
        [Browsable(false)]
        public Requester Source { get; } = source;
        public int ID => Source.IdSol;
        public string Alias => Source.Alias;
        public string NombreCompleto => Source.NombreCompleto;
        public string Formato => Source.FirmaBadge;
        public string Archivo => string.IsNullOrWhiteSpace(Source.FirmaNombre) ? "—" : Source.FirmaNombre;
        public string Peso => Source.FirmaBytes is { Length: > 0 } ? $"{Math.Max(1, Source.FirmaBytes.Length / 1024)} KB" : "—";
        public int OtsAsoc => svc.GetRequesterWorkOrderCount(Source.IdSol);
        public string Estado => Source.Activo ? "Activo" : "Inactivo";
    }
}
