"""Ajustes de kernel: sysctl, gobernador de CPU y fstrim."""

from __future__ import annotations

from pathlib import Path

from .. import privileged, reverts

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


ZRAM_SWAPPINESS = 180


def zram_swap_active() -> bool:
    """True si hay un dispositivo zram usado como swap (swap comprimido en RAM)."""
    try:
        return any("/dev/zram" in ln for ln in Path("/proc/swaps").read_text().splitlines()[1:])
    except OSError:
        return False


def list_tweaks() -> list[dict]:
    items: list[dict] = []
    zram = zram_swap_active()
    for spec in SYSCTL_KEYS:
        current = read_sysctl(spec["key"])
        recommended, maximum, note = spec["recommended"], spec["max"], ""
        if spec["key"] == "vm.swappiness" and zram:
            # Con zram la swap es RAM comprimida: un valor alto es lo correcto.
            recommended, maximum = ZRAM_SWAPPINESS, 200
            note = "Con zram activo conviene un valor alto."
        items.append(
            {
                "id": spec["key"],
                "kind": "sysctl",
                "title": spec["title"],
                "subtitle": spec["subtitle"],
                "min": spec["min"],
                "max": maximum,
                "recommended": recommended,
                "note": note,
                "original": reverts.original("sysctl", spec["key"]),
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
            "original": reverts.original("governor", "cpu"),
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
    before = read_sysctl(key)
    result = privileged.run("sysctl_set", key=key, value=int(value))
    if result.get("ok") and before is not None and before != int(value):
        reverts.remember("sysctl", key, before)
    return result


def restore_sysctl(key: str) -> dict:
    """Vuelve al valor que había antes de RETH y quita la línea persistente."""
    before = reverts.original("sysctl", key)
    if before is None:
        return {"ok": False, "error": "No hay un valor original guardado."}
    result = privileged.run("sysctl_restore", key=key, value=int(before))
    if result.get("ok"):
        reverts.forget("sysctl", key)
    return result


def apply_governor(name: str) -> dict:
    before = current_governor()
    result = privileged.run("set_governor", governor=name)
    if result.get("ok") and before and before != name:
        reverts.remember("governor", "cpu", before)
    return result


def restore_governor() -> dict:
    before = reverts.original("governor", "cpu")
    if not before:
        return {"ok": False, "error": "No hay un gobernador original guardado."}
    result = privileged.run("set_governor", governor=before)
    if result.get("ok"):
        reverts.forget("governor", "cpu")
    return result


def run_fstrim() -> dict:
    return privileged.run("fstrim")
