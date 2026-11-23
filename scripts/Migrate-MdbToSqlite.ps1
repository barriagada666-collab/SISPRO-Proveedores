<#
.SYNOPSIS
    Migración integral de todas las tablas desde OT_PROV.mdb, BD MANTENCION.mdb y PERSONAL_MANTENCION.mdb hacia SQLite (sispro.db).
.DESCRIPTION
    Usa streaming CSV UTF-8 mediante Jet OLEDB 32-bit para exportar de forma ultrarrápida
    las ~340.000 filas históricas (7.284 OTs de proveedores, 7.803 detalles, 214 proveedores,
    1.823 activos industriales con ficha técnica y árbol 10 niveles, 43.994 OTs internas,
    208.121 tareas MP, 2.462 insumos, 134 técnicos, checklists y catálogos) y construye
    la base SQLite normalizada, indexada y validada con PRAGMA integrity_check y foreign_key_check.
#>
param(
    [string]$OtProvMdb = "D:\archivos qlabel\Proveedores\OT_PROV.mdb",
    [string]$MantencionMdb = "D:\archivos qlabel\Proveedores\BD MANTENCION.mdb",
    [string]$PersonalMdb = "D:\archivos qlabel\Proveedores\PERSONAL_MANTENCION.mdb",
    [string]$LegacyOtProvMdb = "D:\archivos qlabel\Proveedores\data\legacy\OT_PROV.mdb",
    [string]$SqlitePath = "D:\archivos qlabel\Proveedores\data\sispro.db"
)

