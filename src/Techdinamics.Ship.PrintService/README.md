# Techship Print Service

Modern .NET service for Techship label printing, designed to run in Linux Docker containers.

## Overview

The Techship Print Service monitors print queues from one or more Techship portals and sends print jobs to designated printers. It supports:
- **Multi-Printer/Multi-Portal**: Run multiple independent worker threads within a single container.
- **ZPL & PDF Support**: Handles direct ZPL thermal labels and various PDF documents (labels, packing slips, invoices).
- **Network & Local Printing**: Supports TCP/IP network printers and local/USB printers (via CUPS).
- **Linux Native**: Optimized for Linux container environments.

## Prerequisites

- **Docker**: Docker Desktop (Windows/macOS) or Docker Engine (Linux).
- **Printers**: 
  - Network-accessible thermal printers (ZPL) with a static IP address.
  - Local/USB printers configured via CUPS (for Linux hosts).
- **Portal Access**: Techship portal URL and `ApiSecret`.
- **Connectivity**: Network access to both the Techship portal(s) and the printer(s).

## Quick Start

### 1. Prepare Configuration
Download `docker-compose.yml` and `config/appsettings.example.json`. 
Copy `config/appsettings.example.json` to `config/appsettings.json`.

### 2. Configure Printers
Edit `config/appsettings.json` with your portal details and printer addresses. See [Configuration](#configuration) for details.

### 3. Start the Service
Run the following command in the directory containing `docker-compose.yml`:
```bash
docker-compose up -d
```

Note: The container runs in `privileged` mode to allow CUPS to access local USB printers.

## CUPS Management

The service includes a built-in CUPS instance for local/USB and SMB printing. You can manage printers via the CUPS web interface at `http://localhost:631` (if running locally) or `http://[container-ip]:631`.

- **SMB Printers**: To add an SMB printer, use the CUPS web interface. The device URI format is usually `smb://[user:password@]domain/server/printer`.
- **Credentials**:
    - **Username**: `admin` (default)
    - **Password**: `admin` (default)
- **Changing Credentials**: You can set custom credentials using environment variables in your `docker-compose.yml` or `docker run` command:
  ```yaml
  environment:
    - CUPS_USER=myuser
    - CUPS_PASSWORD=mypassword
  ```
- **Default Configuration**: CUPS is configured to allow remote access.
- **Persistence**: CUPS configurations and certificates are stored in Docker volumes (`cups-config`, `cups-certs`).

## Configuration

The service is configured via `appsettings.json`. You can define multiple printer configurations in the `Printers` array.

### Example Multi-Printer Configuration
```json
{
  "PrintService": {
    "Printers": [
      {
        "Enabled": true,
        "Portal": "https://yourportal.techship.io",
        "ApiSecret": "your-api-secret-here",
        "ConnectionName": "warehouse_a",
        "ZplConnectionType": "Network",
        "ZplPrinterAddress": "192.168.1.100:9100",
        "PollingIntervalMs": 2000
      },
      {
        "Enabled": true,
        "Portal": "https://yourportal.techship.io",
        "ApiSecret": "your-api-secret-here",
        "ConnectionName": "office",
        "PdfConnectionType": "Local",
        "PdfPrinterName": "HP_LaserJet_Office",
        "ThermalPdfPrinterName": "Zebra_Thermal_PDF",
        "SkipPackingSlips": false,
        "PollingIntervalMs": 5000
      },
      {
        "Enabled": true,
        "Portal": "https://yourportal.techship.io",
        "ApiSecret": "your-api-secret-here",
        "ConnectionName": "smb_printer",
        "PdfConnectionType": "Local",
        "PdfPrinterName": "SMB_Printer_Name",
        "PollingIntervalMs": 2000
      }
    ]
  }
}
```

### Configuration Reference

| Property | Description | Required | Default |
|----------|-------------|----------|---------|
| `Enabled` | Enable/disable this printer worker | No | `true` |
| `Portal` | Techship portal URL (e.g., `https://example.techship.io`) | Yes | - |
| `ApiSecret` | API Secret (x-secret-key) for authentication | Yes | - |
| `ConnectionName`| Connection Name (client key). Used for logging as well. | Yes | - |
| `ZplConnectionType` | `Network` or `Local` | No | `Network` |
| `ZplPrinterAddress` | IP:Port for network ZPL printers (e.g., `192.168.1.10:9100`) | Yes* | - |
| `ZplPrinterName` | System printer name for local ZPL | Yes* | - |
| `PdfConnectionType` | `Network` or `Local` | No | `Local` |
| `PdfPrinterName` | System printer name for PDF printing | Yes** | - |
| `ThermalPdfPrinterName` | System printer name for thermal label PDFs | No | - |
| `PollingIntervalMs`| Interval between polling requests | No | `2000` |
| `SkipPackingSlips` | If true, packing slips will be ignored | No | `false` |

*\* Required if printing ZPL labels.*
*\*\* Required if printing PDF documents.*

## Docker Usage

### Running with Docker Run (Environment Variables)
For simple single-printer setups:
```bash
docker run -d \
  --name techship-print-service \
  --restart unless-stopped \
  -e PrintService__Printers__0__Portal=https://yourportal.techship.io \
  -e PrintService__Printers__0__ApiSecret=your_secret \
  -e PrintService__Printers__0__ConnectionName=client_key \
  -e PrintService__Printers__0__ZplPrinterAddress=192.168.1.100:9100 \
  techship-print-service:latest
```

### Running with Mount (Custom Config)
```bash
docker run -d \
  --name techship-print-service \
  -v $(pwd)/config/appsettings.json:/app/appsettings.json:ro \
  techship-print-service:latest
```

## Troubleshooting

### View Logs
Check the logs to see the status of each printer worker:
```bash
docker logs techship-print-service
```

### Common Issues
- **Connection Failed**: Ensure the container has network access to the portal and the printer IP.
- **Authentication Error**: Verify `ApiSecret` and `ConnectionName`.
- **Printer Offline**: For network printers, try `telnet [IP] 9100` from the host to verify connectivity.
- **CUPS/Local Printers**: Ensure the printer is correctly installed in the host's CUPS and shared if necessary.

## Platform Notes

- **Windows**: Use Docker Desktop with WSL2 backend.
- **Linux**: Supported natively via Docker Engine.
- **macOS**: Supported via Docker Desktop.
- **Architecture**: Supports `amd64` and `arm64` (including Apple Silicon and Raspberry Pi).

## Updating
To update to the latest version:
```bash
docker-compose pull
docker-compose up -d
```
