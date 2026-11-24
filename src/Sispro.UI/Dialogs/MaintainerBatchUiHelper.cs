using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Theme;

namespace Sispro.UI.Dialogs;

/// <summary>
/// Flujo unificado de verificación de dependencias, consulta de OT relacionadas y ejecución
/// segura de operaciones masivas en Proveedores, Solicitantes, Firmas y Árbol de Activos.
/// </summary>
public static class MaintainerBatchUiHelper
{
    public static bool InspectAndManage(
        IWin32Window owner,
        SisproApplicationContext ctx,
        UserSession session,
        MaintainerEntityKind kind,
        IReadOnlyCollection<int> ids,
        Action onUpdated)
    {
        List<int> normalized = ids?.Where(x => x > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            CambiasoTheme.ShowWarning(owner, "Marque al menos un registro mediante su casilla de verificación.");
            return false;
        }

        MaintainerDependencyReport report = LoadReport(ctx, session, kind, normalized);
        using MaintainerDependenciesDialog dlg = new(ctx, session, report);
        dlg.ShowDialog(owner);
        if (dlg.ChangesApplied)
        {
            onUpdated();
            return true;
        }

        return false;
    }

    public static bool DeactivateChecked(
        IWin32Window owner,
        SisproApplicationContext ctx,
        UserSession session,
        MaintainerEntityKind kind,
        IReadOnlyCollection<int> ids,
        Action onUpdated)
    {
        if (!EnsureAdminUi(owner, ctx, session, kind))
        {
            return false;
        }

        List<int> normalized = ids?.Where(x => x > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            CambiasoTheme.ShowWarning(owner, "Marque al menos un registro mediante su casilla de verificación.");
            return false;
        }

        MaintainerDependencyReport report = LoadReport(ctx, session, kind, normalized);
        if (report.RelatedWorkOrders.Count > 0 || report.DependentChildNodesCount > 0)
        {
            using MaintainerDependenciesDialog dlg = new(ctx, session, report);
            dlg.ShowDialog(owner);
            if (dlg.ChangesApplied)
            {
                onUpdated();
                return true;
            }

            return false;
        }

        if (!CambiasoTheme.Confirm(owner, $"¿Desactivar {normalized.Count} registro(s) seleccionado(s)?\nNo podrán usarse en nuevas órdenes de trabajo, pero conservarán su historial."))
        {
            return false;
        }

        try
        {
            MaintainerBatchResult res = kind switch
            {
                MaintainerEntityKind.Proveedor => ctx.MaintainerService.SetProvidersActiveBatch(session, normalized, false),
                MaintainerEntityKind.Solicitante or MaintainerEntityKind.Firma => ctx.MaintainerService.SetRequestersActiveBatch(session, normalized, false),
                MaintainerEntityKind.Activo => ctx.AssetTreeService.SetActiveBatch(session, normalized, false, includeDescendants: true),
                _ => throw new InvalidOperationException("Tipo no soportado.")
            };

            CambiasoTheme.ShowInfo(owner, res.BuildSummary());
            onUpdated();
            return res.Succeeded.Count > 0;
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(owner, ex.Message);
            return false;
        }
    }

    public static bool DeleteChecked(
        IWin32Window owner,
        SisproApplicationContext ctx,
        UserSession session,
        MaintainerEntityKind kind,
        IReadOnlyCollection<int> ids,
        Action onUpdated)
    {
        if (!EnsureAdminUi(owner, ctx, session, kind))
        {
            return false;
        }

        List<int> normalized = ids?.Where(x => x > 0).Distinct().ToList() ?? [];
        if (normalized.Count == 0)
        {
            CambiasoTheme.ShowWarning(owner, "Marque al menos un registro mediante su casilla de verificación.");
            return false;
        }

        MaintainerDependencyReport report = LoadReport(ctx, session, kind, normalized);
        if (report.RelatedWorkOrders.Count > 0 || report.DependentChildNodesCount > 0)
        {
            using MaintainerDependenciesDialog dlg = new(ctx, session, report);
            dlg.ShowDialog(owner);
            if (dlg.ChangesApplied)
            {
                onUpdated();
                return true;
            }

            return false;
        }

        string noun = kind == MaintainerEntityKind.Firma ? "firma(s) digital(es)" : "registro(s)";
        if (!CambiasoTheme.Confirm(owner, $"No se detectaron OT asociadas. ¿Eliminar definitivamente {normalized.Count} {noun} seleccionado(s)?"))
        {
            return false;
        }

        try
        {
            MaintainerBatchResult res = kind switch
            {
                MaintainerEntityKind.Proveedor => ctx.MaintainerService.DeleteProvidersBatch(session, normalized),
                MaintainerEntityKind.Solicitante => ctx.MaintainerService.DeleteRequestersBatch(session, normalized),
                MaintainerEntityKind.Firma => ctx.MaintainerService.ClearSignaturesBatch(session, normalized, skipWithWorkOrders: true),
                MaintainerEntityKind.Activo => ctx.AssetTreeService.DeleteNodesBatch(session, normalized),
                _ => throw new InvalidOperationException("Tipo no soportado.")
            };

            CambiasoTheme.ShowInfo(owner, res.BuildSummary());
            onUpdated();
            return res.Succeeded.Count > 0;
        }
        catch (Exception ex)
        {
            CambiasoTheme.ShowWarning(owner, ex.Message);
            return false;
        }
    }

    private static MaintainerDependencyReport LoadReport(
        SisproApplicationContext ctx,
        UserSession session,
        MaintainerEntityKind kind,
        IReadOnlyCollection<int> ids) =>
        kind == MaintainerEntityKind.Activo
            ? ctx.AssetTreeService.InspectDependencies(session, ids)
            : ctx.MaintainerService.InspectDependencies(session, kind, ids);

    private static bool EnsureAdminUi(
        IWin32Window owner,
        SisproApplicationContext ctx,
        UserSession session,
        MaintainerEntityKind kind)
    {
        bool isAdmin = kind == MaintainerEntityKind.Activo
            ? ctx.AssetTreeService.IsAdministrator(session)
            : ctx.MaintainerService.IsAdministrator(session);
        if (isAdmin)
        {
            return true;
        }

        CambiasoTheme.ShowWarning(owner, "Solo los usuarios con perfil Administrador pueden desactivar, eliminar o reasignar registros.");
        return false;
    }
}
