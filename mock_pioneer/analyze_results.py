#!/usr/bin/env python3
import csv
import os

MASTER_LOG = "trial_results.csv"

def main():
    if not os.path.exists(MASTER_LOG):
        print(f"Error: {MASTER_LOG} does not exist. Run trials first!")
        return

    trials = []
    with open(MASTER_LOG, "r") as f:
        reader = csv.DictReader(f)
        for row in reader:
            trials.append(row)

    if not trials:
        print(f"Error: No trials found in {MASTER_LOG}.")
        return

    print("\n=== Driving Task Trials Analysis ===")
    print(f"{'Subject':<10} | {'Trial':<5} | {'Condition':<25} | {'Avg Bytes/Frame':<15} | {'Frames Sent':<12} | {'Latency (ms)':<12}")
    print("-" * 90)

    for t in trials:
        print(f"{t['Subject']:<10} | {t['TrialNumber']:<5} | {t['Condition']:<25} | {float(t['AvgBytes']):<15.1f} | {t['FramesSent']:<12} | {t['LatencyAvgMs']:<12}")

    # Calculate statistics grouped by condition
    stats = {}
    for t in trials:
        cond = t["Condition"]
        if cond not in stats:
            stats[cond] = {"bytes": [], "frames": [], "latency": []}
        stats[cond]["bytes"].append(float(t["AvgBytes"]))
        stats[cond]["frames"].append(int(t["FramesSent"]))
        stats[cond]["latency"].append(float(t["LatencyAvgMs"]))

    print("\n=== Summary by Condition ===")
    print(f"{'Condition':<25} | {'Avg Bytes/Frame':<18} | {'Avg Frames Sent':<16} | {'Avg Latency (ms)':<16}")
    print("-" * 85)
    for cond, data in stats.items():
        avg_b = sum(data["bytes"]) / len(data["bytes"])
        avg_f = sum(data["frames"]) / len(data["frames"])
        avg_l = sum(data["latency"]) / len(data["latency"])
        print(f"{cond:<25} | {avg_b:<18.1f} | {avg_f:<16.1f} | {avg_l:<16.2f}")

    # Write a markdown report
    report_path = "trial_report.md"
    with open(report_path, "w") as f:
        f.write("# Teleoperation Experiment Trial Report\n\n")
        f.write("## Trial Log Summary\n\n")
        f.write("| Subject | Trial | Condition | Avg Bytes/Frame | Frames Sent | Latency (ms) |\n")
        f.write("|---------|-------|-----------|-----------------|-------------|--------------|\n")
        for t in trials:
            f.write(f"| {t['Subject']} | {t['TrialNumber']} | {t['Condition']} | {float(t['AvgBytes']):.1f} | {t['FramesSent']} | {t['LatencyAvgMs']} |\n")
        
        f.write("\n## Aggregated Results\n\n")
        f.write("| Condition | Avg Bytes/Frame | Avg Frames Sent | Avg Latency (ms) |\n")
        f.write("|-----------|-----------------|-----------------|------------------|\n")
        for cond, data in stats.items():
            avg_b = sum(data["bytes"]) / len(data["bytes"])
            avg_f = sum(data["frames"]) / len(data["frames"])
            avg_l = sum(data["latency"]) / len(data["latency"])
            f.write(f"| {cond} | {avg_b:.1f} | {avg_f:.1f} | {avg_l:.2f} |\n")

    print(f"\nMarkdown report generated at: {os.path.abspath(report_path)}")

if __name__ == "__main__":
    main()
