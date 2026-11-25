using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Sispro.UI.Theme;

public enum AppIcon
{
    Save,
    Edit,
    Delete,
    Clear,
    Search,
    Add,
    Remove,
    Print,
    Preview,
    Excel,
    Exit,
    Login,
    Logout,
    Key,
    Settings,
    Dashboard,
    WorkOrder,
    Invoice,
    Provider,
    Users,
    Audit,
    Refresh,
    Check,
    Backup,
    Tree,
    Template,
    UserPlus,
    Cancel,
    Eraser,
    Wrench,
    BarChart,
    Package,
    HardHat,
    ClipboardCheck,
    Factory,
    TrendingUp,
    Cpu,
    Layers,
    MoveRight,
    Power,
    Ban,
    Shield,
    FileText,
    Zap,
    Globe,
    Gauge,
    Download,
    Upload,
    Calendar,
    Copy,
    Expand,
    Collapse,
    ChevronLeft,
    ChevronRight,
    ChevronsLeft,
    ChevronsRight
}

/// <summary>
/// Catálogo vectorial basado en geometría oficial Lucide (viewBox 24x24, strokeWidth=2,
/// strokeLinecap=round, strokeLinejoin=round) renderizado con antialiasing de alta precisión.
/// </summary>
public static class IconCatalog
{
    // Aísla bitmaps/iconos GDI+ por hilo para que controles WinForms concurrentes (p. ej. hilos STA paralelos)
    // nunca compartan el mismo handle GDI+ ni disparen "Object is currently in use elsewhere".
    private static readonly ConcurrentDictionary<(int ThreadId, AppIcon Icon, int Size, int ColorArgb), Bitmap> Cache = new();
    private static readonly ConcurrentDictionary<int, Icon> AppIconCache = new();
    private static readonly ConcurrentDictionary<int, Image?> CompanyLogoCache = new();

