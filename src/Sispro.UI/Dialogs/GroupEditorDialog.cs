using System.Drawing;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo profesional para crear o editar un Grupo de Trabajo (GESTIÓN DE GRUPOS).
/// Incluye control de concurrencia optimista y asignación de responsable.
/// </summary>
public sealed class GroupEditorDialog : Form
{
    private readonly GroupManagementService _groupService;
    private readonly UserSession _session;
    private readonly UserGroup? _existing;
    private readonly TextBox _txtNombre;
    private readonly TextBox _txtDescripcion;
    private readonly ComboBox _cmbResponsable;
    private readonly ComboBox _cmbEstado;
    private readonly List<ResponsibleOption> _responsibleOptions = [];

    public int SavedGroupId { get; private set; }

    public GroupEditorDialog(
        GroupManagementService groupService,
        UserManagementService userService,
        UserSession session,
        UserGroup? existing = null)
    {
        _groupService = groupService;
        _session = session;
        _existing = existing;

        Text = existing is null ? "Nuevo Grupo de Trabajo — SISPRO" : $"Editar Grupo: {existing.Nombre} — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(540, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
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
            Text = existing is null ? "CREAR GRUPO / CATEGORÍA DE TRABAJO" : $"EDITAR GRUPO #{existing.IdGrupo}",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(16, 12, 16, 8)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 138));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _txtNombre = new TextBox
        {
            Name = "txtNombreGrupo",
            MaxLength = 60,
            PlaceholderText = "Ej. INSTRUMENTISTAS, SOLDADORES...",
            Text = existing?.Nombre ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _txtDescripcion = new TextBox
        {
            Name = "txtDescripcionGrupo",
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            MaxLength = 300,
            PlaceholderText = "Descripción operativa del grupo o especialidad...",
            Text = existing?.Descripcion ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _responsibleOptions.Add(new ResponsibleOption(null, "(Sin responsable asignado)"));
        foreach (User u in userService.Search(session, new UserSearchFilter { Activo = true }))
        {
            _responsibleOptions.Add(new ResponsibleOption(u.IdUsuario, $"{u.AliasUsuario} — {u.NombreCompleto} ({u.Nivel})"));
        }

        _cmbResponsable = new ComboBox
        {
            Name = "cmbResponsableGrupo",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        foreach (ResponsibleOption opt in _responsibleOptions)
        {
            _cmbResponsable.Items.Add(opt);
        }
        int respIdx = 0;
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.IdResponsable))
        {
            int idx = _responsibleOptions.FindIndex(o => string.Equals(o.IdUsuario, existing.IdResponsable, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) respIdx = idx;
        }
        if (_cmbResponsable.Items.Count > respIdx) _cmbResponsable.SelectedIndex = respIdx;

        _cmbEstado = new ComboBox
        {
            Name = "cmbEstadoGrupo",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        _cmbEstado.Items.AddRange(["Activo", "Inactivo"]);
        _cmbEstado.SelectedIndex = existing is null || existing.Activo ? 0 : 1;

        string metaText = existing is null
            ? "Creación: (Automática al guardar)  |  Integrantes: 0"
            : $"Creado: {existing.FechaCreacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)}  |  Modif.: {existing.FechaModificacion.ToString("dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture)}  |  Integrantes: {existing.CantidadIntegrantes}";

        Label lblMeta = new()
        {
            Text = metaText,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Nombre *:"), 0, 0);
        body.Controls.Add(_txtNombre, 1, 0);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Descripción:"), 0, 1);
        body.Controls.Add(_txtDescripcion, 1, 1);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Responsable:"), 0, 2);
        body.Controls.Add(_cmbResponsable, 1, 2);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Estado *:"), 0, 3);
        body.Controls.Add(_cmbEstado, 1, 3);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Trazabilidad:"), 0, 4);
        body.Controls.Add(lblMeta, 1, 4);

        FlowLayoutPanel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = CambiasoTheme.CardBackground,
            Padding = new Padding(12, 9, 12, 8)
        };

        Button btnCancel = CambiasoTheme.CreateButton("Cancelar", AppIcon.Cancel, ButtonTone.Secondary, width: 108, height: 32);
        btnCancel.Click += (_, _) => DialogResult = DialogResult.Cancel;

        Button btnSave = CambiasoTheme.CreateButton(existing is null ? "Crear Grupo" : "Guardar Cambios", AppIcon.Save, ButtonTone.Primary, width: 152, height: 32);
        btnSave.Click += (_, _) => Save();

        footer.Controls.AddRange([btnCancel, btnSave]);
        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private void Save()
    {
        try
        {
            ResponsibleOption? resp = _cmbResponsable.SelectedItem as ResponsibleOption;
            UserGroup model = new()
            {
                IdGrupo = _existing?.IdGrupo ?? 0,
                Nombre = _txtNombre.Text.Trim(),
                Descripcion = _txtDescripcion.Text.Trim(),
                IdResponsable = resp?.IdUsuario,
                Activo = _cmbEstado.SelectedIndex == 0,
                RowVersion = _existing?.RowVersion ?? 1
            };

            if (_existing is null)
            {
                SavedGroupId = _groupService.CreateGroup(_session, model);
            }
            else
            {
                _groupService.UpdateGroup(_session, model, expectedRowVersion: _existing.RowVersion);
                SavedGroupId = _existing.IdGrupo;
            }

            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private sealed record ResponsibleOption(string? IdUsuario, string Label)
    {
        public override string ToString() => Label;
    }
}
