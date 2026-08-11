# AGENTS.md

Welcome! This file provides essential guidelines, coding conventions, safety rules, and instructions for building and testing SmartBin.

## Product Purpose
SmartBin is a Windows-first experimental proof-of-concept exploring an adaptive Recycle Bin. Instead of permanently deleting files when storage is constrained, SmartBin intelligently and losslessly compresses recoverable deleted files, restoring them byte-for-byte when requested.

## Architecture Guidelines

- **SmartBin.Contracts**: Interface-only library. Zero dependencies.
- **SmartBin.Core**: Pure C# domain logic. No WinUI or external framework dependencies. Core rules must stay testable.
- **SmartBin.Infrastructure**: Concrete database (SQLite/EF Core), file hashing, and filesystem services.
- **SmartBin.App**: WinUI 3 dashboard application. Inspired by Fluent Design guidelines.

## Code Quality & Conventions

1. **Nullable Reference Types**: Active across all projects (`<Nullable>enable</Nullable>`). Avoid null-suppression operators (`!`) unless absolutely safe.
2. **Async/Await**: Make all IO, DB, and compression APIs async-first. Always support `CancellationToken`.
3. **Dependency Injection**: Design classes with DI in mind (inject interfaces from `SmartBin.Contracts`).
4. **Structured Error Handling**: Use domain-specific exceptions (e.g., `SmartBinException`) to avoid exposing infrastructure failures directly to the UI.

## Absolute Safety Rules (Data Integrity is Priority #1)

- **NEVER** permanently delete user files in the background or during normal storage pressure cleanup.
- **NEVER** silently overwrite an existing file during restoration.
- **NEVER** modify or delete files outside of SmartBin's controlled folder directory.
- Avoid elevated Windows permissions; do not request admin privileges.
- Always write files to a temporary location first, verify integrity, and rename/swap atomically.
- **Verify before Replacing**: Validate compression success (check SHA-256) before removing the original representation.

## Testing Expectations
- **High Test Coverage on Core Logic**: Ensure `SmartBinItem` state changes and compression-decision logic are heavily unit tested.
- **Deterministic Hashing**: Ensure hashing tests cover standard, edge, and invalid inputs.
- **In-Memory Db Tests**: Verify `EfSmartBinRepository` works using in-memory SQLite providers.
- Run `dotnet test` and make sure 100% of the tests pass before submission.
