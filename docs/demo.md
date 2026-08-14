# SmartBin - Controlled Demonstration Script

This document provides a step-by-step repeatable demonstration protocol using programmatically generated test data.

---

## ⚠ Demonstration Safety Guarantees

- **DO NOT USE PERSONAL USER FILES**: All testing should be performed using SmartBin's built-in programmatic test data generator.
- **SINGLE-ITEM BOUNDARY**: SmartBin only operates on one item at a time during controlled experiment proofs.
- **100% BYTE-FOR-BYTE FIDELITY**: Every restored file's SHA-256 hash is computed and validated against its pre-compression hash before completion.

---

## Step-by-Step Demonstration Protocol

### Step 1: Launch SmartBin
1. Launch `SmartBin.App.exe`.
2. Confirm the dashboard loads and displays current storage utilization metrics.

### Step 2: Generate Safe Test Data
1. Navigate to the **Controlled Experiment** tab.
2. In the Demonstration Helper panel, select **"10 MB highly compressible text file"** from the drop-down menu.
3. Click **Generate & Delete to Bin**.
4. SmartBin generates `demo_test_XXXXXX.txt` in a temporary folder and registers it as a Recycle Bin candidate.

### Step 3: Discover and Inspect Candidate
1. Switch to the **Windows Recycle Bin** tab.
2. Verify that `demo_test_XXXXXX.txt` is listed with its original size (10,485,760 bytes) and deletion timestamp.
3. Select the file in the list.
4. Review the Terminal box to observe the plain-language explainability scoring and priority rationale.

### Step 4: Initiate Controlled Experiment
1. Navigate back to the **Controlled Experiment** tab.
2. Verify that `demo_test_XXXXXX.txt` is displayed as the target file.
3. Click **Begin Controlled Test**.

### Step 5: Observe Phase 5 Safety Pipeline Execution
Watch the 6-stage checklist update dynamically:
- `✓ Candidate Item Identified`
- `✓ Safe Extraction/Acquisition`
- `✓ Integrity SHA-256 Calculated`
- `✓ Lossless Compression Done` (Compresses ~10 MB file down to ~15 KB!)
- `✓ Compressed Integrity Verified`
- `✓ Restoration Integrity Verified`

### Step 6: Commit the Operation
1. Observe the status change to **"Status: READY FOR COMMIT"**.
2. Keep **Execute Windows Mutation** checked if testing Windows COM mutation, or uncheck to retain the original Recycle Bin entry.
3. Click **Commit Controlled Experiment**.
4. Terminal confirms: `✓ Controlled Experiment Committed successfully.`

### Step 7: Restore Item and Verify Byte-for-Byte Fidelity
1. Navigate to the **SmartBin Storage** tab.
2. Select `demo_test_XXXXXX.txt` from the list.
3. Click **Restore Selected** and choose a target destination directory (e.g. `Downloads` or `Documents`).
4. Terminal logs:
   - `Restoring...`
   - `Verifying integrity...`
   - `✓ Restored successfully: demo_test_XXXXXX.txt`
   - `SHA-256 verified: [64-character hash]`
5. Confirm the restored file opens perfectly and matches original size and hash down to the bit!
