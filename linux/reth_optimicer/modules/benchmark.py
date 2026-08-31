"""Pruebas sencillas de CPU (hash) y disco secuencial."""

from __future__ import annotations

import hashlib
import os
import tempfile
import time


def cpu_bench(duration: float = 1.0) -> dict:
    duration = max(0.05, float(duration))
    digest = hashlib.sha256()
    block = b"RETH-Optimicer" * 64
    n = 0
    start = time.perf_counter()
    end = start + duration
    while time.perf_counter() < end:
        digest.update(block)
        n += 1
    elapsed = max(0.001, time.perf_counter() - start)
    ops = n / elapsed
    return {
        "ops": n,
        "seconds": elapsed,
        "ops_per_sec": ops,
        "score": int(min(10000, ops / 50.0)),
    }


def disk_bench(size_mb: int = 32) -> dict:
    size_mb = max(1, int(size_mb))
    fd, path = tempfile.mkstemp(prefix="reth-bench-", suffix=".bin")
    os.close(fd)
    payload = os.urandom(1024 * 1024)
    try:
        t0 = time.perf_counter()
        with open(path, "wb") as handle:
            for _ in range(size_mb):
                handle.write(payload)
            handle.flush()
            os.fsync(handle.fileno())
        write_s = max(0.001, time.perf_counter() - t0)

        t0 = time.perf_counter()
        with open(path, "rb") as handle:
            while handle.read(1024 * 1024):
                pass
        read_s = max(0.001, time.perf_counter() - t0)
    finally:
        try:
            os.unlink(path)
        except OSError:
            pass

    write_mbs = size_mb / write_s
    read_mbs = size_mb / read_s
    score = int(min(10000, (write_mbs + read_mbs) * 4))
    return {
        "size_mb": size_mb,
        "write_mbs": write_mbs,
        "read_mbs": read_mbs,
        "score": score,
    }


def run(cpu_seconds: float = 1.0, disk_mb: int = 32) -> dict:
    cpu = cpu_bench(cpu_seconds)
    disk = disk_bench(disk_mb)
    score = int(cpu["score"] * 0.55 + disk["score"] * 0.45)
    score = max(0, min(100000, score))
    return {
        "cpu": cpu,
        "disk": disk,
        "score": score,
    }
