[CmdletBinding()]
param(
    [switch]$MockOcr
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot '.env'

if (-not (Test-Path $envFile)) {
    Copy-Item (Join-Path $repoRoot '.env.example') $envFile
    Write-Host 'Created .env from .env.example.'
}

function Get-EnvValue {
    param(
        [string]$Key,
        [string]$Default
    )

    $pattern = '^{0}=' -f [regex]::Escape($Key)
    $line = Get-Content $envFile | Where-Object { $_ -match $pattern } | Select-Object -Last 1
    if ($line) {
        return ($line -split '=', 2)[1].Trim()
    }

    return $Default
}

# Kong DB-less no interpola variables dentro del archivo declarativo, por eso
# la plantilla se renderiza antes de levantar el stack.
$frontendOrigin = Get-EnvValue -Key 'FRONTEND_ORIGIN' -Default 'http://localhost:5173'
if ([string]::IsNullOrWhiteSpace($frontendOrigin)) {
    throw 'FRONTEND_ORIGIN must not be empty.'
}

$templatePath = Join-Path $repoRoot 'kong/kong.yml.template'
$renderedPath = Join-Path $repoRoot 'kong/kong.yml'
$template = Get-Content -Raw $templatePath
$rendered = $template.Replace('{{FRONTEND_ORIGIN}}', $frontendOrigin)

if ($rendered -match '\{\{[^}]+\}\}') {
    throw "Unresolved placeholders remain in $templatePath."
}

[System.IO.File]::WriteAllText(
    $renderedPath,
    $rendered,
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Rendered kong/kong.yml for FRONTEND_ORIGIN=$frontendOrigin."

$composeArgs = @('compose', '-f', 'docker-compose.yml')
if ($MockOcr) {
    $composeArgs += @('-f', 'tests/docker-compose.mock.yml')
}

$composeArgs += @('up', '-d', '--wait', '--wait-timeout', '180')
if ($MockOcr) {
    # La prueba simulada valida exclusivamente Kong y su upstream; no necesita
    # construir PostgreSQL, worker, ACL ni conectarse a Paperless.
    $composeArgs += @('kong', 'ocr-api')
}
else {
    # Los repositorios de OCR y ACL se construyen desde el código local. Sin
    # --build, Compose podría reutilizar imágenes anteriores y ocultar cambios.
    $composeArgs += '--build'
}

Push-Location $repoRoot
try {
    & docker @composeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose up failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

$adminPort = 8001
$adminPortLine = Get-Content $envFile | Where-Object { $_ -match '^KONG_ADMIN_PORT=' } | Select-Object -Last 1
if ($adminPortLine) {
    $adminPort = ($adminPortLine -split '=', 2)[1].Trim()
}

$proxyPort = 8000
$proxyPortLine = Get-Content $envFile | Where-Object { $_ -match '^KONG_PROXY_PORT=' } | Select-Object -Last 1
if ($proxyPortLine) {
    $proxyPort = ($proxyPortLine -split '=', 2)[1].Trim()
}

$deadline = (Get-Date).AddSeconds(60)
$ready = $false
do {
    Start-Sleep -Seconds 2
    try {
        $statusResponse = Invoke-WebRequest -Uri "http://127.0.0.1:$adminPort/status" -UseBasicParsing -TimeoutSec 2
        if ($statusResponse.StatusCode -eq 200) {
            $ready = $true
        }
    }
    catch {
        $ready = $false
    }
} while (-not $ready -and (Get-Date) -lt $deadline)

if (-not $ready) {
    throw "Kong did not become ready at http://127.0.0.1:$adminPort/status within 60 seconds."
}

Write-Host "Kong is ready at http://localhost:$proxyPort. Admin API: http://localhost:$adminPort."
