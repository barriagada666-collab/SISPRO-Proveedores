using System.Drawing;
using Sispro.Core.Enums;
using Sispro.Core.Validation;
using Sispro.UI.Theme;

namespace Sispro.UI.Controls;

/// <summary>
/// Control estandarizado de filtro de fechas con Date Pickers (dd-MM-yyyy),
/// atajos rápidos (Todos / Hoy / Semana / Mes / Año / Rango) y validación automática Desde <= Hasta.
/// </summary>
public sealed class DateRangeFilterControl : UserControl
{
    private readonly ComboBox _cmbPreset;
    private readonly DateTimePicker _dtpDesde;
    private readonly DateTimePicker _dtpHasta;
    private bool _suppressEvents;

    public event EventHandler? RangeChanged;

    public DateTime? FechaInicio => _dtpDesde.Checked ? _dtpDesde.Value.Date : null;
    public DateTime? FechaFin => _dtpHasta.Checked ? _dtpHasta.Value.Date : null;

    public DateRangePreset ActivePreset =>
        _cmbPreset.SelectedIndex switch
        {
            1 => DateRangePreset.Hoy,
            2 => DateRangePreset.UltimaSemana,
            3 => DateRangePreset.UltimoMes,
            4 => DateRangePreset.EsteAnio,
            5 => DateRangePreset.Personalizado,
            _ => DateRangePreset.Todos
        };

    public DateRangeFilterControl(bool compact = false)
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BackColor = Color.Transparent;
        Margin = new Padding(0, 0, 6, 0);

        FlowLayoutPanel flow = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };

        Label lblPer = new()
        {
            Text = "Período:",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 7, 4, 0)
        };

        _cmbPreset = new ComboBox
        {
            Name = "cmbPeriodoFecha",
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = compact ? 96 : 112,
            Margin = new Padding(0, 3, 6, 0)
        };
        _cmbPreset.Items.AddRange(["Todos", "Hoy", "Últ. Semana", "Últ. Mes", "Este Año", "Rango"]);
        _cmbPreset.SelectedIndex = 0;
        _cmbPreset.SelectedIndexChanged += (_, _) => OnPresetSelected();

        Label lblDesde = new()
        {
            Text = "Desde:",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 7, 3, 0)
        };

        _dtpDesde = new DateTimePicker
        {
            Name = "dtpFechaDesde",
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MM-yyyy",
            ShowCheckBox = true,
            Checked = false,
            Value = DateTime.Today.AddMonths(-1),
            Width = compact ? 110 : 116,
            Margin = new Padding(0, 3, 6, 0)
        };
        _dtpDesde.ValueChanged += (_, _) => OnManualPickerChanged();

        Label lblHasta = new()
        {
            Text = "Hasta:",
            Font = CambiasoTheme.LabelFont,
            ForeColor = CambiasoTheme.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 7, 3, 0)
        };

        _dtpHasta = new DateTimePicker
        {
            Name = "dtpFechaHasta",
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MM-yyyy",
            ShowCheckBox = true,
            Checked = false,
            Value = DateTime.Today,
            Width = compact ? 110 : 116,
            Margin = new Padding(0, 3, 0, 0)
        };
        _dtpHasta.ValueChanged += (_, _) => OnManualPickerChanged();

        flow.Controls.AddRange([lblPer, _cmbPreset, lblDesde, _dtpDesde, lblHasta, _dtpHasta]);
        Controls.Add(flow);
    }

    public bool TryValidate(IWin32Window? owner, out string? error)
    {
        error = DomainValidator.ValidateDateRange(FechaInicio, FechaFin);
        if (error is not null && owner is not null)
        {
            CambiasoTheme.ShowWarning(owner, error);
        }

        return error is null;
    }

    public void SetRange(DateTime? desde, DateTime? hasta, bool raiseEvent = false)
    {
        _suppressEvents = true;
        try
        {
            if (desde.HasValue)
            {
                _dtpDesde.Value = ClampDate(desde.Value);
                _dtpDesde.Checked = true;
            }
            else
            {
                _dtpDesde.Checked = false;
            }

            if (hasta.HasValue)
            {
                _dtpHasta.Value = ClampDate(hasta.Value);
                _dtpHasta.Checked = true;
            }
            else
            {
                _dtpHasta.Checked = false;
            }

            _cmbPreset.SelectedIndex = (!desde.HasValue && !hasta.HasValue) ? 0 : 5;
        }
        finally
        {
            _suppressEvents = false;
        }

        if (raiseEvent)
        {
            RangeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear(bool raiseEvent = false) => SetRange(null, null, raiseEvent);

    public string BuildFilterDescription()
    {
        if (!FechaInicio.HasValue && !FechaFin.HasValue)
        {
            return "Fechas: Todos los registros";
        }

        string d1 = FechaInicio.HasValue ? FechaInicio.Value.ToString("dd-MM-yyyy") : "Inicio";
        string d2 = FechaFin.HasValue ? FechaFin.Value.ToString("dd-MM-yyyy") : "Hoy";
        return $"Fechas: {d1} a {d2}";
    }

    private void OnPresetSelected()
    {
        if (_suppressEvents)
        {
            return;
        }

        DateRangePreset preset = ActivePreset;
        if (preset == DateRangePreset.Personalizado)
        {
            _suppressEvents = true;
            if (!_dtpDesde.Checked) _dtpDesde.Checked = true;
            if (!_dtpHasta.Checked) _dtpHasta.Checked = true;
            _suppressEvents = false;
            RangeChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        (DateTime? desde, DateTime? hasta) = DomainValidator.ResolveDatePreset(preset);
        _suppressEvents = true;
        try
        {
            if (desde.HasValue)
            {
                _dtpDesde.Value = ClampDate(desde.Value);
                _dtpDesde.Checked = true;
            }
            else
            {
                _dtpDesde.Checked = false;
            }

            if (hasta.HasValue)
            {
                _dtpHasta.Value = ClampDate(hasta.Value);
                _dtpHasta.Checked = true;
            }
            else
            {
                _dtpHasta.Checked = false;
            }
        }
        finally
        {
            _suppressEvents = false;
        }

        RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnManualPickerChanged()
    {
        if (_suppressEvents)
        {
            return;
        }

        _suppressEvents = true;
        _cmbPreset.SelectedIndex = (_dtpDesde.Checked || _dtpHasta.Checked) ? 5 : 0;
        _suppressEvents = false;
        RangeChanged?.Invoke(this, EventArgs.Empty);
    }

    private DateTime ClampDate(DateTime dt)
    {
        if (dt < _dtpDesde.MinDate) return _dtpDesde.MinDate;
        if (dt > _dtpDesde.MaxDate) return _dtpDesde.MaxDate;
        return dt;
    }
}
