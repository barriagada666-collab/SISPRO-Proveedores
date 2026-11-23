using System.Drawing;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Validation;
using Sispro.Services;
using Sispro.UI.Controls;
using Sispro.UI.Theme;

namespace Sispro.UI.Forms;

/// <summary>
/// Módulo de Facturas y Guías de Despacho (reemplaza tab_fac.frm y TAB_0_FAC.bas).
/// Permite asociar ítems de OT a Guías/Facturas con cálculo automático de IVA 19% y consulta/eliminación completa.
/// </summary>
public sealed class InvoicesForm : UserControl
{
    private readonly SisproApplicationContext _ctx;
    private readonly UserSession _session;
    private readonly ToolTip _toolTip = new();

    private readonly TabControl _tabs;
    private readonly TabPage _tabRegister;
    private readonly TabPage _tabSearch;

    // Tab 0: Ingreso / Localización
    private readonly TextBox _txtNumDoc;
    private readonly DateTimePicker _dtFechaDoc;
    private readonly ComboBox _cmbProvider;
    private readonly TextBox _txtRecibidoPor;
    private readonly TextBox _txtDetalleDoc;
    private readonly NumericUpDown _numNeto;
    private readonly TextBox _txtIva;
    private readonly TextBox _txtTotalIva;

    private readonly TextBox _txtLookupOt;
    private readonly Button _btnFindOt;
    private readonly DataGridView _gridOtDetails;
    private readonly NumericUpDown _numItemValue;
    private readonly Button _btnAssociateItem;
    private readonly Button _btnRemoveAssoc;
    private readonly DataGridView _gridAssociated;
    private readonly Button _btnSaveInvoice;
    private readonly Button _btnClearInvoice;

    private WorkOrder? _currentLookupOt;
    private readonly List<InvoiceDetail> _associatedItems = [];

    // Tab 1: Búsqueda
    private readonly CheckBox _chkSearchProv;
    private readonly ComboBox _cmbSearchProv;
    private readonly TextBox _txtSearchDoc;
    private readonly TextBox _txtSearchOt;
    private readonly DateRangeFilterControl _searchDateRange;
    private readonly Button _btnSearchInvoices;
    private readonly Button _btnClearSearchInvoices;
    private readonly Button _btnDeleteInvoice;
    private readonly Button _btnExportInvoices;
    private readonly DataGridView _gridInvoices;
    private readonly DataGridView _gridInvoiceItems;
    private IReadOnlyList<InvoiceSearchRow> _invoiceRows = [];
    private bool _syncSubscribed;

    public InvoicesForm(SisproApplicationContext ctx, UserSession session)
    {
        _ctx = ctx;
        _session = session;
        CambiasoTheme.EnableDoubleBuffering(this);
        BackColor = CambiasoTheme.Surface;
        Font = CambiasoTheme.BodyFont;
        Dock = DockStyle.Fill;

        _tabs = new TabControl { Dock = DockStyle.Fill, Font = CambiasoTheme.LabelFont, Padding = new Point(14, 6) };

        // --- TAB 0 ---
        _tabRegister = new TabPage("Guías de Despacho / Facturas") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _txtNumDoc = new TextBox { Width = 120, MaxLength = 10 };
        _txtNumDoc.KeyPress += DigitsOnly_KeyPress;
        _dtFechaDoc = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Width = 116, Value = DateTime.Today };
        _cmbProvider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
        _txtRecibidoPor = new TextBox { Width = 220, PlaceholderText = "Nombre quien recibe" };
        _txtDetalleDoc = new TextBox { Multiline = true, Width = 440, Height = 52, ScrollBars = ScrollBars.Vertical };

        _numNeto = new NumericUpDown { Minimum = 0, Maximum = 999_999_999, ThousandsSeparator = true, Width = 100 };
        _numNeto.ValueChanged += (_, _) => RecalculateTaxes();
        _txtIva = new TextBox { ReadOnly = true, Width = 88, BackColor = CambiasoTheme.Surface, Text = "$0" };
        _txtTotalIva = new TextBox { ReadOnly = true, Width = 102, BackColor = CambiasoTheme.SubtleFill, Font = CambiasoTheme.LabelFont, Text = "$0" };

