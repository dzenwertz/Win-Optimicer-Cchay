#!/usr/bin/env bash
set -euo pipefail

if [ "$(id -u)" -eq 0 ]; then
  PREFIX="${PREFIX:-/usr/local}"
  rm -f /usr/share/polkit-1/actions/com.reth.optimicer.policy
else
  PREFIX="${PREFIX:-$HOME/.local}"
fi

rm -f "$PREFIX/bin/reth-optimicer"
rm -f "$PREFIX/libexec/reth-optimicer-helper"
rm -rf "$PREFIX/share/reth-optimicer"
rm -f "$PREFIX/share/applications/reth-optimicer.desktop"
rm -f "$PREFIX/share/icons/hicolor/256x256/apps/reth-optimicer.png"
echo "RETH Optimicer desinstalado de $PREFIX"
