#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
if [ "$(id -u)" -eq 0 ]; then
  PREFIX="${PREFIX:-/usr/local}"
  LIBEXEC="$PREFIX/libexec"
  SHARE="$PREFIX/share"
  BIN="$PREFIX/bin"
  POLICY="/usr/share/polkit-1/actions"
else
  PREFIX="${PREFIX:-$HOME/.local}"
  LIBEXEC="$PREFIX/libexec"
  SHARE="$PREFIX/share"
  BIN="$PREFIX/bin"
  POLICY=""
fi

mkdir -p "$BIN" "$LIBEXEC" "$SHARE/reth-optimicer" "$SHARE/applications" \
  "$SHARE/icons/hicolor/256x256/apps"

cp -a "$ROOT/reth_optimicer" "$SHARE/reth-optimicer/"
cp -a "$ROOT/assets" "$SHARE/reth-optimicer/"
install -m 0755 "$ROOT/helper/reth-optimicer-helper" "$LIBEXEC/reth-optimicer-helper"
install -m 0755 /dev/stdin "$BIN/reth-optimicer" <<EOF
#!/usr/bin/env python3
import sys
sys.path.insert(0, "$SHARE/reth-optimicer")
from reth_optimicer.app import main
if __name__ == "__main__":
    raise SystemExit(main())
EOF

install -m 0644 "$ROOT/share/applications/reth-optimicer.desktop" \
  "$SHARE/applications/reth-optimicer.desktop"
if [ -f "$ROOT/share/icons/hicolor/256x256/apps/reth-optimicer.png" ]; then
  install -m 0644 "$ROOT/share/icons/hicolor/256x256/apps/reth-optimicer.png" \
    "$SHARE/icons/hicolor/256x256/apps/reth-optimicer.png"
elif [ -f "$ROOT/assets/bird.png" ]; then
  install -m 0644 "$ROOT/assets/bird.png" \
    "$SHARE/icons/hicolor/256x256/apps/reth-optimicer.png"
fi

if [ -n "$POLICY" ] && [ -d "$POLICY" ]; then
  install -m 0644 "$ROOT/polkit/com.reth.optimicer.policy" \
    "$POLICY/com.reth.optimicer.policy"
fi

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$SHARE/applications" >/dev/null 2>&1 || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
  gtk-update-icon-cache -q "$SHARE/icons/hicolor" >/dev/null 2>&1 || true
fi

echo "RETH Optimicer instalado en $PREFIX"
echo "Ejecuta: reth-optimicer"
if [ "$(id -u)" -ne 0 ]; then
  echo "Asegúrate de que $BIN esté en PATH."
  echo "Las acciones de administrador usan pkexec."
fi
