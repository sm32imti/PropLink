param(
    [switch]$Watch
)

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Starting PropLink Web Application..." -ForegroundColor Green
Write-Host "Access the site at: http://localhost:5019" -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop the application." -ForegroundColor Gray
Write-Host "========================================================" -ForegroundColor Cyan

if ($Watch) {
    dotnet watch --project PropLink.csproj
} else {
    dotnet run --project PropLink.csproj
}