        _txtLookupOt = new TextBox { Width = 100, MaxLength = 8 };
        _txtLookupOt.KeyPress += DigitsOnly_KeyPress;
        _txtLookupOt.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LookupWorkOrderForInvoice();
            }
        };
        _btnFindOt = CambiasoTheme.CreateButton("Buscar OT", AppIcon.Search, ButtonTone.Primary, _toolTip, "Cargar ítems de una Orden de Trabajo", width: 114, height: 28);
        _btnFindOt.Click += (_, _) => LookupWorkOrderForInvoice();

        _gridOtDetails = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridOtDetails);

        _numItemValue = new NumericUpDown { Minimum = 0, Maximum = 999_999_999, ThousandsSeparator = true, Width = 130 };
        _btnAssociateItem = CambiasoTheme.CreateButton("Asociar Ítem", AppIcon.Add, ButtonTone.Accent, _toolTip, "Asociar el ítem de OT seleccionado a esta Guía/Factura", width: 132, height: 30);
        _btnAssociateItem.Click += (_, _) => AssociateSelectedOtDetail();

        _btnRemoveAssoc = CambiasoTheme.CreateButton("Quitar Ítem", AppIcon.Remove, ButtonTone.Secondary, _toolTip, "Quitar ítem de la lista asociada", width: 120, height: 30);
        _btnRemoveAssoc.Click += (_, _) => RemoveAssociatedItem();

        _gridAssociated = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridAssociated);
        ContextMenuHelper.AttachToGrid(_gridAssociated, idx =>
            idx >= 0 && idx < _associatedItems.Count
                ? [ContextMenuHelper.Item("Quitar ítem", AppIcon.Remove, CambiasoTheme.Danger, RemoveAssociatedItem)]
                : []);

        _btnSaveInvoice = CambiasoTheme.CreateButton("Localizar / Guardar", AppIcon.Save, ButtonTone.Primary, _toolTip, "Registrar documento y asociar OTs seleccionadas", width: 168);
        _btnSaveInvoice.Click += (_, _) => SaveInvoice();

        _btnClearInvoice = CambiasoTheme.CreateButton("Limpiar", AppIcon.Clear, ButtonTone.Secondary, _toolTip, "Limpiar todos los campos del formulario", width: 116);
        _btnClearInvoice.Click += (_, _) => ClearRegisterForm();

        BuildRegisterLayout();

        // --- TAB 1 ---
        _tabSearch = new TabPage("Buscar Facturas y Guías") { BackColor = CambiasoTheme.Surface, Padding = new Padding(10) };
        _chkSearchProv = new CheckBox { Text = "Proveedor", AutoSize = true };
        _cmbSearchProv = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Enabled = false };
        _chkSearchProv.CheckedChanged += (_, _) => _cmbSearchProv.Enabled = _chkSearchProv.Checked;
        _txtSearchDoc = new TextBox { Width = 98, PlaceholderText = "N° Doc." };
        _txtSearchDoc.KeyPress += DigitsOnly_KeyPress;
        _txtSearchOt = new TextBox { Width = 88, PlaceholderText = "N° OT" };
        _txtSearchOt.KeyPress += DigitsOnly_KeyPress;

        _searchDateRange = new DateRangeFilterControl(compact: true);
        _searchDateRange.RangeChanged += (_, _) => ExecuteInvoiceSearch();

        _btnSearchInvoices = CambiasoTheme.CreateButton("Buscar", AppIcon.Search, ButtonTone.Primary, _toolTip, "Buscar facturas y guías registradas", width: 96, height: 30);
        _btnSearchInvoices.Click += (_, _) => ExecuteInvoiceSearch();

        _btnClearSearchInvoices = CambiasoTheme.CreateButton("Limpiar", AppIcon.Eraser, ButtonTone.Secondary, _toolTip, "Restablecer filtros de búsqueda", width: 96, height: 30);
        _btnClearSearchInvoices.Click += (_, _) => ClearInvoiceSearchFilters();

        _btnDeleteInvoice = CambiasoTheme.CreateButton("Eliminar", AppIcon.Delete, ButtonTone.Danger, _toolTip, "Eliminar factura seleccionada y liberar ítems de OT", width: 102, height: 30);
        _btnDeleteInvoice.Enabled = _session.Permisos.IngresoFacturas;
        _btnDeleteInvoice.Click += (_, _) => DeleteSelectedInvoice();

        _btnExportInvoices = CambiasoTheme.CreateButton("Exportar", AppIcon.Download, ButtonTone.Accent, _toolTip, "Exportar facturas filtradas a Excel, PDF, Word o CSV", width: 108, height: 30);
        _btnExportInvoices.Click += (_, _) => ExportUiHelper.ShowReportExportMenu(
            _btnExportInvoices,
            this,
            _ctx.DocumentExportService,
            "SISPRO_Facturas_Guias",
            () => DocumentExportService.BuildInvoicesReport(_invoiceRows, BuildInvoiceFilterSummary()));

        _gridInvoices = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridInvoices);
        _gridInvoices.SelectionChanged += (_, _) => LoadSelectedInvoiceItems();
        ContextMenuHelper.AttachToGrid(_gridInvoices, idx =>
            idx >= 0 && idx < _invoiceRows.Count
                ?
                [
                    ContextMenuHelper.Item("Ver ítems", AppIcon.Preview, CambiasoTheme.Primary, LoadSelectedInvoiceItems),
                    ContextMenuHelper.Item("Eliminar factura", AppIcon.Delete, CambiasoTheme.Danger, DeleteSelectedInvoice, enabled: _btnDeleteInvoice.Enabled)
                ]
                : []);

        _gridInvoiceItems = new DataGridView { Dock = DockStyle.Fill };
        CambiasoTheme.StyleGrid(_gridInvoiceItems);

        BuildSearchLayout();

        if (_session.Permisos.IngresoFacturas)
        {
            _tabs.TabPages.Add(_tabRegister);
        }

        if (_session.Permisos.BusquedaFacturas)
        {
            _tabs.TabPages.Add(_tabSearch);
        }

        Controls.Add(_tabs);
        LoadProviders();
        RefreshAssociatedGrid();
        SubscribeSync();
        if (_session.Permisos.BusquedaFacturas)
        {
            ExecuteInvoiceSearch();
        }
    }

    private void SubscribeSync()
    {
        if (_syncSubscribed) return;
        _ctx.SyncHub.DataChanged += OnSyncChanged;
        _syncSubscribed = true;
    }

    private void OnSyncChanged(object? sender, DataChangedEventArgs e)
    {
        if (IsDisposed || (e.Kind & (DataChangeKind.Catalogs | DataChangeKind.Invoices)) == 0)
        {
            return;
        }

        void RefreshUi()
        {
            if (IsDisposed) return;
            if ((e.Kind & DataChangeKind.Catalogs) != 0) LoadProviders();
            if ((e.Kind & DataChangeKind.Invoices) != 0 && _session.Permisos.BusquedaFacturas) ExecuteInvoiceSearch();
        }

        if (InvokeRequired && IsHandleCreated) BeginInvoke(RefreshUi);
        else if (!InvokeRequired) RefreshUi();
    }

    public void ReloadCatalogs() => LoadProviders();

    private void BuildRegisterLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

        // Top Split: Left = Invoice Header, Right = OT lookup
        TableLayoutPanel top = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        GroupBox grpDoc = CambiasoTheme.CreateGroupBox("Datos de la Guía de Despacho / Factura");
        grpDoc.Dock = DockStyle.Fill;
        TableLayoutPanel docTbl = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 5,
            Padding = new Padding(4)
        };
        docTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
        docTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        docTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        docTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        docTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        docTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        docTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        docTbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        docTbl.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        _txtNumDoc.Dock = DockStyle.Fill;
        _dtFechaDoc.Dock = DockStyle.Fill;
        _cmbProvider.Dock = DockStyle.Fill;
        _txtRecibidoPor.Dock = DockStyle.Fill;
        _txtDetalleDoc.Dock = DockStyle.Fill;

        docTbl.Controls.Add(CenterLabel("N° Documento:"), 0, 0);
        docTbl.Controls.Add(_txtNumDoc, 1, 0);
        docTbl.Controls.Add(CenterLabel("Fecha:"), 2, 0);
        docTbl.Controls.Add(_dtFechaDoc, 3, 0);

        docTbl.Controls.Add(CenterLabel("Proveedor:"), 0, 1);
        docTbl.Controls.Add(_cmbProvider, 1, 1);
        docTbl.SetColumnSpan(_cmbProvider, 3);

        docTbl.Controls.Add(CenterLabel("Recibido por:"), 0, 2);
        docTbl.Controls.Add(_txtRecibidoPor, 1, 2);
        docTbl.SetColumnSpan(_txtRecibidoPor, 3);

        docTbl.Controls.Add(CenterLabel("Detalle:"), 0, 3);
        docTbl.Controls.Add(_txtDetalleDoc, 1, 3);
        docTbl.SetColumnSpan(_txtDetalleDoc, 3);

        TableLayoutPanel taxTbl = new() { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Margin = Padding.Empty };
        taxTbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        taxTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        taxTbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        taxTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        taxTbl.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        taxTbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        _numNeto.Dock = DockStyle.Fill;
        _txtIva.Dock = DockStyle.Fill;
        _txtTotalIva.Dock = DockStyle.Fill;
        taxTbl.Controls.Add(CenterLabel("Neto ($):"), 0, 0);
        taxTbl.Controls.Add(_numNeto, 1, 0);
        taxTbl.Controls.Add(CenterLabel("IVA 19%:"), 2, 0);
        taxTbl.Controls.Add(_txtIva, 3, 0);
        taxTbl.Controls.Add(CenterLabel("Total:"), 4, 0);
        taxTbl.Controls.Add(_txtTotalIva, 5, 0);
        docTbl.Controls.Add(taxTbl, 0, 4);
        docTbl.SetColumnSpan(taxTbl, 4);

        grpDoc.Controls.Add(docTbl);

        GroupBox grpOt = CambiasoTheme.CreateGroupBox("Buscar Ítems de OT para Asociar");
        grpOt.Dock = DockStyle.Fill;
        FlowLayoutPanel otTop = new() { Dock = DockStyle.Top, Height = 38 };
        otTop.Controls.AddRange([Wrap("N° OT:", _txtLookupOt), _btnFindOt]);
        FlowLayoutPanel otBot = new() { Dock = DockStyle.Bottom, Height = 40 };
        otBot.Controls.AddRange([Wrap("Valor Trabajo ($):", _numItemValue), _btnAssociateItem]);
        grpOt.Controls.Add(_gridOtDetails);
        grpOt.Controls.Add(otTop);
        grpOt.Controls.Add(otBot);

        top.Controls.Add(grpDoc, 0, 0);
        top.Controls.Add(grpOt, 1, 0);

        GroupBox grpAssoc = CambiasoTheme.CreateGroupBox("Ítems de OT Asociados al Documento");
        grpAssoc.Dock = DockStyle.Fill;
        FlowLayoutPanel assocTools = new() { Dock = DockStyle.Top, Height = 38 };
        assocTools.Controls.Add(_btnRemoveAssoc);
        grpAssoc.Controls.Add(_gridAssociated);
        grpAssoc.Controls.Add(assocTools);

        FlowLayoutPanel footer = new() { Dock = DockStyle.Fill, Padding = new Padding(2, 8, 2, 4) };
        footer.Controls.AddRange([_btnSaveInvoice, _btnClearInvoice]);

        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(grpAssoc, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        _tabRegister.Controls.Add(layout);
    }

    private void BuildSearchLayout()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));

        GroupBox grpFilters = CambiasoTheme.CreateGroupBox("Filtros Combinados de Búsqueda (orden más reciente primero)");
        grpFilters.Dock = DockStyle.Fill;

        TableLayoutPanel fltGrid = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        fltGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        fltGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        FlowLayoutPanel flt1 = new() { Dock = DockStyle.Fill, WrapContents = false };
        flt1.Controls.AddRange([_chkSearchProv, _cmbSearchProv, Wrap("N° Documento:", _txtSearchDoc), Wrap("N° OT:", _txtSearchOt), _btnSearchInvoices, _btnClearSearchInvoices, _btnDeleteInvoice, _btnExportInvoices]);

        FlowLayoutPanel flt2 = new() { Dock = DockStyle.Fill, WrapContents = false };
        flt2.Controls.Add(_searchDateRange);

        fltGrid.Controls.Add(flt1, 0, 0);
        fltGrid.Controls.Add(flt2, 0, 1);
        grpFilters.Controls.Add(fltGrid);

        GroupBox grpMaster = CambiasoTheme.CreateGroupBox("Documentos Registrados (orden cronológico descendente)");
        grpMaster.Dock = DockStyle.Fill;
        grpMaster.Controls.Add(_gridInvoices);

        GroupBox grpDetail = CambiasoTheme.CreateGroupBox("Detalle de OTs Asociadas al Documento Seleccionado");
        grpDetail.Dock = DockStyle.Fill;
        grpDetail.Controls.Add(_gridInvoiceItems);

        layout.Controls.Add(grpFilters, 0, 0);
        layout.Controls.Add(grpMaster, 0, 1);
        layout.Controls.Add(grpDetail, 0, 2);
        _tabSearch.Controls.Add(layout);
    }

    private void LoadProviders()
    {
        IReadOnlyList<Provider> providers = _ctx.MaintainerService.GetProviders();
        foreach (ComboBox cmb in new[] { _cmbProvider, _cmbSearchProv })
        {
            object? previousValue = cmb.SelectedValue;
            cmb.DataSource = null;
            cmb.DisplayMember = nameof(Provider.Nombre);
            cmb.ValueMember = nameof(Provider.IdProv);
            cmb.DataSource = providers.ToList();

            if (previousValue is not null)
            {
                cmb.SelectedValue = previousValue;
            }

            if (cmb.SelectedIndex < 0 && cmb.Items.Count > 0)
            {
                cmb.SelectedIndex = 0;
            }
        }
    }

    private void RecalculateTaxes()
    {
        decimal net = _numNeto.Value;
        decimal iva = TaxCalculator.CalculateIva(net);
        decimal total = TaxCalculator.CalculateTotalWithIva(net);
        _txtIva.Text = $"${iva:N0}";
        _txtTotalIva.Text = $"${total:N0}";
    }

    private void LookupWorkOrderForInvoice()
    {
        if (!int.TryParse(_txtLookupOt.Text.Trim(), out int numOt) || numOt <= 0)
        {
            CambiasoTheme.ShowWarning(this, "Ingrese un N° de OT válido.");
            return;
        }

        _currentLookupOt = _ctx.WorkOrderService.GetByNumber(numOt);
        if (_currentLookupOt is null)
        {
            _gridOtDetails.DataSource = null;
            CambiasoTheme.ShowInfo(this, $"No se encontró la OT N° {numOt}.");
            return;
        }

        _gridOtDetails.DataSource = _currentLookupOt.Detalles.Select(d => new
        {
            d.IdDet,
            N_OT = _currentLookupOt.NumeroOt,
            Proveedor = _currentLookupOt.NombreProveedor,
            d.Cantidad,
            Detalle = d.Descripcion,
            Estado = d.AsociadoAFactura ? "Ya Facturado" : "Disponible"
        }).ToList();

        if (_gridOtDetails.Columns.Count >= 6)
        {
            _gridOtDetails.Columns[0].HeaderText = "ID";
            _gridOtDetails.Columns[0].FillWeight = 8;
            _gridOtDetails.Columns[1].HeaderText = "N° OT";
            _gridOtDetails.Columns[1].FillWeight = 10;
            _gridOtDetails.Columns[2].FillWeight = 20;
            _gridOtDetails.Columns[3].FillWeight = 8;
            _gridOtDetails.Columns[4].FillWeight = 40;
            _gridOtDetails.Columns[5].FillWeight = 14;
        }
    }

    private void AssociateSelectedOtDetail()
    {
        if (_currentLookupOt is null || _gridOtDetails.CurrentRow is null)
        {
            CambiasoTheme.ShowWarning(this, "Busque y seleccione un ítem de OT primero.");
            return;
        }

        if (_cmbProvider.SelectedValue is not int selectedProvId)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un proveedor.");
            return;
        }

        if (_currentLookupOt.IdProv != selectedProvId)
        {
            CambiasoTheme.ShowWarning(this, $"La OT N° {_currentLookupOt.NumeroOt} pertenece al proveedor '{_currentLookupOt.NombreProveedor}' y no coincide con el proveedor de la factura.");
            return;
        }

        int idx = _gridOtDetails.CurrentRow.Index;
        if (idx < 0 || idx >= _currentLookupOt.Detalles.Count)
        {
            return;
        }

        WorkOrderDetail det = _currentLookupOt.Detalles[idx];
        if (det.AsociadoAFactura || _ctx.InvoiceService.IsDetailAlreadyInvoiced(det.IdDet))
        {
            CambiasoTheme.ShowWarning(this, "El ítem seleccionado ya se encuentra asociado a otra factura.");
            return;
        }

        if (_associatedItems.Any(i => i.IdDet == det.IdDet))
        {
            CambiasoTheme.ShowWarning(this, "Este ítem ya fue agregado a la lista actual.");
            return;
        }

        _associatedItems.Add(new InvoiceDetail
        {
            IdOt = _currentLookupOt.IdOt,
            NumeroOt = _currentLookupOt.NumeroOt,
            IdDet = det.IdDet,
            DescripcionTrabajo = det.Descripcion,
            FechaOt = _currentLookupOt.FechaOt,
            ValorTrabajo = _numItemValue.Value
        });

        _numItemValue.Value = 0;
        _numNeto.Value = Math.Min(_numNeto.Maximum, _associatedItems.Sum(i => i.ValorTrabajo));
        RefreshAssociatedGrid();
    }

    private void RemoveAssociatedItem()
    {
        if (_gridAssociated.CurrentRow is null)
        {
            return;
        }

        int idx = _gridAssociated.CurrentRow.Index;
        if (idx >= 0 && idx < _associatedItems.Count)
        {
            _associatedItems.RemoveAt(idx);
            _numNeto.Value = Math.Min(_numNeto.Maximum, _associatedItems.Sum(i => i.ValorTrabajo));
            RefreshAssociatedGrid();
        }
    }

    private void RefreshAssociatedGrid()
    {
        _gridAssociated.DataSource = null;
        _gridAssociated.DataSource = _associatedItems.Select(i => new
        {
            N_OT = i.NumeroOt,
            ID_Detalle = i.IdDet,
            Fecha_OT = i.FechaOt.ToString("dd-MM-yyyy"),
            Detalle = i.DescripcionTrabajo,
            Valor = $"${i.ValorTrabajo:N0}"
        }).ToList();

        if (_gridAssociated.Columns.Count >= 5)
        {
            _gridAssociated.Columns[0].HeaderText = "N° OT";
            _gridAssociated.Columns[0].FillWeight = 10;
            _gridAssociated.Columns[1].HeaderText = "ID Ítem";
            _gridAssociated.Columns[1].FillWeight = 10;
            _gridAssociated.Columns[2].HeaderText = "Fecha OT";
            _gridAssociated.Columns[2].FillWeight = 12;
            _gridAssociated.Columns[3].FillWeight = 52;
            _gridAssociated.Columns[4].HeaderText = "Valor ($)";
            _gridAssociated.Columns[4].FillWeight = 16;
        }
    }

    private void SaveInvoice()
    {
        try
        {
            if (!int.TryParse(_txtNumDoc.Text.Trim(), out int numDoc) || numDoc <= 0)
            {
                throw new ArgumentException("Debe ingresar un número de Guía de Despacho o Factura válido.");
            }

            Invoice inv = new()
            {
                NumeroDocumento = numDoc,
                IdProv = _cmbProvider.SelectedValue is int pid ? pid : 0,
                Fecha = _dtFechaDoc.Value.Date,
                RecibidoPor = _txtRecibidoPor.Text.Trim(),
                TotalNeto = _numNeto.Value,
                Detalle = _txtDetalleDoc.Text.Trim(),
                Items = [.. _associatedItems]
            };

            _ctx.InvoiceService.Register(_session, inv);
            CambiasoTheme.ShowInfo(this, $"Documento N° {inv.NumeroDocumento} registrado correctamente (Total c/IVA: ${inv.TotalConIva:N0}).");
            ClearRegisterForm();
            if (_session.Permisos.BusquedaFacturas)
            {
                ExecuteInvoiceSearch();
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ClearRegisterForm()
    {
        _txtNumDoc.Clear();
        _dtFechaDoc.Value = DateTime.Today;
        _txtRecibidoPor.Clear();
        _txtDetalleDoc.Clear();
        _numNeto.Value = 0;
        _numItemValue.Value = 0;
        _txtLookupOt.Clear();
        _currentLookupOt = null;
        _gridOtDetails.DataSource = null;
        _associatedItems.Clear();
        RefreshAssociatedGrid();
        RecalculateTaxes();
    }

    private void ExecuteInvoiceSearch()
    {
        if (!_searchDateRange.TryValidate(this, out _))
        {
            return;
        }

        try
        {
            InvoiceSearchFilter filter = new()
            {
                IdProv = _chkSearchProv.Checked && _cmbSearchProv.SelectedValue is int pid ? pid : null,
                NumeroDocumento = int.TryParse(_txtSearchDoc.Text.Trim(), out int nd) && nd > 0 ? nd : null,
                NumeroOt = int.TryParse(_txtSearchOt.Text.Trim(), out int not) && not > 0 ? not : null,
                FechaInicio = _searchDateRange.FechaInicio,
                FechaFin = _searchDateRange.FechaFin
            };

            _invoiceRows = _ctx.InvoiceService.Search(_session, filter);
            _gridInvoices.DataSource = _invoiceRows.Select(r => new
            {
                Documento = r.NumeroDocumento,
                Fecha = r.Fecha.ToString("dd-MM-yyyy"),
                r.Proveedor,
                RecibidoPor = r.RecibidoPor,
                Ítems = r.CantidadItems,
                Neto = $"${r.TotalNeto:N0}",
                IVA = $"${r.Iva:N0}",
                Total = $"${r.TotalConIva:N0}",
                r.Detalle
            }).ToList();

            if (_invoiceRows.Count == 0)
            {
                _gridInvoiceItems.DataSource = null;
            }
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(this, ex.Message);
        }
    }

    private void ClearInvoiceSearchFilters()
    {
        _chkSearchProv.Checked = false;
        _txtSearchDoc.Clear();
        _txtSearchOt.Clear();
        _searchDateRange.Clear();
        ExecuteInvoiceSearch();
    }

    private string BuildInvoiceFilterSummary()
    {
        string prov = _chkSearchProv.Checked && _cmbSearchProv.SelectedItem is Provider p ? p.Nombre : "Todos";
        return $"Proveedor: {prov} | {_searchDateRange.BuildFilterDescription()}";
    }

    private void LoadSelectedInvoiceItems()
    {
        if (_gridInvoices.CurrentRow is null)
        {
            _gridInvoiceItems.DataSource = null;
            return;
        }

        int idx = _gridInvoices.CurrentRow.Index;
        if (idx < 0 || idx >= _invoiceRows.Count)
        {
            return;
        }

        Invoice? full = _ctx.InvoiceService.GetById(_invoiceRows[idx].IdFactura);
        _gridInvoiceItems.DataSource = full?.Items.Select(i => new
        {
            N_OT = i.NumeroOt,
            Fecha_OT = i.FechaOt.ToString("dd-MM-yyyy"),
            Detalle = i.DescripcionTrabajo,
            Valor = $"${i.ValorTrabajo:N0}",
            RegistradoPor = i.UsuarioSesion
        }).ToList();
    }

    private void DeleteSelectedInvoice()
    {
        if (_gridInvoices.CurrentRow is null)
        {
            CambiasoTheme.ShowWarning(this, "Seleccione un documento para eliminar.");
            return;
        }

        int idx = _gridInvoices.CurrentRow.Index;
        if (idx < 0 || idx >= _invoiceRows.Count)
        {
            return;
        }

        InvoiceSearchRow row = _invoiceRows[idx];
        if (!CambiasoTheme.Confirm(this, $"¿Eliminar el documento N° {row.NumeroDocumento} de '{row.Proveedor}'? Los ítems de OT volverán a quedar disponibles."))
        {
            return;
        }

        try
        {
            _ctx.InvoiceService.Delete(_session, row.IdFactura);
            CambiasoTheme.ShowInfo(this, "Documento eliminado correctamente.");
            ExecuteInvoiceSearch();
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowError(this, ex.Message);
        }
    }

    private static Label CenterLabel(string text)
    {
        Label lbl = CambiasoTheme.CreateFieldLabel(text);
        lbl.Anchor = AnchorStyles.Left;
        return lbl;
    }

    private static FlowLayoutPanel Wrap(string label, Control ctl)
    {
        FlowLayoutPanel p = new() { AutoSize = true, Margin = new Padding(4, 2, 10, 2), WrapContents = false };
        Label lbl = CambiasoTheme.CreateFieldLabel(label);
        lbl.Margin = new Padding(0, 5, 6, 0);
        p.Controls.Add(lbl);
        p.Controls.Add(ctl);
        return p;
    }

    private static void DigitsOnly_KeyPress(object? sender, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
        {
            e.Handled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_syncSubscribed)
            {
                _ctx.SyncHub.DataChanged -= OnSyncChanged;
                _syncSubscribed = false;
            }

            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }
}
