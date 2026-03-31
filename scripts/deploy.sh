#!/bin/bash
# ShogiDroid - エミュレータにデプロイして起動
set -e

PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Debug}"
PACKAGE="com.ngs43.shogidroid"
ACTIVITY="crc64721063ab64a94a2e.MainActivity"
APK="$PROJECT_DIR/ShogiDroid/bin/${CONFIG}/net9.0-android/android-arm64/com.ngs43.shogidroid-Signed.apk"

if [ ! -f "$APK" ]; then
    echo "APK not found. Run build.sh first."
    exit 1
fi

echo "=== Installing APK ==="
adb install -r "$APK"

echo ""
echo "=== Granting storage permission ==="
adb shell appops set "$PACKAGE" MANAGE_EXTERNAL_STORAGE allow 2>/dev/null || true

echo ""
echo "=== Launching app ==="
adb shell am start -n "$PACKAGE/$ACTIVITY"

echo ""
echo "=== Tailing logs (Ctrl+C to stop) ==="
adb logcat -s ShogiDroid AndroidRuntime
