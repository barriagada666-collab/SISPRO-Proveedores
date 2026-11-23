param(
    [string]$MdbPath = "D:\archivos qlabel\Proveedores\data\legacy\OT_PROV.mdb",
    [string]$SqlitePath = "D:\archivos qlabel\Proveedores\data\sispro.db"
)

$ErrorActionPreference = "Stop"
$tempJson = Join-Path ([System.IO.Path]::GetTempPath()) "sispro_mdb_export.json"
$ps32 = "C:\Windows\SysWOW64\WindowsPowerShell\v1.0\powershell.exe"

if (-not (Test-Path $MdbPath)) {
    throw "No se encontró el archivo Access: $MdbPath"
}

Write-Host "Extrayendo tablas desde $MdbPath (Jet OLEDB 32-bit)..."
& $ps32 -NoProfile -Command {
    param($mdb, $outJson)
    $ErrorActionPreference = "Stop"
    $enc1252 = [System.Text.Encoding]::GetEncoding(1252)
    $conn = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=$mdb;")
    $conn.Open()

    function Read-Table($sql) {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $sql
        $rdr = $cmd.ExecuteReader()
        $list = New-Object System.Collections.ArrayList
        while ($rdr.Read()) {
            $obj = @{}
            for ($i = 0; $i -lt $rdr.FieldCount; $i++) {
                $name = $rdr.GetName($i)
                $val = $rdr.GetValue($i)
                if ($val -is [System.DBNull]) {
                    $obj[$name] = $null
                } elseif ($val -is [System.DateTime]) {
                    $obj[$name] = $val.ToString("yyyy-MM-dd")
                } else {
                    $obj[$name] = $val
                }
            }
            [void]$list.Add($obj)
        }
        $rdr.Close()
        return ,$list
    }

    $usuariosRaw = Read-Table "SELECT * FROM Usuarios"
    foreach ($u in $usuariosRaw) {
        $p = [string]$u["pass_usuario"]
        $bytes = $enc1252.GetBytes($p)
        $hex = ($bytes | ForEach-Object { $_.ToString("X2") }) -join ""
        $u["pass_hex"] = $hex
    }

    $export = @{
        Nivel       = Read-Table "SELECT * FROM Nivel ORDER BY id_nivel"
        Proveedores = Read-Table "SELECT * FROM Proveedores ORDER BY id_prov"
        Solicitante = Read-Table "SELECT * FROM Solicitante ORDER BY id_sol"
        Usuarios    = $usuariosRaw
        Permisos    = Read-Table "SELECT * FROM Permisos"
        OT_PROV     = Read-Table "SELECT * FROM OT_PROV ORDER BY id_ot"
        Detalle_ot  = Read-Table "SELECT * FROM Detalle_ot ORDER BY Id_Det"
        Facturas    = Read-Table "SELECT * FROM Facturas"
        Facturas_loc = Read-Table "SELECT * FROM Facturas_loc"
        VB          = Read-Table "SELECT * FROM VB"
    }

    $conn.Close()
    $json = $export | ConvertTo-Json -Depth 6 -Compress
    [System.IO.File]::WriteAllText($outJson, $json, [System.Text.Encoding]::UTF8)
} -Args $MdbPath, $tempJson

$pyScript = Join-Path $PSScriptRoot "build_sqlite.py"
Write-Host "Construyendo base SQLite $SqlitePath..."
python $pyScript $tempJson $SqlitePath
Remove-Item $tempJson -Force -ErrorAction SilentlyContinue
Write-Host "Migración completada exitosamente."
