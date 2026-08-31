"""Diagnóstico: systemd-analyze, errores del diario, TRIM y actualizaciones."""

from __future__ import annotations

import shutil
import subprocess

from .. import privileged


def _run(cmd: list[str], timeout: int = 20) -> str:
    if shutil.which(cmd[0]) is None:
        return ""
    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired) as exc:
        return str(exc)
    out = (proc.stdout or "").strip()
    err = (proc.stderr or "").strip()
    return out or err


def systemd_analyze() -> str:
    return _run(["systemd-analyze"])


def blame(limit: int = 15) -> list[str]:
    text = _run(["systemd-analyze", "blame"])
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    return lines[:limit]


def journal_errors(limit: int = 40) -> list[str]:
    text = _run(
        ["journalctl", "-p", "err", "-n", str(limit), "--no-pager", "--output=short"],
        timeout=15,
    )
    if not text:
        return []
    return [ln for ln in text.splitlines() if ln.strip()][:limit]


def update_hint() -> str:
    if shutil.which("pacman"):
        proc = None
        try:
            proc = subprocess.run(
                ["pacman", "-Qu"],
                capture_output=True,
                text=True,
                timeout=20,
                check=False,
            )
        except (OSError, subprocess.TimeoutExpired):
            return "No se pudo consultar actualizaciones de pacman."
        if proc is None:
            return ""
        lines = [ln for ln in proc.stdout.splitlines() if ln.strip()]
        if not lines:
            return "pacman: el sistema parece actualizado (o necesita sincronizar las bases)."
        return f"pacman: {len(lines)} paquete(s) pendientes de actualizar."
    if shutil.which("apt-get"):
        return "APT: use «sudo apt update && sudo apt upgrade» para aplicar actualizaciones."
    if shutil.which("dnf"):
        return "DNF: use «sudo dnf upgrade» para aplicar actualizaciones."
    return "No se detectó un gestor de paquetes conocido."


def report() -> dict:
    return {
        "analyze": systemd_analyze(),
        "blame": blame(),
        "errors": journal_errors(),
        "updates": update_hint(),
        "fstrim_available": bool(shutil.which("fstrim")),
    }


def run_fstrim() -> dict:
    return privileged.run("fstrim")
