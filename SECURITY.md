# Security & Data-Safety Policy

Data safety, cryptographic integrity, and trust boundary isolation are fundamental to SmartBin. We appreciate responsible disclosure of any potential vulnerabilities or data safety concerns.

---

## Reporting Safety or Security Issues

**Please do NOT report potential security vulnerabilities or data integrity risks in public GitHub issues.**

If you discover a potential security flaw, path traversal vulnerability, privilege escalation risk, or data integrity issue:

1. **Email Report:** Send a detailed report to the maintainers or use GitHub's Private Vulnerability Reporting mechanism.
2. **Report Details:** Please include:
   * Description of the potential risk or vulnerability
   * Steps to reproduce using synthetic test data
   * Impact assessment
   * Proposed fix or mitigation (if available)

---

## Core Security Invariants

SmartBin is engineered around explicit defensive boundaries:
* **User-Mode Execution:** Operates strictly in standard user mode without requiring administrative privileges.
* **Path Traversal Defense:** All internal storage paths are strictly validated to reside within canonical storage root prefixes.
* **Reparse-Point Rejection:** Symbolic links and junctions (`FileAttributes.ReparsePoint`) are explicitly rejected before file operations.
* **Overwrite Protection:** Restoration operations explicitly reject overwriting existing destination files.
* **Cryptographic Verification:** SHA-256 stream hashes are verified before committing any external modifications.

---

## Response Process

Maintainers will acknowledge receipt of reports within 48 hours and work with the reporter to investigate, verify, and address any security issues prior to public disclosure.
