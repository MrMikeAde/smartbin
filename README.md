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

## Working Prototype (Phase 3)
SmartBin is now equipped with **Adaptive Storage Intelligence**:
- **Storage Pressure Monitoring**: Leverages `DriveInfo` system APIs to calculate available space, capacity, and percentage. Maps space conditions to configurable states: `Normal`, `Low`, and `Critical`.
- **Recommendation Policy**: Deterministically evaluates pressure states to output clear optimization action plans.
- **Explainable Scoring & Prioritization**: Ranks files using a deterministic Priority Score formula incorporating size, age, compressibility, and optimization status. Rationale is fully structured and explained (e.g. "Large file, deleted 42 days ago, high expected savings").
- **Batch Optimization Planner**: Selects the minimal optimal set of candidates to compress to satisfy a target space requirement, sorting by priority score first.
- **Dynamic Optimization Executor**: Executes plans sequentially with active revalidation, cancellation checks, and early stopping if space constraints resolve midway.
- **Safe Import & Restore**: Computes stream-based SHA-256 hashes, implements multi-phase atomic temporary file swaps, and provides overwrite-protected restoration.
- **Storage Simulator**: Includes an interactive programmatic simulator to toggle states (`Normal`, `Low`, `Critical`) and metrics to safely dry-run adaptive compression.

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell (supporting conditional headless live demo mode on Linux).
- `src/SmartBin.Core`: Core domain models, state enums, heuristics, priority scorers, batch planners, executors, and simulators.
- `src/SmartBin.Infrastructure`: SQLite database, EF Core persistence, stream-based hashing, storage managers, and ZIP compression service.
- `src/SmartBin.Contracts`: Common interfaces, custom exception definitions, and service contracts.
- `tests/`: 51 automated unit and integration tests validating safe import, heuristics, scoring models, batch planners, executors, restoration integrity, and atomicity rollbacks.

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
4. Run the live demo console simulation (featuring simulated pressure, candidate explanation, batch planning, and actual space recovery updates) using:
   ```bash
   dotnet run --project src/SmartBin.App
   ```
