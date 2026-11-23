using Microsoft.Data.Sqlite;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class ProviderRepository(SqliteConnectionFactory factory) : IProviderRepository
{
    public IReadOnlyList<Provider> GetAll(bool includeInactive = false, string? search = null)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        List<string> where = [];
        if (!includeInactive)
        {
            where.Add("activo = 1");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            where.Add("(nom_prov LIKE $q OR dir_prov LIKE $q OR mail_prov LIKE $q OR contacto_prov LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        string clause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT id_prov, nom_prov, dir_prov, fono_prov, fax_prov, mail_prov,
                   contacto_prov, mail_contacto_prov, observaciones, activo
            FROM Proveedores
            {clause}
            ORDER BY nom_prov;
            """;

        List<Provider> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(Map(rdr));
        }

        return list;
    }

    public Provider? GetById(int idProv)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_prov, nom_prov, dir_prov, fono_prov, fax_prov, mail_prov,
                   contacto_prov, mail_contacto_prov, observaciones, activo
            FROM Proveedores WHERE id_prov = $id LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", idProv);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? Map(rdr) : null;
    }

    public Provider? GetByName(string name)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_prov, nom_prov, dir_prov, fono_prov, fax_prov, mail_prov,
                   contacto_prov, mail_contacto_prov, observaciones, activo
            FROM Proveedores WHERE LOWER(nom_prov) = LOWER($name) LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$name", name.Trim());
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? Map(rdr) : null;
    }

    public int Create(Provider provider)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Proveedores(
                nom_prov, dir_prov, fono_prov, fax_prov, mail_prov,
                contacto_prov, mail_contacto_prov, observaciones, activo
            ) VALUES (
                $nom, $dir, $fono, $fax, $mail, $cont, $mailc, $obs, $act
            );
            SELECT last_insert_rowid();
            """;
        Bind(cmd, provider);
        int id = Convert.ToInt32(cmd.ExecuteScalar());
        provider.IdProv = id;
        return id;
    }

    public void Update(Provider provider)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Proveedores
            SET nom_prov = $nom, dir_prov = $dir, fono_prov = $fono, fax_prov = $fax,
                mail_prov = $mail, contacto_prov = $cont, mail_contacto_prov = $mailc,
                observaciones = $obs, activo = $act
            WHERE id_prov = $id;
            """;
        cmd.Parameters.AddWithValue("$id", provider.IdProv);
        Bind(cmd, provider);
        cmd.ExecuteNonQuery();
    }

    public bool HasWorkOrders(int idProv)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM OrdenesTrabajo WHERE id_prov = $id);";
        cmd.Parameters.AddWithValue("$id", idProv);
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    public void Delete(int idProv)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Proveedores WHERE id_prov = $id;";
        cmd.Parameters.AddWithValue("$id", idProv);
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, Provider p)
    {
        cmd.Parameters.AddWithValue("$nom", p.Nombre.Trim());
        cmd.Parameters.AddWithValue("$dir", p.Direccion.Trim());
        cmd.Parameters.AddWithValue("$fono", p.Telefono.Trim());
        cmd.Parameters.AddWithValue("$fax", p.Fax.Trim());
        cmd.Parameters.AddWithValue("$mail", p.Correo.Trim());
        cmd.Parameters.AddWithValue("$cont", p.Contacto.Trim());
        cmd.Parameters.AddWithValue("$mailc", p.CorreoContacto.Trim());
        cmd.Parameters.AddWithValue("$obs", p.Observaciones.Trim());
        cmd.Parameters.AddWithValue("$act", p.Activo ? 1 : 0);
    }

    private static Provider Map(SqliteDataReader rdr) => new()
    {
        IdProv = rdr.GetInt32(0),
        Nombre = rdr.GetString(1),
        Direccion = rdr.GetString(2),
        Telefono = rdr.GetString(3),
        Fax = rdr.GetString(4),
        Correo = rdr.GetString(5),
        Contacto = rdr.GetString(6),
        CorreoContacto = rdr.GetString(7),
        Observaciones = rdr.GetString(8),
        Activo = rdr.GetInt32(9) != 0
    };
}