$ErrorActionPreference = "Stop"
$ps32 = "C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
$dumpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("sispro_mdb_dump_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $dumpDir -Force | Out-Null

if (-not (Test-Path $OtProvMdb)) {
    throw "No se encontró OT_PROV.mdb en: $OtProvMdb"
}
if (-not (Test-Path $MantencionMdb)) {
    throw "No se encontró BD MANTENCION.mdb en: $MantencionMdb"
}

$exportBlock = {
    param($otMdb, $mantMdb, $persMdb, $legacyOtMdb, $outDir)
    $ErrorActionPreference = "Stop"
    $enc1252 = [System.Text.Encoding]::GetEncoding(1252)
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    function Escape-Csv([string]$v) {
        if ($null -eq $v) { return "" }
        if ($v.IndexOfAny([char[]]@('"', ',', "`r", "`n")) -ge 0) {
            return '"' + $v.Replace('"', '""') + '"'
        }
        return $v
    }

    function Export-TableCsv($conn, [string]$tableName, [string]$outFile, [bool]$addPassHex = $false) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = "SELECT * FROM [$tableName]"
        $rdr = $cmd.ExecuteReader()
        $sw = New-Object System.IO.StreamWriter($outFile, $false, $utf8NoBom, 65536)
        try {
            $fc = $rdr.FieldCount
            $headers = New-Object System.Collections.Generic.List[string]
            $passIdx = -1
            for ($i = 0; $i -lt $fc; $i++) {
                $name = $rdr.GetName($i)
                $headers.Add((Escape-Csv $name))
                if ($addPassHex -and $name -ieq "pass_usuario") { $passIdx = $i }
            }
            if ($addPassHex) { $headers.Add("pass_hex") }
            $sw.WriteLine(($headers -join ","))

            $count = 0
            $vals = New-Object string[] ($fc + $(if ($addPassHex) { 1 } else { 0 }))
            while ($rdr.Read()) {
                for ($i = 0; $i -lt $fc; $i++) {
                    $val = $rdr.GetValue($i)
                    if ($val -is [System.DBNull] -or $null -eq $val) {
                        $vals[$i] = ""
                    } elseif ($val -is [System.DateTime]) {
                        if ($val.TimeOfDay.TotalSeconds -eq 0) {
                            $vals[$i] = $val.ToString("yyyy-MM-dd")
                        } else {
                            $vals[$i] = $val.ToString("yyyy-MM-dd HH:mm:ss")
                        }
                    } elseif ($val -is [bool]) {
                        $vals[$i] = if ($val) { "1" } else { "0" }
                    } else {
                        $vals[$i] = Escape-Csv ([System.Convert]::ToString($val, [System.Globalization.CultureInfo]::InvariantCulture))
                    }
                }
                if ($addPassHex) {
                    $rawPass = if ($passIdx -ge 0 -and -not $rdr.IsDBNull($passIdx)) { [string]$rdr.GetValue($passIdx) } else { "" }
                    $bytes = $enc1252.GetBytes($rawPass)
                    $sb = New-Object System.Text.StringBuilder($bytes.Length * 2)
                    foreach ($b in $bytes) { [void]$sb.Append($b.ToString("X2")) }
                    $vals[$fc] = $sb.ToString()
                }
                $sw.WriteLine(($vals -join ","))
                $count++
            }
            Write-Host ("  [OK] {0}: {1} filas" -f $tableName, $count)
        }
        finally {
            $sw.Dispose()
            $rdr.Close()
        }
    }

    Write-Host "=== Exportando OT_PROV.mdb ==="
    $c1 = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$otMdb;")
    $c1.Open()
    try {
        foreach ($t in @("Nivel", "Proveedores", "Solicitante", "Permisos", "OT_PROV", "Detalle_ot", "Facturas", "Facturas_loc", "VB")) {
            Export-TableCsv $c1 $t (Join-Path $outDir "ot_$t.csv") $false
        }
        Export-TableCsv $c1 "Usuarios" (Join-Path $outDir "ot_Usuarios.csv") $true
    }
    finally { $c1.Close() }

    if (Test-Path $legacyOtMdb) {
        Write-Host "=== Exportando solicitantes históricos (archivo legacy) ==="
        $cLeg = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$legacyOtMdb;")
        $cLeg.Open()
        try {
            Export-TableCsv $cLeg "Solicitante" (Join-Path $outDir "legacy_Solicitante.csv") $false
        }
        finally { $cLeg.Close() }
    }

    Write-Host "=== Exportando BD MANTENCION.mdb ==="
    $c2 = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$mantMdb;")
    $c2.Open()
    try {
        $mantTables = @(
            "ACTIVO", "N1_N2", "N1_N3", "N1_N4", "N1_N5", "N1_N6", "N1_N7", "N1_N8", "N1_N9", "N1_N10",
            "OFICIOS", "MOTIVO", "EMPRESA", "DESC_INFORMES",
            "PERSONAL", "ROPA", "INSUMOS2", "INSUMOS_UTILIZADOS",
            "OT", "OT_EXTERNA", "OT_EXTERNA_MP", "OT_EXTERNA_OT",
            "MP", "CICLOMP", "TAREAS_MP", "PERSONAL_UTILIZADO",
            "CHECKLIST", "TAREAS_CHECKLIST", "CHK_TAREAS_PENDIENTES", "CHK_TAREAS_REALIZADAS", "PERSONAL_CHECKLIST",
            "USUARIOS", "PERMISOS_USUARIO"
        )
        foreach ($t in $mantTables) {
            Export-TableCsv $c2 $t (Join-Path $outDir "mant_$t.csv") $false
        }
    }
    finally { $c2.Close() }

    if (Test-Path $persMdb) {
        Write-Host "=== Exportando PERSONAL_MANTENCION.mdb ==="
        $c3 = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$persMdb;")
        $c3.Open()
        try {
            Export-TableCsv $c3 "PERSONAL" (Join-Path $outDir "rrhh_PERSONAL.csv") $false
        }
        finally { $c3.Close() }
    }
}

try {
    & $ps32 -NoProfile -Command $exportBlock -Args $OtProvMdb, $MantencionMdb, $PersonalMdb, $LegacyOtProvMdb, $dumpDir
    if ($LASTEXITCODE -ne 0) {
        throw "Error durante la extracción Access 32-bit (código $LASTEXITCODE)."
    }

    $pyScript = Join-Path $PSScriptRoot "build_sqlite.py"
    Write-Host "=== Construyendo base SQLite unificada: $SqlitePath ==="
    python $pyScript $dumpDir $SqlitePath
    if ($LASTEXITCODE -ne 0) {
        throw "Error construyendo SQLite (código $LASTEXITCODE)."
    }
}
finally {
    if (Test-Path $dumpDir) {
        Remove-Item $dumpDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Host "Migración integral completada exitosamente."
