using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;

namespace Sispro.Services;

/// <summary>
/// Servicio de administración completa (CRUD, reubicación, activación en cascada y normalización)
/// del Árbol de Activos de 10 niveles. Toda operación de escritura exige perfil Administrador
/// verificado contra la base de datos y registra trazabilidad en Auditoría + SyncHub.
/// </summary>
public sealed class AssetTreeService(
    IAssetTreeRepository tree,
    IUserRepository users,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null)
{
    public bool IsAdministrator(UserSession? session)
    {
        if (session is null || !session.Usuario.Activo || session.Usuario.Nivel != UserRoleLevel.Administrador)
        {
            return false;
        }

        User? dbUser = users.GetById(session.Usuario.IdUsuario);
        return dbUser is not null && dbUser.Activo && dbUser.Nivel == UserRoleLevel.Administrador;
    }

    private void EnsureAdministrator(UserSession? session, string actionDescription)
    {
        if (IsAdministrator(session))
        {
            return;
        }

        audit.Log(
            session?.Usuario.AliasUsuario ?? "ANONIMO",
            "Árbol de Activos",
            "ACCESO_NO_AUTORIZADO",
            $"Intento no autorizado en editor del árbol: {actionDescription}.");

        throw new UnauthorizedAccessException($"Acceso denegado: la acción '{actionDescription}' es exclusiva del perfil Administrador.");
    }

    public AssetNodeInfo GetNodeInfo(int idActivo)
    {
        AssetNode node = tree.GetById(idActivo)
            ?? throw new InvalidOperationException("El nodo seleccionado no existe.");

        IReadOnlyList<AssetNode> pathNodes = tree.GetPathToRoot(idActivo);
        return new AssetNodeInfo
        {
            Node = node,
            Depth = Math.Max(1, pathNodes.Count),
            ActiveChildren = tree.CountChildren(idActivo, includeInactive: false),
            TotalChildren = tree.CountChildren(idActivo, includeInactive: true),
            WorkOrderReferences = tree.CountWorkOrderReferences(idActivo),
            Path = pathNodes.Count > 0 ? string.Join(" \\ ", pathNodes.Select(n => n.Descripcion)) : node.Descripcion
        };
    }

    public int CountLevelAnomalies() => tree.CountLevelAnomalies();

    public IReadOnlyList<AssetTreeExportRow> GetSubtreeForExport(int rootId, bool includeInactive = true) =>
        tree.GetSubtreeRows(rootId, includeInactive);

    public int AddChild(UserSession session, int parentId, string descripcion, string categoria = "") =>
        AddChild(session, parentId, new AssetNode
        {
            Descripcion = descripcion,
            Categoria = categoria
        });

    public int AddChild(UserSession session, int parentId, AssetNode draft)
    {
        EnsureAdministrator(session, "Crear nodo en el árbol de activos");

        string desc = ValidateAndTrimDescription(draft.Descripcion);
        AssetNode parent = tree.GetById(parentId)
            ?? throw new InvalidOperationException("El nodo padre seleccionado no existe.");

        if (!parent.Activo)
        {
            throw new InvalidOperationException($"El nodo padre '{parent.Descripcion}' está inactivo. Actívelo primero antes de agregar subnodos.");
        }

        int parentDepth = tree.GetDepth(parentId);
        if (parentDepth >= 10)
        {
            throw new InvalidOperationException("El árbol de activos admite un máximo de 10 niveles de profundidad.");
        }

        IReadOnlyList<AssetNode> siblings = tree.GetChildren(parentId, includeInactive: true);
        if (siblings.Any(s => string.Equals(s.Descripcion.Trim(), desc, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Ya existe un nodo llamado '{desc}' bajo '{parent.Descripcion}'.");
        }

        string categoria = AssetNodeTypes.Normalize(draft.Categoria);
        if (string.IsNullOrEmpty(categoria))
        {
            categoria = AssetNodeTypes.SuggestChildType(parent.Categoria);
        }

        AssetNode node = new()
        {
            ParentId = parentId,
            Nivel = parentDepth + 1,
            Descripcion = desc,
            Activo = true,
            Categoria = categoria,
            CodSap = draft.CodSap.Trim(),
            Fabricante = draft.Fabricante.Trim(),
            Serie = draft.Serie.Trim(),
            Modelo = draft.Modelo.Trim(),
            TipoEquipo = draft.TipoEquipo.Trim(),
            EstadoOperativo = draft.EstadoOperativo.Trim(),
            Prioridad = Math.Clamp(draft.Prioridad, 0, 10),
            Observacion = draft.Observacion.Trim()
        };

        int id = tree.Create(node);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            "CREAR_ACTIVO",
            $"Nodo '{node.Descripcion}' (ID #{id}, Nivel {node.Nivel}, Tipo '{AssetNodeTypes.GetLabel(node.Categoria)}') creado bajo '{parent.Descripcion}'.");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "CREAR_ACTIVO");
        return id;
    }

    public void UpdateNode(UserSession session, AssetNode updated)
    {
        EnsureAdministrator(session, "Editar nodo del árbol de activos");

        string desc = ValidateAndTrimDescription(updated.Descripcion);
        AssetNode existing = tree.GetById(updated.IdActivo)
            ?? throw new InvalidOperationException("El nodo seleccionado no existe.");

        IReadOnlyList<AssetNode> siblings = tree.GetChildren(existing.ParentId, includeInactive: true);
        if (siblings.Any(s => s.IdActivo != existing.IdActivo &&
                              string.Equals(s.Descripcion.Trim(), desc, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Ya existe otro nodo hermano llamado '{desc}' en el mismo nivel.");
        }

        existing.Descripcion = desc;
        existing.Categoria = AssetNodeTypes.Normalize(updated.Categoria);
        existing.CodSap = updated.CodSap.Trim();
        existing.Fabricante = updated.Fabricante.Trim();
        existing.Modelo = updated.Modelo.Trim();
        existing.Serie = updated.Serie.Trim();
        existing.TipoEquipo = updated.TipoEquipo.Trim();
        existing.EstadoOperativo = updated.EstadoOperativo.Trim();
        existing.Prioridad = Math.Clamp(updated.Prioridad, 0, 10);
        existing.Observacion = updated.Observacion.Trim();

        tree.Update(existing);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            "MODIFICAR_ACTIVO",
            $"Nodo ID #{existing.IdActivo} actualizado ('{existing.Descripcion}', Tipo '{AssetNodeTypes.GetLabel(existing.Categoria)}', SAP '{existing.CodSap}').");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "MODIFICAR_ACTIVO");
    }

    public int SetActive(UserSession session, int idActivo, bool active, bool includeDescendants = true)
    {
        EnsureAdministrator(session, active ? "Activar nodo del árbol" : "Desactivar nodo del árbol");

        AssetNode node = tree.GetById(idActivo)
            ?? throw new InvalidOperationException("El nodo seleccionado no existe.");

        if (!node.ParentId.HasValue && !active)
        {
            throw new InvalidOperationException("No es posible desactivar el nodo raíz de la empresa.");
        }

        if (active && node.ParentId.HasValue)
        {
            AssetNode? parent = tree.GetById(node.ParentId.Value);
            if (parent is not null && !parent.Activo)
            {
                throw new InvalidOperationException($"Active primero el nodo padre '{parent.Descripcion}'.");
            }
        }

        List<int> targetIds = !active || includeDescendants
            ? [idActivo, .. tree.GetDescendantIds(idActivo)]
            : [idActivo];

        int affected = tree.SetActiveMany(targetIds, active);
        string action = active ? "ACTIVAR_ACTIVO" : "DESACTIVAR_ACTIVO";
        string verb = active ? "activado" : "desactivado";
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            action,
            $"Nodo '{node.Descripcion}' (ID #{idActivo}) {verb} ({affected} nodo(s) afectados).");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, action);
        return affected;
    }

    public void MoveNode(UserSession session, int idActivo, int newParentId)
    {
        EnsureAdministrator(session, "Mover nodo en el árbol de activos");

        AssetNode node = tree.GetById(idActivo)
            ?? throw new InvalidOperationException("El nodo seleccionado no existe.");

        if (!node.ParentId.HasValue)
        {
            throw new InvalidOperationException("No es posible mover el nodo raíz de la empresa.");
        }

        if (idActivo == newParentId)
        {
            throw new InvalidOperationException("No es posible mover un nodo dentro de sí mismo.");
        }

        if (node.ParentId == newParentId)
        {
            throw new InvalidOperationException("El nodo ya se encuentra bajo ese nodo padre.");
        }

        AssetNode newParent = tree.GetById(newParentId)
            ?? throw new InvalidOperationException("El nodo padre de destino no existe.");

        if (!newParent.Activo)
        {
            throw new InvalidOperationException($"El nodo padre de destino '{newParent.Descripcion}' está inactivo.");
        }

        IReadOnlyList<int> descendants = tree.GetDescendantIds(idActivo);
        if (descendants.Contains(newParentId))
        {
            throw new InvalidOperationException("No es posible mover un nodo dentro de uno de sus propios subnodos.");
        }

        int destDepth = tree.GetDepth(newParentId);
        int subtreeHeight = tree.GetSubtreeHeight(idActivo);
        int resultingMaxDepth = destDepth + 1 + subtreeHeight;
        if (resultingMaxDepth > 10)
        {
            throw new InvalidOperationException($"No es posible mover '{node.Descripcion}': la profundidad resultante ({resultingMaxDepth}) superaría el máximo permitido de 10 niveles.");
        }

        IReadOnlyList<AssetNode> destSiblings = tree.GetChildren(newParentId, includeInactive: true);
        if (destSiblings.Any(s => string.Equals(s.Descripcion.Trim(), node.Descripcion.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Ya existe un nodo llamado '{node.Descripcion}' bajo '{newParent.Descripcion}'.");
        }

        tree.MoveSubtree(idActivo, newParentId, destDepth + 1);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            "MOVER_ACTIVO",
            $"Nodo '{node.Descripcion}' (ID #{idActivo}) movido bajo '{newParent.Descripcion}' (ID #{newParentId}, nuevo nivel base {destDepth + 1}).");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "MOVER_ACTIVO");
    }

    public void DeleteNode(UserSession session, int idActivo)
    {
        EnsureAdministrator(session, "Eliminar nodo del árbol de activos");

        AssetNode node = tree.GetById(idActivo)
            ?? throw new InvalidOperationException("El nodo seleccionado no existe.");

        if (!node.ParentId.HasValue)
        {
            throw new InvalidOperationException("No es posible eliminar el nodo raíz de la empresa.");
        }

        int children = tree.CountChildren(idActivo, includeInactive: true);
        if (children > 0)
        {
            throw new InvalidOperationException($"El nodo tiene {children} subnodo(s). Elimínelos o muévalos primero.");
        }

        int refs = tree.CountWorkOrderReferences(idActivo);
        if (refs > 0)
        {
            throw new InvalidOperationException($"El nodo está asociado a {refs} OT(s) históricas; desactívelo en lugar de eliminarlo.");
        }

        tree.Delete(idActivo);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            "ELIMINAR_ACTIVO",
            $"Nodo '{node.Descripcion}' (ID #{idActivo}) eliminado definitivamente.");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "ELIMINAR_ACTIVO");
    }

    public int NormalizeLevels(UserSession session)
    {
        EnsureAdministrator(session, "Normalizar niveles jerárquicos del árbol");

        int updated = tree.NormalizeLevels();
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            "NORMALIZAR_NIVELES_ARBOL",
            $"Niveles del árbol recalculados desde la raíz ({updated} nodo(s) corregidos).");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "NORMALIZAR_NIVELES_ARBOL");
        return updated;
    }

    private static string ValidateAndTrimDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new ArgumentException("Debe ingresar la descripción del nodo.");
        }

        string trimmed = raw.Trim();
        if (trimmed.Length > 120)
        {
            throw new ArgumentException("La descripción del nodo no puede superar los 120 caracteres.");
        }

        return trimmed;
    }
}
