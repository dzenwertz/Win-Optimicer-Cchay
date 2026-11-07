"""Memoria: medición clara, reclamo de caché y cierre de apps pesadas."""

from __future__ import annotations

import os
import signal
import time
from collections import defaultdict
from pathlib import Path

from .. import privileged
from ..paths import fmt_bytes
from . import system_info

PAGE = 4096
FRIENDLY = {
    "brave": "Brave",
    "chrome": "Chrome",
    "chromium": "Chromium",
    "firefox": "Firefox",
    "code": "VS Code",
    "antigravity": "Antigravity",
    "grok": "Grok",
    "electron": "Electron",
    "discord": "Discord",
    "slack": "Slack",
    "spotify": "Spotify",
    "telegram": "Telegram",
    "steam": "Steam",
    "java": "Java",
    "python3": "Python",
    "node": "Node.js",
}
PROTECTED_NAMES = {
    "systemd",
    "init",
    "kthreadd",
    "Xorg",
    "Hyprland",
    "sway",
    "plasmashell",
    "gnome-shell",
    "pipewire",
    "wireplumber",
    "dbus-daemon",
    "dbus-broker",
}


def _page_size() -> int:
    try:
        return int(os.sysconf("SC_PAGE_SIZE"))
    except (OSError, ValueError):
        return PAGE


def _pid_mem(entry: Path, page: int) -> tuple[int, int, int]:
    """Retorna (pss, rss, swap) en bytes leyendo smaps_rollup si existe."""
    rollup = entry / "smaps_rollup"
    pss = 0
    rss = 0
    swap = 0
    if rollup.is_file():
        try:
            for line in rollup.read_text().splitlines():
                if line.startswith("Pss:"):
                    pss = int(line.split()[1]) * 1024
                elif line.startswith("Rss:"):
                    rss = int(line.split()[1]) * 1024
                elif line.startswith("Swap:"):
                    swap = int(line.split()[1]) * 1024
            if pss or rss:
                return (pss or rss), rss, swap
        except (OSError, ValueError, IndexError):
            pass
    try:
        statm = (entry / "statm").read_text().split()
        rss = int(statm[1]) * page
        return rss, rss, 0
    except (OSError, ValueError, IndexError):
        return 0, 0, 0


def _resolve_label(comm: str, cmdline: str) -> tuple[str, str]:
    """Devuelve (friendly_label, process_group_key)."""
    comm_low = comm.lower()
    cmd_low = cmdline.lower()

    if comm_low in ("java", "javaw"):
        if "gradle" in cmd_low:
            return "Gradle Daemon (Java)", "gradle-daemon"
        if "minecraft" in cmd_low:
            return "Minecraft", "minecraft"
        return "Java", "java"

    if comm_low in ("node", "nodejs") or comm_low.startswith("npm"):
        if "firebase" in cmd_low:
            return "Firebase CLI (Node)", "firebase"
        if "mcp" in cmd_low:
            return "MCP Server (Node)", "mcp-server"
        if "vite" in cmd_low:
            return "Vite Dev Server", "vite"
        if "next" in cmd_low:
            return "Next.js Dev Server", "nextjs"
        return "Node.js", "node"

    if comm_low.startswith("python"):
        parts = cmdline.split()
        script = ""
        for p in parts[1:]:
            if p.endswith(".py") or "/" in p:
                script = Path(p).name
                break
        if script:
            return f"Python ({script})", f"python-{script}"
        return "Python", "python"

    if comm_low == "qs":
        return "QuickShell (Bar/Widgets)", "qs"

    if comm_low == "kioworker":
        return "KIO Worker (KDE/Dolphin)", "kioworker"

    if comm_low.startswith("xdg-desktop-por"):
        return "XDG Desktop Portal", "xdg-desktop-portal"

    if comm_low.startswith("plasma-browser-"):
        return "Plasma Browser Integration", "plasma-browser"

    if comm_low == "language_server":
        return "Language Server (IDE)", "language_server"

    label = FRIENDLY.get(comm_low, comm)
    return label, comm


