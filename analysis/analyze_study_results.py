#!/usr/bin/env python3
"""
PBL5 Foveated Teleoperation — User Study Analysis Script

Processes all subject trial CSVs and produces:
  - Descriptive statistics per (condition, scenario)
  - Repeated-measures ANOVA on completion_time
  - Pairwise comparisons with Bonferroni correction
  - Figures (bar charts, individual differences, workload)
  - STUDY_RESULTS.md summary document

Usage:
    cd PBL5_FoveatedTeleoperation
    python analysis/analyze_study_results.py

Prerequisites:
    pip install pandas matplotlib seaborn scipy pingouin
"""

import os
import sys
import glob
import warnings
from pathlib import Path
from datetime import datetime

import pandas as pd
import numpy as np
import matplotlib
matplotlib.use("Agg")  # Non-interactive backend for headless rendering
import matplotlib.pyplot as plt
import seaborn as sns
from scipy import stats

try:
    import pingouin as pg
    HAS_PINGOUIN = True
except ImportError:
    HAS_PINGOUIN = False
    print("WARNING: pingouin not installed. Skipping RM-ANOVA. Install with: pip install pingouin")

# ─── Configuration ──────────────────────────────────────────────────────────

# Where to find trial CSVs
LOGS_DIR = Path(__file__).resolve().parent.parent / "foveated-teleop-unity" / "logs"
MOCK_LOGS_DIR = Path(__file__).resolve().parent.parent / "mock_pioneer" / "logs"

# Where to write outputs
OUTPUT_DIR = Path(__file__).resolve().parent
FIGURES_DIR = OUTPUT_DIR / "figures"

# Survey data (manually entered after study)
SURVEY_FILE = OUTPUT_DIR / "survey_data.csv"

# Condition labels for display
CONDITION_LABELS = {
    "UniformQ50": "Uniform",
    "Foveated_15_85": "Foveated",
    "PeripheralOnly_Q30": "Peripheral-Only",
}

# Colorblind-friendly palette
PALETTE = {"Uniform": "#4477AA", "Foveated": "#228833", "Peripheral-Only": "#CC6677"}

warnings.filterwarnings("ignore", category=FutureWarning)
sns.set_theme(style="whitegrid", font_scale=1.2)


# ─── Data Loading ───────────────────────────────────────────────────────────

def load_trial_data():
    """Load and concatenate all trial CSVs from both possible log directories."""
    patterns = [
        str(LOGS_DIR / "trial_metrics_*.csv"),
        str(MOCK_LOGS_DIR / "trial_metrics_*.csv"),
    ]
    
    all_files = []
    for pattern in patterns:
        all_files.extend(glob.glob(pattern))
    
    if not all_files:
        print(f"ERROR: No trial CSV files found in:")
        print(f"  {LOGS_DIR}")
        print(f"  {MOCK_LOGS_DIR}")
        print("Run the user study first, then re-run this script.")
        sys.exit(1)
    
    frames = []
    for filepath in sorted(all_files):
        try:
            df = pd.read_csv(filepath)
            # Derive subject ID from filename
            fname = Path(filepath).stem  # e.g., trial_metrics_20260622_143000
            # If filename contains S## pattern, extract it
            if "_S" in fname:
                subject_id = fname.split("_S")[-1].split("_")[0]
                subject_id = f"S{subject_id}"
            else:
                # Use session timestamp as fallback subject identifier
                parts = fname.replace("trial_metrics_", "")
                subject_id = f"S_{parts}"
            
            df["subject_id"] = subject_id
            df["source_file"] = Path(filepath).name
            frames.append(df)
            print(f"  Loaded {filepath} → {len(df)} rows, subject={subject_id}")
        except Exception as e:
            print(f"  WARNING: Failed to load {filepath}: {e}")
    
    if not frames:
        print("ERROR: No valid data loaded.")
        sys.exit(1)
    
    combined = pd.concat(frames, ignore_index=True)
    
    # Normalize condition names for display
    if "condition" in combined.columns:
        combined["condition_raw"] = combined["condition"]
        combined["condition"] = combined["condition"].map(
            lambda x: CONDITION_LABELS.get(x, x)
        )
    
    print(f"\nTotal: {len(combined)} trial rows from {len(all_files)} files, "
          f"{combined['subject_id'].nunique()} subjects")
    
    return combined


