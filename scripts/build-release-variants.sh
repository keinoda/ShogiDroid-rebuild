#!/bin/bash
# release/distribute 用: 通常版と classic 版の両方をビルドして artifacts に退避
set -euo pipefail

DOTNET="${DOTNET:-dotnet}"
PROJECT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
CSPROJ="$PROJECT_DIR/ShogiDroid/ShogiDroid.csproj"
RID="${1:-android-arm64}"
OUT_DIR="$PROJECT_DIR/artifacts/release/$RID"

STANDARD_APK="$PROJECT_DIR/ShogiDroid/bin/Release/net9.0-android/$RID/com.ngs436.ShogiDroidR-Signed.apk"
CLASSIC_APK="$PROJECT_DIR/ShogiDroid/bin/Release/net9.0-android/$RID/com.ngs436.ShogiDroidRClassic-Signed.apk"

mkdir -p "$OUT_DIR"

echo "=== Build standard release ==="
"$DOTNET" build -c Release "$CSPROJ" -r "$RID"

if [ ! -f "$STANDARD_APK" ]; then
	echo "ERROR: standard APK not found at $STANDARD_APK"
	exit 1
fi

cp "$STANDARD_APK" "$OUT_DIR/ShogiDroidR-release.apk"

echo ""
echo "=== Build classic release ==="
"$DOTNET" build -c Release "$CSPROJ" -r "$RID" -p:ClassicUi=true

if [ ! -f "$CLASSIC_APK" ]; then
	echo "ERROR: classic APK not found at $CLASSIC_APK"
	exit 1
fi

cp "$CLASSIC_APK" "$OUT_DIR/ShogiDroidR-classic-release.apk"

echo ""
echo "=== Release artifacts ==="
ls -lh "$OUT_DIR"
