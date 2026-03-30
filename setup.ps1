# Setup & Install Script for Obsidian Monitor
# Run this from a PowerShell terminal (Administrator)

Write-Host "--- 💎 Obsidian Monitor Setup ---" -ForegroundColor Cyan

# 1. Check for .NET 9.0 SDK
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-Host "⚠️  .NET SDK not found. Please install it from: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Red
    exit 1
}

$sdk = dotnet --version
Write-Host "✅ Using .NET SDK: $sdk"

# 2. Build the project
Write-Host "🚀 Building application..." -ForegroundColor Yellow
dotnet build -c Release

$exePath = Join-Path $PSScriptRoot "bin\Release\net9.0-windows\Obsidian Monitor.exe"

if (-not (Test-Path $exePath)) {
    # Fallback for net9.0-windows-specific path
    $exePath = (Get-ChildItem -Path "bin\Release" -Recurse -Filter "Obsidian Monitor.exe") | Select-Object -ExpandProperty FullName -First 1
}

if (-not $exePath) {
    Write-Host "❌ Build failed. Could not find executable." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful!"

# 3. Create Desktop Shortcut (Optional)
$choice = Read-Host "Create a Desktop shortcut? (y/n)"
if ($choice -eq 'y') {
    $WshShell = New-Object -ComObject WScript.Shell
    $Shortcut = $WshShell.CreateShortcut("$HOME\Desktop\Obsidian Monitor.lnk")
    $Shortcut.TargetPath = $exePath
    $Shortcut.WorkingDirectory = Split-Path $exePath
    $Shortcut.Description = "Obsidian Performance Monitor HUD"
    $Shortcut.IconLocation = $exePath # Uses embedded icon
    $Shortcut.Save()
    Write-Host "✅ Desktop shortcut created!" -ForegroundColor Green
}

Write-Host "`nInstallation Complete! You can now run the app from bin\Release or your Desktop." -ForegroundColor Green
Write-Host "Note: Application requires Administrator privileges for hardware monitoring."
pause
