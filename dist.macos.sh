#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

APP_NAME="Display Blackout"
EXE_NAME="DisplayBlackout"
PROJECT_FILE="$SCRIPT_DIR/src/DisplayBlackout/DisplayBlackout.csproj"
CONFIG="Release"
RID_X64="osx-x64"
RID_ARM="osx-arm64"
OUT_DIR="$SCRIPT_DIR/dist"
PUBLISH_DIR="$OUT_DIR/publish"
TMP_DIR="$OUT_DIR/tmp"
APP_BUNDLE="$OUT_DIR/${APP_NAME}.app"

command -v dotnet >/dev/null || { echo "dotnet not found; please install .NET SDK or add it to PATH." >&2; exit 1; }
command -v lipo >/dev/null || { echo "lipo not found; install Xcode command line tools." >&2; exit 1; }
command -v hdiutil >/dev/null || { echo "hdiutil not found; this script must run on macOS." >&2; exit 1; }

rm -rf "$OUT_DIR"
mkdir -p "$PUBLISH_DIR" "$TMP_DIR"

echo "Publishing $APP_NAME for $RID_X64..."
dotnet publish "$PROJECT_FILE" -c "$CONFIG" -r "$RID_X64" -p:PublishAot=true -p:SelfContained=true -o "$PUBLISH_DIR/$RID_X64"

echo "Publishing $APP_NAME for $RID_ARM..."
dotnet publish "$PROJECT_FILE" -c "$CONFIG" -r "$RID_ARM" -p:PublishAot=true -p:SelfContained=true -o "$PUBLISH_DIR/$RID_ARM"

X64_EXE="$PUBLISH_DIR/$RID_X64/$EXE_NAME"
ARM_EXE="$PUBLISH_DIR/$RID_ARM/$EXE_NAME"

if [ ! -f "$X64_EXE" ] || [ ! -f "$ARM_EXE" ]; then
  echo "Could not find published executables." >&2
  exit 1
fi

if ! file "$X64_EXE" | grep -qi "Mach-O" || ! file "$ARM_EXE" | grep -qi "Mach-O"; then
  echo "One of the published host files is not a Mach-O binary." >&2
  file "$X64_EXE" || true
  file "$ARM_EXE" || true
  exit 1
fi

echo "Creating universal binary..."
UNIV_EXE="$TMP_DIR/$EXE_NAME.universal"
lipo -create -output "$UNIV_EXE" "$X64_EXE" "$ARM_EXE"

echo "Creating app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS" "$APP_BUNDLE/Contents/Resources"
rsync -a --exclude='*.dSYM' "$PUBLISH_DIR/$RID_ARM/" "$APP_BUNDLE/Contents/Resources/"
mv "$UNIV_EXE" "$APP_BUNDLE/Contents/MacOS/$EXE_NAME"
chmod +x "$APP_BUNDLE/Contents/MacOS/$EXE_NAME"

ICNS_SRC="$SCRIPT_DIR/src/DisplayBlackout/Assets/icon.icns"
if [ -f "$ICNS_SRC" ]; then
  cp "$ICNS_SRC" "$APP_BUNDLE/Contents/Resources/icon.icns"
fi

cat > "$APP_BUNDLE/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>com.aprillz.displayblackout</string>
  <key>CFBundleVersion</key>
  <string>0.1.1</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.1</string>
  <key>CFBundleExecutable</key>
  <string>$EXE_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleIconFile</key>
  <string>icon</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
</dict>
</plist>
PLIST

echo "Done: $APP_BUNDLE"
echo "To create a DMG:"
echo "  hdiutil create -volname '$APP_NAME' -srcfolder '$APP_BUNDLE' -ov -format UDZO '$OUT_DIR/$EXE_NAME.dmg'"
