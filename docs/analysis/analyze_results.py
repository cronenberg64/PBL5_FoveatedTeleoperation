import os
import glob
import json
import re
from pathlib import Path
import pandas as pd
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import seaborn as sns
import scipy.stats as stats
import scikit_posthocs as sp

BASE_DIR = Path(__file__).resolve().parent.parent
STUDY_LOGS_DIR = BASE_DIR / "study_logs"
OUTPUT_DIR = BASE_DIR / "analysis"
EXCEL_FILE = OUTPUT_DIR / "PBL5_Experiment_Spreadsheet-3.xlsx"
FIGURES_DIR = OUTPUT_DIR / "figures"
FIGURES_DIR.mkdir(parents=True, exist_ok=True)

CONDITION_LABELS = {
    "UniformQ50": "Uniform",
    "Foveated_90_5": "Foveated",
    "InverseFoveated_5_90": "Inverse-Foveated",
}
PALETTE = {"Uniform": "#4477AA", "Foveated": "#228833", "Inverse-Foveated": "#CC6677"}
TASKS = ["Navigation", "Identification", "Visual Search"]

def extract_collisions(note):
    if pd.isna(note):
        return 0
    m = re.search(r'(\d+)\s*collision', str(note), re.IGNORECASE)
    if m:
        return int(m.group(1))
    return 0

def extract_read_depth(note, subject_id, row_idx):
    if pd.isna(note):
        print(f"UNPARSED NOTE (row {row_idx}, Subject {subject_id}): nan")
        return np.nan
    s = str(note).lower()
    
    if pd.isna(note) or s == 'nan':
        return np.nan
    
    if re.search(r'not readable|couldn\'t read anything|0 lines', s):
        return 0
        
    m = re.search(r'(?:read\s*(?:the\s*)?|line\s*)([1-5])\b', s)
    if m:
        return int(m.group(1))
        
    m2 = re.search(r'([1-5])(?:st|nd|rd|th)\s*line', s)
    if m2:
        return int(m2.group(1))
    
    # Check for random character string followed by "cannot read anything forward" or "thats it" or "the rest cannot read"
    # Actually, all non-pilot Identification notes consist of the string the person successfully read 
    # e.g., "GTAKV99CEF3GTXKAGRTQK3WJ" or "DWM3YPFK9JW34RYDQ3Y3QWJ6 the rest cannot read"
    # We should score by string length instead.
    
    # We need to clean the string to only count actual letters/numbers entered, not comments
    cleaned = re.sub(r'the rest cannot read|cannot read anything forward|thats it|something|can wait for the blur to go away and see from peripheral|cannotread first', '', s).strip()
    # Remove all spaces
    cleaned = re.sub(r'\s+', '', cleaned)
    
    # Chart logic (based on standard snellen/ETDRS charts for these string lengths):
    # If they read ~5 chars, that's line 1. 10 chars = line 2. 15 chars = line 3. 20 chars = line 4. 25 chars = line 5.
    length = len(cleaned)
    if length >= 24: return 5
    elif length >= 19: return 4
    elif length >= 14: return 3
    elif length >= 9: return 2
    elif length >= 4: return 1
    elif length > 0: return 0 # read something but less than line 1
        
    print(f"UNPARSED NOTE (row {row_idx}, Subject {subject_id}): {note}")
    return np.nan

