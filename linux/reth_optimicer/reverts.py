"""Registro de valores originales para poder deshacer cada cambio de RETH.

Se guarda en ~/.config/reth-optimicer/reverts.json. Solo se registra el valor
que había ANTES del primer cambio de RETH; aplicar varias veces no lo pisa.
"""

from __future__ import annotations

import json
from pathlib import Path

from . import paths


def _file() -> Path:
    return paths.CONFIG_DIR / "reverts.json"


def load() -> dict:
    try:
        data = json.loads(_file().read_text(encoding="utf-8"))
        if isinstance(data, dict):
            return data
    except (OSError, json.JSONDecodeError):
        pass
    return {}


def _save(data: dict) -> None:
    paths.ensure_dirs()
    _file().write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def remember(section: str, key: str, original) -> None:
    """Guarda el original de section/key si aún no estaba guardado."""
    data = load()
    bucket = data.setdefault(section, {})
    if key not in bucket:
        bucket[key] = original
        _save(data)


def original(section: str, key: str):
    return load().get(section, {}).get(key)


def has(section: str, key: str) -> bool:
    return key in load().get(section, {})


def forget(section: str, key: str) -> None:
    data = load()
    if key in data.get(section, {}):
        del data[section][key]
        _save(data)
