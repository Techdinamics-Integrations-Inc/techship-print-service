# Print Service Rebuild - Project Plan Overview

## Summary
Rebuild the legacy Techdinamics.PrintClient (WPF tray app) as a modern .NET service that runs exclusively in Linux Docker containers. The service is designed and supported only for Linux. If executed on other platforms, it will throw a platform compatibility exception.

**Multi-Printer/Multi-Portal Support**: The service supports multiple printer/portal configurations running concurrently within a single Docker container. Each configuration runs as an independent worker thread monitoring its own queue and sending to its designated printer.

## Core Workflow (from legacy app analysis)
1. Connect to web portal using credentials (Portal URL, ApiSecret, ConnectionName, PrinterId)
2. Poll endpoint `Integration/ProcessNextOrderExt` for print jobs
3. Download PDF or ZPL label data from response
4. Print to configured printer (ZPL thermal, PDF, or generic)
5. Confirm print completion via API callback

## Subtasks

| # | File | Description |
|---|------|-------------|
| 01 | 01-project-setup.md | Set up .NET project structure, dependencies, and configuration model |
| 02 | 02-api-client.md | Implement web API client for portal communication |
| 03 | 03-print-service.md | Implement print service with ZPL/PDF printing (Network, USB/Local, File modes) |
| 04 | 04-worker-service.md | Implement background worker/polling service |
| 05 | 05-docker-setup.md | Create Dockerfile and docker-compose for multi-platform support |
| 06 | 06-unit-tests.md | Create unit test project with mocked API |
| 07 | 07-user-guide.md | Write end-user installation and configuration guide |
| 08 | 08-integration-testing.md | Mock web service, Docker integration tests, Windows USB printer testing guide |

## Configuration Requirements
The service supports an **array of printer configurations**, each with:
- Portal URL
- API Secret
- Connection Name (client key)
- Printer ID
- Printer settings with connection type (Network or Local/USB)
- ZPL printer: network address:port OR local printer name
- PDF printer: local printer name
- Enabled flag

Each configuration spawns its own worker thread for concurrent processing.

## Printer Connection Types
- **Network**: TCP socket to IP:port (default for Docker, ZPL port 9100)
- **Local/USB**: Local printer name via CUPS (standard for the Linux container environment)

## Document Types Supported
Orders can contain mixed document types:
- ZPL labels (DIRECT type)
- PDF labels (thermal printer)
- PDF packing slips (standard printer)
- PDF commercial invoices
- PDF DG declarations
- Image labels

## Notes
- App Techdinamics.PrintClient is included to this repo for reference. Use it to check old functionality, but do not update or commit it.  
- **Authentication Note**: Username/Password authentication has been deprecated and removed. The service now uses `ApiSecret` (sent via `x-secret-key` header) for authentication.