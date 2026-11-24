using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Services;

internal sealed record WorkOrderSignatureSlot(
    string SignerName,
    string RoleTitle,
    string? AssetImagePath,
    byte[]? ImageBytes);

internal static class WorkOrderSignatureLayout
{
    /// <summary>
    /// Construye la lista ordenada de firmas a incluir al pie del documento de OT.
    /// El orden visual ubica al Solicitante (Técnico) entre Jefatura y Supervisor.
    /// Si ninguna firma está seleccionada, retorna lista vacía para omitir todo el bloque del pie.
    /// </summary>
    public static List<WorkOrderSignatureSlot> BuildSlots(
        WorkOrder order,
        SignatureOption option,
        string assetsDir)
    {
        List<WorkOrderSignatureSlot> slots = [];

        if (option.IncludeJefatura())
        {
            string jefeName = string.IsNullOrWhiteSpace(order.FirmaJefaturaNombre)
                ? "Juan Ramos Moyano"
                : order.FirmaJefaturaNombre.Trim();
            string? fallbackAsset = jefeName.Contains("Juan Ramos", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(assetsDir, "Firmas", "JR.jpg")
                : null;
            slots.Add(new WorkOrderSignatureSlot(
                jefeName,
                "Jefatura Mantención",
                order.FirmaJefaturaPreviewPng is { Length: > 0 } ? null : fallbackAsset,
                order.FirmaJefaturaPreviewPng));
        }

        if (option.IncludeTecnico())
        {
            string tecName = !string.IsNullOrWhiteSpace(order.FirmaTecnicoNombre)
                ? order.FirmaTecnicoNombre.Trim()
                : (string.IsNullOrWhiteSpace(order.SolicitanteDisplayNombre) ? "Técnico Solicitante" : order.SolicitanteDisplayNombre);
            byte[]? tecBytes = order.FirmaTecnicoPreviewPng is { Length: > 0 }
                ? order.FirmaTecnicoPreviewPng
                : (string.IsNullOrWhiteSpace(order.FirmaTecnicoNombre) ||
                   string.Equals(order.FirmaTecnicoNombre.Trim(), order.SolicitanteDisplayNombre, StringComparison.OrdinalIgnoreCase))
                    ? order.FirmaSolicitantePreviewPng
                    : null;
            string? fallbackAsset = tecBytes is { Length: > 0 }
                ? null
                : tecName.Contains("Juan Ramos", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(assetsDir, "Firmas", "JR.jpg")
                    : tecName.Contains("Marcelo Silva", StringComparison.OrdinalIgnoreCase)
                        ? Path.Combine(assetsDir, "Firmas", "MS.jpg")
                        : null;
            slots.Add(new WorkOrderSignatureSlot(
                tecName,
                "Técnico",
                fallbackAsset,
                tecBytes));
        }

        if (option.IncludeSupervisor())
        {
            string supName = string.IsNullOrWhiteSpace(order.FirmaSupervisorNombre)
                ? "Marcelo Silva Morgado"
                : order.FirmaSupervisorNombre.Trim();
            string? fallbackAsset = supName.Contains("Marcelo Silva", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(assetsDir, "Firmas", "MS.jpg")
                : null;
            slots.Add(new WorkOrderSignatureSlot(
                supName,
                "Supervisor Mantención",
                order.FirmaSupervisorPreviewPng is { Length: > 0 } ? null : fallbackAsset,
                order.FirmaSupervisorPreviewPng));
        }

        return slots;
    }
}
