import os
import glob
import json
import re
from pathlib import Path
from datetime import datetime
import pandas as pd
import numpy as np
import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
import seaborn as sns
import scipy.stats as stats

try:
    import pingouin as pg
    HAS_PINGOUIN = True
except ImportError:
    HAS_PINGOUIN = False

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
    
    return df_trials, prescreen, tlx

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
            "tasks": df_study.groupby("Scenario")["SubjectID"].nunique().to_dict(),
            "vision_correction": vision_correction_count,
            "vr_exp": {str(k): int(v) for k, v in vr_exp_counts.items()}
        },
        "stats": {}
    }
    
    sns.set_theme(style="whitegrid", font_scale=1.2)
    cond_order = ["Uniform", "Foveated", "Inverse-Foveated"]
    
    # 1. Main Effect Duration (1 figure, 3 subplots)
    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Scenario"] == task]
        if not df_task.empty:
            sns.barplot(data=df_task, x="ConditionLabel", y="DurationSeconds", 
                        order=cond_order, palette=PALETTE, capsize=.1, errorbar="se", ax=axes[i], legend=False)
            axes[i].set_title(f"{task}", fontweight="bold")
            axes[i].set_xlabel("")
            axes[i].set_ylabel("Completion Time (s)" if i == 0 else "")
            
            if HAS_PINGOUIN:
                try:
                    res = pg.friedman(data=df_task, dv="DurationSeconds", within="ConditionLabel", subject="SubjectID")
                    pptx_data["stats"][f"{task}_duration_friedman_p"] = float(res.loc["Friedman", "p_unc"])
                except: pass
    plt.suptitle("Mean Completion Time by Condition across Tasks", fontsize=16, fontweight="bold")
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "main_effect_duration_combined.png", dpi=150)
    plt.close()

    # 2. Duration Boxplot (1 figure, 3 subplots)
    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Scenario"] == task]
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

    # 3. Individual Differences (1 figure, 3 subplots)
    df_study['ConditionCat'] = pd.Categorical(df_study['ConditionLabel'], categories=cond_order, ordered=True)
    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = df_study[df_study["Scenario"] == task].copy()
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

    # 4. Collisions Analysis (1 figure, 3 subplots)
    tlx_study['ConditionCat'] = pd.Categorical(tlx_study['ConditionLabel'], categories=cond_order, ordered=True)
    fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
    for i, task in enumerate(TASKS):
        df_task = tlx_study[tlx_study["Task"] == task]
        if not df_task.empty and df_task["ExtractedCollisions"].sum() > 0:
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

    # 5. NASA TLX (1 figure per metric, each with 3 subplots)
    tlx_metrics = [
        ("TLX Mental Demand (0-100)", "Mental Demand"),
        ("TLX Physical Demand (0-100)", "Physical Demand"),
        ("TLX Frustration (0-100)", "Frustration"),
        ("RTLX (auto avg)", "Overall Workload (RTLX)")
    ]
    
    for (col, title) in tlx_metrics:
        fig, axes = plt.subplots(1, 3, figsize=(18, 6), sharey=False)
        for i, task in enumerate(TASKS):
            df_task = tlx_study[tlx_study["Task"] == task]
            if not df_task.empty and col in df_task.columns:
                sns.barplot(data=df_task, x="ConditionLabel", y=col,
                            order=cond_order, palette=PALETTE, capsize=.1, errorbar="se", ax=axes[i], legend=False)
                axes[i].set_title(f"{task}", fontweight="bold")
                axes[i].set_xlabel("")
                axes[i].set_ylabel("Score (0-100)" if i == 0 else "")
                axes[i].set_ylim(0, 100)
                
                try:
                    res = pg.friedman(data=df_task.dropna(subset=[col]), dv=col, within="ConditionLabel", subject="SubjectCode")
                    p = res.loc["Friedman", "p_unc"]
                    pptx_data["stats"][f"{task}_tlx_{title.lower().replace(' ', '_')}_p"] = float(p)
                except: pass
                
        plt.suptitle(f"NASA-TLX: {title} across Tasks", fontsize=16, fontweight="bold")
        plt.tight_layout()
        plt.savefig(FIGURES_DIR / f"nasa_tlx_{title.replace(' ', '_')}_combined.png", dpi=150)
        plt.close()
        
    with open(OUTPUT_DIR / "pptx_data.json", "w") as f:
        json.dump(pptx_data, f, indent=4)
        
if __name__ == "__main__":
    df_trials, prescreen, tlx = load_data()
    analyze_and_plot(df_trials, prescreen, tlx)
