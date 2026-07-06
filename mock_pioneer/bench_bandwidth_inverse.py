#!/usr/bin/env python3
import subprocess
import time
import socket
import math
import csv
import threading
import glob
import os
import sys

# Configuration
DURATION_S = 60
BASELINE_Q = 50
PERIPH_QS = [40, 45, 50, 55]
FOVEA_QS = [5, 10, 15, 20]
LOG_DIR = "logs_inverse"

def send_gaze_updates(stop_event):
    """Send synthetic Lissajous pattern gaze to port 1236."""
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        for _ in range(200):
            try:
                s.connect(("127.0.0.1", 1236))
                break
            except ConnectionRefusedError:
                time.sleep(0.1)
        else:
            print("[Bench] Failed to connect to gaze port.")
            return

        t0 = time.time()
        while not stop_event.is_set():
            t = time.time() - t0
            u = 0.5 + 0.4 * math.sin(t)
            v = 0.5 + 0.4 * math.sin(t * 1.7)
            
            msg = f"$GAZE{int(u*999):03d}{int(v*999):03d}\n".encode("ascii")
            try:
                s.sendall(msg)
            except Exception:
                break
            time.sleep(1.0 / 30.0) # 30 Hz
    finally:
        s.close()

def drain_camera_stream(stop_event):
    """Connect to port 1235 to trigger the server to encode and send frames."""
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        for _ in range(200):
            try:
                s.connect(("127.0.0.1", 1235))
                break
            except ConnectionRefusedError:
                time.sleep(0.1)
        else:
            print("[Bench] Failed to connect to camera port.")
            return

        s.settimeout(1.0)
        while not stop_event.is_set():
            try:
                data = s.recv(65536)
                if not data:
                    break
            except socket.timeout:
                continue
            except Exception:
                break
    finally:
        s.close()

def run_server_and_get_avg_bytes(mode: str, quality_args: list) -> float:
    """Run server for DURATION_S, kill it, and parse the resulting CSV."""
    # Prefer the local virtualenv python if it exists so server subprocess
    # imports match the harness environment (e.g., `cv2` in .venv).
    # Resolve .venv relative to this script's directory (robust to cwd)
    script_dir = os.path.dirname(os.path.abspath(__file__))
    venv_py = os.path.join(script_dir, ".venv", "Scripts", "python.exe")
    python_exec = venv_py if os.path.exists(venv_py) else sys.executable

    # Use a per-iteration log directory so CSVs don't collide between runs
    iteration_start = time.time()
    run_log_dir = os.path.join(LOG_DIR, f"run_{int(iteration_start)}")
    os.makedirs(run_log_dir, exist_ok=True)

    cmd = [python_exec, "-u", "server.py", "--mode", mode, "--log-dir", run_log_dir] + quality_args
    print(f"    [Bench] launching server with interpreter {python_exec}: {' '.join(cmd)}")
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
    
    def print_server_output():
        for line in proc.stdout:
            print(f"    [Server Out] {line.strip()}")
            
    threading.Thread(target=print_server_output, daemon=True).start()
    # Allow server a short warm-up to initialize and bind sockets
    time.sleep(2.5)

    stop_event = threading.Event()
    gaze_thread = threading.Thread(target=send_gaze_updates, args=(stop_event,))
    camera_thread = threading.Thread(target=drain_camera_stream, args=(stop_event,))

    gaze_thread.start()
    camera_thread.start()

    time.sleep(DURATION_S)
    
    stop_event.set()
    proc.terminate()
    proc.wait()
    gaze_thread.join()
    camera_thread.join()

    csv_files = glob.glob(os.path.join(run_log_dir, "*.csv"))
    if not csv_files:
        print(f"[Bench] Error: No CSV generated in {run_log_dir}!")
        return 0.0, None

    csv_file = sorted(csv_files)[-1]
    total_bytes = 0
    frame_count = 0
    with open(csv_file, "r") as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row["event"] == "frame_sent":
                total_bytes += int(row["bytes_total"])
                frame_count += 1
                
    if frame_count == 0:
        return 0.0, csv_file
    return total_bytes / frame_count, csv_file

