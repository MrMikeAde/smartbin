# SmartBin

An experimental adaptive Recycle Bin that explores reclaiming storage from recoverable deleted files through intelligent compression.

> **Disclaimer:** SmartBin is an independent experimental project and is not affiliated with Microsoft or Apple. It does not replace the default Windows Recycle Bin.

## The Problem
When modern computer storage becomes constrained, users are forced to permanently delete files, risking the loss of recoverable assets.

## Proposed Solution & Workflow
Deleted files remain recoverable inside a secure local storage area. When the user's storage gets constrained, recoverable files are intelligently compressed using algorithms like ZIP, Brotli, or Zstandard, preserving their original path and metadata inside a lightweight database. When a file is restored, SmartBin decompresses it, guaranteeing byte-for-byte identity.

```text
20 GB deleted file
        ↓
intelligent compression
        ↓
6 GB stored
        ↓
14 GB temporarily reclaimed
        ↓
restore
        ↓
exact original 20 GB file
```

## Working Prototype (Phase 6)
SmartBin is a fully functional **Automatic Storage Protection** application:
- **Background Disk Space Monitoring**: Leverages `DriveInfo` system APIs to observe drive space at a configurable interval. Transition states (`Normal`, `Low`, `Critical`) raise dynamic, throttled events.
- **Configurable Settings Policy**: Offers full user settings controls: automatic optimization toggle (`OFF`, `NOTIFY ME`, `AUTOMATIC`), low/critical space threshold percentages, safety floor margin (default: 5 GB), and battery-saving pause configurations.
- **Failsafe Safety Floor & Power-Awareness**: Enforces a non-negotiable safety floor. Pauses background optimization if free space is below the Safety Floor or if running on battery power (using native `GetSystemPowerStatus` P/Invoke).
- **Sequential One-Item-at-a-Time Execution**: For automatic optimization, processes exactly **one successful item per loop cycle**. Rechecks spaces and revalidates candidates (confirming they still exist, sizes match, and paths match) before every single operation to prevent stale plans.
- **Crash Recovery & Cleanup**: Scans the controlled `temp/` folder on startup (`CrashRecoveryService`) to identify and clean up intermediate `.acq`, `.zip`, `.unzip`, and `.restore` residual files from crashed or interrupted runs.
- **Throttled Non-Spam Notifications**: Implements debounced notifications with cooldown throttling for alerts.
- **Activity History Logs**: Records detailed transactional activity history logs directly to the metadata SQLite DB.
- **WinUI 3 Dashboard Redesign**: Upgraded with a Settings Page, Activity History logs grid, and real-time simulator overrides.

## Project Structure
- `src/SmartBin.App`: WinUI 3 dashboard desktop application shell (with separate controlled storage, read-only Windows Recycle Bin, Controlled Experiment, Settings, and Activity History tabs, supporting conditional headless live demo mode on Linux).
- `src/SmartBin.Core`: Core domain models, state enums, heuristics, priority scorers, batch planners, executors, simulated Recycle Bin providers, test file generators, and Phase 6 background protection engines.
- `src/SmartBin.Infrastructure`: SQLite database, EF Core persistence, activity logs, stream-based hashing, storage managers, ZIP compression, native Windows Shell COM Recycle Bin mutation services, and Windows power line P/Invokes.
- `src/SmartBin.Contracts`: Common interfaces, custom exception definitions, and service contracts.
- `tests/`: 64 automated unit and integration tests validating safe import, heuristics, scoring models, batch planners, simulated Windows Recycle Bin, and Phase 6 automatic settings, safety floors, and crash recovery rollbacks.

## Safety Philosophy
Data integrity is our highest priority.
- No permanent deletion in MVP.
- Overwrite protection on restoration.
- No elevation / administrative privilege requests.
- Transactional metadata matching the exact state of physical files.

## Development Setup & Testing
1. Clone this repository.
2. Build the solution using:
   ```bash
   dotnet build smartbin.sln
   ```
3. Run unit and integration tests using:
   ```bash
   dotnet test smartbin.sln
   ```
4. Run the live demo console simulation (featuring simulated pressure, candidate explanation, batch planning, startup crash recovery sweep, power-awareness battery checks, background protection, and actual space recovery updates) using:
   ```bash
   dotnet run --project src/SmartBin.App
   ```
