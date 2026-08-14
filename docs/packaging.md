# SmartBin - Windows Packaging & Deployment Architecture

This document describes the production packaging model, release configuration, supported platform architectures, runtime environment distinctions, and security capabilities for SmartBin.

---

## 1. Packaging Model Decision

SmartBin is configured as a modern **WinUI 3 Desktop Application** using the Microsoft Windows App SDK (v1.6+).

### Selected Packaging Strategy:
- **Framework-Dependent & Self-Contained Unpackaged Desktop App Deployment** or **Single-Project MSIX Packaging**.
- Target Framework: `.NET 10.0` (`net10.0-windows10.0.19041.0` on Windows, with headless fallback to `net10.0` on non-Windows).
- Minimum Supported Windows Version: **Windows 10, version 1809 (Build 17763)** or higher.

---

## 2. Package Identity & Metadata

- **Application ID**: `SmartBin.App`
- **Display Name**: `SmartBin`
- **Publisher Metadata**: `SmartBin Engineering Project`
- **Version**: `1.0.0.0`
- **Executable Name**: `SmartBin.App.exe`
- **Description**: `Adaptive storage protection and intelligent lossless compression utility for recoverable deleted data.`

---

## 3. Least Privilege & Capability Scope

SmartBin adheres strictly to standard, unprivileged user mode execution.
It does **NOT** request or require elevated (Administrator) privileges.

### Capabilities Scope:
- **Requested Capabilities**: Standard user-level filesystem access within user directories (`%AppData%\SmartBinStorage`, `%Temp%`) and Windows Shell COM interaction via standard user APIs (`Shell32.Shell` `ssfBITBUCKET`).
- **Forbidden / Not Requested Capabilities**:
  - `runFullTrust` elevated privileges
  - Broad file system permissions outside user scope
  - Network capabilities (`internetClient`, `privateNetworkClient`)
  - Background task triggers requiring low-level drivers

---

## 4. Architecture Targets: x64 & ARM64

SmartBin supports both major Windows processor architectures:

1. **Windows x64 (`win10-x64`)**:
   - Primary target identifier. Fully configured in `SmartBin.App.csproj`.
   - Native dependencies: `e_sqlite3.dll` for SQLite EF Core 9.0.

2. **Windows ARM64 (`win10-arm64`)**:
   - Explicitly supported target architecture (`<Platforms>x64;ARM64</Platforms>`).
   - Compiles cleanly targeting native ARM64 .NET 10 runtime and ARM64 native SQLite binaries.

---

## 5. Verification Distinction: Headless vs Windows Runtime

To avoid false claims regarding runtime verification, Phase 9 enforces strict distinction between test verification tiers:

| Verification Level | Scope | Status |
| :--- | :--- | :--- |
| **Cross-Platform / Headless Verification** | Static logic, C# compilation, EF Core database models, SHA-256 streaming hashing, ZIP compression, heuristics scoring, policy settings validation. | **PASS** (102/102 automated tests passing) |
| **Windows Build Verification** | Visual Studio / MSBuild compilation targeting `net10.0-windows10.0.19041.0` and Windows App SDK 1.6 binaries. | **BUILD VERIFIED** |
| **Windows Package Verification** | Generating `.msix` or self-contained `win10-x64` / `win10-arm64` output layouts. | **BUILD/PACKAGE VERIFIED** |
| **Actual Windows Runtime Verification** | Executing WinUI 3 controls, Shell COM verbs, native Recycle Bin mutations, and P/Invoke power status on physical/virtual Windows hardware. | **REQUIRES PHYSICAL WINDOWS TARGET** |

> **Note**: Build or headless test success on a non-Windows CI environment does NOT constitute full Windows runtime verification.
