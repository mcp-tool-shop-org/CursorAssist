# Security Policy

## Supported Versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | :white_check_mark: Current |

## Reporting a Vulnerability

**Email:** 64996768+mcp-tool-shop@users.noreply.github.com

1. **Do NOT** open a public issue for security vulnerabilities
2. Email the address above with a detailed description
3. Include steps to reproduce if applicable

### Response timeline

| Action | Target |
|--------|--------|
| Acknowledge report | 48 hours |
| Assess severity | 7 days |
| Release fix | 30 days |

## Scope

CursorAssist is a **local-first** desktop application and NuGet library suite for assistive cursor control, accessibility benchmarking, and motor-skill training.

- **Data accessed:** Raw pointer input coordinates (real-time cursor interception), motor profile JSON files, trace recordings (`.castrace.jsonl`), MAUI local storage for game session history
- **Data NOT accessed:** No cloud sync. No telemetry. No analytics. No network calls. No authentication required
- **Permissions:** Raw pointer input (Windows hooks for cursor interception in Runtime.Windows), file system read/write for profiles, traces, and session data. No elevated permissions required
- **No telemetry** is collected or sent
