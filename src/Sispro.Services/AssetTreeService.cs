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
    SisproSyncHub? syncHub = null,
    IWorkOrderRepository? workOrders = null,
    IProviderRepository? providers = null)
{
    public bool IsAdministrator(UserSession? session) => AuthService.IsAdministrator(session, users);

    private void EnsureAdministrator(UserSession? session, string actionDescription) =>
        AuthService.EnsureAdministrator(session, users, audit, "Árbol de Activos", actionDescription);

    public MaintainerDependencyReport InspectDependencies(UserSession session, IReadOnlyCollection<int> assetIds)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor || p.MenuOrdenesTrabajo, "Consultar dependencias del árbol de activos");
        List<int> normalized = assetIds?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            return new MaintainerDependencyReport { EntityKind = MaintainerEntityKind.Activo };
        }

        List<string> labels = [];
        HashSet<int> subtreeIds = [.. normalized];
        int childCount = 0;

        foreach (int id in normalized)
        {
            AssetNode? node = tree.GetById(id);
            if (node is not null)
            {
                labels.Add(node.Descripcion);
            }

            foreach (int descId in tree.GetDescendantIds(id))
            {
                if (subtreeIds.Add(descId))
                {
                    childCount++;
                }
            }
        }

        IReadOnlyList<RelatedWorkOrderInfo> related = tree.GetRelatedWorkOrders(subtreeIds.ToList());
        return new MaintainerDependencyReport
        {
            EntityKind = MaintainerEntityKind.Activo,
            SelectedIds = normalized,
            SelectedLabels = labels,
            RelatedWorkOrders = related,
            DependentChildNodesCount = childCount
        };
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

        (AssetNode newParent, int destDepth) = ValidateMoveTarget(node, newParentId);

        tree.MoveSubtree(idActivo, newParentId, destDepth + 1);
        audit.Log(
            session.Usuario.AliasUsuario,
            "Árbol de Activos",
            "MOVER_ACTIVO",
            $"Nodo '{node.Descripcion}' (ID #{idActivo}) movido bajo '{newParent.Descripcion}' (ID #{newParentId}, nuevo nivel base {destDepth + 1}).");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "MOVER_ACTIVO");
    }

    public MaintainerBatchResult SetActiveBatch(
        UserSession session,
        IReadOnlyCollection<int> assetIds,
        bool active,
        bool includeDescendants = true)
    {
        EnsureAdministrator(session, active ? "Activar nodos del árbol" : "Desactivar nodos del árbol");
        MaintainerBatchResult result = new()
        {
            ActionName = active ? "Activar nodos del árbol" : "Desactivar nodos del árbol"
        };

        List<(int Id, int Depth, AssetNode Node)> items = [];
        foreach (int id in assetIds?.Where(x => x > 0).Distinct() ?? [])
        {
            AssetNode? node = tree.GetById(id);
            if (node is null)
            {
                result.FailedOrSkipped.Add(($"Nodo #{id}", "El nodo no existe."));
                continue;
            }

            items.Add((id, tree.GetDepth(id), node));
        }

        // Activar de arriba hacia abajo; desactivar orden estable
        IEnumerable<(int Id, int Depth, AssetNode Node)> ordered = active
            ? items.OrderBy(i => i.Depth)
            : items.OrderByDescending(i => i.Depth);

        foreach ((int id, int _, AssetNode node) in ordered)
        {
            AssetNode current = tree.GetById(id)!;
            if (!current.ParentId.HasValue && !active)
            {
                result.FailedOrSkipped.Add((current.Descripcion, "No es posible desactivar el nodo raíz de la empresa."));
                continue;
            }

            if (active && current.ParentId.HasValue)
            {
                AssetNode? parent = tree.GetById(current.ParentId.Value);
                if (parent is not null && !parent.Activo)
                {
                    result.FailedOrSkipped.Add((current.Descripcion, $"Su nodo padre '{parent.Descripcion}' se encuentra inactivo."));
                    continue;
                }
            }

            List<int> targetIds = !active || includeDescendants
                ? [id, .. tree.GetDescendantIds(id)]
                : [id];

            tree.SetActiveMany(targetIds, active);
            result.Succeeded.Add(current.Descripcion);
        }

        if (result.Succeeded.Count > 0)
        {
            string action = active ? "ACTIVAR_ACTIVOS_LOTE" : "DESACTIVAR_ACTIVOS_LOTE";
            audit.Log(session.Usuario.AliasUsuario, "Árbol de Activos", action,
                $"{result.Succeeded.Count} nodo(s) {(active ? "activados" : "desactivados")}; {result.FailedOrSkipped.Count} omitido(s).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, action);
        }

        return result;
    }

    public MaintainerBatchResult DeleteNodesBatch(UserSession session, IReadOnlyCollection<int> assetIds)
    {
        EnsureAdministrator(session, "Eliminar nodos del árbol de activos");
        MaintainerBatchResult result = new() { ActionName = "Eliminar nodos del árbol" };

        List<(int Id, int Depth, AssetNode Node)> ordered = [];
        foreach (int id in assetIds?.Where(x => x > 0).Distinct() ?? [])
        {
            AssetNode? node = tree.GetById(id);
            if (node is null)
            {
                result.FailedOrSkipped.Add(($"Nodo #{id}", "El nodo no existe."));
                continue;
            }

            ordered.Add((id, tree.GetDepth(id), node));
        }

        // Eliminar primero los nodos hoja más profundos para permitir borrar ramas completas seleccionadas
        foreach ((int id, int _, AssetNode node) in ordered.OrderByDescending(i => i.Depth))
        {
            if (!node.ParentId.HasValue)
            {
                result.FailedOrSkipped.Add((node.Descripcion, "No es posible eliminar el nodo raíz de la empresa."));
                continue;
            }

            int childCount = tree.CountChildren(id, includeInactive: true);
            if (childCount > 0)
            {
                result.FailedOrSkipped.Add((node.Descripcion, $"Posee {childCount} subnodo(s) activo(s)/inactivo(s). Muévalos o elimínelos primero."));
                continue;
            }

            int otCount = tree.CountWorkOrderReferences(id);
            if (otCount > 0)
            {
                result.FailedOrSkipped.Add((node.Descripcion, $"Posee {otCount} OT(s) asociada(s). Reasigne las OTs o desactive el nodo en su lugar."));
                continue;
            }

            tree.Delete(id);
            result.Succeeded.Add(node.Descripcion);
        }

        if (result.Succeeded.Count > 0)
        {
            audit.Log(session.Usuario.AliasUsuario, "Árbol de Activos", "ELIMINAR_ACTIVOS_LOTE",
                $"{result.Succeeded.Count} nodo(s) eliminado(s); {result.FailedOrSkipped.Count} omitido(s).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "ELIMINAR_ACTIVOS_LOTE");
        }

        return result;
    }

    public int ReassignWorkOrdersNode(
        UserSession session,
        IReadOnlyCollection<int> sourceAssetIds,
        int targetAssetId,
        bool includeDescendants = true)
    {
        EnsureAdministrator(session, "Reasignar activo en órdenes de trabajo");
        AssetNode target = tree.GetById(targetAssetId)
            ?? throw new InvalidOperationException("El nodo destino seleccionado no existe.");

        if (!target.Activo)
        {
            throw new InvalidOperationException($"El nodo destino '{target.Descripcion}' está inactivo. Seleccione un activo válido y activo.");
        }

        IReadOnlyList<AssetNode> targetPath = tree.GetPathToRoot(targetAssetId);
        if (targetPath.Count < 2)
        {
            throw new ArgumentException("Debe seleccionar un nodo de nivel 2 o superior (planta, equipo, máquina o componente) para asociar a las OTs.");
        }

        HashSet<int> expandedSources = [];
        foreach (int id in sourceAssetIds?.Where(x => x > 0) ?? [])
        {
            if (id != targetAssetId)
            {
                expandedSources.Add(id);
            }

            if (includeDescendants)
            {
                foreach (int desc in tree.GetDescendantIds(id))
                {
                    if (desc != targetAssetId)
                    {
                        expandedSources.Add(desc);
                    }
                }
            }
        }

        if (expandedSources.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un nodo origen distinto al nodo destino.");
        }

        List<int> targetPathIds = targetPath.Select(n => n.IdActivo).ToList();
        string targetBreadcrumb = string.Join(" \\ ", targetPath.Select(n => n.Descripcion));
        int reassigned = tree.ReassignWorkOrdersNode(expandedSources.ToList(), targetPathIds, targetBreadcrumb);
        audit.Log(session.Usuario.AliasUsuario, "Árbol de Activos", "REASIGNAR_ACTIVO_OTS",
            $"{reassigned} OT(s) reasignada(s) al nodo '{target.Descripcion}' (ID #{targetAssetId}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Catalogs | DataChangeKind.Audit, "REASIGNAR_ACTIVO_OTS");
        return reassigned;
    }

    public int ReassignWorkOrdersProvider(
        UserSession session,
        IReadOnlyCollection<int> assetIds,
        int targetProviderId,
        bool includeDescendants = true)
    {
        EnsureAdministrator(session, "Reasignar proveedor en OTs asociadas a activos");
        if (providers is null)
        {
            throw new InvalidOperationException("El repositorio de proveedores no está disponible.");
        }

        Provider targetProvider = providers.GetById(targetProviderId)
            ?? throw new InvalidOperationException("El proveedor destino seleccionado no existe.");
        if (!targetProvider.Activo)
        {
            throw new InvalidOperationException($"El proveedor '{targetProvider.Nombre}' está inactivo.");
        }

        HashSet<int> expanded = [];
        foreach (int id in assetIds?.Where(x => x > 0) ?? [])
        {
            expanded.Add(id);
            if (includeDescendants)
            {
                foreach (int desc in tree.GetDescendantIds(id))
                {
                    expanded.Add(desc);
                }
            }
        }

        if (expanded.Count == 0)
        {
            return 0;
        }

        int reassigned = tree.ReassignWorkOrdersProviderForAssets(expanded.ToList(), targetProviderId);
        audit.Log(session.Usuario.AliasUsuario, "Árbol de Activos", "REASIGNAR_PROVEEDOR_ACTIVO_OTS",
            $"{reassigned} OT(s) de los activos seleccionados reasignada(s) al proveedor '{targetProvider.Nombre}'.");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Catalogs | DataChangeKind.Audit, "REASIGNAR_PROVEEDOR_ACTIVO_OTS");
        return reassigned;
    }

    public MaintainerBatchResult ReassignChildrenAndMove(
        UserSession session,
        IReadOnlyCollection<int> sourceParentIds,
        int targetParentId)
    {
        EnsureAdministrator(session, "Reasignar subnodos de activo a otro padre");
        MaintainerBatchResult result = new() { ActionName = "Reasignar subnodos a otro nodo padre" };

        AssetNode? targetParent = tree.GetById(targetParentId);
        if (targetParent is null)
        {
            throw new InvalidOperationException("El nodo padre destino no existe.");
        }

        if (!targetParent.Activo)
        {
            throw new InvalidOperationException($"El nodo padre destino '{targetParent.Descripcion}' está inactivo.");
        }

        foreach (int parentId in sourceParentIds?.Where(x => x > 0 && x != targetParentId).Distinct() ?? [])
        {
            IReadOnlyList<AssetNode> children = tree.GetChildren(parentId, includeInactive: true);
            foreach (AssetNode child in children)
            {
                try
                {
                    (_, int destDepth) = ValidateMoveTarget(child, targetParentId);
                    tree.MoveSubtree(child.IdActivo, targetParentId, destDepth + 1);
                    result.Succeeded.Add(child.Descripcion);
                }
                catch (Exception ex)
                {
                    result.FailedOrSkipped.Add((child.Descripcion, ex.Message));
                }
            }
        }

        if (result.Succeeded.Count > 0)
        {
            audit.Log(session.Usuario.AliasUsuario, "Árbol de Activos", "REASIGNAR_SUBNODOS_ACTIVO",
                $"{result.Succeeded.Count} subnodo(s) reasignado(s) bajo '{targetParent.Descripcion}' (ID #{targetParentId}).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "REASIGNAR_SUBNODOS_ACTIVO");
        }

        return result;
    }

    public int DeleteRelatedWorkOrdersExplicitly(UserSession session, IReadOnlyCollection<int> idOts, string contextDescription)
    {
        EnsureAdministrator(session, "Eliminar explícitamente OTs asociadas al árbol de activos");
        if (workOrders is null)
        {
            throw new InvalidOperationException("El repositorio de órdenes de trabajo no está disponible.");
        }

        List<int> normalized = idOts?.Where(x => x > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            return 0;
        }

        int deleted = workOrders.DeleteMany(normalized);
        audit.Log(session.Usuario.AliasUsuario, "Árbol de Activos", "ELIMINAR_OTS_ACTIVO_EXPLICITO",
            $"{deleted} OT(s) eliminada(s) explícitamente desde Árbol de Activos ({contextDescription}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "ELIMINAR_OTS_ACTIVO_EXPLICITO");
        return deleted;
    }

    private (AssetNode NewParent, int DestDepth) ValidateMoveTarget(AssetNode node, int newParentId)
    {
        if (!node.ParentId.HasValue)
        {
            throw new InvalidOperationException("No es posible mover el nodo raíz de la empresa.");
        }

        if (node.IdActivo == newParentId)
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

        IReadOnlyList<int> descendants = tree.GetDescendantIds(node.IdActivo);
        if (descendants.Contains(newParentId))
        {
            throw new InvalidOperationException("No es posible mover un nodo dentro de uno de sus propios subnodos.");
        }

        int destDepth = tree.GetDepth(newParentId);
        int subtreeHeight = tree.GetSubtreeHeight(node.IdActivo);
        int resultingMaxDepth = destDepth + 1 + subtreeHeight;
        if (resultingMaxDepth > 10)
        {
            throw new InvalidOperationException(
                $"No es posible mover '{node.Descripcion}': la profundidad resultante ({resultingMaxDepth}) superaría el máximo permitido de 10 niveles.");
        }

        IReadOnlyList<AssetNode> destSiblings = tree.GetChildren(newParentId, includeInactive: true);
        if (destSiblings.Any(s => string.Equals(s.Descripcion.Trim(), node.Descripcion.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Ya existe un nodo llamado '{node.Descripcion}' bajo '{newParent.Descripcion}'.");
        }

        return (newParent, destDepth);
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
