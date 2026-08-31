"""Privacidad: telemetría y reportes de fallos conocidos."""

from __future__ import annotations

import shutil
from pathlib import Path

from .. import privileged

ITEMS = [
    {
        "id": "apport",
        "title": "Apport",
        "subtitle": "Informes de fallos de Ubuntu/Debian.",
        "unit": "apport.service",
        "paths": [Path("/etc/default/apport"), Path("/usr/share/apport")],
        "bins": ["apport-cli"],
    },
    {
        "id": "whoopsie",
        "title": "Whoopsie",
        "subtitle": "Envío de informes de fallos a Canonical.",
        "unit": "whoopsie.service",
        "paths": [Path("/usr/bin/whoopsie")],
        "bins": ["whoopsie"],
    },
    {
        "id": "popularity-contest",
        "title": "popularity-contest",
        "subtitle": "Estadísticas de paquetes enviadas de forma periódica.",
        "unit": None,
        "paths": [Path("/etc/popularity-contest.conf"), Path("/usr/sbin/popularity-contest")],
        "bins": ["popularity-contest"],
    },
    {
        "id": "ubuntu-report",
        "title": "ubuntu-report",
        "subtitle": "Informe de instalación de Ubuntu.",
        "unit": None,
        "paths": [Path("/usr/bin/ubuntu-report")],
        "bins": ["ubuntu-report"],
    },
]


def _present(item: dict) -> bool:
    if any(p.exists() for p in item["paths"]):
        return True
    if any(shutil.which(b) for b in item["bins"]):
        return True
    unit = item.get("unit")
    if unit:
        path = Path("/lib/systemd/system") / unit
        path2 = Path("/usr/lib/systemd/system") / unit
        if path.exists() or path2.exists():
            return True
    return False


def _apport_enabled() -> bool:
    path = Path("/etc/default/apport")
    if path.is_file():
        try:
            for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
                if line.strip().startswith("enabled="):
                    return line.split("=", 1)[-1].strip() not in {"0", "false", "False"}
        except OSError:
            return True
    return Path("/usr/share/apport").exists()


def _popcon_enabled() -> bool:
    path = Path("/etc/popularity-contest.conf")
    if not path.is_file():
        return False
    try:
        for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.strip().startswith("PARTICIPATE"):
                return "yes" in line.lower()
    except OSError:
        return False
    return True


def scan() -> list[dict]:
    rows = []
    for item in ITEMS:
        present = _present(item)
        enabled = False
        if present:
            if item["id"] == "apport":
                enabled = _apport_enabled()
            elif item["id"] == "popularity-contest":
                enabled = _popcon_enabled()
            else:
                enabled = present
        rows.append(
            {
                "id": item["id"],
                "title": item["title"],
                "subtitle": item["subtitle"],
                "present": present,
                "enabled": enabled,
            }
        )
    return rows


def disable(item_id: str) -> dict:
    ids = {i["id"] for i in ITEMS}
    if item_id not in ids:
        return {"ok": False, "error": "Elemento no válido."}
    return privileged.run("privacy_disable", target=item_id)


def disable_all_present() -> dict:
    results = []
    ok = True
    for item in scan():
        if item["present"] and item["enabled"]:
            res = disable(item["id"])
            results.append({"id": item["id"], **res})
            ok = ok and bool(res.get("ok"))
    return {"ok": ok, "steps": results}
