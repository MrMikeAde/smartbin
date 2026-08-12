# SmartBin Safety & Data Integrity

Data safety and absolute file preservation are the highest priorities of SmartBin. As an experimental project, SmartBin adopts defensive principles to ensure zero data loss.

## Core Safety Rules (Phase 6 & Beyond)

1. **No Permanent Deletion**: SmartBin never automatically permanently deletes user files. Reclaiming space is only achieved by non-destructive compression.
2. **No Overwrites on Restore**: When restoring a file, if a file already exists at the destination path, SmartBin will never silently overwrite it. It throws a `SmartBinConflictException`, aborting the process to protect user data.
3. **No External Modifications**: SmartBin only manages files placed inside its designated controlled storage area. It never alters files in place on the user's desktop or elsewhere.
4. **No Real System Interception**: In this PoC phase, SmartBin does not hook into Windows Explorer or intercept system-level file deletion.
5. **No Administrator Privileges Required**: SmartBin is designed to run in standard user mode without needing elevated permissions, keeping it isolated and secure.
6. **Windows Recycle Bin Read-Only Safety**: During regular operation, all Windows Recycle Bin interactions are kept strictly read-only. SmartBin discovers, reads metadata, and performs read-only priority scoring analysis on Windows items.
7. **Controlled Experiment Commit Boundary**: In the Controlled Experiment mode, a single Windows Recycle Bin item can be safely optimized. The operation must follow a strict sequential state machine:
   - Secure Acquisition (coping content to `temp/`).
   - Acquisition Verification (size check & original SHA-256 calculation).
   - Atomic Compression (deflating to temporary file).
   - Decompression Verification (decompressing and confirming SHA-256 matches original perfectly).
   - Restoration Dry-Run (restoring to dry-run temp path and re-confirming SHA-256).
   - Commit Boundary (presenting `ReadyForCommit` status to the user).
   - Explicit User Confirmation (the original Recycle Bin item is only ever modified *after* all previous checks pass and the user gives explicit, manual confirmation to commit).
   - Rollback Protection (any failure at any step before the final committed stage immediately cleans up all temporary files and leaves the original Recycle Bin item completely untouched).
8. **Automatic Optimization Safety Floor**: In automatic background protection mode, SmartBin enforces strict, non-negotiable safety conditions:
   - **Mode Disabled by Default**: Automatic protection is strictly OFF by default and must be manually activated.
   - **Safety Floor / Safety Margin Protection**: If the available space is below the hard safety floor (default: 5 GB), automatic compression is completely disabled, protecting system resources from starvation.
   - **Power-Awareness**: Background optimizations are automatically paused when the system is running on battery power to prevent power draining.
   - **One-Item-at-a-Time Limit**: Restricts automatic processing to exactly one file per cycle, sequentially rechecking drive space and revalidating candidates (confirming they still exist, sizes match, and paths match) before every single operation.

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

### 4. Crash Recovery & Ambiguous Artifacts Cleanup
- **Scenario**: The application crashes or the system shuts down during a background optimization.
- **Defense**:
  - Upon next boot, the `CrashRecoveryService` automatically scans the controlled `temp/` folder.
  - Any intermediate `.acq`, `.zip`, `.unzip`, `.restore`, or `.dryrestore` files are safely identified and cleaned up.
  - Ensures no corrupted or half-written temporary files are ever locked or assumed successful, keeping the system state 100% clean and consistent.
