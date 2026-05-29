#!/usr/bin/env python3
import subprocess
import time
import socket
import struct
import math
import csv
import threading
import glob
import os
import sys
import signal

# Configuration
DURATION_S = 60
LOG_DIR = "logs"

CONDITIONS = [
    {
        "name": "Uniform Q50",
        "mode": "uniform",
        "flags": ["--quality", "50"]
    },
    {
        "name": "Foveated (15,85)",
        "mode": "gaze",
        "flags": ["--periph-quality", "15", "--fovea-quality", "85"]
    },
    {
        "name": "PeripheralOnly Q30",
        "mode": "periph",
        "flags": ["--periph-quality", "30"]
    }
]

LOSS_RATES = [0, 2, 5, 10, 20]

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
            print("[Client] Failed to connect to gaze port.")
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

def drain_camera_stream(stop_event, recv_stats):
    """
    Connect to port 1235, read length-prefixed frames,
    and record arrival times and payload sizes.
    """
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    try:
        for _ in range(200):
            try:
                s.connect(("127.0.0.1", 1235))
                break
            except ConnectionRefusedError:
                time.sleep(0.1)
        else:
            print("[Client] Failed to connect to camera port.")
            return

        s.settimeout(1.0)
        while not stop_event.is_set():
            try:
                # 1. Read 4-byte length prefix
                len_buf = b""
                while len(len_buf) < 4:
                    chunk = s.recv(4 - len(len_buf))
                    if not chunk:
                        return
                    len_buf += chunk
                
                length = struct.unpack(">I", len_buf)[0]
                
                # 2. Read raw JPEG/dual-payload bytes
                payload = b""
                while len(payload) < length:
                    chunk = s.recv(min(length - len(payload), 65536))
                    if not chunk:
                        break
                    payload += chunk
                if len(payload) < length:
                    break
                
                # Record arrival time and payload size
                recv_stats.append((time.time(), len(payload)))
                
            except socket.timeout:
                continue
            except Exception:
                break
    finally:
        s.close()

def run_iteration(condition, loss_rate, duration_s):
    print(f"\n--- Running: {condition['name']} | Loss: {loss_rate}% ---")
    script_dir = os.path.dirname(os.path.abspath(__file__))
    venv_py = os.path.join(script_dir, ".venv", "bin", "python")
    if not os.path.exists(venv_py):
        venv_py = os.path.join(script_dir, ".venv", "Scripts", "python.exe")
    python_exec = venv_py if os.path.exists(venv_py) else sys.executable

    iteration_start = time.time()
    run_log_dir = os.path.join(script_dir, LOG_DIR, f"experiment_{int(iteration_start)}")
    os.makedirs(run_log_dir, exist_ok=True)

    cmd = [
        python_exec, "-u", os.path.join(script_dir, "server.py"),
        "--camera-source", "webcam",
        "--mode", condition["mode"],
        "--loss", str(loss_rate),
        "--log-dir", run_log_dir
    ] + condition["flags"]

    print(f"[Experiment] Launching server: {' '.join(cmd)}")
    proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
    
    def print_server_output():
        for line in proc.stdout:
            # Silently consume server output
            pass
            
    threading.Thread(target=print_server_output, daemon=True).start()
    
    # Wait 2.5s for warm-up
    time.sleep(2.5)

    stop_event = threading.Event()
    recv_stats = [] # list of (arrival_time, size)
    
    gaze_thread = threading.Thread(target=send_gaze_updates, args=(stop_event,))
    camera_thread = threading.Thread(target=drain_camera_stream, args=(stop_event, recv_stats))

    gaze_thread.start()
    camera_thread.start()

    time.sleep(duration_s)

    stop_event.set()
    
    # SIGINT the server to allow clean shutdown and CSV writing
    try:
        proc.send_signal(signal.SIGINT)
    except Exception:
        proc.terminate()
        
    proc.wait()
    gaze_thread.join()
    camera_thread.join()

    # Find the CSV file
    csv_files = glob.glob(os.path.join(run_log_dir, "*.csv"))
    if not csv_files:
        print(f"[Experiment] Error: No CSV generated in {run_log_dir}!")
        return {
            "condition": condition["name"],
            "loss_rate_set_pct": loss_rate,
            "frames_sent": 0,
            "frames_received": 0,
            "frames_dropped": 0,
            "drop_rate_observed_pct": 0.0,
            "avg_bytes_per_frame": 0.0,
            "avg_latency_ms": "FAILED — no frames received"
        }

    csv_file = sorted(csv_files)[-1]
    
    # Parse server CSV
    frames_sent = 0
    total_bytes_sent = 0
    
    with open(csv_file, "r") as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row["event"] == "frame_sent":
                frames_sent += 1
                total_bytes_sent += int(row["bytes_total"])

    frames_received = len(recv_stats)
    avg_bytes = total_bytes_sent / frames_sent if frames_sent > 0 else 0.0

    if frames_received == 0:
        return {
            "condition": condition["name"],
            "loss_rate_set_pct": loss_rate,
            "frames_sent": frames_sent,
            "frames_received": 0,
            "frames_dropped": frames_sent,
            "drop_rate_observed_pct": 100.0,
            "avg_bytes_per_frame": avg_bytes,
            "avg_latency_ms": "FAILED — no frames received"
        }

    frames_dropped = frames_sent - frames_received
    drop_rate_observed = (frames_dropped / frames_sent) * 100.0 if frames_sent > 0 else 0.0

    # Calculate average latency from arrival timestamp deltas
    arrival_times = [stat[0] for stat in recv_stats]
    deltas = [(arrival_times[i] - arrival_times[i-1]) * 1000.0 for i in range(1, len(arrival_times))]
    avg_latency = sum(deltas) / len(deltas) if deltas else 0.0

    return {
        "condition": condition["name"],
        "loss_rate_set_pct": loss_rate,
        "frames_sent": frames_sent,
        "frames_received": frames_received,
        "frames_dropped": frames_dropped,
        "drop_rate_observed_pct": round(drop_rate_observed, 3),
        "avg_bytes_per_frame": round(avg_bytes, 2),
        "avg_latency_ms": round(avg_latency, 2)
    }

