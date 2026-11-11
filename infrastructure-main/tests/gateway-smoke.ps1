# Prueba de humo del gateway contra el upstream simulado de
# tests/docker-compose.mock.yml. No valida el contrato funcional del OCR real.
[CmdletBinding()]
param(
    [string]$GatewayUrl,
    [string]$AdminUrl
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$envFile = Join-Path $repoRoot '.env'
$proxyPort = '8000'
$adminPort = '8001'

if (Test-Path $envFile) {
    $proxyPortLine = Get-Content $envFile | Where-Object { $_ -match '^KONG_PROXY_PORT=' } | Select-Object -Last 1
    if ($proxyPortLine) {
        $proxyPort = ($proxyPortLine -split '=', 2)[1].Trim()
    }

    $adminPortLine = Get-Content $envFile | Where-Object { $_ -match '^KONG_ADMIN_PORT=' } | Select-Object -Last 1
    if ($adminPortLine) {
        $adminPort = ($adminPortLine -split '=', 2)[1].Trim()
    }
}

if (-not $GatewayUrl) {
    $GatewayUrl = "http://localhost:$proxyPort"
}

if (-not $AdminUrl) {
    $AdminUrl = "http://127.0.0.1:$adminPort"
}

$baseUrl = $GatewayUrl.TrimEnd('/')
$documentId = '11111111-1111-4111-8111-111111111111'

function Assert-True {
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition) {
        throw "ASSERTION FAILED: $Message"
    }
}

function Invoke-CurlRequest {
    param(
        [string]$Url,
        [string[]]$CurlArguments = @()
    )

    $responseFile = [System.IO.Path]::GetTempFileName()
    try {
        $httpCode = & curl.exe -sS -o $responseFile -w '%{http_code}' @CurlArguments $Url
        if ($LASTEXITCODE -ne 0) {
            throw "curl.exe failed with exit code $LASTEXITCODE for $Url."
        }

        [pscustomobject]@{
            StatusCode = [int]$httpCode
            Body = Get-Content -Raw $responseFile
        }
    }
    finally {
        Remove-Item $responseFile -ErrorAction SilentlyContinue
    }
}

function Get-UpstreamRequestLine {
    param([string]$Body)

    $match = [regex]::Match($Body, '(?m)^(GET|POST|DELETE|OPTIONS) (\S+) HTTP/1\.1\r?$')
    if (-not $match.Success) {
        throw 'Could not parse the upstream request line from the mock response.'
    }

    [pscustomobject]@{
        Method = $match.Groups[1].Value
        Path = $match.Groups[2].Value
    }
}

Write-Host 'Checking Kong Admin API...'
$adminResponse = Invoke-CurlRequest -Url "$AdminUrl/status"
Assert-True ($adminResponse.StatusCode -eq 200) "Kong Admin API returned HTTP $($adminResponse.StatusCode) instead of 200."

Write-Host 'Checking list route and query string...'
$listResponse = Invoke-CurlRequest -Url "$baseUrl/api/ocr/v1/documents?page=1&pageSize=10"
Assert-True ($listResponse.StatusCode -eq 200) "List request returned HTTP $($listResponse.StatusCode) instead of 200."
$listRequest = Get-UpstreamRequestLine $listResponse.Body
Assert-True ($listRequest.Method -eq 'GET') 'List request did not reach the upstream as GET.'
Assert-True ($listRequest.Path -eq '/api/v1/documents?page=1&pageSize=10') 'List path or query string was not forwarded correctly.'

Write-Host 'Checking detail route...'
$detailResponse = Invoke-CurlRequest -Url "$baseUrl/api/ocr/v1/documents/$documentId"
Assert-True ($detailResponse.StatusCode -eq 200) "Detail request returned HTTP $($detailResponse.StatusCode) instead of 200."
$detailRequest = Get-UpstreamRequestLine $detailResponse.Body
Assert-True ($detailRequest.Method -eq 'GET') 'Detail request did not reach the upstream as GET.'
Assert-True ($detailRequest.Path -eq "/api/v1/documents/$documentId") 'Detail path was not forwarded correctly.'

