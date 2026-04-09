#!/bin/bash
# GalaChain Wallet Plugin — Setup Script
# Run this from your Godot project root (where project.godot lives).
#
# This script:
# 1. Checks that a .csproj file exists (C# must be enabled in Godot)
# 2. Adds the required NuGet packages
# 3. Runs dotnet restore
#
# Usage:
#   bash addons/galachain_wallet/setup.sh
#   -- or on Windows --
#   powershell -File addons/galachain_wallet/setup.ps1

set -e

# Find the .csproj file
CSPROJ=$(find . -maxdepth 1 -name "*.csproj" | head -1)

if [ -z "$CSPROJ" ]; then
    echo "ERROR: No .csproj file found in the current directory."
    echo ""
    echo "C# must be enabled in your Godot project before using this plugin."
    echo "Steps:"
    echo "  1. Open your project in the Godot editor"
    echo "  2. Go to Project > Project Settings"
    echo "  3. Enable the C#/.NET option (or create any .cs file)"
    echo "  4. Build once (Build > Build Project)"
    echo "  5. Re-run this script"
    exit 1
fi

echo "Found project: $CSPROJ"
echo "Adding GalaChain Wallet dependencies..."

dotnet add "$CSPROJ" package Nethereum.Accounts --version 5.8.0
dotnet add "$CSPROJ" package Nethereum.HDWallet --version 5.8.0
dotnet add "$CSPROJ" package Nethereum.Signer --version 5.8.0

echo ""
echo "Restoring packages..."
dotnet restore "$CSPROJ"

echo ""
echo "Setup complete!"
echo ""
echo "Next steps:"
echo "  1. Open your project in the Godot editor"
echo "  2. Build the C# project (Build > Build Project)"
echo "  3. Go to Project > Project Settings > Plugins"
echo "  4. Enable 'GalaChain Wallet'"
echo "  5. The 'Wallet' autoload singleton is now available in GDScript"
