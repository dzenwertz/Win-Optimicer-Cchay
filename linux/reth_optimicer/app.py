"""Interfaz GTK 4 / libadwaita de RETH Optimicer."""
from __future__ import annotations

import fcntl
import os
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
    system_info,
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

PAGES = [
    ("inicio", "Inicio"),
    ("memoria", "Memoria"),
    ("disco", "Disco"),
    ("ajustes", "Ajustes"),
    ("servicios", "Servicios"),
    ("autostart", "Inicio automático"),
    ("red", "Red"),
    ("paquetes", "Paquetes"),
    ("privacidad", "Privacidad"),
    ("reparar", "Reparar"),
    ("benchmark", "Benchmark"),
    ("preferencias", "Preferencias"),
]


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


def _confirm(parent: Gtk.Window, title: str, body: str, callback) -> None:
    dialog = Adw.MessageDialog.new(parent, title, body)
    dialog.add_response("cancel", "Cancelar")
    dialog.add_response("ok", "Continuar")
    dialog.set_response_appearance("ok", Adw.ResponseAppearance.DESTRUCTIVE)
    dialog.connect("response", lambda d, resp: callback() if resp == "ok" else None)
    dialog.present()


def _busy(widget: Gtk.Widget, running: bool) -> None:
    widget.set_sensitive(not running)


def _bg(fn, done):
    def worker():
        try:
            result = fn()
            err = None
        except Exception as exc:  # noqa: BLE001
            result, err = None, exc
        GLib.idle_add(done, result, err)

    threading.Thread(target=worker, daemon=True).start()


