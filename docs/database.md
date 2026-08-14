# SmartBin — Database Schema & Versioning Strategy

SmartBin leverages SQLite mapped via Entity Framework Core (EF Core) to maintain transactional recovery and state metadata.

## Database Schema Model

### 1. `SmartBinItems` Table
Stores records of compressed, recoverable deleted files.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `Id` | `GUID` | No | Primary Key. Unique identifier of the item inside SmartBin. |
| `OriginalPath` | `TEXT` | No | Absolute canonical original location of the file before deletion. |
| `OriginalFileName`| `TEXT` | No | File name of the deleted file. |
| `OriginalExtension`| `TEXT` | No | Extension (including dot). |
| `OriginalSize` | `INTEGER`| No | Size in bytes before compression. |
| `CurrentStoredSize`| `INTEGER`| No | Size in bytes of the active compressed representation. |
| `Sha256Hash` | `TEXT` | No | Cryptographic original SHA-256 string for verification. |
| `CurrentStoragePath`| `TEXT` | No | Disk path of the compressed representation inside `objects/`. |
| `DeletedTimestamp` | `DATETIME`| No | Date/time the item was originally deleted. |
| `CompressionStatus`| `INTEGER`| No | Enum: 0=Uncompressed, 1=Compressed, 2=Failed, 3=NotFeasible. |
| `CompressionAlgorithm`| `INTEGER`| No | Enum: 0=None, 1=Zip, 2=Brotli, 3=Gzip, 4=Zstandard. |
| `CompressionTimestamp`| `DATETIME`| Yes | Timestamp compression was completed. |
| `RestorationStatus`| `INTEGER`| No | Enum: 0=Pending, 1=Restored, 2=Failed. |

### 2. `ActivityLogs` Table
Records historical activity entries for transparency and audit.

| Column | Type | Nullable | Description |
| :--- | :--- | :---: | :--- |
| `Id` | `INTEGER`| No | Primary Key. Autoincrement. |
| `Timestamp` | `DATETIME`| No | Timestamp of logging. |
| `OperationType` | `TEXT` | No | Name of activity (e.g., "Automatic Optimization"). |
| `ItemName` | `TEXT` | Yes | Path of the file involved. |
| `OriginalSize` | `INTEGER`| Yes | Size before optimization. |
| `CompressedSize`| `INTEGER`| Yes | Size after optimization. |
| `ReclaimedBytes` | `INTEGER`| Yes | Difference (net savings). |
| `ResultState` | `TEXT` | No | Status (e.g., "Verified", "Failed", "Aborted"). |
| `FailureReason` | `TEXT` | Yes | Exception error message. |
| `IsAutomatic` | `BOOLEAN`| No | Whether background engine executed it. |
| `Rationale` | `TEXT` | Yes | Plain-text explainability description. |

---

## Schema Versioning & Migrations

To support application updates safely:
1. **Explicit Version Checking**: On application startup, SmartBin queries `PRAGMA user_version;` on the SQLite database before running any queries.
2. **Migrations Safety**: Standard Entity Framework Migrations are version-locked. Incompatible DB schema versions will raise a controlled upgrade prompt rather than silently deleting or corrupting the user's database.
3. **Database locking**: Mapped connections are local, isolated to single-user context, and wrapped in exclusive transaction locks during writes to prevent table locking contention.
4. **Backup and Rebuild**: If a database is corrupted or incompatible, SmartBin prompts the user with a recovery rebuild dialog, which scans `objects/` for valid files and rebuilds metadata safely.
