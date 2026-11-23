using Microsoft.Data.Sqlite;

namespace Sispro.Data.Database;

public sealed class SqliteConnectionFactory
{
    public string DatabasePath { get; }
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("La ruta de la base de datos no puede estar vacía.", nameof(databasePath));
        }

        DatabasePath = Path.GetFullPath(databasePath);
        string? dir = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 15
        };

        _connectionString = builder.ToString();
    }

    public SqliteConnection OpenConnection()
    {
        SqliteConnection connection = new(_connectionString);
        connection.Open();
        using SqliteCommand pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public void BackupTo(string destinationPath)
    {
        string? dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using SqliteConnection source = OpenConnection();
        using SqliteConnection destination = new($"Data Source={destinationPath}");
        destination.Open();
        source.BackupDatabase(destination);
    }
}
