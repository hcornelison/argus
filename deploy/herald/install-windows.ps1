# Publishes and installs the Argus Herald agent as a Windows Service.
# Run from an elevated PowerShell prompt:
#   .\install-windows.ps1
param(
    [string]$RepoRoot = (Resolve-Path "$PSScriptRoot\..\.."),
    [string]$InstallDir = "C:\Program Files\Argus\Herald",
    [string]$ServiceName = "ArgusHerald"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing herald (win-x64)..."
dotnet publish "$RepoRoot\src\Argus.Herald\Argus.Herald.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o $InstallDir

$exe = Join-Path $InstallDir "Argus.Herald.exe"

if (Get-Service $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Stopping existing service..."
    Stop-Service $ServiceName
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating service $ServiceName..."
New-Service -Name $ServiceName -BinaryPathName $exe -DisplayName "Argus Herald" -StartupType Automatic
Start-Service $ServiceName

Write-Host "Done. Edit $InstallDir\appsettings.json (StyxGrpcEndpoint, ApiKey, LogPaths),"
Write-Host "then: Restart-Service $ServiceName"
