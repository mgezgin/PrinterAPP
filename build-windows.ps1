# Build script for creating Windows .exe
# This script publishes the application as a self-contained Windows executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true
)

Write-Host "Building PrinterAPP for Windows..." -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Runtime: $Runtime" -ForegroundColor Cyan
Write-Host "Self-Contained: $SelfContained" -ForegroundColor Cyan
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean PrinterAPP/PrinterAPP.csproj -c $Configuration

# Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Yellow
dotnet restore PrinterAPP/PrinterAPP.csproj

# Publish the application
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish PrinterAPP/PrinterAPP.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained $SelfContained `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o "./publish/$Runtime"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build completed successfully!" -ForegroundColor Green
    Write-Host "Output location: ./publish/$Runtime" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "You can find the executable at:" -ForegroundColor Green
    Write-Host "  ./publish/$Runtime/PrinterAPP.exe" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "Build failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
