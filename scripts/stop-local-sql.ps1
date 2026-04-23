# Para o SQL Server local (container). Dados persistem no volume Docker salvo se usar docker compose down -v.
# Execute na raiz do repositorio: .\scripts\stop-local-sql.ps1
$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

$composeFile = Join-Path $RepoRoot "docker-compose.yml"
if (-not (Test-Path $composeFile)) {
    Write-Error "Arquivo docker-compose.yml nao encontrado na raiz do repositorio."
}

Write-Host "Encerrando servicos (docker compose down)..."
docker compose -f $composeFile down
Write-Host "Concluido."