def load_data():
    all_files = glob.glob(str(STUDY_LOGS_DIR / "task*" / "*" / "trial_metrics_*.csv"))
    frames = []
    for filepath in all_files:
        path = Path(filepath)
        task_folder = path.parent.parent.name
        subject_id_folder = path.parent.name
        try:
            df = pd.read_csv(filepath)
            df["SubjectID"] = subject_id_folder
            df["TaskFolder"] = task_folder
            df["IsPilot"] = df["SubjectID"].str.startswith("P")
            if "Condition" in df.columns:
                df["ConditionLabel"] = df["Condition"].map(lambda x: CONDITION_LABELS.get(x, x))
            frames.append(df)
        except Exception as e:
            print(f"Error loading {filepath}: {e}")
            
    df_trials = pd.concat(frames, ignore_index=True) if frames else pd.DataFrame()
    # rename Scenario to Task to match tlx
    if not df_trials.empty and "Scenario" in df_trials.columns:
        df_trials = df_trials.rename(columns={"Scenario": "Task"})
    
    prescreen = pd.read_excel(EXCEL_FILE, sheet_name="2_PreScreening", skiprows=3)
    prescreen = prescreen.dropna(subset=["Subject ID"])
    prescreen["SubjectCode"] = prescreen["Subject ID"].apply(lambda x: x.split("-")[0] if isinstance(x, str) else x)
    prescreen["IsPilot"] = prescreen["SubjectCode"].str.startswith("P")
    
    tlx = pd.read_excel(EXCEL_FILE, sheet_name="4_PostConditionSurvey", skiprows=3)
    tlx = tlx.dropna(subset=["Subject ID"])
    tlx["SubjectCode"] = tlx["Subject ID"].apply(lambda x: x.split("-")[0] if isinstance(x, str) else x)
    letter_map = {"A": "Uniform", "B": "Foveated", "C": "Inverse-Foveated"}
    tlx["ConditionLabel"] = tlx["Condition Letter"].map(letter_map)
    tlx["IsPilot"] = tlx["SubjectCode"].str.startswith("P")
    tlx["ExtractedCollisions"] = tlx["Notes (optional)"].apply(extract_collisions)
    
    read_depths = []
    parsed_count = 0
    unparsed_count = 0
    print("--- Parsing Identification Read Depth Notes ---")
    for idx, row in tlx.iterrows():
        if row["Task"] == "Identification" and not row["IsPilot"]:
            rd = extract_read_depth(row["Notes (optional)"], row["Subject ID"], idx)
            read_depths.append(rd)
            if pd.isna(rd):
                unparsed_count += 1
            else:
                parsed_count += 1
        else:
            read_depths.append(np.nan)
    tlx["ReadDepth"] = read_depths
    print(f"Identification Notes Parsed: {parsed_count}, Unparsed: {unparsed_count}")
    print("---------------------------------------------")
    
    # Merge TLX metrics into df_trials
    if not df_trials.empty and not tlx.empty:
        df_trials["SubjectCode"] = df_trials["SubjectID"].apply(lambda x: x.split("-")[0])
        # tlx has SubjectCode, Task, ConditionLabel
        tlx_sub = tlx[["SubjectCode", "Task", "ConditionLabel", "ExtractedCollisions", "ReadDepth", 
                       "TLX Mental Demand (0-100)", "TLX Physical Demand (0-100)", 
                       "TLX Temporal Demand (0-100)", "TLX Frustration (0-100)", "RTLX (auto avg)"]]
        df_trials = pd.merge(df_trials, tlx_sub, on=["SubjectCode", "Task", "ConditionLabel"], how="left")
        
    return df_trials, prescreen, tlx

def run_friedman_test(df, task, dv, subject_col="SubjectCode", cond_col="ConditionLabel"):
    df_task = df[(df["Task"] == task) & (df[dv].notna())].copy()
    if df_task.empty:
        return None
        
    conds = df_task[cond_col].unique()
    if len(conds) != 3:
        return None
    
    subj_counts = df_task.groupby(subject_col)[cond_col].nunique()
    valid_subjs = subj_counts[subj_counts == 3].index
    dropped_subjs = subj_counts[subj_counts < 3].index
    
    if len(dropped_subjs) > 0:
        print(f"[{task} - {dv}] Dropped subjects due to missing conditions: {list(dropped_subjs)}")
        
    df_valid = df_task[df_task[subject_col].isin(valid_subjs)]
    if len(valid_subjs) < 2:
        return None
        
    pivot = df_valid.pivot(index=subject_col, columns=cond_col, values=dv)
    pivot = pivot[["Uniform", "Foveated", "Inverse-Foveated"]]
    
    try:
        stat, p_val = stats.friedmanchisquare(pivot["Uniform"], pivot["Foveated"], pivot["Inverse-Foveated"])
    except ValueError:
        stat, p_val = np.nan, np.nan
        
    res = {
        "Task": task,
        "DV": dv,
        "ChiSq": stat,
        "df": 2,
        "p": p_val,
        "Significant": "Yes" if pd.notna(p_val) and p_val < 0.05 else "No",
        "N": len(valid_subjs),
        "Pairwise": ""
    }
    
    if res["Significant"] == "Yes":
        try:
            # scikit-posthocs Nemenyi test
            melted = df_valid[[subject_col, cond_col, dv]].copy()
            melted[dv] = pd.to_numeric(melted[dv], errors='coerce') # Ensure numeric
            melted[cond_col] = melted[cond_col].astype(str) # Ensure string for comparison
            
            # Find groups (simplified: just sum of ranks for display)
            ranks = df_valid.groupby(subject_col)[[cond_col, dv]].apply(lambda x: x.set_index(cond_col)[dv].rank())
            sum_ranks = ranks.groupby(level=1).sum().to_dict()
            res["Pairwise"] = f"Sum Ranks: {sum_ranks}"
        except Exception as e:
            res["Pairwise"] = f"Posthoc failed: {e}"
            
    return res

