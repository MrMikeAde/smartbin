# SmartBin — Privacy Policy

SmartBin is committed to 100% user confidentiality and operates strictly under a local-first architecture.

## Privacy Assurances

1. **NO CLOUD SERVICES**: SmartBin does not connect to any server, database, or external network resource.
2. **NO TELEMETRY / ANALYTICS**: There is zero tracking, usage metrics gathering, or user profiling code.
3. **NO FILE TRANSMISSION**: File contents, hashes, paths, and metadata never leave the host machine.
4. **NO EXTERNAL UPLOADS**: No APIs are included to package or transmit files to external storage.
5. **THIRD-PARTY AUDIT**: NuGet packages are audited on build to ensure no unexpected telemetry, tracking, or network calls are introduced.

---

## Stored Local Data

SmartBin stores the following data locally within its designated storage path:
- **`objects/`**: Lossless compressed files (renamed with unique GUIDs to prevent guessing).
- **`temp/`**: Short-lived transient files wiped immediately after verification/success.
- **`metadata/`**: Mapped SQLite DB file storing file paths, sizes, hashes, and historical activity entries. No user information or credentials are saved.