def main():
    duration = DURATION_S
    # Allow overriding duration for quick test
    if len(sys.argv) > 1 and sys.argv[1] == "--test-short":
        duration = 5
        print(f"[Experiment] Running in SHORT TEST MODE (duration={duration}s)")

    results = []
    total_runs = len(CONDITIONS) * len(LOSS_RATES)
    run_idx = 0

    for cond in CONDITIONS:
        for loss in LOSS_RATES:
            run_idx += 1
            print(f"\nProgress: Run {run_idx}/{total_runs}")
            res = run_iteration(cond, loss, duration)
            results.append(res)

    # Sort results by condition, then loss rate
    results.sort(key=lambda x: (x["condition"], x["loss_rate_set_pct"]))

    # Print summary table
    print("\n" + "="*80)
    print(" EXPERIMENT SUMMARY TABLE")
    print("="*80)
    header_fmt = "{:<20} | {:<12} | {:<12} | {:<12} | {:<15} | {:<12} | {:<12}"
    row_fmt = "{:<20} | {:<12} | {:<12} | {:<12} | {:<15} | {:<12} | {:<12}"
    print(header_fmt.format("Condition", "Loss Set %", "Sent", "Received", "Dropped", "Observed Drop%", "Avg Latency"))
    print("-"*105)
    for r in results:
        # Format latency differently if failed
        lat_str = r["avg_latency_ms"]
        if isinstance(lat_str, float):
            lat_str = f"{lat_str:.2f} ms"
        print(row_fmt.format(
            r["condition"],
            f"{r['loss_rate_set_pct']}%",
            r["frames_sent"],
            r["frames_received"],
            r["frames_dropped"],
            f"{r['drop_rate_observed_pct']:.2f}%",
            lat_str
        ))

    # Write to CSV
    script_dir = os.path.dirname(os.path.abspath(__file__))
    out_csv = os.path.join(script_dir, "loss_experiment_results.csv")
    with open(out_csv, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=[
            "condition", "loss_rate_set_pct", "frames_sent", "frames_received",
            "frames_dropped", "drop_rate_observed_pct", "avg_bytes_per_frame", "avg_latency_ms"
        ])
        writer.writeheader()
        writer.writerows(results)

    print(f"\n[Experiment] All runs complete. Results written to {out_csv}")

if __name__ == "__main__":
    main()
