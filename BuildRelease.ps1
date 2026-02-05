param(
    [Parameter(Mandatory=$false)]
    [string]$Version
)

if (-not $Version) {
    $csprojContent = Get-Content "SimpleGSXIntegrator.csproj"
    $versionLine = $csprojContent | Select-String -Pattern '<Version>(.*)</Version>'
    if ($versionLine) {
        $Version = $versionLine.Matches.Groups[1].Value
        Write-Host "Using version from project file: $Version" -ForegroundColor Cyan
    } else {
        Write-Host "ERROR: Could not find version in project file" -ForegroundColor Red
        exit 1
    }
}

Write-Host "`n=== Building SimpleGSXIntegrator v$Version ===" -ForegroundColor Green

Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
Remove-Item -Path ".\release" -Recurse -Force -ErrorAction SilentlyContinue

Write-Host "`nBuilding Release..." -ForegroundColor Yellow
dotnet publish SimpleGSXIntegrator.csproj -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

$exePath = ".\release\SimpleGSXIntegrator.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
    exit 1
}

$exeSize = [math]::Round((Get-Item $exePath).Length / 1MB, 2)
Write-Host ""
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "  Executable: $exePath ($exeSize MB)" -ForegroundColor Cyan
Write-Host ""

Write-Host "Creating update package..." -ForegroundColor Yellow
$zipPath = ".\release\SimpleGSXIntegrator-v$Version.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Copy-Item "lib\SimConnect.dll" "release\" -Force
Copy-Item "lib\Microsoft.FlightSimulator.SimConnect.dll" "release\" -Force
Copy-Item "logo.ico" "release\" -Force

$filesToPackage = @(
    "release\SimpleGSXIntegrator.exe",
    "release\SimConnect.dll",
    "release\Microsoft.FlightSimulator.SimConnect.dll",
    "release\logo.ico"
)

Compress-Archive -Path $filesToPackage -DestinationPath $zipPath
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "  Package: $zipPath ($zipSize MB)" -ForegroundColor Cyan

Write-Host ""
Write-Host "=== Release Files ===" -ForegroundColor Green
Write-Host "Upload to GitHub Release v${Version}:" -ForegroundColor Cyan
Write-Host "  - $zipPath (for auto-updates)" -ForegroundColor White
Write-Host "  - Your installer (for manual downloads)" -ForegroundColor White
Write-Host ""
Write-Host "Update version.json downloadUrl to point to the .zip file!" -ForegroundColor Yellow
Write-Host ""
