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
    Template
}

/// <summary>
/// Generador de íconos vectoriales nítidos en caché para garantizar que todos los botones del sistema
/// tengan un ícono claro, alineado y consistente sin depender de recursos binarios heredados.
/// </summary>
public static class IconCatalog
{
    private static readonly ConcurrentDictionary<(AppIcon Icon, int Size, int ColorArgb), Bitmap> Cache = new();
    private static Icon? _appIcon;
    private static Image? _companyLogo;

    public static Icon GetAppIcon()
    {
        if (_appIcon is not null)
        {
            return _appIcon;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "sispro.ico");
        _appIcon = File.Exists(path) ? new Icon(path) : SystemIcons.Application;
        return _appIcon;
    }

    public static Image? GetCompanyLogo()
    {
        if (_companyLogo is not null)
        {
            return _companyLogo;
        }

        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "logo_cambiaso.jpg");
        if (File.Exists(path))
        {
            using FileStream fs = File.OpenRead(path);
            _companyLogo = Image.FromStream(fs);
        }

        return _companyLogo;
    }

    public static Bitmap Get(AppIcon icon, Color color, int size = 16)
    {
        return Cache.GetOrAdd((icon, size, color.ToArgb()), key => RenderIcon(key.Icon, Color.FromArgb(key.ColorArgb), key.Size));
    }

    private static Bitmap RenderIcon(AppIcon icon, Color color, int size)
    {
        Bitmap bmp = new(size, size);
        using Graphics g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.Transparent);

        float s = size;
        float pad = s * 0.14f;
        float w = s - (pad * 2);
        using Pen pen = new(color, Math.Max(1.5f, s * 0.11f))
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        };
        using SolidBrush brush = new(color);

        switch (icon)
        {
            case AppIcon.Save:
                g.DrawRectangle(pen, pad, pad, w, w);
                g.FillRectangle(brush, pad + w * 0.2f, pad, w * 0.55f, w * 0.35f);
                g.DrawRectangle(pen, pad + w * 0.22f, pad + w * 0.56f, w * 0.56f, w * 0.44f);
                break;

            case AppIcon.Edit:
                g.DrawLine(pen, pad + w * 0.15f, pad + w * 0.85f, pad + w * 0.78f, pad + w * 0.22f);
                g.DrawLine(pen, pad + w * 0.62f, pad + w * 0.12f, pad + w * 0.88f, pad + w * 0.38f);
                g.DrawLine(pen, pad, pad + w, pad + w * 0.9f, pad + w);
                break;

            case AppIcon.Delete:
                g.DrawLine(pen, pad + w * 0.1f, pad + w * 0.22f, pad + w * 0.9f, pad + w * 0.22f);
                g.DrawLine(pen, pad + w * 0.35f, pad + w * 0.08f, pad + w * 0.65f, pad + w * 0.08f);
                g.DrawRectangle(pen, pad + w * 0.22f, pad + w * 0.24f, w * 0.56f, w * 0.7f);
                g.DrawLine(pen, pad + w * 0.42f, pad + w * 0.42f, pad + w * 0.42f, pad + w * 0.78f);
                g.DrawLine(pen, pad + w * 0.58f, pad + w * 0.42f, pad + w * 0.58f, pad + w * 0.78f);
                break;

            case AppIcon.Clear:
                g.DrawLine(pen, pad + w * 0.2f, pad + w * 0.8f, pad + w * 0.8f, pad + w * 0.2f);
                g.DrawLine(pen, pad + w * 0.2f, pad + w * 0.2f, pad + w * 0.8f, pad + w * 0.8f);
                break;

            case AppIcon.Search:
                g.DrawEllipse(pen, pad, pad, w * 0.68f, w * 0.68f);
                g.DrawLine(pen, pad + w * 0.58f, pad + w * 0.58f, pad + w * 0.96f, pad + w * 0.96f);
                break;

            case AppIcon.Add:
                g.DrawLine(pen, pad + w * 0.5f, pad + w * 0.12f, pad + w * 0.5f, pad + w * 0.88f);
                g.DrawLine(pen, pad + w * 0.12f, pad + w * 0.5f, pad + w * 0.88f, pad + w * 0.5f);
                break;

            case AppIcon.Remove:
                g.DrawLine(pen, pad + w * 0.12f, pad + w * 0.5f, pad + w * 0.88f, pad + w * 0.5f);
                break;

            case AppIcon.Print:
                g.DrawRectangle(pen, pad + w * 0.22f, pad, w * 0.56f, w * 0.34f);
                g.DrawRectangle(pen, pad, pad + w * 0.34f, w, w * 0.42f);
                g.DrawRectangle(pen, pad + w * 0.22f, pad + w * 0.62f, w * 0.56f, w * 0.36f);
                break;

            case AppIcon.Preview:
                g.DrawRectangle(pen, pad + w * 0.12f, pad, w * 0.74f, w * 0.92f);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.3f, pad + w * 0.72f, pad + w * 0.3f);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.55f, pad + w * 0.72f, pad + w * 0.55f);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.76f, pad + w * 0.56f, pad + w * 0.76f);
                break;

            case AppIcon.Excel:
                g.DrawRectangle(pen, pad, pad, w, w);
                g.DrawLine(pen, pad + w * 0.25f, pad + w * 0.25f, pad + w * 0.75f, pad + w * 0.75f);
                g.DrawLine(pen, pad + w * 0.75f, pad + w * 0.25f, pad + w * 0.25f, pad + w * 0.75f);
                break;

            case AppIcon.Exit:
            case AppIcon.Logout:
                g.DrawLine(pen, pad + w * 0.45f, pad + w * 0.1f, pad + w * 0.1f, pad + w * 0.1f);
                g.DrawLine(pen, pad + w * 0.1f, pad + w * 0.1f, pad + w * 0.1f, pad + w * 0.9f);
                g.DrawLine(pen, pad + w * 0.1f, pad + w * 0.9f, pad + w * 0.45f, pad + w * 0.9f);
                g.DrawLine(pen, pad + w * 0.35f, pad + w * 0.5f, pad + w * 0.92f, pad + w * 0.5f);
                g.DrawLine(pen, pad + w * 0.7f, pad + w * 0.28f, pad + w * 0.92f, pad + w * 0.5f);
                g.DrawLine(pen, pad + w * 0.7f, pad + w * 0.72f, pad + w * 0.92f, pad + w * 0.5f);
                break;

            case AppIcon.Login:
                g.DrawLine(pen, pad + w * 0.55f, pad + w * 0.1f, pad + w * 0.9f, pad + w * 0.1f);
                g.DrawLine(pen, pad + w * 0.9f, pad + w * 0.1f, pad + w * 0.9f, pad + w * 0.9f);
                g.DrawLine(pen, pad + w * 0.9f, pad + w * 0.9f, pad + w * 0.55f, pad + w * 0.9f);
                g.DrawLine(pen, pad + w * 0.08f, pad + w * 0.5f, pad + w * 0.68f, pad + w * 0.5f);
                g.DrawLine(pen, pad + w * 0.46f, pad + w * 0.28f, pad + w * 0.68f, pad + w * 0.5f);
                g.DrawLine(pen, pad + w * 0.46f, pad + w * 0.72f, pad + w * 0.68f, pad + w * 0.5f);
                break;

            case AppIcon.Key:
                g.DrawEllipse(pen, pad, pad + w * 0.22f, w * 0.44f, w * 0.44f);
                g.DrawLine(pen, pad + w * 0.44f, pad + w * 0.44f, pad + w * 0.96f, pad + w * 0.44f);
                g.DrawLine(pen, pad + w * 0.74f, pad + w * 0.44f, pad + w * 0.74f, pad + w * 0.7f);
                g.DrawLine(pen, pad + w * 0.92f, pad + w * 0.44f, pad + w * 0.92f, pad + w * 0.66f);
                break;

            case AppIcon.Settings:
                g.DrawEllipse(pen, pad + w * 0.2f, pad + w * 0.2f, w * 0.6f, w * 0.6f);
                g.DrawEllipse(pen, pad + w * 0.4f, pad + w * 0.4f, w * 0.2f, w * 0.2f);
                g.DrawLine(pen, pad + w * 0.5f, pad, pad + w * 0.5f, pad + w * 0.18f);
                g.DrawLine(pen, pad + w * 0.5f, pad + w * 0.82f, pad + w * 0.5f, pad + w);
                g.DrawLine(pen, pad, pad + w * 0.5f, pad + w * 0.18f, pad + w * 0.5f);
                g.DrawLine(pen, pad + w * 0.82f, pad + w * 0.5f, pad + w, pad + w * 0.5f);
                break;

            case AppIcon.Dashboard:
                g.DrawRectangle(pen, pad, pad, w * 0.42f, w * 0.42f);
                g.DrawRectangle(pen, pad + w * 0.56f, pad, w * 0.42f, w * 0.42f);
                g.DrawRectangle(pen, pad, pad + w * 0.56f, w * 0.42f, w * 0.42f);
                g.DrawRectangle(pen, pad + w * 0.56f, pad + w * 0.56f, w * 0.42f, w * 0.42f);
                break;

            case AppIcon.WorkOrder:
                g.DrawRectangle(pen, pad + w * 0.12f, pad + w * 0.1f, w * 0.76f, w * 0.88f);
                g.DrawRectangle(pen, pad + w * 0.32f, pad, w * 0.36f, w * 0.18f);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.44f, pad + w * 0.72f, pad + w * 0.44f);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.68f, pad + w * 0.72f, pad + w * 0.68f);
                break;

            case AppIcon.Invoice:
                g.DrawRectangle(pen, pad + w * 0.1f, pad, w * 0.8f, w);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.3f, pad + w * 0.72f, pad + w * 0.3f);
                g.DrawLine(pen, pad + w * 0.28f, pad + w * 0.55f, pad + w * 0.72f, pad + w * 0.55f);
                g.DrawLine(pen, pad + w * 0.5f, pad + w * 0.75f, pad + w * 0.72f, pad + w * 0.75f);
                break;

            case AppIcon.Provider:
                g.DrawRectangle(pen, pad + w * 0.1f, pad + w * 0.2f, w * 0.8f, w * 0.78f);
                g.DrawLine(pen, pad, pad + w * 0.98f, pad + w, pad + w * 0.98f);
                g.FillRectangle(brush, pad + w * 0.26f, pad + w * 0.36f, w * 0.16f, w * 0.16f);
                g.FillRectangle(brush, pad + w * 0.58f, pad + w * 0.36f, w * 0.16f, w * 0.16f);
                g.FillRectangle(brush, pad + w * 0.42f, pad + w * 0.68f, w * 0.16f, w * 0.3f);
                break;

            case AppIcon.Users:
                g.DrawEllipse(pen, pad + w * 0.26f, pad + w * 0.06f, w * 0.44f, w * 0.44f);
                g.DrawArc(pen, pad + w * 0.08f, pad + w * 0.56f, w * 0.84f, w * 0.55f, 180, 180);
                break;

            case AppIcon.Audit:
                g.DrawEllipse(pen, pad, pad, w, w);
                g.DrawLine(pen, pad + w * 0.5f, pad + w * 0.22f, pad + w * 0.5f, pad + w * 0.52f);
                g.DrawLine(pen, pad + w * 0.5f, pad + w * 0.52f, pad + w * 0.74f, pad + w * 0.66f);
                break;

            case AppIcon.Refresh:
                g.DrawArc(pen, pad + w * 0.06f, pad + w * 0.06f, w * 0.88f, w * 0.88f, 35, 290);
                g.DrawLine(pen, pad + w * 0.72f, pad + w * 0.08f, pad + w * 0.92f, pad + w * 0.26f);
                g.DrawLine(pen, pad + w * 0.92f, pad + w * 0.06f, pad + w * 0.92f, pad + w * 0.26f);
                break;

            case AppIcon.Check:
                g.DrawLine(pen, pad + w * 0.12f, pad + w * 0.55f, pad + w * 0.4f, pad + w * 0.82f);
                g.DrawLine(pen, pad + w * 0.4f, pad + w * 0.82f, pad + w * 0.88f, pad + w * 0.22f);
                break;

            case AppIcon.Backup:
                g.DrawEllipse(pen, pad + w * 0.12f, pad + w * 0.06f, w * 0.76f, w * 0.28f);
                g.DrawLine(pen, pad + w * 0.12f, pad + w * 0.2f, pad + w * 0.12f, pad + w * 0.82f);
                g.DrawLine(pen, pad + w * 0.88f, pad + w * 0.2f, pad + w * 0.88f, pad + w * 0.82f);
                g.DrawArc(pen, pad + w * 0.12f, pad + w * 0.68f, w * 0.76f, w * 0.28f, 0, 180);
                break;

            case AppIcon.Tree:
                g.DrawRectangle(pen, pad + w * 0.08f, pad + w * 0.08f, w * 0.32f, w * 0.24f);
                g.DrawLine(pen, pad + w * 0.24f, pad + w * 0.32f, pad + w * 0.24f, pad + w * 0.86f);
                g.DrawLine(pen, pad + w * 0.24f, pad + w * 0.56f, pad + w * 0.54f, pad + w * 0.56f);
                g.DrawLine(pen, pad + w * 0.24f, pad + w * 0.86f, pad + w * 0.54f, pad + w * 0.86f);
                g.DrawRectangle(pen, pad + w * 0.54f, pad + w * 0.44f, w * 0.36f, w * 0.22f);
                g.DrawRectangle(pen, pad + w * 0.54f, pad + w * 0.74f, w * 0.36f, w * 0.22f);
                break;

            case AppIcon.Template:
                g.DrawRectangle(pen, pad, pad, w, w);
                g.DrawLine(pen, pad, pad + w * 0.32f, pad + w, pad + w * 0.32f);
                g.DrawLine(pen, pad + w * 0.36f, pad + w * 0.32f, pad + w * 0.36f, pad + w);
                break;
        }

        return bmp;
    }
}
