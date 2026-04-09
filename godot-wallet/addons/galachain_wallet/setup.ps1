# GalaChain Wallet Plugin — Setup Script (Windows PowerShell)
# Run this from your Godot project root (where project.godot lives).
#
# This script:
# 1. Checks that a .csproj file exists (C# must be enabled in Godot)
# 2. Adds the required NuGet packages
# 3. Runs dotnet restore
#
# Usage:
#   powershell -File addons/galachain_wallet/setup.ps1

$ErrorActionPreference = "Stop"

$csproj = Get-ChildItem -Path . -Filter "*.csproj" -Depth 0 | Select-Object -First 1

if (-not $csproj) {
    Write-Host "ERROR: No .csproj file found in the current directory." -ForegroundColor Red
    Write-Host ""
    Write-Host "C# must be enabled in your Godot project before using this plugin."
    Write-Host "Steps:"
    Write-Host "  1. Open your project in the Godot editor"
    Write-Host "  2. Go to Project > Project Settings"
    Write-Host "  3. Enable the C#/.NET option (or create any .cs file)"
    Write-Host "  4. Build once (Build > Build Project)"
    Write-Host "  5. Re-run this script"
    exit 1
}

Write-Host "Found project: $($csproj.Name)"
Write-Host "Adding GalaChain Wallet dependencies..."

dotnet add $csproj.FullName package Nethereum.Accounts --version 5.8.0
dotnet add $csproj.FullName package Nethereum.HDWallet --version 5.8.0
dotnet add $csproj.FullName package Nethereum.Signer --version 5.8.0

Write-Host ""
Write-Host "Restoring packages..."
dotnet restore $csproj.FullName

Write-Host ""
Write-Host "Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:"
Write-Host "  1. Open your project in the Godot editor"
Write-Host "  2. Build the C# project (Build > Build Project)"
Write-Host "  3. Go to Project > Project Settings > Plugins"
Write-Host "  4. Enable 'GalaChain Wallet'"
Write-Host "  5. The 'Wallet' autoload singleton is now available in GDScript"
