# SmartBin — Technical Limitations & Disclosures

To maintain absolute technical honesty, this document discloses all known limitations, environmental requirements, and scope boundaries of the SmartBin proof-of-concept.

---

## 1. Content & Compression Limitations

* **Pre-Compressed File Formats:** Formats that already use heavy entropy coding or compression (e.g., `.jpg`, `.png`, `.mp4`, `.zip`, `.7z`, `.gz`, `.docx`, `.xlsx`) provide little to no compression yield. SmartBin automatically fast-path skips these formats using extension heuristics.
* **Incompressible Data Rollback:** Encrypted files, high-entropy random binary data, or pre-compressed streams that do not yield at least 5% space savings are rolled back and marked `NotFeasible`.
* **CPU vs. Compression Tradeoff:** SmartBin uses standard streaming Deflate/ZIP compression. Extremely high-ratio compression algorithms (e.g. LZMA2 level 9) are not used to keep CPU and memory overhead low during background execution.

---

## 2. Platform & OS Limitations

* **Windows Operating System Dependency:** SmartBin relies on native Windows Shell COM APIs (`Shell32.Shell` namespaces like `ssfBITBUCKET`) and Win32 P/Invoke APIs (`GetSystemPowerStatus`). Full functional execution requires Windows 10 or Windows 11.
* **Non-Windows Execution Limits:** On non-Windows OS environments (e.g. Linux/macOS CI builders), UI components are excluded, and Recycle Bin calls revert to `SimulatedRecycleBinProvider` for headless unit and integration testing.
* **Filesystem Scope:** SmartBin operates on local fixed NTFS/ReFS drives. Removable drives, network shares (SMB/NFS), and cloud-synced virtual files (OneDrive Files On-Demand) are excluded from processing.

---

## 3. UI & Packaging Limitations

* **WinUI 3 Unpackaged Desktop App:** The prototype runs as an unpackaged WinUI 3 desktop application. Standard MSIX packaging configs exist in the solution (`win10-x64`, `win10-arm64`), but signing requires a developer certificate.
* **Single-Item Batch Operations:** To maximize data safety, the controlled optimization engine operates strictly one candidate item at a time. Mass bulk optimization is intentionally throttled.

---

## 4. Scope & Functional Disclosures

* **Not a File Recovery Tool:** SmartBin cannot recover files that were already permanently deleted before SmartBin was launched or files deleted via `Shift + Delete`.
* **Not an Antivirus or File Shredder:** SmartBin does not scan for malware or perform DOD-compliant multi-pass disk sanitization.
