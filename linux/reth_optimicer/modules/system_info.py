"""Información del sistema: distro, CPU, RAM, discos, temperaturas y salud."""

from __future__ import annotations

import os
import platform
import re
import shutil
import time
from pathlib import Path

from ..paths import fmt_bytes, fmt_duration

_PREV_CPU: tuple[int, int] | None = None


def _read(path: str | Path, default: str = "") -> str:
    try:
        return Path(path).read_text(encoding="utf-8", errors="replace")
    except OSError:
        return default


def _os_release() -> dict[str, str]:
    data: dict[str, str] = {}
    text = _read("/etc/os-release")
    for line in text.splitlines():
        if "=" not in line:
            continue
        key, val = line.split("=", 1)
        data[key] = val.strip().strip('"')
    return data


def _cpu_name() -> str:
    for line in _read("/proc/cpuinfo").splitlines():
        if line.lower().startswith("model name"):
            return line.split(":", 1)[-1].strip()
    return platform.processor() or platform.machine() or "CPU"


def _meminfo() -> dict[str, int]:
    info: dict[str, int] = {}
    for line in _read("/proc/meminfo").splitlines():
        parts = line.split()
        if len(parts) < 2:
            continue
        key = parts[0].rstrip(":")
        try:
            kb = int(parts[1])
        except ValueError:
            continue
        info[key] = kb * 1024
    return info


def _cpu_times() -> tuple[int, int] | None:
    line = _read("/proc/stat").splitlines()
    if not line:
        return None
    parts = line[0].split()
    if parts[0] != "cpu" or len(parts) < 5:
        return None
    nums = [int(x) for x in parts[1:]]
    idle = nums[3] + (nums[4] if len(nums) > 4 else 0)
    total = sum(nums)
    return total, idle


def cpu_percent() -> float:
    global _PREV_CPU
    cur = _cpu_times()
    if cur is None:
        return 0.0
    if _PREV_CPU is None:
        _PREV_CPU = cur
        return 0.0
    dt = cur[0] - _PREV_CPU[0]
    di = cur[1] - _PREV_CPU[1]
    _PREV_CPU = cur
    if dt <= 0:
        return 0.0
    return max(0.0, min(100.0, (1.0 - di / dt) * 100.0))


def _loadavg() -> tuple[float, float, float]:
    try:
        a, b, c = os.getloadavg()
        return float(a), float(b), float(c)
    except OSError:
        parts = _read("/proc/loadavg").split()
        if len(parts) >= 3:
            return float(parts[0]), float(parts[1]), float(parts[2])
    return 0.0, 0.0, 0.0


def _uptime_seconds() -> float:
    text = _read("/proc/uptime").split()
    try:
        return float(text[0])
    except (IndexError, ValueError):
        return 0.0


def _disks() -> list[dict]:
    skip_fs = {
        "tmpfs",
        "devtmpfs",
        "overlay",
        "squashfs",
        "proc",
        "sysfs",
        "cgroup",
        "cgroup2",
        "devpts",
        "securityfs",
        "pstore",
        "efivarfs",
        "autofs",
        "fuse.portal",
        "fuse.gvfsd-fuse",
        "nsfs",
        "bpf",
        "tracefs",
        "debugfs",
        "hugetlbfs",
        "mqueue",
        "ramfs",
        "configfs",
        "fusectl",
        "rpc_pipefs",
        "binfmt_misc",
    }
    skip_mount = {"/boot/efi", "/efi"}
    seen: set[str] = set()
    disks: list[dict] = []
    try:
        mounts = Path("/proc/mounts").read_text(encoding="utf-8", errors="replace")
    except OSError:
        usage = shutil.disk_usage("/")
        return [
            {
                "mount": "/",
                "fstype": "",
                "device": "",
                "total": usage.total,
                "used": usage.used,
                "free": usage.free,
                "percent": (usage.used / usage.total * 100) if usage.total else 0,
            }
        ]
    for line in mounts.splitlines():
        parts = line.split()
        if len(parts) < 3:
            continue
        device, mount, fstype = parts[0], parts[1], parts[2]
        if fstype in skip_fs or mount in skip_mount:
            continue
        if not device.startswith("/dev/") and mount != "/":
            continue
        if mount in seen:
            continue
        try:
            usage = shutil.disk_usage(mount)
        except OSError:
            continue
        if usage.total <= 0:
            continue
        seen.add(mount)
        disks.append(
            {
                "mount": mount,
                "fstype": fstype,
                "device": device,
                "total": usage.total,
                "used": usage.used,
                "free": usage.free,
                "percent": usage.used / usage.total * 100,
            }
        )
    disks.sort(key=lambda d: 0 if d["mount"] == "/" else 1)
    return disks


