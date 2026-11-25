using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using Sispro.Core.Dtos;

namespace Sispro.UI.Theme;

/// <summary>
/// Controlador moderno de checkboxes dibujados directamente sobre los encabezados de columna
/// de un <see cref="DataGridView"/> (diseño de dos zonas: título completo arriba + casilla
/// interactiva centrada abajo) para elegir dinámicamente qué campos se incluirán al exportar
/// (Excel, PDF, Word y CSV) sin alterar el ordenamiento al hacer clic en el título.
/// </summary>
public sealed class GridExportColumnHeaderController
{
    private const int CheckBoxSize = 15;
    private const int CheckBoxZoneTop = 22;

    private static readonly Color HeaderActiveFill = CambiasoTheme.Primary;
    private static readonly Color HeaderActiveHoverFill = CambiasoTheme.PrimaryHover;
    private static readonly Color HeaderInactiveFill = Color.FromArgb(78, 99, 117);
    private static readonly Color HeaderInactiveHoverFill = Color.FromArgb(92, 115, 134);
    private static readonly Color CheckBoxActiveFill = Color.FromArgb(0, 159, 212);
    private static readonly Color CheckBoxActiveBorder = Color.FromArgb(148, 224, 248);
    private static readonly Color CheckBoxInactiveFill = Color.FromArgb(62, 81, 98);
    private static readonly Color CheckBoxInactiveBorder = Color.FromArgb(196, 212, 224);
    private static readonly Color HeaderInactiveText = Color.FromArgb(207, 220, 232);
    private static readonly Color HeaderGridDivider = Color.FromArgb(30, 82, 118);

    private readonly DataGridView _grid;
    private readonly List<(string PropertyName, WorkOrderExportField Field)> _gridColumnMappings;
    private readonly Dictionary<string, WorkOrderExportField> _propertyToField = new(StringComparer.Ordinal);
    private readonly HashSet<WorkOrderExportField> _selectedFields = [];
    private readonly Action? _onSelectionChanged;
    private int _hoveredColIndex = -1;
    private bool _hoveredOverCheckBox;
    private bool _attached;

    public GridExportColumnHeaderController(
        DataGridView grid,
        IReadOnlyList<(string PropertyName, WorkOrderExportField Field)> gridColumnMappings,
        Action? onSelectionChanged = null)
    {
        _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        _gridColumnMappings = gridColumnMappings?.ToList() ?? throw new ArgumentNullException(nameof(gridColumnMappings));
        _onSelectionChanged = onSelectionChanged;

        foreach ((string prop, WorkOrderExportField exportField) in _gridColumnMappings)
        {
            _propertyToField[prop] = exportField;
            _selectedFields.Add(exportField);
        }

        _grid.CellPainting += OnCellPainting;
        _grid.ColumnHeaderMouseClick += OnColumnHeaderMouseClick;
        _grid.CellMouseMove += OnCellMouseMove;
        _grid.CellMouseLeave += OnCellMouseLeave;
        _grid.DataBindingComplete += OnDataBindingComplete;
        _attached = true;
    }

    public int GridFieldCount => _gridColumnMappings.Count;

    public int SelectedGridFieldCount => _gridColumnMappings.Count(m => _selectedFields.Contains(m.Field));

    public int SelectedExtraFieldCount => _selectedFields.Count - SelectedGridFieldCount;

    /// <summary>
    /// Campos seleccionados para exportación en orden canónico de presentación.
    /// </summary>
    public IReadOnlyList<WorkOrderExportField> SelectedFields
    {
        get
        {
            List<WorkOrderExportField> ordered = [];
            foreach (WorkOrderExportField exportField in WorkOrderExportFieldCatalog.AllFields)
            {
                if (_selectedFields.Contains(exportField))
                {
                    ordered.Add(exportField);
                }
            }

            return ordered;
        }
    }

    public IReadOnlyList<(string PropertyName, WorkOrderExportField Field)> GridMappings => _gridColumnMappings;

