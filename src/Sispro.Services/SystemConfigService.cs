using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Enums;
using Sispro.Core.Interfaces;
using Sispro.Core.Validation;

namespace Sispro.Services;

/// <summary>
/// Servicio centralizado de configuración del sistema con control estricto RBAC (solo Administrador)
/// y tres operaciones diferenciadas: Probar, Aplicar (temporal) y Guardar (persistente en BD).
/// </summary>
public sealed class SystemConfigService
{
    public const string KeyCompanyName = "CompanyName";
    public const string KeyCompanyRut = "CompanyRut";
    public const string KeyCompanyDepartment = "CompanyDepartment";
    public const string KeyCompanyAddress = "CompanyAddress";
    public const string KeyCompanyCity = "CompanyCity";

    private readonly SisproApplicationContext _ctx;
    private AppSettings? _persistedSnapshot;

    public bool HasTemporaryChanges { get; private set; }

    public SystemConfigService(SisproApplicationContext ctx)
    {
        _ctx = ctx;
    }

    public static AppSettings CloneSettings(AppSettings src) => new()
    {
        DatabasePath = src.DatabasePath,
        CompanyName = src.CompanyName,
        CompanyRut = src.CompanyRut,
        CompanyDepartment = src.CompanyDepartment,
        CompanyAddress = src.CompanyAddress,
        CompanyCity = src.CompanyCity,
        LegacyAccessTreePath = src.LegacyAccessTreePath
    };

    /// <summary>
    /// Hidrata la configuración activa desde la tabla centralizada ConfiguracionSistema de la BD.
    /// </summary>
    public void LoadFromDatabaseIntoSettings(AppSettings target)
    {
        IReadOnlyDictionary<string, string> dbValues = _ctx.SystemConfig.GetAll();
        if (dbValues.TryGetValue(KeyCompanyName, out string? name) && !string.IsNullOrWhiteSpace(name))
        {
            target.CompanyName = name.Trim();
        }

        if (dbValues.TryGetValue(KeyCompanyRut, out string? rut))
        {
            target.CompanyRut = rut.Trim();
        }

        if (dbValues.TryGetValue(KeyCompanyDepartment, out string? dept) && !string.IsNullOrWhiteSpace(dept))
        {
            target.CompanyDepartment = dept.Trim();
        }

        if (dbValues.TryGetValue(KeyCompanyAddress, out string? addr) && !string.IsNullOrWhiteSpace(addr))
        {
            target.CompanyAddress = addr.Trim();
        }

        if (dbValues.TryGetValue(KeyCompanyCity, out string? city) && !string.IsNullOrWhiteSpace(city))
        {
            target.CompanyCity = city.Trim();
        }
    }

    /// <summary>
    /// PROBAR: valida parámetros, compatibilidad de BD y permisos sin modificar la configuración activa ni la BD.
    /// </summary>
    public SystemConfigTestResult TestConfiguration(UserSession? session, AppSettings candidate)
    {
        AuthService.EnsureAdministrator(session, _ctx.Users, _ctx.Audit, "Configuración", "Probar configuración del sistema");
        return ValidateCandidate(candidate);
    }

    /// <summary>
    /// APLICAR: aplica temporalmente los cambios en la sesión activa sin sobrescribir la configuración persistida en BD ni settings.json.
    /// </summary>
    public SystemConfigTestResult ApplyTemporaryConfiguration(UserSession? session, AppSettings candidate)
    {
        AuthService.EnsureAdministrator(session, _ctx.Users, _ctx.Audit, "Configuración", "Aplicar configuración temporal del sistema");

        SystemConfigTestResult test = ValidateCandidate(candidate);
        if (!test.Succeeded)
        {
            return test;
        }

        _persistedSnapshot ??= CloneSettings(_ctx.Settings);

        AppSettings normalized = Normalize(candidate);
        bool dbChanged = !string.Equals(_ctx.Settings.DatabasePath, normalized.DatabasePath, StringComparison.OrdinalIgnoreCase);

        if (dbChanged)
        {
            _ctx.SwitchDatabaseTemporary(normalized.DatabasePath, normalized);
        }
        else
        {
            CopyCompanySettings(normalized, _ctx.Settings);
            _ctx.NotifyConfigurationChanged(isTemporary: true);
        }

        HasTemporaryChanges = true;
        return new SystemConfigTestResult(
            true,
            test.Checks,
            [],
            "Configuración aplicada temporalmente en la interfaz activa. Presione 'Guardar' para hacer los cambios permanentes en la base de datos.");
    }