Write-Host 'Checking original download route...'
$downloadResponse = Invoke-CurlRequest -Url "$baseUrl/api/ocr/v1/documents/$documentId/original"
Assert-True ($downloadResponse.StatusCode -eq 200) "Original download returned HTTP $($downloadResponse.StatusCode) instead of 200."
$downloadRequest = Get-UpstreamRequestLine $downloadResponse.Body
Assert-True ($downloadRequest.Method -eq 'GET') 'Original download did not reach the upstream as GET.'
Assert-True ($downloadRequest.Path -eq "/api/v1/documents/$documentId/original") 'Original download path was not forwarded correctly.'

Write-Host 'Checking searchable PDF download route...'
$archiveResponse = Invoke-CurlRequest -Url "$baseUrl/api/ocr/v1/documents/$documentId/archive"
Assert-True ($archiveResponse.StatusCode -eq 200) "Archive download returned HTTP $($archiveResponse.StatusCode) instead of 200."
$archiveRequest = Get-UpstreamRequestLine $archiveResponse.Body
Assert-True ($archiveRequest.Method -eq 'GET') 'Archive download did not reach the upstream as GET.'
Assert-True ($archiveRequest.Path -eq "/api/v1/documents/$documentId/archive") 'Archive download path was not forwarded correctly.'

Write-Host 'Checking multipart upload route...'
$tempFile = Join-Path ([System.IO.Path]::GetTempPath()) ("gateway-smoke-{0}.pdf" -f [guid]::NewGuid())
try {
    Set-Content -Path $tempFile -Value '%PDF-1.4 gateway smoke test' -NoNewline -Encoding Ascii
    $uploadResponse = Invoke-CurlRequest -Url "$baseUrl/api/ocr/v1/documents" -CurlArguments @(
        '-X', 'POST',
        '-F', "file=@$tempFile;type=application/pdf"
    )
    Assert-True ($uploadResponse.StatusCode -eq 200) "Upload request returned HTTP $($uploadResponse.StatusCode) instead of 200."
    $uploadRequest = Get-UpstreamRequestLine $uploadResponse.Body
    Assert-True ($uploadRequest.Method -eq 'POST') 'Upload request did not reach the upstream as POST.'
    Assert-True ($uploadRequest.Path -eq '/api/v1/documents') 'Upload path was not forwarded correctly.'
    $contentTypeMatch = [regex]::Match($uploadResponse.Body, '(?m)^Content-Type:\s*(.+)$')
    Assert-True ($contentTypeMatch.Success) 'The mock response does not include the Content-Type header.'
    $contentType = $contentTypeMatch.Groups[1].Value.Trim()
    Assert-True ($contentType -like 'multipart/form-data; boundary=*') 'Multipart content type was not preserved.'
}
finally {
    Remove-Item $tempFile -ErrorAction SilentlyContinue
}

Write-Host 'Checking delete route...'
$deleteResponse = Invoke-CurlRequest -Url "$baseUrl/api/ocr/v1/documents/$documentId" -CurlArguments @('-X', 'DELETE')
Assert-True ($deleteResponse.StatusCode -eq 200) "Delete request returned HTTP $($deleteResponse.StatusCode) instead of 200."
$deleteRequest = Get-UpstreamRequestLine $deleteResponse.Body
Assert-True ($deleteRequest.Method -eq 'DELETE') 'Delete request did not reach the upstream as DELETE.'
Assert-True ($deleteRequest.Path -eq "/api/v1/documents/$documentId") 'Delete path was not forwarded correctly.'

Write-Host 'Checking that unknown public routes are not forwarded...'
$unknownResponse = Invoke-CurlRequest -Url "$baseUrl/api/unknown"
Assert-True ($unknownResponse.StatusCode -eq 404) "Unknown route returned HTTP $($unknownResponse.StatusCode) instead of 404."

Write-Host 'Gateway smoke test passed.'
