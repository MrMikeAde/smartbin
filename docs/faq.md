# SmartBin — Technical FAQ

This document answers common technical questions about SmartBin's architecture, safety mechanisms, and design choices.

---

### 1. Why not simply empty the Recycle Bin?
Emptying the Recycle Bin permanently destroys files, making them unrecoverable through standard operating system tools. SmartBin explores a middle ground: reducing the storage footprint of recoverable files while preserving their complete restoration path and cryptographic integrity.

### 2. Does SmartBin delete my files?
No. SmartBin never permanently deletes files during optimization. It replaces the uncompressed representation in the Recycle Bin with a compressed representation stored in SmartBin's local storage folder (`%LocalAppData%\SmartBin\objects`). The file remains fully restorable.

### 3. Where does SmartBin store optimized data?
Optimized file representations are stored in a local user-isolated folder at `%LocalAppData%\SmartBin\objects\`. Metadata and activity logs are persisted in an embedded SQLite database at `%LocalAppData%\SmartBin\smartbin.db`.

### 4. Can SmartBin restore files?
Yes. When a user requests restoration, SmartBin decompresses the stored object, verifies its SHA-256 cryptographic hash against the pre-compression baseline hash, and writes it back to the target destination path.

### 5. What happens if compression makes the file larger?
SmartBin's `CompressionEngine` checks space yield before committing. If compression fails to yield at least 5% space reduction (such as on random binary data), SmartBin automatically rolls back the trial operation, leaving the original item untouched in the Recycle Bin and marking it `NotFeasible`.

### 6. What happens if storage runs out?
SmartBin includes a strict safety floor policy (defaulting to 5 GB free space). If available disk space falls below this threshold, automatic background optimization immediately halts to avoid competing for scarce system resources.

### 7. What happens if a file changes?
While a file resides in the Windows Recycle Bin, it is static. SmartBin re-validates candidate metadata (file size, creation time, path) immediately prior to execution. If a candidate file was modified, moved, or deleted outside SmartBin, execution is safely aborted.

### 8. Does SmartBin require administrator privileges?
No. SmartBin runs entirely in standard non-elevated user mode. It interacts with the Windows Recycle Bin using standard Windows Shell COM APIs (`Shell32.Shell`) and non-elevated Win32 APIs.

### 9. Does SmartBin send files to the cloud?
No. SmartBin is 100% local-first and offline. It contains zero network code, zero cloud synchronization APIs, and zero telemetry or analytics collectors.

### 10. Does SmartBin work with every file type?
SmartBin can process any file type, but compression efficiency varies by content. Text, JSON, CSV, logs, and source code achieve 76% to 99.8% space reduction. Pre-compressed formats (such as JPEG, PNG, MP4, and ZIP) are automatically identified by fast-path heuristics and skipped.

### 11. Is SmartBin a backup system?
No. SmartBin is an experimental utility for managing local Recycle Bin storage footprint. It is not a backup solution and should not replace proper offsite or system backups.

### 12. Can SmartBin replace the Windows Recycle Bin?
No. SmartBin is a companion prototype that interacts with the Windows Recycle Bin via Shell COM APIs. It does not replace the Windows Recycle Bin shell extension or operating system filesystem drivers.

### 13. What happens when SmartBin is uninstalled?
Uninstalling SmartBin removes the application binaries. Any compressed items currently managed in SmartBin's storage area can be restored to their original locations prior to uninstallation using SmartBin's UI or built-in restore utility.
