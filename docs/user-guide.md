# SmartBin - User Guide & Policy Reference

This guide explains SmartBin's interface, settings, storage pressure states, and background protection rules.

---

## 1. Main Dashboard Overview

SmartBin's dashboard provides real-time awareness of drive storage and protection states:

- **Drive Space Utilization Bar**: Displays visual disk usage percentage and current system pressure state.
- **System State Labels**:
  - `NORMAL`: Free space is above the low pressure threshold (Default > 15%). No optimization required.
  - `LOW`: Free space dropped below low threshold (Default ≤ 15%). Scanning and candidate prioritization active.
  - `CRITICAL`: Free space dropped below critical threshold (Default ≤ 5%). Automatic sequential compression enabled (if Mode is set to Automatic).
- **SmartBin Engine Metrics**: Shows number of protected files, total original vs stored bytes, and total reclaimed storage.
- **Operations Terminal**: Real-time log terminal displaying background events, state updates, and SHA-256 validation results.

---

## 2. Policy Settings & Inputs Validation

SmartBin policy rules can be customized on the **Settings** tab:

### Automatic Protection Modes:
- **OFF (Default)**: Zero automatic background optimizations occur. SmartBin operates purely in manual/controlled mode.
- **NOTIFY ME**: Background thread scans storage capacity periodically and raises desktop notifications when low space is detected.
- **AUTOMATIC**: Automatically executes single-item optimizations sequentially when critical pressure is reached.

### Configurable Thresholds:
1. **Low Storage Threshold Percentage (Default: 15%)**: Free space percentage at which warning alerts and candidate scans begin.
2. **Critical Storage Threshold Percentage (Default: 5%)**: Free space percentage at which automatic sequential compression starts. Must be strictly less than Low Threshold.
3. **Target Free-Space Percentage (Default: 20%)**: Target free space level SmartBin aims to achieve during optimization. Must be strictly greater than Low Threshold.
4. **Hard Safety Floor Margin (Default: 5 GB)**: Safety margin below which SmartBin will never attempt optimization to protect system OS operations.
5. **Pause on Battery Power**: Toggle switch to pause background scans when laptop is running on battery power.

### Interactive Validation:
If an invalid value is entered (e.g. Critical Threshold set higher than Low Threshold, or negative Safety Floor), SmartBin displays an immediate inline warning explaining what is wrong, why it matters, and valid input ranges, while safely forcing Automatic Mode to **OFF**.

---

## 3. Activity History & Auditability

Every storage scan, controlled experiment, compression, restoration, and crash recovery event is persisted in a local SQLite database (`smartbin.db`).

Inspect your activity history on the **Activity History** tab:
- **Timestamp**: Exact time of event.
- **Operation**: Import, Controlled Experiment, Restore, Automatic Optimization, or Recovery.
- **State**: Completed, Failed, Cancelled, or Swept.
- **Reclaimed Bytes**: Storage saved during the operation.
- **Rationale/Details**: Plain-language explainability text detailing why the operation was performed.
