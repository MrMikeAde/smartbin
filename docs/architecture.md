# SmartBin Architecture

This document describes the design and modular architectural layers of **SmartBin**, an adaptive Recycle Bin for Windows.

## Architectural Layers

```
        SmartBin.App (WinUI 3 App / UI)
                    ↓
        SmartBin.Core (Business Logic)
                    ↓
       SmartBin.Contracts (Abstractions)

  SmartBin.Infrastructure (Data Access, OS Services)
                    ↓
        SmartBin.Core (Business Logic)
                    ↓
       SmartBin.Contracts (Abstractions)
```

### 1. SmartBin.Contracts
- **Responsibilities**: Contains only service contracts/interfaces and generic definitions. It has no dependencies.
- **Key Abstractions**:
  - `IFileHasher`: SHA-256 byte-for-byte verification.
  - `ICompressionService`: Abstract compression, decompresion, analysis.
  - `ISmartBinRepository`: SQLite CRUD operations for metadata.
  - `IStorageManager`: Directory and storage area management.
  - `IStoragePathProvider`: Resolves the root of the controlled storage area (supports isolation in testing).
  - `IImportService`: Manages importing files safely without deleting original user source.
  - `ICompressionEngine`: Runs atomic compression, evaluation, and verification.
  - `IStoragePressureMonitor`: Monitors disk pressure and triggers compression pipeline.
  - `IRestoreService`: File restoration.

### 2. SmartBin.Core
- **Responsibilities**: Contains pure domain models (`SmartBinItem`), business rules, value objects, and core decision logic.
- **Key Implementations**:
  - `ImportService`: Executes safe import steps (calculating original hash, copying, matching copied hash, saving metadata, preserving original file).
  - `CompressionEngine`: Runs the atomic compression flow:
    1. Evaluates extension heuristics (`CompressionHeuristics`).
    2. Compresses original file representation to temporary path.
    3. Analyzes savings thresholds (minimum 5% space savings and minimum 1024 bytes saved).
    4. Decompresses temp file and validates SHA-256 hash.
    5. Swap/commit file representation and update DB metadata.
  - `CompressionHeuristics`: Static evaluation of commonly already-compressed extensions (`.zip`, `.rar`, `.mp4`, `.png`, etc.) to skip unnecessary CPU cycles.
- **Constraints**:
  - Pure C# standard project.
  - Strictly **no UI dependencies** (WinUI/Windows App SDK).
  - Strictly **no infrastructure dependencies** (EF Core, SQLite).

### 3. SmartBin.Infrastructure
- **Responsibilities**: Implements contracts using concrete storage engines, frameworks, and system APIs.
- **Key Implementations**:
  - `Sha256FileHasher`: Cryptographic SHA-256 hashing using stream-based APIs.
  - `EfSmartBinRepository` / `SmartBinDbContext`: Uses SQLite and EF Core to store file metadata separately from the actual physical files.
  - `StorageManager`: Safely creates and manages physical storage folders (`objects/`, `temp/`, `metadata/`).
  - `ZipCompressionService`: Implementation of `ICompressionService` using stream-based `System.IO.Compression.DeflateStream` supporting large files.
  - `RestoreService`: Coordinates safe restore: ensures target file does not exist (overwrite protection), copies or decompresses representation to a temporary restoration file, verifies SHA-256 integrity, and renames/moves file to its target path.

### 4. SmartBin.App
- **Responsibilities**: WinUI 3 Application desktop UI providing views, modern Windows 11 dashboards, and conceptual visualizations.
- **Constraints**:
  - Consumes services registered in the Dependency Injection container.
  - Business logic flows entirely through `SmartBin.Core` and `SmartBin.Contracts`.

## Safe File Lifecycle

```
[User-Selected File]
       ↓
Import into SmartBin (ImportService)
       ↓
Calculate original SHA-256
       ↓
Store safely in SmartBin objects/ folder
       ↓
Record Metadata (Uncompressed)
       ↓
Analyze Compression / Heuristics (CompressionEngine)
       ↓
Compress to temp/ file only if worthwhile (threshold check)
       ↓
Decompress & verify SHA-256 matches original
       ↓
Swap file representation & mark database metadata as Compressed
       ↓
Restore on request (RestoreService)
       ↓
Write decompressed data to temp/ restore file
       ↓
Verify restored SHA-256 matches original
       ↓
Move atomically to requested destination (Overwrite protected)
       ↓
Byte-for-byte integrity confirmed
```
