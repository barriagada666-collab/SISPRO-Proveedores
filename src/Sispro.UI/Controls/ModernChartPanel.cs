using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using Sispro.Core.Dtos;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Gráficas vectoriales modernas de alta precisión (GDI+ High-DPI con antialiasing)
/// diseñadas bajo la paleta corporativa Cambiaso Hnos S.A.C. (Petrol Navy, Cyan Industrial y Dorado).
/// </summary>
public abstract class ModernChartCard : Panel
{
    private readonly string _title;
    private readonly string _subtitle;
    private readonly AppIcon _icon;
    protected IReadOnlyList<ChartDataPoint> Points { get; private set; } = [];

    protected ModernChartCard(string title, string subtitle, AppIcon icon)
    {
        _title = title;
        _subtitle = subtitle;
        _icon = icon;

        BackColor = CambiasoTheme.CardBackground;
        Margin = new Padding(0, 0, 10, 6);
        CambiasoTheme.EnableDoubleBuffering(this);
        Resize += (_, _) => Invalidate();
    }

    public void SetData(IReadOnlyList<ChartDataPoint> data)
    {
        Points = data;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // Card border & top subtle bar
        using Pen border = new(CambiasoTheme.BorderColor, 1f);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using SolidBrush topAccent = new(CambiasoTheme.Primary);
        g.FillRectangle(topAccent, 0, 0, Width, 3);

        // Header with Lucide icon badge
        Rectangle badge = new(12, 11, 26, 26);
        using SolidBrush badgeBg = new(CambiasoTheme.SubtleFill);
        g.FillEllipse(badgeBg, badge);
        g.DrawImage(IconCatalog.Get(_icon, CambiasoTheme.Primary, 15), badge.X + 5, badge.Y + 5, 15, 15);

        using SolidBrush titleBrush = new(CambiasoTheme.TextPrimary);
        using SolidBrush subBrush = new(CambiasoTheme.TextMuted);
        using Font titleFont = new("Segoe UI", 9.5f, FontStyle.Bold);
        using Font subFont = new("Segoe UI", 7.8f, FontStyle.Regular);

        g.DrawString(_title, titleFont, titleBrush, 44, 10);
        g.DrawString(_subtitle, subFont, subBrush, 44, 26);

        RectangleF plot = new(12, 46, Math.Max(40, Width - 24), Math.Max(40, Height - 56));
        if (Points.Count == 0)
        {
            using StringFormat sf = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("Sin datos registrados", CambiasoTheme.SmallFont, subBrush, plot, sf);
            return;
        }

        PaintPlot(g, plot);
    }

    protected abstract void PaintPlot(Graphics g, RectangleF plot);

