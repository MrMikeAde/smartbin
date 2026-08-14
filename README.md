# SmartBin

An experimental adaptive Recycle Bin that explores reclaiming storage from recoverable deleted files through intelligent compression.

> **Disclaimer:** SmartBin is an independent experimental project and is not affiliated with Microsoft or Apple. It does not replace the default Windows Recycle Bin.

## Positioning & Trust Scope

To understand SmartBin, it is critical to distinguish what the application is and what it is not.

### What SmartBin IS
- **Adaptive storage protection**: A non-elevated user-mode system utility that monitors drive storage constraints and intelligently compresses eligible deleted files.
- **Byte-for-byte fidelity**: Lossless compression utilizing Deflate algorithms with strict SHA-256 validation to ensure restored files are identical down to the bit.
- **Transactional state machine**: Coordinates single-item operations inside a secure, sequential safety pipeline with automated crash recovery.

### What SmartBin IS NOT
- **Conventional deletion tool**: SmartBin never automatically permanently deletes user data or bypasses Recycle Bin retention.
- **File shredder**: It does not securely overwrite or permanently wipe disks.
- **Cloud backup service**: SmartBin operates 100% locally. It contains no network code, analytics, or telemetry.
- **Blind disk cleaner**: It does not sweep cache folders or arbitrary system locations.
- **Explorer replacement**: It is a companion utility, not a shell replacement or filesystem driver.

---

## Proposed Solution & Workflow
Deleted files remain recoverable inside a secure local storage area. When the user's storage gets constrained, recoverable files are intelligently compressed using algorithms like ZIP, Brotli, or Zstandard, preserving their original path and metadata inside a lightweight database. When a file is restored, SmartBin decompresses it, guaranteeing byte-for-byte identity.

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
exact original 20 GB file
```

---

## Architectural Diagram

```text
     [User Interaction]
             │
             ▼
     [WinUI Dashboard]
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
      [Receipt Journal] ──(Crash Recovery Wal)
```

---

## Working Prototype (Phase 8)
SmartBin is a fully hardened, secure, and production-quality Windows-first adaptive storage solution:
- **Harden Trust Boundaries**: All file writes and reads are locked within the canonical storage root via strict prefix check guards. Reparse points (junctions, symlinks) are explicitly rejected.
- **Defensive Configuration Validation**: User settings implement out-of-range checks on thresholds and defaults, immediately disabling automatic protect modes and falling back to conservative limits if corrupted.
- **Background Disk Space Monitoring**: Observes drive capacity at regular intervals and raises throttled, debounced alerts.
- **Failsafe Safety Floor & Power-Awareness**: Enforces a non-negotiable safety floor margin (default: 5 GB) and pauses optimizations when running on battery.
- **Sequential Commit Boundary**: Mutates Recycle Bin items sequentially, exactly one file at a time, following an 11-stage verification state machine.
- **Crash Recovery & Receipt Journaling**: Reconciles filesystem state with database records upon startup via transactional `.receipt` files to protect against database transaction interruptions.

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell.
- `src/SmartBin.Core`: Core domain models, prioritize planners, heuristics, and state engines.
- `src/SmartBin.Infrastructure`: SQLite repository, Ef Core, ZIP compression, Shell COM mutation services, and Windows power P/Invokes.
- `src/SmartBin.Contracts`: Common interfaces, failure-injection hooks, custom exception definitions, and settings configurations.
- `tests/`: Extensive automated unit, integration, failure-injection, and security regression suites (102 passing tests).

## Development Setup & Testing
1. Clone this repository.
2. Build the solution using:
   ```bash
   dotnet build smartbin.sln
   ```
3. Run all tests (including safety invariants and security hardening tests) using:
   ```bash
   dotnet test smartbin.sln
   ```
4. Run the live dashboard Console/WinUI app:
   ```bash
   dotnet run --project src/SmartBin.App
   ```
