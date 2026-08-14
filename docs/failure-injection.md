# Failure Injection Architecture & Test Points

SmartBin implements a test-only, non-invasive **Failure Injection Framework** to assert safe recovery states at critical boundaries.

## Architecture

```text
       +---------------------------------------------+
       |           Test Suite (Adversarial)          |
       +---------------------------------------------+
                              | Configure/Enable
                              v
+------------------+   +-----------------------------+
| IFailureInjector |-->| TestFailureInjector         |
+------------------+   +-----------------------------+
          ^
          | DI (Injected at construction)
          |
+----------------------------------------------------+
|  ControlledExperimentEngine / CompressionEngine   |
|  RestoreService / EfSmartBinRepository             |
+----------------------------------------------------+
```

- **Production Build**: Compiles with `NoOpFailureInjector`, which has zero performance overhead and does absolutely nothing. No keyboard shortcuts, environment variables, or secret flags.
- **Test Build**: Resolves to `TestFailureInjector` where specific checkpoint failures can be enabled on-demand.

---

## Named Checkpoints

| Checkpoint Name | Location | Description / Exception Triggered |
| :--- | :--- | :--- |
| `AfterAcquisition` | `ControlledExperimentEngine` | Simulates file extraction being abruptly terminated right after copy. |
| `AfterHashing` | `ControlledExperimentEngine` | Triggers right after calculating original SHA-256 hash. |
| `BeforeCompression` | `ControlledExperimentEngine`, `CompressionEngine` | Triggers before compression service deflate. |
| `DuringCompression` | `ControlledExperimentEngine`, `CompressionEngine` | Simulates write exception during active compression streams. |
| `AfterCompression` | `ControlledExperimentEngine`, `CompressionEngine` | Triggers after compression archive is closed but before verification. |
| `BeforeCompressionVerification` | `ControlledExperimentEngine`, `CompressionEngine` | Triggers before temporary decompression verification. |
| `AfterCompressionVerification` | `ControlledExperimentEngine`, `CompressionEngine` | Triggers after decompression SHA-256 calculation. |
| `BeforeRestorationVerification` | `ControlledExperimentEngine` | Triggers before dry-run restoration hash matching. |
| `AfterRestorationVerification` | `ControlledExperimentEngine` | Triggers after dry-run restoration succeeds. |
| `BeforeCommit` | `ControlledExperimentEngine`, `RestoreService`, `CompressionEngine` | Simulates failure right before Recycle Bin mutation / final atomic swap. |
| `DuringCommit` | `ControlledExperimentEngine`, `RestoreService` | Simulates Windows Recycle Bin mutation COM Verb / filesystem write failure. |
| `AfterCommit` | `ControlledExperimentEngine`, `RestoreService` | Triggers after COM Verb deletion succeeds but before database state persist. |
| `BeforeActivityPersistence` | `ControlledExperimentEngine`, `ActivityRepository` | Triggers before recording log in SQLite. |
| `AfterActivityPersistence` | `ControlledExperimentEngine`, `ActivityRepository` | Triggers after recording log in SQLite. |
| `DatabaseAdd` | `EfSmartBinRepository` | Simulates general DB insert connection failure. |
| `DatabaseUpdate` | `EfSmartBinRepository` | Simulates general DB update transaction locked/failure. |
| `StorageMonitoringFailure` | `AutomaticProtectionEngine` | Simulates disk space checking returning empty/failing. |
| `NotificationFailure` | `AutomaticProtectionEngine` | Simulates UI notification messaging throwing exceptions. |

---

## Inconsistency Recovery via Receipt Journals

When `DuringCommit` or `AfterCommit` triggers, the Windows Recycle Bin item is permanently removed, but the DB has not yet saved the compressed item record.

To resolve this critical DB-filesystem divergence:
1. Before invoking the Recycle Bin mutation, `ControlledExperimentEngine` writes a text-based transactional receipt `temp/[item_id].receipt` listing the file size, path, SHA-256, and stored compressed path.
2. After the database insert succeeds, the receipt file is safely deleted.
3. On application startup, `CrashRecoveryService` checks for orphaned `*.receipt` files. If it finds one where the database lacks a corresponding record but the physical compressed file exists in `objects/`, it reconstructs and adds the database record, reconciling state safely without silent data loss.
