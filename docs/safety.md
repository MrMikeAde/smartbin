# SmartBin Safety & Data Integrity

Data safety and absolute file preservation are the highest priorities of SmartBin. As an experimental project, SmartBin adopts defensive principles to ensure zero data loss.

## Core Safety Rules (Phase 4 & Beyond)

1. **No Permanent Deletion**: SmartBin never automatically permanently deletes user files. Reclaiming space is only achieved by non-destructive compression.
2. **No Overwrites on Restore**: When restoring a file, if a file already exists at the destination path, SmartBin will never silently overwrite it. It throws a `SmartBinConflictException`, aborting the process to protect user data.
3. **No External Modifications**: SmartBin only manages files placed inside its designated controlled storage area. It never alters files in place on the user's desktop or elsewhere.
4. **No Real System Interception**: In this PoC phase, SmartBin does not hook into Windows Explorer or intercept system-level file deletion.
5. **No Administrator Privileges Required**: SmartBin is designed to run in standard user mode without needing elevated permissions, keeping it isolated and secure.
6. **Windows Recycle Bin Read-Only Safety**: During this phase, all Windows Recycle Bin operations are kept strictly read-only. SmartBin discovers, reads metadata, and performs read-only priority scoring analysis on Windows items. **It does not perform compression, replacement, deletion, or movement of real Windows Recycle Bin files.**

## Failure Scenarios & Recovery Guarantees

### 1. Atomic Compression Guarantee
- **Scenario**: The system attempts to compress an imported file, but the process gets cut short or fails.
- **Defense**:
  - Compression is performed into a temporary file inside `temp/`.
  - The original uncompressed file in `objects/` remains completely untouched.
  - If compression fails, or the size threshold check is not met, the temp file is deleted, and the original uncompressed representation remains the active primary representation.
  - No database changes are committed until after a successful and fully verified compression.

### 2. Verification Guarantee
- **Scenario**: A compressed file gets corrupted during compression or contains faulty blocks.
- **Defense**:
  - Immediately after compression, the temporary file is decompressed into a temporary validation stream.
  - The SHA-256 hash calculated on the decompressed stream is verified against the immutable original file hash.
  - If the hashes do not match, the archive is discarded (deleted from `temp/`), and the original file is preserved.
  - Only upon successful verification is the uncompressed file deleted and replaced with the compressed representation.

### 3. Atomic Restoration Guarantee
- **Scenario**: Restoring a file to its original path is interrupted midway.
- **Defense**:
  - The file is decompressed/copied to a temporary file in `temp/`.
  - The SHA-256 hash of this temp file is verified against the database.
  - It is then moved atomically to the destination directory. If writing/moving fails, the database remains in a `Pending` state, and the original compressed representation inside SmartBin objects remains fully intact, ensuring no data is ever lost.
  - Any failed restoration cleans up temporary files immediately.

### 4. Storage Intelligence Safeguards
- **Minimum Free Space Protection**: Before any compression occurs, the executor checks the `StoragePressureMonitor`. If the available space has already met the safety target, compression halts immediately to preserve resources.
- **Cancellation**: Both compression and optimization executions actively listen to `CancellationToken`, stopping immediately and cleanly rolling back any temporary files if the user cancels.
- **Stale Plan Safety (Revalidation)**: SmartBin never blindly executes a plan created earlier. Before modifying a file, it re-queries the database to confirm that the file hasn't already been restored or modified in the meantime, skipping any stale candidate targets.
- **No Direct `$Recycle.Bin` Manipulation**: SmartBin does not manually edit directories or modify `$I` or `$R` metadata files directly. All interaction with the native Recycle Bin is routed through official, safe Windows Shell APIs.
