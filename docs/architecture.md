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
  - `IStoragePressureMonitor`: Monitors disk pressure and triggers compression pipeline.
  - `IRestoreService`: File restoration.

### 2. SmartBin.Core
- **Responsibilities**: Contains pure domain models (`SmartBinItem`), business rules, value objects, and core decision logic.
- **Constraints**:
  - Pure C# standard project.
  - Strictly **no UI dependencies** (WinUI/Windows App SDK).
  - Strictly **no infrastructure dependencies** (EF Core, SQLite).
- **Domain Logic**:
  - Validation of file states.
  - Compression-decision logic: determines if a compressed version of a file should be kept (i.e. `compressedSize < originalSize`) or discarded.

### 3. SmartBin.Infrastructure
- **Responsibilities**: Implements contracts using concrete storage engines, frameworks, and system APIs.
- **Key Implementations**:
  - `Sha256FileHasher`: Cryptographic SHA-256 hashing.
  - `EfSmartBinRepository` / `SmartBinDbContext`: Uses SQLite and EF Core to store file metadata separately from the actual physical files.
  - Future OS adapters, file readers, and compression algorithm implementations (ZIP, Brotli, Zstandard).

### 4. SmartBin.App
- **Responsibilities**: WinUI 3 Application desktop UI providing views, modern Windows 11 dashboards, and conceptual visualizations.
- **Constraints**:
  - Consumes services registered in the Dependency Injection container.
  - Business logic flows entirely through `SmartBin.Core` and `SmartBin.Contracts`.

## Future Windows Integration
While this phase establishes the core foundation, future versions will integrate with the native Windows Shell or host background services without requiring high administrator privileges.
