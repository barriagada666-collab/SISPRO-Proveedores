using System.Globalization;
using System.Text;

namespace Sispro.Core.Entities;

/// <summary>
/// Catálogo institucional de tipos estructurales para nodos del árbol de activos de Cambiaso.
/// Normaliza variantes históricas importadas desde Access sin pérdida de información.
/// </summary>
public static class AssetNodeTypes
{
    public const string Empresa = "EMPRESA";
    public const string Planta = "PLANTA";
    public const string Seccion = "SECCION";
    public const string Maquina = "MAQUINA";
    public const string Modulo = "MODULO";
    public const string Componente = "COMPONENTE";
    public const string SinClasificar = "";

    public static IReadOnlyList<(string Code, string Label)> All { get; } =
    [
        (Empresa, "Empresa"),
        (Planta, "Planta"),
        (Seccion, "Sección / Área"),
        (Maquina, "Máquina"),
        (Modulo, "Módulo"),
        (Componente, "Componente"),
        (SinClasificar, "Sin clasificar")
    ];

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SinClasificar;
        }

        string formD = raw.Trim().Normalize(NormalizationForm.FormD);
        StringBuilder sb = new(formD.Length);
        foreach (char c in formD)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        string upper = sb.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant().Trim();
        return upper switch
        {
            "EMPRESA" => Empresa,
            "PLANTA" => Planta,
            "AREA" or "SECCION" or "SECCION / AREA" => Seccion,
            "MAQUINA" => Maquina,
            "MODULO" => Modulo,
            "COMPONENTE" => Componente,
            "SIN CLASIFICAR" => SinClasificar,
            _ => upper
        };
    }

    public static string GetLabel(string? code)
    {
        string normalized = Normalize(code);
        foreach ((string c, string label) in All)
        {
            if (string.Equals(c, normalized, StringComparison.Ordinal))
            {
                return label;
            }
        }

        return string.IsNullOrEmpty(normalized) ? "Sin clasificar" : normalized;
    }

    public static string SuggestChildType(string? parentCode) => Normalize(parentCode) switch
    {
        Empresa => Planta,
        Planta => Seccion,
        Seccion => Maquina,
        Maquina => Modulo,
        Modulo => Componente,
        Componente => Componente,
        _ => SinClasificar
    };
}
