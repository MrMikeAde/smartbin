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

## Working Prototype (Phase 2)
SmartBin is now a fully functional core file-lifecycle and compression prototype:
- **Safe Import**: Users can import any selected file into the controlled storage directory safely.
- **SHA-256 Hashing**: Calculates deterministic hashes using buffered streaming I/O suitable for large files.
- **Compression Heuristics**: Inspects file extensions (`.zip`, `.mp4`, `.png`, etc.) to skip compression on already-optimized formats.
- **Threshold Decision Logic**: Only compresses if it meets configurable thresholds (default: at least 5% savings and 1024 bytes saved).
- **Atomic Compression**: Writes to a temporary file first, verifies decompression checksum matches original, then deletes uncompressed representation and updates SQLite metadata.
- **Safe Overwrite-Protected Restore**: Decompresses or copies to a temporary restoration file, verifies SHA-256 checksum byte-for-byte, and moves atomically to destination. If target destination already exists, aborts with a clear conflict exception.

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell (supporting conditional headless live demo mode on Linux).
- `src/SmartBin.Core`: Core domain models, state enums, heuristics, and safe import/compression engine coordinator services.
- `src/SmartBin.Infrastructure`: SQLite database, EF Core persistence, stream-based hashing, storage managers, and ZIP compression service.
- `src/SmartBin.Contracts`: Common interfaces, custom exception definitions, and service contracts.
- `tests/`: Extensive automated unit and integration tests validating safe import, compression heuristics, thresholds, restoration integrity, and atomicity rollbacks.

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
4. Run the live demo console simulation using:
   ```bash
   dotnet run --project src/SmartBin.App
   ```
