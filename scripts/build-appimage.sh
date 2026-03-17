#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PUBLISH_DIR="$ROOT_DIR/artifacts/linux-x64"
APPDIR="$ROOT_DIR/artifacts/AppDir"

pushd "$ROOT_DIR" >/dev/null

dotnet publish "ZV Player.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$PUBLISH_DIR"

echo "Linux single-file binary published to $PUBLISH_DIR"

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
cp -r "$PUBLISH_DIR"/* "$APPDIR/usr/bin/"

cat > "$APPDIR/AppRun" <<'APP_RUN'
#!/usr/bin/env bash
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$HERE/usr/bin/ZV-Tube" "$@"
APP_RUN
chmod +x "$APPDIR/AppRun"
chmod +x "$APPDIR/usr/bin/ZV-Tube"

cat > "$APPDIR/ZV-Tube.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=ZV Tube
Exec=ZV-Tube
Icon=zv-tube
Categories=AudioVideo;
DESKTOP

cp "$ROOT_DIR/zv-tube.png" "$APPDIR/zv-tube.png"

if command -v appimagetool >/dev/null 2>&1; then
  ARCH=x86_64 appimagetool "$APPDIR" "$ROOT_DIR/artifacts/ZV-Tube-x86_64.AppImage"
  echo "AppImage created at artifacts/ZV-Tube-x86_64.AppImage"
else
  echo "appimagetool is not installed. AppDir prepared at $APPDIR"
fi

popd >/dev/null
