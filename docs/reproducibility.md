# SmartBin — Reproducibility & Build Guide

This document provides complete instructions for developers and technical evaluators to independently build, test, run benchmarks, and verify SmartBin's proof-of-concept implementation.

---

## Prerequisites

* **Operating System:** Windows 10/11 x64 or ARM64 (for UI & native Shell integration) or Linux/macOS (for headless build & unit tests).
* **SDK:** .NET 10.0 SDK installed (`dotnet --version` >= 10.0.100).
* **Git:** Installed and configured.

---

## 1. Clone & Build

```bash
# Clone repository
git clone https://github.com/MrMikeAde/smartbin.git
cd smartbin

# Restore dependencies & compile solution
dotnet build smartbin.sln
```

---

## 2. Execute Automated Test Suite

Run the full automated test suite (104 tests):

```bash
dotnet test smartbin.sln
```

Expected output:
```text
Passed!  - Failed:     0, Passed:    27, Skipped:     0, Total:    27 (SmartBin.Core.Tests.dll)
Passed!  - Failed:     0, Passed:    77, Skipped:     0, Total:    77 (SmartBin.Infrastructure.Tests.dll)
Total Tests: 104 Passed
```

---

## 3. Generate Test Data & Run Empirical Benchmarks

SmartBin includes unit/integration tests that execute empirical benchmarks against synthetic datasets:

```bash
# Run benchmark test suite directly
dotnet test tests/SmartBin.Infrastructure.Tests/SmartBin.Infrastructure.Tests.csproj --filter "FullyQualifiedName~AdaptiveIntelligenceTests"
```

To run synthetic test data generation programmatically:
```csharp
using SmartBin.Core.Services;

var generator = new TestFileGenerator();
string testFile = Path.Combine(Path.GetTempPath(), "benchmark_sample.txt");
await generator.GenerateCompressibleFileAsync(testFile, sizeInBytes: 10 * 1024 * 1024);
```

---

## 4. Launch WinUI 3 Application (Windows Only)

To launch the desktop dashboard interface on Windows:

```bash
dotnet run --project src/SmartBin.App
```

From the UI dashboard:
1. View live drive space metrics and storage pressure status.
2. Select **"Controlled Experiment"** tab to test single-item candidate optimization.
3. Observe live terminal logs tracking the 11-stage pipeline.
4. Verify restored items under **"SmartBin Storage"**.
