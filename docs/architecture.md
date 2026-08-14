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
  - `IFailureInjector`: Abstract interface for test-only checkpoints.
  - `IFileHasher`: SHA-256 byte-for-byte verification.
  - `ICompressionService`: Abstract compression, decompresion, analysis.
  - `ISmartBinRepository`: SQLite CRUD operations for metadata.
  - `IStorageManager`: Directory and storage area management.
  - `IStoragePathProvider`: Resolves the root of the controlled storage area.
  - `IImportService`: Manages importing files safely.
  - `ICompressionEngine`: Runs atomic compression, evaluation, and verification.
  - `IStoragePressureMonitor`: Monitors drive space and pressure states.
  - `IStorageMonitor`: Background drive observer loop.
  - `IActivityLogger`: Manages persisting activity history entries.
  - `INotificationService`: Specifications for throttled, non-spam system notifications.
  - `IPowerStateProvider`: Queries AC vs battery power-state.
  - `IOptimizationPlanner`: Planning of candidate batches.
  - `IOptimizationExecutor`: Orchestrates plan batch processing.
  - `IRecycleBinProvider`: Specifies the contract for enumerating and analyzing Windows Recycle Bin items safely.
  - `IRecycleBinMutationService`: Specifies the contract for extracting content, restoring, and deleting individual Recycle Bin items.
  - `IRestoreService`: File restoration.

### 2. SmartBin.Core
- **Responsibilities**: Contains pure domain models (`SmartBinItem`), business rules, value objects, and core decision logic.
- **Key Implementations**:
  - `NoOpFailureInjector`: Production implementation of failure injector (does nothing).
  - `ImportService`: Executes safe import steps.
  - `CompressionEngine`: Runs atomic compression.
  - `CompressionHeuristics`: Evaluates pre-compressed file extensions.
  - `StoragePressurePolicy`: Converts space metrics into recommendations.
  - `CandidateAnalyzer`: Evaluates, scores, and prioritizes both local and external Windows Recycle Bin items with detailed explanation rationales.
  - `OptimizationPlanner`: Batch planning of candidates.
  - `OptimizationExecutor`: Sequential batch orchestrator.
  - `StoragePressureSimulator`: Programs mock metrics overrides.
  - `SimulatedRecycleBinProvider`: Fakes realistic Windows Recycle Bin metadata.
  - `ControlledExperimentEngine`: Orchestrates the safe, multi-phase Phase 5 state machine.
  - `TestFileGenerator`: Creates deterministic compressible/incompressible test files.
  - `CrashRecoveryService`: Safely scans and cleans up intermediate temp files on startup, and resolves DB-external receipt journals.
  - `AutomaticProtectionEngine`: Coordinates the background automatic storage protection pipeline.

### 3. SmartBin.Infrastructure
- **Responsibilities**: Implements contracts using concrete storage engines, frameworks, and system APIs.
- **Key Implementations**:
  - `Sha256FileHasher`: Cryptographic stream-based hashing.
  - `EfSmartBinRepository` / `SmartBinDbContext`: SQLite database metadata mapping.
  - `ActivityRepository`: Activity log sqlite table mapping, implementing `IActivityLogger`.
  - `StorageManager`: Safely creates and manages physical storage folders and checks drive free space.
  - `ZipCompressionService`: Implementation of `ICompressionService` using Deflate streams.
  - `StoragePressureMonitor`: Scans actual physical disks using `DriveInfo`.
  - `StorageMonitor`: Asynchronous background periodic observer implementing `IStorageMonitor`.
  - `NotificationService`: Throttled event notifier implementing `INotificationService`.
  - `WindowsPowerStateProvider`: Queries AC/Battery lines via kernel32 `GetSystemPowerStatus` P/Invoke.
  - `WindowsRecycleBinProvider`: Connects to `Shell32.Shell` namespaces to safely read metadata from `ssfBITBUCKET (10)`.
  - `WindowsRecycleBinMutationService`: Leverages native Windows Shell COM APIs and verbs (`InvokeVerb("restore")` / `InvokeVerb("delete")`) to perform safe, non-elevated single-item restoration and removal.
  - `RestoreService`: Coordinates safe overwrite-protected restoration.

### 4. SmartBin.App
- **Responsibilities**: WinUI 3 Application desktop UI providing views and dashboard panels. It includes five separate tabs: "SmartBin Storage", "Windows Recycle Bin", "Controlled Experiment", "Settings", and "Activity History".

---

## Reliability & Fault-Tolerance Architecture

SmartBin's architecture is designed under the **fail-safe principle**: when an operation is interrupted or encounters hardware/filesystem errors, it rolls back state and preserves user data completely.

```text
               [Prepare & Verify Stage]
                          │
         Writes temp file inside storage area
                          │
                 [Verification Stage]
                          │
          Double checks SHA-256 against source
                          │ (If fails, rollback temp)
                          ▼
            [Receipt Journaling Stage]
                          │
       Writes receipt temp/[item_id].receipt to disk
                          │
             [Recycle Bin Mutation Stage]
                          │ (If fails, rollback temp & receipt)
                          ▼
              [Database Persistence Stage]
                          │
        Commits SmartBinItem, deletes receipt file
                          │ (If fails, receipt remains)
                          ▼
                      [Success]
```

### 1. Sequential Commit Boundaries
A destructive external Recycle Bin mutation is never executed until the copied content has been successfully deflated, verified byte-for-byte using cryptographic SHA-256 hashes, and a dry-run restoration dry-restore verification has succeeded.

### 2. Transaction Receipt Journaling (WAL)
To prevent inconsistencies where a filesystem/external operation succeeds but the SQLite database write fails (e.g. power loss, lock errors), SmartBin uses transactional receipt files:
- Just before mutating the Recycle Bin, SmartBin writes a `.receipt` transaction journal file to disk detailing the exact state.
- Once the database insert successfully persists, the `.receipt` file is cleaned up.
- On startup, `CrashRecoveryService` parses remaining `.receipt` files to reconcile the database with actual filesystem states, ensuring zero silent data loss.
