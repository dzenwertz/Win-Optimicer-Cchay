"""Limpieza de disco: caché, papelera, miniaturas, diario y paquetes."""

from __future__ import annotations

import os
import shutil
import subprocess
import time
from pathlib import Path

from .. import privileged
from ..paths import fmt_bytes

CACHE_DENY = {
    "dconf",
    "ibus",
    "keyrings",
    "keyring",
    "input",
    "sessions",
    "gdm",
    "gnome-shell",
}


def _home() -> Path:
    return Path.home()


def dir_size(path: Path, limit: int = 25000, deadline: float = 2.5) -> int:
    if not path.exists():
        return 0
    total = 0
    files = 0
    end = time.monotonic() + deadline
    if path.is_file():
        try:
            return path.stat().st_size
        except OSError:
            return 0
    try:
        for root, dirs, names in os.walk(path, followlinks=False):
            if time.monotonic() > end or files >= limit:
                break
            dirs[:] = [d for d in dirs if not d.startswith(".") or d in (".cache",)]
            for name in names:
                files += 1
                if files >= limit or time.monotonic() > end:
                    break
                try:
                    st = os.lstat(os.path.join(root, name))
                except OSError:
                    continue
                if os.path.stat.S_ISREG(st.st_mode):
                    total += st.st_size
    except OSError:
        return total
    return total


def _journal_bytes(user: bool = False) -> int:
    cmd = ["journalctl", "--disk-usage"]
    if user:
        cmd.insert(1, "--user")
    try:
        out = subprocess.run(
            cmd, capture_output=True, text=True, timeout=8, check=False
        ).stdout
    except (OSError, subprocess.TimeoutExpired):
        return 0
    # "Archived and active journals take up 240.5M in the file system."
    import re

    match = re.search(r"([0-9]+(?:[.,][0-9]+)?)\s*([KMGTPE]i?B?)", out, re.I)
    if not match:
        return 0
    num = float(match.group(1).replace(",", "."))
    unit = match.group(2).upper().replace("IB", "B")
    mul = {
        "B": 1,
        "KB": 1000,
        "KIB": 1024,
        "MB": 1000**2,
        "MIB": 1024**2,
        "GB": 1000**3,
        "GIB": 1024**3,
    }
    key = unit if unit in mul else unit + "B"
    if unit.endswith("B") is False:
        key = unit + "B"
    return int(num * mul.get(key, mul.get(unit, 1)))


def _pkg_cache_path() -> Path | None:
    from .packages import cache_path

    return cache_path()


def targets() -> list[dict]:
    home = _home()
    pkg = _pkg_cache_path()
    return [
        {
            "id": "user_cache",
            "title": "Caché de usuario",
            "subtitle": "Archivos temporales en ~/.cache",
            "path": home / ".cache",
            "destructive": False,
            "needs_root": False,
            "kind": "dir",
        },
        {
            "id": "thumbnails",
            "title": "Miniaturas",
            "subtitle": "Vistas previas de archivos",
            "path": home / ".cache" / "thumbnails",
            "destructive": False,
            "needs_root": False,
            "kind": "dir",
        },
        {
            "id": "trash",
            "title": "Papelera",
            "subtitle": "Elementos de ~/.local/share/Trash",
            "path": home / ".local" / "share" / "Trash",
            "destructive": True,
            "needs_root": False,
            "kind": "trash",
        },
        {
            "id": "journal_user",
            "title": "Diario de usuario",
            "subtitle": "Registros journalctl --user",
            "path": None,
            "destructive": False,
            "needs_root": False,
            "kind": "journal_user",
        },
        {
            "id": "journal_system",
            "title": "Diario del sistema",
            "subtitle": "Registros journalctl (requiere privilegios)",
            "path": None,
            "destructive": False,
            "needs_root": True,
            "kind": "journal_system",
        },
        {
            "id": "pkg_cache",
            "title": "Caché de paquetes",
            "subtitle": "Archivos descargados del gestor de paquetes",
            "path": pkg,
            "destructive": False,
            "needs_root": True,
            "kind": "pkg_cache",
        },
    ]


def scan_targets() -> list[dict]:
    items = []
    for t in targets():
        kind = t["kind"]
        size = 0
        if kind == "journal_user":
            size = _journal_bytes(user=True)
        elif kind == "journal_system":
            size = _journal_bytes(user=False)
        elif t.get("path"):
            size = dir_size(Path(t["path"]))
        row = dict(t)
        row["bytes"] = size
        row["size_h"] = fmt_bytes(size)
        row["path_h"] = str(t["path"]) if t.get("path") else "—"
        items.append(row)
    return items


def _clear_dir(path: Path, deny: set[str] | None = None) -> int:
    freed = 0
    if not path.exists():
        return 0
    deny = deny or set()
    try:
        for child in path.iterdir():
            if child.name in deny:
                continue
            try:
                if child.is_symlink() or child.is_file():
                    size = child.stat().st_size if child.is_file() else 0
                    child.unlink()
                    freed += size
                elif child.is_dir():
                    size = dir_size(child, limit=80000, deadline=8.0)
                    shutil.rmtree(child, ignore_errors=True)
                    freed += size
            except OSError:
                continue
    except OSError:
        return freed
    return freed


def clean_target(target_id: str) -> dict:
    mapping = {t["id"]: t for t in targets()}
    t = mapping.get(target_id)
    if t is None:
        return {"ok": False, "error": "Objetivo desconocido.", "freed": 0}

    if t["kind"] == "journal_user":
        try:
            proc = subprocess.run(
                ["journalctl", "--user", "--vacuum-time=3d"],
                capture_output=True,
                text=True,
                timeout=30,
                check=False,
            )
            return {
                "ok": proc.returncode == 0,
                "message": (proc.stdout or proc.stderr or "Diario de usuario compactado.").strip(),
                "freed": 0,
            }
        except (OSError, subprocess.TimeoutExpired) as exc:
            return {"ok": False, "error": str(exc), "freed": 0}

    if t["kind"] == "journal_system":
        return privileged.run("journal_vacuum")

    if t["kind"] == "pkg_cache":
        return privileged.run("pkg_clean_cache")

    path = Path(t["path"]) if t.get("path") else None
    if path is None:
        return {"ok": False, "error": "Ruta no disponible.", "freed": 0}

    if t["kind"] == "trash":
        files = path / "files"
        info = path / "info"
        freed = dir_size(files) + dir_size(info)
        _clear_dir(files)
        _clear_dir(info)
        return {"ok": True, "freed": freed, "freed_h": fmt_bytes(freed)}

    if t["id"] == "user_cache":
        freed = _clear_dir(path, CACHE_DENY)
        return {"ok": True, "freed": freed, "freed_h": fmt_bytes(freed)}

    freed = _clear_dir(path)
    return {"ok": True, "freed": freed, "freed_h": fmt_bytes(freed)}


def clean_safe_user() -> dict:
    total = 0
    logs = []
    for tid in ("thumbnails", "user_cache"):
        res = clean_target(tid)
        freed = int(res.get("freed") or 0)
        total += freed
        logs.append({"id": tid, **res})
    return {"ok": True, "freed": total, "freed_h": fmt_bytes(total), "steps": logs}
