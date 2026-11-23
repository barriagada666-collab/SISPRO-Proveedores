using Sispro.Core.Dtos;
using Sispro.Core.Interfaces;

namespace Sispro.Services;

/// <summary>
/// Servicio de consultas, analítica y exploración de los datos industriales importados
/// desde BD MANTENCION.mdb y PERSONAL_MANTENCION.mdb.
/// </summary>
public sealed class MaintenanceService(IMaintenanceRepository maintenance)
{
    public DashboardAnalytics GetDashboardAnalytics() => maintenance.GetDashboardAnalytics();

    public IReadOnlyList<string> GetTradeNames() => maintenance.GetTradeNames();

    public IReadOnlyList<string> GetWorkOrderStatuses() => maintenance.GetWorkOrderStatuses();

    public IReadOnlyList<int> GetWorkOrderYears() => maintenance.GetWorkOrderYears();

    public IReadOnlyList<MantWorkOrderRow> SearchInternalWorkOrders(UserSession session, MantWorkOrderFilter filter)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar OTs de Mantención");
        string? dateErr = Sispro.Core.Validation.DomainValidator.ValidateDateRange(filter.FechaInicio, filter.FechaFin);
        if (dateErr is not null)
        {
            throw new ArgumentException(dateErr);
        }

        return maintenance.SearchInternalWorkOrders(filter);
    }

    public MantWorkOrderDetail? GetInternalWorkOrderDetail(UserSession session, int id)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar detalle de OT de Mantención");
        return maintenance.GetInternalWorkOrderDetail(id);
    }

    public IReadOnlyList<MantPreventivePlanRow> SearchPreventivePlans(UserSession session, string? search = null, string? estado = null)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar planes de mantención preventiva");
        return maintenance.SearchPreventivePlans(search, estado);
    }

    public IReadOnlyList<MantPreventiveCycleRow> GetPreventiveCycles(UserSession session, int idMp)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar ciclos MP");
        return maintenance.GetPreventiveCycles(idMp);
    }

    public IReadOnlyList<MantPreventiveTaskRow> GetPreventiveTasks(UserSession session, int idMp, int? idCiclo = null, int maxRows = 300)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar tareas MP");
        return maintenance.GetPreventiveTasks(idMp, idCiclo, maxRows);
    }

    public IReadOnlyList<MantSupplyRow> SearchSupplies(UserSession session, string? search = null, int maxRows = 500)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar insumos SAP");
        return maintenance.SearchSupplies(search, maxRows);
    }

    public IReadOnlyList<MantPersonnelRow> SearchPersonnel(UserSession session, string? search = null, string? oficio = null)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar personal técnico");
        return maintenance.SearchPersonnel(search, oficio);
    }

    public IReadOnlyList<MantHrPersonnelRow> GetHrPersonnel(UserSession session, string? search = null)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar fichas RRHH");
        return maintenance.GetHrPersonnel(search);
    }

    public IReadOnlyList<MantExternalOrderRow> GetLegacyExternalOrders(UserSession session)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar OTs externas históricas");
        return maintenance.GetLegacyExternalOrders();
    }

    public IReadOnlyList<MantCatalogItem> GetFailureMotives(UserSession session)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar motivos de falla");
        return maintenance.GetFailureMotives();
    }

    public IReadOnlyList<MantCatalogItem> GetTradesCatalog(UserSession session)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar catálogo de oficios");
        return maintenance.GetTradesCatalog();
    }

    public IReadOnlyList<MantCatalogItem> GetChecklistsSummary(UserSession session)
    {
        AuthService.EnsurePermission(session, p => p.MenuOrdenesTrabajo || p.MenuMantenedor, "Consultar checklist de mantención");
        return maintenance.GetChecklistsSummary();
    }
}