def main():
    if not os.path.exists(LOG_DIR):
        os.makedirs(LOG_DIR)

    print(f"=== Phase 3: Bandwidth Calibration Sweep ===")
    print(f"Duration per run: {DURATION_S}s. Total runs: {1 + len(PERIPH_QS) * len(FOVEA_QS)}.")
    print("This will take approximately 21 minutes.\n")
    
    print("--- Measuring Uniform Baseline ---")
    baseline_bytes, baseline_csv = run_server_and_get_avg_bytes("uniform", ["--quality", str(BASELINE_Q)])
    print(f"Baseline Uniform Q{BASELINE_Q}: {baseline_bytes:.1f} bytes/frame (from {baseline_csv})\n")

    if baseline_bytes == 0:
        print("Failed to get baseline. Aborting.")
        return

    print("--- Sweeping Gaze Mode ---")
    results = []
    total_runs = len(PERIPH_QS) * len(FOVEA_QS)
    current_run = 0
    for pq in PERIPH_QS:
        for fq in FOVEA_QS:
            current_run += 1
            print(f"Run {current_run}/{total_runs}: periph={pq}, fovea={fq} ... ", end="", flush=True)
            args = ["--periph-quality", str(pq), "--fovea-quality", str(fq)]
            print(f"[Sweep] periph_q={pq}, fovea_q={fq}")
            avg_bytes, csv_file = run_server_and_get_avg_bytes("gaze", args)

            if avg_bytes == 0:
                print("FAILED")
                continue

            delta_pct = ((avg_bytes - baseline_bytes) / baseline_bytes) * 100.0
            print(f"{avg_bytes:.1f} bytes ({delta_pct:+.1f}%)  (csv: {csv_file})")
            
            results.append({
                "periph_q": pq,
                "fovea_q": fq,
                "avg_bytes": avg_bytes,
                "delta_pct": delta_pct,
                "abs_delta_pct": abs(delta_pct)
            })

    if not results:
        return

    results.sort(key=lambda x: x["abs_delta_pct"])
    
    print("\n--- Results Sorted by Proximity to Baseline ---")
    print(f"Baseline (Uniform Q{BASELINE_Q}): {baseline_bytes:.1f} bytes/frame")
    print(f"{'Periph Q':<10} | {'Fovea Q':<10} | {'Avg Bytes':<12} | {'Delta %':<10}")
    print("-" * 50)
    for i, r in enumerate(results):
        marker = " <== CLOSEST MATCH" if i == 0 else ""
        print(f"{r['periph_q']:<10} | {r['fovea_q']:<10} | {r['avg_bytes']:<12.1f} | {r['delta_pct']:>+6.1f}%{marker}")

    out_csv = "bench_results_inverse.csv"
    with open(out_csv, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=["periph_q", "fovea_q", "avg_bytes", "delta_pct", "abs_delta_pct"])
        writer.writeheader()
        writer.writerows(results)
    
    print(f"\nFull results saved to {out_csv}")
    print("\n--- Sanity Check: Monotonicity ---")
    violations = 0
    for fq in FOVEA_QS:
        prev_bytes = -1
        prev_pq = -1
        for pq in sorted(PERIPH_QS):
            r = next((x for x in results if x["periph_q"] == pq and x["fovea_q"] == fq), None)
            if r is None: continue
            if prev_bytes != -1 and r["avg_bytes"] < prev_bytes:
                print(f"VIOLATION: fovea_q={fq}, periph_q={prev_pq} ({prev_bytes:.1f} B) > periph_q={pq} ({r['avg_bytes']:.1f} B)")
                violations += 1
            prev_bytes = r["avg_bytes"]
            prev_pq = pq
    if violations == 0:
        print("PASS: Bytes increase monotonically with periph_q at fixed fovea_q.")
    else:
        print(f"FAIL: {violations} monotonicity violations found.")
    
    print("\nNext step: Pick the 'CLOSEST MATCH' preset for Phase 4!")

if __name__ == "__main__":
    main()
