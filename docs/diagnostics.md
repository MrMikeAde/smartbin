# SmartBin — Diagnostics & Logging Model

SmartBin implements structured, non-intrusive logging and diagnostics to assist in troubleshooting while preserving user privacy.

## Log Storage Location

- **Windows path**: `%LOCALAPPDATA%\SmartBin\logs\diagnostics.log`
- **Linux/CI path**: `./logs/diagnostics.log`

---

## Log Structured Format

Logs are formatted in line-based JSON or structured text containing:
- `Timestamp` (UTC format)
- `OperationId` (Guid tracking the pipeline session)
- `State` (Current pipeline step, e.g., `AcquisitionVerified`)
- `Severity` (`INFO`, `WARN`, `ERROR`, `FATAL`)
- `Event` (Action name)
- `Metadata` (Non-sensitive numbers, e.g., size in bytes, elapsed milliseconds, compression ratio)

---

## Logging Rules (Privacy-Hardened)

To respect user confidentiality and adhere to the strict trust boundary:
- **NO FILE CONTENTS**: Under no circumstances are contents of files written, read, or dumped into logs.
- **CANONICAL ERROR CLASSIFICATION**: No raw stack traces are printed in standard logs; instead, exceptions are caught, logged with a canonical code, and classified.
- **NO CREDENTIALS / SENSITIVE INFORMATION**: No tokens, session state, usernames, or sensitive complete paths are printed.
- **LOG ROTATION**: Logs are automatically rotated. A maximum of 5 files are kept, each capped at 10 MB, preventing unbounded disk consumption.
