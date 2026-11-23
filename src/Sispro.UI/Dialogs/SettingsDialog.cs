using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

public sealed class SettingsDialog : Form
{
    private readonly SisproApplicationContext _ctx;
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _txtDbPath;
    private readonly TextBox _txtCompany;
    private readonly TextBox _txtRut;
    private readonly TextBox _txtDept;
    private readonly TextBox _txtAddress;

    public SettingsDialog(SisproApplicationContext ctx)
    {
        _ctx = ctx;

        Text = "Configuración del Sistema — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(640, 420);
        MinimumSize = new Size(600, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;

        Panel header = new() { Dock = DockStyle.Top, Height = 52, BackColor = CambiasoTheme.Primary, Padding = new Padding(16, 12, 16, 12) };
        header.Controls.Add(new Label
        {
            Text = "Configuración de Base de Datos y Empresa",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 15)
        });

        Panel body = new() { Dock = DockStyle.Fill, Padding = new Padding(18) };
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));

        _txtDbPath = new TextBox { Dock = DockStyle.Fill, Text = _ctx.Settings.DatabasePath, ReadOnly = true };
        Button btnBrowse = CambiasoTheme.CreateButton("Examinar", AppIcon.Search, ButtonTone.Secondary, _toolTip, "Seleccionar archivo SQLite (.db)", width: 108, height: 28);
        btnBrowse.Click += (_, _) => BrowseDatabase();

        _txtCompany = new TextBox { Dock = DockStyle.Fill, Text = _ctx.Settings.CompanyName };
        _txtRut = new TextBox { Dock = DockStyle.Fill, Text = _ctx.Settings.CompanyRut };
        _txtDept = new TextBox { Dock = DockStyle.Fill, Text = _ctx.Settings.CompanyDepartment };
        _txtAddress = new TextBox { Dock = DockStyle.Fill, Text = _ctx.Settings.CompanyAddress };

        AddRow(grid, 0, "Base de Datos:", _txtDbPath, btnBrowse);
        AddRow(grid, 1, "Empresa:", _txtCompany, null);
        AddRow(grid, 2, "RUT Empresa:", _txtRut, null);
        AddRow(grid, 3, "Departamento:", _txtDept, null);
        AddRow(grid, 4, "Dirección Planta:", _txtAddress, null);

        Button btnBackup = CambiasoTheme.CreateButton("Respaldar BD", AppIcon.Backup, ButtonTone.Accent, _toolTip, "Crear copia de seguridad consistente de sispro.db", width: 150, height: 34);
        btnBackup.Location = new Point(18, 215);
        btnBackup.Click += (_, _) => BackupDatabase();

        body.Controls.Add(grid);
        body.Controls.Add(btnBackup);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(12, 10, 16, 10),
            BackColor = CambiasoTheme.CardBackground
        };

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Descartar cambios", width: 120);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Button btnSave = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip, "Guardar configuración", width: 128);
        btnSave.Click += (_, _) => SaveSettings();

        footer.Controls.Add(btnCancel);
        footer.Controls.Add(btnSave);

        Controls.Add(body);
        Controls.Add(footer);
        Controls.Add(header);
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

    private void BrowseDatabase()
    {
        using OpenFileDialog dlg = new()
        {
            Title = "Seleccionar Base de Datos SISPRO (SQLite)",
            Filter = "Base de datos SQLite (*.db)|*.db|Todos los archivos (*.*)|*.*",
            CheckFileExists = false,
            FileName = Path.GetFileName(_txtDbPath.Text)
        };
        if (dlg.ShowDialog(this) == DialogResult.OK)
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
        if (dlg.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _ctx.ConnectionFactory.BackupTo(dlg.FileName);
            _ctx.Audit.Log(_ctx.CurrentSession?.Usuario.AliasUsuario ?? "SISTEMA", "Configuración", "RESPALDO_BD", $"Respaldo generado en {dlg.FileName}");
            CambiasoTheme.ShowInfo(this, $"Respaldo creado correctamente en:\n{dlg.FileName}");
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, $"Error al respaldar la base de datos: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(_txtDbPath.Text))
        {
            CambiasoTheme.ShowWarning(this, "Debe indicar una ruta de base de datos válida.");
            return;
        }

        try
        {
            _ctx.Settings.CompanyName = _txtCompany.Text.Trim();
            _ctx.Settings.CompanyRut = _txtRut.Text.Trim();
            _ctx.Settings.CompanyDepartment = _txtDept.Text.Trim();
            _ctx.Settings.CompanyAddress = _txtAddress.Text.Trim();

            if (!string.Equals(_ctx.Settings.DatabasePath, _txtDbPath.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _ctx.ReloadDatabase(_txtDbPath.Text.Trim());
            }
            else
            {
                AppSettingsStore.Save(_ctx.Settings);
            }

            CambiasoTheme.ShowInfo(this, "Configuración guardada correctamente.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, $"No se pudo aplicar la configuración: {ex.Message}");
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
