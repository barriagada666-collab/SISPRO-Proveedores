using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Core.Validation;

namespace Sispro.Services;

public sealed class MaintainerService(
    IProviderRepository providers,
    IRequesterRepository requesters,
    IAssetTreeRepository assetTree,
    IAuditRepository audit)
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
            return id;
        }

        providers.Update(provider);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "MODIFICAR_PROVEEDOR", $"Proveedor '{provider.Nombre}' (ID {provider.IdProv}) actualizado.");
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
            return;
        }

        providers.Delete(idProv);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_PROVEEDOR", $"Proveedor '{existing.Nombre}' eliminado.");
    }

    // --- SOLICITANTES ---
    public IReadOnlyList<Requester> GetRequesters(bool includeInactive = false, string? search = null) =>
        requesters.GetAll(includeInactive, search);

    public int SaveRequester(UserSession session, Requester requester)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, "Administrar solicitantes");
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

        if (requester.IdSol == 0)
        {
            int id = requesters.Create(requester);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "CREAR_SOLICITANTE", $"Solicitante '{requester.Alias}' creado.");
            return id;
        }

        requesters.Update(requester);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "MODIFICAR_SOLICITANTE", $"Solicitante '{requester.Alias}' actualizado.");
        return requester.IdSol;
    }

    public void DeleteRequester(UserSession session, int idSol)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, "Eliminar solicitantes");
        Requester existing = requesters.GetById(idSol)
            ?? throw new InvalidOperationException("El solicitante seleccionado no existe.");

        if (requesters.HasWorkOrders(idSol))
        {
            existing.Activo = false;
            requesters.Update(existing);
            audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "DESACTIVAR_SOLICITANTE",
                $"Solicitante '{existing.Alias}' desactivado (mantiene OTs históricas).");
            return;
        }

        requesters.Delete(idSol);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "ELIMINAR_SOLICITANTE", $"Solicitante '{existing.Alias}' eliminado.");
    }

    // --- ÁRBOL DE ACTIVOS (10 NIVELES) ---
    public IReadOnlyList<AssetNode> GetAssetChildren(int? parentId, bool includeInactive = false) =>
        assetTree.GetChildren(parentId, includeInactive);

    public IReadOnlyList<AssetNode> SearchAssets(string query, bool includeInactive = false) =>
        string.IsNullOrWhiteSpace(query) ? [] : assetTree.Search(query, includeInactive);

    public IReadOnlyList<AssetNode> GetAssetPath(int idActivo) =>
        assetTree.GetPathToRoot(idActivo);

    public int AddAssetChild(UserSession session, int parentId, string descripcion)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, "Administrar árbol de activos");
        if (string.IsNullOrWhiteSpace(descripcion))
        {
            throw new ArgumentException("Debe ingresar la descripción del activo.");
        }

        AssetNode parent = assetTree.GetById(parentId)
            ?? throw new InvalidOperationException("El nodo padre seleccionado no existe.");

        if (parent.Nivel >= 10)
        {
            throw new InvalidOperationException("El árbol de activos admite un máximo de 10 niveles de profundidad.");
        }

        AssetNode node = new()
        {
            ParentId = parent.IdActivo,
            Nivel = parent.Nivel + 1,
            Descripcion = descripcion.Trim(),
            Activo = true
        };

        int id = assetTree.Create(node);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "CREAR_ACTIVO",
            $"Nodo '{node.Descripcion}' (Nivel {node.Nivel}, ID {id}) creado bajo '{parent.Descripcion}'.");
        return id;
    }

    public void RenameAsset(UserSession session, int idActivo, string nuevaDescripcion, bool activo)
    {
        AuthService.EnsurePermission(session, p => p.MenuMantenedor, "Modificar árbol de activos");
        if (string.IsNullOrWhiteSpace(nuevaDescripcion))
        {
            throw new ArgumentException("La descripción no puede estar vacía.");
        }

        AssetNode node = assetTree.GetById(idActivo)
            ?? throw new InvalidOperationException("El activo seleccionado no existe.");

        if (node.IdActivo == 1 && !activo)
        {
            throw new InvalidOperationException("No es posible desactivar el nodo raíz de la empresa.");
        }

        node.Descripcion = nuevaDescripcion.Trim();
        node.Activo = activo;
        assetTree.Update(node);
        audit.Log(session.Usuario.AliasUsuario, "Mantenedores", "MODIFICAR_ACTIVO",
            $"Activo ID {idActivo} actualizado a '{node.Descripcion}' (Activo={activo}).");
    }
}
