using System.ComponentModel;
using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.UI.Controls;
using Sispro.UI.Forms;
using Sispro.UI.Theme;
using Xunit;

namespace Sispro.Tests.UI;

public sealed class GridSortingAndPaginationTests
{
    private sealed class TestOtRow(WorkOrderSearchRow source) : IGridRowSource<WorkOrderSearchRow>
    {
        [Browsable(false)]
        public WorkOrderSearchRow Source { get; } = source;
        public int NumeroOt => Source.NumeroOt;
        public DateTime Fecha => Source.Fecha;
        public string Proveedor => Source.Proveedor;
    }

    [Fact]
    public void SortableBindingList_SortsDatesNumbersAndStrings_AndPreservesRowSource()
    {
        TempTestDatabase.RunOnStaThread(() =>
        {
            WorkOrderSearchRow r1 = new() { NumeroOt = 1002, Fecha = new DateTime(2025, 05, 10), Proveedor = "Beta" };
            WorkOrderSearchRow r2 = new() { NumeroOt = 1001, Fecha = new DateTime(2026, 01, 20), Proveedor = "Alpha" };
            WorkOrderSearchRow r3 = new() { NumeroOt = 1003, Fecha = new DateTime(2024, 12, 01), Proveedor = "Gamma" };

            SortableBindingList<TestOtRow> bindingList = new([new(r1), new(r2), new(r3)]);

            using Form host = new() { Size = new Size(640, 320), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            using DataGridView grid = new() { Dock = DockStyle.Fill, DataSource = bindingList };
            host.Controls.Add(grid);
            host.Show();
            Application.DoEvents();

            // Orden cronológico ascendente por Fecha (DateTime real, no texto)
            grid.Sort(grid.Columns[1], ListSortDirection.Ascending);
            Application.DoEvents();
            Assert.Equal(1003, GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 0)!.NumeroOt);
            Assert.Equal(1002, GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 1)!.NumeroOt);
            Assert.Equal(1001, GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 2)!.NumeroOt);

            // Orden descendente por Proveedor (texto)
            grid.Sort(grid.Columns[2], ListSortDirection.Descending);
            Application.DoEvents();
            Assert.Equal("Gamma", GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 0)!.Proveedor);
            Assert.Equal("Beta", GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 1)!.Proveedor);
            Assert.Equal("Alpha", GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 2)!.Proveedor);

            // Orden numérico por NumeroOt
            grid.Sort(grid.Columns[0], ListSortDirection.Ascending);
            Application.DoEvents();
            Assert.Equal(1001, GridBindingHelper.GetRowSource<WorkOrderSearchRow>(grid, 0)!.NumeroOt);
        });
    }

    [Fact]
    public void UsersForm_AfterColumnSort_CheckboxesAndSelectionResolveExactSortedUser()
    {
        TempTestDatabase.RunOnStaThread(() =>
        {
            using TempTestDatabase temp = new();
            UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;

            // Crear usuarios con alias en extremos alfabéticos para garantizar reordenamiento claro
            temp.Context.UserManagementService.CreateUser(admin, new User
            {
                AliasUsuario = "aaa_primero",
                NombreUsuario = "Ana",
                ApePatUsuario = "Zúñiga",
                Nivel = UserRoleLevel.Usuario,
                Activo = true,
                Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
            }, "Clave2026!", "Clave2026!");
            temp.Context.UserManagementService.CreateUser(admin, new User
            {
                AliasUsuario = "zzz_ultimo",
                NombreUsuario = "Zacarías",
                ApePatUsuario = "Morales",
                Nivel = UserRoleLevel.Usuario,
                Activo = true,
                Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Usuario)
            }, "Clave2026!", "Clave2026!");

            using Form host = new() { Size = new Size(1280, 820), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            using UsersForm usersForm = new(temp.Context, admin) { Dock = DockStyle.Fill };
            host.Controls.Add(usersForm);
            host.Show();
            Application.DoEvents();

            // Ordenar descendente por Usuario -> 'zzz_ultimo' queda en fila 0 aunque en la lista base estuviera después/antes
            usersForm.GridUsers.Sort(usersForm.GridUsers.Columns["colUsuario"]!, ListSortDirection.Descending);
            Application.DoEvents();

            User? firstDesc = usersForm.GridUsers.Rows[0].Tag as User;
            Assert.NotNull(firstDesc);
            Assert.Equal("zzz_ultimo", firstDesc.AliasUsuario);

            // Ordenar ascendente por Usuario -> 'aaa_primero' pasa a fila 0
            usersForm.GridUsers.Sort(usersForm.GridUsers.Columns["colUsuario"]!, ListSortDirection.Ascending);
            Application.DoEvents();

            User? firstAsc = usersForm.GridUsers.Rows[0].Tag as User;
            Assert.NotNull(firstAsc);
            Assert.Equal("aaa_primero", firstAsc.AliasUsuario);

            // Seleccionar fila 0 y marcar checkbox debe operar exactamente sobre 'aaa_primero'
            usersForm.GridUsers.ClearSelection();
            usersForm.GridUsers.Rows[0].Selected = true;
            usersForm.GridUsers.CurrentCell = usersForm.GridUsers.Rows[0].Cells[1];
            usersForm.GridUsers.Rows[0].Cells[0].Value = true;
            Application.DoEvents();

            List<TextBox> boxes = CollectAll<TextBox>(usersForm);
            Assert.Contains(boxes, tb => string.Equals(tb.Text, "aaa_primero", StringComparison.Ordinal));

            List<Label> labels = CollectAll<Label>(usersForm);
            Assert.Contains(labels, l => l.Text == "Sel: 1");
        });
    }

    [Fact]
    public void GridPagerBar_NavigatesPagesAndUpdatesStateCleanly()
    {
        TempTestDatabase.RunOnStaThread(() =>
        {
            using Form host = new() { Size = new Size(900, 120), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            using GridPagerBar pager = new(50);
            int fired = 0;
            pager.PageChanged += (_, _) => fired++;
            pager.SetState(0, 50, 120);
            host.Controls.Add(pager);
            host.Show();
            Application.DoEvents();

            Assert.Equal(3, pager.TotalPages);
            Assert.Equal(0, pager.PageIndex);

            List<Button> buttons = CollectAll<Button>(pager);
            Assert.Equal(4, buttons.Count);
            Assert.All(buttons, b => Assert.NotNull(b.Image));

            // Botones en orden izquierdo->derecho: [0]=First, [1]=Prev, [2]=Next, [3]=Last
            Assert.False(buttons[1].Enabled); // Prev deshabilitado en página 0
            Assert.True(buttons[2].Enabled);  // Next habilitado

            buttons[2].PerformClick();
            Application.DoEvents();
            Assert.Equal(1, pager.PageIndex);
            Assert.Equal(1, fired);

            pager.SetState(pager.PageIndex, 50, 120);
            buttons[3].PerformClick(); // Last -> página 2
            Application.DoEvents();
            Assert.Equal(2, pager.PageIndex);

            pager.SetState(pager.PageIndex, 50, 120);
            buttons[0].PerformClick(); // First -> página 0
            Application.DoEvents();
            Assert.Equal(0, pager.PageIndex);
        });
    }

    private static List<T> CollectAll<T>(Control root) where T : Control
    {
        List<T> found = [];
        foreach (Control c in root.Controls)
        {
            if (c is T typed)
            {
                found.Add(typed);
            }

            if (c.HasChildren)
            {
                found.AddRange(CollectAll<T>(c));
            }
        }

        return found;
    }
}