def analyze_and_plot(df, prescreen, tlx):
    df_study = df[~df["IsPilot"]].copy()
    tlx_study = tlx[~tlx["IsPilot"]].copy()
    prescreen_study = prescreen[~prescreen["IsPilot"]].copy()
    
    prescreen_study["HasCorrection"] = ~prescreen_study["Corrective lenses"].str.contains("No correction", case=False, na=False)
    vision_correction_count = int(prescreen_study["HasCorrection"].sum())
    vr_exp_counts = prescreen_study["VR experience"].value_counts().to_dict() if "VR experience" in prescreen_study.columns else {}
    
    pptx_data = {
        "participants": {
            "total": int(df_study["SubjectID"].nunique()),
            "tasks": df_study.groupby("Task")["SubjectID"].nunique().to_dict(),
            "vision_correction": vision_correction_count,
            "vr_exp": {str(k): int(v) for k, v in vr_exp_counts.items()}
        },
        "stats": {}
    }
    
    sns.set_theme(style="whitegrid", font_scale=1.2)
    cond_order = ["Uniform", "Foveated", "Inverse-Foveated"]
    
    # ---------------------------------------------------------
    # PART 1: Identification "read depth" accuracy metric
    # ---------------------------------------------------------
    ident_df = df_study[df_study["Task"] == "Identification"].copy()
    if not ident_df.empty and "ReadDepth" in ident_df.columns:
        plt.figure(figsize=(8, 6))
        sns.barplot(data=ident_df, x="ConditionLabel", y="ReadDepth", 
                    order=cond_order, palette=PALETTE, capsize=.1, errorbar="se", legend=False)
        plt.title("Identification: Mean Read Depth (0-5 scale)", fontsize=14, fontweight="bold")
        plt.ylabel("Read Depth")
        plt.xlabel("")
        plt.ylim(0, 5.5)
        plt.tight_layout()
        plt.savefig(FIGURES_DIR / "identification_read_depth.png", dpi=150)
        plt.close()

    # ---------------------------------------------------------
    # PART 2: Friedman's test per task, per DV
    # ---------------------------------------------------------
    print("\n--- Running Friedman's Tests ---")
    all_dvs = [
        "DurationSeconds", 
        "ExtractedCollisions", 
        "RTLX (auto avg)", 
        "TLX Mental Demand (0-100)", 
        "TLX Frustration (0-100)",
        "ReadDepth"
    ]
    
    stats_results = []
    for task in TASKS:
        for dv in all_dvs:
            if dv == "ExtractedCollisions" and task == "Identification":
                continue
            if dv == "ReadDepth" and task != "Identification":
                continue
                
            res = run_friedman_test(df_study, task, dv)
            if res:
                stats_results.append(res)
    
    stats_df = pd.DataFrame(stats_results)
    
    print("\n--- Friedman Stats Summary ---")
    if not stats_df.empty:
        print(stats_df.to_string(index=False))
    print("------------------------------\n")
    
    summary_md = ""
    summary_md += "## Statistical Significance (Friedman's Test)\n\n"
    summary_md += "P < 0.05 means there is difference between the three conditions.\n\n"
    
    if not stats_df.empty:
        summary_md += "| Task | DV | N | Friedman chi-sq | df | p-value | Significant (p<0.05)? | Pairwise / Sum of Ranks |\n"
        summary_md += "|---|---|---|---|---|---|---|---|\n"
        for _, row in stats_df.iterrows():
            chisq_str = f"{row['ChiSq']:.3f}" if pd.notna(row['ChiSq']) else "NaN"
            pval_str = f"{row['p']:.4g}" if pd.notna(row['p']) else "NaN"
            summary_md += f"| {row['Task']} | {row['DV']} | {row['N']} | {chisq_str} | {row['df']} | {pval_str} | {row['Significant']} | {row['Pairwise']} |\n"
    
    summary_md += "\n"
    
    # Generate Plots
    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Task"] == task]
        if not df_task.empty:
            sns.barplot(data=df_task, x="ConditionLabel", y="DurationSeconds", 
                        order=cond_order, palette=PALETTE, capsize=.1, errorbar="se", ax=axes[i], legend=False)
            axes[i].set_title(f"{task}", fontweight="bold")
            axes[i].set_xlabel("")
            axes[i].set_ylabel("Completion Time (s)" if i == 0 else "")
    plt.suptitle("Mean Completion Time by Condition across Tasks", fontsize=16, fontweight="bold")
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "main_effect_duration_combined.png", dpi=150)
    plt.close()

    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Task"] == task]
        if not df_task.empty:
            sns.boxplot(data=df_task, x="ConditionLabel", y="DurationSeconds",
                        order=cond_order, palette=PALETTE, ax=axes[i], hue="ConditionLabel", legend=False)
            axes[i].set_title(f"{task}", fontweight="bold")
            axes[i].set_xlabel("")
            axes[i].set_ylabel("Duration (s)" if i == 0 else "")
    plt.suptitle("Completion Time Distribution across Tasks", fontsize=16, fontweight="bold")
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "duration_by_scenario_combined.png", dpi=150)
    plt.close()

    df_study['ConditionCat'] = pd.Categorical(df_study['ConditionLabel'], categories=cond_order, ordered=True)
    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Task"] == task].copy()
        if not df_task.empty:
            for sid in df_task["SubjectID"].unique():
                subj_data = df_task[df_task["SubjectID"] == sid].sort_values("ConditionCat")
                if len(subj_data) == 3:
                    axes[i].plot(subj_data["ConditionCat"], subj_data["DurationSeconds"], marker="o", color="gray", alpha=0.3)
            means = df_task.groupby("ConditionCat")["DurationSeconds"].mean()
            axes[i].plot(means.index, means.values, marker="D", color="#B00000", linewidth=3, markersize=8, label="Group Mean")
            axes[i].set_title(f"{task}", fontweight="bold")
            axes[i].set_xlabel("")
            axes[i].set_ylabel("Duration (s)" if i == 0 else "")
    plt.suptitle("Individual Differences in Completion Time across Tasks", fontsize=16, fontweight="bold")
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "individual_differences_combined.png", dpi=150)
    plt.close()

    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Task"] == task]
        if not df_task.empty and "ExtractedCollisions" in df_task.columns and df_task["ExtractedCollisions"].sum() > 0:
            sns.barplot(data=df_task, x="ConditionLabel", y="ExtractedCollisions", 
                        order=cond_order, palette=PALETTE, capsize=.1, errorbar="se", ax=axes[i], legend=False)
            axes[i].set_title(f"{task}", fontweight="bold")
        else:
            axes[i].set_title(f"{task} (N/A)", fontweight="bold")
            
        axes[i].set_xlabel("")
        axes[i].set_ylabel("Collision Count" if i == 0 else "")
    plt.suptitle("Average Collisions across Tasks", fontsize=16, fontweight="bold")
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "main_effect_collisions_combined.png", dpi=150)
    plt.close()

    tlx_metrics = [
        ("TLX Mental Demand (0-100)", "Mental Demand"),
        ("TLX Physical Demand (0-100)", "Physical Demand"),
        ("TLX Frustration (0-100)", "Frustration"),
        ("RTLX (auto avg)", "Overall Workload (RTLX)")
    ]
    
    for (col, title) in tlx_metrics:
        fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
        for i, task in enumerate(TASKS):
            df_task = df_study[df_study["Task"] == task]
            if not df_task.empty and col in df_task.columns:
                sns.barplot(data=df_task, x="ConditionLabel", y=col,
                            order=cond_order, palette=PALETTE, capsize=.1, errorbar="se", ax=axes[i], legend=False)
                axes[i].set_title(f"{task}", fontweight="bold")
                axes[i].set_xlabel("")
                axes[i].set_ylabel("Score (0-100)" if i == 0 else "")
                axes[i].set_ylim(0, 100)
                
        plt.suptitle(f"NASA-TLX: {title} across Tasks", fontsize=16, fontweight="bold")
        plt.tight_layout()
        plt.savefig(FIGURES_DIR / f"nasa_tlx_{title.replace(' ', '_')}_combined.png", dpi=150)
        plt.close()
        
    with open(OUTPUT_DIR / "pptx_data.json", "w") as f:
        json.dump(pptx_data, f, indent=4)
        
    summary_md += "## Descriptive Statistics (Means & SD)\n\n"
    summary_md += "| Task | Metric | Condition | Mean | SD |\n"
    summary_md += "|---|---|---|---|---|\n"
    
    metrics_to_summarize = [
        ("DurationSeconds", "Duration (s)"),
        ("ExtractedCollisions", "Collisions"),
        ("ReadDepth", "Read Depth (Identification)"),
        ("TLX Mental Demand (0-100)", "TLX Mental Demand"),
        ("TLX Frustration (0-100)", "TLX Frustration"),
        ("RTLX (auto avg)", "Overall Workload (RTLX)")
    ]
    
    for task in TASKS:
        df_task = df_study[df_study["Task"] == task]
        for col, label in metrics_to_summarize:
            if col in df_task.columns and not df_task[col].isna().all():
                for cond in cond_order:
                    mean_val = df_task[df_task["ConditionLabel"] == cond][col].mean()
                    std_val = df_task[df_task["ConditionLabel"] == cond][col].std()
                    summary_md += f"| {task} | {label} | {cond} | {mean_val:.2f} | {std_val:.2f} |\n"
        
    with open(OUTPUT_DIR / "summary_statistics.md", "w") as f:
        f.write("# Summary Statistics & Tests\n\n")
        f.write(summary_md)
        
if __name__ == "__main__":
    df_trials, prescreen, tlx = load_data()
    analyze_and_plot(df_trials, prescreen, tlx)
