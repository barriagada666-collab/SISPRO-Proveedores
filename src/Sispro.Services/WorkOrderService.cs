using System.Globalization;
using System.Text.RegularExpressions;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;

namespace Sispro.Services;

public sealed partial class WorkOrderService(
    IWorkOrderRepository workOrders,
    IProviderRepository providers,
    IRequesterRepository requesters,
    IAssetTreeRepository assetTree,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null,
    IUserRepository? users = null)
{
    private static readonly CultureInfo ClCulture = new("es-CL");

    [GeneratedRegex(@"^\s*-?\d+\.\d{1,2}\s*$")]
    private static partial Regex DotDecimalPattern();

    public static bool TryParseNetValue(string? rawText, out decimal value)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            value = 0m;
            return true;
        }

        string cleaned = rawText
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace("CLP", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (DotDecimalPattern().IsMatch(cleaned) &&
            decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal dotVal))
        {
            value = dotVal;
            return true;
        }

        if (decimal.TryParse(cleaned, NumberStyles.Number, ClCulture, out decimal clVal))
        {
            value = clVal;
            return true;
        }

        value = 0m;
        return false;
    }

    public int GetNextWorkOrderNumber() => workOrders.GetNextWorkOrderNumber();

    public WorkOrder? GetByNumber(int numeroOt) => workOrders.GetByNumber(numeroOt);

    public WorkOrder? GetById(int idOt) => workOrders.GetById(idOt);

    public IReadOnlyList<WorkOrder> GetByIds(UserSession session, IReadOnlyCollection<int> idOts)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && (p.BusquedaOt || p.IngresoOt || p.ImpresionOt), "Consultar órdenes de trabajo");
        return workOrders.GetByIds(idOts);
    }

    public DashboardAnalytics GetDashboardAnalytics() => workOrders.GetDashboardAnalytics();

    public IReadOnlyList<ApproverDirectoryEntry> GetApproverDirectory() =>
        users?.GetApprovers() ?? [];

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
            $"OT N° {order.NumeroOt} creada ({order.Detalles.Count} ítems, Proveedor ID {order.IdProv}, Neto $ {order.ValorNeto:N0}, Firmas: {order.FirmasSeleccionadas.FormatResumen()}).");
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
            $"OT N° {order.NumeroOt} (ID {order.IdOt}) actualizada ({order.Detalles.Count} ítems, Neto $ {order.ValorNeto:N0}, Firmas: {order.FirmasSeleccionadas.FormatResumen()}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "MODIFICAR_OT");
    }

    public void UpdateSignatures(UserSession session, int idOt, SignatureOption signatures)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && (p.IngresoOt || p.ImpresionOt), "Actualizar selección de firmas de OT");
        WorkOrder existing = workOrders.GetById(idOt)
            ?? throw new InvalidOperationException("La orden de trabajo seleccionada no existe.");

        if (existing.FirmasSeleccionadas == signatures)
        {
            return;
        }

        workOrders.UpdateSignatures(idOt, signatures);
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", "ACTUALIZAR_FIRMAS_OT",
            $"OT N° {existing.NumeroOt}: selección de firmas actualizada a '{signatures.FormatResumen()}'.");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "ACTUALIZAR_FIRMAS_OT");
    }

    public void Delete(UserSession session, int idOt)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo && p.IngresoOt, "Eliminar órdenes de trabajo");
        WorkOrder existing = workOrders.GetById(idOt)
            ?? throw new InvalidOperationException("La orden de trabajo seleccionada no existe.");

        workOrders.Delete(idOt);
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", "ELIMINAR_OT",
            $"OT N° {existing.NumeroOt} (ID {idOt}) eliminada.");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "ELIMINAR_OT");
    }

    public int DeleteManyExplicitly(UserSession session, IReadOnlyCollection<int> idOts, string reason)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Órdenes de Trabajo", "Eliminar explícitamente OTs asociadas");
        if (idOts is null || idOts.Count == 0)
        {
            return 0;
        }

        List<WorkOrder> snapshot = workOrders.GetByIds(idOts).ToList();
        if (snapshot.Count == 0)
        {
            return 0;
        }

        int deleted = workOrders.DeleteMany(snapshot.Select(o => o.IdOt).ToList());
        string nums = string.Join(", ", snapshot.Take(10).Select(o => $"#{o.NumeroOt}"));
        if (snapshot.Count > 10)
        {
            nums += $" (+{snapshot.Count - 10} más)";
        }

        string safeReason = string.IsNullOrWhiteSpace(reason) ? "Eliminación explícita por Administrador" : reason.Trim();
        audit.Log(session.Usuario.AliasUsuario, "Órdenes de Trabajo", "ELIMINAR_OTS_EXPLICITO",
            $"{deleted} OT(s) eliminada(s) explícitamente [{nums}]. Motivo: {safeReason}.");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "ELIMINAR_OTS_EXPLICITO");
        return deleted;
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

        WorkOrder? currentOrder = null;
        if (isUpdate)
        {
            currentOrder = workOrders.GetById(order.IdOt)
                ?? throw new InvalidOperationException("La orden de trabajo seleccionada no existe.");
        }

        if (order.NumeroOt <= 0)
        {
            throw new ArgumentException("Debe ingresar un número de OT válido mayor a cero.");
        }

        if (order.ValorNeto < 0m)
        {
            throw new ArgumentException("El valor neto de la OT debe ser mayor o igual a cero.");
        }

        order.Observacion = order.Observacion?.Trim() ?? string.Empty;
        order.ValorNeto = Math.Round(order.ValorNeto, 2, MidpointRounding.AwayFromZero);

        Provider? provider = providers.GetById(order.IdProv);
        if (order.IdProv <= 0 || provider is null)
        {
            throw new ArgumentException("Debe seleccionar un proveedor válido.");
        }

        if (!provider.Activo && (!isUpdate || currentOrder!.IdProv != order.IdProv))
        {
            throw new InvalidOperationException($"El proveedor '{provider.Nombre}' está inactivo y no puede utilizarse en nuevas OT.");
        }

        if (order.IdSol.HasValue && order.IdSol.Value > 0)
        {
            Requester? requester = requesters.GetById(order.IdSol.Value)
                ?? throw new ArgumentException("El solicitante seleccionado no existe.");
            if (!requester.Activo && (!isUpdate || currentOrder!.IdSol != order.IdSol))
            {
                throw new InvalidOperationException($"El solicitante '{requester.NombreCompleto}' está inactivo y no puede utilizarse en nuevas OT.");
            }
        }

        if (order.IdNivel < 2 || order.IdN2 <= 0)
        {
            throw new ArgumentException("Debe seleccionar al menos una planta o ubicación en el árbol de activos.");
        }

        int leafId = order.GetLevelArray().LastOrDefault(id => id > 0);
        int currentLeafId = currentOrder?.GetLevelArray().LastOrDefault(id => id > 0) ?? 0;
        if (leafId > 0)
        {
            AssetNode? leafNode = assetTree.GetById(leafId)
                ?? throw new ArgumentException("El activo seleccionado no existe en el árbol.");
            if (!leafNode.Activo && (!isUpdate || currentLeafId != leafId))
            {
                throw new InvalidOperationException($"El activo '{leafNode.Descripcion}' está inactivo y no puede utilizarse en nuevas OT.");
            }
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
