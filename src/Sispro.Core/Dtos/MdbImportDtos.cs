namespace Sispro.Core.Dtos;

public enum MdbDiffAction
{
    Nuevo,
    Actualizar,
    SinCambios
}

public sealed class MdbProviderRow
{
    public int LegacyIdProv { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Direccion { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Fax { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
}

public sealed class MdbRequesterRow
{
    public int LegacyIdSol { get; init; }
    public string Alias { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string ApellidoPaterno { get; init; } = string.Empty;
    public string ApellidoMaterno { get; init; } = string.Empty;
    public string Telefono { get; init; } = string.Empty;
    public string Correo { get; init; } = string.Empty;
}

public sealed class MdbWorkOrderRow
{
    public int LegacyIdOt { get; init; }
    public int NumeroOt { get; init; }
    public int LegacyIdProv { get; init; }
    public int? LegacyIdSol { get; init; }
    public DateTime FechaOt { get; init; }
    public bool ActivoFlag { get; init; }
    public int GuiaDespacho { get; init; }
    public int IdN1 { get; init; }
    public int IdN2 { get; init; }
    public int IdN3 { get; init; }
    public int IdN4 { get; init; }
    public int IdN5 { get; init; }
    public int IdN6 { get; init; }
    public int IdN7 { get; init; }
    public int IdN8 { get; init; }
    public int IdN9 { get; init; }
    public int IdN10 { get; init; }
    public int IdNivel { get; init; }
}

public sealed class MdbWorkOrderDetailRow
{
    public int LegacyIdDet { get; init; }
    public int LegacyIdOt { get; init; }
    public decimal Cantidad { get; init; }
    public string Detalle { get; init; } = string.Empty;
    public bool EstadoDet { get; init; }
}

public sealed class MdbSnapshot
{
    public string MdbPath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public IReadOnlyList<MdbProviderRow> Providers { get; init; } = [];
    public IReadOnlyList<MdbRequesterRow> Requesters { get; init; } = [];
    public IReadOnlyList<MdbWorkOrderRow> WorkOrders { get; init; } = [];
    public IReadOnlyList<MdbWorkOrderDetailRow> WorkOrderDetails { get; init; } = [];
}

public sealed class MdbProviderDiffItem
{
    public MdbDiffAction Action { get; init; }
    public int? TargetIdProv { get; init; }
    public int LegacyIdProv { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string DireccionMdb { get; init; } = string.Empty;
    public string TelefonoMdb { get; init; } = string.Empty;
    public string FaxMdb { get; init; } = string.Empty;
    public string CorreoMdb { get; init; } = string.Empty;
    public string DireccionActual { get; init; } = string.Empty;
    public string TelefonoActual { get; init; } = string.Empty;
    public string FaxActual { get; init; } = string.Empty;
    public string CorreoActual { get; init; } = string.Empty;
    public string DireccionFinal { get; init; } = string.Empty;
    public string TelefonoFinal { get; init; } = string.Empty;
    public string FaxFinal { get; init; } = string.Empty;
    public string CorreoFinal { get; init; } = string.Empty;
    public IReadOnlyList<string> ChangedFields { get; init; } = [];
    public string PreviewBeforeAfter { get; init; } = string.Empty;

    public string ActionLabel => Action switch
    {
        MdbDiffAction.Nuevo => "Nuevo",
        MdbDiffAction.Actualizar => "Actualizar",
        _ => "Sin cambios"
    };

    public string ChangedFieldsSummary => ChangedFields.Count == 0
        ? "-"
        : string.Join(", ", ChangedFields);
}

public sealed class MdbImportOptions
{
    public bool InsertNewProviders { get; set; } = true;
    public bool UpdateExistingProviders { get; set; } = true;
    public bool OverwriteWithEmptyValues { get; set; }
    public bool SyncNewRequesters { get; set; } = true;
    public bool SyncNewWorkOrders { get; set; }
    public bool CreateBackupBeforeApply { get; set; } = true;

    /// <summary>
    /// Cuando es true, limpia las tablas operativas de sispro.db (Proveedores, Solicitantes,
    /// OrdenesTrabajo, DetalleOT y VistoBueno) dentro de la misma transacción e importa todo
    /// el contenido del MDB desde cero, preservando Usuarios, Permisos, Grupos, Activos,
    /// Configuración del Sistema y Auditoría.
    /// </summary>
    public bool RewriteAllFromMdb { get; set; }
}

public sealed record OperationalDatabaseCounts(
    int ProvidersCount,
    int RequestersCount,
    int WorkOrdersCount,
    int WorkOrderDetailsCount);

public sealed class MdbDatabaseCleanResult
{
    public string? BackupPath { get; init; }
    public int ProvidersDeleted { get; init; }
    public int RequestersDeleted { get; init; }
    public int WorkOrdersDeleted { get; init; }
    public int WorkOrderDetailsDeleted { get; init; }

    public int TotalDeleted => ProvidersDeleted + RequestersDeleted + WorkOrdersDeleted + WorkOrderDetailsDeleted;

    public string BuildSummaryText() =>
        $"Proveedores eliminados: {ProvidersDeleted} | Solicitantes eliminados: {RequestersDeleted}\n" +
        $"Órdenes de trabajo eliminadas: {WorkOrdersDeleted} ({WorkOrderDetailsDeleted} ítems de detalle)\n" +
        "Conservados intactos: Usuarios, Perfiles, Grupos, Árbol de Activos, Configuración del Sistema y Auditoría.";
}

public sealed class MdbImportPreview
{
    public string MdbPath { get; init; } = string.Empty;
    public long MdbFileSizeBytes { get; init; }
    public bool IsFullRewriteMode { get; init; }
    public int MdbProvidersTotal { get; init; }
    public int DbProvidersBefore { get; init; }
    public int DbRequestersBefore { get; init; }
    public int DbWorkOrdersBefore { get; init; }
    public int NewProvidersCount { get; init; }
    public int UpdatedProvidersCount { get; init; }
    public int UnchangedProvidersCount { get; init; }
    public int MdbRequestersTotal { get; init; }
    public int NewRequestersCount { get; init; }
    public int UpdatedRequestersCount { get; init; }
    public int MdbWorkOrdersTotal { get; init; }
    public int NewWorkOrdersCount { get; init; }
    public int NewWorkOrderDetailsCount { get; init; }
    public IReadOnlyList<MdbProviderDiffItem> ProviderDiffs { get; init; } = [];
    public IReadOnlyList<MdbRequesterRow> NewOrUpdatedRequesters { get; init; } = [];
    public IReadOnlyList<MdbWorkOrderRow> NewWorkOrders { get; init; } = [];
    public IReadOnlyList<MdbWorkOrderDetailRow> NewWorkOrderDetails { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public int TotalProviderChanges => NewProvidersCount + UpdatedProvidersCount;

    public string BuildSummaryText() => IsFullRewriteMode
        ? $"MODO REESCRITURA COMPLETA | Se reemplazarán {DbProvidersBefore} prov., {DbRequestersBefore} sol. y {DbWorkOrdersBefore} OTs por: " +
          $"{NewProvidersCount} proveedores, {NewRequestersCount} solicitantes y {NewWorkOrdersCount} OTs ({NewWorkOrderDetailsCount} ítems) desde MDB."
        : $"Proveedores MDB: {MdbProvidersTotal} | En BD actual: {DbProvidersBefore} | " +
          $"Nuevos: {NewProvidersCount} | Para actualizar: {UpdatedProvidersCount} | Sin cambios: {UnchangedProvidersCount} | " +
          $"Solicitantes nuevos: {NewRequestersCount} | OTs nuevas en MDB: {NewWorkOrdersCount} ({NewWorkOrderDetailsCount} ítems)";
}

public sealed class MdbImportResult
{
    public string MdbPath { get; init; } = string.Empty;
    public string? BackupPath { get; init; }
    public bool WasFullRewrite { get; init; }
    public int ProvidersCleared { get; init; }
    public int RequestersCleared { get; init; }
    public int WorkOrdersCleared { get; init; }
    public int ProvidersInserted { get; init; }
    public int ProvidersUpdated { get; init; }
    public int ProvidersUnchanged { get; init; }
    public int RequestersInserted { get; init; }
    public int RequestersUpdated { get; init; }
    public int WorkOrdersInserted { get; init; }
    public int WorkOrderDetailsInserted { get; init; }
    public int DbProvidersAfter { get; init; }
    public int DbWorkOrdersAfter { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public string BuildSummaryText()
    {
        string rewriteHeader = WasFullRewrite
            ? $"Reescritura limpia ejecutada (eliminados previamente: {ProvidersCleared} prov., {RequestersCleared} sol., {WorkOrdersCleared} OTs).\n"
            : string.Empty;
        return rewriteHeader +
               $"Proveedores agregados: {ProvidersInserted} | Proveedores actualizados: {ProvidersUpdated} | Sin cambios: {ProvidersUnchanged}\n" +
               $"Solicitantes agregados/actualizados: {RequestersInserted}/{RequestersUpdated} | OTs agregadas: {WorkOrdersInserted} ({WorkOrderDetailsInserted} ítems)\n" +
               $"Total en BD ahora: {DbProvidersAfter} proveedores y {DbWorkOrdersAfter} órdenes de trabajo.";
    }
}
