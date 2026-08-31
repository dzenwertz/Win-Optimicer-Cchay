"""Memoria: procesos, cachés de página y compactación."""

from __future__ import annotations

from pathlib import Path

from .. import privileged
from ..paths import fmt_bytes
from . import system_info

PAGE = 4096


def snapshot() -> dict:
    info = system_info.collect()
    return {
        "ram_total": info["ram_total"],
        "ram_used": info["ram_used"],
        "ram_available": info["ram_available"],
        "ram_percent": info["ram_percent"],
        "swap_total": info["swap_total"],
        "swap_free": info["swap_free"],
        "ram_total_h": info["ram_total_h"],
        "ram_used_h": info["ram_used_h"],
        "ram_available_h": info["ram_available_h"],
    }


def top_processes(limit: int = 20) -> list[dict]:
    items: list[dict] = []
    proc = Path("/proc")
    try:
        page = os_page_size()
    except OSError:
        page = PAGE
    for entry in proc.iterdir():
        if not entry.name.isdigit():
            continue
        try:
            statm = (entry / "statm").read_text().split()
            rss = int(statm[1]) * page
            comm = (entry / "comm").read_text().strip()
            cmdline = (entry / "cmdline").read_bytes().replace(b"\x00", b" ").decode(
                "utf-8", errors="replace"
            ).strip()
        except (OSError, ValueError, IndexError):
            continue
        items.append(
            {
                "pid": int(entry.name),
                "name": comm or "?",
                "cmd": cmdline or comm,
                "rss": rss,
                "rss_h": fmt_bytes(rss),
            }
        )
    items.sort(key=lambda x: x["rss"], reverse=True)
    return items[: max(1, limit)]


def os_page_size() -> int:
    try:
        return os_sysconf()
    except (OSError, ValueError):
        return PAGE


def os_sysconf() -> int:
    import os

    return int(os.sysconf("SC_PAGE_SIZE"))


def drop_caches() -> dict:
    before = snapshot()
    result = privileged.run("drop_caches")
    after = snapshot()
    freed = max(0, after["ram_available"] - before["ram_available"])
    result.setdefault("ok", False)
    result["freed"] = freed
    result["freed_h"] = fmt_bytes(freed)
    result["before"] = before
    result["after"] = after
    return result


def compact_memory() -> dict:
    return privileged.run("compact_memory")
