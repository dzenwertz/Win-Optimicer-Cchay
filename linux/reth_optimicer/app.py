"""Interfaz GTK 4 / libadwaita de RETH Optimicer."""
from __future__ import annotations

import fcntl
import os
import re
import threading
from pathlib import Path

import gi

gi.require_version("Gtk", "4.0")
gi.require_version("Adw", "1")
from gi.repository import Adw, Gdk, Gio, GLib, Gtk  # noqa: E402

from . import __app_id__, __app_name__, __version__
from .modules import (
    benchmark,
    disk,
    memory,
    network,
    optimize,
    packages,
    privacy,
    repair,
    services,
    startup,
    tweaks,
)
from .paths import (
    LOCK_FILE,
    asset,
    ensure_dirs,
    fmt_bytes,
    load_settings,
    save_settings,
)

# (id, título, icono)
PAGES = [
    ("inicio", "Inicio", "user-home-symbolic"),
    ("memoria", "Memoria", "media-flash-symbolic"),
    ("disco", "Disco", "drive-harddisk-symbolic"),
    ("ajustes", "Rendimiento", "power-profile-performance-symbolic"),
    ("servicios", "Servicios", "system-run-symbolic"),
    ("autostart", "Inicio automático", "preferences-desktop-apps-symbolic"),
    ("red", "Red", "preferences-system-network-symbolic"),
    ("paquetes", "Paquetes", "package-x-generic-symbolic"),
    ("privacidad", "Privacidad", "preferences-system-privacy-symbolic"),
    ("reparar", "Diagnóstico", "system-search-symbolic"),
    ("benchmark", "Prueba de rendimiento", "applications-engineering-symbolic"),
    ("preferencias", "Preferencias", "emblem-system-symbolic"),
]

SERVICES_SHOWN = 40
# «1min 2.345s unidad.service» o «812ms unidad.service»
_BLAME_RE = re.compile(r"^((?:\d+min\s*)?[\d.]+\s*(?:ms|s|min))\s+(\S.*)$")


def _css() -> None:
    provider = Gtk.CssProvider()
    css_path = Path(__file__).with_name("theme.css")
    if css_path.is_file():
        provider.load_from_path(str(css_path))
        Gtk.StyleContext.add_provider_for_display(
            Gdk.Display.get_default(),
            provider,
            Gtk.STYLE_PROVIDER_PRIORITY_APPLICATION,
        )


def _toast(window: Adw.ApplicationWindow, text: str) -> None:
    overlay = getattr(window, "_toast_overlay", None)
    if overlay is None:
        return
    overlay.add_toast(Adw.Toast.new(text))


def _confirm(parent: Gtk.Window, title: str, body: str, callback, label: str = "Continuar") -> None:
    dialog = Adw.MessageDialog.new(parent, title, body)
    dialog.add_response("cancel", "Cancelar")
    dialog.add_response("ok", label)
    dialog.set_response_appearance("ok", Adw.ResponseAppearance.DESTRUCTIVE)
    dialog.set_default_response("cancel")
    dialog.connect("response", lambda d, resp: callback() if resp == "ok" else None)
    dialog.present()


def _bg(fn, done):
    def worker():
        try:
            result = fn()
            err = None
        except Exception as exc:  # noqa: BLE001
            result, err = None, exc
        GLib.idle_add(done, result, err)

    threading.Thread(target=worker, daemon=True).start()


def _clear(box: Gtk.Widget) -> None:
    child = box.get_first_child()
    while child:
        nxt = child.get_next_sibling()
        box.remove(child)
        child = nxt


def _row(title: str, subtitle: str = "", *suffixes: Gtk.Widget, icon: str | None = None) -> Adw.ActionRow:
    row = Adw.ActionRow(title=title, subtitle=subtitle)
    row.set_use_markup(False)
    if icon:
        row.add_prefix(Gtk.Image.new_from_icon_name(icon))
    for widget in suffixes:
        widget.set_valign(Gtk.Align.CENTER)
        row.add_suffix(widget)
    return row


def _button(label: str, callback, primary: bool = False, destructive: bool = False) -> Gtk.Button:
    btn = Gtk.Button(label=label)
    btn.add_css_class("pill-button")
    if primary:
        btn.add_css_class("suggested-action")
    elif destructive:
        btn.add_css_class("destructive-action")
    btn.connect("clicked", callback)
    return btn


def _value(text: str) -> Gtk.Label:
    lbl = Gtk.Label(label=text)
    lbl.add_css_class("dim-label")
    lbl.add_css_class("numeric")
    return lbl


def _badge(text: str, ok: bool = True) -> Gtk.Label:
    lbl = Gtk.Label(label=text)
    lbl.add_css_class("badge")
    lbl.add_css_class("badge-ok" if ok else "badge-muted")
    return lbl


