using System.Drawing;
using System.Runtime.CompilerServices;

namespace Sispro.UI.Theme;

/// <summary>
/// Utilidad centralizada para menús contextuales seguros en WinForms.
/// Desacopla la ejecución de la acción del evento síncrono Closed y evita liberar
/// el ContextMenuStrip mientras un mensaje de WinForms o diálogo modal sigue activo.
/// </summary>
public static class ContextMenuHelper
{
    private static readonly ConditionalWeakTable<Control, ContextMenuStrip> LastMenusByAnchor = new();

    public static ToolStripMenuItem Item(
        string text,
        AppIcon icon,
        Color iconColor,
        Action action,
        bool enabled = true)
    {
        return new ToolStripMenuItem(text, IconCatalog.Get(icon, iconColor, 16))
        {
            Tag = action,
            Enabled = enabled
        };
    }

    public static ToolStripSeparator Separator() => new();

    internal static ContextMenuStrip Show(Control anchor, Point clientLocation, IEnumerable<ToolStripItem> items)
    {
        if (LastMenusByAnchor.TryGetValue(anchor, out ContextMenuStrip? prev))
        {
            LastMenusByAnchor.Remove(anchor);
            if (!prev.IsDisposed && !prev.Visible)
            {
                ClearImages(prev.Items);
                prev.Dispose();
            }
        }
        else
        {
            anchor.Disposed += (_, _) =>
            {
                if (LastMenusByAnchor.TryGetValue(anchor, out ContextMenuStrip? last))
                {
                    LastMenusByAnchor.Remove(anchor);
                    if (!last.IsDisposed)
                    {
                        ClearImages(last.Items);
                        last.Dispose();
                    }
                }
            };
        }

        List<ToolStripItem> filtered = NormalizeSeparators(items);
        ContextMenuStrip menu = new() { Font = CambiasoTheme.BodyFont };

        if (filtered.Count == 0)
        {
            menu.Dispose();
            return menu;
        }

        bool actionPending = false;
        void TryDisposeMenu()
        {
            if (!menu.IsDisposed && !menu.Visible)
            {
                ClearImages(menu.Items);
                menu.Dispose();
            }
        }

        foreach (ToolStripItem item in filtered)
        {
            AttachDeferredClicks(anchor, menu, item, () => actionPending = true, TryDisposeMenu);
            menu.Items.Add(item);
        }

        menu.Closed += (_, _) =>
        {
            ClearImages(menu.Items);
            if (!actionPending)
            {
                Defer(anchor, TryDisposeMenu);
            }
        };

        LastMenusByAnchor.Add(anchor, menu);
        menu.Show(anchor, clientLocation);
        return menu;
    }

    public static ContextMenuStrip ShowBelow(Control anchor, IEnumerable<ToolStripItem> items) =>
        Show(anchor, new Point(0, anchor.Height), items);

    public static void AttachToGrid(DataGridView grid, Func<int, IEnumerable<ToolStripItem>> builder)
    {
        grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count)
            {
                return;
            }

            DataGridViewRow row = grid.Rows[e.RowIndex];
            if (!row.Selected || grid.CurrentRow?.Index != e.RowIndex)
            {
                DataGridViewColumn? targetCol = grid.Columns
                    .Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => c.Visible && c is not DataGridViewCheckBoxColumn)
                    ?? grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);

                if (targetCol is not null)
                {
                    grid.CurrentCell = row.Cells[targetCol.Index];
                }
            }

            List<ToolStripItem> items = NormalizeSeparators(builder(e.RowIndex));
            if (items.Count > 0)
            {
                Show(grid, grid.PointToClient(Cursor.Position), items);
            }
        };
    }

    public static void AttachToListBox(ListBox listBox, Func<int, IEnumerable<ToolStripItem>> builder)
    {
        listBox.MouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int idx = listBox.IndexFromPoint(e.Location);
            if (idx >= 0 && idx < listBox.Items.Count)
            {
                listBox.SelectedIndex = idx;
            }

            List<ToolStripItem> items = NormalizeSeparators(builder(listBox.SelectedIndex));
            if (items.Count > 0)
            {
                Show(listBox, e.Location, items);
            }
        };
    }

    private static List<ToolStripItem> NormalizeSeparators(IEnumerable<ToolStripItem> items)
    {
        List<ToolStripItem> filtered = [];
        foreach (ToolStripItem item in items)
        {
            if (item is ToolStripSeparator)
            {
                if (filtered.Count == 0 || filtered[^1] is ToolStripSeparator)
                {
                    item.Dispose();
                    continue;
                }
            }

            filtered.Add(item);
        }

        if (filtered.Count > 0 && filtered[^1] is ToolStripSeparator trailing)
        {
            filtered.RemoveAt(filtered.Count - 1);
            trailing.Dispose();
        }

        return filtered;
    }

    private static void AttachDeferredClicks(
        Control anchor,
        ContextMenuStrip menu,
        ToolStripItem item,
        Action markActionPending,
        Action onFinished)
    {
        if (item is ToolStripMenuItem menuItem)
        {
            if (menuItem.Tag is Action action)
            {
                menuItem.Click += (_, _) =>
                {
                    markActionPending();
                    if (menu.Visible)
                    {
                        menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                    }

                    Defer(anchor, () =>
                    {
                        try
                        {
                            if (!anchor.IsDisposed)
                            {
                                action();
                            }
                        }
                        finally
                        {
                            Defer(anchor, onFinished);
                        }
                    });
                };
            }

            if (menuItem.HasDropDownItems)
            {
                foreach (ToolStripItem child in menuItem.DropDownItems)
                {
                    AttachDeferredClicks(anchor, menu, child, markActionPending, onFinished);
                }
            }
        }
    }

    private static void ClearImages(ToolStripItemCollection items)
    {
        foreach (ToolStripItem item in items)
        {
            item.Image = null;
            if (item is ToolStripDropDownItem dropDown && dropDown.HasDropDownItems)
            {
                ClearImages(dropDown.DropDownItems);
            }
        }
    }

    private static void Defer(Control anchor, Action action)
    {
        if (anchor.IsDisposed)
        {
            return;
        }

        if (anchor.IsHandleCreated)
        {
            anchor.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }
}
