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

    public static TabularReportData BuildWorkOrdersReport(IReadOnlyList<WorkOrderSearchRow> rows, string? filterSummary = null) => new()
    {
        Title = "REPORTE DE ÓRDENES DE TRABAJO EXTERNAS",
        SheetName = "Órdenes de Trabajo",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("N° OT", ExportColumnKind.Integer, 0.7),
            new("Fecha", ExportColumnKind.Date, 0.9),
            new("Proveedor", ExportColumnKind.Text, 1.8),
            new("Solicitante", ExportColumnKind.Text, 1.1),
            new("Cant.", ExportColumnKind.Integer, 0.55, SumInFooter: true),
            new("Detalle del Trabajo", ExportColumnKind.Text, 2.8),
            new("Ruta de Activo", ExportColumnKind.Text, 1.7),
            new("VB Sup.", ExportColumnKind.Text, 0.6),
            new("VB Jef.", ExportColumnKind.Text, 0.6),
            new("Fact.", ExportColumnKind.Text, 0.6)
        ],
        Rows = rows.Select(r => new object?[]
        {
            r.NumeroOt, r.Fecha, r.Proveedor, r.Solicitante, r.Cantidad,
            r.Detalle, r.RutaActivo, r.TieneVbs ? "Sí" : "No", r.TieneVbj ? "Sí" : "No", r.AsociadoAFactura ? "Sí" : "No"
        }).ToList()
    };

    public static TabularReportData BuildInvoicesReport(IReadOnlyList<InvoiceSearchRow> rows, string? filterSummary = null) => new()
    {
        Title = "REPORTE DE FACTURAS Y GUÍAS DE DESPACHO",
        SheetName = "Facturas y Guías",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("N° Documento", ExportColumnKind.Integer, 0.9),
            new("Fecha", ExportColumnKind.Date, 0.9),
            new("Proveedor", ExportColumnKind.Text, 2.0),
            new("Recibido Por", ExportColumnKind.Text, 1.2),
            new("Ítems", ExportColumnKind.Integer, 0.6, SumInFooter: true),
            new("Neto ($)", ExportColumnKind.Currency, 1.0, SumInFooter: true),
            new("IVA 19% ($)", ExportColumnKind.Currency, 0.95, SumInFooter: true),
            new("Total c/IVA ($)", ExportColumnKind.Currency, 1.05, SumInFooter: true),
            new("Detalle", ExportColumnKind.Text, 2.0)
        ],
        Rows = rows.Select(r => new object?[]
        {
            r.NumeroDocumento, r.Fecha, r.Proveedor, r.RecibidoPor, r.CantidadItems,
            r.TotalNeto, r.Iva, r.TotalConIva, r.Detalle
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
            u.Nivel.ToString(),
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

    public static TabularReportData BuildMaintenanceOrdersReport(IReadOnlyList<MantWorkOrderRow> rows, string? filterSummary = null) => new()
    {
        Title = "REPORTE DE OTs DE MANTENCIÓN INTERNA (BD MANTENCION)",
        SheetName = "OTs Mantención",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Orden cronológico descendente (más recientes primero)" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("N° OT", ExportColumnKind.Text, 0.8),
            new("Inicio", ExportColumnKind.Text, 0.9),
            new("Término", ExportColumnKind.Text, 0.9),
            new("Oficio", ExportColumnKind.Text, 1.0),
            new("Planta", ExportColumnKind.Text, 1.2),
            new("Equipo", ExportColumnKind.Text, 1.4),
            new("Estado", ExportColumnKind.Text, 0.9),
            new("Solicitada Por", ExportColumnKind.Text, 1.1),
            new("Descripción", ExportColumnKind.Text, 2.4)
        ],
        Rows = rows.Select(o => new object?[]
        {
            o.NumeroOt, o.FechaInicio, o.FechaFin, o.Oficio, o.Planta, o.Equipo, o.Estado, o.SolicitadaPor, o.DescripcionBreve
        }).ToList()
    };

    public static TabularReportData BuildSuppliesReport(IReadOnlyList<MantSupplyRow> rows, string? filterSummary = null) => new()
    {
        Title = "CATÁLOGO DE INSUMOS Y REPUESTOS SAP",
        SheetName = "Insumos SAP",
        FilterSummary = string.IsNullOrWhiteSpace(filterSummary) ? "Catálogo de repuestos e insumos" : filterSummary,
        Landscape = true,
        Columns =
        [
            new("ID", ExportColumnKind.Integer, 0.6),
            new("Código SAP", ExportColumnKind.Text, 1.1),
            new("Cód. Proveedor", ExportColumnKind.Text, 1.1),
            new("Descripción", ExportColumnKind.Text, 3.0),
            new("Stock", ExportColumnKind.Decimal, 0.8, SumInFooter: true),
            new("Días Ent.", ExportColumnKind.Integer, 0.7),
            new("Prioridad", ExportColumnKind.Integer, 0.7),
            new("Usos Hist.", ExportColumnKind.Integer, 0.8, SumInFooter: true)
        ],
        Rows = rows.Select(s => new object?[]
        {
            s.IdItem, s.CodSap, s.CodProveedor, s.Descripcion, s.StockUnidad, s.DiasEntrega, s.Prioridad, s.UsosHistoricos
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

    private static TabularReportData BuildSingleWorkOrderTabular(WorkOrder order) => new()
    {
        Title = $"ORDEN DE TRABAJO EXTERNA N° {order.NumeroOt:D6}",
        SheetName = $"OT_{order.NumeroOt}",
        FilterSummary = $"Proveedor: {order.NombreProveedor} | Fecha: {order.FechaOt:dd-MM-yyyy}",
        Landscape = false,
        Columns =
        [
            new("N° OT", ExportColumnKind.Integer, 0.7),
            new("Fecha", ExportColumnKind.Date, 0.9),
            new("Proveedor", ExportColumnKind.Text, 1.8),
            new("Solicitante", ExportColumnKind.Text, 1.2),
            new("Cant.", ExportColumnKind.Integer, 0.6, SumInFooter: true),
            new("Detalle", ExportColumnKind.Text, 3.2),
            new("Activo", ExportColumnKind.Text, 1.8)
        ],
        Rows = order.Detalles.Select(d => new object?[]
        {
            order.NumeroOt, order.FechaOt, order.NombreProveedor, order.AliasSolicitante,
            d.Cantidad, d.Descripcion, order.RutaActivo
        }).ToList()
    };
}