def validate_data(df):
    """Validate data completeness and flag issues."""
    issues = []
    
    for sid in df["subject_id"].unique():
        subj_df = df[df["subject_id"] == sid]
        n_rows = len(subj_df)
        
        if n_rows != 9:
            issues.append(f"Subject {sid}: expected 9 rows, got {n_rows}")
        
        # Check condition coverage
        conditions = subj_df["condition"].unique()
        expected_conditions = set(CONDITION_LABELS.values())
        missing = expected_conditions - set(conditions)
        if missing:
            issues.append(f"Subject {sid}: missing conditions {missing}")
    
    if issues:
        print("\n⚠️  Data validation issues:")
        for issue in issues:
            print(f"  - {issue}")
    else:
        print("\n✅ Data validation passed: all subjects have 9 rows, 3 conditions each")
    
    return issues


# ─── Descriptive Statistics ─────────────────────────────────────────────────

def compute_descriptive_stats(df):
    """Compute mean ± SD per (condition, scenario) for key metrics."""
    metrics = ["completion_time_seconds", "collision_count"]
    
    results = []
    for metric in metrics:
        if metric not in df.columns:
            continue
        
        grouped = df.groupby(["condition", "scenario"])[metric].agg(
            ["mean", "std", "count"]
        ).reset_index()
        grouped["metric"] = metric
        grouped["ci95"] = 1.96 * grouped["std"] / np.sqrt(grouped["count"])
        results.append(grouped)
    
    # Success rate
    if "success" in df.columns:
        success_rate = df.groupby(["condition", "scenario"])["success"].agg(
            ["mean", "count"]
        ).reset_index()
        success_rate.columns = ["condition", "scenario", "mean", "count"]
        success_rate["std"] = np.nan
        success_rate["ci95"] = np.nan
        success_rate["metric"] = "success_rate"
        results.append(success_rate)
    
    if results:
        stats_df = pd.concat(results, ignore_index=True)
        stats_df.to_csv(OUTPUT_DIR / "descriptive_stats.csv", index=False)
        print(f"\n📊 Descriptive stats saved to {OUTPUT_DIR / 'descriptive_stats.csv'}")
        return stats_df
    
    return pd.DataFrame()


# ─── Statistical Tests ──────────────────────────────────────────────────────

def run_rm_anova(df):
    """Run repeated-measures ANOVA on completion_time_seconds."""
    if not HAS_PINGOUIN:
        print("\n⚠️  Skipping RM-ANOVA (pingouin not installed)")
        return None
    
    if "completion_time_seconds" not in df.columns:
        print("\n⚠️  Skipping RM-ANOVA (completion_time_seconds column missing)")
        return None
    
    # Need at least 2 subjects with complete data
    complete_subjects = []
    for sid in df["subject_id"].unique():
        subj_df = df[df["subject_id"] == sid]
        if len(subj_df["condition"].unique()) == 3:
            complete_subjects.append(sid)
    
    if len(complete_subjects) < 2:
        print(f"\n⚠️  Skipping RM-ANOVA (need ≥2 complete subjects, got {len(complete_subjects)})")
        return None
    
    analysis_df = df[df["subject_id"].isin(complete_subjects)].copy()
    
    try:
        # Two-way RM-ANOVA: condition × scenario
        if "scenario" in analysis_df.columns and analysis_df["scenario"].nunique() > 1:
            anova_result = pg.rm_anova(
                data=analysis_df,
                dv="completion_time_seconds",
                within=["condition", "scenario"] if analysis_df["scenario"].nunique() > 1 else ["condition"],
                subject="subject_id",
            )
        else:
            anova_result = pg.rm_anova(
                data=analysis_df,
                dv="completion_time_seconds",
                within="condition",
                subject="subject_id",
            )
        
        anova_result.to_csv(OUTPUT_DIR / "rm_anova_results.csv", index=False)
        print(f"\n📈 RM-ANOVA results saved to {OUTPUT_DIR / 'rm_anova_results.csv'}")
        print(anova_result.to_string())
        return anova_result
    
    except Exception as e:
        print(f"\n⚠️  RM-ANOVA failed: {e}")
        return None


