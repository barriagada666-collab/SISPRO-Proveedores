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
            main.Size = new Size(1320, 800);
            main.Show();
            Application.DoEvents();

            main.ShowDashboard();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "02_dashboard.png"));

            main.ShowWorkOrders(2815);
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "03_ordenes_trabajo.png"));

            main.ShowInvoices();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "04_facturas_guias.png"));

            main.ShowMaintainers();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "05_mantenedores.png"));

            main.ShowUsers();
            Application.DoEvents();
            SaveFormBitmap(main, Path.Combine(outDir, "06_usuarios_rbac.png"));

            main.Hide();
        });
    }

    private static void SaveFormBitmap(Form form, string filePath)
    {
        using Bitmap bmp = new(form.Width, form.Height);
        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
        bmp.Save(filePath, ImageFormat.Png);
    }
}
