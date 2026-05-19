#!/usr/bin/env python3
import subprocess
import time
import csv
import glob
import os
import random
import sys
import threading

# Trial setup
CONDITIONS = [
    {"id": "A", "name": "Uniform Q50", "args": ["--mode", "uniform", "--quality", "50"]},
    {"id": "B", "name": "Foveated [20, 90]", "args": ["--mode", "gaze", "--periph-quality", "20", "--fovea-quality", "90"]},
    {"id": "C", "name": "Peripheral-only Q20", "args": ["--mode", "uniform", "--quality", "20"]}
]

LOG_DIR = "logs"
MASTER_LOG = "trial_results.csv"

def get_latest_csv() -> str:
    csv_files = glob.glob(os.path.join(LOG_DIR, "*.csv"))
    if not csv_files:
        return ""
    return sorted(csv_files)[-1]

def analyze_trial(csv_path: str) -> dict:
    if not csv_path or not os.path.exists(csv_path):
        return {}
    
    total_bytes = 0
    frame_count = 0
    latency_sum = 0.0
    latency_count = 0
    cmd_count = 0
    start_t = None
    end_t = None
    
    with open(csv_path, "r") as f:
        reader = csv.DictReader(f)
        for row in reader:
            t = float(row["t"])
            if start_t is None:
                start_t = t
            end_t = t
            
            event = row["event"]
            if event == "frame_sent":
                total_bytes += int(row["bytes_total"])
                frame_count += 1
                
                try:
                    lat = float(row["delta_ms"])
                    if lat > 0:
                        latency_sum += lat
                        latency_count += 1
                except (ValueError, TypeError):
                    pass
            elif event == "cmd_received":
                cmd_count += 1
                
    duration = (end_t - start_t) if (start_t and end_t) else 0.0
    avg_bytes = (total_bytes / frame_count) if frame_count > 0 else 0.0
    avg_latency = (latency_sum / latency_count) if latency_count > 0 else 0.0
    
    return {
        "AvgBytes": avg_bytes,
        "FramesSent": frame_count,
        "AvgLatencyMs": avg_latency,
        "CmdsReceived": cmd_count,
        "DurationS": duration
    }

def main():
    print("=== Phase 4: Pilot Driving Task Trials (Mouse-Gaze) ===")
    subject_id = input("Enter Subject ID (e.g., S001): ").strip()
    if not subject_id:
        subject_id = "UNKNOWN"
        
    # Shuffle conditions
    trials = list(CONDITIONS)
    random.shuffle(trials)
    
    # Ensure master log exists and matches headers exactly as requested
    # (Subject, TrialNumber, Condition, AvgBytes, FramesSent, PacketLossPct, LatencyAvgMs)
    if not os.path.exists(MASTER_LOG):
        with open(MASTER_LOG, "w", newline="") as f:
            writer = csv.writer(f)
            writer.writerow([
                "Subject", "TrialNumber", "Condition", "AvgBytes", 
                "FramesSent", "PacketLossPct", "LatencyAvgMs"
            ])
            
    print(f"\nSubject: {subject_id}")
    print("Shuffled trial order:")
    for idx, t in enumerate(trials, 1):
        print(f"  Trial {idx}: {t['name']}")
    print("-" * 50)
    
    for idx, trial in enumerate(trials, 1):
        print(f"\n>>> TRIAL {idx}/{len(trials)}: {trial['name']} (Condition {trial['id']})")
        print("Instructions:")
        print("  1. Prepare the Unity scene (DesktopFoveated).")
        print("  2. Ensure the scene is NOT running.")
        print("  3. Press ENTER in this terminal to start the camera server.")
        input("Press ENTER to start the server...")
        
        # Start server
        cmd = [sys.executable, "-u", "server.py"] + trial["args"]
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, bufsize=1)
        
        def print_server_output():
            for line in proc.stdout:
                l = line.strip()
                if "frame_sent" not in l and "gaze_received" not in l:
                    print(f"    [Server] {l}")
        
        threading.Thread(target=print_server_output, daemon=True).start()
        
        # Wait a moment for server to bind
        time.sleep(2.0)
        
        print("\nServer is running! Now:")
        print("  -> Press Play in Unity and complete the driving trial.")
        print("  -> Press ENTER in this terminal ONLY when you have reached the finish line!")
        
        input("\nPress ENTER when the trial is complete...")
        
        # Terminate server
        proc.terminate()
        proc.wait()
        
        # Parse results
        latest_csv = get_latest_csv()
        stats = analyze_trial(latest_csv)
        
        if stats:
            print("\nTrial Results:")
            print(f"  Avg Bytes/Frame: {stats['AvgBytes']:.1f}")
            print(f"  Total Frames Sent: {stats['FramesSent']}")
            print(f"  Avg Latency (Gaze-to-Encode): {stats['AvgLatencyMs']:.2f} ms")
            print(f"  Control Commands Received: {stats['CmdsReceived']}")
            print(f"  Duration: {stats['DurationS']:.1f} s")
            
            # Log to master
            with open(MASTER_LOG, "a", newline="") as f:
                writer = csv.writer(f)
                writer.writerow([
                    subject_id, idx, trial['name'], round(stats['AvgBytes'], 1), 
                    stats['FramesSent'], 0.0, round(stats['AvgLatencyMs'], 2)
                ])
        else:
            print("\nError: Could not retrieve trial stats from log CSV.")
            
        if idx < len(trials):
            print("\nStarting 5-second cooldown before next trial...")
            time.sleep(5)
            
    print("\n=== All Trials Completed! ===")
    print(f"Summary logs saved to {MASTER_LOG}")

if __name__ == "__main__":
    main()
