"""Rutas de datos, configuración y recursos de RETH Optimicer."""

from __future__ import annotations

import json
import os
from pathlib import Path

APP_ID = "com.reth.optimicer"
APP_NAME = "RETH Optimicer"
CONFIG_DIR = Path.home() / ".config" / "reth-optimicer"
DATA_DIR = Path.home() / ".local" / "share" / "reth-optimicer"
SETTINGS_FILE = CONFIG_DIR / "settings.json"
LOCK_FILE = DATA_DIR / "reth-optimicer.lock"
SYSCTL_DROPIN = "99-reth-optimicer.conf"

DEFAULT_SETTINGS = {
    "confirm_destructive": True,
    "optimize_user_cache": True,
    "optimize_thumbnails": True,
    "optimize_journal": True,
    "optimize_drop_caches": True,
    "optimize_fstrim": True,
    "optimize_empty_trash": False,
    "dns_provider": "cloudflare",
    "single_instance": True,
}

_PKG = Path(__file__).resolve().parent
_ROOT = _PKG.parent


def ensure_dirs() -> None:
    CONFIG_DIR.mkdir(parents=True, exist_ok=True)
    DATA_DIR.mkdir(parents=True, exist_ok=True)


def asset(*parts: str) -> Path | None:
    name = Path(*parts)
    candidates = [
        _ROOT / "assets" / name,
        _PKG / "assets" / name,
        DATA_DIR / name,
        Path("/usr/local/share/reth-optimicer") / name,
        Path.home() / ".local" / "share" / "reth-optimicer" / name,
        Path("/usr/share/reth-optimicer") / name,
    ]
    if name.suffix == ".png" and name.stem == "bird":
        candidates.append(
            Path("/usr/local/share/icons/hicolor/256x256/apps/reth-optimicer.png")
        )
        candidates.append(
            Path.home() / ".local/share/icons/hicolor/256x256/apps/reth-optimicer.png"
        )
        candidates.append(_ROOT / "share/icons/hicolor/256x256/apps/reth-optimicer.png")
    for path in candidates:
        if path.is_file():
            return path
    return None


def helper_candidates() -> list[Path]:
    return [
        _ROOT / "helper" / "reth-optimicer-helper",
        Path("/usr/local/libexec/reth-optimicer-helper"),
        Path("/usr/libexec/reth-optimicer-helper"),
        Path.home() / ".local" / "libexec" / "reth-optimicer-helper",
        Path("/usr/local/lib/reth-optimicer/helper/reth-optimicer-helper"),
        Path.home() / ".local/lib/reth-optimicer/helper/reth-optimicer-helper",
    ]


def find_helper() -> Path | None:
    for path in helper_candidates():
        if path.is_file() and os.access(path, os.X_OK):
            return path
    for path in helper_candidates():
        if path.is_file():
            return path
    return None


def load_settings() -> dict:
    ensure_dirs()
    data = dict(DEFAULT_SETTINGS)
    try:
        if SETTINGS_FILE.is_file():
            loaded = json.loads(SETTINGS_FILE.read_text(encoding="utf-8"))
            if isinstance(loaded, dict):
                data.update(loaded)
    except (OSError, json.JSONDecodeError):
        pass
    return data


def save_settings(data: dict) -> None:
    ensure_dirs()
    merged = dict(DEFAULT_SETTINGS)
    merged.update(data)
    SETTINGS_FILE.write_text(
        json.dumps(merged, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def fmt_bytes(n: int | float) -> str:
    value = float(max(0, n))
    for unit in ("B", "KiB", "MiB", "GiB", "TiB"):
        if value < 1024 or unit == "TiB":
            if unit == "B":
                return f"{int(value)} B"
            return f"{value:.1f} {unit}"
        value /= 1024
    return f"{value:.1f} TiB"


def fmt_duration(seconds: float) -> str:
    total = int(max(0, seconds))
    days, rem = divmod(total, 86400)
    hours, rem = divmod(rem, 3600)
    minutes, secs = divmod(rem, 60)
    parts = []
    if days:
        parts.append(f"{days} d")
    if hours or days:
        parts.append(f"{hours} h")
    if minutes or hours or days:
        parts.append(f"{minutes} min")
    if not parts:
        parts.append(f"{secs} s")
    return " ".join(parts)
