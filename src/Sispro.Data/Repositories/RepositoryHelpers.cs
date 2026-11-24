using Microsoft.Data.Sqlite;

namespace Sispro.Data.Repositories;

internal static class RepositoryHelpers
{
    public static int[] NormalizePositiveIds(IReadOnlyCollection<int> ids) =>
        ids.Where(static id => id > 0).Distinct().ToArray();

    public static string AddInParameters(SqliteCommand cmd, string prefix, IReadOnlyList<int> ids)
    {
        List<string> names = new(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            string name = $"${prefix}{i}";
            names.Add(name);
            cmd.Parameters.AddWithValue(name, ids[i]);
        }

        return string.Join(", ", names);
    }
}