def run_pairwise_comparisons(df):
    """Run pairwise t-tests between conditions with Bonferroni correction."""
    if "completion_time_seconds" not in df.columns:
        return None
    
    conditions = sorted(df["condition"].unique())
    if len(conditions) < 2:
        return None
    
    results = []
    
    # Collapsed across scenarios
    for i in range(len(conditions)):
        for j in range(i + 1, len(conditions)):
            c1, c2 = conditions[i], conditions[j]
            
            # Get subject-level means
            means_c1 = df[df["condition"] == c1].groupby("subject_id")["completion_time_seconds"].mean()
            means_c2 = df[df["condition"] == c2].groupby("subject_id")["completion_time_seconds"].mean()
            
            # Only include subjects present in both conditions
            common_subjects = means_c1.index.intersection(means_c2.index)
            if len(common_subjects) < 2:
                continue
            
            t_stat, p_val = stats.ttest_rel(
                means_c1.loc[common_subjects],
                means_c2.loc[common_subjects]
            )
            
            # Bonferroni correction (3 comparisons)
            p_corrected = min(p_val * 3, 1.0)
            
            results.append({
                "comparison": f"{c1} vs {c2}",
                "scenario": "All",
                "t_statistic": round(t_stat, 3),
                "p_value": round(p_val, 4),
                "p_bonferroni": round(p_corrected, 4),
                "n_subjects": len(common_subjects),
                "mean_diff": round(
                    means_c1.loc[common_subjects].mean() - means_c2.loc[common_subjects].mean(), 3
                ),
                "significant": "Yes" if p_corrected < 0.05 else "No",
            })
    
    if results:
        pw_df = pd.DataFrame(results)
        pw_df.to_csv(OUTPUT_DIR / "pairwise_comparisons.csv", index=False)
        print(f"\n🔍 Pairwise comparisons saved to {OUTPUT_DIR / 'pairwise_comparisons.csv'}")
        print(pw_df.to_string())
        return pw_df
    
    return None


# ─── Figures ────────────────────────────────────────────────────────────────

def plot_completion_time_by_condition_scenario(df):
    """Grouped bar chart: x=scenario, hue=condition, y=mean completion time."""
    if "completion_time_seconds" not in df.columns:
        return
    
    fig, ax = plt.subplots(figsize=(10, 6))
    
    order = sorted(df["scenario"].unique()) if "scenario" in df.columns else None
    hue_order = [v for v in CONDITION_LABELS.values() if v in df["condition"].unique()]
    
    sns.barplot(
        data=df, x="scenario", y="completion_time_seconds", hue="condition",
        hue_order=hue_order, order=order,
        palette=PALETTE, ci=95, capsize=0.05, ax=ax
    )
    
    ax.set_xlabel("Scenario", fontweight="bold")
    ax.set_ylabel("Completion Time (s)", fontweight="bold")
    ax.set_title("Completion Time by Condition and Scenario", fontweight="bold")
    ax.legend(title="Condition")
    
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "completion_time_by_condition_scenario.png", dpi=150)
    plt.close()
    print(f"  📊 completion_time_by_condition_scenario.png")


def plot_collisions_by_condition_scenario(df):
    """Grouped bar chart for collision counts."""
    if "collision_count" not in df.columns:
        return
    
    fig, ax = plt.subplots(figsize=(10, 6))
    
    order = sorted(df["scenario"].unique()) if "scenario" in df.columns else None
    hue_order = [v for v in CONDITION_LABELS.values() if v in df["condition"].unique()]
    
    sns.barplot(
        data=df, x="scenario", y="collision_count", hue="condition",
        hue_order=hue_order, order=order,
        palette=PALETTE, ci=95, capsize=0.05, ax=ax
    )
    
    ax.set_xlabel("Scenario", fontweight="bold")
    ax.set_ylabel("Collision Count", fontweight="bold")
    ax.set_title("Collisions by Condition and Scenario", fontweight="bold")
    ax.legend(title="Condition")
    
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "collisions_by_condition_scenario.png", dpi=150)
    plt.close()
    print(f"  📊 collisions_by_condition_scenario.png")


