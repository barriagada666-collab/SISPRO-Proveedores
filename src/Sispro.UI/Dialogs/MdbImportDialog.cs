using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Enums;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo de análisis comparativo e importación desde base Access (.mdb) hacia SQLite,
/// mostrando el resumen exacto de registros nuevos y modificados antes de aplicar cambios.
/// </summary>
public sealed class MdbImportDialog : Form
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly TextBox _txtMdbPath;
    private readonly Button _btnBrowse;
    private readonly Button _btnAnalyze;
    private readonly Button _btnCleanDb;
    private readonly Button _btnApply;
    private readonly Button _btnClose;

    private readonly Label _kpiMdbTotalVal;
    private readonly Label _kpiMdbTotalSub;
    private readonly Label _kpiNewVal;
    private readonly Label _kpiUpdateVal;
    private readonly Label _kpiUnchangedVal;
    private readonly Label _kpiExtraVal;
    private readonly Label _kpiExtraSub;

    private readonly CheckBox _chkRewriteAll;
    private readonly CheckBox _chkInsertNew;
    private readonly CheckBox _chkUpdateExisting;
    private readonly CheckBox _chkOverwriteEmpty;
    private readonly CheckBox _chkSyncRequesters;
    private readonly CheckBox _chkSyncWorkOrders;
    private readonly CheckBox _chkBackupFirst;

    private readonly ComboBox _cmbFilterAction;
    private readonly TextBox _txtFilterText;
    private readonly DataGridView _gridDiff;
    private readonly Label _lblStatus;

    private MdbImportPreview? _currentPreview;
    private bool _isBusy;
    private bool _suppressRewriteAnalysis;

    public MdbImportResult? LastResult { get; private set; }
    public MdbDatabaseCleanResult? LastCleanResult { get; private set; }

    internal Button BtnBrowse => _btnBrowse;
    internal Button BtnAnalyze => _btnAnalyze;
    internal Button BtnCleanDb => _btnCleanDb;
    internal Button BtnApply => _btnApply;
    internal Button BtnClose => _btnClose;
    internal CheckBox ChkRewriteAll => _chkRewriteAll;
    internal DataGridView GridDiff => _gridDiff;

    public MdbImportDialog(SisproApplicationContext ctx, UserSession session, string? initialMdbPath = null)
    {
        _ctx = ctx;
        _session = session;

        Text = "Importar Base de Datos Access (.mdb) — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(1060, 720);
        MinimumSize = new Size(920, 620);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 58, BackColor = CambiasoTheme.Primary, Padding = new Padding(18, 10, 18, 10) };
        header.Controls.Add(new Label
        {
            Text = "Importación y Actualización desde Microsoft Access (.mdb)",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(18, 8)
        });
        header.Controls.Add(new Label
        {
            Text = "Analiza cuántos proveedores, solicitantes y OTs cambiarán antes de actualizar su base de datos SISPRO",
            Font = CambiasoTheme.SmallFont,
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Location = new Point(18, 33)
        });

        string defaultDesktopMdb = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "OT_PROV.mdb");

        string prefillPath = !string.IsNullOrWhiteSpace(initialMdbPath)
            ? initialMdbPath
            : (File.Exists(defaultDesktopMdb) ? defaultDesktopMdb : string.Empty);

        // Barra selección archivo MDB
        Panel filePanel = new()
        {
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(16, 10, 16, 8),
            BackColor = CambiasoTheme.CardBackground
        };

        TableLayoutPanel fileGrid = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
        fileGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 146));

        Label lblFile = CambiasoTheme.CreateFieldLabel("Archivo Access (.mdb):");
        lblFile.Anchor = AnchorStyles.Left;

        _txtMdbPath = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = prefillPath,
            PlaceholderText = @"Ej: C:\Users\usuario\Desktop\OT_PROV.mdb"
        };

        _btnBrowse = CambiasoTheme.CreateButton("Examinar...", AppIcon.Upload, ButtonTone.Secondary, _toolTip, "Seleccionar archivo Microsoft Access (.mdb)", width: 126, height: 30);
        _btnBrowse.Click += async (_, _) => await BrowseMdbFileAsync();

        _btnAnalyze = CambiasoTheme.CreateButton("Analizar MDB", AppIcon.Search, ButtonTone.Primary, _toolTip, "Leer archivo .mdb y calcular cuántos datos cambiarán", width: 138, height: 30);
        _btnAnalyze.Click += async (_, _) => await RunAnalysisAsync();

        fileGrid.Controls.Add(lblFile, 0, 0);
        fileGrid.Controls.Add(_txtMdbPath, 1, 0);
        fileGrid.Controls.Add(_btnBrowse, 2, 0);
        fileGrid.Controls.Add(_btnAnalyze, 3, 0);
        filePanel.Controls.Add(fileGrid);

        // Panel KPI (cuántos datos)
        TableLayoutPanel kpiStrip = new()
        {
            Dock = DockStyle.Top,
            Height = 104,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(14, 8, 14, 10),
            BackColor = CambiasoTheme.Surface
        };
        kpiStrip.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        for (int i = 0; i < 5; i++)
        {
            kpiStrip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20f));
        }

        kpiStrip.Controls.Add(BuildKpiCard("PROVEEDORES EN MDB", CambiasoTheme.Primary, out _kpiMdbTotalVal, out _kpiMdbTotalSub), 0, 0);
        kpiStrip.Controls.Add(BuildKpiCard("NUEVOS A AGREGAR", Color.FromArgb(22, 163, 74), out _kpiNewVal, out Label subNew), 1, 0);
        subNew.Text = "No existen en sispro.db";
        kpiStrip.Controls.Add(BuildKpiCard("A ACTUALIZAR", Color.FromArgb(217, 119, 6), out _kpiUpdateVal, out Label subUpd), 2, 0);
        subUpd.Text = "Con cambios respecto a BD";
        kpiStrip.Controls.Add(BuildKpiCard("SIN CAMBIOS", CambiasoTheme.TextMuted, out _kpiUnchangedVal, out Label subSame), 3, 0);
        subSame.Text = "Ya están al día";
        kpiStrip.Controls.Add(BuildKpiCard("OTs / SOLICITANTES", CambiasoTheme.Accent, out _kpiExtraVal, out _kpiExtraSub), 4, 0);

        // Opciones + Filtros
        GroupBox grpOptions = CambiasoTheme.CreateGroupBox("Opciones de Sincronización y Filtros de Vista Previa");
        grpOptions.Dock = DockStyle.Top;
        grpOptions.AutoSize = true;
        grpOptions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        grpOptions.Padding = new Padding(12, 16, 12, 6);

        TableLayoutPanel optLayout = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3
        };
        optLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        optLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        optLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        optLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Panel rewriteStrip = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Color.FromArgb(254, 242, 242),
            Padding = new Padding(8, 5, 8, 5),
            Margin = new Padding(0, 0, 0, 5)
        };
        _chkRewriteAll = new CheckBox
        {
            Name = "chkMdbRewriteAll",
            Text = "Reescribir todo desde cero al importar (limpia Proveedores, Solicitantes y OTs de sispro.db e importa todo el MDB; conserva Usuarios, Activos y Configuración)",
            Checked = false,
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Danger,
            Margin = Padding.Empty
        };
        _toolTip.SetToolTip(_chkRewriteAll, "Limpia atómicamente Proveedores, Solicitantes y Órdenes de Trabajo de sispro.db y carga desde cero el contenido completo del archivo .mdb.");
        _chkRewriteAll.CheckedChanged += async (_, _) => await OnRewriteAllToggledAsync();
        rewriteStrip.Controls.Add(_chkRewriteAll);

        FlowLayoutPanel optFlow = new()
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        };

        _chkInsertNew = new CheckBox { Text = "Agregar proveedores nuevos", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 14, 2) };
        _chkUpdateExisting = new CheckBox { Text = "Actualizar datos de existentes", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 14, 2) };
        _chkOverwriteEmpty = new CheckBox { Text = "Sobrescribir con vacíos del MDB", Checked = false, AutoSize = true, Margin = new Padding(0, 3, 14, 2) };
        _chkSyncRequesters = new CheckBox { Text = "Sincronizar solicitantes", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 14, 2) };
        _chkSyncWorkOrders = new CheckBox { Text = "Importar también OTs detectadas", Checked = false, AutoSize = true, Margin = new Padding(0, 3, 14, 2) };
        _chkBackupFirst = new CheckBox { Text = "Respaldar sispro.db antes de aplicar", Checked = true, AutoSize = true, Margin = new Padding(0, 3, 10, 2) };

        _chkInsertNew.CheckedChanged += (_, _) => UpdateApplyStateAndSummary();
        _chkUpdateExisting.CheckedChanged += (_, _) => UpdateApplyStateAndSummary();
        _chkSyncRequesters.CheckedChanged += (_, _) => UpdateApplyStateAndSummary();
        _chkSyncWorkOrders.CheckedChanged += (_, _) => UpdateApplyStateAndSummary();
        _chkOverwriteEmpty.CheckedChanged += async (_, _) =>
        {
            if (!_suppressRewriteAnalysis && _currentPreview is not null && !_isBusy)
            {
                await RunAnalysisAsync();
            }
        };

        optFlow.Controls.AddRange([_chkInsertNew, _chkUpdateExisting, _chkOverwriteEmpty, _chkSyncRequesters, _chkSyncWorkOrders, _chkBackupFirst]);

        FlowLayoutPanel filterFlow = new()
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 4, 0, 2)
        };
        _cmbFilterAction = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 240,
            Margin = new Padding(4, 2, 16, 0)
        };
        _cmbFilterAction.Items.AddRange([
            "Todos los proveedores MDB",
            "Solo con cambios (Nuevos + Actualizar)",
            "Solo Nuevos",
            "Solo A Actualizar",
            "Solo Sin cambios"
        ]);
        _cmbFilterAction.SelectedIndex = 0;
        _cmbFilterAction.SelectedIndexChanged += (_, _) => BindDiffGrid();

        _txtFilterText = new TextBox
        {
            Width = 280,
            PlaceholderText = "Filtrar proveedor, correo, teléfono...",
            Margin = new Padding(4, 2, 0, 0)
        };
        _txtFilterText.TextChanged += (_, _) => BindDiffGrid();

        filterFlow.Controls.Add(new Label { Text = "Mostrar:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), ForeColor = CambiasoTheme.TextMuted });
        filterFlow.Controls.Add(_cmbFilterAction);
        filterFlow.Controls.Add(new Label { Text = "Buscar:", AutoSize = true, Margin = new Padding(0, 5, 4, 0), ForeColor = CambiasoTheme.TextMuted });
        filterFlow.Controls.Add(_txtFilterText);

        optLayout.Controls.Add(rewriteStrip, 0, 0);
        optLayout.Controls.Add(optFlow, 0, 1);
        optLayout.Controls.Add(filterFlow, 0, 2);
        grpOptions.Controls.Add(optLayout);

        // Grilla de diferencias
        GroupBox grpGrid = CambiasoTheme.CreateGroupBox("Detalle Comparativo de Proveedores (MDB vs sispro.db)");
        grpGrid.Dock = DockStyle.Fill;
        grpGrid.Padding = new Padding(10, 8, 10, 8);

        _gridDiff = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, ReadOnly = true };
        CambiasoTheme.StyleGrid(_gridDiff);
        CambiasoTheme.EnableDoubleBuffering(_gridDiff);
        ConfigureGridColumns();
        _gridDiff.CellFormatting += GridDiff_CellFormatting;
        grpGrid.Controls.Add(_gridDiff);

        // Footer
        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(16, 10, 16, 10)
        };

        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Presione 'Analizar MDB' para comparar el archivo Access contra su base de datos actual sin modificar datos.",
            ForeColor = CambiasoTheme.TextMuted,
            Font = CambiasoTheme.SmallFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        FlowLayoutPanel btnPanel = new()
        {
            Dock = DockStyle.Right,
            Width = 540,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnClose = CambiasoTheme.CreateButton("Cerrar", AppIcon.Cancel, ButtonTone.Secondary, _toolTip, "Cerrar ventana", width: 104, height: 34);
        _btnClose.Click += (_, _) => Close();

        _btnApply = CambiasoTheme.CreateButton("Actualizar BD desde MDB", AppIcon.Save, ButtonTone.Primary, _toolTip, "Aplicar los cambios analizados a la base de datos SQLite", width: 232, height: 34);
        _btnApply.Enabled = false;
        _btnApply.Click += async (_, _) => await ApplyImportAsync();

        _btnCleanDb = CambiasoTheme.CreateButton("Limpiar sispro.db", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Limpiar Proveedores, Solicitantes y OTs de sispro.db para importar limpio desde cero (conserva Usuarios, Activos y Configuración)", width: 174, height: 34);
        _btnCleanDb.Click += async (_, _) => await CleanDatabaseAsync();

        btnPanel.Controls.Add(_btnClose);
        btnPanel.Controls.Add(_btnApply);
        btnPanel.Controls.Add(_btnCleanDb);

        footer.Controls.Add(_lblStatus);
        footer.Controls.Add(btnPanel);

        Controls.Add(grpGrid);
        Controls.Add(grpOptions);
        Controls.Add(kpiStrip);
        Controls.Add(filePanel);
        Controls.Add(footer);
        Controls.Add(header);

        CancelButton = _btnClose;
        ResetKpis();
    }

    private void ConfigureGridColumns()
    {
        _gridDiff.AutoGenerateColumns = false;
        _gridDiff.Columns.Clear();
        _gridDiff.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Estado",
            HeaderText = "Acción",
            Width = 104,
            ReadOnly = true
        });
        _gridDiff.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Nombre",
            HeaderText = "Proveedor",
            Width = 210,
            ReadOnly = true
        });
        _gridDiff.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Campos",
            HeaderText = "Campos con cambio",
            Width = 145,
            ReadOnly = true
        });
        _gridDiff.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "DetalleCambio",
            HeaderText = "Comparación (Actual → Nuevo)",
            MinimumWidth = 240,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            ReadOnly = true
        });
        _gridDiff.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Telefono",
            HeaderText = "Teléfono final",
            Width = 110,
            ReadOnly = true
        });
        _gridDiff.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Correo",
            HeaderText = "Correo final",
            Width = 170,
            ReadOnly = true
        });
    }

    private void GridDiff_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != 0)
        {
            return;
        }

        string? action = e.Value?.ToString();
        if (e.CellStyle is null)
        {
            return;
        }

        e.CellStyle.Font = CambiasoTheme.ButtonFont;
        if (action == "Nuevo")
        {
            e.CellStyle.ForeColor = Color.FromArgb(21, 128, 61);
            e.CellStyle.BackColor = Color.FromArgb(240, 253, 244);
        }
        else if (action == "Actualizar")
        {
            e.CellStyle.ForeColor = Color.FromArgb(180, 83, 9);
            e.CellStyle.BackColor = Color.FromArgb(255, 251, 235);
        }
        else
        {
            e.CellStyle.ForeColor = CambiasoTheme.TextMuted;
        }
    }

    private static Panel BuildKpiCard(string title, Color accentColor, out Label lblValue, out Label lblSub)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(10, 6, 10, 6),
            BackColor = CambiasoTheme.CardBackground,
            BorderStyle = BorderStyle.FixedSingle
        };

        Label lblTitle = new()
        {
            Text = title,
            Font = CambiasoTheme.KpiTitleFont,
            ForeColor = CambiasoTheme.TextMuted,
            Dock = DockStyle.Top,
            Height = 16
        };

        lblValue = new Label
        {
            Text = "0",
            Font = CambiasoTheme.KpiCompactValueFont,
            ForeColor = accentColor,
            Dock = DockStyle.Top,
            Height = 26
        };

        lblSub = new Label
        {
            Text = string.Empty,
            Font = CambiasoTheme.KpiSubFont,
            ForeColor = CambiasoTheme.TextMuted,
            Dock = DockStyle.Fill
        };

        card.Controls.Add(lblSub);
        card.Controls.Add(lblValue);
        card.Controls.Add(lblTitle);
        return card;
    }

    private void ResetKpis()
    {
        _kpiMdbTotalVal.Text = "-";
        _kpiMdbTotalSub.Text = "Pendiente de análisis";
        _kpiNewVal.Text = "-";
        _kpiUpdateVal.Text = "-";
        _kpiUnchangedVal.Text = "-";
        _kpiExtraVal.Text = "-";
        _kpiExtraSub.Text = "Solicitantes / OTs";
    }

    private async Task BrowseMdbFileAsync()
    {
        using OpenFileDialog dlg = new()
        {
            Title = "Seleccionar base de datos Access (.mdb)",
            Filter = "Base de datos Access (*.mdb;*.accdb)|*.mdb;*.accdb|Todos los archivos (*.*)|*.*",
            CheckFileExists = true,
            FileName = File.Exists(_txtMdbPath.Text.Trim()) ? _txtMdbPath.Text.Trim() : string.Empty
        };

        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _txtMdbPath.Text = dlg.FileName;
        await RunAnalysisAsync();
    }

    private MdbImportOptions BuildOptions() => new()
    {
        InsertNewProviders = _chkRewriteAll.Checked || _chkInsertNew.Checked,
        UpdateExistingProviders = _chkRewriteAll.Checked || _chkUpdateExisting.Checked,
        OverwriteWithEmptyValues = _chkRewriteAll.Checked || _chkOverwriteEmpty.Checked,
        SyncNewRequesters = _chkSyncRequesters.Checked,
        SyncNewWorkOrders = _chkSyncWorkOrders.Checked,
        CreateBackupBeforeApply = _chkBackupFirst.Checked,
        RewriteAllFromMdb = _chkRewriteAll.Checked
    };

    private async Task OnRewriteAllToggledAsync()
    {
        bool rewrite = _chkRewriteAll.Checked;
        _suppressRewriteAnalysis = true;
        try
        {
            if (rewrite)
            {
                _chkInsertNew.Checked = true;
                _chkUpdateExisting.Checked = true;
                _chkOverwriteEmpty.Checked = true;
                _chkSyncRequesters.Checked = true;
                _chkSyncWorkOrders.Checked = true;
                _chkBackupFirst.Checked = true;
                _btnApply.Text = "Reescribir BD desde MDB";
                _btnApply.BackColor = CambiasoTheme.Danger;
                _btnApply.FlatAppearance.MouseOverBackColor = CambiasoTheme.DangerHover;
            }
            else
            {
                _chkOverwriteEmpty.Checked = false;
                _btnApply.Text = "Actualizar BD desde MDB";
                _btnApply.BackColor = CambiasoTheme.Primary;
                _btnApply.FlatAppearance.MouseOverBackColor = CambiasoTheme.PrimaryHover;
            }

            _chkInsertNew.Enabled = !rewrite && !_isBusy;
            _chkUpdateExisting.Enabled = !rewrite && !_isBusy;
            _chkOverwriteEmpty.Enabled = !rewrite && !_isBusy;
        }
        finally
        {
            _suppressRewriteAnalysis = false;
        }

        UpdateApplyStateAndSummary();
        if (_currentPreview is not null && !_isBusy)
        {
            await RunAnalysisAsync();
        }
    }

    private async Task CleanDatabaseAsync()
    {
        try
        {
            OperationalDatabaseCounts counts = _ctx.MdbImportService.GetOperationalCounts(_session);
            if (counts.ProvidersCount == 0 && counts.RequestersCount == 0 && counts.WorkOrdersCount == 0)
            {
                CambiasoTheme.ShowInfo(this, "La base de datos operativa sispro.db ya se encuentra limpia (0 proveedores, 0 solicitantes, 0 OTs). Puede importar directamente desde su archivo .mdb.");
                return;
            }

            string confirmMsg =
                "ATENCIÓN: Se limpiarán los datos operativos actuales de sispro.db para realizar una importación limpia desde cero:\n\n" +
                $"• Proveedores a eliminar: {counts.ProvidersCount:N0}\n" +
                $"• Solicitantes a eliminar: {counts.RequestersCount:N0}\n" +
                $"• Órdenes de trabajo a eliminar: {counts.WorkOrdersCount:N0} ({counts.WorkOrderDetailsCount:N0} ítems de detalle)\n\n" +
                "Se conservarán intactos: Usuarios, Perfiles, Grupos, Árbol de Activos, Configuración del Sistema y Auditoría.\n" +
                $"Respaldo automático previo: {(_chkBackupFirst.Checked ? "Sí (carpeta backups)" : "No")}\n\n" +
                "¿Confirma que desea limpiar sispro.db ahora?";

            if (!CambiasoTheme.Confirm(this, confirmMsg, "Confirmar Limpieza de sispro.db"))
            {
                return;
            }

            SetBusy(true, "Respaldando y limpiando datos operativos de sispro.db...");
            bool createBackup = _chkBackupFirst.Checked;
            MdbDatabaseCleanResult cleanRes = await Task.Run(() => _ctx.MdbImportService.CleanOperationalDatabase(_session, createBackup));
            if (IsDisposed) return;

            LastCleanResult = cleanRes;
            string infoMsg = "Limpieza de sispro.db completada correctamente:\n\n" + cleanRes.BuildSummaryText();
            if (!string.IsNullOrWhiteSpace(cleanRes.BackupPath))
            {
                infoMsg += $"\n\nRespaldo previo guardado en:\n{cleanRes.BackupPath}";
            }

            CambiasoTheme.ShowInfo(this, infoMsg);

            if (!string.IsNullOrWhiteSpace(_txtMdbPath.Text.Trim()) && File.Exists(_txtMdbPath.Text.Trim()))
            {
                await RunAnalysisAsync();
            }
            else
            {
                _currentPreview = null;
                ResetKpis();
                _gridDiff.Rows.Clear();
                UpdateApplyStateAndSummary();
            }
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                CambiasoTheme.ShowError(this, $"Error al limpiar sispro.db:\n{ex.Message}");
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private async Task RunAnalysisAsync()
    {
        string path = _txtMdbPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            CambiasoTheme.ShowWarning(this, "Seleccione primero un archivo .mdb para analizar.");
            return;
        }

        SetBusy(true, "Leyendo y comparando archivo MDB contra sispro.db...");
        try
        {
            MdbImportOptions opts = BuildOptions();
            MdbImportPreview preview = await Task.Run(() => _ctx.MdbImportService.AnalyzeMdb(_session, path, opts));
            if (IsDisposed) return;

            _currentPreview = preview;
            _kpiMdbTotalVal.Text = preview.MdbProvidersTotal.ToString();
            _kpiMdbTotalSub.Text = $"BD actual: {preview.DbProvidersBefore} prov.";
            _kpiNewVal.Text = preview.NewProvidersCount.ToString();
            _kpiUpdateVal.Text = preview.UpdatedProvidersCount.ToString();
            _kpiUnchangedVal.Text = preview.UnchangedProvidersCount.ToString();
            _kpiExtraVal.Text = $"+{preview.NewWorkOrdersCount} OT";
            _kpiExtraSub.Text = $"+{preview.NewRequestersCount} sol. nuevos ({preview.NewWorkOrderDetailsCount} ítems OT)";

            if (preview.TotalProviderChanges > 0)
            {
                _cmbFilterAction.SelectedIndex = 1; // Mostrar cambios primero
            }
            else
            {
                _cmbFilterAction.SelectedIndex = 0;
            }

            BindDiffGrid();
            UpdateApplyStateAndSummary();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                _currentPreview = null;
                ResetKpis();
                _gridDiff.Rows.Clear();
                UpdateApplyStateAndSummary();
                CambiasoTheme.ShowError(this, $"No se pudo analizar el archivo MDB:\n{ex.Message}");
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private void BindDiffGrid()
    {
        _gridDiff.Rows.Clear();
        if (_currentPreview is null)
        {
            return;
        }

        int mode = _cmbFilterAction.SelectedIndex;
        string q = _txtFilterText.Text.Trim();

        IEnumerable<MdbProviderDiffItem> rows = _currentPreview.ProviderDiffs;
        rows = mode switch
        {
            1 => rows.Where(r => r.Action != MdbDiffAction.SinCambios),
            2 => rows.Where(r => r.Action == MdbDiffAction.Nuevo),
            3 => rows.Where(r => r.Action == MdbDiffAction.Actualizar),
            4 => rows.Where(r => r.Action == MdbDiffAction.SinCambios),
            _ => rows
        };

        if (!string.IsNullOrWhiteSpace(q))
        {
            rows = rows.Where(r =>
                r.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.CorreoFinal.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.TelefonoFinal.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.PreviewBeforeAfter.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        foreach (MdbProviderDiffItem item in rows)
        {
            _gridDiff.Rows.Add(
                item.ActionLabel,
                item.Nombre,
                item.ChangedFieldsSummary,
                item.PreviewBeforeAfter,
                item.TelefonoFinal,
                item.CorreoFinal);
        }
    }

    private void UpdateApplyStateAndSummary()
    {
        if (_currentPreview is null)
        {
            _btnApply.Enabled = false;
            _lblStatus.Text = _chkRewriteAll.Checked
                ? "Modo REESCRITURA COMPLETA activo. Presione 'Analizar MDB' para previsualizar la importación desde cero."
                : "Presione 'Analizar MDB' para comparar el archivo Access contra su base de datos actual.";
            return;
        }

        if (_chkRewriteAll.Checked)
        {
            int provAll = _currentPreview.MdbProvidersTotal;
            int reqAll = _chkSyncRequesters.Checked ? _currentPreview.MdbRequestersTotal : 0;
            int otAll = _chkSyncWorkOrders.Checked ? _currentPreview.MdbWorkOrdersTotal : 0;
            _btnApply.Enabled = !_isBusy && (provAll + reqAll + otAll > 0 || _currentPreview.DbProvidersBefore + _currentPreview.DbWorkOrdersBefore > 0);
            _lblStatus.Text =
                $"REESCRITURA COMPLETA: limpiará BD actual y cargará {provAll} prov., {reqAll} sol. y {otAll} OTs desde el MDB.";
            return;
        }

        int provAdd = _chkInsertNew.Checked ? _currentPreview.NewProvidersCount : 0;
        int provUpd = _chkUpdateExisting.Checked ? _currentPreview.UpdatedProvidersCount : 0;
        int reqChanges = _chkSyncRequesters.Checked ? _currentPreview.NewOrUpdatedRequesters.Count : 0;
        int otAdd = _chkSyncWorkOrders.Checked ? _currentPreview.NewWorkOrdersCount : 0;

        int totalPending = provAdd + provUpd + reqChanges + otAdd;
        _btnApply.Enabled = !_isBusy && totalPending > 0;

        _lblStatus.Text = totalPending > 0
            ? $"Listo para actualizar: +{provAdd} proveedores nuevos, {provUpd} por actualizar, {reqChanges} solicitantes, +{otAdd} OTs nuevas."
            : "No hay cambios pendientes para aplicar con las opciones seleccionadas (su base de datos ya coincide con el MDB).";
    }

    private async Task ApplyImportAsync()
    {
        if (_currentPreview is null)
        {
            return;
        }

        bool fullRewrite = _chkRewriteAll.Checked;
        string confirmMsg;
        string confirmTitle;
        if (fullRewrite)
        {
            int reqCount = _chkSyncRequesters.Checked ? _currentPreview.MdbRequestersTotal : 0;
            int otCount = _chkSyncWorkOrders.Checked ? _currentPreview.MdbWorkOrdersTotal : 0;
            int detCount = _chkSyncWorkOrders.Checked ? _currentPreview.NewWorkOrderDetailsCount : 0;
            confirmTitle = "Confirmar REESCRITURA COMPLETA desde MDB";
            confirmMsg =
                $"ATENCIÓN — REESCRITURA LIMPIA DESDE CERO:\n{_currentPreview.MdbPath}\n\n" +
                $"Se eliminarán los datos operativos actuales de sispro.db ({_currentPreview.DbProvidersBefore:N0} prov., {_currentPreview.DbRequestersBefore:N0} sol., {_currentPreview.DbWorkOrdersBefore:N0} OTs) y se importarán desde cero:\n" +
                $"• Proveedores del MDB: {_currentPreview.MdbProvidersTotal:N0}\n" +
                $"• Solicitantes del MDB: {reqCount:N0}\n" +
                $"• Órdenes de trabajo del MDB: {otCount:N0} ({detCount:N0} ítems)\n\n" +
                "Conservados intactos: Usuarios, Perfiles, Grupos, Árbol de Activos, Configuración y Auditoría.\n" +
                $"• Respaldo previo automático: {(_chkBackupFirst.Checked ? "Sí" : "No")}\n\n" +
                "¿Confirma que desea reescribir toda la base operativa ahora?";
        }
        else
        {
            int provAdd = _chkInsertNew.Checked ? _currentPreview.NewProvidersCount : 0;
            int provUpd = _chkUpdateExisting.Checked ? _currentPreview.UpdatedProvidersCount : 0;
            int reqChanges = _chkSyncRequesters.Checked ? _currentPreview.NewOrUpdatedRequesters.Count : 0;
            int otAdd = _chkSyncWorkOrders.Checked ? _currentPreview.NewWorkOrdersCount : 0;
            confirmTitle = "Confirmar importación MDB";
            confirmMsg =
                $"Se aplicará la actualización desde:\n{_currentPreview.MdbPath}\n\n" +
                $"• Proveedores nuevos a agregar: {provAdd}\n" +
                $"• Proveedores existentes a actualizar: {provUpd}\n" +
                $"• Solicitantes nuevos/actualizados: {reqChanges}\n" +
                $"• Órdenes de trabajo nuevas a importar: {otAdd} ({(_chkSyncWorkOrders.Checked ? _currentPreview.NewWorkOrderDetailsCount : 0)} ítems)\n" +
                $"• Respaldo previo automático: {(_chkBackupFirst.Checked ? "Sí" : "No")}\n\n" +
                "¿Desea actualizar su base de datos ahora?";
        }

        if (!CambiasoTheme.Confirm(this, confirmMsg, confirmTitle))
        {
            return;
        }

        SetBusy(true, fullRewrite ? "Respaldando, limpiando y reescribiendo sispro.db desde MDB..." : "Aplicando cambios y actualizando sispro.db...");
        try
        {
            MdbImportOptions opts = BuildOptions();
            string path = _currentPreview.MdbPath;
            MdbImportResult result = await Task.Run(() => _ctx.MdbImportService.ApplyImport(_session, path, opts));
            if (IsDisposed) return;

            LastResult = result;
            string msg = "Actualización completada correctamente:\n\n" + result.BuildSummaryText();
            if (!string.IsNullOrWhiteSpace(result.BackupPath))
            {
                msg += $"\n\nRespaldo previo guardado en:\n{result.BackupPath}";
            }

            if (result.Warnings.Count > 0)
            {
                msg += $"\n\nAdvertencias ({result.Warnings.Count}):\n• " + string.Join("\n• ", result.Warnings.Take(5));
            }

            CambiasoTheme.ShowInfo(this, msg);
            await RunAnalysisAsync();
        }
        catch (Exception ex)
        {
            if (!IsDisposed)
            {
                CambiasoTheme.ShowError(this, $"Error durante la actualización desde MDB:\n{ex.Message}");
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                SetBusy(false);
            }
        }
    }

    private void SetBusy(bool busy, string? status = null)
    {
        _isBusy = busy;
        UseWaitCursor = busy;
        _btnBrowse.Enabled = !busy;
        _btnAnalyze.Enabled = !busy;
        _btnCleanDb.Enabled = !busy;
        _btnClose.Enabled = !busy;
        _chkRewriteAll.Enabled = !busy;
        _chkInsertNew.Enabled = !busy && !_chkRewriteAll.Checked;
        _chkUpdateExisting.Enabled = !busy && !_chkRewriteAll.Checked;
        _chkOverwriteEmpty.Enabled = !busy && !_chkRewriteAll.Checked;
        if (busy)
        {
            _btnApply.Enabled = false;
            if (!string.IsNullOrWhiteSpace(status))
            {
                _lblStatus.Text = status;
            }
        }
        else
        {
            UpdateApplyStateAndSummary();
        }
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
