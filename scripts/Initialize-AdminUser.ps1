<#
.SYNOPSIS
    Aprovisionamiento seguro e idempotente de una cuenta Administrador inicial en SISPRO (SQLite).
.DESCRIPTION
    Genera el hash PBKDF2-HMAC-SHA256 (100.000 iteraciones + salt aleatorio CSPRNG de 16 bytes)
    en memoria, sin almacenar credenciales en texto plano en el código fuente ni en los logs,
    y crea la cuenta únicamente si el nombre de usuario aún no existe en la base de datos.
.EXAMPLE
    $env:SISPRO_INIT_ADMIN_PASSWORD = "<clave_inicial>"
    .\scripts\Initialize-AdminUser.ps1 -DatabasePath ".\data\sispro.db" -Username "barriagada" -FirstName "B." -LastName "Arriagada"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$DatabasePath,

    [Parameter(Mandatory = $true)]
    [string]$Username,

    [string]$FirstName = "Administrador",
    [string]$LastName = "Sistema",
    [string]$SecondLastName = "",
    [string]$Email = "",
    [string]$Phone = "",

    [SecureString]$Password,

    [bool]$RequirePasswordChange = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $DatabasePath)) {
    throw "No se encontró la base de datos SQLite en: $DatabasePath"
}

$plainPwd = $null
if ($null -ne $Password) {
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Password)
    try {
        $plainPwd = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}
