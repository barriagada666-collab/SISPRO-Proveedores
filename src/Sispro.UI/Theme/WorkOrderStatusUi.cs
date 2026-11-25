using System.Drawing;
using Sispro.Core.Enums;

namespace Sispro.UI.Theme;

/// <summary>
/// Helpers visuales y de menú contextual para los 6 estados oficiales de Órdenes de Trabajo:
/// Abierta, En Curso, Modificada, Terminada, Cerrada y Anulada.
/// </summary>
public static class WorkOrderStatusUi
{
    public static Color GetColor(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Abierta => CambiasoTheme.Primary,
        WorkOrderStatus.EnCurso => CambiasoTheme.Accent,
        WorkOrderStatus.Modificada => CambiasoTheme.Warning,
        WorkOrderStatus.Terminada => CambiasoTheme.Success,
        WorkOrderStatus.Cerrada => CambiasoTheme.PrimaryDark,
        WorkOrderStatus.Anulada => CambiasoTheme.Danger,
        _ => CambiasoTheme.Primary
    };

    public static AppIcon GetIcon(WorkOrderStatus status) => status switch
    {
        WorkOrderStatus.Abierta => AppIcon.WorkOrder,
        WorkOrderStatus.EnCurso => AppIcon.Refresh,
        WorkOrderStatus.Modificada => AppIcon.Edit,
        WorkOrderStatus.Terminada => AppIcon.ClipboardCheck,
        WorkOrderStatus.Cerrada => AppIcon.Shield,
        WorkOrderStatus.Anulada => AppIcon.Ban,
        _ => AppIcon.WorkOrder
    };

    public static ToolStripMenuItem BuildChangeStatusMenu(
        WorkOrderStatus? currentStatus,
        bool enabled,
        Action<WorkOrderStatus> onSelect,
        string title = "Cambiar estado")
    {
        List<ToolStripItem> children = [];
        foreach (WorkOrderStatus status in WorkOrderStatusExtensions.AllOrdered)
        {
            WorkOrderStatus captured = status;
            bool isCurrent = currentStatus.HasValue && currentStatus.Value == status;
            string label = isCurrent ? $"{status.ToDisplayName()} (Actual)" : status.ToDisplayName();
            ToolStripMenuItem item = ContextMenuHelper.Item(
                label,
                GetIcon(status),
                GetColor(status),
                () => onSelect(captured),
                enabled: enabled);
            if (isCurrent)
            {
                item.Font = CambiasoTheme.LabelFont;
            }

            children.Add(item);
        }

        return ContextMenuHelper.SubMenu(title, AppIcon.ClipboardCheck, CambiasoTheme.Accent, children, enabled: enabled);
    }
}
