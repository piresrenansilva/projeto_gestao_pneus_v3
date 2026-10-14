$ErrorActionPreference = "Stop"
$url = "http://localhost:5080/"
$serverResponding = $false
$loginStatusReady = $false

try {
    $statusResponse = Invoke-WebRequest -Uri "${url}api/login/status" -TimeoutSec 2 -UseBasicParsing
    $statusBody = $statusResponse.Content | ConvertFrom-Json
    $serverResponding = $statusResponse.StatusCode -eq 200
    $loginStatusReady = $serverResponding -and $statusBody.configurado -is [bool]
}
catch {
    try {
        $rootResponse = Invoke-WebRequest -Uri $url -TimeoutSec 2 -UseBasicParsing
        $serverResponding = $rootResponse.StatusCode -ge 200 -and $rootResponse.StatusCode -lt 500
    }
    catch {
        $serverResponding = $null -ne $_.Exception.Response
    }
}

if ($loginStatusReady) {
    Start-Process $url
    exit 0
}

if ($serverResponding) {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(
        "Uma instancia antiga do sistema ainda esta em execucao e bloqueia a atualizacao. Feche-a antes de iniciar novamente.",
        "Fabrica de Pneus",
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Warning
    ) | Out-Null
    exit 1
}

$logDirectory = Join-Path $env:LOCALAPPDATA "FabricaPneus\logs"
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stdoutLog = Join-Path $logDirectory "servidor-$timestamp.log"
$stderrLog = Join-Path $logDirectory "servidor-$timestamp-error.log"
$env:ASPNETCORE_ENVIRONMENT = "Development"

try {
    $server = Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @("run", "--no-launch-profile") `
        -WorkingDirectory $PSScriptRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutLog `
        -RedirectStandardError $stderrLog `
        -PassThru
}
catch {
    Add-Type -AssemblyName PresentationFramework
    [System.Windows.MessageBox]::Show(
        "Nao foi possivel iniciar o sistema. Verifique se o .NET esta instalado.`n`nDetalhes: $($_.Exception.Message)",
        "Fabrica de Pneus",
        [System.Windows.MessageBoxButton]::OK,
        [System.Windows.MessageBoxImage]::Error
    ) | Out-Null
    exit 1
}

$deadline = (Get-Date).AddSeconds(60)
while ((Get-Date) -lt $deadline -and -not $server.HasExited) {
    try {
        $response = Invoke-WebRequest -Uri $url -TimeoutSec 2 -UseBasicParsing
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
            Start-Process $url
            exit 0
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

$logDetails = "Saída: $stdoutLog`nErros: $stderrLog"
if ($server.HasExited) {
    $message = "O servidor foi encerrado durante a inicialização."
}
else {
    $message = "O servidor não respondeu após 60 segundos."
}

Add-Type -AssemblyName PresentationFramework
[System.Windows.MessageBox]::Show(
    "$message`n`n$logDetails",
    "Fabrica de Pneus",
    [System.Windows.MessageBoxButton]::OK,
    [System.Windows.MessageBoxImage]::Error
) | Out-Null
