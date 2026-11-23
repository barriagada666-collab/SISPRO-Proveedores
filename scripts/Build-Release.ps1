param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Write-Host "=== 1. Restaurando dependencias ==="
dotnet restore SISPRO.sln

Write-Host "=== 2. Compilando Debug (0 errores, 0 advertencias) ==="
dotnet build SISPRO.sln -c Debug --no-restore

Write-Host "=== 3. Compilando Release (0 errores, 0 advertencias) ==="
dotnet build SISPRO.sln -c Release --no-restore

Write-Host "=== 4. Ejecutando pruebas automatizadas ==="
dotnet test SISPRO.sln -c Release --no-build --verbosity minimal

Write-Host "=== 5. Publicando paquete autocontenido win-x64 ==="
$pubDir = Join-Path $root "publish\win-x64"
if (Test-Path $pubDir) { Remove-Item $pubDir -Recurse -Force }
dotnet publish src\Sispro.UI\Sispro.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -o $pubDir

if (-not $SkipInstaller) {
    $isccCandidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        throw "No se encontró Inno Setup 6 (ISCC.exe)."
    }

    Write-Host "=== 6. Compilando instalador Inno Setup 6 ==="
    & $iscc (Join-Path $root "installer\Setup_SISPRO.iss")

    $setupExe = Join-Path $root "installer\Output\SISPRO_Setup_v2.0.0_win-x64.exe"
    $hash = (Get-FileHash $setupExe -Algorithm SHA256).Hash
    $checksumFile = Join-Path $root "installer\Output\checksums_sha256.txt"
    "$hash  SISPRO_Setup_v2.0.0_win-x64.exe" | Set-Content -Path $checksumFile -Encoding UTF8
    Write-Host "Instalador generado: $setupExe"
    Write-Host "SHA-256: $hash"
}
