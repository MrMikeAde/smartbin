# SmartBin - Installation & Setup Guide

This guide provides instructions for installing, running, demonstrating, and uninstalling SmartBin.

---

## 1. Prerequisites

Before installing SmartBin on a Windows system, ensure your system meets the following requirements:

- **Operating System**: Windows 10 version 1809 (Build 17763) or Windows 11 (x64 or ARM64).
- **Runtime**: .NET 10 Desktop Runtime (included automatically if running self-contained).
- **Windows App SDK**: Windows App Runtime 1.6 or higher.
- **Privileges**: Standard user permissions (No Administrator privileges required).

---

## 2. Installation Options

### Option A: Self-Contained Release Deployment
1. Download the latest `SmartBin-win10-x64.zip` (or `SmartBin-win10-arm64.zip`) release package.
2. Extract the archive to a folder of your choice (e.g. `C:\Program Files\SmartBin` or `C:\Users\<User>\AppData\Local\Programs\SmartBin`).
3. Run `SmartBin.App.exe`.

### Option B: Building from Source
1. Clone the repository:
   ```bash
   git clone https://github.com/MrMikeAde/smartbin.git
   cd smartbin
   ```
2. Build the solution using .NET SDK:
   ```bash
   dotnet build smartbin.sln -c Release
   ```
3. Run the application:
   ```bash
   dotnet run --project src/SmartBin.App -c Release
   ```

---

## 3. First Launch & Welcome Experience

When you launch SmartBin for the first time:
1. The **Welcome Guide** tab opens automatically, detailing how SmartBin works, its local storage boundary, and safety principles.
2. **Automatic Protection Mode** defaults to **OFF**. SmartBin will not make any automatic background storage changes unless you explicitly enable it in Settings.
3. Local application databases and storage folders are initialized at `%AppData%\SmartBinStorage\`.

---

## 4. Controlled Demonstration

To safely observe SmartBin's multi-phase verification pipeline without risking personal files:
1. Open the **Controlled Experiment** tab.
2. Click **Generate & Delete to Bin** to create a safe, highly compressible 10 MB test file.
3. Select the test item in the candidate list.
4. Click **Begin Controlled Test**. Observe all 6 safety checks pass sequentially.
5. Click **Commit Controlled Experiment** to complete the operation.
6. Navigate to **SmartBin Storage**, click **Restore Selected**, and verify that the file restores with 100% byte-for-byte SHA-256 match.

---

## 5. Uninstallation Behavior

SmartBin respects user privacy and follows the strict Phase 8 lifecycle contract during uninstallation:

- **Application Removal**: Deleting the SmartBin executable folder removes the app binaries.
- **Preservation of Recoverable Data**: SmartBin does **NOT** silently purge or delete user files stored in `%AppData%\SmartBinStorage\` during application uninstall. Your recoverable files remain safely preserved in local storage.
- **Data Cleanup**: To remove all SmartBin data manually, delete the `%AppData%\SmartBinStorage\` directory after restoring any required files.
