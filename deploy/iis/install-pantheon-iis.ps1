# Builds the pantheon SPA and deploys it as an IIS site.
#
# Prerequisites on the IIS host:
#   - IIS with the "URL Rewrite" and "Application Request Routing" (ARR) modules installed,
#     and ARR proxy enabled (IIS Manager > server node > Application Request Routing Cache >
#     Server Proxy Settings > "Enable proxy"). Required for the /api + /hubs reverse proxy.
#   - The "WebSocket Protocol" Windows feature (for SignalR).
#   - Node.js (to build the Angular app), unless you build elsewhere and copy dist/.
#
# Run from an elevated PowerShell prompt:
#   .\install-pantheon-iis.ps1
param(
    [string]$RepoRoot  = (Resolve-Path "$PSScriptRoot\..\.."),
    [string]$SitePath  = "C:\inetpub\argus-pantheon",
    [string]$SiteName  = "ArgusPantheon",
    [string]$AppPool   = "ArgusPantheon",
    [int]$Port         = 8080  # site port; styx service stays on 8080 internally via localhost — pick a free port
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

# Note: styx listens on localhost:8080; choose a DIFFERENT public port for this site.
if ($Port -eq 8080) { $Port = 80 }

Write-Host "Building pantheon (production)..."
Push-Location "$RepoRoot\src\pantheon"
try {
    if (-not (Test-Path node_modules)) { npm ci }
    npx ng build --configuration production
}
finally { Pop-Location }

Write-Host "Deploying to $SitePath..."
if (Test-Path $SitePath) { Remove-Item "$SitePath\*" -Recurse -Force }
New-Item -ItemType Directory -Force -Path $SitePath | Out-Null
Copy-Item "$RepoRoot\src\pantheon\dist\pantheon\browser\*" $SitePath -Recurse -Force
Copy-Item "$RepoRoot\deploy\iis\pantheon.web.config" "$SitePath\web.config" -Force

Write-Host "Configuring IIS app pool + site..."
if (Test-Path "IIS:\AppPools\$AppPool") { Remove-WebAppPool $AppPool }
New-WebAppPool $AppPool | Out-Null
# Static site: no managed runtime needed.
Set-ItemProperty "IIS:\AppPools\$AppPool" managedRuntimeVersion ""

if (Get-Website -Name $SiteName -ErrorAction SilentlyContinue) { Remove-Website -Name $SiteName }
New-Website -Name $SiteName -PhysicalPath $SitePath -ApplicationPool $AppPool -Port $Port | Out-Null

Start-WebAppPool $AppPool
Start-Website $SiteName

Write-Host ""
Write-Host "Done. pantheon is served at http://localhost:$Port/"
Write-Host "It reverse-proxies /api and /hubs/live to the styx service on localhost:8080."
Write-Host "Confirm ARR proxy is enabled and the WebSocket feature is installed, or those calls will 404/502."
