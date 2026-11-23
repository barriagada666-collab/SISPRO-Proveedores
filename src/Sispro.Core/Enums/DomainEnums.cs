namespace Sispro.Core.Enums;

public enum WorkOrderStatus
{
    Abierta = 0,
    ConGuiaParcial = 1,
    Facturada = 2,
    Anulada = 3
}

public enum ApprovalType
{
    Supervisor,
    Jefatura
}

public enum UserRoleLevel
{
    Usuario = 1,
    Supervisor = 2,
    Jefatura = 3,
    Administrador = 4
}

public enum SignatureOption
{
    Ninguna = 0,
    JuanRamos = 1,
    MarceloSilva = 2,
    Ambas = 3
}

[Flags]
public enum DataChangeKind
{
    None = 0,
    Users = 1,
    Groups = 2,
    WorkOrders = 4,
    Invoices = 8,
    Catalogs = 16,
    Audit = 32,
    All = Users | Groups | WorkOrders | Invoices | Catalogs | Audit
}

public enum DateRangePreset
{
    Todos = 0,
    Hoy = 1,
    UltimaSemana = 2,
    UltimoMes = 3,
    EsteAnio = 4,
    Personalizado = 5
}

public enum ExportFormat
{
    Excel = 0,
    Pdf = 1,
    Word = 2,
    Csv = 3
}
