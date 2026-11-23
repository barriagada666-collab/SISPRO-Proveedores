using System.Drawing;
using Sispro.Core.Entities;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Diálogo corporativo para crear o editar un nodo del Árbol de Activos (10 niveles)
/// incluyendo clasificación estructural (Empresa, Planta, Sección, Máquina, Módulo, Componente)
/// y ficha técnica completa.
/// </summary>
public sealed class AssetNodeEditorDialog : Form
{
    private readonly AssetNode? _existing;
    private readonly AssetNode _parent;
    private readonly ComboBox _cmbTipoNodo;
    private readonly TextBox _txtDescripcion;
    private readonly TextBox _txtSap;
    private readonly TextBox _txtFabricante;
    private readonly TextBox _txtModelo;
    private readonly TextBox _txtSerie;
    private readonly TextBox _txtTipoEquipo;
    private readonly ComboBox _cmbEstadoOperativo;
    private readonly NumericUpDown _numPrioridad;
    private readonly TextBox _txtObservacion;
    private readonly int _nodeLevel;

    public AssetNode Result { get; private set; } = new();

    public AssetNodeEditorDialog(AssetNode? existing, AssetNode parent, string parentPath)
    {
        _existing = existing;
        _parent = parent;

        int parentDepth = string.IsNullOrWhiteSpace(parentPath)
            ? Math.Max(1, parent.Nivel)
            : parentPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length;

        _nodeLevel = existing is null
            ? Math.Clamp(parentDepth + 1, 1, 10)
            : (existing.ParentId.HasValue ? Math.Clamp(parentDepth + 1, 1, 10) : 1);

        Text = existing is null
            ? "Nuevo Nodo del Árbol — SISPRO"
            : $"Editar Nodo #{existing.IdActivo}: {existing.Descripcion} — SISPRO";
        Icon = IconCatalog.GetAppIcon();
        Size = new Size(640, 560);
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
            Text = existing is null
                ? $"NUEVO SUBNODO BAJO '{parent.Descripcion}'"
                : $"EDITAR NODO #{existing.IdActivo} — ÁRBOL DE ACTIVOS",
            Font = CambiasoTheme.SectionFont,
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        });

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 9,
            Padding = new Padding(16, 12, 16, 8)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        for (int r = 0; r < 8; r++)
        {
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        }
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        string parentDisplay = existing is not null && !existing.ParentId.HasValue
            ? "(Nodo raíz de la empresa)"
            : (string.IsNullOrWhiteSpace(parentPath) ? parent.Descripcion : parentPath);

        TextBox txtParent = new()
        {
            Text = parentDisplay,
            ReadOnly = true,
            BackColor = CambiasoTheme.Surface,
            ForeColor = CambiasoTheme.TextMuted,
            Dock = DockStyle.Fill
        };

        TextBox txtLevel = new()
        {
            Text = $"Nivel {_nodeLevel} de 10",
            ReadOnly = true,
            BackColor = CambiasoTheme.Surface,
            ForeColor = CambiasoTheme.Primary,
            Font = CambiasoTheme.LabelFont,
            Dock = DockStyle.Fill
        };

        _cmbTipoNodo = new ComboBox
        {
            Name = "cmbTipoNodo",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };

        string targetType = existing is null
            ? AssetNodeTypes.SuggestChildType(parent.Categoria)
            : AssetNodeTypes.Normalize(existing.Categoria);

        int selectedTypeIdx = -1;
        for (int i = 0; i < AssetNodeTypes.All.Count; i++)
        {
            (string code, string label) = AssetNodeTypes.All[i];
            _cmbTipoNodo.Items.Add(new NodeTypeOption(code, label));
            if (string.Equals(code, targetType, StringComparison.Ordinal))
            {
                selectedTypeIdx = i;
            }
        }

        if (selectedTypeIdx < 0 && !string.IsNullOrEmpty(targetType))
        {
            _cmbTipoNodo.Items.Add(new NodeTypeOption(targetType, targetType));
            selectedTypeIdx = _cmbTipoNodo.Items.Count - 1;
        }

        _cmbTipoNodo.SelectedIndex = selectedTypeIdx >= 0 ? selectedTypeIdx : _cmbTipoNodo.Items.Count - 1;

        _txtDescripcion = new TextBox
        {
            Name = "txtDescripcionNodo",
            MaxLength = 120,
            PlaceholderText = "Ej. PLANTA PE, LÍNEA EXTRUSIÓN 2, MOTOR PRINCIPAL...",
            Text = existing?.Descripcion ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _txtSap = new TextBox
        {
            Name = "txtSapNodo",
            MaxLength = 60,
            Text = existing?.CodSap ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _txtFabricante = new TextBox
        {
            Name = "txtFabricanteNodo",
            MaxLength = 80,
            Text = existing?.Fabricante ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _txtModelo = new TextBox
        {
            Name = "txtModeloNodo",
            MaxLength = 80,
            Text = existing?.Modelo ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _txtSerie = new TextBox
        {
            Name = "txtSerieNodo",
            MaxLength = 80,
            Text = existing?.Serie ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _txtTipoEquipo = new TextBox
        {
            Name = "txtTipoEquipoNodo",
            MaxLength = 80,
            Text = existing?.TipoEquipo ?? string.Empty,
            Dock = DockStyle.Fill
        };

        _cmbEstadoOperativo = new ComboBox
        {
            Name = "cmbEstadoOperativoNodo",
            DropDownStyle = ComboBoxStyle.DropDown,
            Dock = DockStyle.Fill
        };
        _cmbEstadoOperativo.Items.AddRange(["Operativa", "No Operativa", "En Mantención", "Fuera de Servicio"]);
        if (!string.IsNullOrWhiteSpace(existing?.EstadoOperativo))
        {
            _cmbEstadoOperativo.Text = existing.EstadoOperativo;
        }
        else if (existing is null)
        {
            _cmbEstadoOperativo.SelectedIndex = 0;
        }

        _numPrioridad = new NumericUpDown
        {
            Name = "numPrioridadNodo",
            Minimum = 0,
            Maximum = 10,
            Value = Math.Clamp(existing?.Prioridad ?? 0, 0, 10),
            Dock = DockStyle.Left,
            Width = 90
        };

        _txtObservacion = new TextBox
        {
            Name = "txtObservacionNodo",
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            MaxLength = 500,
            Text = existing?.Observacion ?? string.Empty,
            Dock = DockStyle.Fill
        };

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Nodo Padre:"), 0, 0);
        body.Controls.Add(txtParent, 1, 0);
        body.SetColumnSpan(txtParent, 3);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Nivel:"), 0, 1);
        body.Controls.Add(txtLevel, 1, 1);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Tipo de nodo:"), 2, 1);
        body.Controls.Add(_cmbTipoNodo, 3, 1);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Descripción *:"), 0, 2);
        body.Controls.Add(_txtDescripcion, 1, 2);
        body.SetColumnSpan(_txtDescripcion, 3);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Código SAP:"), 0, 3);
        body.Controls.Add(_txtSap, 1, 3);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Estado Op.:"), 2, 3);
        body.Controls.Add(_cmbEstadoOperativo, 3, 3);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Fabricante:"), 0, 4);
        body.Controls.Add(_txtFabricante, 1, 4);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Modelo:"), 2, 4);
        body.Controls.Add(_txtModelo, 3, 4);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("N° Serie:"), 0, 5);
        body.Controls.Add(_txtSerie, 1, 5);
        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Tipo Equipo:"), 2, 5);
        body.Controls.Add(_txtTipoEquipo, 3, 5);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Prioridad (0–10):"), 0, 6);
        body.Controls.Add(_numPrioridad, 1, 6);

        body.Controls.Add(CambiasoTheme.CreateFieldLabel("Observación:"), 0, 7);
        body.Controls.Add(_txtObservacion, 1, 7);
        body.SetColumnSpan(_txtObservacion, 3);
        body.SetRowSpan(_txtObservacion, 2);

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

        Button btnSave = CambiasoTheme.CreateButton("Guardar", AppIcon.Save, ButtonTone.Primary, width: 126, height: 32);
        btnSave.Click += (_, _) => SaveAndClose();

        footer.Controls.AddRange([btnCancel, btnSave]);
        AcceptButton = btnSave;
        CancelButton = btnCancel;

        Controls.Add(body);
        Controls.Add(header);
        Controls.Add(footer);
    }

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtDescripcion.Text))
        {
            CambiasoTheme.ShowWarning(this, "Debe ingresar la descripción del nodo.");
            _txtDescripcion.Focus();
            return;
        }

        string selectedCategory = (_cmbTipoNodo.SelectedItem as NodeTypeOption)?.Code ?? AssetNodeTypes.SinClasificar;
        Result = new AssetNode
        {
            IdActivo = _existing?.IdActivo ?? 0,
            ParentId = _existing is null ? _parent.IdActivo : _existing.ParentId,
            Nivel = _nodeLevel,
            Descripcion = _txtDescripcion.Text.Trim(),
            Activo = _existing?.Activo ?? true,
            Categoria = selectedCategory,
            CodSap = _txtSap.Text.Trim(),
            Fabricante = _txtFabricante.Text.Trim(),
            Modelo = _txtModelo.Text.Trim(),
            Serie = _txtSerie.Text.Trim(),
            TipoEquipo = _txtTipoEquipo.Text.Trim(),
            EstadoOperativo = _cmbEstadoOperativo.Text.Trim(),
            Prioridad = (int)_numPrioridad.Value,
            Observacion = _txtObservacion.Text.Trim()
        };

        DialogResult = DialogResult.OK;
    }

    private sealed record NodeTypeOption(string Code, string Label)
    {
        public override string ToString() => Label;
    }
}