def _temps() -> list[dict]:
    temps: list[dict] = []
    thermal = Path("/sys/class/thermal")
    if thermal.is_dir():
        for zone in sorted(thermal.glob("thermal_zone*")):
            try:
                milli = int((zone / "temp").read_text().strip())
                name = _read(zone / "type", zone.name).strip() or zone.name
                temps.append({"name": name, "celsius": milli / 1000.0})
            except (OSError, ValueError):
                continue
    hwmon = Path("/sys/class/hwmon")
    if hwmon.is_dir():
        for chip in sorted(hwmon.glob("hwmon*")):
            label = _read(chip / "name", chip.name).strip()
            for inp in sorted(chip.glob("temp*_input")):
                try:
                    milli = int(inp.read_text().strip())
                except (OSError, ValueError):
                    continue
                suffix = inp.name.replace("temp", "").replace("_input", "")
                pretty = _read(chip / f"temp{suffix}_label", "").strip()
                name = pretty or f"{label} {suffix}".strip()
                celsius = milli / 1000.0
                if any(abs(t["celsius"] - celsius) < 0.6 and t["name"] == name for t in temps):
                    continue
                temps.append({"name": name, "celsius": celsius})
    return temps


def _health(ram_percent: float, disks: list[dict], load: tuple[float, float, float], temps: list[dict], ncpu: int) -> int:
    score = 100.0
    score -= max(0.0, ram_percent - 70.0) * 0.7
    if disks:
        root = next((d for d in disks if d["mount"] == "/"), disks[0])
        score -= max(0.0, root["percent"] - 80.0) * 1.2
    load1 = load[0]
    if ncpu > 0:
        load_ratio = load1 / ncpu
        score -= max(0.0, (load_ratio - 0.85) * 25.0)
    if temps:
        hottest = max(t["celsius"] for t in temps)
        score -= max(0.0, hottest - 80.0) * 1.5
    return int(max(5, min(100, round(score))))


def collect() -> dict:
    release = _os_release()
    mem = _meminfo()
    total = mem.get("MemTotal", 0)
    available = mem.get("MemAvailable", mem.get("MemFree", 0))
    used = max(0, total - available)
    ram_percent = (used / total * 100) if total else 0.0
    disks = _disks()
    load = _loadavg()
    temps = _temps()
    ncpu = os.cpu_count() or 1
    uptime = _uptime_seconds()
    cpu_p = cpu_percent()
    distro = release.get("PRETTY_NAME") or release.get("NAME") or platform.platform()
    return {
        "distro": distro,
        "distro_id": release.get("ID", ""),
        "kernel": platform.release(),
        "hostname": platform.node(),
        "arch": platform.machine(),
        "cpu": _cpu_name(),
        "cpu_count": ncpu,
        "cpu_percent": cpu_p,
        "ram_total": total,
        "ram_available": available,
        "ram_used": used,
        "ram_percent": ram_percent,
        "swap_total": mem.get("SwapTotal", 0),
        "swap_free": mem.get("SwapFree", 0),
        "disks": disks,
        "uptime": fmt_duration(uptime),
        "uptime_seconds": uptime,
        "temps": temps,
        "load": load,
        "health": _health(ram_percent, disks, load, temps, ncpu),
        "ram_total_h": fmt_bytes(total),
        "ram_used_h": fmt_bytes(used),
        "ram_available_h": fmt_bytes(available),
    }


_SWAPPINESS_RE = re.compile(r"^[0-9]+$")
