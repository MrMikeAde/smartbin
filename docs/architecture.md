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
- **Responsibilities**: Contains only service contracts/interfaces, generic definitions, enums, and DTOs. It has no dependencies.
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
  - `IRecycleBinMutationService`: Specifies the contract for extracting content, restoring, and deleting individual Recycle Bin items.
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
  - `ControlledExperimentEngine`: Orchestrates the safe, multi-phase Phase 5 state machine (`Discovered`, `Acquired`, `AcquisitionVerified`, `Compressed`, `CompressionVerified`, `RestorationVerified`, `ReadyForCommit`, `Committed`, `Failed`, etc.) for exactly ONE Recycle Bin item.
  - `TestFileGenerator`: Creates deterministic compressible/incompressible test files.

### 3. SmartBin.Infrastructure
- **Responsibilities**: Implements contracts using concrete storage engines, frameworks, and system APIs.
- **Key Implementations**:
  - `Sha256FileHasher`: Cryptographic stream-based hashing.
  - `EfSmartBinRepository` / `SmartBinDbContext`: SQLite database metadata mapping.
  - `StorageManager`: Safely creates and manages physical storage folders.
  - `ZipCompressionService`: Implementation of `ICompressionService` using Deflate streams.
  - `StoragePressureMonitor`: Scans actual physical disks using `DriveInfo`.
  - `WindowsRecycleBinProvider`: Connects to `Shell32.Shell` namespaces to safely read metadata from `ssfBITBUCKET (10)` virtual Recycle Bin without elevated permissions.
  - `WindowsRecycleBinMutationService`: Leverages native Windows Shell COM APIs and verbs (`InvokeVerb("restore")` / `InvokeVerb("delete")`) to perform safe, non-elevated single-item restoration and removal.
  - `RestoreService`: Coordinates safe overwrite-protected restoration.

### 4. SmartBin.App
- **Responsibilities**: WinUI 3 Application desktop UI providing views and dashboard panels. It includes three separate tabs: "SmartBin Storage", "Windows Recycle Bin", and "Controlled Experiment" (Phase 5).

## Windows Recycle Bin Integration Boundary

To protect data integrity, the integration with the Windows Recycle Bin is strictly mediated:
```
Windows Recycle Bin ──> [ WindowsRecycleBinProvider ] ──> WindowsRecycleBinItems ──> [ CandidateAnalyzer ] ──> Explainable Analysis
```
Mutating operations (such as deletion or restoration) are strictly isolated behind `WindowsRecycleBinMutationService` and are only ever triggered under explicit user-confirmed commit boundaries.