def plot_individual_differences(df):
    """Line plot: x=condition, y=completion_time, one line per subject."""
    if "completion_time_seconds" not in df.columns:
        return
    
    subj_means = df.groupby(["subject_id", "condition"])["completion_time_seconds"].mean().reset_index()
    
    fig, ax = plt.subplots(figsize=(8, 6))
    
    hue_order = [v for v in CONDITION_LABELS.values() if v in subj_means["condition"].unique()]
    
    for sid in subj_means["subject_id"].unique():
        subj_data = subj_means[subj_means["subject_id"] == sid]
        # Map condition to x position
        x_vals = [hue_order.index(c) if c in hue_order else 0 for c in subj_data["condition"]]
        ax.plot(x_vals, subj_data["completion_time_seconds"], marker="o",
                alpha=0.4, linewidth=1, color="gray")
    
    # Add group mean
    group_means = subj_means.groupby("condition")["completion_time_seconds"].mean()
    x_group = [hue_order.index(c) if c in hue_order else 0 for c in group_means.index]
    ax.plot(x_group, group_means.values, marker="D", markersize=10,
            linewidth=3, color="#EE6677", label="Group Mean", zorder=5)
    
    ax.set_xticks(range(len(hue_order)))
    ax.set_xticklabels(hue_order)
    ax.set_xlabel("Condition", fontweight="bold")
    ax.set_ylabel("Mean Completion Time (s)", fontweight="bold")
    ax.set_title("Individual Differences Across Conditions", fontweight="bold")
    ax.legend()
    
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "individual_differences.png", dpi=150)
    plt.close()
    print(f"  📊 individual_differences.png")


def plot_condition_main_effect(df):
    """Bar chart of mean completion time collapsed across scenarios."""
    if "completion_time_seconds" not in df.columns:
        return
    
    fig, ax = plt.subplots(figsize=(7, 5))
    
    hue_order = [v for v in CONDITION_LABELS.values() if v in df["condition"].unique()]
    
    sns.barplot(
        data=df, x="condition", y="completion_time_seconds",
        order=hue_order, palette=PALETTE, ci=95, capsize=0.08, ax=ax
    )
    
    ax.set_xlabel("Condition", fontweight="bold")
    ax.set_ylabel("Mean Completion Time (s)", fontweight="bold")
    ax.set_title("Condition Main Effect on Completion Time", fontweight="bold")
    
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "condition_main_effect.png", dpi=150)
    plt.close()
    print(f"  📊 condition_main_effect.png")


def plot_workload(survey_df):
    """Bar chart of mean workload rating by condition."""
    if survey_df is None or survey_df.empty:
        return
    
    if "mental_demand" not in survey_df.columns:
        return
    
    fig, axes = plt.subplots(1, 2, figsize=(14, 5))
    
    for ax, col, title in zip(axes, ["mental_demand", "visual_quality"],
                               ["Mental Demand", "Visual Quality"]):
        if col not in survey_df.columns:
            continue
        
        hue_order = [v for v in CONDITION_LABELS.values() if v in survey_df["condition"].unique()]
        
        sns.barplot(
            data=survey_df, x="condition", y=col,
            order=hue_order, palette=PALETTE, ci=95, capsize=0.08, ax=ax
        )
        ax.set_xlabel("Condition", fontweight="bold")
        ax.set_ylabel(f"Mean {title} Rating (1–7)", fontweight="bold")
        ax.set_title(f"{title} by Condition", fontweight="bold")
        ax.set_ylim(0.5, 7.5)
    
    plt.tight_layout()
    plt.savefig(FIGURES_DIR / "workload_ratings.png", dpi=150)
    plt.close()
    print(f"  📊 workload_ratings.png")


# ─── Report Generation ─────────────────────────────────────────────────────

