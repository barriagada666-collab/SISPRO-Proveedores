using Microsoft.Data.Sqlite;
using Sispro.Core.Entities;
using Sispro.Core.Interfaces;
using Sispro.Data.Database;

namespace Sispro.Data.Repositories;

public sealed class RequesterRepository(SqliteConnectionFactory factory) : IRequesterRepository
{
    public IReadOnlyList<Requester> GetAll(bool includeInactive = false, string? search = null)
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
            where.Add("(alias LIKE $q OR nombre_sol LIKE $q OR ape_pat_sol LIKE $q OR mail_sol LIKE $q)");
            cmd.Parameters.AddWithValue("$q", $"%{search.Trim()}%");
        }

        string clause = where.Count > 0 ? $"WHERE {string.Join(" AND ", where)}" : string.Empty;
        cmd.CommandText = $"""
            SELECT id_sol, alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, activo,
                   firma_bytes, COALESCE(firma_nombre, ''), COALESCE(firma_tipo, ''), firma_preview_png
            FROM Solicitantes
            {clause}
            ORDER BY alias;
            """;

        List<Requester> list = [];
        using SqliteDataReader rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(Map(rdr));
        }

        return list;
    }

    public Requester? GetById(int idSol)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_sol, alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, activo,
                   firma_bytes, COALESCE(firma_nombre, ''), COALESCE(firma_tipo, ''), firma_preview_png
            FROM Solicitantes WHERE id_sol = $id LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$id", idSol);
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? Map(rdr) : null;
    }

    public Requester? GetByAlias(string alias)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id_sol, alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, activo,
                   firma_bytes, COALESCE(firma_nombre, ''), COALESCE(firma_tipo, ''), firma_preview_png
            FROM Solicitantes WHERE LOWER(alias) = LOWER($alias) LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$alias", alias.Trim());
        using SqliteDataReader rdr = cmd.ExecuteReader();
        return rdr.Read() ? Map(rdr) : null;
    }

    public int Create(Requester requester)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Solicitantes(
                alias, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, activo,
                firma_bytes, firma_nombre, firma_tipo, firma_preview_png
            ) VALUES (
                $alias, $nom, $pat, $mat, $fono, $mail, $act,
                $fbytes, $fnom, $ftipo, $fpng
            );
            SELECT last_insert_rowid();
            """;
        Bind(cmd, requester);
        int id = Convert.ToInt32(cmd.ExecuteScalar());
        requester.IdSol = id;
        return id;
    }

    public void Update(Requester requester)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE Solicitantes
            SET alias = $alias, nombre_sol = $nom, ape_pat_sol = $pat,
                ape_mat_sol = $mat, fono_sol = $fono, mail_sol = $mail, activo = $act,
                firma_bytes = $fbytes, firma_nombre = $fnom, firma_tipo = $ftipo, firma_preview_png = $fpng
            WHERE id_sol = $id;
            """;
        cmd.Parameters.AddWithValue("$id", requester.IdSol);
        Bind(cmd, requester);
        cmd.ExecuteNonQuery();
    }

    public void EnsureFromUser(User user)
    {
        if (!user.EsSolicitante)
        {
            return;
        }

        Requester? existing = GetByAlias(user.AliasUsuario);
        if (existing is null)
        {
            Create(new Requester
            {
                Alias = user.AliasUsuario,
                Nombre = user.NombreUsuario,
                ApellidoPaterno = user.ApePatUsuario,
                ApellidoMaterno = user.ApeMatUsuario,
                Telefono = user.FonoUsuario,
                Correo = user.MailUsuario,
                Activo = user.Activo
            });
        }
        else
        {
            existing.Nombre = user.NombreUsuario;
            existing.ApellidoPaterno = user.ApePatUsuario;
            existing.ApellidoMaterno = user.ApeMatUsuario;
            if (!string.IsNullOrWhiteSpace(user.FonoUsuario))
            {
                existing.Telefono = user.FonoUsuario;
            }

            if (!string.IsNullOrWhiteSpace(user.MailUsuario))
            {
                existing.Correo = user.MailUsuario;
            }

            existing.Activo = user.Activo;
            Update(existing);
        }
    }

    public bool HasWorkOrders(int idSol)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM OrdenesTrabajo WHERE id_sol = $id);";
        cmd.Parameters.AddWithValue("$id", idSol);
        return Convert.ToInt32(cmd.ExecuteScalar()) != 0;
    }

    public void Delete(int idSol)
    {
        using SqliteConnection conn = factory.OpenConnection();
        using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Solicitantes WHERE id_sol = $id;";
        cmd.Parameters.AddWithValue("$id", idSol);
        cmd.ExecuteNonQuery();
    }

    private static void Bind(SqliteCommand cmd, Requester r)
    {
        cmd.Parameters.AddWithValue("$alias", r.Alias.Trim());
        cmd.Parameters.AddWithValue("$nom", r.Nombre.Trim());
        cmd.Parameters.AddWithValue("$pat", r.ApellidoPaterno.Trim());
        cmd.Parameters.AddWithValue("$mat", r.ApellidoMaterno.Trim());
        cmd.Parameters.AddWithValue("$fono", r.Telefono.Trim());
        cmd.Parameters.AddWithValue("$mail", r.Correo.Trim());
        cmd.Parameters.AddWithValue("$act", r.Activo ? 1 : 0);
        cmd.Parameters.Add("$fbytes", SqliteType.Blob).Value = r.FirmaBytes is { Length: > 0 } ? r.FirmaBytes : DBNull.Value;
        cmd.Parameters.AddWithValue("$fnom", r.FirmaNombre.Trim());
        cmd.Parameters.AddWithValue("$ftipo", r.FirmaTipo.Trim().ToUpperInvariant());
        cmd.Parameters.Add("$fpng", SqliteType.Blob).Value = r.FirmaPreviewPng is { Length: > 0 } ? r.FirmaPreviewPng : DBNull.Value;
    }

    private static Requester Map(SqliteDataReader rdr) => new()
    {
        IdSol = rdr.GetInt32(0),
        Alias = rdr.GetString(1),
        Nombre = rdr.GetString(2),
        ApellidoPaterno = rdr.GetString(3),
        ApellidoMaterno = rdr.GetString(4),
        Telefono = rdr.GetString(5),
        Correo = rdr.GetString(6),
        Activo = rdr.GetInt32(7) != 0,
        FirmaBytes = rdr.IsDBNull(8) ? null : (byte[])rdr.GetValue(8),
        FirmaNombre = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9),
        FirmaTipo = rdr.IsDBNull(10) ? string.Empty : rdr.GetString(10),
        FirmaPreviewPng = rdr.IsDBNull(11) ? null : (byte[])rdr.GetValue(11)
    };
}
