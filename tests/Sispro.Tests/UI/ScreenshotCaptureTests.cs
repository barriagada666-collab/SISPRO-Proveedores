using System.Drawing;
using System.Drawing.Imaging;
using Sispro.Core.Dtos;
using Sispro.UI.Forms;
using Xunit;

namespace Sispro.Tests.UI;

public sealed class ScreenshotCaptureTests
{
    [Fact]
    public void CaptureAllModuleScreenshots_WhenRequested()
    {
        string? outDir = Environment.GetEnvironmentVariable("SISPRO_CAPTURE_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(outDir))
        {
            return;
        }

        Directory.CreateDirectory(outDir);
        using TempTestDatabase temp = new(copyMigratedRealDb: true);
        UserSession admin = temp.Context.AuthService.Login("L Reyes", "wanderes").Session!;

        TempTestDatabase.RunOnStaThread(() =>
        {
            // 0. Corporate Splash Screen
            using (SplashForm splash = new())
            {
                splash.StartPosition = FormStartPosition.Manual;
                splash.Location = new Point(-2000, -2000);
                splash.Show();
                splash.SetProgress(68, "Inicializando servicios y pool de base de datos SQLite...");
                Application.DoEvents();
                SaveFormBitmap(splash, Path.Combine(outDir, "00_splash.png"));
                splash.Hide();
            }

            // 1. Login Form
            using (LoginForm login = new(temp.Context))
            {
                login.ShowInTaskbar = false;
                login.StartPosition = FormStartPosition.Manual;
                login.Location = new Point(-2000, -2000);
                login.Show();
                Application.DoEvents();
                SaveFormBitmap(login, Path.Combine(outDir, "01_login.png"));
                login.Hide();
            }

            // 2-6. MainForm modules
            using MainForm main = new(temp.Context, admin);
            main.ShowInTaskbar = false;
            main.WindowState = FormWindowState.Normal;
            main.StartPosition = FormStartPosition.Manual;
            main.Location = new Point(-2000, -2000);
            main.Size = new Size(1380, 840);
            main.Show();
            Application.DoEvents();

            main.ShowDashboard();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "02_dashboard.png"));

            main.ShowWorkOrders(14752);
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "03_ordenes_trabajo.png"));

            // Captura de Búsqueda de OT con CRUD (Nuevo, Consultar, Editar, Eliminar) y selección masiva
            WorkOrdersForm? woCtrl = FindChildControl<WorkOrdersForm>(main);
            if (woCtrl is not null)
            {
                woCtrl.OpenSearchTab();
                Application.DoEvents();
                SaveFormBitmap(main, Path.Combine(outDir, "12_busqueda_ot_masiva.png"));
            }

            main.ShowMaintainers();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "05_mantenedores.png"));

            // Captura de pestañas dedicadas dentro de Mantenedores (Firmas, Editor de Árbol, Configuración, Mi Clave)
            MaintainersForm? maintCtrl = FindChildControl<MaintainersForm>(main);
            if (maintCtrl is not null)
            {
                maintCtrl.SelectTabByTitle("Firmas");
                Application.DoEvents();
                SaveFormBitmap(main, Path.Combine(outDir, "13_mantenedores_firmas.png"));

                maintCtrl.SelectTabByTitle("Editor de Árbol");
                Application.DoEvents();
                SaveFormBitmap(main, Path.Combine(outDir, "09_editor_arbol.png"));

                maintCtrl.SelectTabByTitle("Configuración");
                Application.DoEvents();
                SaveFormBitmap(main, Path.Combine(outDir, "16_configuracion_sistema.png"));

                maintCtrl.SelectTabByTitle("Mi Clave");
                Application.DoEvents();
                SaveFormBitmap(main, Path.Combine(outDir, "19_mi_clave.png"));

                maintCtrl.TabControlMain.SelectedIndex = 0;
            }

            main.ShowUsers();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "06_usuarios_rbac.png"));

            main.ShowUsers(openRequestersTab: true);
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "07_solicitantes_firmas.png"));

            UsersForm? usrCtrl = FindChildControl<UsersForm>(main);
            if (usrCtrl is not null && usrCtrl.MainTabs.TabPages.Count >= 3)
            {
                usrCtrl.MainTabs.SelectedIndex = 2;
                Application.DoEvents();
                SaveFormBitmap(main, Path.Combine(outDir, "14_perfiles_usuarios.png"));
                usrCtrl.MainTabs.SelectedIndex = 0;
            }

            main.ShowWorkOrders();
            WorkOrdersForm? woNew = FindChildControl<WorkOrdersForm>(main);
            woNew?.BtnSearchNewOt.PerformClick();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "10_ot_externa.png"));

            using (Sispro.UI.Dialogs.AboutDialog aboutDlg = new(temp.Context))
            {
                aboutDlg.ShowInTaskbar = false;
                aboutDlg.StartPosition = FormStartPosition.Manual;
                aboutDlg.Location = new Point(-2000, -2000);
                aboutDlg.Show();
                Application.DoEvents();
                SaveFormBitmap(aboutDlg, Path.Combine(outDir, "17_acerca_de.png"));
                aboutDlg.Hide();
            }

            Sispro.Core.Entities.WorkOrder? detailOrder = temp.Context.WorkOrderService.GetByNumber(14752);
            if (detailOrder is not null)
            {
                using Sispro.UI.Dialogs.WorkOrderDetailDialog detailDlg = new(temp.Context, detailOrder);
                detailDlg.ShowInTaskbar = false;
                detailDlg.StartPosition = FormStartPosition.Manual;
                detailDlg.Location = new Point(-2000, -2000);
                detailDlg.Show();
                Application.DoEvents();
                SaveFormBitmap(detailDlg, Path.Combine(outDir, "18_consulta_ot.png"));
                detailDlg.Hide();
            }

            using (Sispro.UI.Dialogs.AuditLogDialog auditDlg = new(temp.Context.Audit, temp.Context.DocumentExportService))
            {
                auditDlg.ShowInTaskbar = false;
                auditDlg.StartPosition = FormStartPosition.Manual;
                auditDlg.Location = new Point(-2000, -2000);
                auditDlg.Show();
                Application.DoEvents();
                SaveFormBitmap(auditDlg, Path.Combine(outDir, "08_auditoria_fechas.png"));
                auditDlg.Hide();
            }

            using (Sispro.UI.Dialogs.MdbImportDialog mdbDlg = new(temp.Context, admin))
            {
                mdbDlg.ShowInTaskbar = false;
                mdbDlg.StartPosition = FormStartPosition.Manual;
                mdbDlg.Location = new Point(-2000, -2000);
                mdbDlg.Show();
                Application.DoEvents();
                SaveFormBitmap(mdbDlg, Path.Combine(outDir, "15_importar_mdb.png"));
                mdbDlg.Hide();
            }

            main.Hide();
        });
    }

    private static void SaveFormBitmap(Form form, string filePath)
    {
        using Bitmap bmp = new(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(filePath, ImageFormat.Png);
    }

    private static T? FindChildControl<T>(Control root) where T : Control
    {
        foreach (Control c in root.Controls)
        {
            if (c is T typed) return typed;
            T? nested = FindChildControl<T>(c);
            if (nested is not null) return nested;
        }

        return null;
    }
}
