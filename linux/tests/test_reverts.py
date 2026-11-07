"""Deshacer cambios: registro de originales y acciones de restauración del helper.

Usa carpetas temporales y comandos simulados; no toca el sistema.
"""
import importlib.machinery
import importlib.util
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from reth_optimicer import paths, reverts  # noqa: E402
from reth_optimicer.modules import privacy, tweaks  # noqa: E402

loader = importlib.machinery.SourceFileLoader("helper_rev", str(ROOT / "helper/reth-optimicer-helper"))
spec = importlib.util.spec_from_loader(loader.name, loader)
helper = importlib.util.module_from_spec(spec)
loader.exec_module(helper)

OK = subprocess.CompletedProcess([], 0, "", "")


class TempConfig(unittest.TestCase):
    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self._patch = patch.object(paths, "CONFIG_DIR", Path(self._tmp.name))
        self._patch.start()

    def tearDown(self):
        self._patch.stop()
        self._tmp.cleanup()


class RevertStoreTests(TempConfig):
    def test_first_original_is_kept(self):
        reverts.remember("sysctl", "vm.swappiness", 60)
        reverts.remember("sysctl", "vm.swappiness", 10)
        self.assertEqual(reverts.original("sysctl", "vm.swappiness"), 60)
        reverts.forget("sysctl", "vm.swappiness")
        self.assertFalse(reverts.has("sysctl", "vm.swappiness"))

    def test_apply_then_restore_sysctl(self):
        calls = []

        def fake_run(action, **params):
            calls.append((action, params))
            return {"ok": True}

        with patch.object(tweaks, "read_sysctl", return_value=100), patch.object(tweaks.privileged, "run", fake_run):
            tweaks.apply_sysctl("vm.vfs_cache_pressure", 50)
            self.assertEqual(reverts.original("sysctl", "vm.vfs_cache_pressure"), 100)
            tweaks.restore_sysctl("vm.vfs_cache_pressure")
        self.assertEqual(calls[-1], ("sysctl_restore", {"key": "vm.vfs_cache_pressure", "value": 100}))
        self.assertFalse(reverts.has("sysctl", "vm.vfs_cache_pressure"))

    def test_failed_apply_records_nothing(self):
        with patch.object(tweaks, "read_sysctl", return_value=100), \
                patch.object(tweaks.privileged, "run", return_value={"ok": False, "error": "x"}):
            tweaks.apply_sysctl("vm.vfs_cache_pressure", 50)
        self.assertFalse(reverts.has("sysctl", "vm.vfs_cache_pressure"))

    def test_privacy_restore_only_if_reth_disabled_it(self):
        self.assertFalse(privacy.restore("whoopsie")["ok"])
        with patch.object(privacy.privileged, "run", return_value={"ok": True}):
            privacy.disable("whoopsie")
            self.assertTrue(privacy.restore("whoopsie")["ok"])
        self.assertFalse(reverts.has("privacy", "whoopsie"))


class HelperRestoreTests(unittest.TestCase):
    def test_sysctl_restore_removes_key_and_empty_dropin(self):
        with tempfile.TemporaryDirectory() as folder:
            dropin = Path(folder) / "99-reth-optimicer.conf"
            dropin.write_text("vm.swappiness=10\n")
            with patch.object(helper, "SYSCTL_DROPIN", dropin), patch.object(helper, "_run", return_value=OK):
                self.assertTrue(helper.sysctl_restore({"key": "vm.swappiness", "value": 60})["ok"])
            self.assertFalse(dropin.exists())

    def test_sysctl_restore_keeps_other_keys(self):
        with tempfile.TemporaryDirectory() as folder:
            dropin = Path(folder) / "99-reth-optimicer.conf"
            dropin.write_text("vm.swappiness=10\nvm.vfs_cache_pressure=50\n")
            with patch.object(helper, "SYSCTL_DROPIN", dropin), patch.object(helper, "_run", return_value=OK):
                helper.sysctl_restore({"key": "vm.swappiness", "value": 60})
            self.assertEqual(dropin.read_text(), "vm.vfs_cache_pressure=50\n")

    def test_sysctl_ranges_per_key(self):
        self.assertFalse(helper.sysctl_set({"key": "vm.dirty_ratio", "value": 150})["ok"])
        self.assertFalse(helper.sysctl_set({"key": "kernel.panic", "value": 1})["ok"])
        with tempfile.TemporaryDirectory() as folder:
            with patch.object(helper, "SYSCTL_DROPIN", Path(folder) / "d.conf"), \
                    patch.object(helper, "_run", return_value=OK):
                self.assertTrue(helper.sysctl_set({"key": "vm.vfs_cache_pressure", "value": 200})["ok"])

    def test_apport_toggle_preserves_other_lines(self):
        with tempfile.TemporaryDirectory() as folder:
            conf = Path(folder) / "apport"
            conf.write_text("# comentario\nenabled=1\nOTRA=valor\n")
            helper._set_conf_line(conf, "enabled", "enabled=0")
            self.assertEqual(conf.read_text(), "# comentario\nenabled=0\nOTRA=valor\n")


if __name__ == "__main__":
    unittest.main()