    /// <summary>
    /// Revierte cambios aplicados temporalmente restaurando la última configuración persistida.
    /// </summary>
    public void RevertTemporaryConfiguration(UserSession? session)
    {
        AuthService.EnsureAdministrator(session, _ctx.Users, _ctx.Audit, "Configuración", "Revertir configuración temporal");
        if (!HasTemporaryChanges || _persistedSnapshot is null)
        {
            return;
        }

        AppSettings original = CloneSettings(_persistedSnapshot);
        bool dbChanged = !string.Equals(_ctx.Settings.DatabasePath, original.DatabasePath, StringComparison.OrdinalIgnoreCase);
        if (dbChanged)
        {
            _ctx.SwitchDatabaseTemporary(original.DatabasePath, original);
        }
        else
        {
            CopyCompanySettings(original, _ctx.Settings);
            _ctx.NotifyConfigurationChanged(isTemporary: false);
        }

        HasTemporaryChanges = false;
        _persistedSnapshot = null;
    }

    /// <summary>
    /// GUARDAR: valida y persiste definitivamente la configuración en la tabla centralizada ConfiguracionSistema de la BD y settings.json.
    /// </summary>
    public SystemConfigTestResult SaveConfiguration(UserSession? session, AppSettings candidate)
    {
        AuthService.EnsureAdministrator(session, _ctx.Users, _ctx.Audit, "Configuración", "Guardar configuración permanente del sistema");

        SystemConfigTestResult test = ValidateCandidate(candidate);
        if (!test.Succeeded)
        {
            return test;
        }

        AppSettings normalized = Normalize(candidate);
        bool dbChanged = !string.Equals(_ctx.Settings.DatabasePath, normalized.DatabasePath, StringComparison.OrdinalIgnoreCase);

        CopyCompanySettings(normalized, _ctx.Settings);

        if (dbChanged)
        {
            _ctx.ReloadDatabase(normalized.DatabasePath, preserveProvidedCompanySettings: true);
        }
        else
        {
            AppSettingsStore.Save(_ctx.Settings);
        }

        Dictionary<string, string> kv = new(StringComparer.OrdinalIgnoreCase)
        {
            [KeyCompanyName] = _ctx.Settings.CompanyName,
            [KeyCompanyRut] = _ctx.Settings.CompanyRut,
            [KeyCompanyDepartment] = _ctx.Settings.CompanyDepartment,
            [KeyCompanyAddress] = _ctx.Settings.CompanyAddress,
            [KeyCompanyCity] = _ctx.Settings.CompanyCity
        };

        string actor = session!.Usuario.AliasUsuario;
        _ctx.SystemConfig.SaveAll(kv, actor);

        HasTemporaryChanges = false;
        _persistedSnapshot = null;

        _ctx.Audit.Log(
            actor,
            "Configuración",
            "GUARDAR_CONFIGURACION",
            $"Configuración guardada en BD ({_ctx.Settings.CompanyName}, RUT {_ctx.Settings.CompanyRut}, BD: {_ctx.Settings.DatabasePath}).");
        _ctx.SyncHub.Publish(dbChanged ? DataChangeKind.All : DataChangeKind.Catalogs | DataChangeKind.Audit, "GUARDAR_CONFIGURACION");
        _ctx.NotifyConfigurationChanged(isTemporary: false);

        return new SystemConfigTestResult(
            true,
            test.Checks,
            [],
            "Configuración validada y guardada definitivamente en la base de datos.");
    }

    private static AppSettings Normalize(AppSettings src)
    {
        string rawRut = src.CompanyRut?.Trim() ?? string.Empty;
        return new AppSettings
        {
            DatabasePath = Path.GetFullPath((src.DatabasePath ?? string.Empty).Trim()),
            CompanyName = (src.CompanyName ?? string.Empty).Trim(),
            CompanyRut = string.IsNullOrEmpty(rawRut) ? string.Empty : RutValidator.Format(rawRut),
            CompanyDepartment = (src.CompanyDepartment ?? string.Empty).Trim(),
            CompanyAddress = (src.CompanyAddress ?? string.Empty).Trim(),
            CompanyCity = string.IsNullOrWhiteSpace(src.CompanyCity) ? "VALPARAÍSO" : src.CompanyCity.Trim(),
            LegacyAccessTreePath = (src.LegacyAccessTreePath ?? string.Empty).Trim()
        };
    }

    private static void CopyCompanySettings(AppSettings from, AppSettings to)
    {
        to.CompanyName = from.CompanyName;
        to.CompanyRut = from.CompanyRut;
        to.CompanyDepartment = from.CompanyDepartment;
        to.CompanyAddress = from.CompanyAddress;
        to.CompanyCity = from.CompanyCity;
    }

