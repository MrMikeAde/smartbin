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

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell.
- `src/SmartBin.Core`: Core domain models, state enums, and compression decision logic.
- `src/SmartBin.Infrastructure`: SQLite database, EF Core persistence, file hasher implementation.
- `src/SmartBin.Contracts`: Common interfaces and service definitions.
- `tests/`: Project tests validating hashing, repository CRUD, and compression logic.

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
   dotnet build
   ```
3. Run unit tests using:
   ```bash
   dotnet test
   ```
