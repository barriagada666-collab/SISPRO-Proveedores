using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;

namespace Sispro.Services;

/// <summary>
/// Fachada unificada de exportación corporativa (Excel, PDF, Word y CSV) que garantiza
/// identidad visual homogénea, respeto estricto de filtros activos y orden cronológico descendente.
/// </summary>
public sealed class DocumentExportService(
    ExcelExportService excel,
    PdfExportService pdf,
    WordExportService word,
    CsvExportService csv)
{
    public void ExportReport(TabularReportData report, ExportFormat format, string outputPath, string? assetsDirectory = null)
    {
        switch (format)
        {
            case ExportFormat.Excel:
                excel.ExportTabularReport(report, outputPath, assetsDirectory);
                break;
            case ExportFormat.Pdf:
                pdf.ExportTabularReport(report, outputPath, assetsDirectory);
                break;
            case ExportFormat.Word:
                word.ExportTabularReport(report, outputPath, assetsDirectory);
                break;
            case ExportFormat.Csv:
                csv.ExportTabularReport(report, outputPath);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Formato de exportación no soportado.");
        }
    }

    public void ExportWorkOrder(
        WorkOrder order,
        ExportFormat format,
        string outputPath,
        SignatureOption signatureOption = SignatureOption.Ambas,
        string? assetsDirectory = null)
    {
        switch (format)
        {
            case ExportFormat.Excel:
                excel.ExportWorkOrderDocument(order, outputPath, signatureOption, assetsDirectory);
                break;
            case ExportFormat.Pdf:
                pdf.ExportWorkOrderDocument(order, outputPath, signatureOption, assetsDirectory);
                break;
            case ExportFormat.Word:
                word.ExportWorkOrderDocument(order, outputPath, signatureOption, assetsDirectory);
                break;
            case ExportFormat.Csv:
                csv.ExportTabularReport(BuildSingleWorkOrderTabular(order), outputPath);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, "Formato no soportado.");
        }
    }

    public static TabularReportData BuildWorkOrdersReport(
        IReadOnlyList<WorkOrderSearchRow> rows,
        string? filterSummary = null,
        IReadOnlyList<WorkOrderExportField>? selectedFields = null)
    {
        WorkOrderExportField[] fields = ResolveWorkOrderFields(
            selectedFields,
            [
                WorkOrderExportField.NumeroOt,
                WorkOrderExportField.Fecha,
                WorkOrderExportField.Proveedor,
                WorkOrderExportField.Solicitante,
                WorkOrderExportField.Cantidad,
                WorkOrderExportField.Detalle,
                WorkOrderExportField.RutaActivo,
                WorkOrderExportField.Observacion,
                WorkOrderExportField.ValorNeto,
                WorkOrderExportField.Estado,
                WorkOrderExportField.Firmas
            ]);

        return new TabularReportData
        {
            Title = "REPORTE DE ÓRDENES DE TRABAJO EXTERNAS",
            SheetName = "Órdenes de Trabajo",
            FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
            Landscape = true,
            Columns = fields.Select(f => BuildWorkOrderColumnSpec(f, isSearchList: true)).ToList(),
            Rows = rows.Select(r => fields.Select(f => ExtractSearchRowField(r, f)).ToArray()).ToList()
        };
    }

    public static TabularReportData BuildSelectedWorkOrdersReport(
        IReadOnlyList<WorkOrder> orders,
        string? filterSummary = null,
        IReadOnlyList<WorkOrderExportField>? selectedFields = null)
    {
        List<WorkOrder> distinctOrders = orders
            .Where(o => o.IdOt > 0 || o.NumeroOt > 0)
            .GroupBy(o => o.IdOt > 0 ? o.IdOt : o.NumeroOt)
            .Select(g => g.First())
            .ToList();

        WorkOrderExportField[] fields = ResolveWorkOrderFields(
            selectedFields,
            [
                WorkOrderExportField.NumeroOt,
                WorkOrderExportField.Fecha,
                WorkOrderExportField.TipoOrden,
                WorkOrderExportField.Solicitante,
                WorkOrderExportField.Proveedor,
                WorkOrderExportField.RutaActivo,
                WorkOrderExportField.Detalle,
                WorkOrderExportField.Observacion,
                WorkOrderExportField.ValorNeto,
                WorkOrderExportField.Estado,
                WorkOrderExportField.Firmas
            ]);

        return new TabularReportData
        {
            Title = "EXPORTACIÓN DE ÓRDENES DE TRABAJO SELECCIONADAS",
            SheetName = "OT Seleccionadas",
            FilterSummary = string.IsNullOrWhiteSpace(filterSummary)
                ? $"Registros exportados: {distinctOrders.Count} orden(es) única(s)"
                : filterSummary,
            Landscape = true,
            Columns = fields.Select(f => BuildWorkOrderColumnSpec(f, isSearchList: false)).ToList(),
            Rows = distinctOrders.Select(o => fields.Select(f => ExtractWorkOrderField(o, f)).ToArray()).ToList()
        };
    }

    private static WorkOrderExportField[] ResolveWorkOrderFields(
        IReadOnlyList<WorkOrderExportField>? requested,
        WorkOrderExportField[] fallback)
    {
        if (requested is null || requested.Count == 0)
        {
            return fallback;
        }

        WorkOrderExportField[] distinct = requested.Distinct().ToArray();
        return distinct.Length > 0 ? distinct : fallback;
    }

    private static ExportColumn BuildWorkOrderColumnSpec(WorkOrderExportField field, bool isSearchList) => field switch
    {
        WorkOrderExportField.NumeroOt => new("N° OT", ExportColumnKind.Integer, 0.62),
        WorkOrderExportField.Fecha => new("Fecha", ExportColumnKind.Date, 0.88),
        WorkOrderExportField.TipoOrden => new("Tipo", ExportColumnKind.Text, 0.78),
        WorkOrderExportField.Proveedor => new("Proveedor", ExportColumnKind.Text, 1.55),
        WorkOrderExportField.Solicitante => new("Solicitante", ExportColumnKind.Text, 1.15),
        WorkOrderExportField.Cantidad => new("Cant.", ExportColumnKind.Integer, 0.50, SumInFooter: true),
        WorkOrderExportField.Detalle => new(isSearchList ? "Detalle del Trabajo" : "Detalle", ExportColumnKind.Text, 2.65),
        WorkOrderExportField.RutaActivo => new(isSearchList ? "Ruta de Activo" : "Activo", ExportColumnKind.Text, 1.55),
        WorkOrderExportField.Observacion => new("Observaciones", ExportColumnKind.Text, 1.50),
        WorkOrderExportField.ValorNeto => new("Valor Neto", ExportColumnKind.Currency, 0.98, SumInFooter: true),
        WorkOrderExportField.Estado => new("Estado", ExportColumnKind.Text, 0.82),
        WorkOrderExportField.Firmas => new("Firmas", ExportColumnKind.Text, 1.25),
        _ => new(field.ToString(), ExportColumnKind.Text, 1.0)
    };

    private static object? ExtractSearchRowField(WorkOrderSearchRow r, WorkOrderExportField field) => field switch
    {
        WorkOrderExportField.NumeroOt => r.NumeroOt,
        WorkOrderExportField.Fecha => r.Fecha,
        WorkOrderExportField.TipoOrden => $"OT {r.TipoOrden}",
        WorkOrderExportField.Proveedor => r.Proveedor,
        WorkOrderExportField.Solicitante => r.Solicitante,
        WorkOrderExportField.Cantidad => r.Cantidad,
        WorkOrderExportField.Detalle => r.Detalle,
        WorkOrderExportField.RutaActivo => r.RutaActivo,
        WorkOrderExportField.Observacion => r.Observacion,
        WorkOrderExportField.ValorNeto => r.MostrarValorNeto ? r.ValorNeto : null,
        WorkOrderExportField.Estado => r.EstadoDisplay,
        WorkOrderExportField.Firmas => r.FirmasDisplay,
        _ => string.Empty
    };

    private static object? ExtractWorkOrderField(WorkOrder o, WorkOrderExportField field) => field switch
    {
        WorkOrderExportField.NumeroOt => o.NumeroOt,
        WorkOrderExportField.Fecha => o.FechaOt,
        WorkOrderExportField.TipoOrden => $"OT {o.TipoOrden}",
        WorkOrderExportField.Proveedor => o.NombreProveedor,
        WorkOrderExportField.Solicitante => o.SolicitanteDisplayNombre,
        WorkOrderExportField.Cantidad => o.Detalles.Count > 0 ? o.Detalles.Sum(static d => d.Cantidad) : 0,
        WorkOrderExportField.Detalle => o.Detalles.Count switch
        {
            0 => string.Empty,
            1 => o.Detalles[0].Descripcion,
            _ => string.Join("\n", o.Detalles.Select(static d => $"{d.Cantidad}x {d.Descripcion}"))
        },
        WorkOrderExportField.RutaActivo => o.RutaActivo,
        WorkOrderExportField.Observacion => o.Observacion,
        WorkOrderExportField.ValorNeto => o.MostrarValorNeto ? o.ValorNeto : null,
        WorkOrderExportField.Estado => o.Estado.ToDisplayName(),
        WorkOrderExportField.Firmas => o.FirmasSeleccionadas.FormatResumen(),
        _ => string.Empty
    };

    public static TabularReportData BuildRequestersReport(IReadOnlyList<Requester> requesters, string? filterSummary = null) => new()
    {
        Title = "CATÁLOGO DE SOLICITANTES Y FIRMAS DIGITALES",
        SheetName = "Solicitantes",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Catálogo institucional de solicitantes OT" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.5),
            new("Alias", ExportColumnKind.Text, 1.1),
            new("Nombre Completo", ExportColumnKind.Text, 1.8),
            new("Teléfono", ExportColumnKind.Text, 1.0),
            new("Correo", ExportColumnKind.Text, 1.6),
            new("Firma Digital", ExportColumnKind.Text, 0.9),
            new("Archivo Firma", ExportColumnKind.Text, 1.4),
            new("Estado", ExportColumnKind.Text, 0.7)
        ],
        Rows = requesters.Select(r => new object?[]
        {
            r.IdSol, r.Alias, r.NombreCompleto, r.Telefono, r.Correo,
            r.FirmaBadge, r.FirmaNombre, r.Activo ? "Activo" : "Inactivo"
        }).ToList()
    };

    public static TabularReportData BuildUsersReport(IReadOnlyList<User> users, string? filterSummary = null) => new()
    {
        Title = "REPORTE DE GESTIÓN DE USUARIOS Y GRUPOS",
        SheetName = "Usuarios",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.5),
            new("Usuario", ExportColumnKind.Text, 1.1),
            new("Nombre Completo", ExportColumnKind.Text, 1.8),
            new("Correo Electrónico", ExportColumnKind.Text, 1.7),
            new("Teléfono", ExportColumnKind.Text, 1.0),
            new("Perfil", ExportColumnKind.Text, 1.0),
            new("Grupo", ExportColumnKind.Text, 1.1),
            new("Estado", ExportColumnKind.Text, 0.7),
            new("Fecha Creación", ExportColumnKind.DateTime, 1.2),
            new("Último Acceso", ExportColumnKind.DateTime, 1.2)
        ],
        Rows = users.Select(u => new object?[]
        {
            u.CodigoId,
            u.AliasUsuario,
            u.NombreCompleto,
            u.MailUsuario,
            u.FonoUsuario,
            u.PerfilDisplay,
            string.IsNullOrWhiteSpace(u.NombreGrupo) ? "Sin grupo" : u.NombreGrupo,
            u.Activo ? "Activo" : "Inactivo",
            u.FechaCreacion,
            u.UltimoAcceso
        }).ToList()
    };

    public static TabularReportData BuildGroupsReport(IReadOnlyList<UserGroup> groups, string? filterSummary = null) => new()
    {
        Title = "REPORTE DE GRUPOS DE TRABAJO",
        SheetName = "Grupos",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Catálogo institucional de grupos de trabajo" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.5),
            new("Grupo", ExportColumnKind.Text, 1.4),
            new("Descripción", ExportColumnKind.Text, 2.8),
            new("Responsable", ExportColumnKind.Text, 1.4),
            new("Integrantes", ExportColumnKind.Integer, 0.8, SumInFooter: true),
            new("Estado", ExportColumnKind.Text, 0.8),
            new("Fecha Creación", ExportColumnKind.DateTime, 1.2),
            new("Modificación", ExportColumnKind.DateTime, 1.2)
        ],
        Rows = groups.Select(g => new object?[]
        {
            g.IdGrupo,
            g.Nombre,
            g.Descripcion,
            string.IsNullOrWhiteSpace(g.AliasResponsable) ? "Sin asignar" : g.AliasResponsable,
            g.CantidadIntegrantes,
            g.Activo ? "Activo" : "Inactivo",
            g.FechaCreacion,
            g.FechaModificacion
        }).ToList()
    };

    public static TabularReportData BuildGroupMovementsReport(IReadOnlyList<GroupMovementEntry> rows, string? filterSummary = null) => new()
    {
        Title = "HISTORIAL DE MOVIMIENTOS ENTRE GRUPOS",
        SheetName = "Movimientos Grupos",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("Fecha y Hora", ExportColumnKind.DateTime, 1.2),
            new("Usuario", ExportColumnKind.Text, 1.1),
            new("Grupo Origen", ExportColumnKind.Text, 1.3),
            new("Grupo Destino", ExportColumnKind.Text, 1.3),
            new("Ejecutado Por", ExportColumnKind.Text, 1.1),
            new("Motivo", ExportColumnKind.Text, 2.5)
        ],
        Rows = rows.Select(m => new object?[]
        {
            m.FechaMovimiento, m.AliasUsuario, m.NombreGrupoOrigen, m.NombreGrupoDestino, m.EjecutadoPor, m.Motivo
        }).ToList()
    };

    public static TabularReportData BuildProvidersReport(IReadOnlyList<Provider> providers, string? filterSummary = null) => new()
    {
        Title = "CATÁLOGO DE PROVEEDORES",
        SheetName = "Proveedores",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Listado de proveedores" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.5),
            new("Proveedor", ExportColumnKind.Text, 2.0),
            new("Dirección", ExportColumnKind.Text, 2.0),
            new("Teléfono", ExportColumnKind.Text, 1.0),
            new("Correo", ExportColumnKind.Text, 1.6),
            new("Contacto", ExportColumnKind.Text, 1.3),
            new("Estado", ExportColumnKind.Text, 0.7)
        ],
        Rows = providers.Select(p => new object?[]
        {
            p.IdProv, p.Nombre, p.Direccion, p.Telefono, p.Correo, p.Contacto, p.Activo ? "Activo" : "Inactivo"
        }).ToList()
    };

    public static TabularReportData BuildAssetTreeReport(IReadOnlyList<AssetTreeExportRow> nodes, string? filterSummary = null) => new()
    {
        Title = "ESTRUCTURA JERÁRQUICA DEL ÁRBOL DE ACTIVOS",
        SheetName = "Árbol de Activos",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Subárbol completo (orden jerárquico)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.55),
            new("Nivel", ExportColumnKind.Integer, 0.55),
            new("Tipo", ExportColumnKind.Text, 1.0),
            new("Descripción", ExportColumnKind.Text, 2.2),
            new("Ruta Jerárquica", ExportColumnKind.Text, 3.0),
            new("Código SAP", ExportColumnKind.Text, 1.0),
            new("Fabricante", ExportColumnKind.Text, 1.1),
            new("Modelo", ExportColumnKind.Text, 1.0),
            new("Estado", ExportColumnKind.Text, 0.7)
        ],
        Rows = nodes.Select(n =>
        {
            string prefix = n.Depth > 1 ? string.Concat(Enumerable.Repeat("· ", n.Depth - 1)) : string.Empty;
            return new object?[]
            {
                n.IdActivo,
                n.Depth,
                AssetNodeTypes.GetLabel(n.Categoria),
                $"{prefix}{n.Descripcion}",
                n.Ruta,
                n.CodSap,
                n.Fabricante,
                n.Modelo,
                n.Activo ? "Activo" : "Inactivo"
            };
        }).ToList()
    };

    public static TabularReportData BuildAuditReport(IReadOnlyList<AuditEntry> rows, string? filterSummary = null) => new()
    {
        Title = "BITÁCORA DE AUDITORÍA Y SEGURIDAD",
        SheetName = "Auditoría",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.6),
            new("Fecha y Hora", ExportColumnKind.DateTime, 1.3),
            new("Usuario", ExportColumnKind.Text, 1.1),
            new("Módulo", ExportColumnKind.Text, 1.2),
            new("Operación", ExportColumnKind.Text, 1.5),
            new("Detalle", ExportColumnKind.Text, 3.3)
        ],
        Rows = rows.Select(a => new object?[]
        {
            a.IdLog, a.Fecha, a.Usuario, a.Modulo, a.Accion, a.Detalle
        }).ToList()
    };

    private static TabularReportData BuildSingleWorkOrderTabular(WorkOrder order)
    {
        List<ExportColumn> cols =
        [
            new("N° OT", ExportColumnKind.Integer, 0.7),
            new("Fecha", ExportColumnKind.Date, 0.9),
            new("Proveedor", ExportColumnKind.Text, 1.8),
            new("Solicitante", ExportColumnKind.Text, 1.2),
            new("Cant.", ExportColumnKind.Integer, 0.6, SumInFooter: true),
            new("Detalle", ExportColumnKind.Text, 2.8),
            new("Observaciones", ExportColumnKind.Text, 1.6)
        ];
        if (order.MostrarValorNeto)
        {
            cols.Add(new("Valor Neto", ExportColumnKind.Currency, 0.9));
        }

        cols.Add(new("Estado", ExportColumnKind.Text, 0.8));
        cols.Add(new("Firmas", ExportColumnKind.Text, 1.2));
        cols.Add(new("Activo", ExportColumnKind.Text, 1.7));

        return new TabularReportData
        {
            Title = $"ORDEN DE TRABAJO {order.TipoOrden.ToUpperInvariant()} N° {order.NumeroOt:D6}",
            SheetName = $"OT_{order.NumeroOt}",
            FilterSummary = order.MostrarValorNeto
                ? $"Proveedor: {order.NombreProveedor} | Fecha: {order.FechaOt:dd-MM-yyyy} | Valor Neto: $ {order.ValorNeto:N0}"
                : $"Proveedor: {order.NombreProveedor} | Fecha: {order.FechaOt:dd-MM-yyyy}",
            Landscape = false,
            Columns = cols,
            Rows = order.Detalles.Select(d =>
            {
                List<object?> row =
                [
                    order.NumeroOt, order.FechaOt, order.NombreProveedor, order.SolicitanteDisplayNombre,
                    d.Cantidad, d.Descripcion, order.Observacion
                ];
                if (order.MostrarValorNeto)
                {
                    row.Add(order.ValorNeto);
                }

                row.Add(order.Estado.ToDisplayName());
                row.Add(order.FirmasSeleccionadas.FormatResumen());
                row.Add(order.RutaActivo);
                return row.ToArray();
            }).ToList()
        };
    }
}
