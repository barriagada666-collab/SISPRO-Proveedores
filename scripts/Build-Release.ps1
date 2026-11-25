param(
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

function Assert-LastExitCode([string]$stepName) {
    if ($LASTEXITCODE -ne 0) {
        throw "Falló el paso '$stepName' con código de salida $LASTEXITCODE."
    }
}

Write-Host "=== 1. Restaurando dependencias ==="
dotnet restore SISPRO.sln
Assert-LastExitCode "dotnet restore SISPRO.sln"
dotnet restore src\Sispro.UI\Sispro.UI.csproj -r win-x64
Assert-LastExitCode "dotnet restore win-x64"

Write-Host "=== 2. Compilando Debug (0 errores, 0 advertencias) ==="
dotnet build SISPRO.sln -c Debug --no-restore
Assert-LastExitCode "dotnet build Debug"

Write-Host "=== 3. Compilando Release (0 errores, 0 advertencias) ==="
dotnet build SISPRO.sln -c Release --no-restore
Assert-LastExitCode "dotnet build Release"

Write-Host "=== 4. Ejecutando pruebas automatizadas ==="
dotnet test SISPRO.sln -c Release --no-build --verbosity minimal
Assert-LastExitCode "dotnet test Release"

Write-Host "=== 5. Publicando paquete autocontenido win-x64 ==="
$pubDir = Join-Path $root "publish\win-x64"
if (Test-Path $pubDir) { Remove-Item $pubDir -Recurse -Force }
dotnet publish src\Sispro.UI\Sispro.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true --no-restore -o $pubDir
Assert-LastExitCode "dotnet publish win-x64"
if (-not (Test-Path (Join-Path $pubDir "SISPRO.exe"))) {
    throw "No se generó SISPRO.exe en '$pubDir'."
}

if (-not $SkipInstaller) {
    $isccCmd = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    $isccCandidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        ($isccCmd?.Source)
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $iscc) {
        throw "No se encontró Inno Setup 6 (ISCC.exe)."
    }

    Write-Host "=== 6. Compilando instalador Inno Setup 6 ==="
    & $iscc (Join-Path $root "installer\Setup_SISPRO.iss")
    Assert-LastExitCode "ISCC.exe"

    [xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
    $appVersion = $props.Project.PropertyGroup.Version.Trim()
    $setupName = "SISPRO_Setup_v${appVersion}_win-x64.exe"
    $setupExe = Join-Path $root "installer\Output\$setupName"
    $hash = (Get-FileHash $setupExe -Algorithm SHA256).Hash
    $checksumFile = Join-Path $root "installer\Output\checksums_sha256.txt"
    "$hash  $setupName" | Set-Content -Path $checksumFile -Encoding UTF8
    Write-Host "Instalador generado: $setupExe"
    Write-Host "SHA-256: $hash"
}
