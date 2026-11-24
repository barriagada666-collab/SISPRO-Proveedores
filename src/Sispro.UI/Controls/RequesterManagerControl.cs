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
/// Control unificado para gestionar "Solicitado Por" y su Firma Digital (PNG, JPG o PDF),
/// con selección múltiple, consulta de OT relacionadas, reasignación y sincronización inmediata.
/// </summary>
public sealed class RequesterManagerControl : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();
    private readonly bool _isAdmin;

    private IReadOnlyList<Requester> _allRequesters = [];
    private int _selectedId;
    private ProcessedSignature? _pendingSignature;
    private byte[]? _existingPreviewPng;
    private byte[]? _existingRawBytes;
    private string _existingFileName = string.Empty;
    private string _existingFileType = string.Empty;
    private bool _clearSignature;
    private bool _syncSubscribed;
    private bool _suppressSelection;

    public event EventHandler? RequestersChanged;

    internal TextBox TxtId { get; }
    internal TextBox TxtAlias { get; }
    internal TextBox TxtNombre { get; }
    internal TextBox TxtPat { get; }
    internal TextBox TxtMat { get; }
    internal TextBox TxtFono { get; }
    internal TextBox TxtMail { get; }
    internal CheckBox ChkActivo { get; }

    internal PictureBox PicSignature { get; }
    internal Label LblSignatureMeta { get; }
    internal Label LblModeBanner { get; }
    internal Label LblStats { get; }
    internal Label LblSelectedCount { get; }
    internal CheckBox ChkSelectAllVisible { get; }

    internal Button BtnNuevo { get; }
    internal Button BtnGuardar { get; }
    internal Button BtnActualizar { get; }
    internal Button BtnCancelar { get; }
    internal Button BtnActivar { get; }
    internal Button BtnDesactivar { get; }
    internal Button BtnEliminar { get; }
    internal Button BtnConsultarOt { get; }
    internal Button BtnReasignar { get; }
    internal Button BtnClearSelection { get; }
    internal Button BtnAdjuntarFirma { get; }
    internal Button BtnQuitarFirma { get; }
    internal Button BtnExportar { get; }
    internal Button BtnRefrescar { get; }

    internal TextBox TxtSearch { get; }
    internal ComboBox CmbFilter { get; }
    internal DataGridView GridRequesters { get; }
    internal GridCheckboxSelectionController<Requester> SelectionController { get; }

    public RequesterManagerControl(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        _isAdmin = _ctx.MaintainerService.IsAdministrator(_session);
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        // Banner modo / estado
        Panel modeBanner = new()
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = CambiasoTheme.SubtleFill,
            Padding = new Padding(10, 0, 10, 0)
        };
        LblModeBanner = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.Primary,
            TextAlign = ContentAlignment.MiddleLeft
        };
        modeBanner.Controls.Add(LblModeBanner);

        // Campos de ficha
        Padding fm = new(0, 3, 8, 3);
        TxtId = new TextBox { Text = "(Nuevo)", ReadOnly = true, TabStop = false, BackColor = CambiasoTheme.Surface, ForeColor = CambiasoTheme.TextMuted, Dock = DockStyle.Fill, Margin = fm };
        TxtAlias = new TextBox { Name = "txtSolAlias", MaxLength = 40, PlaceholderText = "Ej. J Ramos", Dock = DockStyle.Fill, Margin = fm };
        TxtNombre = new TextBox { Name = "txtSolNombre", MaxLength = 60, PlaceholderText = "Nombres", Dock = DockStyle.Fill, Margin = fm };
        TxtPat = new TextBox { Name = "txtSolPat", MaxLength = 60, PlaceholderText = "Apellido paterno", Dock = DockStyle.Fill, Margin = fm };
        TxtMat = new TextBox { Name = "txtSolMat", MaxLength = 60, PlaceholderText = "Apellido materno", Dock = DockStyle.Fill, Margin = fm };
        TxtFono = new TextBox { Name = "txtSolFono", MaxLength = 30, PlaceholderText = "+56 32 ...", Dock = DockStyle.Fill, Margin = fm };
        TxtMail = new TextBox { Name = "txtSolMail", MaxLength = 100, PlaceholderText = "usuario@cambiaso.cl", Dock = DockStyle.Fill, Margin = fm };
        ChkActivo = new CheckBox { Text = "Solicitante activo", Checked = true, AutoSize = true, Margin = new Padding(0, 6, 8, 2) };

        // Firma Digital
        PicSignature = new PictureBox
        {
            Name = "picSolSignature",
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        LblSignatureMeta = new Label
        {
            Dock = DockStyle.Fill,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Sin firma digital adjunta (PNG, JPG o PDF)",
            TextAlign = ContentAlignment.MiddleLeft
        };

        BtnAdjuntarFirma = CambiasoTheme.CreateButton("Adjuntar Firma", AppIcon.FileText, ButtonTone.Accent, _toolTip, "Adjuntar firma digital en formato PNG, JPG o PDF (máx. 10 MB)", width: 146, height: 30);
        BtnAdjuntarFirma.Click += (_, _) => BrowseSignatureFile();

        BtnQuitarFirma = CambiasoTheme.CreateButton("Quitar Firma", AppIcon.Remove, ButtonTone.Secondary, _toolTip, "Eliminar la firma digital adjunta de este solicitante", width: 124, height: 30);
        BtnQuitarFirma.Click += (_, _) => ClearSignature();

        // Botones CRUD
        BtnNuevo = CreateActionButton("Nuevo / Limpiar", AppIcon.Add, ButtonTone.Secondary, "Limpiar campos y preparar un nuevo Solicitado Por");
        BtnNuevo.Click += (_, _) => EnterNewMode();

        BtnGuardar = CreateActionButton("Guardar Nuevo", AppIcon.Save, ButtonTone.Primary, "Registrar nuevo Solicitado Por con su firma digital");
        BtnGuardar.Click += (_, _) => SaveRequester(isUpdate: false);

        BtnActualizar = CreateActionButton("Actualizar", AppIcon.Check, ButtonTone.Accent, "Guardar cambios en el Solicitado Por seleccionado");
        BtnActualizar.Click += (_, _) => SaveRequester(isUpdate: true);

        BtnCancelar = CreateActionButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, "Cancelar edición y restaurar datos");
        BtnCancelar.Click += (_, _) => CancelEdit();

        BtnActivar = CreateActionButton("Activar", AppIcon.Power, ButtonTone.Primary, "Activar solicitante(s) seleccionado(s) (solo Administrador)");
        BtnActivar.Click += (_, _) => SetSelectedActive(true);

        BtnDesactivar = CreateActionButton("Desactivar", AppIcon.Ban, ButtonTone.Secondary, "Desactivar solicitante(s) conservando historial (solo Administrador)");
        BtnDesactivar.Click += (_, _) => SetSelectedActive(false);

        BtnEliminar = CreateActionButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, "Eliminar o gestionar dependencias de solicitante(s) (solo Administrador)");
        BtnEliminar.Click += (_, _) => DeleteSelectedRequester();

        BtnConsultarOt = CreateActionButton("Consultar OT", AppIcon.Search, ButtonTone.Accent, "Consultar órdenes de trabajo asociadas a los seleccionados");
        BtnConsultarOt.Click += (_, _) => InspectSelectedRequesters();

        BtnReasignar = CreateActionButton("Reasignar", AppIcon.MoveRight, ButtonTone.Secondary, "Reasignar OTs de los solicitantes seleccionados hacia otro activo (solo Administrador)");
        BtnReasignar.Click += (_, _) => InspectSelectedRequesters();

        BtnExportar = CreateActionButton("Exportar", AppIcon.Download, ButtonTone.Accent, "Exportar catálogo de solicitantes y firmas");
        BtnExportar.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            BtnExportar,
            this,
            _ctx.DocumentExportService,
            "SISPRO_Solicitantes",
            () => DocumentExportService.BuildRequestersReport(GetFilteredRequesters(), BuildFilterSummary()));

        BtnRefrescar = CreateActionButton("Refrescar", AppIcon.Refresh, ButtonTone.Secondary, "Recargar solicitantes y firmas desde SQLite");
        BtnRefrescar.Click += (_, _) => ReloadRequesters(_selectedId > 0 ? _selectedId : null);

        ChkSelectAllVisible = new CheckBox
        {
            Name = "chkSelectAllSolicitantes",
            Text = "Seleccionar todos visibles",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            Margin = new Padding(0, 6, 8, 0)
        };
        LblSelectedCount = new Label
        {
            Text = "Sel.: 0",
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.PrimaryDark,
            Margin = new Padding(0, 7, 10, 0)
        };
        BtnClearSelection = CreateActionButton("Limpiar Sel.", AppIcon.Eraser, ButtonTone.Secondary, "Limpiar casillas marcadas");

        // Filtros y Grilla
        TxtSearch = new TextBox { Width = 200, PlaceholderText = "Buscar por alias, nombre, correo o archivo..." };
        TxtSearch.TextChanged += (_, _) => ApplyFilterAndBind();

        CmbFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 148 };
        CmbFilter.Items.AddRange(["Todos", "Activos", "Inactivos", "Con Firma Digital", "Sin Firma Digital"]);
        CmbFilter.SelectedIndex = 0;
        CmbFilter.SelectedIndexChanged += (_, _) => ApplyFilterAndBind();

        LblStats = new Label { AutoSize = true, Font = CambiasoTheme.SmallFont, ForeColor = CambiasoTheme.TextMuted, Margin = new Padding(10, 7, 0, 0) };

        GridRequesters = new DataGridView { Name = "gridSolicitantes", Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(GridRequesters);
        SelectionController = new GridCheckboxSelectionController<Requester>(
            GridRequesters,
            r => r.IdSol,
            ChkSelectAllVisible,
            UpdateActionStates);
        BtnClearSelection.Click += (_, _) => SelectionController.ClearSelection();

        GridRequesters.SelectionChanged += (_, _) => OnGridSelectionChanged();
        GridRequesters.CellClick += (_, _) => OnGridSelectionChanged();
        GridRequesters.CellDoubleClick += (_, _) => TxtNombre.Focus();
        GridRequesters.CellFormatting += OnGridCellFormatting;

        ContextMenuHelper.AttachToGrid(GridRequesters, idx =>
        {
            Requester? req = GridBindingHelper.GetRowSource<Requester>(GridRequesters, idx);
            if (req is null)
            {
                return [ContextMenuHelper.Item("Nuevo solicitante", AppIcon.Add, CambiasoTheme.Primary, EnterNewMode)];
            }

            List<int> targets = SelectionController.SelectedCount > 0 ? SelectionController.SelectedIds.ToList() : [req.IdSol];
            return
            [
                ContextMenuHelper.Item("Editar solicitante", AppIcon.Edit, CambiasoTheme.Primary, () => { LoadRequesterToEditor(req); TxtNombre.Focus(); }),
                ContextMenuHelper.Item("Adjuntar / Cambiar firma (PNG/JPG/PDF)…", AppIcon.FileText, CambiasoTheme.Accent, () => { LoadRequesterToEditor(req); BrowseSignatureFile(); }),
                ContextMenuHelper.Item("Quitar firma digital", AppIcon.Remove, CambiasoTheme.Warning, () => { LoadRequesterToEditor(req); ClearSignature(); }, enabled: req.TieneFirma),
                ContextMenuHelper.Item("Consultar OT asociadas…", AppIcon.Search, CambiasoTheme.Accent, () => MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Solicitante, targets, OnBatchCompleted)),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Activar", AppIcon.Power, CambiasoTheme.Success, () => ToggleRequesterActive(req, true), enabled: _isAdmin && !req.Activo),
                ContextMenuHelper.Item("Desactivar / Verificar OT…", AppIcon.Ban, CambiasoTheme.Warning, () => { MaintainerBatchUiHelper.DeactivateChecked(this, _ctx, _session, MaintainerEntityKind.Solicitante, targets, OnBatchCompleted); }, enabled: _isAdmin && (req.Activo || targets.Count > 1)),
                ContextMenuHelper.Item("Reasignar relaciones en OT…", AppIcon.MoveRight, CambiasoTheme.Primary, () => MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Solicitante, targets, OnBatchCompleted), enabled: _isAdmin),
                ContextMenuHelper.Item("Eliminar / Verificar OT…", AppIcon.Delete, CambiasoTheme.Danger, () => { MaintainerBatchUiHelper.DeleteChecked(this, _ctx, _session, MaintainerEntityKind.Solicitante, targets, OnBatchCompleted); }, enabled: _isAdmin),
                ContextMenuHelper.Separator(),
                ContextMenuHelper.Item("Nuevo solicitante", AppIcon.Add, CambiasoTheme.Accent, EnterNewMode)
            ];
        });

        BuildLayout(modeBanner);

        _ctx.SyncHub.DataChanged += OnSyncDataChanged;
        _syncSubscribed = true;

        ReloadRequesters();
        EnterNewMode();
    }

    public void AttachSignatureFile(string filePath)
    {
        ProcessedSignature processed = SignatureFileService.ProcessFile(filePath);
        ApplyProcessedSignature(processed);
    }

    public void AttachSignatureBytes(string fileName, byte[] rawBytes)
    {
        ProcessedSignature processed = SignatureFileService.ProcessBytes(fileName, rawBytes);
        ApplyProcessedSignature(processed);
    }

    public void ClearSignature()
    {
        _pendingSignature = null;
        _existingPreviewPng = null;
        _existingRawBytes = null;
        _existingFileName = string.Empty;
        _existingFileType = string.Empty;
        _clearSignature = true;
        UpdateSignaturePreview();
        UpdateActionStates();
    }

    public void ReloadRequesters(int? selectIdSol = null)
    {
        _allRequesters = _ctx.MaintainerService.GetRequesters(includeInactive: true);
        ApplyFilterAndBind(selectIdSol);
    }

    private void ApplyProcessedSignature(ProcessedSignature processed)
    {
        _pendingSignature = processed;
        _clearSignature = false;
        UpdateSignaturePreview();
        UpdateActionStates();
    }

    private void BrowseSignatureFile()
    {
        using OpenFileDialog dlg = new()
        {
            Title = "Seleccionar Firma Digital del Solicitante",
            Filter = "Firmas (PNG, JPG, PDF)|*.png;*.jpg;*.jpeg;*.pdf|Imagen PNG (*.png)|*.png|Imagen JPG (*.jpg;*.jpeg)|*.jpg;*.jpeg|Documento PDF (*.pdf)|*.pdf",
            CheckFileExists = true
        };

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            AttachSignatureFile(dlg.FileName);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message, "SISPRO — Firma Digital");
        }
    }

    private void EnterNewMode()
    {
        _suppressSelection = true;
        try
        {
            GridRequesters.ClearSelection();
        }
        finally
        {
            _suppressSelection = false;
        }

        _selectedId = 0;
        TxtId.Text = "(Nuevo)";
        TxtAlias.Clear();
        TxtNombre.Clear();
        TxtPat.Clear();
        TxtMat.Clear();
        TxtFono.Clear();
        TxtMail.Clear();
        ChkActivo.Checked = true;

        _pendingSignature = null;
        _existingPreviewPng = null;
        _existingRawBytes = null;
        _existingFileName = string.Empty;
        _existingFileType = string.Empty;
        _clearSignature = false;

        UpdateSignaturePreview();
        UpdateActionStates();
        TxtAlias.Focus();
    }

    private void CancelEdit()
    {
        if (_selectedId > 0)
        {
            Requester? current = _allRequesters.FirstOrDefault(r => r.IdSol == _selectedId);
            if (current is not null)
            {
                LoadRequesterToEditor(current);
                return;
            }
        }

        EnterNewMode();
    }

    private void LoadRequesterToEditor(Requester r)
    {
        _selectedId = r.IdSol;
        TxtId.Text = r.IdSol.ToString(System.Globalization.CultureInfo.InvariantCulture);
        TxtAlias.Text = r.Alias;
        TxtNombre.Text = r.Nombre;
        TxtPat.Text = r.ApellidoPaterno;
        TxtMat.Text = r.ApellidoMaterno;
        TxtFono.Text = r.Telefono;
        TxtMail.Text = r.Correo;
        ChkActivo.Checked = r.Activo;

        _pendingSignature = null;
        _existingRawBytes = r.FirmaBytes;
        _existingPreviewPng = r.FirmaPreviewPng;
        _existingFileName = r.FirmaNombre;
        _existingFileType = r.FirmaTipo;
        _clearSignature = false;

        UpdateSignaturePreview();
        UpdateActionStates();
    }

    internal bool SaveFromEditor(bool isUpdate, bool showConfirmationDialog = false)
    {
        try
        {
            if (isUpdate && _selectedId <= 0)
            {
                if (showConfirmationDialog)
                {
                    CambiasoTheme.ShowWarning(this, "Seleccione un solicitante del listado para actualizar.");
                }
                return false;
            }

            Requester model = new()
            {
                IdSol = isUpdate ? _selectedId : 0,
                Alias = TxtAlias.Text.Trim(),
                Nombre = TxtNombre.Text.Trim(),
                ApellidoPaterno = TxtPat.Text.Trim(),
                ApellidoMaterno = TxtMat.Text.Trim(),
                Telefono = TxtFono.Text.Trim(),
                Correo = TxtMail.Text.Trim(),
                Activo = ChkActivo.Checked
            };

            if (_pendingSignature is not null)
            {
                model.FirmaBytes = _pendingSignature.RawBytes;
                model.FirmaNombre = _pendingSignature.FileName;
                model.FirmaTipo = _pendingSignature.FileType;
                model.FirmaPreviewPng = _pendingSignature.PreviewPngBytes;
            }
            else if (!_clearSignature && isUpdate)
            {
                model.FirmaBytes = _existingRawBytes;
                model.FirmaNombre = _existingFileName;
                model.FirmaTipo = _existingFileType;
                model.FirmaPreviewPng = _existingPreviewPng;
            }

            int savedId = _ctx.MaintainerService.SaveRequester(_session, model);
            if (showConfirmationDialog)
            {
                CambiasoTheme.ShowInfo(this, $"Solicitante '{model.Alias}' guardado correctamente{(model.TieneFirma ? $" con firma ({model.FirmaTipo})" : string.Empty)}.");
            }
            ReloadRequesters(savedId);
            if (!isUpdate)
            {
                EnterNewMode();
            }

            RequestersChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            if (showConfirmationDialog)
            {
                CambiasoTheme.ShowWarning(this, ex.Message);
            }
            else
            {
                throw;
            }
            return false;
        }
    }

    private void SaveRequester(bool isUpdate) => SaveFromEditor(isUpdate, showConfirmationDialog: true);

    private IReadOnlyCollection<int> GetTargetIds()
    {
        if (SelectionController.SelectedCount > 0)
        {
            return SelectionController.SelectedIds;
        }

        return _selectedId > 0 ? [_selectedId] : [];
    }

    private void InspectSelectedRequesters()
    {
        if (MaintainerBatchUiHelper.InspectAndManage(this, _ctx, _session, MaintainerEntityKind.Solicitante, GetTargetIds(), OnBatchCompleted))
        {
            SelectionController.ClearSelection();
        }
    }

    private void OnBatchCompleted()
    {
        ReloadRequesters();
        EnterNewMode();
        RequestersChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetSelectedActive(bool active)
    {
        IReadOnlyCollection<int> targets = GetTargetIds();
        if (targets.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione o marque al menos un solicitante.");
            return;
        }

        if (!active)
        {
            if (MaintainerBatchUiHelper.DeactivateChecked(this, _ctx, _session, MaintainerEntityKind.Solicitante, targets, OnBatchCompleted))
            {
                SelectionController.ClearSelection();
            }

            return;
        }

        try
        {
            MaintainerBatchResult res = _ctx.MaintainerService.SetRequestersActiveBatch(_session, targets, true);
            CambiasoTheme.ShowInfo(this, res.BuildSummary());
            SelectionController.ClearSelection();
            ReloadRequesters(_selectedId > 0 ? _selectedId : null);
            RequestersChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ToggleRequesterActive(Requester req, bool active)
    {
        try
        {
            _ctx.MaintainerService.SetRequesterActive(_session, req.IdSol, active);
            ReloadRequesters(req.IdSol);
            RequestersChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void DeleteSelectedRequester()
    {
        IReadOnlyCollection<int> targets = GetTargetIds();
        if (targets.Count == 0)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione o marque al menos un solicitante.");
            return;
        }

        if (MaintainerBatchUiHelper.DeleteChecked(this, _ctx, _session, MaintainerEntityKind.Solicitante, targets, OnBatchCompleted))
        {
            SelectionController.ClearSelection();
        }
    }

    private void UpdateSignaturePreview()
    {
        byte[]? previewBytes = _pendingSignature?.PreviewPngBytes ?? (_clearSignature ? null : _existingPreviewPng);
        string fileName = _pendingSignature?.FileName ?? (_clearSignature ? string.Empty : _existingFileName);
        string fileType = _pendingSignature?.FileType ?? (_clearSignature ? string.Empty : _existingFileType);
        int rawLen = _pendingSignature?.RawBytes.Length ?? (_clearSignature ? 0 : (_existingRawBytes?.Length ?? 0));

        Image? oldImg = PicSignature.Image;
        if (previewBytes is { Length: > 0 })
        {
            PicSignature.Image = SignatureFileService.CreateBitmapFromBytes(previewBytes);
            string kb = Math.Max(1, rawLen / 1024).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string kindNote = string.Equals(fileType, "PDF", StringComparison.OrdinalIgnoreCase) ? "PDF · Página 1" : fileType;
            LblSignatureMeta.Text = $"Archivo: {fileName} ({kindNote} · {kb} KB)";
            LblSignatureMeta.ForeColor = CambiasoTheme.Success;
        }
        else
        {
            PicSignature.Image = IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 32);
            LblSignatureMeta.Text = "Sin firma digital adjunta (PNG, JPG o PDF)";
            LblSignatureMeta.ForeColor = CambiasoTheme.TextMuted;
        }

        if (oldImg is not null && !ReferenceEquals(oldImg, IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 32)))
        {
            oldImg.Dispose();
        }
    }

    private void UpdateActionStates()
    {
        bool isEdit = _selectedId > 0;
        int checkedCount = SelectionController.SelectedCount;
        bool hasAnyTarget = checkedCount > 0 || isEdit;
        bool hasSig = _pendingSignature is not null || (!_clearSignature && ((_existingPreviewPng is { Length: > 0 }) || (_existingRawBytes is { Length: > 0 })));
        Requester? current = isEdit ? _allRequesters.FirstOrDefault(r => r.IdSol == _selectedId) : null;

        LblSelectedCount.Text = $"Sel.: {checkedCount}";

        BtnGuardar.Enabled = !isEdit;
        BtnActualizar.Enabled = isEdit;
        BtnCancelar.Enabled = isEdit;
        BtnConsultarOt.Enabled = hasAnyTarget;

        BtnActivar.Visible = _isAdmin;
        BtnDesactivar.Visible = _isAdmin;
        BtnEliminar.Visible = _isAdmin;
        BtnReasignar.Visible = _isAdmin;

        BtnActivar.Enabled = _isAdmin && (checkedCount > 0 || (isEdit && current is { Activo: false }));
        BtnDesactivar.Enabled = _isAdmin && (checkedCount > 0 || (isEdit && current is { Activo: true }));
        BtnEliminar.Enabled = _isAdmin && hasAnyTarget;
        BtnReasignar.Enabled = _isAdmin && hasAnyTarget;
        BtnClearSelection.Enabled = checkedCount > 0;
        BtnQuitarFirma.Enabled = hasSig;

        LblModeBanner.Text = isEdit
            ? $"MODO: EDITANDO SOLICITANTE '{TxtAlias.Text}' (ID #{_selectedId}) — Adjunte o actualice su firma PNG/JPG/PDF y presione Actualizar."
            : "MODO: NUEVO SOLICITADO POR — Complete Alias, Nombre y Apellido, adjunte firma (PNG, JPG o PDF) y presione Guardar Nuevo.";
    }

    private List<Requester> GetFilteredRequesters()
    {
        string q = TxtSearch.Text.Trim();
        int filterIdx = CmbFilter.SelectedIndex;

        return _allRequesters.Where(r =>
        {
            if (filterIdx == 1 && !r.Activo) return false;
            if (filterIdx == 2 && r.Activo) return false;
            if (filterIdx == 3 && !r.TieneFirma) return false;
            if (filterIdx == 4 && r.TieneFirma) return false;

            if (!string.IsNullOrWhiteSpace(q))
            {
                bool match = r.Alias.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.NombreCompleto.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.Correo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.Telefono.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                             r.FirmaNombre.Contains(q, StringComparison.OrdinalIgnoreCase);
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
            List<Requester> filtered = GetFilteredRequesters();
            GridRequesters.DataSource = new SortableBindingList<RequesterGridRow>(
                filtered.Select(r => new RequesterGridRow(r)));

            if (GridRequesters.Columns.Count >= 9)
            {
                SelectionController.ConfigureDataColumn(0, "ID", 6);
                SelectionController.ConfigureDataColumn(1, "Solicitado Por", 13);
                SelectionController.ConfigureDataColumn(2, "Nombre Completo", 22);
                SelectionController.ConfigureDataColumn(3, "Teléfono", 11);
                SelectionController.ConfigureDataColumn(4, "Correo", 20);
                SelectionController.ConfigureDataColumn(5, "Firma Digital", 10);
                SelectionController.ConfigureDataColumn(6, "Archivo Firma", 11);
                SelectionController.ConfigureDataColumn(7, "Estado", 7);
            }

            SelectionController.EnsureSelectColumnAndSync();

            int withSig = _allRequesters.Count(r => r.TieneFirma);
            LblStats.Text = $"Mostrando {filtered.Count} de {_allRequesters.Count} solicitante(s) · {withSig} con firma digital";

            if (selectIdSol.HasValue)
            {
                for (int i = 0; i < GridRequesters.Rows.Count; i++)
                {
                    if (GridBindingHelper.GetRowSource<Requester>(GridRequesters, i)?.IdSol == selectIdSol.Value)
                    {
                        GridRequesters.ClearSelection();
                        GridRequesters.Rows[i].Selected = true;
                        GridRequesters.CurrentCell = GridRequesters.Rows[i].Cells[2];
                        break;
                    }
                }
            }
            else if (_selectedId == 0)
            {
                GridRequesters.ClearSelection();
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        if (selectIdSol.HasValue)
        {
            Requester? target = _allRequesters.FirstOrDefault(r => r.IdSol == selectIdSol.Value);
            if (target is not null)
            {
                LoadRequesterToEditor(target);
            }
        }
    }

    private void OnGridSelectionChanged()
    {
        if (_suppressSelection)
        {
            return;
        }

        Requester? row = GridBindingHelper.GetRowSource<Requester>(GridRequesters);
        if (row is not null)
        {
            LoadRequesterToEditor(row);
        }
    }

    private void OnGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        Requester? r = GridBindingHelper.GetRowSource<Requester>(GridRequesters, e.RowIndex);
        if (r is null || e.CellStyle is null || e.ColumnIndex < 0 || e.ColumnIndex >= GridRequesters.Columns.Count)
        {
            return;
        }

        string prop = GridRequesters.Columns[e.ColumnIndex].DataPropertyName;
        if (string.Equals(prop, nameof(RequesterGridRow.Firma), StringComparison.Ordinal))
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = r.TieneFirma ? CambiasoTheme.Success : CambiasoTheme.TextMuted;
        }
        else if (string.Equals(prop, nameof(RequesterGridRow.Estado), StringComparison.Ordinal))
        {
            e.CellStyle.Font = CambiasoTheme.LabelFont;
            e.CellStyle.ForeColor = r.Activo ? CambiasoTheme.Success : CambiasoTheme.Danger;
        }
    }

    private void OnSyncDataChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & DataChangeKind.Catalogs) == 0)
        {
            return;
        }

        void RefreshSafe()
        {
            if (!IsDisposed)
            {
                ReloadRequesters(_selectedId > 0 ? _selectedId : null);
            }
        }

        if (InvokeRequired && IsHandleCreated)
        {
            BeginInvoke(RefreshSafe);
        }
        else if (!InvokeRequired)
        {
            RefreshSafe();
        }
    }

    private string BuildFilterSummary() =>
        $"Filtro: {CmbFilter.SelectedItem ?? "Todos"} | Búsqueda: {(string.IsNullOrWhiteSpace(TxtSearch.Text) ? "Ninguna" : TxtSearch.Text.Trim())}";

    private void BuildLayout(Panel modeBanner)
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(4)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 204));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // 1. Top Form + Signature Preview Split
        TableLayoutPanel topSplit = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        topSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 63));
        topSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));

        GroupBox grpData = CambiasoTheme.CreateGroupBox("DATOS DEL SOLICITADO POR (SOLICITANTE OT)");
        grpData.Dock = DockStyle.Fill;

        TableLayoutPanel formWrap = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        formWrap.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        formWrap.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel fields = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4, Padding = new Padding(2, 4, 2, 0) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 114));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int r = 0; r < 4; r++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        fields.Controls.Add(FieldLabel("ID:"), 0, 0);
        fields.Controls.Add(TxtId, 1, 0);
        fields.Controls.Add(FieldLabel("Alias (OT) *:"), 2, 0);
        fields.Controls.Add(TxtAlias, 3, 0);

        fields.Controls.Add(FieldLabel("Nombres *:"), 0, 1);
        fields.Controls.Add(TxtNombre, 1, 1);
        fields.Controls.Add(FieldLabel("Ap. Paterno *:"), 2, 1);
        fields.Controls.Add(TxtPat, 3, 1);

        fields.Controls.Add(FieldLabel("Ap. Materno:"), 0, 2);
        fields.Controls.Add(TxtMat, 1, 2);
        fields.Controls.Add(FieldLabel("Teléfono:"), 2, 2);
        fields.Controls.Add(TxtFono, 3, 2);

        fields.Controls.Add(FieldLabel("Correo:"), 0, 3);
        fields.Controls.Add(TxtMail, 1, 3);
        fields.Controls.Add(FieldLabel("Estado:"), 2, 3);
        fields.Controls.Add(ChkActivo, 3, 3);

        formWrap.Controls.Add(modeBanner, 0, 0);
        formWrap.Controls.Add(fields, 0, 1);
        grpData.Controls.Add(formWrap);

        // Signature Panel
        GroupBox grpSig = CambiasoTheme.CreateGroupBox("FIRMA DIGITAL ADJUNTA (PNG · JPG · PDF)");
        grpSig.Dock = DockStyle.Fill;

        TableLayoutPanel sigLayout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(4, 2, 4, 2) };
        sigLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sigLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        sigLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        FlowLayoutPanel sigButtons = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
        sigButtons.Controls.AddRange([BtnAdjuntarFirma, BtnQuitarFirma]);

        sigLayout.Controls.Add(PicSignature, 0, 0);
        sigLayout.Controls.Add(LblSignatureMeta, 0, 1);
        sigLayout.Controls.Add(sigButtons, 0, 2);
        grpSig.Controls.Add(sigLayout);

        topSplit.Controls.Add(grpData, 0, 0);
        topSplit.Controls.Add(grpSig, 1, 0);

        // 2. Action Bar
        FlowLayoutPanel actionBar = new() { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(2, 4, 2, 2) };
        actionBar.Controls.AddRange([BtnNuevo, BtnGuardar, BtnActualizar, BtnCancelar, BtnConsultarOt, BtnReasignar, BtnActivar, BtnDesactivar, BtnEliminar, BtnClearSelection, BtnExportar, BtnRefrescar]);

        // 3. Filter Bar
        FlowLayoutPanel filterBar = new() { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(2, 4, 2, 2) };
        Label lblBuscar = CambiasoTheme.CreateFieldLabel("Buscar:");
        lblBuscar.Margin = new Padding(6, 6, 6, 0);
        Label lblFiltro = CambiasoTheme.CreateFieldLabel("Mostrar:");
        lblFiltro.Margin = new Padding(10, 6, 6, 0);
        filterBar.Controls.AddRange([ChkSelectAllVisible, LblSelectedCount, lblBuscar, TxtSearch, lblFiltro, CmbFilter, LblStats]);

        // 4. Grid
        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("CATÁLOGO DE SOLICITADOS POR Y FIRMAS DIGITALES");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Controls.Add(GridRequesters);

        root.Controls.Add(topSplit, 0, 0);
        root.Controls.Add(actionBar, 0, 1);
        root.Controls.Add(filterBar, 0, 2);
        root.Controls.Add(grpGrid, 0, 3);
        Controls.Add(root);
    }

    private Button CreateActionButton(string text, AppIcon icon, ButtonTone tone, string tooltip)
    {
        Button btn = CambiasoTheme.CreateButton(text, icon, tone, _toolTip, tooltip, width: 90, height: 30);
        btn.AutoSize = true;
        btn.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        btn.MinimumSize = new Size(0, 30);
        btn.Padding = new Padding(6, 0, 8, 0);
        btn.Margin = new Padding(0, 1, 5, 1);
        return btn;
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        Font = CambiasoTheme.LabelFont,
        ForeColor = CambiasoTheme.TextMuted,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleRight,
        Padding = new Padding(0, 0, 6, 0)
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_syncSubscribed)
            {
                _ctx.SyncHub.DataChanged -= OnSyncDataChanged;
                _syncSubscribed = false;
            }

            if (PicSignature.Image is not null && !ReferenceEquals(PicSignature.Image, IconCatalog.Get(AppIcon.FileText, CambiasoTheme.BorderColor, 32)))
            {
                PicSignature.Image.Dispose();
                PicSignature.Image = null;
            }

            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class RequesterGridRow(Requester source) : IGridRowSource<Requester>
    {
        [Browsable(false)]
        public Requester Source { get; } = source;
        public int ID => Source.IdSol;
        public string SolicitadoPor => Source.Alias;
        public string NombreCompleto => Source.NombreCompleto;
        public string Telefono => Source.Telefono;
        public string Correo => Source.Correo;
        public string Firma => Source.FirmaBadge;
        public string Archivo => string.IsNullOrWhiteSpace(Source.FirmaNombre) ? "—" : Source.FirmaNombre;
        public string Estado => Source.Activo ? "Activo" : "Inactivo";
    }
}