elseif (-not [string]::IsNullOrEmpty($env:SISPRO_INIT_ADMIN_PASSWORD)) {
    $plainPwd = $env:SISPRO_INIT_ADMIN_PASSWORD
}
else {
    $promptSec = Read-Host -Prompt "Ingrese contraseña inicial para '$Username'" -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($promptSec)
    try {
        $plainPwd = [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

if ([string]::IsNullOrWhiteSpace($plainPwd) -or $plainPwd.Length -lt 6) {
    throw "La contraseña inicial debe tener al menos 6 caracteres."
}

# Compute PBKDF2-HMAC-SHA256 (100,000 iterations, 16-byte CSPRNG salt, 32-byte key) compatible with PasswordSecurity.cs
$iterations = 100000
$salt = New-Object byte[] 16
[System.Security.Cryptography.RandomNumberGenerator]::Fill($salt)
$derived = [System.Security.Cryptography.Rfc2898DeriveBytes]::Pbkdf2(
    [System.Text.Encoding]::UTF8.GetBytes($plainPwd),
    $salt,
    $iterations,
    [System.Security.Cryptography.HashAlgorithmName]::SHA256,
    32
)
$pbkdf2Hash = "PBKDF2`$$iterations`$$([Convert]::ToBase64String($salt))`$$([Convert]::ToBase64String($derived))"
$plainPwd = $null

$pythonScript = @'
import sqlite3, sys

db_path, alias, pwd_hash, first_name, last_name, second_last, email, phone, req_change = sys.argv[1:10]
req_flag = 1 if req_change.lower() == "true" else 0

con = sqlite3.connect(db_path)
con.execute("PRAGMA foreign_keys = ON;")
cur = con.cursor()

cols = {row[1].lower() for row in cur.execute("PRAGMA table_info(Usuarios);")}
if "codigo_id" not in cols:
    cur.execute("ALTER TABLE Usuarios ADD COLUMN codigo_id INTEGER;")
if "debe_cambiar_clave" not in cols:
    cur.execute("ALTER TABLE Usuarios ADD COLUMN debe_cambiar_clave INTEGER NOT NULL DEFAULT 0;")
if "fecha_modificacion" not in cols:
    cur.execute("ALTER TABLE Usuarios ADD COLUMN fecha_modificacion TEXT NOT NULL DEFAULT '';")
    cur.execute("UPDATE Usuarios SET fecha_modificacion = COALESCE(NULLIF(fecha_creacion, ''), datetime('now','localtime')) WHERE fecha_modificacion = '' OR fecha_modificacion IS NULL;")

missing = [r[0] for r in cur.execute("SELECT id_usuario FROM Usuarios WHERE codigo_id IS NULL ORDER BY rowid;")]
if missing:
    next_code = cur.execute("SELECT COALESCE(MAX(codigo_id), 0) + 1 FROM Usuarios;").fetchone()[0]
    for uid in missing:
        cur.execute("UPDATE Usuarios SET codigo_id = ? WHERE id_usuario = ?;", (next_code, uid))
        next_code += 1

cur.execute("CREATE UNIQUE INDEX IF NOT EXISTS IX_Usuarios_CodigoId ON Usuarios(codigo_id);")
cur.execute("CREATE INDEX IF NOT EXISTS IX_Usuarios_Nivel_Activo ON Usuarios(id_nivel, activo);")

exists = cur.execute("SELECT COUNT(*) FROM Usuarios WHERE LOWER(alias_usuario) = LOWER(?);", (alias,)).fetchone()[0]
if exists > 0:
    con.commit()
    con.close()
    print(f"OMITIDO: El usuario '{alias}' ya existe en {db_path} (operación idempotente).")
    sys.exit(0)

next_code = cur.execute("SELECT COALESCE(MAX(codigo_id), 0) + 1 FROM Usuarios;").fetchone()[0]
user_id = f"USR-{next_code:04d}"
while cur.execute("SELECT COUNT(*) FROM Usuarios WHERE id_usuario = ?;", (user_id,)).fetchone()[0] > 0:
    next_code += 1
    user_id = f"USR-{next_code:04d}"

cur.execute("""
    INSERT INTO Usuarios(
        id_usuario, codigo_id, alias_usuario, pass_hash, nombre_usuario, ape_pat_usuario,
        ape_mat_usuario, id_nivel, mail_usuario, fono_usuario, es_solicitante,
        activo, debe_cambiar_clave, fecha_creacion, fecha_modificacion
    ) VALUES (
        ?, ?, ?, ?, ?, ?, ?, 4, ?, ?, 0, 1, ?, datetime('now','localtime'), datetime('now','localtime')
    );
""", (user_id, next_code, alias, pwd_hash, first_name, last_name, second_last, email, phone, req_flag))

cur.execute("""
    INSERT INTO Permisos(
        id_usuario, m1_ot, tab_0_1_ingreso_ot, tab_0_vbj, tab_0_vbs,
        tab_0_2_busqueda_ot, tab_0_3_impresion_ot, m2_facturas,
        tab_1_0_ingreso_fac, tab_1_1_busqueda_fac, m3_mantenedor,
        m4_usuarios, tab_2_0_ingreso_us, tab_2_1_listado_us
    ) VALUES (?, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
""", (user_id,))

cur.execute("""
    INSERT INTO AuditLog(usuario, modulo, accion, detalle)
    VALUES ('SISTEMA', 'Seguridad', 'INIT_ADMIN_PROVISIONED', ?);
""", (f"Cuenta inicial Administrador '{alias}' (ID {next_code}) creada de forma segura (CambioClavePrimerAcceso={req_change}).",))

con.commit()
con.close()
print(f"CREADO: Usuario Administrador '{alias}' (ID {next_code}, PK {user_id}) en {db_path}.")
'@

$tmpPy = Join-Path ([System.IO.Path]::GetTempPath()) ("sispro_init_admin_" + [Guid]::NewGuid().ToString("N") + ".py")
try {
    Set-Content -LiteralPath $tmpPy -Value $pythonScript -Encoding UTF8
    & python $tmpPy $DatabasePath $Username $pbkdf2Hash $FirstName $LastName $SecondLastName $Email $Phone $RequirePasswordChange.ToString()
    if ($LASTEXITCODE -ne 0) {
        throw "Error al ejecutar el aprovisionamiento en SQLite (código $LASTEXITCODE)."
    }
}
finally {
    if (Test-Path -LiteralPath $tmpPy) {
        Remove-Item -LiteralPath $tmpPy -Force -ErrorAction SilentlyContinue
    }
}
