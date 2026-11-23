using System.Drawing;
using Sispro.UI.Theme;
using Xunit;

namespace Sispro.Tests.UI;

public sealed class ContextMenuHelperTests
{
    [Fact]
    public void ContextMenuHelper_ExecutesDeferredActionAfterClose_AndPreservesCachedIcons()
    {
        TempTestDatabase.RunOnStaThread(() =>
        {
            Bitmap cachedIcon = IconCatalog.Get(AppIcon.Excel, CambiasoTheme.Success, 16);
            int executed = 0;

            using Form host = new()
            {
                Size = new Size(320, 220),
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-2000, -2000)
            };
            Button anchor = new() { Location = new Point(12, 12), Size = new Size(120, 32), Text = "Exportar" };
            host.Controls.Add(anchor);
            host.Show();
            Application.DoEvents();

            ContextMenuStrip menu = ContextMenuHelper.Show(
                anchor,
                new Point(0, anchor.Height),
                [
                    ContextMenuHelper.Separator(),
                    ContextMenuHelper.Item("Exportar a Excel (.xlsx)", AppIcon.Excel, CambiasoTheme.Success, () => executed++),
                    ContextMenuHelper.Separator(),
                    ContextMenuHelper.Separator()
                ]);

            // Leading/trailing/duplicate separators normalized to single item
            Assert.Single(menu.Items);

            ToolStripMenuItem item = Assert.IsType<ToolStripMenuItem>(menu.Items[0]);

            // Close with ItemClicked mirrors WinForms' exact runtime sequence (Closed BEFORE Click)
            if (menu.Visible)
            {
                menu.Close(ToolStripDropDownCloseReason.ItemClicked);
            }
            item.PerformClick();
            Application.DoEvents();

            Assert.Equal(1, executed);
            Assert.True(menu.IsDisposed);

            // Cached IconCatalog bitmap MUST remain valid and usable on subsequent menu openings
            Assert.Equal(16, cachedIcon.Width);
            Bitmap secondRef = IconCatalog.Get(AppIcon.Excel, CambiasoTheme.Success, 16);
            Assert.Same(cachedIcon, secondRef);
            Assert.Equal(16, secondRef.Height);

            // Second invocation on same anchor runs cleanly without ObjectDisposedException
            ContextMenuStrip secondMenu = ContextMenuHelper.Show(
                anchor,
                new Point(0, anchor.Height),
                [
                    ContextMenuHelper.Item("Segunda acción", AppIcon.FileText, CambiasoTheme.Primary, () => executed += 10)
                ]);
            ToolStripMenuItem secondItem = Assert.IsType<ToolStripMenuItem>(secondMenu.Items[0]);
            if (secondMenu.Visible)
            {
                secondMenu.Close(ToolStripDropDownCloseReason.ItemClicked);
            }
            secondItem.PerformClick();
            Application.DoEvents();

            Assert.Equal(11, executed);
            Assert.True(secondMenu.IsDisposed);

            host.Hide();
        });
    }
}
