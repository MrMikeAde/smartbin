# Windows Recycle Bin Integration Investigation & Design

This document details the research, api selection, capabilities, limitations, and safety design of SmartBin's Windows Recycle Bin integration layer.

## 1. APIs Investigated

During Phase 4 and Phase 5, several mechanisms for interacting with the actual Windows Recycle Bin were analyzed:

1. **Direct `$Recycle.Bin` Directory & Metadata Parsing**:
   - *Mechanism*: Manually scanning directories under `C:\$Recycle.Bin` and parsing `$I` (index) and `$R` (data) files.
   - *Risk*: Highly fragile. `$I`/`$R` file formats changed between Windows versions (e.g. from Windows 7/8 to 10/11) and bypass official OS protections. Directly manipulating these directories poses massive file-corruption risks and requires elevated permissions. Rejected.
2. **Native Win32 C++ API (`SHQueryRecycleBin`)**:
   - *Mechanism*: Querying `SHQueryRecycleBin` and `SHEmptyRecycleBin` in `Shell32.dll` via P/Invoke.
   - *Risk*: Limited metadata. `SHQueryRecycleBin` only returns the total size and item count of the Recycle Bin. It does not allow individual item enumeration, deletion dates, original paths, or single-file restoration. Rejected for enumeration.
3. **Windows Shell COM Automation (`Shell32.Shell`)**:
   - *Mechanism*: Accessing the Windows Shell object model using Special Folder index `10` (`ssfBITBUCKET` / `CSIDL_BITBUCKET`).
   - *Why Selected*: **Officially documented, stable, and secure.** It provides fully drive-agnostic, shell-integrated enumeration of the virtual Recycle Bin. By querying column properties via `Folder.GetDetailsOf`, we can cleanly read the item's filename, original path, deletion timestamp, and file size under standard user permissions, without requiring administrator privileges or low-level parsing hacks.

---

## 2. Windows Mutation Investigation (Phase 5 Findings)

In Phase 5, we investigated whether official, documented Windows APIs support programmatic restoration or permanent deletion of single virtual Recycle Bin items.

### Key Findings:
- **COM Verb Invocation (Supported & Secure)**:
  A virtual Recycle Bin item (`FolderItem`) in the Windows Shell COM object model supports **Verbs**!
  By accessing `item.Verbs()`, we can programmatically trigger native Shell actions without bypassing security boundaries:
  - **Undelete / Restore**: Activating the `"restore"` or `"undelete"` verb programmatically instructs Windows to restore the file back to its exact original path, safely resolving multi-volume paths internally.
  - **Removal / Deletion**: Activating the `"delete"` verb programmatically removes the item from the Recycle Bin permanently, freeing up space at the system level.
- **Physical `$R` File Streaming**:
  The Shell COM item's `.Path` property resolves to the actual underlying `$R` data file path inside the hidden `$Recycle.Bin` directory. This allows SmartBin to stream and copy the contents of any Recycle Bin item securely using standard `FileStream` APIs without loading the file entirely into RAM.
- **Manual Fallback**:
  If the COM verbs fail (e.g., due to localized language differences), the matching `$R` and `$I` files can be safely deleted manually from the local disk as a robust fallback.

### Mutation Service Architecture
To preserve separation of concerns, all mutating Recycle Bin operations are encapsulated within `IRecycleBinMutationService` and implemented in `WindowsRecycleBinMutationService` (in Infrastructure). This is kept separate from `IRecycleBinProvider` (responsible for read-only discovery).

---

## 3. API Selection & Rationale

We selected the **Windows Shell COM Automation API** (`Shell32.Shell` Namespace `ssfBITBUCKET`) implemented via .NET COM interop.
- **Drive Agnosticism**: Handles multiple drives automatically. The Shell folder aggregates deleted items from all mounted volumes (`C:`, `D:`, etc.) into a single virtual namespace, resolving the correct volume internally.
- **No Elevated Privileges**: Runs perfectly under standard user accounts.
- **Explicit Commit Boundary**: Mutation operations are strictly isolated. No automated background changes occur. Every mutation requires explicit user confirmation.

## 4. Capabilities & Metadata Available

Through `Folder.GetDetailsOf`, the following metadata attributes are extracted:
- **Filename**: The item name displayed in Windows.
- **Original Path**: The location from which the file was deleted.
- **Deletion Date**: The timestamp of deletion.
- **Size**: The physical size of the item.
- **Volume**: Parsed from the root of the original path (e.g., `C:`).
- **Identifier**: Uniquely resolved by the shell path.

## 5. Limitations & Unavailable Data

- **Cryptographic Hashes**: It is impossible to calculate SHA-256 hashes of items inside the Windows Recycle Bin without copying or reading their complete byte stream. To avoid high I/O overhead, SmartBin treats Recycle Bin hashes as *unavailable* during initial analysis.
- **Raw Byte Streams**: The Shell folder does not directly expose standard .NET `Stream` readers for deleted items. Reading or decompressing files requires copying them to a temporary path via the Shell, which is not performed in this read-only phase.

## 6. Risks of Manipulation

Direct manipulation of `$Recycle.Bin` internals outside Shell APIs is highly dangerous. It can lead to database desynchronization, corrupted filesystem structures, and file loss. SmartBin avoids this by enforcing a strict **Read-Only First** philosophy and providing an in-memory `SimulatedRecycleBinProvider` for safe automated verification.
