using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Core.Validation;
using Xunit;

namespace Sispro.Tests.Unit;

public sealed class CoreUnitTests
{
    [Theory]
    [InlineData("11111111", "1", true)]
    [InlineData("12849874", "5", true)] // Valid Chilean RUT (193 % 11 = 6 -> 11 - 6 = 5)
    [InlineData("12849874", "2", false)]
    [InlineData("91438000", "6", true)] // Cambiaso Hnos S.A.C. real RUT!
    [InlineData("76086428", "5", true)]
    [InlineData("10000000", "K", false)]
    [InlineData("", "1", false)]
    [InlineData("abc", "1", false)]
    public void RutValidator_ValidatesChileanModulo11(string body, string dv, bool expected)
    {
        Assert.Equal(expected, RutValidator.IsValid(body, dv));
    }

    [Fact]
    public void RutValidator_CalculatesCheckDigitAndFormatsProperly()
    {
        Assert.Equal("6", RutValidator.CalculateCheckDigit(91438000));
        Assert.Equal("91.438.000-6", RutValidator.Format("914380006"));
        Assert.Equal("914380006", RutValidator.Normalize("91.438.000-6"));
    }

    [Fact]
    public void PasswordSecurity_Pbkdf2HashesAndVerifiesWithUniqueSalt()
    {
        string hash1 = PasswordSecurity.HashPassword("ClaveSegura2026!");
        string hash2 = PasswordSecurity.HashPassword("ClaveSegura2026!");

        Assert.NotEqual(hash1, hash2);
        Assert.StartsWith("PBKDF2$100000$", hash1);
        Assert.True(PasswordSecurity.VerifyPassword("ClaveSegura2026!", hash1, "admin", out bool upgrade));
        Assert.False(upgrade);
        Assert.False(PasswordSecurity.VerifyPassword("OtraClave", hash1, "admin", out _));
    }

    [Fact]
    public void PasswordSecurity_VerifiesLegacyVb6HashAndFlagsUpgrade()
    {
        // Exact bytes from legacy OT_PROV.mdb for user "L Reyes"
        const string legacyStored = "LEGACY_VB6$C381C0C9DED7D8";
        Assert.True(PasswordSecurity.VerifyPassword("wanderes", legacyStored, "L Reyes", out bool needsUpgrade));
        Assert.True(needsUpgrade);
        Assert.False(PasswordSecurity.VerifyPassword("wrongpass", legacyStored, "L Reyes", out _));
    }

    [Theory]
    [InlineData(100000, 19000, 119000)]
    [InlineData(0, 0, 0)]
    [InlineData(12345, 2346, 14691)]
    public void TaxCalculator_ComputesChilean19PercentIva(decimal net, decimal expectedIva, decimal expectedTotal)
    {
        Assert.Equal(expectedIva, TaxCalculator.CalculateIva(net));
        Assert.Equal(expectedTotal, TaxCalculator.CalculateTotalWithIva(net));
    }

    [Fact]
    public void TaxCalculator_RejectsNegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxCalculator.CalculateIva(-1));
    }

    [Fact]
    public void PermissionPresets_FollowLeastPrivilegePerRole()
    {
        UserPermissions usr = PermissionPresets.CreateForRole(UserRoleLevel.Usuario);
        Assert.True(usr.MenuOrdenesTrabajo);
        Assert.False(usr.VistoBuenoSupervisor);
        Assert.False(usr.VistoBuenoJefatura);
        Assert.False(usr.MenuFacturas);
        Assert.False(usr.MenuMantenedor);
        Assert.False(usr.MenuUsuarios);

        UserPermissions sup = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor);
        Assert.True(sup.VistoBuenoSupervisor);
        Assert.False(sup.VistoBuenoJefatura);
        Assert.True(sup.MenuFacturas);
        Assert.False(sup.MenuUsuarios);

        UserPermissions jef = PermissionPresets.CreateForRole(UserRoleLevel.Jefatura);
        Assert.True(jef.VistoBuenoSupervisor);
        Assert.True(jef.VistoBuenoJefatura);
        Assert.True(jef.MenuMantenedor);
        Assert.False(jef.MenuUsuarios);

        UserPermissions adm = PermissionPresets.CreateForRole(UserRoleLevel.Administrador);
        Assert.True(adm.MenuOrdenesTrabajo && adm.MenuFacturas && adm.MenuMantenedor && adm.MenuUsuarios);
        Assert.True(adm.IngresoUsuarios && adm.ListadoUsuarios);
    }

    [Fact]
    public void UserPermissions_NormalizeHierarchyClearsOrphanSubPermissions()
    {
        UserPermissions p = new()
        {
            MenuOrdenesTrabajo = false,
            IngresoOt = true,
            VistoBuenoJefatura = true,
            MenuFacturas = false,
            IngresoFacturas = true,
            MenuUsuarios = false,
            IngresoUsuarios = true
        };

        p.NormalizeHierarchy();

        Assert.False(p.IngresoOt);
        Assert.False(p.VistoBuenoJefatura);
        Assert.False(p.IngresoFacturas);
        Assert.False(p.IngresoUsuarios);
    }

    [Fact]
    public void DomainValidator_ValidatesEmailsAndPasswords()
    {
        Assert.True(DomainValidator.IsValidEmailOrEmpty(""));
        Assert.True(DomainValidator.IsValidEmailOrEmpty("contacto@cambiaso.cl"));
        Assert.False(DomainValidator.IsValidEmailOrEmpty("correo-invalido"));

        Assert.NotNull(DomainValidator.ValidatePasswordPolicy("123", "123"));
        Assert.NotNull(DomainValidator.ValidatePasswordPolicy("abcdef", "abcdeg"));
        Assert.Null(DomainValidator.ValidatePasswordPolicy("Segura123", "Segura123"));
    }
}
