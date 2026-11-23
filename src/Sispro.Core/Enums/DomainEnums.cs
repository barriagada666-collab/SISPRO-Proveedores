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
