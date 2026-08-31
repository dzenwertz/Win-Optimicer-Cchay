"""Detección de gestor de paquetes, caché y huérfanos."""

from __future__ import annotations

import shutil
import subprocess
from pathlib import Path

from .. import privileged
from ..paths import fmt_bytes

MANAGERS = (
    {
        "id": "pacman",
        "bin": "pacman",
        "title": "pacman",
        "cache": Path("/var/cache/pacman/pkg"),
    },
    {
        "id": "apt",
        "bin": "apt-get",
        "title": "APT",
        "cache": Path("/var/cache/apt/archives"),
    },
    {
        "id": "dnf",
        "bin": "dnf",
        "title": "DNF",
        "cache": Path("/var/cache/dnf"),
    },
)


def detect() -> dict | None:
    for mgr in MANAGERS:
        if shutil.which(mgr["bin"]):
            return dict(mgr)
    return None


def cache_path() -> Path | None:
    mgr = detect()
    return None if mgr is None else mgr["cache"]


def _run(cmd: list[str], timeout: int = 25) -> subprocess.CompletedProcess | None:
    try:
        return subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return None


def cache_size() -> int:
    path = cache_path()
    if path is None or not path.exists():
        return 0
    from .disk import dir_size

    return dir_size(path, limit=20000, deadline=2.0)


def list_orphans() -> list[str]:
    mgr = detect()
    if mgr is None:
        return []
    if mgr["id"] == "pacman":
        proc = _run(["pacman", "-Qtdq"])
        if proc and proc.returncode == 0:
            return [ln.strip() for ln in proc.stdout.splitlines() if ln.strip()]
        return []
    if mgr["id"] == "apt":
        proc = _run(["deborphan"]) if shutil.which("deborphan") else None
        if proc and proc.returncode == 0:
            return [ln.strip() for ln in proc.stdout.splitlines() if ln.strip()]
        proc = _run(["apt-get", "-s", "autoremove"])
        if not proc:
            return []
        names = []
        for line in proc.stdout.splitlines():
            if line.startswith("Remv ") or "Remover:" in line or "Removing " in line:
                token = line.replace("Remv ", "").split()[0] if line.startswith("Remv ") else ""
                if token:
                    names.append(token.strip(":"))
        return names
    if mgr["id"] == "dnf":
        proc = _run(["dnf", "--quiet", "repoquery", "--unneeded"])
        if proc and proc.returncode == 0:
            return [ln.strip() for ln in proc.stdout.splitlines() if ln.strip()][:80]
    return []


def status() -> dict:
    mgr = detect()
    orphans = list_orphans() if mgr else []
    size = cache_size() if mgr else 0
    return {
        "manager": None if mgr is None else mgr["id"],
        "title": None if mgr is None else mgr["title"],
        "cache": None if mgr is None else str(mgr["cache"]),
        "cache_bytes": size,
        "cache_h": fmt_bytes(size),
        "orphans": orphans,
        "orphan_count": len(orphans),
        "available": mgr is not None,
    }


def clean_cache() -> dict:
    if detect() is None:
        return {"ok": False, "error": "No se detectó un gestor de paquetes compatible."}
    return privileged.run("pkg_clean_cache")


def remove_orphans() -> dict:
    if detect() is None:
        return {"ok": False, "error": "No se detectó un gestor de paquetes compatible."}
    return privileged.run("pkg_remove_orphans")