    public bool IsFieldSelected(WorkOrderExportField exportField) => _selectedFields.Contains(exportField);

    public void SetFieldSelected(WorkOrderExportField exportField, bool selected)
    {
        bool changed = selected ? _selectedFields.Add(exportField) : _selectedFields.Remove(exportField);
        if (!changed)
        {
            return;
        }

        UpdateHeaderTooltips();
        InvalidateHeaders();
        _onSelectionChanged?.Invoke();
    }

    public void ToggleField(WorkOrderExportField exportField) =>
        SetFieldSelected(exportField, !IsFieldSelected(exportField));

    public void SelectAllGridFields()
    {
        bool changed = false;
        foreach ((_, WorkOrderExportField exportField) in _gridColumnMappings)
        {
            changed |= _selectedFields.Add(exportField);
        }

        if (changed)
        {
            UpdateHeaderTooltips();
            InvalidateHeaders();
            _onSelectionChanged?.Invoke();
        }
    }

    public void SelectEssentialFields()
    {
        _selectedFields.Clear();
        foreach (WorkOrderExportField exportField in WorkOrderExportFieldCatalog.EssentialFields)
        {
            _selectedFields.Add(exportField);
        }

        UpdateHeaderTooltips();
        InvalidateHeaders();
        _onSelectionChanged?.Invoke();
    }

    /// <summary>
    /// Aplica <see cref="DataGridViewColumnSortMode.Programmatic"/> y tooltips explicativos
    /// a las columnas mapeadas del grid (llamar tras configurar columnas).
    /// </summary>
    public void ApplyToGrid()
    {
        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (TryGetMappedField(col, out WorkOrderExportField exportField))
            {
                col.SortMode = DataGridViewColumnSortMode.Programmatic;
                col.HeaderCell.ToolTipText = BuildHeaderTooltip(col.HeaderText, _selectedFields.Contains(exportField));
            }
        }