def generate_report(df, stats_df, anova_result, pairwise_df, survey_df):
    """Generate STUDY_RESULTS.md with all findings."""
    
    n_subjects = df["subject_id"].nunique()
    n_trials = len(df)
    
    report = f"""# User Study Results

**PBL5 Foveated Teleoperation — Matched-Bandwidth Evaluation**

Generated: {datetime.now().strftime('%Y-%m-%d %H:%M')}

---

## Study Overview

- **N subjects:** {n_subjects}
- **Total trials:** {n_trials}
- **Conditions:** Uniform (Q50), Foveated (periph Q15, fovea Q85), Peripheral-Only (Q30)
- **Scenarios:** {', '.join(sorted(df['scenario'].unique())) if 'scenario' in df.columns else 'N/A'}
- **Design:** Within-subjects, Latin square counterbalanced

---

## Descriptive Statistics

### Completion Time (seconds)

"""
    
    if "completion_time_seconds" in df.columns:
        ct_stats = df.groupby("condition")["completion_time_seconds"].agg(["mean", "std", "count"])
        report += "| Condition | Mean | SD | N |\n|---|---|---|---|\n"
        for cond, row in ct_stats.iterrows():
            report += f"| {cond} | {row['mean']:.2f} | {row['std']:.2f} | {int(row['count'])} |\n"
    
    if "collision_count" in df.columns:
        report += "\n### Collision Count\n\n"
        cc_stats = df.groupby("condition")["collision_count"].agg(["mean", "std", "count"])
        report += "| Condition | Mean | SD | N |\n|---|---|---|---|\n"
        for cond, row in cc_stats.iterrows():
            report += f"| {cond} | {row['mean']:.2f} | {row['std']:.2f} | {int(row['count'])} |\n"
    
    if "success" in df.columns:
        report += "\n### Success Rate\n\n"
        sr_stats = df.groupby("condition")["success"].mean()
        report += "| Condition | Success Rate |\n|---|---|\n"
        for cond, rate in sr_stats.items():
            report += f"| {cond} | {rate*100:.1f}% |\n"
    
    # ANOVA
    report += "\n---\n\n## Repeated-Measures ANOVA\n\n"
    if anova_result is not None:
        report += "### Completion Time\n\n"
        report += "| Source | F | p | η²p |\n|---|---|---|---|\n"
        for _, row in anova_result.iterrows():
            source = row.get("Source", row.get("source", "?"))
            f_val = row.get("F", row.get("f", 0))
            p_val = row.get("p-unc", row.get("p", 0))
            eta = row.get("np2", row.get("eta2", 0))
            sig = " *" if p_val < 0.05 else ""
            report += f"| {source} | {f_val:.3f} | {p_val:.4f}{sig} | {eta:.3f} |\n"
        report += "\n\\* p < .05\n"
    else:
        report += "*RM-ANOVA not available (insufficient data or pingouin not installed).*\n"
    
    # Pairwise
    report += "\n---\n\n## Pairwise Comparisons (Bonferroni-corrected)\n\n"
    if pairwise_df is not None:
        report += "| Comparison | t | p | p (corrected) | Significant? |\n|---|---|---|---|---|\n"
        for _, row in pairwise_df.iterrows():
            report += (f"| {row['comparison']} | {row['t_statistic']:.3f} | "
                      f"{row['p_value']:.4f} | {row['p_bonferroni']:.4f} | {row['significant']} |\n")
    else:
        report += "*Pairwise comparisons not available.*\n"
    
    # Survey
    if survey_df is not None and not survey_df.empty:
        report += "\n---\n\n## Subjective Ratings\n\n"
        if "mental_demand" in survey_df.columns:
            md_stats = survey_df.groupby("condition")["mental_demand"].agg(["mean", "std"])
            report += "### Mental Demand (1=Very Low, 7=Very High)\n\n"
            report += "| Condition | Mean | SD |\n|---|---|---|\n"
            for cond, row in md_stats.iterrows():
                report += f"| {cond} | {row['mean']:.2f} | {row['std']:.2f} |\n"
        
        if "visual_quality" in survey_df.columns:
            vq_stats = survey_df.groupby("condition")["visual_quality"].agg(["mean", "std"])
            report += "\n### Visual Quality (1=Very Poor, 7=Excellent)\n\n"
            report += "| Condition | Mean | SD |\n|---|---|---|\n"
            for cond, row in vq_stats.iterrows():
                report += f"| {cond} | {row['mean']:.2f} | {row['std']:.2f} |\n"
    
    # Figures
    report += """
---

## Figures

### Completion Time by Condition and Scenario
![Completion Time](figures/completion_time_by_condition_scenario.png)

### Collisions by Condition and Scenario
![Collisions](figures/collisions_by_condition_scenario.png)

### Individual Differences
![Individual Differences](figures/individual_differences.png)

### Condition Main Effect
![Condition Main Effect](figures/condition_main_effect.png)

### Workload Ratings
![Workload](figures/workload_ratings.png)

---

## Summary of Findings

"""
    
    # Auto-generate a brief summary
    if "completion_time_seconds" in df.columns:
        means = df.groupby("condition")["completion_time_seconds"].mean()
        fastest = means.idxmin()
        slowest = means.idxmax()
        report += f"- **Fastest condition:** {fastest} (M = {means[fastest]:.2f}s)\n"
        report += f"- **Slowest condition:** {slowest} (M = {means[slowest]:.2f}s)\n"
    
    if "collision_count" in df.columns:
        col_means = df.groupby("condition")["collision_count"].mean()
        safest = col_means.idxmin()
        report += f"- **Fewest collisions:** {safest} (M = {col_means[safest]:.2f})\n"
    
    report += """
---

## Limitations

- Small sample size (N ≤ 20) limits generalizability
- Within-subjects design may produce practice/fatigue effects (mitigated by Latin square)
- Simulated environment — real-world teleoperation may differ
- Eye tracking accuracy varies by individual and headset fit
- Matched bandwidth does not account for perceptual bandwidth (e.g., perceived quality)
"""
    
    report_path = OUTPUT_DIR / "STUDY_RESULTS.md"
    with open(report_path, "w", encoding="utf-8") as f:
        f.write(report)
    
    print(f"\n📝 Report saved to {report_path}")


