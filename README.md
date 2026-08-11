# SmartBin

An experimental adaptive Recycle Bin that explores reclaiming storage from recoverable deleted files through intelligent compression.

> **Disclaimer:** SmartBin is an independent experimental project and is not affiliated with Microsoft or Apple. It does not replace the default Windows Recycle Bin.

## The Problem
When modern computer storage becomes constrained, users are forced to permanently delete files, risking the loss of recoverable assets.

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

## Working Prototype (Phase 5)
SmartBin includes a fully functional **Controlled Real-World Optimization Proof**:
- **Controlled Experiment Mode**: Safely runs a highly structured, 11-step verification experiment on exactly ONE selected Windows Recycle Bin item. No background or batch optimization occurs.
- **Strict State Machine**: Tracks transitions dynamically (`Discovered`, `Acquired`, `AcquisitionVerified`, `Compressed`, `CompressionVerified`, `RestorationVerified`, `ReadyForCommit`, `Committed`, `Failed`, etc.) to guarantee absolute safety and recoverability.
- **Secure Acquisition & Hashing**: Streams file contents securely into `temp/` without modifying the original item. Calculates deterministic original SHA-256 and checks file size matches exactly.
- **Verification Dry-Runs**: Automatically compresses, decompresses, and performs a restoration dry-run in temporary storage, verifying SHA-256 byte-for-byte, before ever considering a commit.
- **Explicit Commit Boundary**: Transitions to `READY FOR COMMIT`. The user must give explicit manual confirmation before any final commit.
- **Safe COM Verb Mutation**: Connects to Windows `ssfBITBUCKET (10)` Shell COM APIs to safely trigger single-item undelete (`InvokeVerb("restore")`) and permanent deletion (`InvokeVerb("delete")`) under standard user permissions, providing robust file rollbacks on any failure.
- **Test File Generator**: Includes a built-in generator to easily create compressible/incompressible test files (10MB, 100MB, 500MB, 1GB) for safe verification.
- **Storage Simulator**: Includes an interactive programmatic simulator to dry-run different pressure constraints safely.

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell (with separate controlled storage, read-only Windows Recycle Bin, and Controlled Experiment tabs, supporting conditional headless live demo mode on Linux).
- `src/SmartBin.Core`: Core domain models, state enums, heuristics, priority scorers, batch planners, executors, simulated Recycle Bin providers, test file generators, and Phase 5 Controlled Experiment engines.
- `src/SmartBin.Infrastructure`: SQLite database, EF Core persistence, stream-based hashing, storage managers, ZIP compression, and native Windows Shell COM Recycle Bin mutation services.
- `src/SmartBin.Contracts`: Common interfaces, custom exception definitions, and service contracts.
- `tests/`: 59 automated unit and integration tests validating safe import, heuristics, scoring models, batch planners, simulated Windows Recycle Bin, and Phase 5 state machine rollbacks.

## Safety Philosophy
Data integrity is our highest priority.
- No permanent deletion in MVP.
- Overwrite protection on restoration.
- No elevation / administrative privilege requests.
- Transactional metadata matching the exact state of physical files.

## Development Setup & Testing
1. Clone this repository.
2. Build the solution using:
   ```bash
   dotnet build smartbin.sln
   ```
3. Run unit and integration tests using:
   ```bash
   dotnet test smartbin.sln
   ```
4. Run the live demo console simulation (featuring simulated pressure, candidate explanation, batch planning, read-only Windows Recycle Bin enumeration, and actual space recovery updates) using:
   ```bash
   dotnet run --project src/SmartBin.App
   ```
