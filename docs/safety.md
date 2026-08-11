# SmartBin Safety & Data Integrity

Data safety and absolute file preservation are the highest priorities of SmartBin. As an experimental project, SmartBin adopts defensive principles to ensure zero data loss.

## Core Safety Rules (MVP and Beyond)

1. **No Permanent Deletion**: SmartBin never automatically permanently deletes user files. Reclaiming space must only be achieved by non-destructive compression.
2. **No Overwrites on Restore**: When restoring a file, if a file already exists at the destination path, SmartBin will never silently overwrite it. It will raise an error or prompt for user action.
3. **No External Modifications**: SmartBin only manages files placed inside its designated controlled storage area. It never alters files in place on the user's desktop or elsewhere.
4. **No Real System Interception (MVP)**: In this foundation phase, SmartBin does not hook into Windows Explorer or intercept system-level file deletion.
5. **No Administrator Privileges Required**: SmartBin is designed to run in standard user mode without needing elevated permissions, keeping it isolated and secure.

## Failure Scenarios & Recovery Strategies

### 1. Interrupted Compression / Power Loss
- **Scenario**: The system is compressed, but the process gets cut short.
- **Defense**: Write compression to a temporary file (e.g., `temp_archive.zip`). Only after validation is complete is the original uncompressed file inside SmartBin storage replaced. If interrupted, the original remains, and the incomplete temp file is deleted upon next boot.

### 2. Invalid or Corrupted Archives
- **Scenario**: A compressed file gets corrupted.
- **Defense**: SmartBin requires validating compressed archives before designating them as the primary representation. The SHA-256 hash calculated prior to deletion is verified against the decompressed stream. If hashing fails, the archive is discarded, and the original file is preserved.

### 3. Interrupted Restoration
- **Scenario**: Restoring a file to its original path is interrupted midway.
- **Defense**: Files should be copied to a temporary file at the target directory and then renamed atomically. If renaming or writing fails, the repository state is not changed, preventing corrupt files.