def _group(title: str = "", description: str = "") -> Adw.PreferencesGroup:
    group = Adw.PreferencesGroup()
    if title:
        group.set_title(GLib.markup_escape_text(title))
    if description:
        group.set_description(GLib.markup_escape_text(description))
    return group


def _placeholder(group: Adw.PreferencesGroup, text: str = "Cargando…") -> None:
    group.add(_row(text))


class _GroupSlot:
    """Grupo cuyo contenido se reemplaza al recargar."""

    def __init__(self, title: str = "", description: str = ""):
        self.title = title
        self.description = description
        self.holder = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        self.group = _group(title, description)
        self.holder.append(self.group)

    def reset(self, description: str | None = None) -> Adw.PreferencesGroup:
        self.holder.remove(self.group)
        self.group = _group(self.title, self.description if description is None else description)
        self.holder.append(self.group)
        return self.group


class RethWindow(Adw.ApplicationWindow):
    def __init__(self, app: Adw.Application):
        super().__init__(application=app, title=__app_name__)
        self.set_default_size(1040, 720)
        self.set_size_request(360, 480)
        self.settings = load_settings()
        self._loaded: set[str] = set()
        self._loaders = {
            "memoria": self._fill_mem,
            "disco": self._fill_disk,
            "ajustes": self._fill_tweaks,
            "servicios": self._fill_services,
            "autostart": self._fill_startup,
            "red": self._fill_network,
            "paquetes": self._fill_packages,
            "privacidad": self._fill_privacy,
            "reparar": self._fill_repair,
        }

        self._toast_overlay = Adw.ToastOverlay()
        self._split = Adw.NavigationSplitView()
        self._split.set_min_sidebar_width(220)
        self._split.set_max_sidebar_width(260)
        self._split.set_sidebar(self._build_sidebar())
        self._split.set_content(self._build_content())
        self._toast_overlay.set_child(self._split)
        self.set_content(self._toast_overlay)

        if hasattr(Adw, "Breakpoint"):
            bp = Adw.Breakpoint.new(Adw.BreakpointCondition.parse("max-width: 640sp"))
            bp.add_setter(self._split, "collapsed", True)
            self.add_breakpoint(bp)

        self._list.connect("row-selected", self._on_nav)
        self._list.select_row(self._list.get_row_at_index(0))

        GLib.timeout_add_seconds(4, self._refresh_dashboard)
        GLib.idle_add(self._refresh_dashboard)

    # ── Estructura ────────────────────────────────────────────────────────

    def _build_sidebar(self) -> Adw.NavigationPage:
        brand = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
        icon = asset("bird.png")
        if icon:
            logo = Gtk.Image.new_from_file(str(icon))
            logo.set_pixel_size(20)
            brand.append(logo)
        name = Gtk.Label(label="RETH Optimicer")
        name.add_css_class("heading")
        brand.append(name)

        header = Adw.HeaderBar()
        header.set_title_widget(brand)
        header.set_show_end_title_buttons(False)

        self._list = Gtk.ListBox()
        self._list.add_css_class("navigation-sidebar")
        for key, title, icon_name in PAGES:
            row = Gtk.ListBoxRow()
            box = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=12)
            box.append(Gtk.Image.new_from_icon_name(icon_name))
            box.append(Gtk.Label(label=title, xalign=0))
            row.set_child(box)
            row._page_id = key  # type: ignore[attr-defined]
            row._page_title = title  # type: ignore[attr-defined]
            self._list.append(row)

        scroller = Gtk.ScrolledWindow()
        scroller.set_policy(Gtk.PolicyType.NEVER, Gtk.PolicyType.AUTOMATIC)
        scroller.set_child(self._list)

        view = Adw.ToolbarView()
        view.add_top_bar(header)
        view.set_content(scroller)
        page = Adw.NavigationPage(title="RETH Optimicer")
        page.set_child(view)
        return page

    def _build_content(self) -> Adw.NavigationPage:
        self._stack = Gtk.Stack()
        self._stack.set_transition_type(Gtk.StackTransitionType.CROSSFADE)
        self._stack.set_transition_duration(120)
        builders = {
            "inicio": self._page_home,
            "memoria": self._page_memory,
            "disco": self._page_disk,
            "ajustes": self._page_tweaks,
            "servicios": self._page_services,
            "autostart": self._page_startup,
            "red": self._page_network,
            "paquetes": self._page_packages,
            "privacidad": self._page_privacy,
            "reparar": self._page_repair,
            "benchmark": self._page_bench,
            "preferencias": self._page_prefs,
        }
        for key, _title, _icon in PAGES:
            self._stack.add_named(builders[key](), key)

        header = Adw.HeaderBar()
        header.set_title_widget(Gtk.Label(label=""))

        view = Adw.ToolbarView()
        view.add_top_bar(header)
        view.set_content(self._stack)
        self._content_page = Adw.NavigationPage(title="Inicio")
        self._content_page.set_child(view)
        return self._content_page

    def _page(self, *children: Gtk.Widget, title: str = "", subtitle: str = "") -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=28)
        box.set_margin_top(28)
        box.set_margin_bottom(36)
        box.set_margin_start(20)
        box.set_margin_end(20)
        if title:
            head = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=4)
            t = Gtk.Label(label=title, xalign=0, wrap=True)
            t.add_css_class("page-title")
            head.append(t)
            if subtitle:
                s = Gtk.Label(label=subtitle, xalign=0, wrap=True)
                s.add_css_class("dim-label")
                head.append(s)
            box.append(head)
        for child in children:
            box.append(child)
        clamp = Adw.Clamp(maximum_size=720, tightening_threshold=560)
        clamp.set_child(box)
        scroller = Gtk.ScrolledWindow()
        scroller.set_policy(Gtk.PolicyType.NEVER, Gtk.PolicyType.AUTOMATIC)
        scroller.set_child(clamp)
        return scroller

    def _on_nav(self, _list, row):
        if row is None or not hasattr(self, "_stack"):
            return
        key = row._page_id
        self._stack.set_visible_child_name(key)
        # El título grande va en la página; la barra queda limpia.
        self._content_page.set_title(row._page_title)
        self._split.set_show_content(True)
        if key in self._loaders and key not in self._loaded:
            self._loaded.add(key)
            self._loaders[key]()

    # ── Inicio ────────────────────────────────────────────────────────────

    def _tile(self, caption: str) -> tuple[Gtk.Box, Gtk.Label, Gtk.Label, Gtk.LevelBar]:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        box.add_css_class("card")
        box.add_css_class("tile")
        box.set_hexpand(True)
        box.set_size_request(150, -1)
        cap = Gtk.Label(label=caption, xalign=0)
        cap.add_css_class("caption-heading")
        cap.add_css_class("dim-label")
        value = Gtk.Label(label="—", xalign=0)
        value.add_css_class("tile-value")
        value.add_css_class("numeric")
        detail = Gtk.Label(label="", xalign=0, ellipsize=3)
        detail.add_css_class("caption")
        detail.add_css_class("dim-label")
        level = Gtk.LevelBar(min_value=0, max_value=1)
        level.add_css_class("tile-level")
        for widget in (cap, value, level, detail):
            box.append(widget)
        return box, value, detail, level

    def _page_home(self) -> Gtk.Widget:
        tiles = Gtk.FlowBox()
        tiles.set_selection_mode(Gtk.SelectionMode.NONE)
        tiles.set_homogeneous(True)
        tiles.set_min_children_per_line(1)
        tiles.set_max_children_per_line(3)
        tiles.set_column_spacing(12)
        tiles.set_row_spacing(12)
        ram, self._ram_value, self._ram_detail, self._ram_level = self._tile("Memoria disponible")
        dsk, self._disk_value, self._disk_detail, self._disk_level = self._tile("Disco libre")
        cpu, self._cpu_value, self._cpu_detail, self._cpu_level = self._tile("Uso de CPU")
        for tile in (ram, dsk, cpu):
            tiles.append(tile)

        actions = _group("Mantenimiento")
        self._opt_btn = _button("Ejecutar", self._on_optimize, primary=True)
        actions.add(_row(
            "Mantenimiento seguro",
            "Borra caché, miniaturas y registros antiguos. No toca tus archivos ni la papelera.",
            self._opt_btn,
        ))
        self._ram_btn = _button("Liberar", self._on_reclaim)
        actions.add(_row(
            "Liberar memoria",
            "Pide tu contraseña porque actúa sobre el kernel.",
            self._ram_btn,
        ))

        self._advice = Gtk.Label(label="", xalign=0, wrap=True)
        self._advice.add_css_class("dim-label")

        info = _group("Este equipo")
        self._info: dict[str, Adw.ActionRow] = {}
        for key, title in (("distro", "Sistema"), ("kernel", "Kernel"), ("cpu", "Procesador"), ("uptime", "Encendido")):
            row = _row(title, "")
            row.add_css_class("property")
            self._info[key] = row
            info.add(row)

        self._home_status = Gtk.Label(label="Comprobando tu equipo…", xalign=0, wrap=True)
        self._home_status.add_css_class("page-title")
        head = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=6)
        head.append(self._home_status)
        head.append(self._advice)
        return self._page(head, tiles, actions, info)

    def _refresh_dashboard(self) -> bool:
        snap = memory.snapshot()
        total = snap["ram_total"] or 1
        avail_frac = snap["ram_available"] / total
        self._ram_value.set_text(snap["ram_available_h"])
        self._ram_level.set_value(1 - min(1.0, max(0.0, avail_frac)))
        self._ram_detail.set_text(f"de {snap['ram_total_h']} · presión {snap['pressure']}")

        disks = snap.get("disks") or []
        if disks:
            used = min(100.0, max(0.0, float(disks[0].get("percent", 0))))
            self._disk_value.set_text(f"{100 - used:.0f} %")
            self._disk_level.set_value(used / 100)
            self._disk_detail.set_text(f"en {disks[0].get('mount', '/')}")
        cpu = float(snap.get("cpu_percent") or 0)
        self._cpu_value.set_text(f"{cpu:.0f} %")
        self._cpu_level.set_value(min(1.0, cpu / 100))
        self._cpu_detail.set_text(f"{snap.get('cpu_count') or '—'} hilos")

        if avail_frac < 0.1:
            status = "Queda poca memoria"
        elif disks and float(disks[0].get("percent", 0)) > 90:
            status = "El disco está casi lleno"
        else:
            status = "Tu equipo está bien"
        self._home_status.set_text(status)
        self._advice.set_text(self._short_advice(snap, avail_frac))

        self._info["distro"].set_subtitle(snap.get("distro") or "—")
        self._info["kernel"].set_subtitle(snap.get("kernel") or "—")
        self._info["cpu"].set_subtitle(snap.get("cpu") or "—")
        self._info["uptime"].set_subtitle(snap.get("uptime") or "—")
        if "memoria" in self._loaded and self._stack.get_visible_child_name() == "memoria":
            self._fill_mem()
        return True

    @staticmethod
    def _short_advice(snap: dict, avail_frac: float) -> str:
        """Una sola frase: lo que conviene hacer ahora."""
        try:
            top = memory.grouped_processes(1)
        except Exception:  # noqa: BLE001
            top = []
        heavy = top[0] if top and top[0].get("rss", 0) > 800 * 1024 * 1024 else None
        if avail_frac < 0.2 and heavy:
            return f"{heavy['label']} usa {heavy['rss_h']}. Cerrarlo es lo que más memoria libera."
        if avail_frac < 0.2:
            return "Cierra las apps que no uses para recuperar memoria."
        return "Hay margen de memoria. No necesitas hacer nada ahora."

    def _on_reclaim(self, button: Gtk.Button) -> None:
        button.set_sensitive(False)

        def done(result, err):
            button.set_sensitive(True)
            if err:
                _toast(self, str(err))
                return
            _toast(self, (result or {}).get("message") or (result or {}).get("error") or "Listo")
            self._refresh_dashboard()

        _bg(memory.reclaim_ram, done)

    def _on_optimize(self, button: Gtk.Button) -> None:
        button.set_sensitive(False)
        button.set_label("Trabajando…")

        def done(result, err):
            button.set_sensitive(True)
            button.set_label("Ejecutar")
            if err:
                _toast(self, str(err))
                return
            freed = fmt_bytes((result or {}).get("freed") or 0)
            if result and result.get("ok") is False:
                _toast(self, f"Mantenimiento con avisos. Liberado {freed}.")
            else:
                _toast(self, f"Mantenimiento listo. Liberado {freed}.")
            self._refresh_dashboard()

        _bg(lambda: optimize.run_safe(settings=self.settings), done)

    # ── Memoria ───────────────────────────────────────────────────────────

    def _page_memory(self) -> Gtk.Widget:
        self._mem_slot = _GroupSlot(
            "Aplicaciones",
            "Agrupadas por aplicación. Cerrar una app tuya no pide contraseña.",
        )
        _placeholder(self._mem_slot.group)
        return self._page(self._mem_slot.holder, title="Memoria", subtitle="Qué está usando la RAM ahora.")

    def _fill_mem(self) -> bool:
        def done(groups, err):
            group = self._mem_slot.reset()
            if err or not groups:
                _placeholder(group, "No se pudo leer la lista de procesos.")
                return
            for item in groups:
                suffix = [_value(item["rss_h"])]
                if item.get("closable"):
                    name, label, size = item["name"], item["label"], item["rss_h"]
                    btn = Gtk.Button(icon_name="window-close-symbolic", tooltip_text=f"Cerrar {label}")
                    btn.add_css_class("flat")
                    btn.add_css_class("circular")
                    btn.connect("clicked", lambda _b, n=name, l=label, s=size: self._close_group(n, l, s))
                    suffix.append(btn)
                n = item.get("count") or 1
                group.add(_row(item["label"], f"{n} proceso" + ("" if n == 1 else "s"), *suffix))

        _bg(lambda: memory.grouped_processes(12), done)
        return False

    def _close_group(self, name: str, label: str, size: str) -> None:
        def go():
            def done(result, err):
                if err:
                    _toast(self, str(err))
                    return
                if result and not result.get("ok"):
                    _toast(self, result.get("error") or "No se pudo cerrar")
                    return
                _toast(self, f"{label} cerrado")
                self._fill_mem()
                self._refresh_dashboard()

            _bg(lambda: memory.terminate_group(name), done)

        _confirm(self, f"¿Cerrar {label}?", f"Usa {size}. Guarda tu trabajo antes de cerrarla.", go, "Cerrar")

    # ── Disco ─────────────────────────────────────────────────────────────

    def _page_disk(self) -> Gtk.Widget:
        self._disk_slot = _GroupSlot("Se puede limpiar")
        _placeholder(self._disk_slot.group, "Analizando…")
        return self._page(self._disk_slot.holder, title="Disco", subtitle="Espacio que puedes recuperar sin perder tus archivos.")

    def _fill_disk(self) -> bool:
        def done(items, err):
            group = self._disk_slot.reset()
            if err or not items:
                _placeholder(group, "No hay nada que limpiar.")
                return
            for item in items:
                tid = item["id"]
                destructive = bool(item.get("destructive"))
                btn = _button("Limpiar", lambda _b, t=tid, d=destructive: self._clean_disk(t, d), destructive=destructive)
                group.add(_row(item["title"], item.get("subtitle") or "", _value(fmt_bytes(item.get("bytes") or 0)), btn))

        _bg(disk.scan_targets, done)
        return False

    def _clean_disk(self, target_id: str, destructive: bool) -> None:
        def go():
            self._run_action(lambda: disk.clean_target(target_id), "Limpieza completada", after=self._fill_disk)

        if destructive and self.settings.get("confirm_destructive", True):
            _confirm(self, "¿Vaciar la papelera?", "No se puede deshacer.", go, "Vaciar")
        else:
            go()

    # ── Rendimiento (sysctl, gobernador, TRIM) ────────────────────────────

    def _page_tweaks(self) -> Gtk.Widget:
        self._tweak_slot = _GroupSlot("Memoria virtual", "RETH guarda el valor original de cada cambio para poder restaurarlo.")
        _placeholder(self._tweak_slot.group)
        self._cpu_slot = _GroupSlot("Procesador y almacenamiento")
        _placeholder(self._cpu_slot.group)
        return self._page(
            self._tweak_slot.holder,
            self._cpu_slot.holder,
            title="Rendimiento",
            subtitle="Ajustes del kernel. Piden tu contraseña.",
        )

    def _fill_tweaks(self) -> bool:
        def done(items, err):
            vm = self._tweak_slot.reset()
            cpu = self._cpu_slot.reset()
            if err or not items:
                _placeholder(vm, "No disponible en este sistema.")
                return
            for spec in items:
                kind = spec["kind"]
                if kind == "sysctl":
                    cur, rec = spec.get("current"), spec.get("recommended")
                    if cur is None:
                        continue
                    orig = spec.get("original")
                    if orig is not None and orig != cur:
                        key = spec["id"]
                        btn = _button("Restaurar", lambda _b, k=key: self._run_action(
                            lambda: tweaks.restore_sysctl(k), f"{k} restaurado", after=self._fill_tweaks))
                        vm.add(_row(spec["title"], f"Cambiado por RETH: {cur}. Valor original: {orig}.", btn))
                    elif cur == rec:
                        sub = spec.get("note") or spec["subtitle"]
                        vm.add(_row(spec["title"], f"{sub} Valor actual: {cur}.", _badge("Óptimo")))
                    else:
                        key, val = spec["id"], int(rec)
                        sub = f"{spec['subtitle']} Actual {cur} · recomendado {rec}."
                        if spec.get("note"):
                            sub += f" {spec['note']}"
                        btn = _button("Aplicar", lambda _b, k=key, v=val: self._run_action(
                            lambda: tweaks.apply_sysctl(k, v), f"{k} = {v}", after=self._fill_tweaks))
                        vm.add(_row(spec["title"], sub, btn))
                elif kind == "governor" and spec.get("options"):
                    options = spec["options"]
                    combo = Adw.ComboRow(title=spec["title"], subtitle="Cómo equilibra rendimiento y consumo.")
                    combo.set_model(Gtk.StringList.new(options))
                    current = spec.get("current")
                    if current in options:
                        combo.set_selected(options.index(current))
                    combo.connect("notify::selected", self._on_governor, options, current)
                    cpu.add(combo)
                    orig = spec.get("original")
                    if orig and orig != current:
                        cpu.add(_row("Gobernador original", f"Antes de RETH: {orig}.", _button(
                            "Restaurar", lambda *_: self._run_action(
                                tweaks.restore_governor, "Gobernador restaurado", after=self._fill_tweaks))))
                elif kind == "fstrim" and spec.get("available"):
                    btn = _button("Ejecutar", lambda *_: self._run_action(tweaks.run_fstrim, "TRIM finalizado"))
                    cpu.add(_row(spec["title"], spec["subtitle"], btn))

        _bg(tweaks.list_tweaks, done)
        return False

    def _on_governor(self, combo: Adw.ComboRow, _pspec, options: list[str], previous: str | None) -> None:
        name = options[combo.get_selected()]
        if name == previous:
            return
        self._run_action(lambda: tweaks.apply_governor(name), f"Gobernador: {name}", after=self._fill_tweaks)

    # ── Servicios ─────────────────────────────────────────────────────────

    def _page_services(self) -> Gtk.Widget:
        self._svc_slot = _GroupSlot()
        _placeholder(self._svc_slot.group)
        return self._page(
            self._svc_slot.holder,
            title="Servicios",
            subtitle="Los servicios del sistema piden confirmación y contraseña.",
        )

    def _fill_services(self) -> bool:
        def done(items, err):
            group = self._svc_slot.reset(f"Los {SERVICES_SHOWN} más relevantes, primero los que están en marcha.")
            if err or not items:
                _placeholder(group, "No se pudo leer systemd.")
                return
            for it in items:
                unit = it.get("unit") or ""
                name = unit.removesuffix(".service")
                is_user = bool(it.get("user"))
                scope = "Usuario" if is_user else "Sistema"
                running = "en marcha" if it.get("active") else "detenido"
                sub = f"{scope} · {running}"
                state = it.get("state") or ""
                if it.get("protected"):
                    group.add(_row(name, sub + " · protegido", _badge("Protegido", ok=False)))
                elif state in {"enabled", "disabled"}:
                    sw = Gtk.Switch(active=state == "enabled")
                    sw.connect("state-set", self._on_service_toggle, unit, is_user)
                    group.add(_row(name, sub, sw))
                else:
                    group.add(_row(name, f"{sub} · {state}"))

        _bg(lambda: services.list_services(SERVICES_SHOWN), done)
        return False

    def _on_service_toggle(self, switch: Gtk.Switch, state: bool, unit: str, user: bool) -> bool:
        def apply():
            def done(result, err):
                if err or not (result or {}).get("ok"):
                    _toast(self, str(err) if err else (result or {}).get("error") or "No se pudo cambiar")
                    switch.set_state(not state)
                    switch.set_active(not state)
                    return
                switch.set_state(state)
                _toast(self, f"{unit}: {'activado' if state else 'desactivado'}")

            _bg(lambda: services.set_enabled(unit, state, user), done)

        if user:
            apply()
        else:
            verb = "activar" if state else "desactivar"
            dialog = Adw.MessageDialog.new(self, f"¿{verb.capitalize()} {unit}?", "Es un servicio del sistema; se pedirá tu contraseña.")
            dialog.add_response("cancel", "Cancelar")
            dialog.add_response("ok", verb.capitalize())
            dialog.set_default_response("cancel")

            def on_resp(_d, resp):
                if resp == "ok":
                    apply()
                else:
                    switch.set_active(not state)

            dialog.connect("response", on_resp)
            dialog.present()
        return True

    # ── Inicio automático ─────────────────────────────────────────────────

    def _page_startup(self) -> Gtk.Widget:
        self._start_slot = _GroupSlot("Al iniciar sesión", "Apps que se abren solas. Desactivarlas no las desinstala.")
        _placeholder(self._start_slot.group)
        return self._page(self._start_slot.holder, title="Inicio automático")

    def _fill_startup(self) -> bool:
        def done(entries, err):
            group = self._start_slot.reset()
            if err or not entries:
                _placeholder(group, "No hay apps en ~/.config/autostart")
                return
            for entry in entries:
                filename = entry.get("file") or entry.get("filename")
                sw = Gtk.Switch(active=bool(entry.get("enabled")))
                sw.connect("state-set", self._on_startup_toggle, filename)
                group.add(_row(entry.get("name") or filename, entry.get("comment") or "", sw))

        _bg(startup.list_entries, done)
        return False

    def _on_startup_toggle(self, switch: Gtk.Switch, state: bool, filename: str):
        res = startup.set_enabled(filename, state)
        if not res.get("ok"):
            _toast(self, res.get("error") or "No se pudo cambiar")
            switch.set_active(not state)
            return True
        return False

    # ── Red ───────────────────────────────────────────────────────────────

    def _page_network(self) -> Gtk.Widget:
        self._dns_slot = _GroupSlot("Servidor DNS", "Latencia medida desde este equipo.")
        _placeholder(self._dns_slot.group, "Midiendo latencia…")
        self._dns_now_slot = _GroupSlot("En uso ahora")
        _placeholder(self._dns_now_slot.group)
        tools = _group()
        tools.add(_row("Vaciar caché DNS", "Útil si una web no carga tras un cambio.",
                       _button("Vaciar", lambda *_: self._run_action(network.flush, "Caché DNS vaciada"))))
        return self._page(self._dns_slot.holder, self._dns_now_slot.holder, tools, title="Red")

    def _fill_network(self) -> bool:
        def work():
            return network.list_providers(), network.current_dns()

        def done(result, err):
            providers_g = self._dns_slot.reset()
            now_g = self._dns_now_slot.reset()
            if err or not result:
                _placeholder(providers_g, "No se pudo medir la red.")
                return
            providers, current = result
            for p in providers:
                ms = p.get("ping_ms")
                sub = f"{p['primary']} · {ms:.0f} ms" if ms is not None else (p["primary"] or "El de tu router o proveedor")
                pid = p["id"]
                btn = _button("Usar", lambda _b, i=pid: self._run_action(
                    lambda: network.set_provider(i), "DNS actualizado", after=self._fill_network))
                providers_g.add(_row(p["title"], sub, btn))
            if current:
                for server in current:
                    now_g.add(_row(server))
            else:
                _placeholder(now_g, "Sin datos")

        _bg(work, done)
        return False

    # ── Paquetes ──────────────────────────────────────────────────────────

    def _page_packages(self) -> Gtk.Widget:
        self._pkg_slot = _GroupSlot()
        _placeholder(self._pkg_slot.group)
        return self._page(self._pkg_slot.holder, title="Paquetes")

    def _fill_packages(self) -> bool:
        def done(st, err):
            group = self._pkg_slot.reset()
            if err or not st or not st.get("available"):
                _placeholder(group, "No se detectó un gestor de paquetes compatible.")
                return
            group.set_title(GLib.markup_escape_text(st["title"]))
            group.add(_row("Caché de descargas", "Paquetes ya instalados que se pueden volver a bajar.",
                           _value(st["cache_h"]),
                           _button("Limpiar", lambda *_: self._run_action(packages.clean_cache, "Caché limpia", after=self._fill_packages))))
            n = st["orphan_count"]
            orphans_btn = _button("Quitar", lambda *_: _confirm(
                self, "¿Quitar paquetes huérfanos?", "Se eliminarán dependencias que ya nada usa.",
                lambda: self._run_action(packages.remove_orphans, "Huérfanos eliminados", after=self._fill_packages), "Quitar"))
            orphans_btn.set_sensitive(bool(n))
            group.add(_row("Paquetes huérfanos", "Dependencias que ya no necesita ningún programa.", _value(str(n)), orphans_btn))

        _bg(packages.status, done)
        return False

    # ── Privacidad ────────────────────────────────────────────────────────

    def _page_privacy(self) -> Gtk.Widget:
        self._priv_slot = _GroupSlot("Envío de datos")
        _placeholder(self._priv_slot.group)
        return self._page(self._priv_slot.holder, title="Privacidad",
                          subtitle="Servicios que envían informes o estadísticas a terceros.")

    def _fill_privacy(self) -> bool:
        def done(items, err):
            group = self._priv_slot.reset()
            if err or not items:
                _placeholder(group, "No se pudo analizar.")
                return
            if not any(i["present"] for i in items):
                group.set_description("Ninguno está instalado en este sistema.")
            for item in items:
                iid = item["id"]
                if item.get("disabled_by_reth"):
                    row = _row(item["title"], "Desactivado por RETH.", _button(
                        "Restaurar", lambda _b, i=iid: self._run_action(
                            lambda: privacy.restore(i), "Reactivado", after=self._fill_privacy)))
                elif not item["present"]:
                    row = _row(item["title"], item["subtitle"], _badge("No instalado", ok=False))
                elif item.get("enabled"):
                    iid = item["id"]
                    row = _row(item["title"], item["subtitle"], _button("Desactivar", lambda _b, i=iid: self._run_action(
                        lambda: privacy.disable(i), "Desactivado", after=self._fill_privacy)))
                else:
                    row = _row(item["title"], item["subtitle"], _badge("Desactivado"))
                group.add(row)

        _bg(privacy.scan, done)
        return False

    # ── Diagnóstico ───────────────────────────────────────────────────────

    def _page_repair(self) -> Gtk.Widget:
        self._repair_slot = _GroupSlot("Arranque")
        _placeholder(self._repair_slot.group, "Analizando…")
        self._blame_slot = _GroupSlot("Lo que más tarda al arrancar")
        refresh = _group()
        refresh.add(_row("Volver a analizar", "", _button("Analizar", lambda *_: self._fill_repair())))
        return self._page(self._repair_slot.holder, self._blame_slot.holder, refresh, title="Diagnóstico")

    def _fill_repair(self) -> bool:
        def done(result, err):
            group = self._repair_slot.reset()
            blame_g = self._blame_slot.reset()
            if err or not result:
                _placeholder(group, str(err) if err else "Sin datos")
                return
            analyze = (result.get("analyze") or "").strip()
            group.add(_row("Tiempo de arranque", analyze or "Sin datos"))
            hint = result.get("updates") or result.get("hint")
            if hint:
                group.add(_row("Actualizaciones", hint))
            for line in result.get("blame") or []:
                match = _BLAME_RE.match(line.strip())
                if match:
                    blame_g.add(_row(match.group(2).removesuffix(".service"), "", _value(match.group(1))))
                else:
                    blame_g.add(_row(line.strip()))
            if not result.get("blame"):
                _placeholder(blame_g, "Sin datos")

        _bg(repair.report, done)
        return False

    # ── Prueba de rendimiento ─────────────────────────────────────────────

    def _page_bench(self) -> Gtk.Widget:
        group = _group(description="Mide CPU y disco durante unos segundos. No modifica nada.")
        self._bench_btn = _button("Ejecutar", self._on_bench, primary=True)
        group.add(_row("Prueba rápida", "Usa un archivo temporal de 32 MB.", self._bench_btn))
        self._bench_vals: dict[str, Gtk.Label] = {}
        results = _group("Resultados")
        for key, title in (("score", "Puntuación"), ("cpu", "Procesador"),
                           ("write", "Escritura en disco"), ("read", "Lectura de disco")):
            self._bench_vals[key] = _value("—")
            results.add(_row(title, "", self._bench_vals[key]))
        return self._page(group, results, title="Prueba de rendimiento")

    def _on_bench(self, button: Gtk.Button) -> None:
        button.set_sensitive(False)
        button.set_label("Midiendo…")

        def done(result, err):
            button.set_sensitive(True)
            button.set_label("Ejecutar")
            if err:
                _toast(self, str(err))
                return
            cpu = result.get("cpu") or {}
            d = result.get("disk") or {}
            self._bench_vals["score"].set_text(str(result.get("score")))
            self._bench_vals["cpu"].set_text(f"{cpu.get('ops_per_sec', 0):,.0f} op/s".replace(",", " "))
            self._bench_vals["write"].set_text(f"{d.get('write_mbs', 0):.0f} MiB/s")
            self._bench_vals["read"].set_text(f"{d.get('read_mbs', 0):.0f} MiB/s")

        _bg(benchmark.run, done)

    # ── Preferencias ──────────────────────────────────────────────────────

    def _page_prefs(self) -> Gtk.Widget:
        safety = _group("Seguridad")
        maint = _group("Mantenimiento seguro incluye")
        for key, label, sub, grp in (
            ("confirm_destructive", "Confirmar acciones que no se deshacen", "", safety),
            ("optimize_user_cache", "Caché de usuario", "~/.cache", maint),
            ("optimize_thumbnails", "Miniaturas", "Vistas previas de imágenes y vídeos", maint),
            ("optimize_journal", "Registros del sistema", "Conserva los últimos 7 días", maint),
            ("optimize_drop_caches", "Liberar caché del kernel", "Pide contraseña", maint),
            ("optimize_fstrim", "TRIM del SSD", "Pide contraseña", maint),
            ("optimize_empty_trash", "Vaciar papelera", "No recomendado", maint),
        ):
            sw = Gtk.Switch(active=bool(self.settings.get(key)))
            sw.connect("state-set", self._on_pref, key)
            grp.add(_row(label, sub, sw))
        about = _group("Acerca de")
        about.add(_row(__app_name__, f"Versión {__version__}"))
        about.add(_row("RETH Tecnologías", "Lima, Perú · Licencia GPL-3.0"))
        return self._page(safety, maint, about, title="Preferencias")

    def _on_pref(self, switch: Gtk.Switch, state: bool, key: str):
        self.settings[key] = bool(state)
        save_settings(self.settings)
        return False

    # ── Utilidades ────────────────────────────────────────────────────────

    def _run_action(self, fn, success: str, after=None) -> None:
        def done(result, err):
            if err:
                _toast(self, str(err))
                return
            if result and not result.get("ok", True):
                _toast(self, result.get("error") or "La acción no se completó")
                return
            extra = ""
            if result and result.get("freed"):
                extra = f" · {fmt_bytes(result['freed'])}"
            _toast(self, success + extra)
            if after:
                after()
            self._refresh_dashboard()

        _bg(fn, done)


class RethApp(Adw.Application):
    def __init__(self):
        super().__init__(application_id=__app_id__, flags=Gio.ApplicationFlags.FLAGS_NONE)
        self._lock_fd = None

    def do_activate(self):
        win = self.props.active_window
        if win:
            win.present()
            return
        RethWindow(self).present()

    def do_startup(self):
        Adw.Application.do_startup(self)
        Adw.StyleManager.get_default().set_color_scheme(Adw.ColorScheme.FORCE_DARK)
        _css()


def _lock() -> bool:
    ensure_dirs()
    fd = os.open(LOCK_FILE, os.O_CREAT | os.O_RDWR, 0o644)
    try:
        fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        os.close(fd)
        return False
    os.write(fd, str(os.getpid()).encode())
    return True


def main() -> int:
    ensure_dirs()
    settings = load_settings()
    if settings.get("single_instance", True) and not _lock():
        print("RETH Optimicer ya está en ejecución.")
        return 0
    app = RethApp()
    return app.run()
