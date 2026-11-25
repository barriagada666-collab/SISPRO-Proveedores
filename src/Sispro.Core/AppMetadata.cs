using System.Reflection;

namespace Sispro.Core;

/// <summary>
/// Metadatos centralizados de versión, fecha de lanzamiento e identidad del software,
/// obtenidos directamente desde los atributos de ensamblado definidos en Directory.Build.props.
/// </summary>
public static class AppMetadata
{
    private static Assembly SourceAssembly => typeof(AppMetadata).Assembly;

    public static string ShortName => "SISPRO";

    public static string SoftwareName
    {
        get
        {
            string? product = SourceAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
            return string.IsNullOrWhiteSpace(product)
                ? "SISPRO — Gestión de Proveedores y Órdenes de Trabajo"
                : product.Trim();
        }
    }

    public static string Version
    {
        get
        {
            Version? v = SourceAssembly.GetName().Version ?? typeof(AppMetadata).Assembly.GetName().Version;
            return v is not null ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}" : "4.6.0";
        }
    }

    public static string ShortVersion
    {
        get
        {
            Version? v = SourceAssembly.GetName().Version ?? typeof(AppMetadata).Assembly.GetName().Version;
            return v is not null ? $"{v.Major}.{v.Minor}" : "4.6";
        }
    }

    public static string ReleaseDate
    {
        get
        {
            string? meta = GetAssemblyMetadata(SourceAssembly, "ReleaseDate")
                ?? GetAssemblyMetadata(typeof(AppMetadata).Assembly, "ReleaseDate");
            return string.IsNullOrWhiteSpace(meta) ? "25-11-2026" : meta.Trim();
        }
    }

    public static string DeveloperCompany
    {
        get
        {
            string? company = SourceAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company
                ?? typeof(AppMetadata).Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
            return string.IsNullOrWhiteSpace(company) ? "Cambiaso Hnos S.A.C." : company.Trim();
        }
    }

    public static string Description
    {
        get
        {
            string? desc = SourceAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description
                ?? typeof(AppMetadata).Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
            return string.IsNullOrWhiteSpace(desc)
                ? "Sistema corporativo de gestión de proveedores, órdenes de trabajo, activos y usuarios (.NET 10)."
                : desc.Trim();
        }
    }

    private static string? GetAssemblyMetadata(Assembly assembly, string key) =>
        assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;
}
