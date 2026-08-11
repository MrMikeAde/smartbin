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
- **Responsibilities**: Contains only service contracts/interfaces, generic definitions, and DTOs. It has no dependencies.
- **Key Abstractions**:
  - `IFileHasher`: SHA-256 byte-for-byte verification.
  - `ICompressionService`: Abstract compression, decompresion, analysis.
  - `ISmartBinRepository`: SQLite CRUD operations for metadata.
  - `IStorageManager`: Directory and storage area management.
  - `IStoragePathProvider`: Resolves the root of the controlled storage area (supports isolation in testing).
  - `IImportService`: Manages importing files safely without deleting original user source.
  - `ICompressionEngine`: Runs atomic compression, evaluation, and verification.
  - `IStoragePressureMonitor`: Monitors total capacity, free space, percentages, and pressure states.
  - `IOptimizationPlanner`: Deterministic planning of which candidates to compress.
  - `IOptimizationExecutor`: Orchestrates sequential compression of planned candidates, checking space re-evaluation and cancellation.
  - `IRestoreService`: File restoration.

### 2. SmartBin.Core
- **Responsibilities**: Contains pure domain models (`SmartBinItem`), business rules, value objects, and core decision logic.
- **Key Implementations**:
  - `ImportService`: Executes safe import steps.
  - `CompressionEngine`: Runs atomic compression, validation, and swaps.
  - `CompressionHeuristics`: Evaluates file extensions to skip pre-compressed file formats (`.mp4`, `.zip`, etc.).
  - `StoragePressurePolicy`: Converts physical drive space metrics into deterministic recommendations.
  - `CandidateAnalyzer`: Analyzes, ranks, and structures explainable priority scores for all recoverable items.
  - `OptimizationPlanner`: Sorts candidates by priority score and plans the minimal optimal set of files needed to satisfy a target free space.
  - `OptimizationExecutor`: Orchestrates plan sequential processing, checking cancellation, and stopping early if targets are satisfied.
  - `StoragePressureSimulator`: Programs mock overrides to verify different pressure conditions.
- **Constraints**:
  - Pure C# standard project.
  - Strictly **no UI dependencies** (WinUI/Windows App SDK).
  - Strictly **no infrastructure dependencies** (EF Core, SQLite).

### 3. SmartBin.Infrastructure
- **Responsibilities**: Implements contracts using concrete storage engines, frameworks, and system APIs.
- **Key Implementations**:
  - `Sha256FileHasher`: Cryptographic SHA-256 hashing using stream-based APIs.
  - `EfSmartBinRepository` / `SmartBinDbContext`: SQLite database metadata mapping.
  - `StorageManager`: Safely creates and manages physical storage folders (`objects/`, `temp/`, `metadata/`).
  - `ZipCompressionService`: Implementation of `ICompressionService` using Deflate streams.
  - `StoragePressureMonitor`: Scans actual physical disks using `DriveInfo` and maps to configured pressure state thresholds.
  - `RestoreService`: Coordinates safe overwrite-protected restoration.

### 4. SmartBin.App
- **Responsibilities**: WinUI 3 Application desktop UI providing views, modern Windows 11 dashboards, and conceptual visualizations.
- **Constraints**:
  - Consumes services registered in the Dependency Injection container.
  - Business logic flows entirely through `SmartBin.Core` and `SmartBin.Contracts`.

## Safe File Lifecycle & Optimization Pipeline

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
Storage Pressure scan (StoragePressureMonitor)
       ↓
Low / Critical state triggered ──> Recommend Optimization (StoragePressurePolicy)
       ↓
Ranks & scores candidates with explanation (CandidateAnalyzer)
       ↓
Calculates optimization batch (OptimizationPlanner)
       ↓
Executes batch sequentially (OptimizationExecutor)
  - Rechecks space before each candidate; stops early if resolved!
  - Compresses atomically to temp/ file
  - Decompresses and validates SHA-256 checksum
  - Commits DB metadata and deletes uncompressed file
       ↓
Restore on request (RestoreService)
       ↓
Verify restored SHA-256 matches original
       ↓
Byte-for-byte integrity confirmed
```
