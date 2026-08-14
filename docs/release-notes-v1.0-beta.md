# SmartBin v1.0-beta — Release Notes

SmartBin v1.0-beta is an open-source Windows proof-of-concept exploring adaptive storage efficiency for recoverable deleted files in the Recycle Bin.

---

## What's Included

* **WinUI 3 Dashboard UI:** Real-time storage gauge, candidate list, controlled experiment step-by-step viewer, activity log, and policy settings.
* **11-Stage Controlled Optimization Engine:** Sequential, state-driven candidate acquisition, trial compression, dry-run decompression, and SHA-256 verification.
* **Windows Shell COM Integration:** Native interaction with Windows Recycle Bin (`Shell32.Shell`) for candidate enumeration and non-destructive mutation.
* **Hardened Security & Safety:** Path traversal defenses, reparse-point rejection, configuration auto-normalization, and transaction receipt journal crash recovery.
* **Power & Storage Awareness:** Automatic protection pauses when running on battery power or when available space drops below the 5 GB safety floor.
* **Synthetic Test Data Generator:** Built-in `TestFileGenerator` for safe, reproducible testing without risking real user files.

---

## Test Verification

* **104 automated tests passing** across `SmartBin.Core.Tests` and `SmartBin.Infrastructure.Tests`.
* Zero known test failures or regressions.

---

## Disclosed Limitations

* Windows 10/11 is required for native WinUI 3 dashboard and Shell COM integration.
* Pre-compressed formats (`.jpg`, `.mp4`, `.zip`) provide no additional compression yield and are automatically skipped.
* Automatic background protection is disabled by default and requires manual enablement.
