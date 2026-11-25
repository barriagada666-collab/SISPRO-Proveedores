using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class MaintainerService(
    IProviderRepository providers,
    IRequesterRepository requesters,
    IAssetTreeRepository assetTree,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null,
    IWorkOrderRepository? workOrders = null,
    IUserRepository? users = null)
{
    public bool IsAdministrator(UserSession? session) => AuthService.IsAdministrator(session, users);

    public MaintainerDependencyReport InspectDependencies(
        UserSession session,
        MaintainerEntityKind kind,
        IReadOnlyCollection<int> ids)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor || p.MenuUsuarios, "Consultar OT asociadas en mantenedores");
        List<int> normalized = ids?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            return new MaintainerDependencyReport { EntityKind = kind };
        }

        List<string> labels = [];
        IReadOnlyList<RelatedWorkOrderInfo> related = [];
        int descendantCount = 0;

        switch (kind)
        {
            case MaintainerEntityKind.Proveedor:
                foreach (int id in normalized)
                {
                    Provider? p = providers.GetById(id);
                    if (p is not null)
                    {
                        labels.Add(p.Nombre);
                    }
                }

                related = providers.GetRelatedWorkOrders(normalized);
                break;

            case MaintainerEntityKind.Solicitante:
                foreach (int id in normalized)
                {
                    Requester? r = requesters.GetById(id);
                    if (r is not null)
                    {
                        labels.Add($"{r.Alias} — {r.NombreCompleto}");
                    }
                }

                related = requesters.GetRelatedWorkOrders(normalized, "Solicitante asignado en OT");
                break;

            case MaintainerEntityKind.Firma:
                foreach (int id in normalized)
                {
                    Requester? r = requesters.GetById(id);
                    if (r is not null)
                    {
                        string sigInfo = r.TieneFirma ? r.FirmaNombre : "Sin firma adjunta";
                        labels.Add($"{r.Alias} — {r.NombreCompleto} ({sigInfo})");
                    }
                }

                related = requesters.GetRelatedWorkOrders(normalized, "Firma técnica (Solicitante) en OT");
                break;

            case MaintainerEntityKind.Activo:
                HashSet<int> subtreeIds = [.. normalized];
                foreach (int id in normalized)
                {
                    AssetNode? n = assetTree.GetById(id);
                    if (n is not null)
                    {
                        labels.Add(n.Descripcion);
                    }

                    foreach (int childId in assetTree.GetDescendantIds(id))
                    {
                        if (subtreeIds.Add(childId))
                        {
                            descendantCount++;
                        }
                    }
                }

                related = assetTree.GetRelatedWorkOrders(subtreeIds.ToList());
                break;
        }

        return new MaintainerDependencyReport
        {
            EntityKind = kind,
            SelectedIds = normalized,
            SelectedLabels = labels,
            RelatedWorkOrders = related,
            DependentChildNodesCount = descendantCount
        };
    }

    // --- PROVEEDORES ---
    public IReadOnlyList<Provider> GetProviders(bool includeInactive = false, string? search = null) =>
        providers.GetAll(includeInactive, search);

    public int GetProviderWorkOrderCount(int idProv) => providers.CountWorkOrders(idProv);

    public int GetRequesterWorkOrderCount(int idSol) => requesters.CountWorkOrders(idSol);

    public IReadOnlyDictionary<int, int> GetRequesterWorkOrderCounts() => requesters.CountWorkOrdersByRequester();

    public int SaveProvider(UserSession session, Provider provider)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, "Administrar proveedores");
        if (string.IsNullOrWhiteSpace(provider.Nombre))
        {
            throw new ArgumentException("El nombre del proveedor es obligatorio.");
        }

        if (!DomainValidator.IsValidEmailOrEmpty(provider.Correo))
        {
            throw new ArgumentException("El correo electrónico del proveedor no tiene un formato válido.");
        }

        if (!DomainValidator.IsValidEmailOrEmpty(provider.CorreoContacto))
        {
            throw new ArgumentException("El correo del contacto no tiene un formato válido.");
        }

        Provider? duplicate = providers.GetByName(provider.Nombre);
        if (duplicate is not null && duplicate.IdProv != provider.IdProv)
        {
            throw new InvalidOperationException($"Ya existe un proveedor registrado con el nombre '{provider.Nombre}'.");
        }

        if (provider.IdProv == 0)
        {
            int id = providers.Create(provider);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "CREAR_PROVEEDOR", $"Proveedor '{provider.Nombre}' (ID {id}) creado.");
            syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "CREAR_PROVEEDOR");
            return id;
        }

        providers.Update(provider);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "MODIFICAR_PROVEEDOR", $"Proveedor '{provider.Nombre}' (ID {provider.IdProv}) actualizado.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "MODIFICAR_PROVEEDOR");
        return provider.IdProv;
    }

    public void DeleteProvider(UserSession session, int idProv)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", "Eliminar proveedor");
        Provider existing = providers.GetById(idProv)
            ?? throw new InvalidOperationException("El proveedor seleccionado no existe.");

        if (providers.HasWorkOrders(idProv))
        {
            existing.Activo = false;
            providers.Update(existing);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "DESACTIVAR_PROVEEDOR",
                $"Proveedor '{existing.Nombre}' desactivado (mantiene OTs históricas).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "DESACTIVAR_PROVEEDOR");
            return;
        }

        providers.Delete(idProv);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_PROVEEDOR", $"Proveedor '{existing.Nombre}' eliminado.");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "ELIMINAR_PROVEEDOR");
    }

    public void SetProviderActive(UserSession session, int idProv, bool active)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", active ? "Activar proveedor" : "Desactivar proveedor");
        Provider existing = providers.GetById(idProv)
            ?? throw new InvalidOperationException("El proveedor seleccionado no existe.");

        existing.Activo = active;
        providers.Update(existing);
        string accion = active ? "ACTIVAR_PROVEEDOR" : "DESACTIVAR_PROVEEDOR";
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", accion, $"Proveedor '{existing.Nombre}' {(active ? "activado" : "desactivado")}.");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, accion);
    }

    public MaintainerBatchResult SetProvidersActiveBatch(UserSession session, IReadOnlyCollection<int> ids, bool active) =>
        ApplyActiveBatch(
            session,
            ids,
            active,
            "proveedores",
            active ? "ACTIVAR_PROVEEDORES_LOTE" : "DESACTIVAR_PROVEEDORES_LOTE",
            providers.GetById,
            p => p.Nombre,
            p => p.Activo,
            providers.SetActiveMany);

    public MaintainerBatchResult DeleteProvidersBatch(UserSession session, IReadOnlyCollection<int> ids) =>
        ExecuteIntegrityDeleteBatch(
            session,
            ids,
            "Eliminar proveedores",
            "ELIMINAR_PROVEEDORES_LOTE",
            providers.GetById,
            p => p.Nombre,
            providers.CountWorkOrders,
            providers.Delete);

    public int ReassignProviderWorkOrders(UserSession session, IReadOnlyCollection<int> sourceProviderIds, int targetProviderId)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", "Reasignar proveedor en órdenes de trabajo");
        Provider target = providers.GetById(targetProviderId)
            ?? throw new InvalidOperationException("El proveedor destino seleccionado no existe.");

        if (!target.Activo)
        {
            throw new InvalidOperationException($"El proveedor destino '{target.Nombre}' está inactivo. Seleccione un proveedor activo.");
        }

        List<int> sources = sourceProviderIds?.Where(id => id > 0 && id != targetProviderId).Distinct().ToList() ?? [];
        if (sources.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un proveedor origen distinto al proveedor destino.");
        }

        int reassigned = providers.ReassignWorkOrders(sources, targetProviderId);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "REASIGNAR_PROVEEDOR_OTS",
            $"{reassigned} OT(s) reasignada(s) al proveedor '{target.Nombre}' (ID {targetProviderId}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Catalogs | DataChangeKind.Audit, "REASIGNAR_PROVEEDOR_OTS");
        return reassigned;
    }

    // --- SOLICITANTES Y FIRMAS DIGITALES ---
    public IReadOnlyList<Requester> GetRequesters(bool includeInactive = false, string? search = null) =>
        requesters.GetAll(includeInactive, search);

    public Requester? GetRequesterByAlias(string alias) =>
        string.IsNullOrWhiteSpace(alias) ? null : requesters.GetByAlias(alias);

    public int SaveRequester(UserSession session, Requester requester)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor || p.MenuUsuarios, "Administrar solicitantes");
        if (string.IsNullOrWhiteSpace(requester.Alias))
        {
            throw new ArgumentException("El alias del solicitante es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(requester.Nombre) || string.IsNullOrWhiteSpace(requester.ApellidoPaterno))
        {
            throw new ArgumentException("El nombre y apellido paterno del solicitante son obligatorios.");
        }

        if (!DomainValidator.IsValidEmailOrEmpty(requester.Correo))
        {
            throw new ArgumentException("El correo del solicitante no tiene un formato válido.");
        }

        Requester? duplicate = requesters.GetByAlias(requester.Alias);
        if (duplicate is not null && duplicate.IdSol != requester.IdSol)
        {
            throw new InvalidOperationException($"Ya existe un solicitante con el alias '{requester.Alias}'.");
        }

        if (requester.FirmaBytes is { Length: > 0 } && (requester.FirmaPreviewPng is null || requester.FirmaPreviewPng.Length == 0))
        {
            ProcessedSignature processed = SignatureFileService.ProcessBytes(
                string.IsNullOrWhiteSpace(requester.FirmaNombre) ? "firma.png" : requester.FirmaNombre,
                requester.FirmaBytes);
            requester.FirmaBytes = processed.RawBytes;
            requester.FirmaNombre = processed.FileName;
            requester.FirmaTipo = processed.FileType;
            requester.FirmaPreviewPng = processed.PreviewPngBytes;
        }

        string sigNote = requester.TieneFirma
            ? $" (Firma {requester.FirmaTipo}: {requester.FirmaNombre})"
            : " (Sin firma digital)";

        if (requester.IdSol == 0)
        {
            int id = requesters.Create(requester);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "CREAR_SOLICITANTE", $"Solicitante '{requester.Alias}' creado{sigNote}.");
            syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "CREAR_SOLICITANTE");
            return id;
        }

        requesters.Update(requester);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "MODIFICAR_SOLICITANTE", $"Solicitante '{requester.Alias}' actualizado{sigNote}.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "MODIFICAR_SOLICITANTE");
        return requester.IdSol;
    }

    public void DeleteRequester(UserSession session, int idSol)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", "Eliminar solicitante");
        Requester existing = requesters.GetById(idSol)
            ?? throw new InvalidOperationException("El solicitante seleccionado no existe.");

        if (requesters.HasWorkOrders(idSol))
        {
            existing.Activo = false;
            requesters.Update(existing);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "DESACTIVAR_SOLICITANTE",
                $"Solicitante '{existing.Alias}' desactivado (mantiene OTs históricas).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "DESACTIVAR_SOLICITANTE");
            return;
        }

        requesters.Delete(idSol);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_SOLICITANTE", $"Solicitante '{existing.Alias}' eliminado.");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "ELIMINAR_SOLICITANTE");
    }

    public void SetRequesterActive(UserSession session, int idSol, bool active)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", active ? "Activar solicitante" : "Desactivar solicitante");
        Requester existing = requesters.GetById(idSol)
            ?? throw new InvalidOperationException("El solicitante seleccionado no existe.");

        existing.Activo = active;
        requesters.Update(existing);
        string accion = active ? "ACTIVAR_SOLICITANTE" : "DESACTIVAR_SOLICITANTE";
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", accion, $"Solicitante '{existing.Alias}' {(active ? "activado" : "desactivado")}.");
        syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, accion);
    }

    public MaintainerBatchResult SetRequestersActiveBatch(UserSession session, IReadOnlyCollection<int> ids, bool active) =>
        ApplyActiveBatch(
            session,
            ids,
            active,
            "solicitantes",
            active ? "ACTIVAR_SOLICITANTES_LOTE" : "DESACTIVAR_SOLICITANTES_LOTE",
            requesters.GetById,
            r => $"{r.Alias} ({r.NombreCompleto})",
            r => r.Activo,
            requesters.SetActiveMany);

    public MaintainerBatchResult DeleteRequestersBatch(UserSession session, IReadOnlyCollection<int> ids) =>
        ExecuteIntegrityDeleteBatch(
            session,
            ids,
            "Eliminar solicitantes",
            "ELIMINAR_SOLICITANTES_LOTE",
            requesters.GetById,
            r => $"{r.Alias} ({r.NombreCompleto})",
            requesters.CountWorkOrders,
            requesters.Delete);

    public MaintainerBatchResult ClearSignaturesBatch(UserSession session, IReadOnlyCollection<int> idSols, bool skipWithWorkOrders = true)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", "Eliminar firmas digitales");
        MaintainerBatchResult result = new() { ActionName = "Eliminar firmas digitales" };
        List<int> normalized = idSols?.Where(id => id > 0).Distinct().ToList() ?? [];
        List<int> toClear = [];

        foreach (int id in normalized)
        {
            Requester? req = requesters.GetById(id);
            if (req is null)
            {
                result.FailedOrSkipped.Add(($"Firma #{id}", "El solicitante asociado no existe."));
                continue;
            }

            string label = $"{req.Alias} ({req.NombreCompleto})";
            if (!req.TieneFirma)
            {
                result.FailedOrSkipped.Add((label, "No posee firma digital adjunta."));
                continue;
            }

            int orderCount = requesters.CountWorkOrders(id);
            if (skipWithWorkOrders && orderCount > 0)
            {
                result.FailedOrSkipped.Add((label, $"Posee {orderCount} OT(s) asociada(s). Reasigne la firma o el solicitante antes de eliminarla."));
                continue;
            }

            toClear.Add(id);
            result.Succeeded.Add(label);
        }

        if (toClear.Count > 0)
        {
            requesters.ClearSignaturesMany(toClear);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_FIRMAS_LOTE",
                $"{toClear.Count} firma(s) digital(es) eliminada(s).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, "ELIMINAR_FIRMAS_LOTE");
        }

        return result;
    }

    public int ReassignRequesterWorkOrders(
        UserSession session,
        IReadOnlyCollection<int> sourceRequesterIds,
        int targetRequesterId,
        bool requireTargetSignature = false)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores",
            requireTargetSignature ? "Reasignar firma en órdenes de trabajo" : "Reasignar solicitante en órdenes de trabajo");
        Requester target = requesters.GetById(targetRequesterId)
            ?? throw new InvalidOperationException("El solicitante destino seleccionado no existe.");

        if (!target.Activo)
        {
            throw new InvalidOperationException($"El solicitante destino '{target.NombreCompleto}' está inactivo. Seleccione un registro activo.");
        }

        if (requireTargetSignature && !target.TieneFirma)
        {
            throw new InvalidOperationException($"El solicitante destino '{target.NombreCompleto}' no posee una firma digital registrada.");
        }

        List<int> sources = sourceRequesterIds?.Where(id => id > 0 && id != targetRequesterId).Distinct().ToList() ?? [];
        if (sources.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar al menos un registro origen distinto al destino.");
        }

        int reassigned = requesters.ReassignWorkOrders(sources, targetRequesterId);
        string auditAction = requireTargetSignature ? "REASIGNAR_FIRMA_OTS" : "REASIGNAR_SOLICITANTE_OTS";
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", auditAction,
            $"{reassigned} OT(s) reasignada(s) a '{target.Alias} — {target.NombreCompleto}' (ID {targetRequesterId}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Catalogs | DataChangeKind.Audit, auditAction);
        return reassigned;
    }

    public int DeleteRelatedWorkOrdersExplicitly(UserSession session, IReadOnlyCollection<int> idOts, string contextDescription)
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", "Eliminar explícitamente OT asociadas");
        if (workOrders is null)
        {
            throw new InvalidOperationException("El repositorio de órdenes de trabajo no está disponible.");
        }

        List<int> normalized = idOts?.Where(id => id > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            return 0;
        }

        int deleted = workOrders.DeleteMany(normalized);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_OTS_ASOCIADAS",
            $"{deleted} OT(s) eliminada(s) explícitamente desde Mantenedores ({contextDescription}).");
        syncHub?.Publish(DataChangeKind.WorkOrders | DataChangeKind.Audit, "ELIMINAR_OTS_ASOCIADAS");
        return deleted;
    }

    private MaintainerBatchResult ApplyActiveBatch<T>(
        UserSession session,
        IReadOnlyCollection<int> ids,
        bool active,
        string entityLabel,
        string auditAction,
        Func<int, T?> getById,
        Func<T, string> getLabel,
        Func<T, bool> isActive,
        Func<IReadOnlyCollection<int>, bool, int> setMany) where T : class
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores",
            $"{(active ? "Activar" : "Desactivar")} {entityLabel} en lote");

        MaintainerBatchResult result = new()
        {
            ActionName = $"{(active ? "Activar" : "Desactivar")} {entityLabel}"
        };

        List<int> toUpdate = [];
        foreach (int id in ids?.Where(x => x > 0).Distinct() ?? [])
        {
            T? item = getById(id);
            if (item is null)
            {
                result.FailedOrSkipped.Add(($"#{id}", "Registro no encontrado."));
                continue;
            }

            string label = getLabel(item);
            if (isActive(item) == active)
            {
                result.FailedOrSkipped.Add((label, active ? "Ya se encontraba activo." : "Ya se encontraba inactivo."));
                continue;
            }

            toUpdate.Add(id);
            result.Succeeded.Add(label);
        }

        if (toUpdate.Count > 0)
        {
            setMany(toUpdate, active);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", auditAction,
                $"{toUpdate.Count} registro(s) de {entityLabel} {(active ? "activados" : "desactivados")}.");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, auditAction);
        }

        return result;
    }

    private MaintainerBatchResult ExecuteIntegrityDeleteBatch<T>(
        UserSession session,
        IReadOnlyCollection<int> ids,
        string actionTitle,
        string auditAction,
        Func<int, T?> getById,
        Func<T, string> getLabel,
        Func<int, int> countWorkOrders,
        Action<int> deleteSingle) where T : class
    {
        AuthService.EnsureAdministrator(session, users, audit, "Mantenedores", actionTitle);
        MaintainerBatchResult result = new() { ActionName = actionTitle };

        foreach (int id in ids?.Where(x => x > 0).Distinct() ?? [])
        {
            T? item = getById(id);
            if (item is null)
            {
                result.FailedOrSkipped.Add(($"#{id}", "Registro no encontrado."));
                continue;
            }

            string label = getLabel(item);
            int orderCount = countWorkOrders(id);
            if (orderCount > 0)
            {
                result.FailedOrSkipped.Add((label, $"Posee {orderCount} OT(s) asociada(s). Desactívelo o reasigne sus OTs antes de eliminar."));
                continue;
            }

            deleteSingle(id);
            result.Succeeded.Add(label);
        }

        if (result.Succeeded.Count > 0)
        {
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", auditAction,
                $"{result.Succeeded.Count} registro(s) eliminado(s); {result.FailedOrSkipped.Count} omitido(s).");
            syncHub?.Publish(DataChangeKind.Catalogs | DataChangeKind.Audit, auditAction);
        }

        return result;
    }

    // --- ÁRBOL DE ACTIVOS (CONSULTAS DE SOLO LECTURA; ESCRITURA EXCLUSIVA EN AssetTreeService) ---
    public IReadOnlyList<AssetNode> GetAssetChildren(int? parentId, bool includeInactive = false) =>
        assetTree.GetChildren(parentId, includeInactive);

    public IReadOnlyList<AssetNode> SearchAssets(string query, bool includeInactive = false) =>
        string.IsNullOrWhiteSpace(query) ? [] : assetTree.Search(query, includeInactive);

    public IReadOnlyList<AssetNode> GetAssetPath(int idActivo) =>
        assetTree.GetPathToRoot(idActivo);
}