# ─── Survey Data Template ──────────────────────────────────────────────────

def create_survey_template():
    """Create a template CSV for manual survey data entry."""
    if SURVEY_FILE.exists():
        print(f"\n📋 Survey file already exists: {SURVEY_FILE}")
        return pd.read_csv(SURVEY_FILE)
    
    # Create template
    template = pd.DataFrame({
        "subject_id": [],
        "condition": [],
        "condition_order": [],  # 1st, 2nd, 3rd experienced
        "mental_demand": [],
        "visual_quality": [],
        "preference_rank": [],
        "open_feedback": [],
    })
    
    template.to_csv(SURVEY_FILE, index=False)
    print(f"\n📋 Survey template created: {SURVEY_FILE}")
    print("   Fill this in manually after each subject session, then re-run analysis.")
    return None


# ─── Main ───────────────────────────────────────────────────────────────────

def main():
    print("=" * 60)
    print("PBL5 Foveated Teleoperation — User Study Analysis")
    print("=" * 60)
    
    # Ensure output directories exist
    FIGURES_DIR.mkdir(parents=True, exist_ok=True)
    
    # Load data
    print("\n📂 Loading trial data...")
    df = load_trial_data()
    
    # Validate
    issues = validate_data(df)
    
    # Descriptive stats
    print("\n📊 Computing descriptive statistics...")
    stats_df = compute_descriptive_stats(df)
    
    # RM-ANOVA
    print("\n📈 Running repeated-measures ANOVA...")
    anova_result = run_rm_anova(df)
    
    # Pairwise comparisons
    print("\n🔍 Running pairwise comparisons...")
    pairwise_df = run_pairwise_comparisons(df)
    
    # Load survey data (if available)
    survey_df = None
    if SURVEY_FILE.exists():
        try:
            survey_df = pd.read_csv(SURVEY_FILE)
            if len(survey_df) > 0:
                # Normalize condition names
                if "condition" in survey_df.columns:
                    survey_df["condition"] = survey_df["condition"].map(
                        lambda x: CONDITION_LABELS.get(x, x)
                    )
                print(f"\n📋 Loaded survey data: {len(survey_df)} responses")
            else:
                survey_df = None
        except Exception:
            survey_df = None
    else:
        create_survey_template()
    
    # Figures
    print("\n🎨 Generating figures...")
    plot_completion_time_by_condition_scenario(df)
    plot_collisions_by_condition_scenario(df)
    plot_individual_differences(df)
    plot_condition_main_effect(df)
    if survey_df is not None:
        plot_workload(survey_df)
    
    # Report
    print("\n📝 Generating STUDY_RESULTS.md...")
    generate_report(df, stats_df, anova_result, pairwise_df, survey_df)
    
    print("\n" + "=" * 60)
    print("✅ Analysis complete!")
    print(f"   Results: {OUTPUT_DIR / 'STUDY_RESULTS.md'}")
    print(f"   Stats:   {OUTPUT_DIR / 'descriptive_stats.csv'}")
    print(f"   Figures: {FIGURES_DIR}")
    print("=" * 60)


if __name__ == "__main__":
    main()
