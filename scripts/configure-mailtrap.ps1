# Preenche Email:Smtp em src/appsettings.json para testar envio (ex.: Mailtrap Sandbox).
# Execute na raiz do repositorio: .\scripts\configure-mailtrap.ps1
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$path = Join-Path $RepoRoot "src\appsettings.json"
if (-not (Test-Path $path)) {
    Write-Error "Arquivo src\appsettings.json nao encontrado."
}

Write-Host "Informe as credenciais SMTP (Mailtrap Sandbox ou outro provedor de testes)."
$from = Read-Host "From address (ex.: no-reply@ticketprime.local)"
$smtpHost = Read-Host "SMTP host"
$port = [int](Read-Host "SMTP port (ex.: 587)")
$user = Read-Host "SMTP user"
$plain = Read-Host "SMTP password" -AsSecureString
$BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($plain)
$password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)

$json = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
$json.Email.FromAddress = $from
$json.Email.Smtp.Host = $smtpHost
$json.Email.Smtp.Port = $port
$json.Email.Smtp.User = $user
$json.Email.Smtp.Password = $password
$json.Email.Smtp.EnableSsl = $true

$json | ConvertTo-Json -Depth 20 | Set-Content $path -Encoding UTF8
Write-Host "Atualizado: $path"
Write-Host "Reinicie a API: dotnet run --project src/techsonic-inc.csproj"
