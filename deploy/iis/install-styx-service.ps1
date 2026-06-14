# Publishes styx and installs it as a Windows Service (Kestrel).
#
# Why a service and not an IIS site: styx exposes a gRPC endpoint for the herald agents,
# and ASP.NET Core gRPC servers are NOT supported behind IIS (HTTP/2 trailers). styx runs
# on Kestrel as a service; IIS hosts the pantheon site and reverse-proxies REST + SignalR
# to this service (see install-pantheon-iis.ps1 and README.md).
#
# Run from an elevated PowerShell prompt:
#   .\install-styx-service.ps1
param(
    [string]$RepoRoot   = (Resolve-Path "$PSScriptRoot\..\.."),
    [string]$InstallDir = "C:\Program Files\Argus\Styx",
    [string]$ServiceName = "ArgusStyx",
    # SQL Server connection for the data store. Override for your environment.
    [string]$ConnectionString = "Server=localhost;Database=Argus;Trusted_Connection=True;TrustServerCertificate=True"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing styx (win-x64, self-contained)..."
dotnet publish "$RepoRoot\src\Argus.Styx\Argus.Styx.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $InstallDir

$exe = Join-Path $InstallDir "Argus.Styx.exe"

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Removing existing service..."
    Stop-Service $ServiceName -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service $ServiceName..."
New-Service -Name $ServiceName -BinaryPathName "`"$exe`"" -DisplayName "Argus Styx" -StartupType Automatic

# Persist the connection string for the service (machine-level env var the host picks up).
[Environment]::SetEnvironmentVariable("ConnectionStrings__Argus", $ConnectionString, "Machine")

Write-Host "Starting service..."
Start-Service $ServiceName

Write-Host ""
Write-Host "Done. styx listens on http://localhost:8080 (REST + SignalR) and :8081 (gRPC ingest)."
Write-Host "Review $InstallDir\appsettings.json (Retention:Days, Kestrel endpoints)."
Write-Host "IMPORTANT: set a real SQL connection string before exposing this to agents. To change it later:"
Write-Host "  [Environment]::SetEnvironmentVariable('ConnectionStrings__Argus','<conn>','Machine'); Restart-Service $ServiceName"
