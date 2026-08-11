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
  - `IStoragePathProvider`: Resolves the root of the controlled storage area.
  - `IImportService`: Manages importing files safely.
  - `ICompressionEngine`: Runs atomic compression, evaluation, and verification.
  - `IStoragePressureMonitor`: Monitors drive space and pressure states.
  - `IOptimizationPlanner`: Planning of candidate batches.
  - `IOptimizationExecutor`: Orchestrates plan batch processing.
  - `IRecycleBinProvider`: Specifies the contract for enumerating and analyzing Windows Recycle Bin items safely.
  - `IRestoreService`: File restoration.

### 2. SmartBin.Core
- **Responsibilities**: Contains pure domain models (`SmartBinItem`), business rules, value objects, and core decision logic.
- **Key Implementations**:
  - `ImportService`: Executes safe import steps.
  - `CompressionEngine`: Runs atomic compression.
  - `CompressionHeuristics`: Evaluates pre-compressed file extensions.
  - `StoragePressurePolicy`: Converts space metrics into recommendations.
  - `CandidateAnalyzer`: Evaluates, scores, and prioritizes both local and external Windows Recycle Bin items with detailed explanation rationales.
  - `OptimizationPlanner`: Batch planning of candidates.
  - `OptimizationExecutor`: Sequential batch orchestrator.
  - `StoragePressureSimulator`: Programs mock metrics overrides.
  - `SimulatedRecycleBinProvider`: Fakes realistic Windows Recycle Bin metadata for automated testing and simulation.

### 3. SmartBin.Infrastructure
- **Responsibilities**: Implements contracts using concrete storage engines, frameworks, and system APIs.
- **Key Implementations**:
  - `Sha256FileHasher`: Cryptographic stream-based hashing.
  - `EfSmartBinRepository` / `SmartBinDbContext`: SQLite database metadata mapping.
  - `StorageManager`: Safely creates and manages physical storage folders.
  - `ZipCompressionService`: Implementation of `ICompressionService` using Deflate streams.
  - `StoragePressureMonitor`: Scans actual physical disks using `DriveInfo`.
  - `WindowsRecycleBinProvider`: Connects to `Shell32.Shell` namespaces to safely read metadata from `ssfBITBUCKET (10)` virtual Recycle Bin without elevated permissions. It remains cross-platform compile-safe on non-Windows platforms.
  - `RestoreService`: Coordinates safe overwrite-protected restoration.

### 4. SmartBin.App
- **Responsibilities**: WinUI 3 Application desktop UI providing views and dashboard panels. It includes two separate tabs separating "SmartBin Storage" from the read-only "Windows Recycle Bin" panel.

## Windows Recycle Bin Integration Boundary (Phase 4)

To protect data integrity, the integration with the Windows Recycle Bin is strictly **Read-Only**:
```
Windows Recycle Bin ──> [ WindowsRecycleBinProvider ] ──> Normalized WindowsRecycleBinItems ──> [ CandidateAnalyzer ] ──> Explainable Analysis
```
No mutating operations (movement, deletion, or compression) are ever performed on actual Windows Recycle Bin files.