    protected static GraphicsPath RoundedTopRect(RectangleF rect, float radius)
    {
        float r = Math.Min(radius, Math.Min(rect.Width / 2f, rect.Height));
        GraphicsPath path = new();
        if (r <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = r * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        path.CloseFigure();
        return path;
    }

    protected static GraphicsPath RoundedCapsule(RectangleF rect, float radius)
    {
        float r = Math.Min(radius, Math.Min(rect.Width / 2f, rect.Height / 2f));
        GraphicsPath path = new();
        if (r <= 0.5f)
        {
            path.AddRectangle(rect);
            return path;
        }

        float d = r * 2f;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Gráfico de barras verticales con gradiente corporativo, líneas guía y etiquetas de valor.
/// </summary>
public sealed class VerticalBarChartCard(string title, string subtitle, AppIcon icon = AppIcon.TrendingUp)
    : ModernChartCard(title, subtitle, icon)
{
    protected override void PaintPlot(Graphics g, RectangleF plot)
    {
        double maxVal = Math.Max(1.0, Points.Max(p => p.Value));
        float topPad = 18f;
        float bottomPad = 24f;
        float leftPad = 6f;
        float chartH = Math.Max(20f, plot.Height - topPad - bottomPad);
        float chartW = Math.Max(20f, plot.Width - leftPad * 2);
        float baseY = plot.Y + topPad + chartH;

        // Subtle horizontal guide lines
        using Pen gridPen = new(Color.FromArgb(228, 236, 243), 1f) { DashStyle = DashStyle.Dot };
        for (int s = 0; s <= 3; s++)
        {
            float gy = baseY - (chartH * s / 3f);
            g.DrawLine(gridPen, plot.X + leftPad, gy, plot.Right - leftPad, gy);
        }

        int n = Points.Count;
        float slotW = chartW / n;
        float barW = Math.Clamp(slotW * 0.58f, 12f, 34f);

        using Font valFont = new("Segoe UI", 7.6f, FontStyle.Bold);
        using Font lblFont = new("Segoe UI", 7.8f, FontStyle.Regular);
        using SolidBrush valBrush = new(CambiasoTheme.Primary);
        using SolidBrush lblBrush = new(CambiasoTheme.TextMuted);
        using StringFormat center = new() { Alignment = StringAlignment.Center };

        for (int i = 0; i < n; i++)
        {
            ChartDataPoint pt = Points[i];
            float ratio = (float)(pt.Value / maxVal);
            float h = Math.Max(3f, chartH * ratio);
            float x = plot.X + leftPad + (i * slotW) + ((slotW - barW) / 2f);
            float y = baseY - h;

            RectangleF barRect = new(x, y, barW, h);
            bool isLast = i == n - 1;
            Color cTop = isLast ? CambiasoTheme.GoldAccent : CambiasoTheme.Accent;
            Color cBot = isLast ? Color.FromArgb(166, 129, 72) : CambiasoTheme.Primary;

            using GraphicsPath path = RoundedTopRect(barRect, 5f);
            using LinearGradientBrush grad = new(barRect, cTop, cBot, LinearGradientMode.Vertical);
            g.FillPath(grad, path);

            string vText = pt.Value.ToString("N0", CultureInfo.InvariantCulture);
            g.DrawString(vText, valFont, valBrush, new RectangleF(x - 10, y - 15, barW + 20, 14), center);
            g.DrawString(pt.Label, lblFont, lblBrush, new RectangleF(x - 10, baseY + 4, barW + 20, 16), center);
        }
    }
}

/// <summary>
/// Gráfico de anillo (Donut) moderno con KPI central y leyenda de porcentajes.
/// </summary>
public sealed class DonutChartCard(string title, string subtitle, AppIcon icon = AppIcon.Factory)
    : ModernChartCard(title, subtitle, icon)
{
    private static readonly Color[] Palette =
    [
        CambiasoTheme.Primary,
        CambiasoTheme.Accent,
        CambiasoTheme.GoldAccent,
        CambiasoTheme.Success,
        Color.FromArgb(109, 40, 217),
        CambiasoTheme.Warning
    ];

    protected override void PaintPlot(Graphics g, RectangleF plot)
    {
        double total = Math.Max(1.0, Points.Sum(p => p.Value));
        float diameter = Math.Clamp(Math.Min(plot.Height - 10f, plot.Width * 0.46f), 76f, 134f);
        RectangleF ringRect = new(plot.X + 8f, plot.Y + (plot.Height - diameter) / 2f, diameter, diameter);

        float startAngle = -90f;
        for (int i = 0; i < Points.Count; i++)
        {
            float sweep = (float)(Points[i].Value / total * 360.0);
            if (sweep < 0.8f)
            {
                continue;
            }

            using SolidBrush slice = new(Palette[i % Palette.Length]);
            g.FillPie(slice, ringRect.X, ringRect.Y, ringRect.Width, ringRect.Height, startAngle, sweep);
            startAngle += sweep;
        }

        // Inner white cutout for donut ring
        float holeRatio = 0.58f;
        float holeSize = diameter * holeRatio;
        RectangleF holeRect = new(
            ringRect.X + (diameter - holeSize) / 2f,
            ringRect.Y + (diameter - holeSize) / 2f,
            holeSize,
            holeSize);
        using SolidBrush white = new(CambiasoTheme.CardBackground);
        g.FillEllipse(white, holeRect);

        // Center total
        using Font bigFont = new("Segoe UI", 11f, FontStyle.Bold);
        using Font smallFont = new("Segoe UI", 7.2f, FontStyle.Regular);
        using SolidBrush dark = new(CambiasoTheme.TextPrimary);
        using SolidBrush muted = new(CambiasoTheme.TextMuted);
        using StringFormat center = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };

        g.DrawString(total.ToString("N0", CultureInfo.InvariantCulture), bigFont, dark,
            new RectangleF(holeRect.X, holeRect.Y + 4, holeRect.Width, holeRect.Height * 0.56f), center);
        g.DrawString("TOTAL", smallFont, muted,
            new RectangleF(holeRect.X, holeRect.Y + holeRect.Height * 0.54f, holeRect.Width, holeRect.Height * 0.34f), center);

        // Legend on the right
        float legX = ringRect.Right + 14f;
        float legW = Math.Max(60f, plot.Right - legX - 4f);
        float rowH = Math.Min(26f, Math.Max(18f, (plot.Height - 8f) / Math.Max(1, Points.Count)));
        float legY = plot.Y + Math.Max(4f, (plot.Height - rowH * Points.Count) / 2f);

        using Font legFont = new("Segoe UI", 8f, FontStyle.Bold);
        using Font pctFont = new("Segoe UI", 7.6f, FontStyle.Regular);
        using StringFormat trimEllipsis = new() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

        for (int i = 0; i < Points.Count; i++)
        {
            ChartDataPoint pt = Points[i];
            double pct = pt.Value / total * 100.0;
            float y = legY + i * rowH;

            using SolidBrush dot = new(Palette[i % Palette.Length]);
            g.FillEllipse(dot, legX, y + 4, 9, 9);

            string label = $"{pt.Label}";
            string stat = $"{pt.Value:N0} ({pct:0.0}%)";
            g.DrawString(label, legFont, dark, new RectangleF(legX + 14, y, legW - 14, 14), trimEllipsis);
            g.DrawString(stat, pctFont, muted, new RectangleF(legX + 14, y + 12, legW - 14, 13), trimEllipsis);
        }
    }
}

/// <summary>
/// Gráfico horizontal de ranking (Top Proveedores / Top Oficios) con barras cápsula y valores.
/// </summary>
public sealed class HorizontalRankChartCard(string title, string subtitle, AppIcon icon = AppIcon.BarChart)
    : ModernChartCard(title, subtitle, icon)
{
    protected override void PaintPlot(Graphics g, RectangleF plot)
    {
        double maxVal = Math.Max(1.0, Points.Max(p => p.Value));
        int n = Points.Count;
        float rowH = Math.Max(22f, (plot.Height - 4f) / n);

        using Font nameFont = new("Segoe UI", 7.8f, FontStyle.Bold);
        using Font valFont = new("Segoe UI", 7.8f, FontStyle.Bold);
        using SolidBrush nameBrush = new(CambiasoTheme.TextPrimary);
        using SolidBrush valBrush = new(CambiasoTheme.Primary);
        using SolidBrush trackBrush = new(CambiasoTheme.SubtleFill);
        using StringFormat trim = new() { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
        using StringFormat rightAlign = new() { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap };

        for (int i = 0; i < n; i++)
        {
            ChartDataPoint pt = Points[i];
            float y = plot.Y + i * rowH;

            g.DrawString(pt.Label, nameFont, nameBrush, new RectangleF(plot.X, y, plot.Width - 52, 13), trim);
            g.DrawString(pt.Value.ToString("N0", CultureInfo.InvariantCulture), valFont, valBrush,
                new RectangleF(plot.Right - 50, y, 50, 13), rightAlign);

            RectangleF track = new(plot.X, y + 14, plot.Width, 7.5f);
            using GraphicsPath trackPath = RoundedCapsule(track, 4f);
            g.FillPath(trackBrush, trackPath);

            float fillW = Math.Max(8f, (float)(pt.Value / maxVal) * track.Width);
            RectangleF fillRect = new(track.X, track.Y, fillW, track.Height);
            using GraphicsPath fillPath = RoundedCapsule(fillRect, 4f);
            using LinearGradientBrush fillGrad = new(
                fillRect,
                i == 0 ? CambiasoTheme.Primary : CambiasoTheme.Accent,
                i == 0 ? CambiasoTheme.Accent : CambiasoTheme.GoldAccent,
                LinearGradientMode.Horizontal);
            g.FillPath(fillGrad, fillPath);
        }
    }
}
