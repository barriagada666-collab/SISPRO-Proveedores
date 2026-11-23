using Microsoft.Data.Sqlite;
using Sispro.Core.Dtos;
using Sispro.Services;

namespace Sispro.Tests;

internal sealed class TempTestDatabase : IDisposable
{
    public string DbPath { get; }
    public SisproApplicationContext Context { get; }

    public TempTestDatabase(bool copyMigratedRealDb = false)
    {
        DbPath = Path.Combine(Path.GetTempPath(), $"sispro_test_{Guid.NewGuid():N}.db");

        if (copyMigratedRealDb)
        {
            string realDb = FindMigratedDatabase();
            File.Copy(realDb, DbPath, overwrite: true);
        }

        AppSettings settings = new() { DatabasePath = DbPath };
        InitialAdminSeedOptions? testSeed = copyMigratedRealDb
            ? null
            : new InitialAdminSeedOptions
            {
                AliasUsuario = "admin",
                PlainPassword = "Admin2026!",
                Nombre = "Administrador",
                ApellidoPaterno = "Sistema",
                ApellidoMaterno = "SISPRO",
                Correo = "soporte@cambiaso.cl",
                RequirePasswordChange = false
            };

        Context = new SisproApplicationContext(settings, testSeed);
    }

    public static string FindMigratedDatabase()
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, "data", "sispro.db");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("No se encontró data/sispro.db en el árbol del proyecto.");
    }

    public static void RunOnStaThread(Action action)
    {
        Exception? captured = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw new AggregateException("Error ejecutando prueba UI en hilo STA.", captured);
        }
    }

    public void Dispose()
    {
        Context.Dispose();
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            string p = DbPath + suffix;
            if (File.Exists(p))
            {
                try { File.Delete(p); } catch { /* ignore temp cleanup races */ }
            }
        }
    }
}
