using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class MaintainerService(
    IProviderRepository providers,
    IRequesterRepository requesters,
    IAssetTreeRepository assetTree,
    IAuditRepository audit,
    SisproSyncHub? syncHub = null)
{
    // --- PROVEEDORES ---
    public IReadOnlyList<Provider> GetProviders(bool includeInactive = false, string? search = null) =>
        providers.GetAll(includeInactive, search);

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
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, "Eliminar proveedores");
        Provider existing = providers.GetById(idProv)
            ?? throw new InvalidOperationException("El proveedor seleccionado no existe.");

        if (providers.HasWorkOrders(idProv))
        {
            existing.Activo = false;
            providers.Update(existing);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "DESACTIVAR_PROVEEDOR",
                $"Proveedor '{existing.Nombre}' desactivado (mantiene OTs históricas).");
            syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "DESACTIVAR_PROVEEDOR");
            return;
        }

        providers.Delete(idProv);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_PROVEEDOR", $"Proveedor '{existing.Nombre}' eliminado.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "ELIMINAR_PROVEEDOR");
    }

    public void SetProviderActive(UserSession session, int idProv, bool active)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, active ? "Activar proveedor" : "Desactivar proveedor");
        Provider existing = providers.GetById(idProv)
            ?? throw new InvalidOperationException("El proveedor seleccionado no existe.");

        existing.Activo = active;
        providers.Update(existing);
        string accion = active ? "ACTIVAR_PROVEEDOR" : "DESACTIVAR_PROVEEDOR";
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", accion, $"Proveedor '{existing.Nombre}' {(active ? "activado" : "desactivado")}.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, accion);
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
        AuthService.EnsurePermission(session, p => p.MenuMantenedor || p.MenuUsuarios, "Eliminar solicitantes");
        Requester existing = requesters.GetById(idSol)
            ?? throw new InvalidOperationException("El solicitante seleccionado no existe.");

        if (requesters.HasWorkOrders(idSol))
        {
            existing.Activo = false;
            requesters.Update(existing);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "DESACTIVAR_SOLICITANTE",
                $"Solicitante '{existing.Alias}' desactivado (mantiene OTs históricas).");
            syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "DESACTIVAR_SOLICITANTE");
            return;
        }

        requesters.Delete(idSol);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_SOLICITANTE", $"Solicitante '{existing.Alias}' eliminado.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, "ELIMINAR_SOLICITANTE");
    }

    public void SetRequesterActive(UserSession session, int idSol, bool active)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor || p.MenuUsuarios, active ? "Activar solicitante" : "Desactivar solicitante");
        Requester existing = requesters.GetById(idSol)
            ?? throw new InvalidOperationException("El solicitante seleccionado no existe.");

        existing.Activo = active;
        requesters.Update(existing);
        string accion = active ? "ACTIVAR_SOLICITANTE" : "DESACTIVAR_SOLICITANTE";
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", accion, $"Solicitante '{existing.Alias}' {(active ? "activado" : "desactivado")}.");
        syncHub?.Publish(Sispro.Core.Enums.DataChangeKind.Catalogs | Sispro.Core.Enums.DataChangeKind.Audit, accion);
    }

    // --- ÁRBOL DE ACTIVOS (CONSULTAS DE SOLO LECTURA; ESCRITURA EXCLUSIVA EN AssetTreeService) ---
    public IReadOnlyList<AssetNode> GetAssetChildren(int? parentId, bool includeInactive = false) =>
        assetTree.GetChildren(parentId, includeInactive);

    public IReadOnlyList<AssetNode> SearchAssets(string query, bool includeInactive = false) =>
        string.IsNullOrWhiteSpace(query) ? [] : assetTree.Search(query, includeInactive);

    public IReadOnlyList<AssetNode> GetAssetPath(int idActivo) =>
        assetTree.GetPathToRoot(idActivo);
}
