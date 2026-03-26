#!/bin/bash
# ShogiDroid Rebuild - ビルドスクリプト
set -e

DOTNET=/usr/local/share/dotnet/dotnet
export DOTNET_ROOT=/usr/local/share/dotnet
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
CSPROJ="$PROJECT_DIR/ShogiDroid/ShogiDroid.csproj"
CONFIG="${1:-Debug}"

echo "=== ShogiDroid Build (${CONFIG}) ==="
echo "SDK: $($DOTNET --version)"
echo "Project: $CSPROJ"
echo ""

$DOTNET build -c "$CONFIG" "$CSPROJ"

APK="$PROJECT_DIR/ShogiDroid/bin/${CONFIG}/net9.0-android/android-arm64/com.ngs436.ShogiDroidR-Signed.apk"
if [ -f "$APK" ]; then
    echo ""
    echo "=== APK generated ==="
    ls -lh "$APK"
else
    echo ""
    echo "ERROR: APK not found at $APK"
    exit 1
fi
