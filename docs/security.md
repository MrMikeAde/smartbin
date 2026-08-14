# SmartBin — Security hardening Details

This document compiles the comprehensive security reviews, threat modeling, and defensive measures implemented in SmartBin Phase 8.

## Threat Analysis & Mitigations

### 1. Path Traversal & Arbitrary File Overwrites
- **Threat**: An attacker could manipulate Recycle Bin file names (such as containing `..` or nesting directory separators) to force SmartBin to write files outside of its base folders, potentially overwriting Windows system or user files.
- **Mitigation**:
  - **Base prefix checks**: All temporary and permanent object files constructed dynamically are verified to start strictly with the authorized base path via canonicalized string prefix matching.
  - **`Path.GetFullPath` validation**: Dest paths are canonicalized and tested using `StartsWith` on `GetStoragePath()`.
  - **No silent overwrites**: `RestoreService` and `StorageManager` explicitly reject overwriting files. If a file exists, they throw a `SmartBinConflictException` to halt execution.

### 2. Reparse Point Following (Symlink Exploitation)
- **Threat**: An attacker constructs symbolic links or junctions pointing to sensitive system folders (e.g., `C:\Windows\System32`) inside target restore folders or temporary paths, tricking SmartBin into copying or writing files into protected regions.
- **Mitigation**:
  - **File Attribute Inspection**: SmartBin checks the file attributes of source files, temp files, and target locations before copying or copying streams.
  - **Explicit Rejection**: If `(attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint` is true, the operation is immediately aborted with an `InvalidOperationException`.

### 3. MALFORMED / UNTRUSTED CONFIGURATIONS
- **Threat**: A user's settings file gets corrupted or intentionally modified with out-of-bounds metrics (e.g. negative space thresholds, 5,000 max items) causing runaway resource consumption.
- **Mitigation**:
  - **Range bounds checks**: `SmartBinSettings` implements `ValidateAndNormalize()` which asserts limits (percentages between 1-99%, limits on items, non-negative floors).
  - **Fail-Safe Mode**: If any setting is out of bounds or untrusted, settings are reset to safe defaults, and background automatic protection is forced **OFF** to prevent unsafe behaviors.

### 4. PRIVILEGE ESCALATION
- **Threat**: Running with Administrator privileges exposes the system to elevated file operations.
- **Mitigation**:
  - **User-Mode Restriction**: SmartBin explicitly runs in standard user mode. No administrative prompt is requested or accepted. All native P/Invokes and COM verb calls utilize non-elevated user permissions.
