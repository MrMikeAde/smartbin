# SmartBin — Trust Boundary & Security Contract

SmartBin acts as a non-elevated user-mode system utility. To earn user trust and protect system stability, the application operates under a strict, non-negotiable security boundary.

## The Security Contract

### SmartBin MAY:
- **Inspect Storage Metadata**: Scan local drive capacities, read volume labels, and fetch available free space through supported OS APIs (`DriveInfo`).
- **Read Windows Recycle Bin Metadata**: Enumerate deleted items using supported Shell32 COM structures and query properties (filename, size, deleted date).
- **Retrieve authorized files**: Copy content of an explicitly selected item from the Recycle Bin to the controlled `temp/` folder when a compression run begins.
- **Write to controlled folders**: Create, compress, decompress, and delete files strictly within its authorized objects/ and temp/ subdirectories.
- **Persist transactional logs**: Write metadata entries and operation history to the local SQLite database.
- **Restore verified files**: Move decompressed items back to their original file location if, and only if, verification passes and the destination does not already contain a file.

### SmartBin MUST NOT:
- **Access arbitrary user files**: Never touch, scan, read, or modify files on the desktop, documents, or other user folders unless they are explicitly deleted and stored in the Recycle Bin.
- **Silently modify active directories**: Never perform write or delete operations on directories outside the controlled SmartBin folder.
- **Manipulate undocumented internals**: Never directly traverse, edit, or hook into the `$Recycle.Bin` raw directory or directly edit `$I` / `$R` file descriptors.
- **Escalate privileges**: Never request or run as Administrator.
- **Transmit contents**: Never transmit files, paths, or metadata off the machine. SmartBin is 100% local-first.
- **Silently overwrite files**: Never overwrite any existing file during a restoration operation.
- **Claim unverified success**: Never report an operation succeeded unless byte-for-byte SHA-256 validation has succeeded.

---

## Operations Classification

To keep the system highly transparent, every filesystem activity is divided into four distinct classes:

| Class | Authorized Scope | Restricted Scope | Mitigation / Enforcement |
| :--- | :--- | :--- | :--- |
| **READ** | - Space capacity (`DriveInfo`) <br> - Recycle Bin metadata <br> - In-transit temp file streams | - Active filesystem directories <br> - Protected OS files | Checked by user-level access tokens and OS ACLs. |
| **WRITE** | - Controlled `temp/` folder <br> - Controlled `objects/` folder <br> - Controlled `metadata/` DB | - Root drives <br> - Any directory outside the storage path | **Path Traversal Guard**: All writes verify that paths start with the authorized base directory prefix. |
| **MUTATE / DELETE** | - Deleting intermediate files in `temp/` <br> - Standard Shell COM mutation on single item | - Modifying `$Recycle.Bin` directly <br> - Deleting active desktop files | Handled sequentially via the Phase 5 Safety Pipeline state machine. |
| **RESTORE** | - Moving a verified file from `temp/` to its original path or target path | - Overwriting existing files <br> - Restoring into Windows System directories | **Reparse & Traversal Guards**: Rejects symlinks/junctions, and blocks restoration to `C:\Windows` or Linux system roots. |
