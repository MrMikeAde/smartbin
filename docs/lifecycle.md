# SmartBin — Application Lifecycle & Safe Operations

This document establishes the operational lifecycle contract of SmartBin, detailing its behavior from first launch to uninstall/updates.

## 1. Safe Lifecycle Stages

### First Run
- **Deactivated state**: Background monitoring is initialized, but automatic protection is **OFF by default**.
- **Welcome onboarding**: Explains the trust boundaries, safety defaults, and asks for explicit confirmation before automatic settings are enabled.
- **Safety floor validation**: Queries initial drive capacity to ensure the safety floor margin (5 GB) is available.

### Normal Running
- Sequentially processes background checks in non-blocking loops.
- Pauses automatic checks if battery power is active and configured.

### Interrupted/Abrupt Shutdown
- If SmartBin is closed midway during compression, decompression, or restoration, the filesystem is preserved:
  - **No partial files accepted**: Intermediate files reside inside `temp/`.
  - **Reconciliation**: On restart, `CrashRecoveryService` sweeps `temp/` to wipe `.acq`, `.zip`, `.unzip`, and `.restore` files.
  - **Journal recovery**: Any `.receipt` file with a completed physical compressed representation and missing SQLite DB record is safely synced back into the database, guaranteeing no data loss.

---

## 2. Safe Uninstall / Reinstall Contract

Uninstalling SmartBin must **NEVER silently destroy user-recoverable data** created during its lifecycle.

- **Uncompressed Restoration Prompt**: During uninstallation, the uninstaller should prompt the user:
  > "Do you want to restore all compressed recoverable files inside SmartBin back to their original locations before uninstalling?"
- **Preserve SQLite DB option**: Allows keeping settings and metadata databases in `%LOCALAPPDATA%` so reinstalling resolves existing files correctly.
- **Wipe Temp**: Wipes any outstanding cached files in `temp/`.
- **No file shredding**: SmartBin never permanently deletes or shreds objects; the compressed data is left intact on disk unless the user explicitly requests cleanup.