    public static Icon GetAppIcon()
    {
        return AppIconCache.GetOrAdd(Environment.CurrentManagedThreadId, _ =>
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "sispro.ico");
            return File.Exists(path) ? new Icon(path) : (Icon)SystemIcons.Application.Clone();
        });
    }

    public static Image? GetCompanyLogo()
    {
        return CompanyLogoCache.GetOrAdd(Environment.CurrentManagedThreadId, _ =>
        {
            string pngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "logo_cambiaso.png");
            if (!File.Exists(pngPath))
            {
                return null;
            }

            using FileStream fs = File.OpenRead(pngPath);
            using Image loaded = Image.FromStream(fs);
            return new Bitmap(loaded);
        });
    }

    public static Bitmap Get(AppIcon icon, Color color, int size = 16)
    {
        return Cache.GetOrAdd(
            (Environment.CurrentManagedThreadId, icon, size, color.ToArgb()),
            key => RenderLucideIcon(key.Icon, Color.FromArgb(key.ColorArgb), key.Size));
    }

    private static Bitmap RenderLucideIcon(AppIcon icon, Color color, int size)
    {
        Bitmap bmp = new(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        // Lucide canonical 24x24 coordinate system scaled crisply to target pixel size
        float scale = size / 24f;
        g.ScaleTransform(scale, scale);

        using Pen pen = new(color, 2.0f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };

        switch (icon)
        {
            case AppIcon.Save: // lucide: save
                g.DrawLines(pen, [new PointF(5, 3), new PointF(16, 3), new PointF(21, 8), new PointF(21, 19), new PointF(19, 21), new PointF(5, 21), new PointF(3, 19), new PointF(3, 5), new PointF(5, 3)]);
                g.DrawRectangle(pen, 7, 13, 10, 8);
                g.DrawLines(pen, [new PointF(7, 3), new PointF(7, 8), new PointF(15, 8)]);
                break;

            case AppIcon.Edit: // lucide: pencil
                g.DrawPolygon(pen, [new PointF(17, 3), new PointF(21, 7), new PointF(8, 20), new PointF(3, 21), new PointF(4, 16)]);
                g.DrawLine(pen, 15, 5, 19, 9);
                break;

            case AppIcon.Delete: // lucide: trash-2
                g.DrawLine(pen, 3, 6, 21, 6);
                g.DrawLines(pen, [new PointF(8, 6), new PointF(8, 4), new PointF(16, 4), new PointF(16, 6)]);
                g.DrawLines(pen, [new PointF(19, 6), new PointF(18, 20), new PointF(6, 20), new PointF(5, 6)]);
                g.DrawLine(pen, 10, 11, 10, 17);
                g.DrawLine(pen, 14, 11, 14, 17);
                break;

            case AppIcon.Clear: // lucide: x
                g.DrawLine(pen, 18, 6, 6, 18);
                g.DrawLine(pen, 6, 6, 18, 18);
                break;

            case AppIcon.Search: // lucide: search
                g.DrawEllipse(pen, 3, 3, 16, 16);
                g.DrawLine(pen, 21, 21, 16.65f, 16.65f);
                break;

            case AppIcon.Add: // lucide: plus
                g.DrawLine(pen, 12, 5, 12, 19);
                g.DrawLine(pen, 5, 12, 19, 12);
                break;

            case AppIcon.Remove: // lucide: minus
                g.DrawLine(pen, 5, 12, 19, 12);
                break;

            case AppIcon.Print: // lucide: printer
                g.DrawLines(pen, [new PointF(6, 9), new PointF(6, 3), new PointF(18, 3), new PointF(18, 9)]);
                g.DrawLines(pen, [new PointF(6, 18), new PointF(3, 18), new PointF(3, 11), new PointF(5, 9), new PointF(19, 9), new PointF(21, 11), new PointF(21, 18), new PointF(18, 18)]);
                g.DrawRectangle(pen, 6, 14, 12, 7);
                break;

            case AppIcon.Preview: // lucide: eye
                g.DrawArc(pen, 2, 5, 20, 14, 195, 150);
                g.DrawArc(pen, 2, 5, 20, 14, 15, 150);
                g.DrawEllipse(pen, 9, 9, 6, 6);
                break;

            case AppIcon.Excel: // lucide: file-spreadsheet
                g.DrawPolygon(pen, [new PointF(5, 2), new PointF(15, 2), new PointF(20, 7), new PointF(20, 22), new PointF(5, 22)]);
                g.DrawLines(pen, [new PointF(14, 2), new PointF(14, 8), new PointF(20, 8)]);
                g.DrawLine(pen, 8, 13, 16, 13);
                g.DrawLine(pen, 8, 17, 16, 17);
                g.DrawLine(pen, 12, 11, 12, 19);
                break;

            case AppIcon.Exit:
            case AppIcon.Logout: // lucide: log-out
                g.DrawLines(pen, [new PointF(9, 21), new PointF(5, 21), new PointF(3, 19), new PointF(3, 5), new PointF(5, 3), new PointF(9, 3)]);
                g.DrawLines(pen, [new PointF(16, 17), new PointF(21, 12), new PointF(16, 7)]);
                g.DrawLine(pen, 21, 12, 9, 12);
                break;

            case AppIcon.Login: // lucide: log-in
                g.DrawLines(pen, [new PointF(15, 3), new PointF(19, 3), new PointF(21, 5), new PointF(21, 19), new PointF(19, 21), new PointF(15, 21)]);
                g.DrawLines(pen, [new PointF(10, 17), new PointF(15, 12), new PointF(10, 7)]);
                g.DrawLine(pen, 15, 12, 3, 12);
                break;

            case AppIcon.Key: // lucide: key-round
                g.DrawEllipse(pen, 3, 11, 9, 9);
                g.DrawLine(pen, 11, 13, 21, 3);
                g.DrawLine(pen, 17, 7, 20, 10);
                g.DrawLine(pen, 19, 5, 22, 8);
                break;

            case AppIcon.Settings: // lucide: settings (sliders / gear)
                g.DrawEllipse(pen, 9, 9, 6, 6);
                g.DrawEllipse(pen, 4, 4, 16, 16);
                g.DrawLine(pen, 12, 2, 12, 4);
                g.DrawLine(pen, 12, 20, 12, 22);
                g.DrawLine(pen, 2, 12, 4, 12);
                g.DrawLine(pen, 20, 12, 22, 12);
                break;

            case AppIcon.Dashboard: // lucide: layout-dashboard
                DrawRoundedRect(g, pen, 3, 3, 7, 9, 1.5f);
                DrawRoundedRect(g, pen, 14, 3, 7, 5, 1.5f);
                DrawRoundedRect(g, pen, 14, 12, 7, 9, 1.5f);
                DrawRoundedRect(g, pen, 3, 16, 7, 5, 1.5f);
                break;

            case AppIcon.WorkOrder: // lucide: clipboard-list
                DrawRoundedRect(g, pen, 8, 2, 8, 4, 1);
                g.DrawLines(pen, [new PointF(16, 4), new PointF(19, 4), new PointF(20, 5), new PointF(20, 20), new PointF(19, 22), new PointF(5, 22), new PointF(4, 20), new PointF(4, 5), new PointF(5, 4), new PointF(8, 4)]);
                g.DrawLine(pen, 12, 11, 16, 11);
                g.DrawLine(pen, 12, 16, 16, 16);
                g.DrawLine(pen, 8, 11, 8.5f, 11);
                g.DrawLine(pen, 8, 16, 8.5f, 16);
                break;

            case AppIcon.Invoice: // lucide: receipt-text
                g.DrawLines(pen, [new PointF(4, 21), new PointF(4, 3), new PointF(20, 3), new PointF(20, 21), new PointF(17, 19), new PointF(14, 21), new PointF(12, 19), new PointF(10, 21), new PointF(7, 19), new PointF(4, 21)]);
                g.DrawLine(pen, 8, 8, 16, 8);
                g.DrawLine(pen, 8, 12, 16, 12);
                g.DrawLine(pen, 8, 16, 12, 16);
                break;

            case AppIcon.Provider: // lucide: building-2
                g.DrawLines(pen, [new PointF(6, 22), new PointF(6, 4), new PointF(8, 2), new PointF(16, 2), new PointF(18, 4), new PointF(18, 22)]);
                g.DrawLines(pen, [new PointF(6, 12), new PointF(2, 12), new PointF(2, 22), new PointF(22, 22), new PointF(22, 10), new PointF(18, 10)]);
                g.DrawLine(pen, 10, 6, 14, 6);
                g.DrawLine(pen, 10, 10, 14, 10);
                g.DrawLine(pen, 10, 14, 14, 14);
                g.DrawLine(pen, 10, 18, 14, 18);
                break;

            case AppIcon.Users: // lucide: users
                g.DrawEllipse(pen, 5, 3, 8, 8);
                g.DrawArc(pen, 2, 14, 14, 10, 185, 170);
                g.DrawArc(pen, 14, 4, 6, 7, -75, 150);
                g.DrawArc(pen, 16, 15, 6, 8, -80, 100);
                break;

            case AppIcon.Audit: // lucide: history
                g.DrawArc(pen, 3, 3, 18, 18, 140, 310);
                g.DrawLines(pen, [new PointF(3, 4), new PointF(3, 9), new PointF(8, 9)]);
                g.DrawLines(pen, [new PointF(12, 7), new PointF(12, 12), new PointF(16, 14)]);
                break;

            case AppIcon.Refresh: // lucide: refresh-cw
                g.DrawArc(pen, 3, 3, 18, 18, 200, 150);
                g.DrawLines(pen, [new PointF(21, 3), new PointF(21, 8), new PointF(16, 8)]);
                g.DrawArc(pen, 3, 3, 18, 18, 20, 150);
                g.DrawLines(pen, [new PointF(3, 21), new PointF(3, 16), new PointF(8, 16)]);
                break;

            case AppIcon.Check: // lucide: check
                g.DrawLines(pen, [new PointF(20, 6), new PointF(9, 17), new PointF(4, 12)]);
                break;

            case AppIcon.Backup: // lucide: database
                g.DrawEllipse(pen, 3, 3, 18, 6);
                g.DrawLine(pen, 3, 6, 3, 18);
                g.DrawLine(pen, 21, 6, 21, 18);
                g.DrawArc(pen, 3, 9, 18, 6, 0, 180);
                g.DrawArc(pen, 3, 15, 18, 6, 0, 180);
                break;

            case AppIcon.Tree: // lucide: folder-tree
                DrawRoundedRect(g, pen, 3, 3, 7, 5, 1);
                g.DrawLines(pen, [new PointF(6, 8), new PointF(6, 19), new PointF(12, 19)]);
                g.DrawLine(pen, 6, 12, 12, 12);
                DrawRoundedRect(g, pen, 12, 10, 9, 4, 1);
                DrawRoundedRect(g, pen, 12, 17, 9, 4, 1);
                break;

            case AppIcon.Template: // lucide: shield-check
                g.DrawLines(pen, [new PointF(12, 2), new PointF(20, 5), new PointF(20, 12), new PointF(12, 22), new PointF(4, 12), new PointF(4, 5), new PointF(12, 2)]);
                g.DrawLines(pen, [new PointF(9, 12), new PointF(11, 14), new PointF(15, 10)]);
                break;

            case AppIcon.UserPlus: // lucide: user-plus
                g.DrawEllipse(pen, 5, 3, 8, 8);
                g.DrawArc(pen, 2, 14, 14, 10, 185, 170);
                g.DrawLine(pen, 19, 8, 19, 14);
                g.DrawLine(pen, 16, 11, 22, 11);
                break;

            case AppIcon.Cancel: // lucide: circle-x
                g.DrawEllipse(pen, 2, 2, 20, 20);
                g.DrawLine(pen, 15, 9, 9, 15);
                g.DrawLine(pen, 9, 9, 15, 15);
                break;

            case AppIcon.Eraser: // lucide: eraser
                g.DrawPolygon(pen, [new PointF(7, 21), new PointF(2, 16), new PointF(13, 5), new PointF(20, 12), new PointF(11, 21)]);
                g.DrawLine(pen, 7, 21, 22, 21);
                g.DrawLine(pen, 9, 9, 16, 16);
                break;

            case AppIcon.Wrench: // lucide: wrench
                g.DrawArc(pen, 13, 2, 9, 9, -40, 260);
                g.DrawLines(pen, [new PointF(14.5f, 9.5f), new PointF(4, 20), new PointF(2, 22), new PointF(4, 22), new PointF(14.5f, 11.5f)]);
                g.DrawLine(pen, 17, 5, 19, 7);
                break;

            case AppIcon.BarChart: // lucide: bar-chart-3
                g.DrawLines(pen, [new PointF(3, 3), new PointF(3, 21), new PointF(21, 21)]);
                g.DrawLine(pen, 8, 17, 8, 12);
                g.DrawLine(pen, 13, 17, 13, 7);
                g.DrawLine(pen, 18, 17, 18, 10);
                break;

            case AppIcon.Package: // lucide: package
                g.DrawPolygon(pen, [new PointF(12, 2), new PointF(21, 7), new PointF(21, 17), new PointF(12, 22), new PointF(3, 17), new PointF(3, 7)]);
                g.DrawLines(pen, [new PointF(3, 7), new PointF(12, 12), new PointF(21, 7)]);
                g.DrawLine(pen, 12, 12, 12, 22);
                g.DrawLine(pen, 7.5f, 4.5f, 16.5f, 9.5f);
                break;

            case AppIcon.HardHat: // lucide: hard-hat
                g.DrawArc(pen, 4, 6, 16, 14, 180, 180);
                g.DrawLine(pen, 2, 15, 22, 15);
                g.DrawLine(pen, 10, 4, 10, 15);
                g.DrawLine(pen, 14, 4, 14, 15);
                g.DrawLine(pen, 10, 4, 14, 4);
                break;

            case AppIcon.ClipboardCheck: // lucide: clipboard-check
                DrawRoundedRect(g, pen, 8, 2, 8, 4, 1);
                g.DrawLines(pen, [new PointF(16, 4), new PointF(19, 4), new PointF(20, 5), new PointF(20, 20), new PointF(19, 22), new PointF(5, 22), new PointF(4, 20), new PointF(4, 5), new PointF(5, 4), new PointF(8, 4)]);
                g.DrawLines(pen, [new PointF(9, 14), new PointF(11, 16), new PointF(15, 11)]);
                break;

            case AppIcon.Factory: // lucide: factory
                g.DrawLines(pen, [new PointF(2, 20), new PointF(2, 10), new PointF(8, 13), new PointF(8, 10), new PointF(14, 13), new PointF(14, 4), new PointF(20, 4), new PointF(22, 20), new PointF(2, 20)]);
                g.DrawLine(pen, 17, 16, 18, 16);
                g.DrawLine(pen, 12, 16, 13, 16);
                g.DrawLine(pen, 7, 16, 8, 16);
                break;

            case AppIcon.TrendingUp: // lucide: trending-up
                g.DrawLines(pen, [new PointF(2, 17), new PointF(8.5f, 10.5f), new PointF(13.5f, 15.5f), new PointF(22, 7)]);
                g.DrawLines(pen, [new PointF(16, 7), new PointF(22, 7), new PointF(22, 13)]);
                break;

            case AppIcon.Cpu: // lucide: cpu
                DrawRoundedRect(g, pen, 5, 5, 14, 14, 2);
                g.DrawRectangle(pen, 9, 9, 6, 6);
                g.DrawLine(pen, 9, 2, 9, 5);
                g.DrawLine(pen, 15, 2, 15, 5);
                g.DrawLine(pen, 9, 19, 9, 22);
                g.DrawLine(pen, 15, 19, 15, 22);
                g.DrawLine(pen, 2, 9, 5, 9);
                g.DrawLine(pen, 2, 15, 5, 15);
                g.DrawLine(pen, 19, 9, 22, 9);
                g.DrawLine(pen, 19, 15, 22, 15);
                break;

            case AppIcon.Layers: // lucide: layers
                g.DrawPolygon(pen, [new PointF(12, 2), new PointF(22, 7), new PointF(12, 12), new PointF(2, 7)]);
                g.DrawLines(pen, [new PointF(2, 12), new PointF(12, 17), new PointF(22, 12)]);
                g.DrawLines(pen, [new PointF(2, 17), new PointF(12, 22), new PointF(22, 17)]);
                break;

            case AppIcon.MoveRight: // lucide: arrow-left-right
                g.DrawLines(pen, [new PointF(17, 3), new PointF(21, 7), new PointF(17, 11)]);
                g.DrawLine(pen, 21, 7, 7, 7);
                g.DrawLines(pen, [new PointF(7, 13), new PointF(3, 17), new PointF(7, 21)]);
                g.DrawLine(pen, 3, 17, 17, 17);
                break;

            case AppIcon.Power: // lucide: power
                g.DrawLine(pen, 12, 2, 12, 12);
                g.DrawArc(pen, 4, 5, 16, 16, -45, 270);
                break;

            case AppIcon.Ban: // lucide: ban
                g.DrawEllipse(pen, 2, 2, 20, 20);
                g.DrawLine(pen, 5, 5, 19, 19);
                break;

            case AppIcon.Shield: // lucide: shield
                g.DrawLines(pen, [new PointF(12, 2), new PointF(20, 5), new PointF(20, 12), new PointF(12, 22), new PointF(4, 12), new PointF(4, 5), new PointF(12, 2)]);
                break;

            case AppIcon.FileText: // lucide: file-text
                g.DrawPolygon(pen, [new PointF(5, 2), new PointF(15, 2), new PointF(20, 7), new PointF(20, 22), new PointF(5, 22)]);
                g.DrawLines(pen, [new PointF(14, 2), new PointF(14, 8), new PointF(20, 8)]);
                g.DrawLine(pen, 8, 13, 16, 13);
                g.DrawLine(pen, 8, 17, 14, 17);
                break;

            case AppIcon.Zap: // lucide: zap
                g.DrawPolygon(pen, [new PointF(13, 2), new PointF(4, 14), new PointF(12, 14), new PointF(11, 22), new PointF(20, 10), new PointF(12, 10)]);
                break;

            case AppIcon.Globe: // lucide: globe
                g.DrawEllipse(pen, 2, 2, 20, 20);
                g.DrawEllipse(pen, 8, 2, 8, 20);
                g.DrawLine(pen, 2, 12, 22, 12);
                break;

            case AppIcon.Gauge: // lucide: gauge
                g.DrawArc(pen, 2, 4, 20, 20, 180, 180);
                g.DrawLine(pen, 12, 15, 17, 9);
                g.DrawEllipse(pen, 10.5f, 13.5f, 3, 3);
                break;

            case AppIcon.Download: // lucide: download
                g.DrawLines(pen, [new PointF(21, 15), new PointF(21, 19), new PointF(19, 21), new PointF(5, 21), new PointF(3, 19), new PointF(3, 15)]);
                g.DrawLines(pen, [new PointF(7, 10), new PointF(12, 15), new PointF(17, 10)]);
                g.DrawLine(pen, 12, 15, 12, 3);
                break;

            case AppIcon.Upload: // lucide: upload
                g.DrawLines(pen, [new PointF(21, 15), new PointF(21, 19), new PointF(19, 21), new PointF(5, 21), new PointF(3, 19), new PointF(3, 15)]);
                g.DrawLines(pen, [new PointF(17, 8), new PointF(12, 3), new PointF(7, 8)]);
                g.DrawLine(pen, 12, 3, 12, 15);
                break;

            case AppIcon.Calendar: // lucide: calendar
                DrawRoundedRect(g, pen, 3, 4, 18, 18, 2);
                g.DrawLine(pen, 16, 2, 16, 6);
                g.DrawLine(pen, 8, 2, 8, 6);
                g.DrawLine(pen, 3, 10, 21, 10);
                break;

            case AppIcon.Copy: // lucide: copy
                DrawRoundedRect(g, pen, 8, 8, 13, 13, 2);
                g.DrawLines(pen, [new PointF(5, 16), new PointF(4, 16), new PointF(3, 15), new PointF(3, 4), new PointF(4, 3), new PointF(15, 3), new PointF(16, 4), new PointF(16, 5)]);
                break;

            case AppIcon.Expand: // lucide: chevrons-up-down
                g.DrawLines(pen, [new PointF(7, 15), new PointF(12, 20), new PointF(17, 15)]);
                g.DrawLines(pen, [new PointF(7, 9), new PointF(12, 4), new PointF(17, 9)]);
                break;

            case AppIcon.Collapse: // lucide: chevrons-down-up
                g.DrawLines(pen, [new PointF(7, 20), new PointF(12, 15), new PointF(17, 20)]);
                g.DrawLines(pen, [new PointF(7, 4), new PointF(12, 9), new PointF(17, 4)]);
                break;

            case AppIcon.ChevronLeft: // lucide: chevron-left
                g.DrawLines(pen, [new PointF(15, 18), new PointF(9, 12), new PointF(15, 6)]);
                break;

            case AppIcon.ChevronRight: // lucide: chevron-right
                g.DrawLines(pen, [new PointF(9, 18), new PointF(15, 12), new PointF(9, 6)]);
                break;

            case AppIcon.ChevronsLeft: // lucide: chevrons-left
                g.DrawLines(pen, [new PointF(11, 17), new PointF(6, 12), new PointF(11, 7)]);
                g.DrawLines(pen, [new PointF(18, 17), new PointF(13, 12), new PointF(18, 7)]);
                break;

            case AppIcon.ChevronsRight: // lucide: chevrons-right
                g.DrawLines(pen, [new PointF(6, 17), new PointF(11, 12), new PointF(6, 7)]);
                g.DrawLines(pen, [new PointF(13, 17), new PointF(18, 12), new PointF(13, 7)]);
                break;
        }

        return bmp;
    }

    private static void DrawRoundedRect(Graphics g, Pen pen, float x, float y, float w, float h, float r)
    {
        using GraphicsPath path = new();
        float d = r * 2;
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure();
        g.DrawPath(pen, path);
    }
}
