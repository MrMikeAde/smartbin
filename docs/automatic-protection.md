# SmartBin Automatic Storage Protection

This document outlines the background monitoring, settings policies, thresholds, revalidation, limits, and recovery guarantees of SmartBin's automatic storage protection engine.

## 1. Background Monitoring Architecture

SmartBin includes a background monitoring system modeled via the `IStorageMonitor` contract.
- **Drive Observer**: Queries `IStoragePressureMonitor` at a configurable interval (default: 60 seconds).
- **State Transition Events**: Observes total capacity and free space percentage. If the storage pressure state (`Normal`, `Low`, `Critical`) changes, it triggers the `PressureStateChanged` event.
- **Application Cohesive Loop**: Running in the background (using standard `PeriodicTimer` asynchronous loops suitable for desktop applications, without requiring high-privilege Windows background services).

## 2. User Settings Policy

SmartBin puts the user in complete control. By default, automatic protection is **OFF** and must be explicitly enabled.

The settings panel exposes the following controls:
- **Off Mode**: No automatic background optimizations occur.
- **Notify Me Mode**: SmartBin scans and raises a non-spam user notification when storage pressure is Low or Critical, but does not modify any files.
- **Automatic Mode**: SmartBin scans and automatically optimizes compressible deleted files sequentially to free up storage space.

## 3. Storage Thresholds & Safety Floor

- **Configurable Thresholds**:
  - Low Pressure Threshold: percentage below which storage is marked as Low (default: 15% free space).
  - Critical Pressure Threshold: percentage below which storage is marked as Critical (default: 5% free space).
  - Target Free-Space Percentage: target percentage to restore space back to (default: 20%).
- **Hard Safety Floor / Safety Margin**:
  - Configurable hard limit (default: 5 GB).
  - If available free space is below this hard safety margin, SmartBin **aborts immediately** and skips all background optimizations, preventing any resource starvation or system slowdowns.

## 4. Automatic Optimization Flow & Revalidation

When automatic optimization is allowed, SmartBin executes a secure, single-item-at-a-time loop:
```
Background scan ──> Plan generated ──> Recheck space & Revalidate candidate ──> Run Phase 5 Pipeline ──> Re-evaluate ──> Stop early if target satisfied
```

1. **Recheck Space**: Before processing a candidate, the engine checks the `StoragePressureMonitor`. If the available space has already exceeded the target threshold, execution stops immediately, preserving CPU and disk resource cycles.
2. **Revalidate Candidate**: Queries the Recycle Bin provider to verify that the planned candidate still exists, matches its expected size, and its original path metadata remains consistent. If anything differs (e.g. the file was manually restored or deleted), the operation is aborted.
3. **One-item-at-a-time Rule**: Real-file background optimizations process **exactly one item per cycle**, preventing stale plans or bulk processing failures.

## 5. Throttled User Notifications

To prevent spamming, SmartBin features a debounced, throttled `NotificationService` (cooldown of 5 seconds for tests/demos, easily configured higher for production) that raises standard system alerts for:
- Low/Critical storage pressure warnings.
- Successful automatic file optimization results (e.g., "SmartBin optimized 1 recoverable file and reclaimed 4.7 GB").
- Rollback warning notifications upon failure.

## 6. Crash Recovery & Startup Cleanups

The startup initialization loop triggers the `CrashRecoveryService`:
- Scans the controlled storage `temp/` folder on boot.
- Identifies and safely cleans up intermediate `.acq`, `.zip`, `.unzip`, and `.restore` residual files from crashed or interrupted operations.
- Keeps objects and database states 100% consistent, guaranteeing that incomplete operations are never assumed successful.
