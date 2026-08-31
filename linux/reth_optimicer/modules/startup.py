"""Aplicaciones de inicio automático (~/.config/autostart)."""

from __future__ import annotations

import configparser
from pathlib import Path

AUTOSTART = Path.home() / ".config" / "autostart"


def _parse(path: Path) -> dict:
    parser = configparser.ConfigParser(interpolation=None, strict=False)
    parser.optionxform = str
    try:
        parser.read(path, encoding="utf-8")
    except (OSError, configparser.Error):
        return {
            "file": path.name,
            "path": str(path),
            "name": path.stem,
            "exec": "",
            "enabled": False,
            "comment": "",
        }
    section = "Desktop Entry"
    name = parser.get(section, "Name", fallback=path.stem) if parser.has_section(section) else path.stem
    exe = parser.get(section, "Exec", fallback="") if parser.has_section(section) else ""
    comment = parser.get(section, "Comment", fallback="") if parser.has_section(section) else ""
    hidden = False
    if parser.has_section(section):
        hidden = parser.getboolean(section, "Hidden", fallback=False)
        if parser.has_option(section, "X-GNOME-Autostart-enabled"):
            enabled_flag = parser.getboolean(section, "X-GNOME-Autostart-enabled", fallback=True)
        else:
            enabled_flag = True
    else:
        enabled_flag = True
    return {
        "file": path.name,
        "path": str(path),
        "name": name,
        "exec": exe,
        "comment": comment,
        "enabled": (not hidden) and enabled_flag,
    }


def list_entries() -> list[dict]:
    if not AUTOSTART.is_dir():
        return []
    items = []
    for path in sorted(AUTOSTART.glob("*.desktop")):
        items.append(_parse(path))
    return items


def set_enabled(filename: str, enabled: bool) -> dict:
    name = Path(filename).name
    if name != filename or "/" in filename or not name.endswith(".desktop"):
        return {"ok": False, "error": "Archivo no válido."}
    path = AUTOSTART / name
    if not path.is_file():
        return {"ok": False, "error": "No existe el archivo de inicio."}

    text = path.read_text(encoding="utf-8", errors="replace")
    lines = text.splitlines()
    found_hidden = False
    found_gnome = False
    new_lines = []
    in_entry = False
    for line in lines:
        stripped = line.strip()
        if stripped.startswith("[") and stripped.endswith("]"):
            in_entry = stripped == "[Desktop Entry]"
        if in_entry and stripped.lower().startswith("hidden="):
            new_lines.append(f"Hidden={'false' if enabled else 'true'}")
            found_hidden = True
            continue
        if in_entry and stripped.lower().startswith("x-gnome-autostart-enabled="):
            new_lines.append(f"X-GNOME-Autostart-enabled={'true' if enabled else 'false'}")
            found_gnome = True
            continue
        new_lines.append(line)
    if not found_hidden:
        new_lines.append(f"Hidden={'false' if enabled else 'true'}")
    if not found_gnome:
        new_lines.append(f"X-GNOME-Autostart-enabled={'true' if enabled else 'false'}")
    path.write_text("\n".join(new_lines) + "\n", encoding="utf-8")
    return {"ok": True, "file": name, "enabled": enabled}


def remove_entry(filename: str) -> dict:
    name = Path(filename).name
    if name != filename or not name.endswith(".desktop"):
        return {"ok": False, "error": "Archivo no válido."}
    path = AUTOSTART / name
    try:
        path.unlink()
    except OSError as exc:
        return {"ok": False, "error": str(exc)}
    return {"ok": True, "file": name}
