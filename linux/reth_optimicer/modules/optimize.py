"""Pipeline de un clic con pasos seguros."""

from __future__ import annotations

from typing import Callable

from ..paths import fmt_bytes, load_settings
from . import disk, memory, tweaks

Progress = Callable[[str, float], None]

SAFE_STEPS = (
    "user_cache",
    "thumbnails",
    "journal_user",
    "drop_caches",
    "fstrim",
)


def run_safe(progress: Progress | None = None, settings: dict | None = None) -> dict:
    cfg = settings if settings is not None else load_settings()
    steps: list[dict] = []
    total_freed = 0
    n = 0

    def emit(message: str, frac: float) -> None:
        if progress:
            progress(message, frac)

    def add(step_id: str, title: str, result: dict) -> None:
        nonlocal total_freed
        freed = int(result.get("freed") or 0)
        total_freed += freed
        steps.append(
            {
                "id": step_id,
                "title": title,
                "ok": bool(result.get("ok")),
                "message": result.get("message") or result.get("error") or "",
                "freed": freed,
            }
        )

    plan: list[tuple[str, str, bool]] = []
    if cfg.get("optimize_thumbnails", True):
        plan.append(("thumbnails", "Limpiando miniaturas", True))
    if cfg.get("optimize_user_cache", True):
        plan.append(("user_cache", "Limpiando caché de usuario", True))
    if cfg.get("optimize_journal", True):
        plan.append(("journal_user", "Compactando diario de usuario", True))
    if cfg.get("optimize_empty_trash", False):
        plan.append(("trash", "Vaciando papelera", True))
    if cfg.get("optimize_drop_caches", True):
        plan.append(("drop_caches", "Liberando cachés de página", False))
    if cfg.get("optimize_fstrim", True):
        plan.append(("fstrim", "Ejecutando TRIM", False))

    count = max(1, len(plan))
    for step_id, title, local in plan:
        emit(title + "…", n / count)
        if step_id == "drop_caches":
            add(step_id, title, memory.drop_caches())
        elif step_id == "fstrim":
            add(step_id, title, tweaks.run_fstrim())
        else:
            add(step_id, title, disk.clean_target(step_id))
        n += 1
        emit(title, n / count)

    emit("Optimización finalizada", 1.0)
    ok = True
    for step in steps:
        if step["id"] in {"drop_caches", "fstrim"}:
            continue
        ok = ok and step["ok"]
    return {
        "ok": ok,
        "steps": steps,
        "freed": total_freed,
        "freed_h": fmt_bytes(total_freed),
    }
