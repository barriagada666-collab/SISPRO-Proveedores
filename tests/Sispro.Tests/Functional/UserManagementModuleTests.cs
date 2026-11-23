using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Core.Entities;
using Sispro.Core.Enums;
using Sispro.Core.Security;
using Sispro.Data.Database;
using Xunit;

namespace Sispro.Tests.Functional;

/// <summary>
/// Pruebas exhaustivas del módulo GESTIÓN DE USUARIOS (CRUD completo, eliminación lógica,
/// aprovisionamiento seguro idempotente, cambio obligatorio en primer inicio de sesión, RBAC estricto y auditoría).
/// </summary>
public sealed class UserManagementModuleTests
{
    [Fact]
    public void InitialAdminProvisioner_IsIdempotentStoresPbkdf2HashAndForcesFirstLoginPasswordChange()
    {
        using TempTestDatabase temp = new();

        const string initialSecret = "InitSecret#99";
        InitialAdminSeedOptions seed = new()
        {
            AliasUsuario = "barriagada_test",
            PlainPassword = initialSecret,
            Nombre = "B.",
            ApellidoPaterno = "Arriagada",
            Correo = "barriagada@cambiaso.cl",
            RequirePasswordChange = true
        };

        using (SqliteConnection conn = temp.Context.ConnectionFactory.OpenConnection())
        {
            Assert.True(InitialAdminProvisioner.EnsureInitialAdmin(conn, seed));
            // Segunda invocación debe ser idempotente (no duplicar ni sobrescribir)
            Assert.False(InitialAdminProvisioner.EnsureInitialAdmin(conn, seed));
        }

        User? created = temp.Context.Users.GetByAlias("barriagada_test");
        Assert.NotNull(created);
        Assert.True(created.CodigoId > 0);
        Assert.Equal(UserRoleLevel.Administrador, created.Nivel);
        Assert.True(created.Activo);
        Assert.True(created.DebeCambiarClave);
        Assert.StartsWith("PBKDF2$100000$", created.PassHash);
        Assert.DoesNotContain(initialSecret, created.PassHash, StringComparison.Ordinal);
        Assert.True(created.Permisos.MenuOrdenesTrabajo);
        Assert.True(created.Permisos.MenuFacturas);
        Assert.True(created.Permisos.MenuMantenedor);
        Assert.True(created.Permisos.MenuUsuarios);
        Assert.True(created.Permisos.IngresoUsuarios);
        Assert.True(created.Permisos.ListadoUsuarios);

        // Primer inicio de sesión exige cambio de contraseña
        AuthResult firstLogin = temp.Context.AuthService.Login("barriagada_test", initialSecret);
        Assert.True(firstLogin.Success);
        Assert.True(firstLogin.RequiresPasswordChange);
        Assert.NotNull(firstLogin.Session);
        Assert.NotNull(firstLogin.Session.Usuario.UltimoAcceso);

        // No debe permitir reutilizar la misma contraseña inicial en primer acceso
        Assert.Throws<ArgumentException>(() =>
            temp.Context.AuthService.ChangeOwnPassword(firstLogin.Session, initialSecret, initialSecret, initialSecret));

        // Cambio obligatorio de contraseña exitoso
        const string rotatedSecret = "NuevaClaveSegura#2026";
        temp.Context.AuthService.ChangeOwnPassword(firstLogin.Session, initialSecret, rotatedSecret, rotatedSecret);
        Assert.False(firstLogin.Session.Usuario.DebeCambiarClave);

        // Clave antigua rechazada, clave nueva aceptada sin requerir nuevo cambio
        Assert.False(temp.Context.AuthService.Login("barriagada_test", initialSecret).Success);
        AuthResult secondLogin = temp.Context.AuthService.Login("barriagada_test", rotatedSecret);
        Assert.True(secondLogin.Success);
        Assert.False(secondLogin.RequiresPasswordChange);

        // Verificar auditoría sin exposición de contraseñas
        IReadOnlyList<AuditEntry> logs = temp.Context.Audit.GetRecent(50);
        Assert.Contains(logs, e => e.Accion == "INIT_ADMIN_PROVISIONED");
        Assert.Contains(logs, e => e.Accion == "CAMBIO_CLAVE_PRIMER_ACCESO");
        Assert.All(logs, e =>
        {
            Assert.DoesNotContain(initialSecret, e.Detalle, StringComparison.Ordinal);
            Assert.DoesNotContain(rotatedSecret, e.Detalle, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MigratedDatabase_ContainsProvisionedBarriagadaAdminWithFullPermissionsAndPbkdf2()
    {
        using TempTestDatabase temp = new(copyMigratedRealDb: true);

        User? barriagada = temp.Context.Users.GetByAlias("barriagada");
        Assert.NotNull(barriagada);
        Assert.True(barriagada.CodigoId >= 1);
        Assert.Equal(UserRoleLevel.Administrador, barriagada.Nivel);
        Assert.True(barriagada.Activo);
        Assert.True(barriagada.DebeCambiarClave);
        Assert.StartsWith("PBKDF2$100000$", barriagada.PassHash);
        Assert.True(barriagada.Permisos.MenuOrdenesTrabajo);
        Assert.True(barriagada.Permisos.IngresoOt);
        Assert.True(barriagada.Permisos.VistoBuenoSupervisor);
        Assert.True(barriagada.Permisos.VistoBuenoJefatura);
        Assert.True(barriagada.Permisos.BusquedaOt);
        Assert.True(barriagada.Permisos.ImpresionOt);
        Assert.True(barriagada.Permisos.MenuFacturas);
        Assert.True(barriagada.Permisos.IngresoFacturas);
        Assert.True(barriagada.Permisos.BusquedaFacturas);
        Assert.True(barriagada.Permisos.MenuMantenedor);
        Assert.True(barriagada.Permisos.MenuUsuarios);
        Assert.True(barriagada.Permisos.IngresoUsuarios);
        Assert.True(barriagada.Permisos.ListadoUsuarios);
    }

    [Fact]
    public void UserCrud_CreateSearchFilterEditPasswordResetLogicalDeleteAndAudit()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        var svc = temp.Context.UserManagementService;

        // 1. Validaciones obligatorias en creación
        Assert.Throws<ArgumentException>(() => svc.CreateUser(admin, new User
        {
            AliasUsuario = "ab", // < 3 chars
            NombreUsuario = "Ana",
            ApePatUsuario = "Gómez"
        }, "Clave123", "Clave123"));

        Assert.Throws<ArgumentException>(() => svc.CreateUser(admin, new User
        {
            AliasUsuario = "agomez",
            NombreUsuario = "Ana",
            ApePatUsuario = "Gómez",
            MailUsuario = "correo-invalido"
        }, "Clave123", "Clave123"));

        Assert.Throws<ArgumentException>(() => svc.CreateUser(admin, new User
        {
            AliasUsuario = "agomez",
            NombreUsuario = "Ana",
            ApePatUsuario = "Gómez"
        }, "Clave123", "Distinta123"));

        // 2. Creación exitosa con ID automático (sin necesidad de RUT manual)
        int assignedId = svc.CreateUser(admin, new User
        {
            AliasUsuario = "mperez",
            NombreUsuario = "María",
            ApePatUsuario = "Pérez",
            ApeMatUsuario = "Soto",
            MailUsuario = "mperez@cambiaso.cl",
            FonoUsuario = "+56 9 8765 4321",
            Nivel = UserRoleLevel.Supervisor,
            Activo = true,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
        }, "SupPass2026", "SupPass2026");

        Assert.True(assignedId >= 2);

        // Usuario duplicado (insensible a mayúsculas) debe rechazarse
        Assert.Throws<InvalidOperationException>(() => svc.CreateUser(admin, new User
        {
            AliasUsuario = "MPEREZ",
            NombreUsuario = "Otra",
            ApePatUsuario = "Persona"
        }, "OtraClave2026", "OtraClave2026"));

        User? created = temp.Context.Users.GetByAlias("mperez");
        Assert.NotNull(created);
        Assert.Equal(assignedId, created.CodigoId);
        Assert.False(created.Permisos.MenuUsuarios); // No-administrador nunca hereda M4

        // 3. Búsqueda y filtros combinados (texto, perfil y estado)
        Assert.Single(svc.Search(admin, new UserSearchFilter { TextoBusqueda = "Pérez" }));
        Assert.Single(svc.Search(admin, new UserSearchFilter { Perfil = UserRoleLevel.Supervisor }));
        Assert.Empty(svc.Search(admin, new UserSearchFilter { Perfil = UserRoleLevel.Supervisor, Activo = false }));

        // 4. Edición de datos y cambio de perfil (Supervisor -> Jefatura)
        created.NombreUsuario = "María José";
        created.FonoUsuario = "+56 32 220 9900";
        created.Nivel = UserRoleLevel.Jefatura;
        created.Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Jefatura, created.IdUsuario);
        svc.UpdateUser(admin, created);

        User reloaded = temp.Context.Users.GetById(created.IdUsuario)!;
        Assert.Equal("María José", reloaded.NombreUsuario);
        Assert.Equal("+56 32 220 9900", reloaded.FonoUsuario);
        Assert.Equal(UserRoleLevel.Jefatura, reloaded.Nivel);

        // 5. Cambio de contraseña administrativo con forzado en próximo acceso
        svc.ChangeUserPassword(admin, reloaded.IdUsuario, "ResetAdmin#2026", "ResetAdmin#2026", requireChangeOnNextLogin: true);
        Assert.False(temp.Context.AuthService.Login("mperez", "SupPass2026").Success);
        AuthResult resetLogin = temp.Context.AuthService.Login("mperez", "ResetAdmin#2026");
        Assert.True(resetLogin.Success);
        Assert.True(resetLogin.RequiresPasswordChange);

        // Registrar operación histórica (VBJ en una OT) antes de eliminación lógica
        int provId = temp.Context.MaintainerService.SaveProvider(admin, new Provider { Nombre = "PROV HISTORIAL" });
        int plantaId = temp.Context.AssetTreeService.AddChild(admin, 1, "PLANTA HISTORIAL");
        WorkOrder historicalOt = new()
        {
            NumeroOt = 9501,
            IdProv = provId,
            FechaOt = DateTime.Today,
            Detalles = [new WorkOrderDetail { Cantidad = 1, Descripcion = "Trabajo con historial de usuario" }]
        };
        historicalOt.SetLevels([1, plantaId]);
        int otId = temp.Context.WorkOrderService.Create(admin, historicalOt);
        temp.Context.WorkOrderService.SetApproval(resetLogin.Session!, otId, ApprovalType.Jefatura, true);

        // 6. Eliminación lógica: desactiva usuario sin borrar registros históricos ni FK
        svc.DeleteUser(admin, reloaded.IdUsuario);

        User afterLogicalDelete = temp.Context.Users.GetById(reloaded.IdUsuario)!;
        Assert.NotNull(afterLogicalDelete);
        Assert.False(afterLogicalDelete.Activo);
        Assert.True(temp.Context.WorkOrderService.GetByNumber(9501)!.TieneVbj);

        // Usuario desactivado no puede iniciar sesión
        AuthResult blockedLogin = temp.Context.AuthService.Login("mperez", "ResetAdmin#2026");
        Assert.False(blockedLogin.Success);
        Assert.Contains("inactiva", blockedLogin.Message, StringComparison.OrdinalIgnoreCase);

        // Filtro por inactivos lo muestra correctamente
        Assert.Single(svc.Search(admin, new UserSearchFilter { Activo = false }));

        // 7. Reactivación
        svc.SetUserActive(admin, reloaded.IdUsuario, active: true);
        Assert.True(temp.Context.Users.GetById(reloaded.IdUsuario)!.Activo);

        // 8. Verificar auditoría completa (sin contraseñas en los detalles)
        IReadOnlyList<AuditEntry> logs = temp.Context.Audit.GetRecent(100);
        Assert.Contains(logs, e => e.Accion == "CREAR_USUARIO" && e.Detalle.Contains("mperez"));
        Assert.Contains(logs, e => e.Accion == "MODIFICAR_USUARIO" && e.Detalle.Contains("mperez"));
        Assert.Contains(logs, e => e.Accion == "CAMBIO_PERFIL" && e.Detalle.Contains("Supervisor") && e.Detalle.Contains("Jefatura"));
        Assert.Contains(logs, e => e.Accion == "CAMBIO_CLAVE_ADMIN" && e.Detalle.Contains("mperez"));
        Assert.Contains(logs, e => e.Accion == "DESACTIVAR_USUARIO" && e.Detalle.Contains("mperez"));
        Assert.Contains(logs, e => e.Accion == "ACTIVAR_USUARIO" && e.Detalle.Contains("mperez"));
        Assert.All(logs, e =>
        {
            Assert.DoesNotContain("SupPass2026", e.Detalle, StringComparison.Ordinal);
            Assert.DoesNotContain("ResetAdmin#2026", e.Detalle, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void StrictAdminAuthorization_BlocksAllNonAdminRolesAndProtectsLastActiveAdmin()
    {
        using TempTestDatabase temp = new();
        UserSession admin = temp.Context.AuthService.Login("admin", "Admin2026!").Session!;
        var svc = temp.Context.UserManagementService;

        // Crear usuarios de perfiles Supervisor y Jefatura
        svc.CreateUser(admin, new User
        {
            AliasUsuario = "sup_rbac",
            NombreUsuario = "Carlos",
            ApePatUsuario = "Díaz",
            Nivel = UserRoleLevel.Supervisor,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Supervisor)
        }, "ClaveSup#26", "ClaveSup#26");

        svc.CreateUser(admin, new User
        {
            AliasUsuario = "jef_rbac",
            NombreUsuario = "Elena",
            ApePatUsuario = "Rojas",
            Nivel = UserRoleLevel.Jefatura,
            Permisos = PermissionPresets.CreateForRole(UserRoleLevel.Jefatura)
        }, "ClaveJef#26", "ClaveJef#26");

        UserSession supervisor = temp.Context.AuthService.Login("sup_rbac", "ClaveSup#26").Session!;
        UserSession jefatura = temp.Context.AuthService.Login("jef_rbac", "ClaveJef#26").Session!;

        // Incluso si alguien manipula flags en memoria, el backend revalida el perfil contra la BD
        supervisor.Usuario.Permisos.MenuUsuarios = true;
        supervisor.Usuario.Permisos.IngresoUsuarios = true;

        foreach (UserSession nonAdmin in new[] { supervisor, jefatura })
        {
            Assert.Throws<UnauthorizedAccessException>(() => svc.EnsureCanAccessModule(nonAdmin));
            Assert.Throws<UnauthorizedAccessException>(() => svc.Search(nonAdmin));
            Assert.Throws<UnauthorizedAccessException>(() => svc.GetAll(nonAdmin));
            Assert.Throws<UnauthorizedAccessException>(() => svc.GetById(nonAdmin, admin.Usuario.IdUsuario));
            Assert.Throws<UnauthorizedAccessException>(() => svc.CreateUser(nonAdmin, new User
            {
                AliasUsuario = "intruder",
                NombreUsuario = "No",
                ApePatUsuario = "Autorizado",
                Nivel = UserRoleLevel.Administrador
            }, "Hack1234", "Hack1234"));
            Assert.Throws<UnauthorizedAccessException>(() => svc.UpdateUser(nonAdmin, admin.Usuario));
            Assert.Throws<UnauthorizedAccessException>(() => svc.ChangeUserPassword(nonAdmin, admin.Usuario.IdUsuario, "Hack1234", "Hack1234"));
            Assert.Throws<UnauthorizedAccessException>(() => svc.DeleteUser(nonAdmin, admin.Usuario.IdUsuario));
            Assert.Throws<UnauthorizedAccessException>(() => svc.SetUserActive(nonAdmin, admin.Usuario.IdUsuario, false));
        }

        // Verificar que los intentos no autorizados quedaron auditados
        IReadOnlyList<AuditEntry> logs = temp.Context.Audit.GetRecent(100);
        Assert.Contains(logs, e => e.Accion == "ACCESO_NO_AUTORIZADO" && e.Usuario == "sup_rbac");
        Assert.Contains(logs, e => e.Accion == "ACCESO_NO_AUTORIZADO" && e.Usuario == "jef_rbac");

        // Garantizar que siempre exista al menos un Administrador activo (ni siquiera otro admin puede quedar en 0)
        svc.CreateUser(admin, new User
        {
            AliasUsuario = "admin2",
            NombreUsuario = "Segundo",
            ApePatUsuario = "Admin",
            Nivel = UserRoleLevel.Administrador
        }, "AdminTwo#26", "AdminTwo#26");

        UserSession admin2 = temp.Context.AuthService.Login("admin2", "AdminTwo#26").Session!;
        // admin2 desactiva a admin (quedan 1 activo: admin2)
        svc.DeleteUser(admin2, admin.Usuario.IdUsuario);
        Assert.Equal(1, temp.Context.Users.CountActiveAdmins());

        // admin2 no puede desactivarse ni quitarse su propio perfil (último admin activo)
        Assert.Throws<InvalidOperationException>(() => svc.DeleteUser(admin2, admin2.Usuario.IdUsuario));
        User selfDemote = temp.Context.Users.GetById(admin2.Usuario.IdUsuario)!;
        selfDemote.Nivel = UserRoleLevel.Jefatura;
        Assert.Throws<InvalidOperationException>(() => svc.UpdateUser(admin2, selfDemote));
    }

    [Fact]
    public void SourceCodeSecurityAudit_ContainsNoHardcodedPlaintextPasswordsInSrc()
    {
        string? dir = AppContext.BaseDirectory;
        string? srcDir = null;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "src");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(dir, "SISPRO.sln")))
            {
                srcDir = candidate;
                break;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        Assert.NotNull(srcDir);

        foreach (string csFile in Directory.EnumerateFiles(srcDir, "*.cs", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(csFile);
            Assert.DoesNotContain("HashPassword(\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("PlainPassword = \"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Admin2026!", content, StringComparison.Ordinal);
            Assert.DoesNotContain("wanderes", content, StringComparison.Ordinal);
        }
    }
}
