using System.ComponentModel;

namespace Sispro.UI.Theme;

/// <summary>
/// Marca filas tipadas enlazadas a DataGridView que exponen su entidad/DTO de origen
/// sin ser mostradas como columna visible gracias a [Browsable(false)].
/// </summary>
public interface IGridRowSource<out T>
{
    T Source { get; }
}

/// <summary>
/// BindingList ordenable por columna para DataGridView en WinForms (.NET 10).
/// Permite ordenar fechas, números y textos haciendo clic en cualquier encabezado.
/// </summary>
public sealed class SortableBindingList<T> : BindingList<T>
{
    private bool _isSorted;
    private PropertyDescriptor? _sortProperty;
    private ListSortDirection _sortDirection;

    public SortableBindingList()
    {
    }

    public SortableBindingList(IEnumerable<T> items)
        : base(items.ToList())
    {
    }

    protected override bool SupportsSortingCore => true;
    protected override bool IsSortedCore => _isSorted;
    protected override PropertyDescriptor? SortPropertyCore => _sortProperty;
    protected override ListSortDirection SortDirectionCore => _sortDirection;

    protected override void ApplySortCore(PropertyDescriptor prop, ListSortDirection direction)
    {
        if (Items is not List<T> list)
        {
            return;
        }

        list.Sort((x, y) => CompareValues(prop.GetValue(x), prop.GetValue(y), direction));
        _sortProperty = prop;
        _sortDirection = direction;
        _isSorted = true;
        OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
    }

    protected override void RemoveSortCore()
    {
        _isSorted = false;
        _sortProperty = null;
    }

    private static int CompareValues(object? lhs, object? rhs, ListSortDirection direction)
    {
        int result;
        if (ReferenceEquals(lhs, rhs))
        {
            result = 0;
        }
        else if (lhs is null)
        {
            result = -1;
        }
        else if (rhs is null)
        {
            result = 1;
        }
        else if (lhs is string s1 && rhs is string s2)
        {
            result = StringComparer.CurrentCultureIgnoreCase.Compare(s1, s2);
        }
        else if (lhs is IComparable cmp)
        {
            result = cmp.CompareTo(rhs);
        }
        else
        {
            result = StringComparer.CurrentCultureIgnoreCase.Compare(lhs.ToString(), rhs.ToString());
        }

        return direction == ListSortDirection.Descending ? -result : result;
    }
}

public static class GridBindingHelper
{
    public static TSource? GetRowSource<TSource>(DataGridView grid, int? rowIndex = null)
        where TSource : class
    {
        int idx = rowIndex ?? (grid.CurrentRow?.Index ?? -1);
        if (idx < 0 || idx >= grid.Rows.Count)
        {
            return null;
        }

        return grid.Rows[idx].DataBoundItem is IGridRowSource<TSource> bound ? bound.Source : null;
    }

    public static IReadOnlyList<T> SlicePage<T>(IReadOnlyList<T> source, int pageIndex, int pageSize, out int safePageIndex)
    {
        if (source.Count == 0)
        {
            safePageIndex = 0;
            return [];
        }

        int maxPage = (source.Count - 1) / Math.Max(1, pageSize);
        safePageIndex = Math.Clamp(pageIndex, 0, maxPage);
        return source.Skip(safePageIndex * pageSize).Take(pageSize).ToList();
    }

    public static void ConfigureColumn(
        DataGridView grid,
        int columnIndex,
        string? headerText = null,
        float? fillWeight = null,
        string? format = null,
        DataGridViewContentAlignment? alignment = null)
    {
        if (columnIndex < 0 || columnIndex >= grid.Columns.Count)
        {
            return;
        }

        DataGridViewColumn col = grid.Columns[columnIndex];
        if (!string.IsNullOrWhiteSpace(headerText))
        {
            col.HeaderText = headerText;
        }

        if (fillWeight.HasValue)
        {
            col.FillWeight = fillWeight.Value;
        }

        if (!string.IsNullOrEmpty(format))
        {
            col.DefaultCellStyle.Format = format;
        }

        if (alignment.HasValue)
        {
            col.DefaultCellStyle.Alignment = alignment.Value;
        }
    }
}
