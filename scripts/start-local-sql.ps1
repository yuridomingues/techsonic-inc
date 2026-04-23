# Inicia o SQL Server (docker-compose), aguarda ficar pronto, cria o banco TicketPrime e aplica db/schemaTicket.sql.
# Execute na raiz do repositório: .\scripts\start-local-sql.ps1
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$saPassword = "TechSonicInc@2026"
$composeFile = Join-Path $RepoRoot "docker-compose.yml"
if (-not (Test-Path $composeFile)) {
    Write-Error "Arquivo docker-compose.yml nao encontrado na raiz do repositorio."
}

Write-Host "Subindo SQL Server (docker compose)..."
docker compose -f $composeFile up -d

# O motor SQL dentro do container leva ~15–40 s na primeira subida; sqlcmd no stderr com $ErrorActionPreference Stop encerra o script se nao suprimirmos.
Write-Host "Aguardando o SQL Server ficar pronto (pode levar ate alguns minutos na primeira vez)..."
Start-Sleep -Seconds 15

$ready = $false
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "SilentlyContinue"
try {
    for ($i = 0; $i -lt 90; $i++) {
        docker exec ticketprime-sql /opt/mssql-tools18/bin/sqlcmd `
            -S 127.0.0.1 -U sa -P $saPassword -C -l 30 -Q "SELECT 1" *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        if (($i % 5) -eq 0) { Write-Host "  ... ainda aguardando (tentativa $($i + 1)/90)" }
        Start-Sleep -Seconds 2
    }
}
finally {
    $ErrorActionPreference = $prevEap
}

if (-not $ready) {
    Write-Error "SQL Server nao respondeu a tempo. Verifique o Docker: docker logs ticketprime-sql"
}

Write-Host "Criando banco TicketPrime (se nao existir)..."
docker exec ticketprime-sql /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -P $saPassword -C -l 60 -Q "IF DB_ID('TicketPrime') IS NULL CREATE DATABASE TicketPrime;"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Falha ao criar o banco TicketPrime."
}

$schemaPath = Join-Path $RepoRoot "db\schemaTicket.sql"
if (-not (Test-Path $schemaPath)) {
    Write-Error "Arquivo db\schemaTicket.sql nao encontrado."
}

Write-Host "Copiando e aplicando schema..."
docker cp $schemaPath ticketprime-sql:/tmp/schemaTicket.sql
docker exec ticketprime-sql /opt/mssql-tools18/bin/sqlcmd -S 127.0.0.1 -U sa -P $saPassword -C -l 120 -d TicketPrime -i /tmp/schemaTicket.sql
if ($LASTEXITCODE -ne 0) {
    Write-Warning "sqlcmd retornou codigo nao zero. Se o schema ja tinha sido aplicado, isso e esperado. Caso contrario, veja: docker logs ticketprime-sql"
}

Write-Host "Concluido. Connection string de desenvolvimento: Server=127.0.0.1,1433;Database=TicketPrime;User Id=sa;Password=$saPassword;TrustServerCertificate=True;"
