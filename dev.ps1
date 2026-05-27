# UniConnect local dev stack (Windows PowerShell)
$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot

Write-Host "Stopping any running API process..."
Get-Process -Name "UniConnect.Api" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-CimInstance Win32_Process -Filter "name = 'dotnet.exe'" -ErrorAction SilentlyContinue |
    Where-Object { $_.CommandLine -like '*UniConnect.Api*' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
$port5000 = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty OwningProcess -Unique
foreach ($procId in $port5000) {
    Stop-Process -Id $procId -Force -ErrorAction SilentlyContinue
}

Write-Host "Starting PostgreSQL (Docker)..."
Set-Location $Root
docker info 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error @"
Docker is not running. Start Docker Desktop, wait until it is ready, then run .\dev.ps1 again.
Without Postgres the API returns 503 and login shows connection/database errors.
"@
}
docker compose up -d
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Applying EF migrations..."
Set-Location "$Root\src\UniConnect.Infrastructure"
dotnet ef database update --startup-project ../UniConnect.Api
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Starting API on http://localhost:5000 ..."
$apiArgs = @("run", "--project", "$Root\src\UniConnect.Api\UniConnect.Api.csproj")
Start-Process -FilePath "dotnet" -ArgumentList $apiArgs -WorkingDirectory "$Root\src\UniConnect.Api" -WindowStyle Normal
Write-Host "If login shows 502 Bad Gateway, the API did not start. Check the API window for errors."
Write-Host "Windows Smart App Control may block bin\Debug DLLs — run from Visual Studio or allow the project folder in Windows Security."

$webDir = "$Root\web\uniconnect-web"
if (-not (Test-Path "$webDir\node_modules")) {
    Write-Host "Installing web dependencies..."
    Set-Location $webDir
    npm install
}

Write-Host "Starting Vite on http://localhost:5173 ..."
Set-Location $webDir
npm run dev
