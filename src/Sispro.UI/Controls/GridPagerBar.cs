using System.Drawing;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Barra compacta de paginación estandarizada Cambiaso (Primera / Anterior / Página X de Y / Siguiente / Última)
/// con selector de tamaño de página y conteo exacto de registros mostrados.
/// </summary>
public sealed class GridPagerBar : UserControl
{
    private readonly ToolTip _toolTip = new();
    private readonly Button _btnFirst;
    private readonly Button _btnPrev;
    private readonly Button _btnNext;
    private readonly Button _btnLast;
    private readonly Label _lblPageInfo;
    private readonly Label _lblSummary;
    private readonly ComboBox _cmbPageSize;
    private readonly int _defaultPageSize;
    private bool _suppressEvents;

    public GridPagerBar(int defaultPageSize = 100)
    {
        _defaultPageSize = defaultPageSize;
        Height = 38;
        Dock = DockStyle.Bottom;
        BackColor = CambiasoTheme.CardBackground;
        Padding = new Padding(10, 5, 10, 4);
        CambiasoTheme.EnableDoubleBuffering(this);

        _lblSummary = new Label
        {
            AutoSize = false,
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Text = "Sin registros para mostrar."
        };

        FlowLayoutPanel nav = new()
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = new Padding(8, 0, 0, 0)
        };

        _btnFirst = CreateNavButton(AppIcon.ChevronsLeft, "Primera página", () => NavigateTo(0));
        _btnPrev = CreateNavButton(AppIcon.ChevronLeft, "Página anterior", () => NavigateTo(PageIndex - 1));

        _lblPageInfo = new Label
        {
            AutoSize = true,
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.Primary,
            Text = "Pág. 1 de 1",
            Margin = new Padding(8, 5, 8, 0)
        };

        _btnNext = CreateNavButton(AppIcon.ChevronRight, "Página siguiente", () => NavigateTo(PageIndex + 1));
        _btnLast = CreateNavButton(AppIcon.ChevronsRight, "Última página", () => NavigateTo(TotalPages - 1));

        Label lblSizePrefix = new()
        {
            AutoSize = true,
            Text = "Por pág.:",
            Font = CambiasoTheme.SmallFont,
            ForeColor = CambiasoTheme.TextMuted,
            Margin = new Padding(10, 6, 4, 0)
        };

        _cmbPageSize = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 68,
            Font = CambiasoTheme.SmallFont,
            Margin = new Padding(0, 2, 0, 0)
        };
        _cmbPageSize.Items.AddRange([50, 100, 200, 500]);
        if (_cmbPageSize.Items.Contains(defaultPageSize))
        {
            _cmbPageSize.SelectedItem = defaultPageSize;
        }
        else
        {
            _cmbPageSize.SelectedIndex = 1;
        }

        _cmbPageSize.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressEvents)
            {
                return;
            }

            PageIndex = 0;
            PageChanged?.Invoke(this, EventArgs.Empty);
        };

        nav.Controls.AddRange([_btnFirst, _btnPrev, _lblPageInfo, _btnNext, _btnLast, lblSizePrefix, _cmbPageSize]);

        // Fill se agrega primero y Right después para respetar el orden de acoplamiento de WinForms
        Controls.Add(_lblSummary);
        Controls.Add(nav);

        SetState(0, defaultPageSize, 0);
    }

    public int PageIndex { get; private set; }
    public int PageSize => _cmbPageSize.SelectedItem is int size ? size : _defaultPageSize;
    public int TotalItems { get; private set; }
    public int TotalPages => TotalItems <= 0 ? 1 : (int)Math.Ceiling((double)TotalItems / Math.Max(1, PageSize));

    public event EventHandler? PageChanged;

    public void ResetPageIndex() => PageIndex = 0;

    public void SetState(int pageIndex, int pageSize, int totalItems)
    {
        _suppressEvents = true;
        try
        {
            if (!_cmbPageSize.Items.Contains(pageSize))
            {
                _cmbPageSize.Items.Add(pageSize);
            }

            _cmbPageSize.SelectedItem = pageSize;
            TotalItems = Math.Max(0, totalItems);
            int totalPages = TotalPages;
            PageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, totalPages - 1));

            _lblPageInfo.Text = $"Pág. {PageIndex + 1:N0} de {totalPages:N0}";
            if (TotalItems == 0)
            {
                _lblSummary.Text = "Sin registros para los criterios actuales.";
            }
            else
            {
                int from = (PageIndex * pageSize) + 1;
                int to = Math.Min(TotalItems, (PageIndex + 1) * pageSize);
                _lblSummary.Text = $"Mostrando {from:N0}–{to:N0} de {TotalItems:N0} registro(s) · clic en columna para ordenar";
            }

            _btnFirst.Enabled = TotalItems > 0 && PageIndex > 0;
            _btnPrev.Enabled = TotalItems > 0 && PageIndex > 0;
            _btnNext.Enabled = TotalItems > 0 && PageIndex < totalPages - 1;
            _btnLast.Enabled = TotalItems > 0 && PageIndex < totalPages - 1;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using Pen pen = new(CambiasoTheme.BorderColor, 1f);
        e.Graphics.DrawLine(pen, 0, 0, Width, 0);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private Button CreateNavButton(AppIcon icon, string tooltip, Action onClick)
    {
        Button btn = CambiasoTheme.CreateButton(string.Empty, icon, ButtonTone.Secondary, _toolTip, tooltip, width: 32, height: 26);
        btn.TextImageRelation = TextImageRelation.Overlay;
        btn.ImageAlign = ContentAlignment.MiddleCenter;
        btn.Padding = Padding.Empty;
        btn.Margin = new Padding(2, 1, 2, 0);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void NavigateTo(int targetPage)
    {
        int clamped = Math.Clamp(targetPage, 0, Math.Max(0, TotalPages - 1));
        if (clamped == PageIndex)
        {
            return;
        }

        PageIndex = clamped;
        PageChanged?.Invoke(this, EventArgs.Empty);
    }
}
