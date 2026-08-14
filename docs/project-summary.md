# SmartBin — Technical Project Summary

A concise technical summary of SmartBin for public presentation, developer forums, and technical communities.

---

## Pitch / Short Summary

When files are deleted to the Windows Recycle Bin, they remain recoverable but continue consuming full physical disk space. Storage pressure often forces users to permanently delete files just to free up space.

**SmartBin** is an open-source Windows proof-of-concept exploring an alternative model: **What if deleted files could stop occupying their full storage footprint without becoming permanently unrecoverable?**

---

## Key Technical Highlights

* **11-Stage Controlled Pipeline:** Operates sequentially one item at a time with strict commit boundaries.
* **Cryptographic Bit-Fidelity:** Pre-compression and post-restoration SHA-256 stream hashing guarantees restored files match original representations down to the bit.
* **Empirical Compression Gains:** Text, JSON, CSV, logs, and source code achieve **76% to 99.8%** physical disk space reduction.
* **Smart Fast-Path Heuristics:** Automatically detects pre-compressed media (`.jpg`, `.png`, `.mp4`, `.zip`) and skips them to avoid wasting CPU resources.
* **Hardened Security Boundaries:** Built-in defenses against path traversal, symbolic link / reparse-point exploitation, and corrupted configurations.
* **Fail-Safe Reliability:** Transaction receipt journaling (`.receipt` WAL) ensures zero data loss or state corruption even during unexpected power loss or crashes.
* **100% Local & Private:** Runs entirely offline with no cloud dependencies, network calls, or telemetry.

---

## Architectural Stack

* **UI:** WinUI 3 (Windows App SDK 1.6 / Fluent Design)
* **Runtime:** .NET 10.0 (C# 13)
* **Database:** SQLite 3 with EF Core 9.0
* **OS Integration:** Windows Shell COM (`Shell32.Shell`) & Win32 P/Invoke
* **Verification Baseline:** 104 passing automated tests

---

## Project Repository

* GitHub: [https://github.com/MrMikeAde/smartbin](https://github.com/MrMikeAde/smartbin)
* License: MIT License
