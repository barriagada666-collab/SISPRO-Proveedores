using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class InvoiceService(
    IInvoiceRepository invoices,
    IWorkOrderRepository workOrders,
    IProviderRepository providers,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null)
{
    public Invoice? GetById(int idFactura) => invoices.GetById(idFactura);

    public IReadOnlyList<InvoiceSearchRow> Search(UserSession session, InvoiceSearchFilter filter)
    {
        AuthService.EnsurePermission(session, p => p.MenuFacturas && (p.BusquedaFacturas || p.IngresoFacturas), "Buscar facturas y guías de despacho");

        if (filter.FechaInicio.HasValue && filter.FechaFin.HasValue && filter.FechaInicio.Value.Date > filter.FechaFin.Value.Date)
        {
            throw new ArgumentException("La fecha de término debe ser mayor o igual a la fecha de inicio.");
        }

        return invoices.Search(filter);
    }

    public bool IsDetailAlreadyInvoiced(int idDet) => invoices.IsDetailAlreadyInvoiced(idDet);

    public int Register(UserSession session, Invoice invoice)
    {
        AuthService.EnsurePermission(session, p => p.MenuFacturas && p.IngresoFacturas, "Registrar guías de despacho y facturas");

        if (invoice.NumeroDocumento <= 0)
        {
            throw new ArgumentException("Debe ingresar un número de guía de despacho / factura válido.");
        }

        if (invoice.IdProv <= 0 || providers.GetById(invoice.IdProv) is null)
        {
            throw new ArgumentException("Debe seleccionar un proveedor válido.");
        }

        if (invoices.GetByNumberAndProvider(invoice.NumeroDocumento, invoice.IdProv) is not null)
        {
            throw new InvalidOperationException($"El documento N° {invoice.NumeroDocumento} ya está registrado para este proveedor.");
        }

        if (invoice.Items.Count == 0)
        {
            throw new ArgumentException("Debe asociar al menos un ítem de orden de trabajo.");
        }

        HashSet<int> seenDetails = [];
        foreach (InvoiceDetail item in invoice.Items)
        {
            WorkOrder ot = workOrders.GetById(item.IdOt)
                ?? throw new InvalidOperationException($"La OT asociada (ID {item.IdOt}) no existe.");

            if (ot.IdProv != invoice.IdProv)
            {
                throw new InvalidOperationException(
                    $"La OT N° {ot.NumeroOt} pertenece al proveedor '{ot.NombreProveedor}' y no coincide con el proveedor seleccionado.");
            }

            if (item.ValorTrabajo < 0)
            {
                throw new ArgumentException("El valor de un trabajo no puede ser negativo.");
            }

            if (item.IdDet.HasValue)
            {
                if (!seenDetails.Add(item.IdDet.Value))
                {
                    throw new InvalidOperationException("No se puede asociar el mismo ítem de OT dos veces en la misma factura.");
                }

                if (invoices.IsDetailAlreadyInvoiced(item.IdDet.Value))
                {
                    throw new InvalidOperationException($"El ítem #{item.IdDet.Value} de la OT N° {ot.NumeroOt} ya fue asociado a otra factura.");
                }
            }

            item.UsuarioSesion = session.Usuario.AliasUsuario;
        }

        if (invoice.TotalNeto <= 0)
        {
            invoice.TotalNeto = invoice.Items.Sum(i => i.ValorTrabajo);
        }

        invoice.Iva = TaxCalculator.CalculateIva(invoice.TotalNeto);
        invoice.TotalConIva = TaxCalculator.CalculateTotalWithIva(invoice.TotalNeto);
        invoice.UsuarioRegistro = session.Usuario.AliasUsuario;

        int id = invoices.Create(invoice);
        audit.Log(session.Usuario.AliasUsuario, "Facturas y Guías", "REGISTRAR_FACTURA",
            $"Documento N° {invoice.NumeroDocumento} registrado (Neto: ${invoice.TotalNeto:N0}, Total c/IVA: ${invoice.TotalConIva:N0}, {invoice.Items.Count} ítems).");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Invoices | Sispro.Core.Enums.DataChangeKind.WorkOrders | Sispro.Core.Enums.DataChangeKind.Audit, "REGISTRAR_FACTURA");
        return id;
    }

    public void Delete(UserSession session, int idFactura)
    {
        AuthService.EnsurePermission(session, p => p.MenuFacturas && p.IngresoFacturas, "Eliminar facturas y guías de despacho");
        Invoice existing = invoices.GetById(idFactura)
            ?? throw new InvalidOperationException("El documento seleccionado no existe.");

        invoices.Delete(idFactura);
        audit.Log(session.Usuario.AliasUsuario, "Facturas y Guías", "ELIMINAR_FACTURA",
            $"Documento N° {existing.NumeroDocumento} (Proveedor: {existing.NombreProveedor}) eliminado.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Invoices | Sispro.Core.Enums.DataChangeKind.WorkOrders | Sispro.Core.Enums.DataChangeKind.Audit, "ELIMINAR_FACTURA");
    }
}
