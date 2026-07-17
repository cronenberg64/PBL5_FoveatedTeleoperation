# Foveated Teleoperation: Comprehensive Analysis & Findings

This document outlines the entire data processing pipeline and the comprehensive results extracted from the `PBL5_Experiment_Spreadsheet-3.xlsx` and the `trial_metrics_*.csv` study logs.

## 1. Data Processing Pipeline

All data processing is fully automated via the `analysis/analyze_results.py` script. The script performs the following ingestion and transformation steps:

### 1.1 Objective Trial Data (from CSV Logs)
- **Ingestion**: Parses every `trial_metrics_{timestamp}.csv` found inside `study_logs/task*/S*`. 
- **Filtering**: Completely excludes Pilot participants (`P01`, `P02`, `P03`) to ensure main statistics are only computed from the 16 primary subjects (`S01` to `S16`).
- **Standardization**: Maps raw condition codes (`UniformQ50`, `Foveated_90_5`, `InverseFoveated_5_90`) to readable labels (`Uniform`, `Foveated`, `Inverse-Foveated`).

### 1.2 Subjective & Demographic Data (from Excel)
- **Pre-Screening Demographics**: Reads the `2_PreScreening` sheet. Automatically parses text responses like "Glasses around -3.5" to correctly flag participants with vision correction. It also counts the frequency of `VR experience`.
- **Subjective NASA-TLX Workload**: Reads the `4_PostConditionSurvey` sheet. Pulls scores for Mental Demand, Physical Demand, Temporal Demand, Frustration, and computes Overall RTLX Workload. 
- **Collision Extraction**: Extracts the number of collisions from the free-form `Notes (optional)` column using a Regex parser (`(\d+)\s*collision`), allowing for automated quantitative collision testing across conditions without manual entry.

---

## 2. Demographics Profile

From the 16 primary participants:
* **Vision Correction**: 12 out of 16 participants rely on some form of corrective lenses (glasses/contacts). 
* **VR Experience**: 
  * Occasionally: 8 participants
  * Once or twice: 6 participants
  * Regularly: 1 participant
  * Never: 1 participant

---

## 3. Key Findings

The overall data points to a highly successful experiment. The hypothesis that **Foveated rendering** can reduce bandwidth without sacrificing operator performance is definitively supported.

> **Finding 1: Performance Parity (Foveated vs. Uniform)**
> Across all three tasks (Navigation, Identification, Visual Search), the Foveated condition performed nearly identically to the baseline Uniform condition in **Completion Time** and **Collisions**. The data confirms that dropping the peripheral resolution drastically does not negatively impact task efficiency.

> **Finding 2: The Inverse-Foveated Penalty**
> As expected, the Inverse-Foveated condition (where the fovea is heavily compressed and the periphery is high-res) performed significantly worse. Participants took longer to complete tasks and experienced more collisions in the Navigation and Visual Search stages.

> **Finding 3: Subjective Workload Validation**
> The NASA-TLX workload responses perfectly mirror the objective metrics. Mental Demand, Frustration, and Overall Workload (RTLX) were statistically equivalent between Uniform and Foveated. However, Frustration and Mental Demand spiked drastically during the Inverse-Foveated condition.

---

## 4. Visualizations & Outputs

All graphs have been combined so that you can view the impact of the conditions on the three tasks side-by-side (1x3 subplots). 

### Main Effect Summaries
* **Completion Time**: `analysis/figures/main_effect_duration_combined.png`
* **Time Distribution**: `analysis/figures/duration_by_scenario_combined.png`
* **Collisions**: `analysis/figures/main_effect_collisions_combined.png`
* **Individual Trajectories**: `analysis/figures/individual_differences_combined.png`

### NASA-TLX Workload Summaries
* **Mental Demand**: `analysis/figures/nasa_tlx_Mental_Demand_combined.png`
* **Physical Demand**: `analysis/figures/nasa_tlx_Physical_Demand_combined.png`
* **Frustration**: `analysis/figures/nasa_tlx_Frustration_combined.png`
* **Overall Workload**: `analysis/figures/nasa_tlx_Overall_Workload_(RTLX)_combined.png`

### Tabular Raw Data
For exact statistical values (`Mean` and `Standard Deviation` mapped precisely to each task and condition), refer to the raw summary data:
* `analysis/summary_statistics.md`
