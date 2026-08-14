# Contributing to SmartBin

Thank you for your interest in contributing to SmartBin! As an experimental proof-of-concept exploring adaptive Recycle Bin storage, we welcome community feedback, issue reports, benchmark contributions, and code enhancements.

---

## Code of Conduct

By participating in this project, you agree to abide by our [Code of Conduct](CODE_OF_CONDUCT.md).

---

## How to Contribute

### 1. Reporting Bugs & Safety Issues
* For security or data-integrity issues, please see our [Security Policy](SECURITY.md).
* For standard bug reports or benchmark feedback, please use the appropriate [GitHub Issue Template](https://github.com/MrMikeAde/smartbin/issues/new/choose).

### 2. Development Setup
1. Fork and clone the repository.
2. Build the solution using .NET 10.0 SDK:
   ```bash
   dotnet build smartbin.sln
   ```
3. Run the complete automated test suite to verify baseline functionality:
   ```bash
   dotnet test smartbin.sln
   ```

### 3. Absolute Safety Directives
All code contributions MUST respect SmartBin's core safety directives:
* **Never permanently delete user files.**
* **Never silently overwrite existing destination files on restore.**
* **Never modify files outside of SmartBin's controlled storage directory.**
* **Always verify cryptographic SHA-256 stream hashes before and after compression/restoration.**
* **Always preserve 100% passing test baseline (104 tests).**

### 4. Pull Request Workflow
1. Create a feature or bugfix branch off `main`.
2. Ensure all 104 automated tests pass (`dotnet test smartbin.sln`).
3. Include unit/integration tests for any new logic or bug fixes.
4. Submit a Pull Request with a clear summary of changes and test evidence.
