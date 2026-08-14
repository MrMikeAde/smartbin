# SmartBin Safety & Data Integrity

Data safety and absolute file preservation are the highest priorities of SmartBin. As an experimental project, SmartBin adopts defensive principles to ensure zero data loss.

## Core Safety Rules

1. **No Permanent Deletion**: SmartBin never automatically permanently deletes user files. Reclaiming space is only achieved by non-destructive compression.
2. **No Overwrites on Restore**: When restoring a file, if a file already exists at the destination path, SmartBin will never silently overwrite it. It throws a `SmartBinConflictException`, aborting the restoration to protect user data.
3. **No External Modifications**: SmartBin only manages files placed inside its designated controlled storage area. It never alters files in place on the user's desktop or elsewhere.
4. **No Real System Interception**: In this PoC phase, SmartBin does not hook into Windows Explorer or intercept system-level file deletion.
5. **No Administrator Privileges Required**: SmartBin is designed to run in standard user mode without needing elevated permissions, keeping it isolated and secure.
6. **Windows Recycle Bin Read-Only Safety**: During regular operation, all Windows Recycle Bin interactions are kept strictly read-only. SmartBin discovers, reads metadata, and performs read-only priority scoring analysis on Windows items.
7. **Controlled Experiment Commit Boundary**: In the Controlled Experiment mode, a single Windows Recycle Bin item can be safely optimized. The operation must follow a strict sequential state machine:
   - Secure Acquisition (copying content to `temp/`).
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

---

## Tested Safety Invariants (Phase 7 Enforced)

Every execution is governed by ten non-negotiable architectural safety invariants:

### Invariant 1: Acquisition Verification Guard
A real Recycle Bin mutation cannot occur unless acquisition verification succeeded (the copied file matches the size of the Recycle Bin original, and the SHA-256 hash is computed).
*Verified by `Invariant_1_RealMutationBlocked_UnlessAcquisitionVerified`*

### Invariant 2: Compression Verification Guard
A real Recycle Bin mutation cannot occur unless compression verification succeeded (the temporary archive is decompressed and its SHA-256 hash perfectly matches the original).
*Verified by `Invariant_2_RealMutationBlocked_UnlessCompressionVerified`*

### Invariant 3: Restoration Verification Guard
A real Recycle Bin mutation cannot occur unless restoration verification succeeded (the dry-run restored file has been verified byte-for-byte against the original SHA-256).
*Verified by `Invariant_3_RealMutationBlocked_UnlessRestorationVerified`*

### Invariant 4: Continuous Candidate Revalidation
Automatic optimization cannot bypass candidate revalidation. Before executing on any candidate, the background engine re-queries the Recycle Bin to ensure the file still exists, has not been restored, and its size matches the original plan.
*Verified by `Invariant_4_AutomaticOptimization_EnforcesRevalidation`*

### Invariant 5: Safe Storage Claim
SmartBin cannot report actual reclaimed storage before the external mutation and database transaction are successfully completed.
*Verified by `Invariant_5_NoFalsifiedSavingsReported_BeforeCommit`*

### Invariant 6: Overwrite Protection
A restoration operation cannot overwrite an existing destination file automatically. It must abort and throw a `SmartBinConflictException`.
*Verified by `Invariant_6_Restoration_NeverOverwritesDestination`*

### Invariant 7: Ambiguous State Protection
An unknown or interrupted state (such as a database/app crash during mutation) cannot be interpreted as successful. State always defaults to `Failed` or is recovered systematically.
*Verified by `Matrix_CommittedButNotPersisted_ResolvesInconsistencyDuringStartupRecovery`*

### Invariant 8: Policy Enforcement
Automatic optimization cannot run when the user policy is disabled (such as when Settings Mode is OFF or NOTIFY ME).
*Verified by `AutomaticProtectionTests`*

### Invariant 9: Target Storage Stop
Automatic background optimization stops sequentially immediately after the target free space percentage is satisfied.
*Verified by `AutomaticProtection_StopsWhenTargetReached`*

### Invariant 10: Absolute Safety Guarantee
Failures (including database errors, disk full exceptions, system battery pauses, or cancellations) must never silently downgrade the safety guarantees established by earlier phases.
*Verified by the complete `ReliabilityStressFailureTests` Matrix*