    private static SystemConfigTestResult ValidateCandidate(AppSettings candidate)
    {
        List<string> checks = [];
        List<string> errors = [];

        if (candidate is null)
        {
            return new SystemConfigTestResult(false, [], ["Los parámetros de configuración no pueden ser nulos."], "Parámetros inválidos.");
        }

        // 1. Razón social
        string company = (candidate.CompanyName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(company))
        {
            errors.Add("Debe ingresar la razón social o nombre de la empresa.");
        }
        else if (company.Length > 150)
        {
            errors.Add("El nombre de la empresa excede los 150 caracteres permitidos.");
        }
        else
        {
            checks.Add($"Empresa válida: {company}");
        }

        // 2. RUT
        string rut = (candidate.CompanyRut ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(rut))
        {
            if (!RutValidator.IsValid(rut))
            {
                errors.Add("El RUT de la empresa no es válido (verifique dígitos y dígito verificador).");
            }
            else
            {
                checks.Add($"RUT válido: {RutValidator.Format(rut)}");
            }
        }
        else
        {
            checks.Add("RUT empresa: opcional (sin definir)");
        }

        // 3. Departamento y Dirección
        if ((candidate.CompanyDepartment ?? string.Empty).Trim().Length > 120)
        {
            errors.Add("El nombre del departamento excede los 120 caracteres.");
        }
        else
        {
            checks.Add("Departamento: formato válido");
        }

        if ((candidate.CompanyAddress ?? string.Empty).Trim().Length > 200)
        {
            errors.Add("La dirección de planta excede los 200 caracteres.");
        }
        else
        {
            checks.Add("Dirección de planta: formato válido");
        }

        // 4. Ruta de Base de Datos SQLite (lectura y compatibilidad sin modificar la BD)
        string rawDbPath = (candidate.DatabasePath ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawDbPath))
        {
            errors.Add("Debe indicar una ruta de base de datos SQLite válida.");
        }
        else
        {
            try
            {
                string fullDbPath = Path.GetFullPath(rawDbPath);
                string ext = Path.GetExtension(fullDbPath);
                if (!string.Equals(ext, ".db", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".sqlite", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ext, ".sqlite3", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("El archivo de base de datos debe tener extensión .db, .sqlite o .sqlite3.");
                }
                else if (File.Exists(fullDbPath))
                {
                    SqliteConnectionStringBuilder csb = new()
                    {
                        DataSource = fullDbPath,
                        Mode = SqliteOpenMode.ReadOnly,
                        Pooling = false
                    };
                    using SqliteConnection testConn = new(csb.ToString());
                    testConn.Open();

                    using SqliteCommand integrityCmd = testConn.CreateCommand();
                    integrityCmd.CommandText = "PRAGMA quick_check;";
                    string integrity = Convert.ToString(integrityCmd.ExecuteScalar()) ?? string.Empty;
                    if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add($"La base de datos seleccionada presenta errores de integridad SQLite ({integrity}).");
                    }
                    else
                    {
                        using SqliteCommand tblCmd = testConn.CreateCommand();
                        tblCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Usuarios','Proveedores','OrdenesTrabajo');";
                        List<string> foundTables = [];
                        using SqliteDataReader rdr = tblCmd.ExecuteReader();
                        while (rdr.Read())
                        {
                            foundTables.Add(rdr.GetString(0));
                        }

                        if (foundTables.Count == 3)
                        {
                            checks.Add($"Base de datos SQLite compatible y verificada ({Path.GetFileName(fullDbPath)}).");
                        }
                        else if (foundTables.Count == 0)
                        {
                            checks.Add($"Archivo SQLite vacío compatible (se inicializará esquema SISPRO al guardar).");
                        }
                        else
                        {
                            errors.Add("El archivo SQLite seleccionado no corresponde a una estructura SISPRO completa.");
                        }
                    }
                }
                else
                {
                    string? parentDir = Path.GetDirectoryName(fullDbPath);
                    if (string.IsNullOrWhiteSpace(parentDir) || !Directory.Exists(parentDir))
                    {
                        errors.Add($"La carpeta destino de la base de datos no existe o no es accesible: {parentDir}");
                    }
                    else
                    {
                        checks.Add($"Carpeta destino accesible (se creará una nueva base SQLite en {fullDbPath}).");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"No fue posible validar la ruta de base de datos: {ex.Message}");
            }
        }

        bool ok = errors.Count == 0;
        string summary = ok
            ? "Prueba exitosa: todos los parámetros y la base de datos son compatibles con SISPRO."
            : $"Se detectaron {errors.Count} problema(s) de validación en la configuración.";

        return new SystemConfigTestResult(ok, checks, errors, summary);
    }
}
