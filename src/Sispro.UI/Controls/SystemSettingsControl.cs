using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Dialogs;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Panel de Configuración del Sistema integrado dentro de Mantenedores (exclusivo Administrador),
/// con acciones diferenciadas de Probar, Aplicar (temporal) y Guardar (persistente en BD).
/// </summary>
public sealed class SystemSettingsControl : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly TextBox _txtDbPath;
    private readonly TextBox _txtCompany;
    private readonly TextBox _txtRut;
    private readonly TextBox _txtDept;
    private readonly TextBox _txtAddress;
    private readonly Label _lblStatusBanner;
    private readonly TextBox _txtDiagnostic;

    internal Button BtnBrowseDb { get; }
    internal Button BtnTest { get; }
    internal Button BtnApply { get; }
    internal Button BtnSave { get; }
    internal Button BtnRevert { get; }
    internal Button BtnBackup { get; }
    internal Button BtnImportMdb { get; }
    internal TextBox TxtCompany => _txtCompany;
    internal TextBox TxtDbPath => _txtDbPath;
    internal TextBox TxtRut => _txtRut;

    public SystemSettingsControl(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        AuthService.EnsureAdministrator(session, ctx.Users, ctx.Audit, "Configuración", "Acceder a Configuración del Sistema");

        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;
        Padding = new Padding(10);

        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 252));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _lblStatusBanner = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = CambiasoTheme.SubtleFill,
            ForeColor = CambiasoTheme.PrimaryDark,
            Font = CambiasoTheme.LabelFont,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 0, 0, 8),
            Text = "Configuración sincronizada con la base de datos. Use Probar, Aplicar (temporal) o Guardar (permanente)."
        };

        GroupBox grpParams = CambiasoTheme.CreateGroupBox("Parámetros Generales de Base de Datos y Empresa (Centralizados en BD)");
        grpParams.Dock = DockStyle.Fill;
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
            Padding = new Padding(12, 8, 12, 8)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 126));

        _txtDbPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = false };
        BtnBrowseDb = CambiasoTheme.CreateButton("Examinar...", AppIcon.Search, ButtonTone.Secondary, _toolTip, "Seleccionar archivo SQLite (.db)", width: 118, height: 28);
        BtnBrowseDb.Click += (_, _) => BrowseDatabase();

        _txtCompany = new TextBox { Dock = DockStyle.Fill, MaxLength = 150 };
        _txtRut = new TextBox { Dock = DockStyle.Fill, MaxLength = 20 };
        _txtDept = new TextBox { Dock = DockStyle.Fill, MaxLength = 120 };
        _txtAddress = new TextBox { Dock = DockStyle.Fill, MaxLength = 200 };

        AddRow(grid, 0, "Base de Datos:", _txtDbPath, BtnBrowseDb);
        AddRow(grid, 1, "Empresa:", _txtCompany, null);
        AddRow(grid, 2, "RUT Empresa:", _txtRut, null);
        AddRow(grid, 3, "Departamento:", _txtDept, null);
        AddRow(grid, 4, "Dirección Planta:", _txtAddress, null);
        grpParams.Controls.Add(grid);

        FlowLayoutPanel actionBar = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 8, 0, 8)
        };

        BtnTest = CambiasoTheme.CreateButton("Probar", AppIcon.Check, ButtonTone.Secondary, _toolTip,
            "Validar parámetros y compatibilidad de BD sin modificar datos ni configuración activa", width: 116, height: 34);
        BtnTest.Click += (_, _) => RunTest();

        BtnApply = CambiasoTheme.CreateButton("Aplicar", AppIcon.Refresh, ButtonTone.Accent, _toolTip,
            "Aplicar temporalmente los cambios en la interfaz activa para verificar resultados antes de guardar", width: 118, height: 34);
        BtnApply.Click += (_, _) => RunApplyTemporary();

        BtnSave = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip,
            "Confirmar y guardar definitivamente la configuración en la base de datos", width: 124, height: 34);
        BtnSave.Click += (_, _) => RunSavePermanent();

        BtnRevert = CambiasoTheme.CreateButton("Revertir Temp.", AppIcon.Eraser, ButtonTone.Secondary, _toolTip,
            "Restaurar la última configuración guardada descartando cambios temporales", width: 146, height: 34);
        BtnRevert.Enabled = false;
        BtnRevert.Click += (_, _) => RunRevertTemporary();

        BtnBackup = CambiasoTheme.CreateButton("Respaldar BD", AppIcon.Backup, ButtonTone.Secondary, _toolTip,
            "Crear copia de seguridad consistente en caliente de sispro.db", width: 142, height: 34);
        BtnBackup.Click += (_, _) => BackupDatabase();

        BtnImportMdb = CambiasoTheme.CreateButton("Importar MDB", AppIcon.Upload, ButtonTone.Secondary, _toolTip,
            "Analizar e importar proveedores y OTs desde base Access (.mdb)", width: 144, height: 34);
        BtnImportMdb.Click += (_, _) => OpenMdbImport();

        actionBar.Controls.AddRange([BtnTest, BtnApply, BtnSave, BtnRevert, BtnBackup, BtnImportMdb]);

        GroupBox grpDiag = CambiasoTheme.CreateGroupBox("Resultado de Validación, Pruebas y Estado de Configuración");
        grpDiag.Dock = DockStyle.Fill;
        _txtDiagnostic = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = CambiasoTheme.CardBackground,
            Font = CambiasoTheme.SmallFont,
            Text = "Presione 'Probar' para validar la configuración, 'Aplicar' para probar cambios temporalmente en la interfaz o 'Guardar' para persistirlos en la base de datos."
        };
        grpDiag.Controls.Add(_txtDiagnostic);

        root.Controls.Add(_lblStatusBanner, 0, 0);
        root.Controls.Add(grpParams, 0, 1);
        root.Controls.Add(actionBar, 0, 2);
        root.Controls.Add(grpDiag, 0, 3);
        Controls.Add(root);

        LoadFromContext();
    }

    public void LoadFromContext()
    {
        _txtDbPath.Text = _ctx.Settings.DatabasePath;
        _txtCompany.Text = _ctx.Settings.CompanyName;
        _txtRut.Text = _ctx.Settings.CompanyRut;
        _txtDept.Text = _ctx.Settings.CompanyDepartment;
        _txtAddress.Text = _ctx.Settings.CompanyAddress;
        UpdateTemporaryBanner();
    }

    private AppSettings ReadCandidate() => new()
    {
        DatabasePath = _txtDbPath.Text.Trim(),
        CompanyName = _txtCompany.Text.Trim(),
        CompanyRut = _txtRut.Text.Trim(),
        CompanyDepartment = _txtDept.Text.Trim(),
        CompanyAddress = _txtAddress.Text.Trim(),
        CompanyCity = _ctx.Settings.CompanyCity,
        LegacyAccessTreePath = _ctx.Settings.LegacyAccessTreePath
    };

    private void RunTest()
    {
        try
        {
            SystemConfigTestResult res = _ctx.SystemConfigService.TestConfiguration(_session, ReadCandidate());
            RenderDiagnostic("PRUEBA DE CONFIGURACIÓN (sin guardar cambios)", res);
            if (res.Succeeded)
            {
                CambiasoTheme.ShowInfo(this, res.Summary);
            }
            else
            {
                CambiasoTheme.ShowWarning(this, res.Summary + "\n\n• " + string.Join("\n• ", res.Errors));
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void RunApplyTemporary()
    {
        try
        {
            SystemConfigTestResult res = _ctx.SystemConfigService.ApplyTemporaryConfiguration(_session, ReadCandidate());
            RenderDiagnostic("APLICACIÓN TEMPORAL (no guardada en BD)", res);
            UpdateTemporaryBanner();
            if (res.Succeeded)
            {
                CambiasoTheme.ShowInfo(this, res.Summary);
            }
            else
            {
                CambiasoTheme.ShowWarning(this, res.Summary + "\n\n• " + string.Join("\n• ", res.Errors));
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void RunSavePermanent()
    {
        try
        {
            AppSettings candidate = ReadCandidate();
            SystemConfigTestResult preCheck = _ctx.SystemConfigService.TestConfiguration(_session, candidate);
            if (!preCheck.Succeeded)
            {
                RenderDiagnostic("VALIDACIÓN PREVIA AL GUARDADO — ERRORES DETECTADOS", preCheck);
                CambiasoTheme.ShowWarning(this, preCheck.Summary + "\n\n• " + string.Join("\n• ", preCheck.Errors));
                return;
            }

            if (!CambiasoTheme.Confirm(
                this,
                $"¿Confirma guardar definitivamente esta configuración en la base de datos?\n\n" +
                $"• Empresa: {candidate.CompanyName}\n" +
                $"• RUT: {(string.IsNullOrWhiteSpace(candidate.CompanyRut) ? "(vacío)" : candidate.CompanyRut)}\n" +
                $"• Departamento: {candidate.CompanyDepartment}\n" +
                $"• Base de Datos: {candidate.DatabasePath}",
                "Confirmar Guardado de Configuración"))
            {
                return;
            }

            SystemConfigTestResult res = _ctx.SystemConfigService.SaveConfiguration(_session, candidate);
            RenderDiagnostic("GUARDADO PERMANENTE EN BASE DE DATOS", res);
            LoadFromContext();
            if (res.Succeeded)
            {
                CambiasoTheme.ShowInfo(this, res.Summary);
            }
            else
            {
                CambiasoTheme.ShowWarning(this, res.Summary);
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, $"No se pudo guardar la configuración: {ex.Message}");
        }
    }

    private void RunRevertTemporary()
    {
        try
        {
            _ctx.SystemConfigService.RevertTemporaryConfiguration(_session);
            LoadFromContext();
            _txtDiagnostic.Text = $"[{DateTime.Now:HH:mm:ss}] Cambios temporales descartados. Se restauró la configuración guardada en BD.";
            CambiasoTheme.ShowInfo(this, "Se restauró la última configuración guardada.");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private void UpdateTemporaryBanner()
    {
        bool temp = _ctx.SystemConfigService.HasTemporaryChanges;
        BtnRevert.Enabled = temp;
        if (temp)
        {
            _lblStatusBanner.BackColor = Color.FromArgb(254, 243, 199);
            _lblStatusBanner.ForeColor = Color.FromArgb(146, 64, 14);
            _lblStatusBanner.Text = "MODO TEMPORAL ACTIVO: los cambios se reflejan en la interfaz pero aún NO se han guardado definitivamente en la BD.";
        }
        else
        {
            _lblStatusBanner.BackColor = CambiasoTheme.SubtleFill;
            _lblStatusBanner.ForeColor = CambiasoTheme.PrimaryDark;
            _lblStatusBanner.Text = "Configuración sincronizada con la base de datos. Use Probar, Aplicar (temporal) o Guardar (permanente).";
        }
    }

    private void RenderDiagnostic(string heading, SystemConfigTestResult res)
    {
        List<string> lines = [$"[{DateTime.Now:HH:mm:ss}] {heading}", res.Summary];
        if (res.Checks.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Verificaciones OK:");
            lines.AddRange(res.Checks.Select(c => $"  ✓ {c}"));
        }

        if (res.Errors.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Errores detectados:");
            lines.AddRange(res.Errors.Select(e => $"  ✗ {e}"));
        }

        _txtDiagnostic.Text = string.Join(Environment.NewLine, lines);
    }

    private void BrowseDatabase()
    {
        using OpenFileDialog dlg = new()
        {
            Title = "Seleccionar Base de Datos SISPRO (SQLite)",
            Filter = "Base de datos SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*",
            CheckFileExists = false,
            FileName = Path.GetFileName(_txtDbPath.Text)
        };
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
        {
            _txtDbPath.Text = dlg.FileName;
        }
    }

    private void BackupDatabase()
    {
        using SaveFileDialog dlg = new()
        {
            Title = "Guardar respaldo de base de datos",
            Filter = "Respaldo SQLite (*.db)|*.db",
            FileName = $"sispro_respaldo_{DateTime.Now:yyyyMMdd_HHmm}.db"
        };
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _ctx.ConnectionFactory.BackupTo(dlg.FileName);
            _ctx.Audit.Log(_session.Usuario.AliasUsuario, "Configuración", "RESPALDO_BD", $"Respaldo generado en {dlg.FileName}");
            CambiasoTheme.ShowInfo(this, $"Respaldo creado correctamente en:\n{dlg.FileName}");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, $"Error al respaldar la base de datos: {ex.Message}");
        }
    }

    private void OpenMdbImport()
    {
        using MdbImportDialog dlg = new(_ctx, _session);
        dlg.ShowDialog(FindForm());
    }

    private static void AddRow(TableLayoutPanel grid, int row, string label, Control input, Control? extra)
    {
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        Label lbl = CambiasoTheme.CreateFieldLabel(label);
        lbl.Anchor = AnchorStyles.Left;
        grid.Controls.Add(lbl, 0, row);
        grid.Controls.Add(input, 1, row);
        if (extra is not null)
        {
            grid.Controls.Add(extra, 2, row);
        }
        else
        {
            grid.SetColumnSpan(input, 2);
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
