# SmartBin v1.0-rc1 — Release Candidate 1 Notes

SmartBin v1.0-rc1 is the official **Release Candidate** for external beta validation. It represents a fully benchmarked, hardened, and documented proof-of-concept for adaptive Recycle Bin storage on Windows.

---

## Capabilities & Engineering Baseline

* **WinUI 3 Dashboard Application:** Built with Fluent Design, real-time storage pressure gauge, controlled single-item experiment tab, activity history log, onboarding guide, and policy settings.
* **11-Stage Controlled Optimization Pipeline:** Sequential, state-driven candidate acquisition, baseline SHA-256 stream hashing, Deflate compression, dry-run decompression, and verification commit boundary.
* **Native Windows Recycle Bin COM Integration:** Communicates with `Shell32.Shell` namespaces (`ssfBITBUCKET`) for candidate discovery and non-destructive COM verb operations.
* **Hardened Security & Safety Invariants:** Canonical path traversal validation, reparse-point (symlink/junction) rejection, overwrite protection on restore, and defensive settings normalization.
* **Crash Recovery & Transaction Journaling:** Transaction receipt journals (`.receipt` WAL) resolve database-external consistency mismatches upon application startup.
* **Background Protection & Power-Awareness:** Throttled background monitor automatically pauses when running on battery power or when available space drops below the 5 GB safety floor.
* **Synthetic Test Data Generator:** Integrated `TestFileGenerator` allows safe, repeatable testing using generated synthetic test files.

---

## Verification & Test Metrics

* **104 total automated tests**
* **104 PASS / 0 FAIL / 0 SKIPPED**
* **Core Logic:** 27 passing tests (`SmartBin.Core.Tests`)
* **Infrastructure & Integration:** 77 passing tests (`SmartBin.Infrastructure.Tests`)

---

## Classification & Validation

* **Release Classification:** Beta Release Candidate (`v1.0-rc1`)
* **OS Target:** Windows 10/11 x64 and ARM64 (Full WinUI 3 + Shell COM) / net10.0 (Headless CI)
* **Packaging:** Unpackaged WinUI 3 Desktop App / MSIX Packaging Configs (`win10-x64`, `win10-arm64`)

---

## Known Disclosed Limitations

1. **Format Dependency:** Pre-compressed file formats (`.jpg`, `.png`, `.mp4`, `.zip`, `.docx`) provide no compression yield and are automatically skipped by fast-path heuristics.
2. **Local Storage Area:** Compressed representations are stored locally at `%LocalAppData%\SmartBin\objects\`. SmartBin is 100% local-first and does not upload files to the cloud.
3. **Single-Item Batch Boundary:** Automatic and manual optimization processes candidates strictly one item at a time to ensure maximum safety.
