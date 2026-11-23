using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;

namespace Sispro.Services;

public sealed class WorkOrderService(
    IWorkOrderRepository workOrders,
    IProviderRepository providers,
    IRequesterRepository requesters,
    IAssetTreeRepository assetTree,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null)
{
    public int GetNextWorkOrderNumber() => workOrders.GetNextWorkOrderNumber();

    public WorkOrder? GetByNumber(int numeroOt) => workOrders.GetByNumber(numeroOt);

    public WorkOrder? GetById(int idOt) => workOrders.GetById(idOt);

    public IReadOnlyList<WorkOrderSearchRow> Search(UserSession session, WorkOrderSearchFilter filter)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && (p.BusquedaOt || p.IngresoOt || p.ImpresionOt), "Buscar órdenes de trabajo");

        if (filter.FechaInicio.HasValue && filter.FechaFin.HasValue && filter.FechaInicio.Value.Date > filter.FechaFin.Value.Date)
        {
            throw new ArgumentException("La fecha de término debe ser mayor o igual a la fecha de inicio.");
        }

        return workOrders.Search(filter);
    }

    public int Create(UserSession session, WorkOrder order)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && p.IngresoOt, "Ingresar órdenes de trabajo");
        ValidateWorkOrder(order, isUpdate: false);

        if (workOrders.GetByNumber(order.NumeroOt) is not null)
        {
            throw new InvalidOperationException($"El N° de OT {order.NumeroOt} ya se encuentra registrado.");
        }

        PopulateAssetPath(order);
        int id = workOrders.Create(order);
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", "CREAR_OT",
            $"OT N° {order.NumeroOt} creada ({order.Detalles.Count} ítems, Proveedor ID {order.IdProv}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "CREAR_OT");
        return id;
    }

    public void Update(UserSession session, WorkOrder order)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && p.IngresoOt, "Modificar órdenes de trabajo");
        ValidateWorkOrder(order, isUpdate: true);

        WorkOrder? existingByNumber = workOrders.GetByNumber(order.NumeroOt);
        if (existingByNumber is not null && existingByNumber.IdOt != order.IdOt)
        {
            throw new InvalidOperationException($"Ya existe otra orden con el N° de OT {order.NumeroOt}.");
        }

        PopulateAssetPath(order);
        workOrders.Update(order);
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", "MODIFICAR_OT",
            $"OT N° {order.NumeroOt} (ID {order.IdOt}) actualizada ({order.Detalles.Count} ítems).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "MODIFICAR_OT");
    }

    public void Delete(UserSession session, int idOt)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && p.IngresoOt, "Eliminar órdenes de trabajo");
        WorkOrder existing = workOrders.GetById(idOt)
            ?? throw new InvalidOperationException("La orden de trabajo seleccionada no existe.");

        if (workOrders.HasInvoices(idOt))
        {
            throw new InvalidOperationException("No se puede eliminar la OT porque tiene guías de despacho o facturas asociadas.");
        }

        workOrders.Delete(idOt);
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", "ELIMINAR_OT",
            $"OT N° {existing.NumeroOt} (ID {idOt}) eliminada.");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "ELIMINAR_OT");
    }

    public void SetApproval(UserSession session, int idOt, ApprovalType type, bool approved)
    {
        if (type == ApprovalType.Supervisor)
        {
            AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && p.VistoBuenoSupervisor, "Registrar Visto Bueno de Supervisor");
        }
        else
        {
            AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && p.VistoBuenoJefatura, "Registrar Visto Bueno de Jefatura");
        }

        WorkOrder order = workOrders.GetById(idOt)
            ?? throw new InvalidOperationException("La orden de trabajo no existe.");

        workOrders.SetApproval(idOt, session.Usuario.IdUsuario, type, approved);
        string tag = type == ApprovalType.Supervisor ? "VBS" : "VBJ";
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", approved ? $"APROBAR_{tag}" : $"REVOCAR_{tag}",
            $"OT N° {order.NumeroOt}: {(approved ? "Aprobada" : "Revocada")} ({tag}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, approved ? $"APROBAR_{tag}" : $"REVOCAR_{tag}");
    }

    private void ValidateWorkOrder(WorkOrder order, bool isUpdate)
    {
        if (isUpdate && order.IdOt <= 0)
        {
            throw new ArgumentException("Debe indicar una orden de trabajo válida para modificar.");
        }

        if (order.NumeroOt <= 0)
        {
            throw new ArgumentException("Debe ingresar un número de OT válido mayor a cero.");
        }

        if (order.IdProv <= 0 || providers.GetById(order.IdProv) is null)
        {
            throw new ArgumentException("Debe seleccionar un proveedor válido.");
        }

        if (order.IdSol.HasValue && order.IdSol.Value > 0 && requesters.GetById(order.IdSol.Value) is null)
        {
            throw new ArgumentException("El solicitante seleccionado no existe.");
        }

        if (order.IdNivel < 2 || order.IdN2 <= 0)
        {
            throw new ArgumentException("Debe seleccionar al menos una planta o ubicación en el árbol de activos.");
        }

        if (order.Detalles.Count == 0)
        {
            throw new ArgumentException("Debe agregar al menos un detalle de trabajo antes de guardar.");
        }

        foreach (WorkOrderDetail detail in order.Detalles)
        {
            if (detail.Cantidad <= 0)
            {
                throw new ArgumentException("La cantidad de cada trabajo debe ser mayor a cero.");
            }

            if (string.IsNullOrWhiteSpace(detail.Descripcion))
            {
                throw new ArgumentException("Todos los ítems deben tener una descripción del trabajo.");
            }
        }
    }

    private void PopulateAssetPath(WorkOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.RutaActivo))
        {
            return;
        }

        int[] levels = order.GetLevelArray();
        int leafId = levels.LastOrDefault(id => id > 0);
        if (leafId <= 0)
        {
            return;
        }

        IReadOnlyList<AssetNode> path = assetTree.GetPathToRoot(leafId);
        if (path.Count > 0)
        {
            order.SetLevels(path.Select(n => n.IdActivo).ToList());
            order.RutaActivo = string.Join(" \\ ", path.Select(n => n.Descripcion));
        }
    }
}
