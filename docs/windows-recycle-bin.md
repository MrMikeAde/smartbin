# Windows Recycle Bin Integration Investigation & Design

This document details the research, api selection, capabilities, limitations, and safety design of SmartBin's Windows Recycle Bin integration layer.

## 1. APIs Investigated

During Phase 4, several mechanisms for interacting with the actual Windows Recycle Bin were analyzed:

1. **Direct `$Recycle.Bin` Directory & Metadata Parsing**:
   - *Mechanism*: Manually scanning directories under `C:\$Recycle.Bin` and parsing `$I` (index) and `$R` (data) files.
   - *Risk*: Highly fragile. `$I`/`$R` file formats changed between Windows versions (e.g. from Windows 7/8 to 10/11) and bypass official OS protections. Directly manipulating these directories poses massive file-corruption risks and requires elevated permissions. Rejected.
2. **Native Win32 C++ API (`SHQueryRecycleBin`)**:
   - *Mechanism*: Querying `SHQueryRecycleBin` and `SHEmptyRecycleBin` in `Shell32.dll` via P/Invoke.
   - *Risk*: Limited metadata. `SHQueryRecycleBin` only returns the total size and item count of the Recycle Bin. It does not allow individual item enumeration, deletion dates, original paths, or single-file restoration. Rejected for enumeration.
3. **Windows Shell COM Automation (`Shell32.Shell`)**:
   - *Mechanism*: Accessing the Windows Shell object model using Special Folder index `10` (`ssfBITBUCKET` / `CSIDL_BITBUCKET`).
   - *Why Selected*: **Officially documented, stable, and secure.** It provides fully drive-agnostic, shell-integrated enumeration of the virtual Recycle Bin. By querying column properties via `Folder.GetDetailsOf`, we can cleanly read the item's filename, original path, deletion timestamp, and file size under standard user permissions, without requiring administrator privileges or low-level parsing hacks.

## 2. API Selection & Rationale

We selected the **Windows Shell COM Automation API** (`Shell32.Shell` Namespace `ssfBITBUCKET`) implemented via .NET COM interop.
- **Drive Agnosticism**: Handles multiple drives automatically. The Shell folder aggregates deleted items from all mounted volumes (`C:`, `D:`, etc.) into a single virtual namespace, resolving the correct volume internally.
- **No Elevated Privileges**: Runs perfectly under standard user accounts.
- **Absolute Read-Only Safety**: During this PoC phase, all operations are kept strictly read-only. No mutating Shell operations (such as empty or move) are executed.

## 3. Capabilities & Metadata Available

Through `Folder.GetDetailsOf`, the following metadata attributes are extracted:
- **Filename**: The item name displayed in Windows.
- **Original Path**: The location from which the file was deleted.
- **Deletion Date**: The timestamp of deletion.
- **Size**: The physical size of the item.
- **Volume**: Parsed from the root of the original path (e.g., `C:`).
- **Identifier**: Uniquely resolved by the shell path.

## 4. Limitations & Unavailable Data

- **Cryptographic Hashes**: It is impossible to calculate SHA-256 hashes of items inside the Windows Recycle Bin without copying or reading their complete byte stream. To avoid high I/O overhead, SmartBin treats Recycle Bin hashes as *unavailable* during initial analysis.
- **Raw Byte Streams**: The Shell folder does not directly expose standard .NET `Stream` readers for deleted items. Reading or decompressing files requires copying them to a temporary path via the Shell, which is not performed in this read-only phase.

## 5. Security & Multi-Volume Considerations

- **Multi-Volume**: Since each drive contains its own `$Recycle.Bin` folder, the Shell COM API handles this transparently. It maps each item back to its correct volume, which we extract and display in the UI.
- **Permissions**: Safe and secure. If a volume is inaccessible or encrypted (BitLocker locked), the Shell API filters it out gracefully.

## 6. Risks of Manipulation

Direct manipulation of `$Recycle.Bin` internals outside Shell APIs is highly dangerous. It can lead to database desynchronization, corrupted filesystem structures, and file loss. SmartBin avoids this by enforcing a strict **Read-Only First** philosophy and providing an in-memory `SimulatedRecycleBinProvider` for safe automated verification.
