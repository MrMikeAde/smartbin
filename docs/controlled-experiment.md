# SmartBin Controlled Real-World Optimization Experiment

This document outlines the step-by-step safe procedure for running the first real-world controlled experiment on exactly one Windows Recycle Bin item.

## ⚠ ABSOLUTE SAFETY RULES

1. **NO PRODUCTION DATA**: Never use valuable personal files (photos, databases, source code) for the initial testing. Always use the built-in programmatic test file generator.
2. **EXACTLY ONE ITEM**: Never perform batch, background, or multi-item mutations on the actual Windows Recycle Bin.
3. **DO NOT BYPASS SHELL**: Never manually delete or parse raw files inside `C:\$Recycle.Bin`. All physical actions must go through the verified `WindowsRecycleBinMutationService` wrapping official Shell32 COM APIs.

---

## Recommended Step-by-Step Test Procedure

### Step 1: Generate a Test File
Use the built-in development generator helper to create a safe, highly compressible file:
1. Open SmartBin.
2. In the developer tool, trigger the generation of a `10MB_compressible` test file:
   - Output Path: `C:\SmartBinTemp\experiment_test.txt`

### Step 2: Record original SHA-256
Compute and record the original cryptographic hash of the test file:
- Hash: (Calculated via `Sha256FileHasher`)

### Step 3: Delete the File into Windows Recycle Bin
1. Select the file in Windows Explorer.
2. Press **Delete** (or right-click -> Recycle).
3. Confirm the file is present in the native Windows Recycle Bin.

### Step 4: Launch SmartBin and Discover the Item
1. Open the SmartBin Dashboard.
2. Navigate to the **Windows Recycle Bin** tab.
3. Click **Refresh Bin**.
4. Confirm `experiment_test.txt` is detected with correct size (10 MB), deletion timestamp, and original volume.

### Step 5: Start the Controlled Experiment
1. Select exactly `experiment_test.txt` from the list.
2. Navigate to the **Controlled Experiment Panel**.
3. Confirm the safety check indicators light up green sequentially:
   - `✓ Item Identified`: Confirms item remains active.
   - `✓ Content Acquired`: Streams a secure copy to `temp/`.
   - `✓ SHA-256 Calculated`: Original hash computed.
   - `✓ Compression Completed`: Deflates copy to temporary zip.
   - `✓ Compressed Representation Verified`: Decompresses zip and checks SHA-256.
   - `✓ Restoration Test Passed`: Decompresses to a mock dry-run path and verifies hash byte-for-byte.

### Step 6: Trigger User Confirmation Commit
1. The status will transition to `READY FOR COMMIT`.
2. Review the metrics:
   - Original size: `10,485,760 bytes`
   - Stored size: `~15,000 bytes` (99.8% savings!)
3. Under English Windows (where Shell32 COM Verbs are supported), check the **Execute Windows Mutation** option and click **Commit Controlled Experiment**.
4. On non-Windows platforms (or if COM verbs are not ready), click **Keep SmartBin Copy** to safely save the compressed item inside SmartBin without deleting the simulated Recycle Bin file.

### Step 7: Perform Overwrite-Protected Restore
1. Select the committed item from the SmartBin Storage tab.
2. Click **Restore Selected** and select your target directory (e.g. `C:\SmartBinRestored\`).
3. Verify that:
   - The file is restored successfully.
   - The final restored file's SHA-256 is recalculated and matches the original hash perfectly.
   - Core integrity is confirmed!
