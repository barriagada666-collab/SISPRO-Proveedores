using System.Diagnostics;
using System.Globalization;
using System.Text;
using Sispro.Core.Dtos;
using Sispro.Core.Interfaces;

namespace Sispro.Services;

/// <summary>
/// Lector de archivos Microsoft Access (.mdb) compatible con aplicaciones x64
/// mediante host Windows PowerShell de 32 bits (SysWOW64) y Microsoft.Jet.OLEDB.4.0.
/// </summary>
public sealed class Jet32MdbTableReader : IMdbTableReader
{
    private const string ExportScript = """
        $ErrorActionPreference = 'Stop'
        $mdbPath = $env:SISPRO_MDB_PATH
        $outDir = $env:SISPRO_OUT_DIR
        if (-not (Test-Path -LiteralPath $mdbPath)) {
            throw "No existe el archivo MDB: $mdbPath"
        }

        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
        $ext = [System.IO.Path]::GetExtension($mdbPath)
        $providers = if ($ext -ieq '.accdb') {
            @('Microsoft.ACE.OLEDB.12.0', 'Microsoft.ACE.OLEDB.16.0')
        } else {
            @('Microsoft.Jet.OLEDB.4.0', 'Microsoft.ACE.OLEDB.12.0', 'Microsoft.ACE.OLEDB.16.0')
        }

        $conn = $null
        $lastErr = $null
        foreach ($prov in $providers) {
            try {
                $candidate = New-Object System.Data.OleDb.OleDbConnection("Provider=$prov;Data Source=$mdbPath;")
                $candidate.Open()
                $conn = $candidate
                break
            } catch {
                $lastErr = $_
                if ($candidate) { $candidate.Dispose() }
            }
        }

        if ($null -eq $conn) {
            throw "No fue posible abrir el archivo Access con OLEDB (Jet/ACE 32 bits). Detalle: $($lastErr.Exception.Message)"
        }

        try {
            $tablesSchema = $conn.GetSchema('Tables')
            $existing = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($row in $tablesSchema.Rows) {
                $tName = [string]$row['TABLE_NAME']
                if (-not [string]::IsNullOrWhiteSpace($tName)) {
                    [void]$existing.Add($tName)
                }
            }

            if (-not $existing.Contains('Proveedores')) {
                throw "El archivo MDB seleccionado no contiene la tabla requerida 'Proveedores'."
            }

            $specs = @(
                @{ Table = 'Proveedores'; File = 'Proveedores.csv'; Sql = 'SELECT id_prov, nom_prov, dir_prov, fono_prov, fax_prov, mail_prov FROM [Proveedores]' },
                @{ Table = 'Solicitante'; File = 'Solicitante.csv'; Sql = 'SELECT id_sol, nombre_sol, ape_pat_sol, ape_mat_sol, fono_sol, mail_sol, alias FROM [Solicitante]' },
                @{ Table = 'OT_PROV'; File = 'OT_PROV.csv'; Sql = 'SELECT id_ot, id_prov, n_ot, fecha_ot, activo, guia_des_ot, id_n1, id_n2, id_n3, id_n4, id_n5, id_n6, id_n7, id_n8, id_n9, id_n10, id_nivel, id_sol FROM [OT_PROV]' },
                @{ Table = 'Detalle_ot'; File = 'Detalle_ot.csv'; Sql = 'SELECT Id_Det, id_ot, cantidad_det, detalle_ot, Estado_det FROM [Detalle_ot]' }
            )

            foreach ($spec in $specs) {
                if (-not $existing.Contains($spec.Table)) { continue }
                $dt = New-Object System.Data.DataTable
                $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($spec.Sql, $conn)
                try {
                    [void]$adapter.Fill($dt)
                } finally {
                    $adapter.Dispose()
                }

                $csvPath = Join-Path $outDir $spec.File
                $dt | Export-Csv -LiteralPath $csvPath -NoTypeInformation -Encoding UTF8
            }
        } finally {
            $conn.Close()
            $conn.Dispose()
        }
        """;