class RethWindow(Adw.ApplicationWindow):
    def __init__(self, app: Adw.Application):
        super().__init__(application=app, title=__app_name__)
        self.set_default_size(1100, 720)
        self.settings = load_settings()
        self._pages: dict[str, Gtk.Widget] = {}
        self._health_label = Gtk.Label(label="—")
        self._health_label.add_css_class("score")
        self._status = Gtk.Label(label="Cargando…")
        self._status.add_css_class("muted")
        self._status.set_wrap(True)
        self._status.set_xalign(0)
        self._ram_bar = Gtk.ProgressBar()
        self._disk_bar = Gtk.ProgressBar()
        self._info_labels: dict[str, Gtk.Label] = {}

        overlay = Adw.ToastOverlay()
        self._toast_overlay = overlay

        split = Adw.NavigationSplitView()
        split.set_min_sidebar_width(240)
        split.set_sidebar(self._build_sidebar())
        split.set_content(self._build_content())
        overlay.set_child(split)
        self.set_content(overlay)
        self._list.connect("row-selected", self._on_nav)
        self._list.select_row(self._list.get_row_at_index(0))

        GLib.timeout_add_seconds(4, self._refresh_dashboard)
        GLib.idle_add(self._refresh_dashboard)

    def _build_sidebar(self) -> Adw.NavigationPage:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        box.set_margin_top(16)
        box.set_margin_bottom(16)
        box.set_margin_start(12)
        box.set_margin_end(12)
        box.add_css_class("sidebar")

        brand = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10)
        logo = Gtk.Image()
        icon = asset("bird.png")
        if icon:
            logo.set_from_file(str(icon))
            logo.set_pixel_size(28)
        title_col = Gtk.Box(orientation=Gtk.Orientation.VERTICAL)
        name = Gtk.Label(label="RETH", xalign=0)
        name.add_css_class("brand-title")
        sub = Gtk.Label(label="Optimicer", xalign=0)
        sub.add_css_class("muted")
        title_col.append(name)
        title_col.append(sub)
        brand.append(logo)
        brand.append(title_col)
        box.append(brand)

        self._list = Gtk.ListBox()
        self._list.set_selection_mode(Gtk.SelectionMode.SINGLE)
        self._list.add_css_class("navigation-sidebar")
        for key, title in PAGES:
            row = Gtk.ListBoxRow()
            row.set_child(Gtk.Label(label=title, xalign=0, margin_top=8, margin_bottom=8, margin_start=8))
            row._page_id = key  # type: ignore[attr-defined]
            self._list.append(row)
        box.append(self._list)

        page = Adw.NavigationPage(title="RETH")
        page.set_child(box)
        return page

    def _build_content(self) -> Adw.NavigationPage:
        self._stack = Gtk.Stack()
        self._stack.set_hexpand(True)
        self._stack.set_vexpand(True)
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
        for key, _title in PAGES:
            scroller = Gtk.ScrolledWindow()
            scroller.set_child(builders[key]())
            self._stack.add_named(scroller, key)
        nav = Adw.NavigationPage(title=__app_name__)
        nav.set_child(self._stack)
        return nav

    def _on_nav(self, _list, row):
        if row is None or not hasattr(self, "_stack"):
            return
        self._stack.set_visible_child_name(row._page_id)

    def _card(self, title: str, *children: Gtk.Widget) -> Gtk.Box:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        box.add_css_class("card")
        box.set_margin_bottom(12)
        heading = Gtk.Label(label=title, xalign=0)
        heading.add_css_class("title-3")
        box.append(heading)
        for child in children:
            box.append(child)
        return box

    def _wrap(self, *cards: Gtk.Widget) -> Gtk.Box:
        root = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        root.set_margin_top(20)
        root.set_margin_bottom(28)
        root.set_margin_start(24)
        root.set_margin_end(24)
        for card in cards:
            root.append(card)
        return root

    def _page_home(self) -> Gtk.Widget:
        self._opt_btn = Gtk.Button(label="Optimizar")
        self._opt_btn.add_css_class("suggested-action")
        self._opt_btn.connect("clicked", self._on_optimize)
        info_grid = Gtk.Grid(column_spacing=18, row_spacing=8)
        for i, key in enumerate(("distro", "kernel", "cpu", "uptime")):
            cap = Gtk.Label(label=key.capitalize(), xalign=0)
            cap.add_css_class("muted")
            val = Gtk.Label(label="—", xalign=0)
            val.set_wrap(True)
            self._info_labels[key] = val
            info_grid.attach(cap, 0, i, 1, 1)
            info_grid.attach(val, 1, i, 1, 1)
        return self._wrap(
            self._card("Estado del sistema", self._health_label, self._status),
            self._card("Recursos", Gtk.Label(label="RAM", xalign=0), self._ram_bar, Gtk.Label(label="Disco", xalign=0), self._disk_bar),
            self._card("Equipo", info_grid),
            self._card("Mantenimiento seguro", Gtk.Label(label="Limpia cachés de usuario, compacta el diario y, con tu autorización, libera cachés del kernel y ejecuta TRIM.", wrap=True, xalign=0), self._opt_btn),
        )

    def _refresh_dashboard(self) -> bool:
        info = system_info.collect()
        self._health_label.set_text(str(info["health"]))
        self._status.set_text(
            f"{info['distro']} · {info['ram_used_h']} / {info['ram_total_h']} RAM · CPU {info['cpu_percent']:.0f}%"
        )
        self._ram_bar.set_fraction(min(1.0, info["ram_percent"] / 100))
        disks = info.get("disks") or []
        if disks:
            used = disks[0].get("percent", 0) / 100
            self._disk_bar.set_fraction(min(1.0, used))
        self._info_labels["distro"].set_text(info["distro"])
        self._info_labels["kernel"].set_text(info["kernel"])
        self._info_labels["cpu"].set_text(info["cpu"])
        self._info_labels["uptime"].set_text(info["uptime"])
        return True

    def _on_optimize(self, button: Gtk.Button) -> None:
        _busy(button, True)
        self._status.set_text("Optimizando…")

        def work():
            return optimize.run_safe(settings=self.settings)

        def done(result, err):
            _busy(button, False)
            if err:
                _toast(self, str(err))
                return
            freed = fmt_bytes(result.get("freed") or 0)
            _toast(self, f"Optimización lista. Liberado {freed}.")
            self._refresh_dashboard()

        _bg(work, done)

    def _page_memory(self) -> Gtk.Widget:
        self._mem_list = Gtk.Label(label="", xalign=0)
        self._mem_list.set_selectable(True)
        drop = Gtk.Button(label="Liberar cachés de página")
        drop.connect("clicked", lambda *_: self._run_action(memory.drop_caches, "Cachés liberadas."))
        compact = Gtk.Button(label="Compactar memoria")
        compact.connect("clicked", lambda *_: self._run_action(memory.compact_memory, "Memoria compactada."))
        refresh = Gtk.Button(label="Actualizar procesos")
        refresh.connect("clicked", lambda *_: self._fill_mem())
        GLib.idle_add(self._fill_mem)
        return self._wrap(
            self._card("Memoria", drop, compact),
            self._card("Procesos con más RAM", refresh, self._mem_list),
        )

    def _fill_mem(self) -> bool:
        rows = memory.top_processes(12)
        lines = [f"{p['rss_h']:>10}  {p['name']}  pid {p['pid']}" for p in rows]
        self._mem_list.set_text("\n".join(lines) or "Sin datos")
        return False

    def _page_disk(self) -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        self._disk_box = box
        refresh = Gtk.Button(label="Analizar")
        refresh.connect("clicked", lambda *_: self._fill_disk())
        GLib.idle_add(self._fill_disk)
        return self._wrap(self._card("Objetivos de limpieza", refresh, box))

    def _fill_disk(self) -> bool:
        child = self._disk_box.get_first_child()
        while child:
            nxt = child.get_next_sibling()
            self._disk_box.remove(child)
            child = nxt
        for item in disk.scan_targets():
            row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=10)
            text = Gtk.Label(label=f"{item['title']}  ·  {fmt_bytes(item.get('bytes') or 0)}", xalign=0, hexpand=True)
            btn = Gtk.Button(label="Limpiar")
            target_id = item["id"]
            destructive = bool(item.get("destructive"))
            btn.connect("clicked", lambda _b, tid=target_id, dest=destructive: self._clean_disk(tid, dest))
            row.append(text)
            row.append(btn)
            self._disk_box.append(row)
        return False

    def _clean_disk(self, target_id: str, destructive: bool) -> None:
        def go():
            self._run_action(lambda: disk.clean_target(target_id), "Limpieza completada.", after=self._fill_disk)

        if destructive and self.settings.get("confirm_destructive", True):
            _confirm(self, "Vaciar papelera", "Esta acción no se puede deshacer.", go)
        else:
            go()

    def _page_tweaks(self) -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        for spec in tweaks.list_tweaks():
            if spec["kind"] == "fstrim":
                continue
            if spec["kind"] == "sysctl":
                current = spec.get("current")
                rec = spec.get("recommended")
                lbl = Gtk.Label(label=f"{spec['title']}: {current} (recomendado {rec})", xalign=0)
                btn = Gtk.Button(label="Aplicar recomendado")
                key, val = spec["id"], int(rec)
                btn.connect("clicked", lambda _b, k=key, v=val: self._run_action(lambda: tweaks.apply_sysctl(k, v), f"{k} actualizado."))
                box.append(lbl)
                box.append(btn)
            elif spec["kind"] == "governor":
                current = spec.get("current") or "n/d"
                lbl = Gtk.Label(label=f"Gobernador CPU: {current}", xalign=0)
                box.append(lbl)
                for name in spec.get("options") or []:
                    btn = Gtk.Button(label=name)
                    btn.connect("clicked", lambda _b, n=name: self._run_action(lambda: tweaks.apply_governor(n), f"Gobernador {n}."))
                    box.append(btn)
        trim = Gtk.Button(label="Ejecutar TRIM")
        trim.connect("clicked", lambda *_: self._run_action(tweaks.run_fstrim, "TRIM finalizado."))
        box.append(trim)
        return self._wrap(self._card("Kernel y energía", box))

    def _page_services(self) -> Gtk.Widget:
        self._svc_list = Gtk.Label(label="Cargando servicios…", xalign=0)
        self._svc_list.set_wrap(True)
        refresh = Gtk.Button(label="Actualizar")
        refresh.connect("clicked", lambda *_: self._fill_services())
        GLib.idle_add(self._fill_services)
        note = Gtk.Label(label="Las unidades de sistema se activan o desactivan con confirmación y pkexec.", wrap=True, xalign=0)
        return self._wrap(self._card("systemd", note, refresh, self._svc_list))

    def _fill_services(self) -> bool:
        items = services.list_services(40)
        lines = []
        for it in items:
            scope = it.get("scope") or ("usuario" if it.get("user") else "sistema")
            state = "activo" if it.get("active") else "inactivo"
            lines.append(f"{it.get('unit')}  [{scope}]  {state}")
        self._svc_list.set_text("\n".join(lines) or "Sin unidades")
        return False

    def _page_startup(self) -> Gtk.Widget:
        self._start_box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        GLib.idle_add(self._fill_startup)
        return self._wrap(self._card("Autostart", self._start_box))

    def _fill_startup(self) -> bool:
        child = self._start_box.get_first_child()
        while child:
            nxt = child.get_next_sibling()
            self._start_box.remove(child)
            child = nxt
        for entry in startup.list_entries():
            row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
            lbl = Gtk.Label(label=entry.get("name") or entry.get("file"), xalign=0, hexpand=True)
            toggle = Gtk.Switch()
            toggle.set_active(bool(entry.get("enabled")))
            filename = entry.get("file") or entry.get("filename")
            toggle.connect("state-set", self._on_startup_toggle, filename)
            row.append(lbl)
            row.append(toggle)
            self._start_box.append(row)
        if self._start_box.get_first_child() is None:
            self._start_box.append(Gtk.Label(label="No hay entradas en ~/.config/autostart", xalign=0))
        return False

    def _on_startup_toggle(self, switch: Gtk.Switch, state: bool, filename: str):
        res = startup.set_enabled(filename, state)
        if not res.get("ok"):
            _toast(self, res.get("error") or "No se pudo cambiar.")
            switch.set_active(not state)
        return False

    def _page_network(self) -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        for p in network.PROVIDERS:
            ping = ""
            if p["primary"]:
                ms = network.ping(p["primary"])
                ping = f"  ·  {ms:.0f} ms" if ms is not None else ""
            btn = Gtk.Button(label=f"{p['title']}{ping}")
            pid = p["id"]
            btn.connect("clicked", lambda _b, i=pid: self._run_action(lambda: network.set_provider(i), "DNS actualizado."))
            box.append(btn)
        flush = Gtk.Button(label="Vaciar caché DNS")
        flush.connect("clicked", lambda *_: self._run_action(network.flush, "DNS vaciado."))
        current = Gtk.Label(label="\n".join(network.current_dns()) or "Sin datos", xalign=0)
        return self._wrap(self._card("Proveedores", box, flush), self._card("Actual", current))

    def _page_packages(self) -> Gtk.Widget:
        st = packages.status()
        text = "Sin gestor detectado."
        if st.get("available"):
            text = f"{st['title']}\nCaché: {st['cache_h']}\nHuérfanos: {st['orphan_count']}"
        lbl = Gtk.Label(label=text, xalign=0)
        clean = Gtk.Button(label="Limpiar caché")
        clean.connect("clicked", lambda *_: self._run_action(packages.clean_cache, "Caché de paquetes limpia."))
        orphans = Gtk.Button(label="Quitar huérfanos")
        orphans.connect("clicked", lambda *_: _confirm(self, "Quitar huérfanos", "Se eliminarán paquetes no requeridos.", lambda: self._run_action(packages.remove_orphans, "Huérfanos eliminados.")))
        return self._wrap(self._card("Gestor de paquetes", lbl, clean, orphans))

    def _page_privacy(self) -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        for item in privacy.scan():
            state = "presente" if item["present"] else "no instalado"
            extra = " · activo" if item.get("enabled") else ""
            row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
            row.append(Gtk.Label(label=f"{item['title']} ({state}{extra})", xalign=0, hexpand=True))
            if item["present"] and item.get("enabled"):
                btn = Gtk.Button(label="Desactivar")
                iid = item["id"]
                btn.connect("clicked", lambda _b, i=iid: self._run_action(lambda: privacy.disable(i), "Ajuste aplicado."))
                row.append(btn)
            box.append(row)
        return self._wrap(self._card("Recolectores", box))

    def _page_repair(self) -> Gtk.Widget:
        self._repair_lbl = Gtk.Label(label="Analizando…", xalign=0, wrap=True, selectable=True)
        refresh = Gtk.Button(label="Analizar arranque")
        refresh.connect("clicked", lambda *_: self._fill_repair())
        trim = Gtk.Button(label="TRIM")
        trim.connect("clicked", lambda *_: self._run_action(repair.run_fstrim, "TRIM listo."))
        GLib.idle_add(self._fill_repair)
        return self._wrap(self._card("Diagnóstico", refresh, self._repair_lbl, trim))

    def _fill_repair(self) -> bool:
        def work():
            return repair.report()

        def done(result, err):
            if err:
                self._repair_lbl.set_text(str(err))
                return
            analyze = result.get("analyze") or ""
            hint = result.get("updates") or result.get("hint") or ""
            blame = "\n".join(result.get("blame") or [])
            self._repair_lbl.set_text(f"{analyze}\n\n{hint}\n\n{blame}".strip())

        _bg(work, done)
        return False

    def _page_bench(self) -> Gtk.Widget:
        self._bench_lbl = Gtk.Label(label="Pulsa ejecutar para medir CPU y disco.", xalign=0)
        btn = Gtk.Button(label="Ejecutar")
        btn.connect("clicked", self._on_bench)
        return self._wrap(self._card("Prueba rápida", btn, self._bench_lbl))

    def _on_bench(self, button: Gtk.Button) -> None:
        _busy(button, True)
        self._bench_lbl.set_text("Midiendo…")

        def done(result, err):
            _busy(button, False)
            if err:
                self._bench_lbl.set_text(str(err))
                return
            cpu = result.get("cpu") or {}
            disk_r = result.get("disk") or {}
            self._bench_lbl.set_text(
                f"Puntuación {result.get('score')}\n"
                f"CPU {cpu.get('ops_per_sec', 0):.0f} ops/s\n"
                f"Disco escritura {disk_r.get('write_mbs', 0):.0f} MiB/s · "
                f"lectura {disk_r.get('read_mbs', 0):.0f} MiB/s"
            )

        _bg(benchmark.run, done)

    def _page_prefs(self) -> Gtk.Widget:
        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=10)
        for key, label in (
            ("confirm_destructive", "Confirmar acciones destructivas"),
            ("optimize_user_cache", "Incluir caché de usuario"),
            ("optimize_thumbnails", "Incluir miniaturas"),
            ("optimize_journal", "Incluir diario"),
            ("optimize_drop_caches", "Liberar cachés de kernel"),
            ("optimize_fstrim", "Ejecutar TRIM"),
            ("optimize_empty_trash", "Vaciar papelera (no recomendado por defecto)"),
        ):
            row = Gtk.Box(orientation=Gtk.Orientation.HORIZONTAL, spacing=8)
            row.append(Gtk.Label(label=label, xalign=0, hexpand=True))
            sw = Gtk.Switch()
            sw.set_active(bool(self.settings.get(key)))
            sw.connect("state-set", self._on_pref, key)
            row.append(sw)
            box.append(row)
        about = Gtk.Label(label=f"{__app_name__} {__version__}\nRETH Labs · GPL-3.0", xalign=0)
        about.add_css_class("muted")
        box.append(about)
        return self._wrap(self._card("Preferencias", box))

    def _on_pref(self, switch: Gtk.Switch, state: bool, key: str):
        self.settings[key] = bool(state)
        save_settings(self.settings)
        return False

    def _run_action(self, fn, success: str, after=None) -> None:
        def done(result, err):
            if err:
                _toast(self, str(err))
                return
            if result and not result.get("ok", True):
                _toast(self, result.get("error") or "La acción no se completó.")
                return
            extra = ""
            if result and result.get("freed"):
                extra = f" {fmt_bytes(result['freed'])}"
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
