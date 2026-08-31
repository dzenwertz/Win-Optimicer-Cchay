"""Invocación del asistente privilegiado mediante pkexec."""

from __future__ import annotations

import json
import shutil
import subprocess
from typing import Any

from .paths import find_helper


def run(action: str, timeout: int = 180, **params: Any) -> dict:
    helper = find_helper()
    if helper is None:
        return {
            "ok": False,
            "error": "No se encontró el asistente privilegiado. Instale RETH Optimicer.",
        }
    pkexec = shutil.which("pkexec")
    if pkexec is None:
        return {"ok": False, "error": "pkexec no está disponible en el sistema."}

    payload = json.dumps({"action": action, **params}, ensure_ascii=False)
    try:
        proc = subprocess.run(
            [pkexec, str(helper)],
            input=payload.encode("utf-8"),
            capture_output=True,
            timeout=timeout,
            check=False,
        )
    except subprocess.TimeoutExpired:
        return {"ok": False, "error": "Tiempo de espera agotado."}
    except OSError as exc:
        return {"ok": False, "error": str(exc)}

    if proc.returncode in (126, 127):
        return {"ok": False, "error": "Autorización cancelada o denegada."}

    stdout = proc.stdout.decode("utf-8", errors="replace").strip()
    stderr = proc.stderr.decode("utf-8", errors="replace").strip()
    if stdout:
        try:
            data = json.loads(stdout.splitlines()[-1])
            if isinstance(data, dict):
                return data
        except json.JSONDecodeError:
            pass
    if proc.returncode != 0:
        return {"ok": False, "error": stderr or stdout or f"Código {proc.returncode}"}
    return {"ok": False, "error": "Respuesta inválida del asistente."}


def available() -> bool:
    return find_helper() is not None and shutil.which("pkexec") is not None
