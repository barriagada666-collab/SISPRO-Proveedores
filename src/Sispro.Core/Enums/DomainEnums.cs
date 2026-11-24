namespace Sispro.Core.Enums;

public enum WorkOrderStatus
{
    Abierta = 0,
    EnProceso = 1,
    Completada = 2,
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

[Flags]
public enum SignatureOption
{
    Ninguna = 0,
    JuanRamos = 1,
    MarceloSilva = 2,
    Ambas = JuanRamos | MarceloSilva,
    Solicitante = 4,
    JefaturaYTecnico = JuanRamos | Solicitante,
    SupervisorYTecnico = MarceloSilva | Solicitante,
    AmbasConSolicitante = JuanRamos | MarceloSilva | Solicitante
}

public static class SignatureOptionExtensions
{
    public static bool IncludeJefatura(this SignatureOption option) =>
        (option & SignatureOption.JuanRamos) == SignatureOption.JuanRamos;

    public static bool IncludeSupervisor(this SignatureOption option) =>
        (option & SignatureOption.MarceloSilva) == SignatureOption.MarceloSilva;

    public static bool IncludeTecnico(this SignatureOption option) =>
        (option & SignatureOption.Solicitante) == SignatureOption.Solicitante;

    public static SignatureOption FromFlags(bool jefatura, bool supervisor, bool tecnico)
    {
        SignatureOption option = SignatureOption.Ninguna;
        if (jefatura) option |= SignatureOption.JuanRamos;
        if (supervisor) option |= SignatureOption.MarceloSilva;
        if (tecnico) option |= SignatureOption.Solicitante;
        return option;
    }

    public static string FormatResumen(this SignatureOption option)
    {
        if (option == SignatureOption.Ninguna)
        {
            return "Sin firmas";
        }

        List<string> parts = [];
        if (option.IncludeJefatura()) parts.Add("Jefatura");
        if (option.IncludeTecnico()) parts.Add("Técnico");
        if (option.IncludeSupervisor()) parts.Add("Supervisor");
        return parts.Count == 0 ? "Sin firmas" : string.Join(", ", parts);
    }
}

[Flags]
public enum DataChangeKind
{
    None = 0,
    Users = 1,
    Groups = 2,
    WorkOrders = 4,
    Catalogs = 8,
    Audit = 16,
    All = Users | Groups | WorkOrders | Catalogs | Audit
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
