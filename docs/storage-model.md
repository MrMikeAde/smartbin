# SmartBin Storage Model

This document outlines how SmartBin organizes file metadata and physical content.

## Separation of Concerns: Metadata vs. Content

SmartBin maintains a strict separation between file metadata (stored in a relational SQLite database) and actual files/archives (stored in a dedicated system storage folder).

```
+-----------------------------------------------------------+
|                      SmartBin System                      |
+-----------------------------+-----------------------------+
                              |
       +----------------------+----------------------+
       |                                             |
       v                                             v
+--------------+                              +--------------+
| SQLite DB    |                              | Dedicated FS |
| (Metadata)   |                              | Storage      |
|              |                              | (Enclosed)   |
| - File Size  |                              |              |
| - Original   |                              | - Compressed |
|   Path       |                              |   or original|
| - SHA-256    |                              |   file payload|
| - Status     |                              |              |
+--------------+                              +--------------+
```

### 1. The Metadata Database (`SmartBinDbContext`)
SmartBin stores rich metadata about every deleted item inside a SQLite database.
- **Database File**: `smartbin.db` stored in an isolated app-data folder.
- **Entity**: `SmartBinItem` representing deleted/recoverable items.
- **Tracked Attributes**:
  - `Id`: Unique item ID.
  - `OriginalPath`, `OriginalFileName`, `OriginalExtension`: Original file location attributes.
  - `OriginalSize`, `CurrentStoredSize`: To measure space reclamation.
  - `Sha256Hash`: The immutable byte footprint of the original file.
  - `CurrentStoragePath`: Location of the file within SmartBin's controlled directory.
  - `CompressionStatus`, `CompressionAlgorithm`: Current compression state and strategy.
  - `Timestamps`: Tracking deletion, creation, modification, and compression times.

### 2. Controlled Physical Storage
Files sent to SmartBin are moved into an isolated, managed directory.
- The path to files inside this storage area is managed by the application's `IStorageManager`.
- Under storage pressure, files can be converted into compressed archives (e.g., `.zip`, `.br`, `.zst`).
- The system checks if compression is efficient (`compressed_size < original_size`). If not, the original, uncompressed file is preserved in the storage folder to avoid wasting resources on non-compressible files.
- Original paths are always preserved in the metadata database, enabling flawless restoration.
