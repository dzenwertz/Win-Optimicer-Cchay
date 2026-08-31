"""DNS y caché de resolución."""

from __future__ import annotations

import shutil
import subprocess

from .. import privileged

PROVIDERS = [
    {
        "id": "dhcp",
        "title": "Automático (DHCP)",
        "primary": None,
        "secondary": None,
    },
    {
        "id": "cloudflare",
        "title": "Cloudflare",
        "primary": "1.1.1.1",
        "secondary": "1.0.0.1",
    },
    {
        "id": "google",
        "title": "Google",
        "primary": "8.8.8.8",
        "secondary": "8.8.4.4",
    },
    {
        "id": "quad9",
        "title": "Quad9",
        "primary": "9.9.9.9",
        "secondary": "149.112.112.112",
    },
    {
        "id": "adguard",
        "title": "AdGuard",
        "primary": "94.140.14.14",
        "secondary": "94.140.15.15",
    },
]


def list_providers() -> list[dict]:
    items = []
    for p in PROVIDERS:
        row = dict(p)
        row["ping_ms"] = None
        if p["primary"]:
            row["ping_ms"] = ping(p["primary"])
        items.append(row)
    return items


def ping(host: str, timeout: int = 1) -> float | None:
    ping_bin = shutil.which("ping")
    if ping_bin is None:
        return None
    try:
        proc = subprocess.run(
            [ping_bin, "-c", "1", "-W", str(timeout), host],
            capture_output=True,
            text=True,
            timeout=timeout + 2,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired):
        return None
    if proc.returncode != 0:
        return None
    for token in proc.stdout.replace("=", " ").replace("time", " ").split():
        if token.endswith("ms"):
            try:
                return float(token[:-2])
            except ValueError:
                continue
    import re

    match = re.search(r"time[=<]([0-9.]+)", proc.stdout)
    if match:
        return float(match.group(1))
    return None


def current_dns() -> list[str]:
    for cmd in (
        ["resolvectl", "dns"],
        ["resolvectl", "status"],
    ):
        if shutil.which(cmd[0]) is None:
            continue
        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, timeout=8, check=False)
        except (OSError, subprocess.TimeoutExpired):
            continue
        if proc.returncode == 0 and proc.stdout.strip():
            return [ln.strip() for ln in proc.stdout.splitlines() if ln.strip()][:24]
    resolv = "/etc/resolv.conf"
    try:
        lines = open(resolv, encoding="utf-8", errors="replace").read().splitlines()
        return [ln for ln in lines if ln.startswith("nameserver") or ln.startswith("#")][:24]
    except OSError:
        return []


def set_provider(provider_id: str) -> dict:
    ids = {p["id"] for p in PROVIDERS}
    if provider_id not in ids:
        return {"ok": False, "error": "Proveedor DNS no válido."}
    return privileged.run("set_dns", provider=provider_id)


def flush() -> dict:
    if shutil.which("resolvectl"):
        try:
            proc = subprocess.run(
                ["resolvectl", "flush-caches"],
                capture_output=True,
                text=True,
                timeout=10,
                check=False,
            )
            if proc.returncode == 0:
                return {"ok": True, "message": "Caché DNS de usuario vaciada."}
        except (OSError, subprocess.TimeoutExpired):
            pass
    return privileged.run("flush_dns")
