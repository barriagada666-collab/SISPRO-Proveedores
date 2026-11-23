using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Core.Interfaces;

public interface IUserRepository
{
    User? GetByAlias(string alias);
    User? GetById(string idUsuario);
    IReadOnlyList<User> GetAll(string? search = null);
    void Create(User user);
    void Update(User user, bool updatePassword);
    void UpdatePasswordHash(string idUsuario, string newHash);
    void UpdateLastLogin(string idUsuario);
    void Delete(string idUsuario);
    int CountActiveAdmins();
}

public interface IProviderRepository
{
    IReadOnlyList<Provider> GetAll(bool includeInactive = false, string? search = null);
    Provider? GetById(int idProv);
    Provider? GetByName(string name);
    int Create(Provider provider);
    void Update(Provider provider);
    bool HasWorkOrders(int idProv);
    void Delete(int idProv);
}

public interface IRequesterRepository
{
    IReadOnlyList<Requester> GetAll(bool includeInactive = false, string? search = null);
    Requester? GetById(int idSol);
    Requester? GetByAlias(string alias);
    int Create(Requester requester);
    void Update(Requester requester);
    void EnsureFromUser(User user);
    bool HasWorkOrders(int idSol);
    void Delete(int idSol);
}

public interface IAssetTreeRepository
{
    AssetNode? GetById(int idActivo);
    IReadOnlyList<AssetNode> GetChildren(int? parentId, bool includeInactive = false);
    IReadOnlyList<AssetNode> Search(string query, bool includeInactive = false);
    IReadOnlyList<AssetNode> GetPathToRoot(int idActivo);
    int Create(AssetNode node);
    void Update(AssetNode node);
    void SetActive(int idActivo, bool active);
    int GetTotalCount();
}

public interface IWorkOrderRepository
{
    WorkOrder? GetByNumber(int numeroOt);
    WorkOrder? GetById(int idOt);
    int GetNextWorkOrderNumber();
    int Create(WorkOrder workOrder);
    void Update(WorkOrder workOrder);
    void Delete(int idOt);
    bool HasInvoices(int idOt);
    IReadOnlyList<WorkOrderSearchRow> Search(WorkOrderSearchFilter filter);
    void SetApproval(int idOt, string idUsuario, ApprovalType type, bool approved);
    IReadOnlyList<WorkOrderApproval> GetApprovals(int idOt);
}

public interface IInvoiceRepository
{
    Invoice? GetById(int idFactura);
    Invoice? GetByNumberAndProvider(int numeroDocumento, int idProv);
    bool IsDetailAlreadyInvoiced(int idDet);
    int Create(Invoice invoice);
    void Delete(int idFactura);
    IReadOnlyList<InvoiceSearchRow> Search(InvoiceSearchFilter filter);
}

public interface IAuditRepository
{
    void Log(string usuario, string modulo, string accion, string detalle = "");
    IReadOnlyList<AuditEntry> GetRecent(int limit = 200);
    DashboardMetrics GetDashboardMetrics();
}