def snapshot() -> dict:
    info = system_info.collect()
    total = info["ram_total"]
    available = info["ram_available"]
    cached = info.get("ram_cached", 0)
    buffers = info.get("ram_buffers", 0)
    reclaimable = cached + buffers
    apps = max(0, total - available - reclaimable)
    pressure = "alta" if available < total * 0.18 else ("media" if available < total * 0.35 else "baja")
    return {
        **info,
        "apps_bytes": apps,
        "reclaimable": reclaimable,
        "apps_h": fmt_bytes(apps),
        "reclaimable_h": fmt_bytes(reclaimable),
        "pressure": pressure,
    }


def advice(snap: dict | None = None, groups: list[dict] | None = None) -> str:
    snap = snap or snapshot()
    groups = groups if groups is not None else grouped_processes(8)
    lines = []
    avail = snap["ram_available"]
    total = snap["ram_total"] or 1
    if avail < 512 * 1024 * 1024:
        lines.append("RAM disponible muy baja. Cierra el navegador o apps pesadas ahora.")
    elif avail < total * 0.2:
        lines.append("Queda poca RAM útil. Liberar caché ayuda un poco; cerrar apps pesadas ayuda más.")
    else:
        lines.append("Hay buen margen de RAM. Linux usa la RAM no ocupada como caché: eso es normal y deseable.")
    if groups:
        top = groups[0]
        if top["rss"] > 800 * 1024 * 1024:
            lines.append(
                f"{top['label']} está usando {top['rss_h']}. Cerrarlo es lo que más RAM te va a devolver."
            )
    if snap.get("zram_used", 0) > 512 * 1024 * 1024:
        lines.append("Hay datos en zram (swap comprimido). El equipo ya está reciclando memoria.")
    lines.append("Lo importante es la RAM disponible, no el número “libre” de `free`.")
    return " ".join(lines)


def top_processes(limit: int = 20) -> list[dict]:
    items: list[dict] = []
    page = _page_size()
    my_uid = os.getuid()
    proc = Path("/proc")
    for entry in proc.iterdir():
        if not entry.name.isdigit():
            continue
        pid = int(entry.name)
        pss, rss, swap = _pid_mem(entry, page)
        if pss <= 0 and rss <= 0:
            continue
        try:
            comm = (entry / "comm").read_text().strip()
            uid = _pid_uid(entry)
            cmdline = (
                (entry / "cmdline")
                .read_bytes()
                .replace(b"\x00", b" ")
                .decode("utf-8", errors="replace")
                .strip()
            )
        except (OSError, ValueError, IndexError):
            continue
        label, group_key = _resolve_label(comm, cmdline)
        items.append(
            {
                "pid": pid,
                "name": comm or "?",
                "group_key": group_key,
                "label": label,
                "cmd": cmdline or comm,
                "pss": pss,
                "rss": rss,
                "swap": swap,
                "rss_h": fmt_bytes(pss or rss),
                "uid": uid,
                "mine": uid == my_uid,
            }
        )
    items.sort(key=lambda x: x["pss"] or x["rss"], reverse=True)
    return items[: max(1, limit)]


def _pid_uid(proc_dir: Path) -> int:
    try:
        for line in (proc_dir / "status").read_text().splitlines():
            if line.startswith("Uid:"):
                return int(line.split()[1])
    except (OSError, ValueError, IndexError):
        pass
    return -1


def grouped_processes(limit: int = 12) -> list[dict]:
    buckets: dict[str, dict] = {}
    for proc in top_processes(100):
        key = proc.get("group_key") or proc["name"]
        if key not in buckets:
            buckets[key] = {
                "name": proc["name"],
                "group_key": key,
                "label": proc.get("label") or FRIENDLY.get(proc["name"].lower(), proc["name"]),
                "rss": 0,
                "pss": 0,
                "swap": 0,
                "pids": [],
                "mine": proc["mine"],
                "count": 0,
            }
        buckets[key]["pss"] += proc["pss"]
        buckets[key]["rss"] += proc["rss"]
        buckets[key]["swap"] += proc["swap"]
        buckets[key]["count"] += 1
        buckets[key]["pids"].append(proc["pid"])
        buckets[key]["mine"] = buckets[key]["mine"] and proc["mine"]
    rows = list(buckets.values())
    rows.sort(key=lambda x: x["pss"] or x["rss"], reverse=True)
    for row in rows:
        row["rss_h"] = fmt_bytes(row["pss"] or row["rss"])
        row["closable"] = row["mine"] and row["name"] not in PROTECTED_NAMES
    return rows[:limit]


