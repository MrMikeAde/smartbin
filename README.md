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

## Working Prototype (Phase 4)
SmartBin is now integrated safely with the **Windows Recycle Bin**:
- **Windows Recycle Bin Integration**: Safely and securely connects to Windows `ssfBITBUCKET (10)` COM namespaces using standard Windows Shell APIs to enumerate actual Recycle Bin items, original paths, file sizes, deletion dates, and volume origins. Works under normal user privileges (no admin required).
- **Absolute Read-Only Safety**: Enforces a strict read-only design for native items. Modification, compression, deletion, and replacement of real Windows Recycle Bin items are intentionally not implemented in this phase.
- **Explainable Analysis**: Storage intelligence engine (`CandidateAnalyzer`) can analyze simulated and real Windows Recycle Bin items without modifying them, rating them with deterministic priority scores and formatting human-readable rationales (e.g. "Large file, deleted 42 days ago, high expected savings").
- **Simulated Recycle Bin Provider**: Employs an in-memory simulated provider with realistic datasets supporting multi-volume drives for automated validation and headless CI tests.
- **Storage Pressure Monitoring**: Scans available physical and simulated space, grouping constraints into `Normal`, `Low`, and `Critical` states.
- **Batch Planner & Executor**: Sorts eligible candidates and selects the optimal set of files to compress sequentially with active revalidation, cancellation, and early stopping.
- **Safe Import & Restore**: Employs stream-based copy, Deflate compression, SHA-256 integrity verification, and overwrite-protected restoration.

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell (with separate controlled storage and read-only Windows Recycle Bin tabs, supporting conditional headless live demo mode on Linux).
- `src/SmartBin.Core`: Core domain models, state enums, heuristics, priority scorers, batch planners, executors, and simulated Recycle Bin providers.
- `src/SmartBin.Infrastructure`: SQLite database, EF Core persistence, stream-based hashing, storage managers, ZIP compression, and Windows Shell COM Recycle Bin integration.
- `src/SmartBin.Contracts`: Common interfaces, custom exception definitions, and service contracts.
- `tests/`: Extensive automated unit and integration tests validating safe import, heuristics, scoring models, batch planners, simulated Windows Recycle Bin, and atomicity rollbacks.

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