        InvalidateHeaders();
    }

    public bool HitTestHeaderCheckBox(int columnIndex, int relativeY)
    {
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
        {
            return false;
        }

        if (!TryGetMappedField(_grid.Columns[columnIndex], out _))
        {
            return false;
        }

        return relativeY >= CheckBoxZoneTop;
    }

    /// <summary>
    /// Procesa un clic en el encabezado (zona inferior = checkbox de exportación, zona superior = ordenamiento).
    /// </summary>
    public void HandleHeaderClick(int columnIndex, int relativeY)
    {
        if (columnIndex < 0 || columnIndex >= _grid.Columns.Count)
        {
            return;
        }

        DataGridViewColumn col = _grid.Columns[columnIndex];
        if (!TryGetMappedField(col, out WorkOrderExportField exportField))
        {
            return;
        }

        if (HitTestHeaderCheckBox(columnIndex, relativeY))
        {
            ToggleField(exportField);
            return;
        }

        SortColumnProgrammatically(col);
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _grid.CellPainting -= OnCellPainting;
        _grid.ColumnHeaderMouseClick -= OnColumnHeaderMouseClick;
        _grid.CellMouseMove -= OnCellMouseMove;
        _grid.CellMouseLeave -= OnCellMouseLeave;
        _grid.DataBindingComplete -= OnDataBindingComplete;
        _attached = false;
    }

    private void OnDataBindingComplete(object? sender, DataGridViewBindingCompleteEventArgs e) =>
        ApplyToGrid();

    private void OnColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.ColumnIndex < 0)
        {
            return;
        }

        HandleHeaderClick(e.ColumnIndex, e.Y);
    }

    private void OnCellMouseMove(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.ColumnIndex >= _grid.Columns.Count)
        {
            ClearHoverState();
            return;
        }

        if (!TryGetMappedField(_grid.Columns[e.ColumnIndex], out _))
        {
            ClearHoverState();
            return;
        }

        bool overBox = e.Y >= CheckBoxZoneTop;
        if (_hoveredColIndex != e.ColumnIndex || _hoveredOverCheckBox != overBox)
        {
            int prevCol = _hoveredColIndex;
            _hoveredColIndex = e.ColumnIndex;
            _hoveredOverCheckBox = overBox;
            _grid.Cursor = Cursors.Hand;

            if (prevCol >= 0 && prevCol < _grid.Columns.Count)
            {
                _grid.InvalidateCell(prevCol, -1);
            }

            _grid.InvalidateCell(_hoveredColIndex, -1);
        }
    }

    private void OnCellMouseLeave(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex == -1)
        {
            ClearHoverState();
        }
    }

    private void ClearHoverState()
    {
        if (_hoveredColIndex < 0 && !_hoveredOverCheckBox)
        {
            return;
        }

        int prev = _hoveredColIndex;
        _hoveredColIndex = -1;
        _hoveredOverCheckBox = false;
        if (_grid.Cursor == Cursors.Hand)
        {
            _grid.Cursor = Cursors.Default;
        }

        if (prev >= 0 && prev < _grid.Columns.Count)
        {
            _grid.InvalidateCell(prev, -1);
        }
    }

    private void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || e.ColumnIndex >= _grid.Columns.Count || e.Graphics is null)
        {
            return;
        }

        DataGridViewColumn col = _grid.Columns[e.ColumnIndex];
        if (!TryGetMappedField(col, out WorkOrderExportField exportField))
        {
            return;
        }

        bool isChecked = _selectedFields.Contains(exportField);
        bool isHovered = _hoveredColIndex == e.ColumnIndex;
        bool isBoxHovered = isHovered && _hoveredOverCheckBox;

        Rectangle bounds = e.CellBounds;
        Graphics g = e.Graphics;
        SmoothingMode prevSmoothing = g.SmoothingMode;

        // 1. Fondo del encabezado (activo Petrol corporativo vs inactivo pizarra)
        Color backColor = isChecked
            ? (isHovered ? HeaderActiveHoverFill : HeaderActiveFill)
            : (isHovered ? HeaderInactiveHoverFill : HeaderInactiveFill);

        using (SolidBrush bgBrush = new(backColor))
        {
            g.FillRectangle(bgBrush, bounds);
        }

        // Borde separador vertical y línea inferior sutil
        using (Pen sepPen = new(HeaderGridDivider, 1f))
        {
            g.DrawLine(sepPen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
            g.DrawLine(sepPen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
        }

        // Franja inferior moderna indicando que la columna está activa para exportación
        if (isChecked)
        {
            Color accentBarColor = isBoxHovered ? CambiasoTheme.GoldAccent : CheckBoxActiveFill;
            using SolidBrush barBrush = new(accentBarColor);
            g.FillRectangle(barBrush, bounds.Left, bounds.Bottom - 3, bounds.Width - 1, 3);
        }

        // 2. Zona superior: título completo de la columna (100% del ancho disponible) + indicador de orden
        SortOrder sortOrder = col.HeaderCell.SortGlyphDirection;
        int sortReserve = sortOrder != SortOrder.None ? 12 : 2;
        Rectangle titleRect = new(bounds.Left + 2, bounds.Top + 3, Math.Max(8, bounds.Width - 4 - sortReserve), 18);
        Color foreColor = isChecked ? Color.White : HeaderInactiveText;

        TextRenderer.DrawText(
            g,
            col.HeaderText,
            CambiasoTheme.GridHeaderFont,
            titleRect,
            foreColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (sortOrder != SortOrder.None)
        {
            int cx = bounds.Right - 8;
            int cy = bounds.Top + 12;
            PointF[] tri = sortOrder == SortOrder.Ascending
                ? [new PointF(cx - 3.2f, cy + 2f), new PointF(cx + 3.2f, cy + 2f), new PointF(cx, cy - 2.3f)]
                : [new PointF(cx - 3.2f, cy - 2f), new PointF(cx + 3.2f, cy - 2f), new PointF(cx, cy + 2.3f)];
            using SolidBrush glyphBrush = new(CambiasoTheme.GoldAccent);
            g.FillPolygon(glyphBrush, tri);
        }

        // 3. Zona inferior: CheckBox redondeado moderno centrado sobre la columna
        int boxX = bounds.Left + ((bounds.Width - CheckBoxSize) / 2);
        int boxY = bounds.Top + CheckBoxZoneTop + Math.Max(1, ((bounds.Height - CheckBoxZoneTop - 4 - CheckBoxSize) / 2));
        RectangleF boxRect = new(boxX, boxY, CheckBoxSize, CheckBoxSize);

        using (GraphicsPath boxPath = CreateRoundedRectPath(boxRect, 3.5f))
        {
            using SolidBrush fillBrush = new(isChecked ? CheckBoxActiveFill : CheckBoxInactiveFill);
            g.FillPath(fillBrush, boxPath);

            Color borderCol = isBoxHovered
                ? CambiasoTheme.GoldAccent
                : (isChecked ? CheckBoxActiveBorder : CheckBoxInactiveBorder);
            using Pen borderPen = new(borderCol, isBoxHovered ? 1.5f : 1.15f);
            g.DrawPath(borderPen, boxPath);
        }

        if (isChecked)
        {
            using Pen checkPen = new(Color.White, 2.0f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round
            };
            PointF[] checkPts =
            [
                new PointF(boxX + 3.4f, boxY + 7.8f),
                new PointF(boxX + 6.4f, boxY + 10.8f),
                new PointF(boxX + 11.7f, boxY + 4.5f)
            ];
            g.DrawLines(checkPen, checkPts);
        }

        g.SmoothingMode = prevSmoothing;
        e.Handled = true;
    }

    private void SortColumnProgrammatically(DataGridViewColumn targetCol)
    {
        if (_grid.DataSource is null || _grid.Rows.Count == 0)
        {
            return;
        }

        ListSortDirection nextDir = targetCol.HeaderCell.SortGlyphDirection == SortOrder.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (col != targetCol)
            {
                col.HeaderCell.SortGlyphDirection = SortOrder.None;
            }
        }

        _grid.Sort(targetCol, nextDir);
        targetCol.HeaderCell.SortGlyphDirection = nextDir == ListSortDirection.Ascending
            ? SortOrder.Ascending
            : SortOrder.Descending;
        InvalidateHeaders();
    }

    private bool TryGetMappedField(DataGridViewColumn column, out WorkOrderExportField exportField)
    {
        if (!string.IsNullOrEmpty(column.DataPropertyName) &&
            _propertyToField.TryGetValue(column.DataPropertyName, out exportField))
        {
            return true;
        }

        exportField = default;
        return false;
    }

    private void UpdateHeaderTooltips()
    {
        foreach (DataGridViewColumn col in _grid.Columns)
        {
            if (TryGetMappedField(col, out WorkOrderExportField exportField))
            {
                col.HeaderCell.ToolTipText = BuildHeaderTooltip(col.HeaderText, _selectedFields.Contains(exportField));
            }
        }
    }

    private void InvalidateHeaders()
    {
        if (_grid.IsDisposed || _grid.Columns.Count == 0)
        {
            return;
        }

        foreach (DataGridViewColumn col in _grid.Columns)
        {
            _grid.InvalidateCell(col.Index, -1);
        }
    }

    private static string BuildHeaderTooltip(string headerText, bool included) =>
        included
            ? $"Columna '{headerText}' INCLUIDA al exportar.\n• Clic en la casilla inferior para excluirla.\n• Clic en el título superior para ordenar."
            : $"Columna '{headerText}' EXCLUIDA al exportar.\n• Clic en la casilla inferior para volver a incluirla.\n• Clic en el título superior para ordenar.";

    private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
    {
        GraphicsPath path = new();
        float d = radius * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
