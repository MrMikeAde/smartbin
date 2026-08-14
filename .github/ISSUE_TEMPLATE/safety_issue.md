---
name: Safety / Data-Integrity Issue
about: Report a potential data safety or integrity risk (HIGH PRIORITY)
title: '[SAFETY] '
labels: safety, security, high-priority
assignees: ''
---

**CAUTION:** If this is a critical security vulnerability, please refer to [SECURITY.md](../../SECURITY.md) for private reporting options.

**Describe the Safety Concern**
A detailed explanation of the potential data integrity, restoration error, path traversal, or unexpected mutation risk observed.

**Safety Invariant Impacted**
Specify which safety invariant or boundary is affected (e.g., SHA-256 Hash Verification, Overwrite Protection, Reparse Point Guard, State Rollback, Path Traversal Defense).

**Reproduction Steps (Using Synthetic Test Data ONLY)**
1. Generate synthetic test file using `TestFileGenerator`.
2. Execute step...
3. Observe behavior...

**Expected Safety Behavior**
Describe the expected defensive behavior or safety rollback that should occur.

**Environment**
 - OS: [e.g. Windows 11 x64]
 - SmartBin Version: [e.g. v1.0-beta]
