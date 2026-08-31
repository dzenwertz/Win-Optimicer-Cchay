"""Ajustes de kernel: sysctl, gobernador de CPU y fstrim."""

from __future__ import annotations

from pathlib import Path

from .. import privileged

SYSCTL_KEYS = [
    {
        "key": "vm.swappiness",
        "title": "Swappiness",
        "subtitle": "Tendencia a usar la swap. Valores bajos priorizan la RAM.",
        "min": 0,
        "max": 100,
        "recommended": 10,
    },
    {
        "key": "vm.dirty_ratio",
        "title": "Dirty ratio",
        "subtitle": "Porcentaje máximo de memoria sucia antes de escribir a disco.",
        "min": 1,
        "max": 99,
        "recommended": 20,
    },
    {
        "key": "vm.dirty_background_ratio",
        "title": "Dirty background ratio",
        "subtitle": "Umbral para empezar a escribir páginas sucias en segundo plano.",
        "min": 1,
        "max": 80,
        "recommended": 5,
    },
    {
        "key": "vm.vfs_cache_pressure",
        "title": "VFS cache pressure",
        "subtitle": "Presión para reclamar caché de inodos y dentries.",
        "min": 1,
        "max": 500,
        "recommended": 50,
    },
]


def _sysctl_path(key: str) -> Path:
    return Path("/proc/sys") / Path(*key.split("."))


def read_sysctl(key: str) -> int | None:
    path = _sysctl_path(key)
    try:
        return int(path.read_text().strip())
    except (OSError, ValueError):
        return None


def current_governor() -> str | None:
    path = Path("/sys/devices/system/cpu/cpu0/cpufreq/scaling_governor")
    try:
        return path.read_text().strip()
    except OSError:
        return None


def available_governors() -> list[str]:
    path = Path("/sys/devices/system/cpu/cpu0/cpufreq/scaling_available_governors")
    try:
        return path.read_text().split()
    except OSError:
        return []


def list_tweaks() -> list[dict]:
    items: list[dict] = []
    for spec in SYSCTL_KEYS:
        current = read_sysctl(spec["key"])
        items.append(
            {
                "id": spec["key"],
                "kind": "sysctl",
                "title": spec["title"],
                "subtitle": spec["subtitle"],
                "min": spec["min"],
                "max": spec["max"],
                "recommended": spec["recommended"],
                "current": current,
                "available": True,
            }
        )
    govs = available_governors()
    items.append(
        {
            "id": "cpu_governor",
            "kind": "governor",
            "title": "Gobernador de CPU",
            "subtitle": "Política de frecuencia. «performance» maximiza rendimiento.",
            "current": current_governor(),
            "options": govs,
            "available": bool(govs or current_governor()),
            "recommended": "schedutil" if "schedutil" in govs else (
                "performance" if "performance" in govs else None
            ),
        }
    )
    items.append(
        {
            "id": "fstrim",
            "kind": "fstrim",
            "title": "TRIM de SSD (fstrim)",
            "subtitle": "Libera bloques no usados en discos de estado sólido.",
            "current": None,
            "available": Path("/usr/sbin/fstrim").exists()
            or Path("/sbin/fstrim").exists()
            or bool(__import__("shutil").which("fstrim")),
            "recommended": True,
        }
    )
    return items


def apply_sysctl(key: str, value: int) -> dict:
    return privileged.run("sysctl_set", key=key, value=int(value))


def apply_governor(name: str) -> dict:
    return privileged.run("set_governor", governor=name)


def run_fstrim() -> dict:
    return privileged.run("fstrim")
