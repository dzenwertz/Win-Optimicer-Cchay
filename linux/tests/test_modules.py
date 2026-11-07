import os
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))

from reth_optimicer.modules import benchmark, disk, memory, system_info, tweaks  # noqa: E402
from reth_optimicer.paths import fmt_bytes  # noqa: E402


class SystemInfoTests(unittest.TestCase):
    def test_collect_keys(self):
        info = system_info.collect()
        for key in ("distro", "kernel", "cpu", "ram_total", "health", "disks"):
            self.assertIn(key, info)
        self.assertGreater(info["ram_total"], 0)
        self.assertGreaterEqual(info["health"], 0)
        self.assertLessEqual(info["health"], 100)


class DiskTests(unittest.TestCase):
    def test_targets_not_empty(self):
        items = disk.scan_targets()
        self.assertTrue(items)
        self.assertTrue(any(i["id"] == "user_cache" for i in items))


class TweakTests(unittest.TestCase):
    def test_list(self):
        items = tweaks.list_tweaks()
        self.assertTrue(items)
        self.assertTrue(any(i["kind"] == "sysctl" for i in items))


class BenchTests(unittest.TestCase):
    def test_score_range(self):
        res = benchmark.run(cpu_seconds=0.05, disk_mb=1)
        self.assertIn("score", res)
        self.assertGreaterEqual(res["score"], 0)
        self.assertLessEqual(res["score"], 100000)


class MemoryTests(unittest.TestCase):
    def test_snapshot(self):
        snap = memory.snapshot()
        self.assertGreater(snap["ram_total"], 0)
        self.assertIn("ram_available_h", snap)
        self.assertIn("reclaimable_h", snap)
        self.assertIn(snap["pressure"], {"baja", "media", "alta"})

    def test_grouped_processes(self):
        groups = memory.grouped_processes(5)
        self.assertTrue(groups)
        self.assertIn("rss_h", groups[0])
        self.assertTrue(groups[0]["label"])


class PathTests(unittest.TestCase):
    def test_fmt_bytes(self):
        self.assertEqual(fmt_bytes(0), "0 B")
        self.assertIn("KiB", fmt_bytes(2048))


if __name__ == "__main__":
    unittest.main()