def terminate_group(name: str) -> dict:
    groups = {g["name"]: g for g in grouped_processes(40)}
    groups.update({g.get("group_key", g["name"]): g for g in grouped_processes(40)})
    group = groups.get(name)
    if group is None:
        return {"ok": False, "error": "No se encontró ese proceso."}
    if not group["closable"]:
        return {"ok": False, "error": "Ese proceso está protegido o no es tuyo."}
    closed = 0
    for pid in group["pids"]:
        if pid in (1, os.getpid(), os.getppid()):
            continue
        try:
            os.kill(pid, signal.SIGTERM)
            closed += 1
        except OSError:
            continue
    time.sleep(0.6)
    still = []
    for pid in group["pids"]:
        if Path(f"/proc/{pid}").exists():
            still.append(pid)
    for pid in still:
        try:
            os.kill(pid, signal.SIGKILL)
        except OSError:
            pass
    after = snapshot()
    return {
        "ok": True,
        "closed": closed,
        "message": f"Cerrados {closed} procesos de {group['label']}.",
        "available_h": after["ram_available_h"],
    }


def drop_caches() -> dict:
    before = snapshot()
    result = privileged.run("drop_caches")
    after = snapshot()
    cache_before = before.get("ram_cached", 0) + before.get("ram_buffers", 0)
    cache_after = after.get("ram_cached", 0) + after.get("ram_buffers", 0)
    freed_cache = max(0, cache_before - cache_after)
    freed_free = max(0, after.get("ram_free", 0) - before.get("ram_free", 0))
    freed_avail = max(0, after["ram_available"] - before["ram_available"])
    freed = max(freed_cache, freed_free, freed_avail)
    result.setdefault("ok", False)
    result["freed"] = freed
    result["freed_h"] = fmt_bytes(freed)
    result["before"] = before
    result["after"] = after
    return result


def compact_memory() -> dict:
    return privileged.run("compact_memory")


def reclaim_ram() -> dict:
    """Desalojo cgroups + caché de páginas + compactar + compactar zram. Pide privilegio."""
    before = snapshot()
    steps = []

    # 1. Desalojo proactivo de páginas inactivas vía cgroup v2 (análogo a EmptyWorkingSet)
    cgroup = privileged.run("cgroup_reclaim", target="512M")
    steps.append({"id": "cgroup_reclaim", **cgroup})

    # 2. Sincronización a disco y liberación de cachés del kernel
    drop = privileged.run("drop_caches")
    steps.append({"id": "drop_caches", **drop})

    # 3. Compactación de memoria
    compact = privileged.run("compact_memory")
    steps.append({"id": "compact", **compact})

    # 4. Compactación de swap comprimido (zram)
    zram = privileged.run("zram_compact")
    steps.append({"id": "zram", **zram})

    after = snapshot()

    # Cálculo preciso de memoria física real recuperada
    freed_free = max(0, after.get("ram_free", 0) - before.get("ram_free", 0))
    cache_before = before.get("ram_cached", 0) + before.get("ram_buffers", 0)
    cache_after = after.get("ram_cached", 0) + after.get("ram_buffers", 0)
    freed_cache = max(0, cache_before - cache_after)
    freed_avail = max(0, after["ram_available"] - before["ram_available"])
    freed_used = max(0, before["ram_used"] - after["ram_used"])

    freed = max(freed_free, freed_cache, freed_avail, freed_used)
    ok = bool(drop.get("ok") or cgroup.get("ok"))

    if not ok:
        message = drop.get("error") or cgroup.get("error") or "No se pudo liberar memoria. Autoriza pkexec cuando lo pida."
    elif freed < 4 * 1024 * 1024:
        message = (
            f"Memoria optimizada (RAM disponible: {after['ram_available_h']}). "
            "Para liberar más memoria de aplicaciones activas, ciérralas desde la lista."
        )
    else:
        parts = [f"Liberados {fmt_bytes(freed)} de memoria"]
        if freed_cache > 4 * 1024 * 1024:
            parts.append(f"caché recuperada: {fmt_bytes(freed_cache)}")
        parts.append(f"RAM disponible: {after['ram_available_h']}")
        message = " · ".join(parts) + "."

    return {
        "ok": ok,
        "freed": freed,
        "freed_h": fmt_bytes(freed),
        "message": message,
        "before": before,
        "after": after,
        "steps": steps,
    }
