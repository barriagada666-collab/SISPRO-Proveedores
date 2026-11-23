using System.Globalization;
using System.Text.RegularExpressions;

namespace Sispro.Core.Validation;

/// <summary>
/// Validador y formateador de RUT chileno (módulo 11).
/// </summary>
public static class RutValidator
{
    public static string Normalize(string? rawRut)
    {
        if (string.IsNullOrWhiteSpace(rawRut))
        {
            return string.Empty;
        }

        string cleaned = rawRut.Trim().ToUpperInvariant()
            .Replace(".", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return cleaned;
    }

    public static (string Body, string Dv) Split(string? rawRut)
    {
        string normalized = Normalize(rawRut);
        if (normalized.Length < 2)
        {
            return (normalized, string.Empty);
        }

        return (normalized[..^1], normalized[^1..]);
    }

    public static string CalculateCheckDigit(int rutNumber)
    {
        if (rutNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rutNumber), "El cuerpo del RUT debe ser mayor a cero.");
        }

        int sum = 0;
        int multiplier = 2;
        int temp = rutNumber;

        while (temp > 0)
        {
            sum += (temp % 10) * multiplier;
            temp /= 10;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }

        int remainder = 11 - (sum % 11);
        return remainder switch
        {
            11 => "0",
            10 => "K",
            _ => remainder.ToString(CultureInfo.InvariantCulture)
        };
    }

    public static bool IsValid(string? body, string? dv)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(dv))
        {
            return false;
        }

        string cleanBody = body.Trim().Replace(".", string.Empty);
        string cleanDv = dv.Trim().ToUpperInvariant();

        if (cleanBody.Length < 6 || cleanBody.Length > 9 || !int.TryParse(cleanBody, out int number) || number <= 0)
        {
            return false;
        }

        if (cleanDv.Length != 1 || !(char.IsDigit(cleanDv[0]) || cleanDv[0] == 'K'))
        {
            return false;
        }

        return CalculateCheckDigit(number) == cleanDv;
    }

    public static bool IsValid(string? fullRut)
    {
        (string body, string dv) = Split(fullRut);
        return IsValid(body, dv);
    }

    public static string Format(string? fullRut)
    {
        (string body, string dv) = Split(fullRut);
        if (string.IsNullOrEmpty(body) || string.IsNullOrEmpty(dv))
        {
            return fullRut?.Trim() ?? string.Empty;
        }

        if (!int.TryParse(body, out int number))
        {
            return $"{body}-{dv}";
        }

        string formattedBody = number.ToString("#,##0", new CultureInfo("es-CL"));
        return $"{formattedBody}-{dv}";
    }
}

public static class TaxCalculator
{
    public const decimal IvaRate = 0.19m;

    public static decimal CalculateIva(decimal netAmount)
    {
        if (netAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(netAmount), "El monto neto no puede ser negativo.");
        }

        return Math.Round(netAmount * IvaRate, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateTotalWithIva(decimal netAmount)
    {
        return Math.Round(netAmount, 0, MidpointRounding.AwayFromZero) + CalculateIva(netAmount);
    }
}

public static partial class DomainValidator
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    public static bool IsValidEmailOrEmpty(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        return EmailRegex().IsMatch(email.Trim());
    }

    public static string? ValidatePasswordPolicy(string? password, string? confirmPassword)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return "Debe ingresar una contraseña.";
        }

        if (password.Length < 6)
        {
            return "La contraseña debe tener al menos 6 caracteres.";
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return "La contraseña y su confirmación no coinciden.";
        }

        return null;
    }

    public static string? ValidateDateRange(DateTime? desde, DateTime? hasta)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
        {
            return "La fecha inicial ('Desde') no puede ser posterior a la fecha final ('Hasta').";
        }

        return null;
    }

    public static (DateTime? Desde, DateTime? Hasta) ResolveDatePreset(Enums.DateRangePreset preset, DateTime? referenceDate = null)
    {
        DateTime today = (referenceDate ?? DateTime.Today).Date;
        return preset switch
        {
            Enums.DateRangePreset.Hoy => (today, today),
            Enums.DateRangePreset.UltimaSemana => (today.AddDays(-6), today),
            Enums.DateRangePreset.UltimoMes => (today.AddMonths(-1), today),
            Enums.DateRangePreset.EsteAnio => (new DateTime(today.Year, 1, 1), today),
            _ => (null, null)
        };
    }
}
