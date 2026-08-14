# SmartBin

An experimental adaptive Recycle Bin utility for Windows that explores reclaiming storage from recoverable deleted files through intelligent, lossless compression.

> **Disclaimer:** SmartBin is an independent experimental project and is not affiliated with Microsoft Corporation. It does not replace the default Windows Recycle Bin, but operates as an adaptive storage assistant.

---

## Positioning & Trust Scope

To understand SmartBin, it is critical to distinguish what the application is and what it is not.

### What SmartBin IS
- **Adaptive storage protection**: A non-elevated user-mode system utility that monitors drive storage constraints and intelligently compresses eligible deleted files.
- **Byte-for-byte fidelity**: Lossless compression utilizing ZIP algorithms with strict SHA-256 validation to ensure restored files are identical down to the bit.
- **Transactional state machine**: Coordinates single-item operations inside a secure, sequential safety pipeline with automated crash recovery.
- **100% Local & Private**: Operates entirely offline without cloud accounts, network connections, or telemetry.

### What SmartBin IS NOT
- **Conventional deletion tool**: SmartBin never automatically permanently deletes user data or bypasses Recycle Bin retention.
- **File shredder**: It does not securely overwrite or permanently wipe disks.
- **Cloud backup service**: SmartBin operates 100% locally. It contains no network code, analytics, or telemetry.
- **Blind disk cleaner**: It does not sweep cache folders or arbitrary system locations.
- **Explorer replacement**: It is a companion utility, not a shell replacement or filesystem driver.

---

## Proposed Solution & Workflow

Deleted files remain recoverable inside a secure local storage area. When storage becomes constrained, recoverable files are losslessly compressed, preserving their original path and metadata inside a lightweight database. When a file is restored, SmartBin decompresses it, guaranteeing byte-for-byte identity via SHA-256 validation.

```text
20 GB deleted file
        ↓
intelligent compression
        ↓
6 GB stored
        ↓
14 GB temporarily reclaimed
        ↓
restore
        ↓
exact original 20 GB file (verified SHA-256)
```

---

## Architectural Diagram

```text
     [User Interaction]
             │
             ▼
     [WinUI 3 Dashboard]
             │
             ▼
   [Storage Policy / Intel] ──(Checks Space/Power)
             │
             ▼
   [Optimization Planner] ──(Prioritizes Candidates)
             │
             ▼
    [Phase 5 Safety Pipeline] ──(Controlled State Machine)
             │
             ▼
   [Windows Shell Mutation] ──(COM APIs ssfBITBUCKET)
             │
             ▼
     [Verification Stage] ──(SHA-256 Stream Hashing)
             │
             ▼
      [Receipt Journal] ──(Crash Recovery WAL)
```

---

## Phase 9 Release Capabilities

SmartBin Phase 9 transforms the hardened engineering foundation into a shippable Windows application:
- **Polished WinUI 3 Dashboard**: Features real-time storage pressure visualization, system state indicators, SmartBin engine metrics, and live log terminal.
- **Onboarding & First-Run Experience**: Dedicated Welcome Guide tab explaining product purpose, safety guarantees, local storage boundary, and verifying automatic protection is OFF by default.
- **Settings & Validation UX**: Interactive policy controls for thresholds, safety floor, and battery awareness with immediate inline input validation.
- **Safe Controlled Demonstration Helper**: Integrated programmatic test file generator allowing safe, repeatable demonstration using synthetic test data.
- **Multi-Platform Support**: Configured for both Windows x64 (`win10-x64`) and Windows ARM64 (`win10-arm64`) architectures.
- **Comprehensive Documentation**: Complete suite of guides including `packaging.md`, `installation.md`, `demo.md`, `user-guide.md`, and `phase-9-checklist.md`.

---

## Project Structure
- `src/SmartBin.App`: WinUI 3 desktop application dashboard shell.
- `src/SmartBin.Core`: Core domain models, prioritize planners, heuristics, and state engines.
- `src/SmartBin.Infrastructure`: SQLite repository, EF Core 9.0, ZIP compression, Shell COM mutation services, and Windows power P/Invokes.
- `src/SmartBin.Contracts`: Interface contracts, failure-injection hooks, exception definitions, and settings models.
- `tests/`: Automated unit and integration test suites (102 passing tests).
- `docs/`: Technical specifications, security trust boundaries, packaging guides, user guides, and demonstration protocols.

---

## Development Setup & Testing

1. Clone this repository:
   ```bash
   git clone https://github.com/MrMikeAde/smartbin.git
   cd smartbin
   ```
2. Build the solution:
   ```bash
   dotnet build smartbin.sln
   ```
3. Run all automated test suites (102 passing tests):
   ```bash
   dotnet test smartbin.sln
   ```
4. Run the application:
   ```bash
   dotnet run --project src/SmartBin.App
   ```