    public MdbSnapshot ReadSnapshot(string mdbPath)
    {
        if (string.IsNullOrWhiteSpace(mdbPath))
        {
            throw new ArgumentException("Debe indicar la ruta del archivo .mdb.", nameof(mdbPath));
        }

        string fullPath = Path.GetFullPath(mdbPath.Trim());
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"No se encontró el archivo Access especificado: {fullPath}", fullPath);
        }

        string ext = Path.GetExtension(fullPath);
        if (!string.Equals(ext, ".mdb", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".accdb", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("El archivo seleccionado debe tener extensión .mdb o .accdb.", nameof(mdbPath));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("La lectura nativa de archivos Microsoft Access (.mdb) requiere Windows.");
        }

        string wow64Powershell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "SysWOW64",
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");

        if (!File.Exists(wow64Powershell))
        {
            throw new InvalidOperationException("No se encontró Windows PowerShell x86 (SysWOW64) requerido para abrir archivos .mdb.");
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"sispro_mdb_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            string encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(ExportScript));
            ProcessStartInfo psi = new()
            {
                FileName = wow64Powershell,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-EncodedCommand");
            psi.ArgumentList.Add(encodedScript);
            psi.Environment["SISPRO_MDB_PATH"] = fullPath;
            psi.Environment["SISPRO_OUT_DIR"] = tempDir;

            using Process proc = Process.Start(psi)
                ?? throw new InvalidOperationException("No se pudo iniciar el proceso lector de Access (32 bits).");

            string stdOut = proc.StandardOutput.ReadToEnd();
            string stdErr = proc.StandardError.ReadToEnd();
            if (!proc.WaitForExit(60_000))
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* ignore cleanup failure */ }
                throw new TimeoutException("Tiempo de espera agotado al leer la base de datos Access (.mdb).");
            }

            if (proc.ExitCode != 0)
            {
                string detail = string.IsNullOrWhiteSpace(stdErr) ? stdOut : stdErr;
                throw new InvalidOperationException($"Error al leer el archivo MDB: {detail.Trim()}");
            }

            FileInfo fi = new(fullPath);
            List<MdbProviderRow> providers = ReadProvidersCsv(Path.Combine(tempDir, "Proveedores.csv"));
            List<MdbRequesterRow> requesters = ReadRequestersCsv(Path.Combine(tempDir, "Solicitante.csv"));
            List<MdbWorkOrderRow> workOrders = ReadWorkOrdersCsv(Path.Combine(tempDir, "OT_PROV.csv"));
            List<MdbWorkOrderDetailRow> details = ReadWorkOrderDetailsCsv(Path.Combine(tempDir, "Detalle_ot.csv"));

            return new MdbSnapshot
            {
                MdbPath = fullPath,
                FileSizeBytes = fi.Length,
                Providers = providers,
                Requesters = requesters,
                WorkOrders = workOrders,
                WorkOrderDetails = details
            };
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch
            {
                // Ignorar errores transitorios de limpieza temporal
            }
        }
    }

    private static List<MdbProviderRow> ReadProvidersCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            throw new InvalidOperationException("No se exportó la tabla 'Proveedores' desde el archivo MDB.");
        }

        List<MdbProviderRow> list = [];
        foreach (Dictionary<string, string> row in ParseCsv(csvPath))
        {
            string nombre = Get(row, "nom_prov");
            if (string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            list.Add(new MdbProviderRow
            {
                LegacyIdProv = ParseInt(Get(row, "id_prov")),
                Nombre = nombre,
                Direccion = Get(row, "dir_prov"),
                Telefono = Get(row, "fono_prov"),
                Fax = Get(row, "fax_prov"),
                Correo = Get(row, "mail_prov")
            });
        }

        return list;
    }

    private static List<MdbRequesterRow> ReadRequestersCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            return [];
        }

        List<MdbRequesterRow> list = [];
        foreach (Dictionary<string, string> row in ParseCsv(csvPath))
        {
            string alias = Get(row, "alias");
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            list.Add(new MdbRequesterRow
            {
                LegacyIdSol = ParseInt(Get(row, "id_sol")),
                Alias = alias,
                Nombre = Get(row, "nombre_sol"),
                ApellidoPaterno = Get(row, "ape_pat_sol"),
                ApellidoMaterno = Get(row, "ape_mat_sol"),
                Telefono = Get(row, "fono_sol"),
                Correo = Get(row, "mail_sol")
            });
        }

        return list;
    }

    private static List<MdbWorkOrderRow> ReadWorkOrdersCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            return [];
        }

        List<MdbWorkOrderRow> list = [];
        foreach (Dictionary<string, string> row in ParseCsv(csvPath))
        {
            int numeroOt = ParseInt(Get(row, "n_ot"));
            int legacyIdOt = ParseInt(Get(row, "id_ot"));
            if (numeroOt <= 0 || legacyIdOt <= 0)
            {
                continue;
            }

            int legacySol = ParseInt(Get(row, "id_sol"));
            list.Add(new MdbWorkOrderRow
            {
                LegacyIdOt = legacyIdOt,
                NumeroOt = numeroOt,
                LegacyIdProv = ParseInt(Get(row, "id_prov")),
                LegacyIdSol = legacySol > 0 ? legacySol : null,
                FechaOt = ParseAccessDate(Get(row, "fecha_ot")),
                ActivoFlag = ParseBool(Get(row, "activo")),
                GuiaDespacho = ParseInt(Get(row, "guia_des_ot")),
                IdN1 = ParseInt(Get(row, "id_n1")),
                IdN2 = ParseInt(Get(row, "id_n2")),
                IdN3 = ParseInt(Get(row, "id_n3")),
                IdN4 = ParseInt(Get(row, "id_n4")),
                IdN5 = ParseInt(Get(row, "id_n5")),
                IdN6 = ParseInt(Get(row, "id_n6")),
                IdN7 = ParseInt(Get(row, "id_n7")),
                IdN8 = ParseInt(Get(row, "id_n8")),
                IdN9 = ParseInt(Get(row, "id_n9")),
                IdN10 = ParseInt(Get(row, "id_n10")),
                IdNivel = Math.Max(1, ParseInt(Get(row, "id_nivel")))
            });
        }

        return list;
    }

    private static List<MdbWorkOrderDetailRow> ReadWorkOrderDetailsCsv(string csvPath)
    {
        if (!File.Exists(csvPath))
        {
            return [];
        }

        List<MdbWorkOrderDetailRow> list = [];
        foreach (Dictionary<string, string> row in ParseCsv(csvPath))
        {
            int legacyOt = ParseInt(Get(row, "id_ot"));
            string detail = Get(row, "detalle_ot");
            if (legacyOt <= 0 || string.IsNullOrWhiteSpace(detail))
            {
                continue;
            }

            list.Add(new MdbWorkOrderDetailRow
            {
                LegacyIdDet = ParseInt(Get(row, "Id_Det")),
                LegacyIdOt = legacyOt,
                Cantidad = ParseDecimal(Get(row, "cantidad_det")),
                Detalle = detail,
                EstadoDet = ParseBool(Get(row, "Estado_det"))
            });
        }

        return list;
    }

    private static string Get(Dictionary<string, string> row, string key) =>
        row.TryGetValue(key, out string? val) ? val.Trim() : string.Empty;

    private static int ParseInt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : (decimal.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? (int)d : 0);
    }

    private static decimal ParseDecimal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0m;
        }

        if (decimal.TryParse(text.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v))
        {
            return v;
        }

        return decimal.TryParse(text.Trim(), NumberStyles.Any, new CultureInfo("es-CL"), out v) ? v : 0m;
    }

    private static bool ParseBool(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        return normalized is "1" or "true" or "-1" or "sí" or "si" or "yes";
    }

    private static DateTime ParseAccessDate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return DateTime.Today;
        }

        string cleaned = text.Trim();
        string[] formats =
        [
            "MM/dd/yyyy HH:mm:ss",
            "M/d/yyyy H:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
            "d/M/yyyy H:mm:ss",
            "dd-MM-yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy"
        ];

        if (DateTime.TryParseExact(cleaned, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
        {
            return dt.Date;
        }

        if (DateTime.TryParse(cleaned, CultureInfo.CurrentCulture, DateTimeStyles.None, out dt) ||
            DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
        {
            return dt.Date;
        }

        return DateTime.Today;
    }

    private static List<Dictionary<string, string>> ParseCsv(string path)
    {
        string content = File.ReadAllText(path, Encoding.UTF8);
        List<List<string>> rows = ParseCsvRecords(content);
        if (rows.Count <= 1)
        {
            return [];
        }

        List<string> headers = rows[0];
        List<Dictionary<string, string>> result = new(rows.Count - 1);
        for (int i = 1; i < rows.Count; i++)
        {
            List<string> fields = rows[i];
            if (fields.Count == 1 && string.IsNullOrEmpty(fields[0]))
            {
                continue;
            }

            Dictionary<string, string> dict = new(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Count; c++)
            {
                string header = headers[c].Trim();
                if (!string.IsNullOrEmpty(header))
                {
                    dict[header] = c < fields.Count ? fields[c] : string.Empty;
                }
            }

            result.Add(dict);
        }

        return result;
    }

    private static List<List<string>> ParseCsvRecords(string content)
    {
        List<List<string>> records = [];
        List<string> currentRow = [];
        StringBuilder field = new();
        bool inQuotes = false;

        int i = 0;
        if (content.Length > 0 && content[0] == '\uFEFF')
        {
            i = 1;
        }

        while (i < content.Length)
        {
            char ch = content[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                        continue;
                    }

                    inQuotes = false;
                    i++;
                    continue;
                }

                field.Append(ch);
                i++;
            }
            else
            {
                if (ch == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (ch == ',')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    i++;
                }
                else if (ch == '\r' || ch == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    records.Add(currentRow);
                    currentRow = [];

                    if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
                }
                else
                {
                    field.Append(ch);
                    i++;
                }
            }
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString());
            records.Add(currentRow);
        }

        return records;
    }
}
