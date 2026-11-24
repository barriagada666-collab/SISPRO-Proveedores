using System.Drawing;
using Sispro.UI.Theme;

namespace Sispro.UI.Theme;

/// <summary>
/// Controlador reutilizable de selección múltiple por checkbox sobre cualquier DataGridView
/// respaldado por filas tipadas <see cref="IGridRowSource{T}"/>. Preserva los IDs seleccionados
/// entre cambios de página, ordenamiento por columnas y actualización de filtros.
/// </summary>
public sealed class GridCheckboxSelectionController<TSource> where TSource : class
{
    public const string SelectColumnName = "__colSelect";

    private readonly DataGridView _grid;
    private readonly Func<TSource, int> _idSelector;
    private readonly CheckBox? _selectAllVisibleCheckBox;
    private readonly Action? _onSelectionChanged;
    private readonly HashSet<int> _selectedIds = [];
    private bool _suppressUiEvents;

    public GridCheckboxSelectionController(
        DataGridView grid,
        Func<TSource, int> idSelector,
        CheckBox? selectAllVisibleCheckBox = null,
        Action? onSelectionChanged = null)
    {
        _grid = grid;
        _idSelector = idSelector;
        _selectAllVisibleCheckBox = selectAllVisibleCheckBox;
        _onSelectionChanged = onSelectionChanged;

        _grid.CurrentCellDirtyStateChanged += OnCurrentCellDirtyStateChanged;
        _grid.CellValueChanged += OnCellValueChanged;
        _grid.Sorted += (_, _) => SyncVisibleRows();
        _grid.DataBindingComplete += (_, _) => EnsureSelectColumnAndSync();

        if (_selectAllVisibleCheckBox is not null)
        {
            _selectAllVisibleCheckBox.CheckedChanged += OnSelectAllVisibleCheckedChanged;
        }
    }

    public int SelectedCount => _selectedIds.Count;

    public IReadOnlyList<int> SelectedIds => _selectedIds.ToList();

    public bool IsSelected(int id) => _selectedIds.Contains(id);

    public void ClearSelection()
    {
        if (_selectedIds.Count == 0 && (_selectAllVisibleCheckBox is null || !_selectAllVisibleCheckBox.Checked))
        {
            return;
        }

        _selectedIds.Clear();
        SyncVisibleRows();
        _onSelectionChanged?.Invoke();
    }

    public void SetSelected(int id, bool selected)
    {
        if (id <= 0)
        {
            return;
        }

        bool changed = selected ? _selectedIds.Add(id) : _selectedIds.Remove(id);
        SyncVisibleRows();
        if (changed)
        {
            _onSelectionChanged?.Invoke();
        }
    }

    public void SelectAllVisible(bool select)
    {
        bool changed = false;
        foreach (DataGridViewRow row in _grid.Rows)
        {
            TSource? item = GridBindingHelper.GetRowSource<TSource>(_grid, row.Index);
            if (item is null)
            {
                continue;
            }

            int id = _idSelector(item);
            if (id <= 0)
            {
                continue;
            }

            changed |= select ? _selectedIds.Add(id) : _selectedIds.Remove(id);
        }

        SyncVisibleRows();
        if (changed || _selectAllVisibleCheckBox is not null)
        {
            _onSelectionChanged?.Invoke();
        }
    }

    /// <summary>
    /// Configura las columnas de datos desplazadas por la columna checkbox en índice 0.
    /// </summary>
    public void ConfigureDataColumn(
        int dataColumnIndex,
        string? headerText = null,
        float? fillWeight = null,
        string? format = null,
        DataGridViewContentAlignment? alignment = null)
    {
        EnsureSelectColumn();
        GridBindingHelper.ConfigureColumn(_grid, dataColumnIndex + 1, headerText, fillWeight, format, alignment);
    }

    public void EnsureSelectColumnAndSync()
    {
        EnsureSelectColumn();
        SyncVisibleRows();
    }

    public void SyncVisibleRows()
    {
        _suppressUiEvents = true;
        try
        {
            int selectColIdx = EnsureSelectColumn();
            int visibleUniqueCount = 0;
            int visibleCheckedCount = 0;
            HashSet<int> seenVisible = [];

            foreach (DataGridViewRow row in _grid.Rows)
            {
                TSource? item = GridBindingHelper.GetRowSource<TSource>(_grid, row.Index);
                if (item is null)
                {
                    row.Cells[selectColIdx].Value = false;
                    continue;
                }

                int id = _idSelector(item);
                bool checkedState = id > 0 && _selectedIds.Contains(id);
                row.Cells[selectColIdx].Value = checkedState;

                if (id > 0 && seenVisible.Add(id))
                {
                    visibleUniqueCount++;
                    if (checkedState)
                    {
                        visibleCheckedCount++;
                    }
                }
            }

            if (_selectAllVisibleCheckBox is not null)
            {
                _selectAllVisibleCheckBox.Checked = visibleUniqueCount > 0 && visibleCheckedCount == visibleUniqueCount;
            }
        }
        finally
        {
            _suppressUiEvents = false;
        }
    }

    private int EnsureSelectColumn()
    {
        DataGridViewColumn? existing = _grid.Columns[SelectColumnName];
        if (existing is null)
        {
            DataGridViewCheckBoxColumn col = new()
            {
                Name = SelectColumnName,
                HeaderText = "Sel.",
                Width = 50,
                MinimumWidth = 48,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Resizable = DataGridViewTriState.False,
                ReadOnly = false,
                ThreeState = false,
                DisplayIndex = 0
            };
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.Columns.Insert(0, col);
            existing = col;
        }
        else if (existing.Index != 0)
        {
            existing.DisplayIndex = 0;
        }

        _grid.ReadOnly = false;
        foreach (DataGridViewColumn c in _grid.Columns)
        {
            c.ReadOnly = c != existing;
        }

        return existing.Index;
    }

    private void OnCurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (!_suppressUiEvents &&
            _grid.IsCurrentCellDirty &&
            _grid.CurrentCell is DataGridViewCheckBoxCell)
        {
            _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private void OnCellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressUiEvents || e.RowIndex < 0 || e.ColumnIndex < 0)
        {
            return;
        }

        DataGridViewColumn? col = _grid.Columns[SelectColumnName];
        if (col is null || e.ColumnIndex != col.Index)
        {
            return;
        }

        TSource? item = GridBindingHelper.GetRowSource<TSource>(_grid, e.RowIndex);
        if (item is null)
        {
            return;
        }

        int id = _idSelector(item);
        if (id <= 0)
        {
            return;
        }

        bool isChecked = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value is bool b && b;
        bool changed = isChecked ? _selectedIds.Add(id) : _selectedIds.Remove(id);

        // Sincronizar otras filas visibles con el mismo ID (ej. OTs multi-ítem) y el checkbox global
        SyncVisibleRows();
        if (changed)
        {
            _onSelectionChanged?.Invoke();
        }
    }

    private void OnSelectAllVisibleCheckedChanged(object? sender, EventArgs e)
    {
        if (_suppressUiEvents || _selectAllVisibleCheckBox is null)
        {
            return;
        }

        SelectAllVisible(_selectAllVisibleCheckBox.Checked);
    }
}
