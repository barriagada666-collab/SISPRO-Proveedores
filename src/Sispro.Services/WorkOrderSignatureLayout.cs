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
            slots.Add(new WorkOrderSignatureSlot(
                "Juan Ramos Moyano",
                "Jefatura Mantención",
                Path.Combine(assetsDir, "Firmas", "JR.jpg"),
                null));
        }

        if (option.IncludeTecnico())
        {
            slots.Add(new WorkOrderSignatureSlot(
                string.IsNullOrWhiteSpace(order.SolicitanteDisplayNombre) ? "Técnico Solicitante" : order.SolicitanteDisplayNombre,
                "Técnico",
                null,
                order.FirmaSolicitantePreviewPng));
        }

        if (option.IncludeSupervisor())
        {
            slots.Add(new WorkOrderSignatureSlot(
                "Marcelo Silva Morgado",
                "Supervisor Mantención",
                Path.Combine(assetsDir, "Firmas", "MS.jpg"),
                null));
        }

        return slots;
    }
}
