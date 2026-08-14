# SmartBin — Reproducible Demonstration Protocol

This document defines a step-by-step, reproducible demonstration protocol for testing SmartBin safely using synthetic test data.

> **CRITICAL SAFETY DIRECTIVE:** Demonstration protocols MUST strictly use generated test data produced by `TestFileGenerator`. Never run demonstration workflows on real user documents.

---

## Prerequisites

1. **Operating System:** Windows 10 or Windows 11 (x64 or ARM64).
2. **SDK:** .NET 10.0 SDK installed.
3. **SmartBin Repository:** Cloned and built (`dotnet build smartbin.sln`).

---

## Step-by-Step Demonstration Workflow

### Step 1: Generate Synthetic Test Data
Run the test data generator or automated integration test to create a deterministic test file:

```csharp
// Generates a 10 MB highly compressible synthetic text file
var generator = new TestFileGenerator();
string testFilePath = Path.Combine(Path.GetTempPath(), "SmartBin_Demo_File.txt");
await generator.GenerateCompressibleFileAsync(testFilePath, sizeInBytes: 10 * 1024 * 1024);
```

### Step 2: Record Baseline Metrics & SHA-256
Compute and log the initial file metrics:
* **File Size:** `10,485,760 bytes` (~10 MB)
* **SHA-256 Hash:** Compute stream hash (e.g., `8f3a...b91e`)

### Step 3: Delete File to Windows Recycle Bin
Move the generated test file into the Windows Recycle Bin using Windows Explorer or standard file deletion APIs.

### Step 4: Launch SmartBin Application
Launch the SmartBin WinUI 3 dashboard:
```bash
dotnet run --project src/SmartBin.App
```

### Step 5: Candidate Discovery & Scoring
1. Navigate to the **"Windows Recycle Bin"** tab.
2. Observe `WindowsRecycleBinProvider` discovering the test file (`SmartBin_Demo_File.txt`).
3. Note the candidate score assigned by `CandidateAnalyzer` based on size, file extension, and age heuristics.

### Step 6: Initiate Controlled Experiment
1. Navigate to the **"Controlled Experiment"** tab.
2. Select `SmartBin_Demo_File.txt` from the candidate dropdown.
3. Click **"Prepare Optimization"**.

### Step 7: Observe Pipeline Stages
Watch the live terminal log as the 11-stage pipeline executes:
1. Candidate Acquisition (`.acq` created in temp)
2. Baseline SHA-256 Hash calculation
3. Streaming Deflate Compression (`.zip` created in temp)
4. Decompression SHA-256 Verification
5. Restoration Dry-Run Verification
6. State set to **`ReadyForCommit`**

### Step 8: User Confirmation & Commit
1. Review the estimated storage yield (~99.8% reduction, reclaiming ~10.4 MB).
2. Click **"Commit Optimization"**.
3. Confirm the receipt journal entry creation and original Recycle Bin removal.

### Step 9: Restore the File
1. Navigate to **"SmartBin Storage"**.
2. Locate `SmartBin_Demo_File.txt` and click **"Restore"**.
3. Specify the target restore directory (e.g., `C:\Temp\Restored`).

### Step 10: Verify Byte-for-Byte Restoration
Compute the SHA-256 hash of the restored file and compare it against the Step 2 baseline:
```text
Original Baseline SHA-256 : 8f3a...b91e
Restored File SHA-256    : 8f3a...b91e
Result                   : IDENTICAL (100% Match)
```

---

## Verification Matrix

| Step | Expected Result | Pass Criteria |
| :--- | :--- | :--- |
| Candidate Discovery | Item listed in UI with correct original path & size | Match |
| Trial Compression | Archive size ~15 KB (from 10 MB) | > 99% space yield |
| SHA-256 Verification | Hashes match perfectly | Bit-for-bit identical |
| Commit Boundary | Recycle Bin item removed, metadata logged | Zero orphaned items |
| Restoration | File restored to target directory | Matching SHA-256 |
