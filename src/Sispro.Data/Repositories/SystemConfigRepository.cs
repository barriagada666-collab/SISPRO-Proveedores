using Microsoft.Data.Sqlite;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

/// <summary>
/// Repositorio centralizado de configuración general del sistema almacenada en SQLite (tabla ConfiguracionSistema).
/// </summary>
public sealed class SystemConfigRepository(SqliteConnectionFactory factory) : ISystemConfigRepository
{
    public IReadOnlyDictionary<string, string> GetAll()
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT clave, valor FROM ConfiguracionSistema ORDER BY clave;";
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            result[rdr.GetString(0)] = rdr.GetString(1);
        }

        return result;
    }

    public string? GetValue(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT valor FROM ConfiguracionSistema WHERE clave = $k LIMIT 1;";
        cmd.Parameters.AddWithValue("$k", key.Trim());
        object? raw = cmd.ExecuteScalar();
        return raw is null || raw == DBNull.Value ? null : Convert.ToString(raw);
    }

    public void SaveAll(IReadOnlyDictionary<string, string> values, string modifiedBy)
    {
        if (values is null || values.Count == 0)
        {
            return;
        }

        string actor = string.IsNullOrWhiteSpace(modifiedBy) ? "SISTEMA" : modifiedBy.Trim();
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteTransaction tx = conn.BeginTransaction();

        foreach (KeyValuePair<string, string> kv in values)
        {
            if (string.IsNullOrWhiteSpace(kv.Key))
            {
                continue;
            }

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO ConfiguracionSistema(clave, valor, fecha_modificacion, modificado_por)
                VALUES ($k, $v, datetime('now','localtime'), $u)
                ON CONFLICT(clave) DO UPDATE SET
                    valor = excluded.valor,
                    fecha_modificacion = datetime('now','localtime'),
                    modificado_por = excluded.modificado_por;
                """;
            cmd.Parameters.AddWithValue("$k", kv.Key.Trim());
            cmd.Parameters.AddWithValue("$v", kv.Value ?? string.Empty);
            cmd.Parameters.AddWithValue("$u", actor);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }
}
