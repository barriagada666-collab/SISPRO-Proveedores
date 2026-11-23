using Microsoft.Data.Sqlite;
using Sispro.Core.Enums;
using Sispro.Data.Database;

namespace Sispro.Services;

public sealed class DataChangedEventArgs(DataChangeKind kind, string sourceAction, bool fromExternalSession = false) : EventArgs
{
    public DataChangeKind Kind { get; } = kind;
    public string SourceAction { get; } = sourceAction;
    public bool FromExternalSession { get; } = fromExternalSession;
    public DateTime Timestamp { get; } = DateTime.Now;
}

/// <summary>
/// Bus reactivo de sincronización inmediata (in-app tras confirmación en BD) y detector
/// de cambios concurrentes entre sesiones usando SQLite WAL PRAGMA data_version.
/// </summary>
public sealed class SisproSyncHub : IDisposable
{
    private readonly Lock _syncLock = new();
    private SqliteConnectionFactory? _factory;
    private SqliteConnection? _watchConnection;
    private long _lastDataVersion;
    private bool _disposed;

    public event EventHandler<DataChangedEventArgs>? DataChanged;

    public long LastKnownDataVersion
    {
        get
        {
            lock (_syncLock)
            {
                return _lastDataVersion;
            }
        }
    }

    public SisproSyncHub(SqliteConnectionFactory? factory = null)
    {
        if (factory is not null)
        {
            AttachDatabase(factory);
        }
    }

    public void AttachDatabase(SqliteConnectionFactory factory)
    {
        lock (_syncLock)
        {
            _watchConnection?.Dispose();
            _watchConnection = null;
            _factory = factory;
            _lastDataVersion = ReadDataVersionCore();
        }
    }

    /// <summary>
    /// Publica una notificación de cambio únicamente después de confirmar la persistencia en SQLite.
    /// </summary>
    public void Publish(DataChangeKind kind, string sourceAction)
    {
        if (_disposed || kind == DataChangeKind.None)
        {
            return;
        }

        // Sincronizar la versión observada para evitar auto-disparos en el siguiente sondeo
        lock (_syncLock)
        {
            _lastDataVersion = ReadDataVersionCore();
        }

        DataChanged?.Invoke(this, new DataChangedEventArgs(kind, sourceAction, fromExternalSession: false));
    }

    /// <summary>
    /// Consulta PRAGMA data_version sobre la conexión vigía. SQLite incrementa data_version
    /// automáticamente cuando cualquier otra conexión o proceso confirma una transacción.
    /// </summary>
    public bool PollExternalChanges()
    {
        if (_disposed)
        {
            return false;
        }

        bool changed = false;
        lock (_syncLock)
        {
            if (_factory is null)
            {
                return false;
            }

            long current = ReadDataVersionCore();
            if (_lastDataVersion > 0 && current > 0 && current != _lastDataVersion)
            {
                _lastDataVersion = current;
                changed = true;
            }
            else if (_lastDataVersion == 0 && current > 0)
            {
                _lastDataVersion = current;
            }
        }

        if (changed)
        {
            DataChanged?.Invoke(this, new DataChangedEventArgs(DataChangeKind.All, "SINCRONIZACION_CONCURRENTE", fromExternalSession: true));
        }

        return changed;
    }

    private long ReadDataVersionCore()
    {
        if (_factory is null)
        {
            return 0;
        }

        try
        {
            if (_watchConnection is null || _watchConnection.State != System.Data.ConnectionState.Open)
            {
                _watchConnection?.Dispose();
                _watchConnection = _factory.OpenConnection();
            }

            using SqliteCommand cmd = _watchConnection.CreateCommand();
            cmd.CommandText = "PRAGMA data_version;";
            object? result = cmd.ExecuteScalar();
            return result is null or DBNull ? 0 : Convert.ToInt64(result);
        }
        catch
        {
            _watchConnection?.Dispose();
            _watchConnection = null;
            return _lastDataVersion;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_syncLock)
        {
            _watchConnection?.Dispose();
            _watchConnection = null;
        }
    }
}
