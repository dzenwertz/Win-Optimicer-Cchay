"""Regression tests using temporary files and mocked system commands only."""
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
from reth_optimicer.modules import disk, optimize

loader = importlib.machinery.SourceFileLoader("helper", str(ROOT / "helper/reth-optimicer-helper"))
spec = importlib.util.spec_from_loader(loader.name, loader)
helper = importlib.util.module_from_spec(spec)
loader.exec_module(helper)


class SafetyTests(unittest.TestCase):
    def test_scan_and_clean_counts_actual_bytes(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "nested").mkdir()
            (root / "nested/file").write_bytes(b"test")
            self.assertEqual(disk.dir_size(root), 4)
            self.assertEqual(disk._clear_dir(root), 4)
            self.assertEqual(list(root.iterdir()), [])

    def test_failed_delete_not_counted(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / "nested").mkdir()
            (root / "nested/file").write_bytes(b"test")
            with patch.object(disk.shutil, "rmtree", side_effect=PermissionError):
                self.assertEqual(disk._clear_dir(root), 0)
            self.assertTrue((root / "nested/file").exists())

    def test_symlink_root_is_not_cleaned(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            target = root / "target"
            target.mkdir()
            (target / "keep").write_bytes(b"test")
            link = root / "link"
            try:
                link.symlink_to(target, target_is_directory=True)
            except OSError:
                self.skipTest("Symbolic links require privileges on this host")
            self.assertEqual(disk._clear_dir(link), 0)
            self.assertTrue((target / "keep").exists())

    def test_privileged_failure_is_reported(self):
        with patch.object(disk, "clean_target", return_value={"ok": True}), patch.object(optimize.memory, "reclaim_ram", return_value={"ok": False, "error": "denied"}):
            result = optimize.run_safe(settings={"optimize_drop_caches": True})
        self.assertFalse(result["ok"])

    def test_exception_does_not_abort_remaining_steps(self):
        with patch.object(disk, "clean_target", side_effect=[OSError("denied"), {"ok": True}, {"ok": True}]):
            result = optimize.run_safe(settings={})
        self.assertFalse(result["ok"])
        self.assertEqual(len(result["steps"]), 3)

    def test_dns_command_failure_is_reported(self):
        outputs = [subprocess.CompletedProcess([], 0, "Link 2 (eth0)\n", ""), subprocess.CompletedProcess([], 1, "", "denied")]
        with patch.object(helper.Path, "exists", return_value=True), patch.object(helper, "_run", side_effect=outputs):
            self.assertFalse(helper.set_dns({"provider": "cloudflare"})["ok"])

    def test_dns_without_resolvectl_does_not_overwrite_configuration(self):
        with patch.object(helper.Path, "exists", return_value=False), patch.object(helper.Path, "write_text") as write:
            self.assertFalse(helper.set_dns({"provider": "cloudflare"})["ok"])
            write.assert_not_called()


if __name__ == "__main__":
    unittest.main()
