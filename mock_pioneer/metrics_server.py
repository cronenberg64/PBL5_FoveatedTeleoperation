"""
Server-side session metrics logger.

Opens a CSV file in mock_pioneer/logs/ at construction time and appends one
row per event.  Call close() (or use as a context manager) when the session ends.
"""

import csv
import os
import time
from pathlib import Path
from typing import Optional


class MetricsServer:
    """Thread-safe CSV logger for server events."""

    COLUMNS = ["t", "event", "bytes_out", "cmd_received", "gaze_uv", "quality_mode"]

    def __init__(self, log_dir: str = "logs") -> None:
        Path(log_dir).mkdir(parents=True, exist_ok=True)
        ts = time.strftime("%Y%m%d_%H%M%S")
        self._path = os.path.join(log_dir, f"server_session_{ts}.csv")
        self._file = open(self._path, "w", newline="", buffering=1)  # line-buffered
        self._writer = csv.DictWriter(self._file, fieldnames=self.COLUMNS)
        self._writer.writeheader()
        print(f"[Metrics] Logging to {self._path}")

    # ── Public API ──────────────────────────────────────────────────────────

    def log(
        self,
        event: str,
        bytes_out: int = 0,
        cmd_received: str = "",
        gaze_uv: str = "",
        quality_mode: str = "",
    ) -> None:
        row = {
            "t": f"{time.time():.4f}",
            "event": event,
            "bytes_out": bytes_out,
            "cmd_received": cmd_received,
            "gaze_uv": gaze_uv,
            "quality_mode": quality_mode,
        }
        try:
            self._writer.writerow(row)
        except Exception as exc:
            print(f"[Metrics] Write error: {exc}")

    def close(self) -> None:
        try:
            self._file.flush()
            self._file.close()
        except Exception:
            pass

    # ── Context manager ─────────────────────────────────────────────────────

    def __enter__(self) -> "MetricsServer":
        return self

    def __exit__(self, *_) -> None:
        self.close()
