"""Unidades systemd de usuario y de sistema."""

from __future__ import annotations

import re
import subprocess

from .. import privileged

UNIT_RE = re.compile(r"^[A-Za-z0-9_@.:\\-]+\.(service|socket|timer)$")
PROTECTED = {
    "dbus.service",
    "dbus-broker.service",
    "systemd-journald.service",
    "systemd-logind.service",
    "systemd-resolved.service",
    "systemd-udevd.service",
    "systemd-networkd.service",
    "polkit.service",
    "NetworkManager.service",
    "sshd.service",
    "ssh.service",
    "gdm.service",
    "sddm.service",
    "lightdm.service",
    "display-manager.service",
}


def _plain_list(user: bool) -> list[dict]:
    cmd = ["systemctl", "list-unit-files", "--type=service", "--no-pager", "--no-legend"]
    if user:
        cmd.insert(1, "--user")
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=20, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return []
    items = []
    for line in proc.stdout.splitlines():
        parts = line.split()
        if len(parts) < 2:
            continue
        unit, state = parts[0], parts[1]
        if not unit.endswith(".service"):
            continue
        items.append(
            {
                "unit": unit,
                "state": state,
                "user": user,
                "protected": unit in PROTECTED,
            }
        )
    return items


def _active_set(user: bool) -> set[str]:
    cmd = ["systemctl", "list-units", "--type=service", "--state=running", "--no-pager", "--no-legend"]
    if user:
        cmd.insert(1, "--user")
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=20, check=False)
    except (OSError, subprocess.TimeoutExpired):
        return set()
    names = set()
    for line in proc.stdout.splitlines():
        parts = line.split()
        if parts:
            names.add(parts[0])
    return names


def list_services(limit: int = 400) -> list[dict]:
    user_items = _plain_list(True)
    sys_items = _plain_list(False)
    user_active = _active_set(True)
    sys_active = _active_set(False)
    combined = []
    for item in user_items:
        item["active"] = item["unit"] in user_active
        item["scope"] = "usuario"
        combined.append(item)
    for item in sys_items:
        item["active"] = item["unit"] in sys_active
        item["scope"] = "sistema"
        combined.append(item)

    def rank(it: dict) -> tuple:
        enabled = it["state"] in {"enabled", "enabled-runtime", "static", "indirect"}
        return (0 if it["active"] else 1, 0 if enabled else 1, it["unit"])

    combined.sort(key=rank)
    return combined[:limit]


def set_enabled(unit: str, enabled: bool, user: bool) -> dict:
    if not UNIT_RE.fullmatch(unit):
        return {"ok": False, "error": "Nombre de unidad no válido."}
    if unit in PROTECTED and not enabled:
        return {"ok": False, "error": "Esta unidad está protegida."}
    op = "enable" if enabled else "disable"
    if user:
        cmd = ["systemctl", "--user", op, "--now", unit]
        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, timeout=30, check=False)
        except (OSError, subprocess.TimeoutExpired) as exc:
            return {"ok": False, "error": str(exc)}
        if proc.returncode != 0:
            return {"ok": False, "error": (proc.stderr or proc.stdout).strip() or "Error"}
        return {"ok": True, "message": f"{unit}: {op}"}
    return privileged.run("systemctl", operation=op, unit=unit)
