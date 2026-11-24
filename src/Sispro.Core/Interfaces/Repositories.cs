using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Core.Interfaces;

public interface IUserRepository
{
    User? GetByAlias(string alias);
    User? GetById(string idUsuario);
    User? GetByCodigoId(int codigoId);
    IReadOnlyList<User> GetAll(string? search = null);
    IReadOnlyList<User> Search(UserSearchFilter filter);
    int Create(User user);
    void Update(User user, bool updatePassword, int? expectedRowVersion = null);
    void UpdatePasswordHash(string idUsuario, string newHash, bool requirePasswordChange = false);
    void UpdateLastLogin(string idUsuario);
    void SetActive(string idUsuario, bool active);
    void SetGroup(string idUsuario, int? idGrupo);
    void SetRole(string idUsuario, UserRoleLevel newRole, UserPermissions normalizedPermissions);
    int CountActiveAdmins();
    int CountWithoutGroup();
    IReadOnlyList<ApproverDirectoryEntry> GetApprovers();
}

public interface IGroupRepository
{
    IReadOnlyList<UserGroup> GetAll(bool includeInactive = true, string? search = null);
    UserGroup? GetById(int idGrupo);
    UserGroup? GetByName(string nombre);
    int Create(UserGroup group);
    void Update(UserGroup group, int? expectedRowVersion = null);
    void SetActive(int idGrupo, bool active);
    int CountAssignedUsers(int idGrupo);
    bool HasDependencies(int idGrupo);
    void Delete(int idGrupo);
    void RecordMovement(GroupMovementEntry movement);
    IReadOnlyList<GroupMovementEntry> GetMovements(GroupMovementFilter? filter = null);
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
    int SetActiveMany(IReadOnlyCollection<int> ids, bool active);
    int GetTotalCount();
    int CountChildren(int idActivo, bool includeInactive = true);
    int CountWorkOrderReferences(int idActivo);
    IReadOnlyList<int> GetDescendantIds(int idActivo);
    int GetSubtreeHeight(int idActivo);
    int GetDepth(int idActivo);
    void Delete(int idActivo);
    void MoveSubtree(int idActivo, int newParentId, int newLevel);
    int CountLevelAnomalies();
    int NormalizeLevels();
    IReadOnlyList<AssetTreeExportRow> GetSubtreeRows(int rootId, bool includeInactive = true);
}

public interface IWorkOrderRepository
{
    WorkOrder? GetByNumber(int numeroOt);
    WorkOrder? GetById(int idOt);
    int GetNextWorkOrderNumber();
    int Create(WorkOrder workOrder);
    void Update(WorkOrder workOrder);
    void Delete(int idOt);
    IReadOnlyList<WorkOrderSearchRow> Search(WorkOrderSearchFilter filter);
    void SetApproval(int idOt, string idUsuario, ApprovalType type, bool approved);
    IReadOnlyList<WorkOrderApproval> GetApprovals(int idOt);
    DashboardAnalytics GetDashboardAnalytics();
}

public interface IAuditRepository
{
    void Log(string usuario, string modulo, string accion, string detalle = "");
    IReadOnlyList<AuditEntry> GetRecent(int limit = 200);
    IReadOnlyList<AuditEntry> Search(AuditSearchFilter filter);
    DashboardMetrics GetDashboardMetrics();
}
