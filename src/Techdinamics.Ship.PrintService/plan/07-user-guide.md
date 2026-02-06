# Subtask 07: End-User Installation and Configuration Guide

## Objective
Write comprehensive documentation for end users to install and configure the Docker-based print service with support for multiple printers/portals.

## Requirements

### 1. Create README.md
Main documentation file with:
- Overview of the print service
- Prerequisites (Docker, network printer)
- Quick start guide
- Multi-printer configuration examples
- Detailed configuration reference
- Troubleshooting section

### 2. Document Prerequisites
- Docker Desktop (Windows/macOS) or Docker Engine (Linux)
- Network-accessible thermal printer(s) (ZPL) with IP address
- Techship portal credentials (one or more portals)
- Network connectivity to portal(s) and printer(s)

### 3. Installation Steps

#### Option A: Docker Compose with Config File (Recommended for Multiple Printers)
```bash
# 1. Download docker-compose.yml and config/appsettings.example.json
# 2. Copy config/appsettings.example.json to config/appsettings.json
# 3. Edit config/appsettings.json with your printer configurations
# 4. Run:
docker-compose up -d
```

#### Option B: Docker Run with Single Printer
```bash
docker run -d \
  --name techship-print-service \
  --restart unless-stopped \
  --network host \
  -e PrintService__Printers__0__Name=MyPrinter \
  -e PrintService__Printers__0__Enabled=true \
  -e PrintService__Printers__0__Portal=techship.example.com \
  -e PrintService__Printers__0__Username=your_username \
  -e PrintService__Printers__0__Password=your_password \
  -e PrintService__Printers__0__ConnectionName=your_client_key \
  -e PrintService__Printers__0__PrinterId=unique_id \
  -e PrintService__Printers__0__ZplPrinterAddress=192.168.1.100:9100 \
  techship-print-service:latest
```

#### Option C: Docker Run with Multiple Printers
```bash
docker run -d \
  --name techship-print-service \
  --restart unless-stopped \
  --network host \
  -v $(pwd)/config/appsettings.json:/app/appsettings.json:ro \
  techship-print-service:latest
```

### 4. Multi-Printer Configuration Example
Example `config/appsettings.json` for multiple printers:
```json
{
  "PrintService": {
    "Printers": [
      {
        "Name": "Warehouse-A-Labels",
        "Enabled": true,
        "Portal": "techship.example.com",
        "Username": "warehouse_a",
        "Password": "secret1",
        "ConnectionName": "client_warehouse_a",
        "PrinterId": "printer-a-001",
        "ZplPrinterAddress": "192.168.1.100:9100",
        "PollingIntervalMs": 2000
      },
      {
        "Name": "Warehouse-B-Labels",
        "Enabled": true,
        "Portal": "techship.example.com",
        "Username": "warehouse_b",
        "Password": "secret2",
        "ConnectionName": "client_warehouse_b",
        "PrinterId": "printer-b-001",
        "ZplPrinterAddress": "192.168.1.101:9100",
        "PollingIntervalMs": 2000
      },
      {
        "Name": "Office-PackingSlips",
        "Enabled": true,
        "Portal": "techship2.example.com",
        "Username": "office_user",
        "ApiSecret": "api-key-here",
        "ConnectionName": "office_client",
        "PrinterId": "office-pdf-001",
        "PdfPrinterName": "HP-LaserJet",
        "PollingIntervalMs": 5000
      }
    ]
  }
}
```

### 5. Configuration Reference Table
| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| Name | Friendly name for logging | No | - |
| Enabled | Enable/disable this printer | No | true |
| Portal | Techship portal hostname | Yes | - |
| Username | Portal login username | Yes | - |
| Password | Portal login password | Yes* | - |
| ApiSecret | API secret (alternative to password) | Yes* | - |
| ConnectionName | Client key from portal | Yes | - |
| PrinterId | Unique identifier for this printer | Yes | - |
| ZplPrinterAddress | Thermal printer IP:port | Yes** | - |
| PdfPrinterName | PDF printer name | Yes** | - |
| PollingIntervalMs | Poll interval in milliseconds | No | 2000 |

*Either Password or ApiSecret required
**Either ZplPrinterAddress or PdfPrinterName required depending on print type

### 6. Printer Setup Guide
- How to find printer IP address
- Default ZPL port (9100)
- Testing printer connectivity: `telnet 192.168.1.100 9100`
- Common printer configurations
- Setting up multiple printers on same network

### 7. Troubleshooting Section
- Container won't start
- Cannot connect to portal
- Cannot connect to printer
- Print jobs not appearing
- One printer failing doesn't affect others
- How to view logs: `docker logs techship-print-service`
- Filtering logs by printer name

### 8. Updating the Service
```bash
docker-compose pull
docker-compose up -d
```

### 9. Platform-Specific Notes
- Windows: Docker Desktop with WSL2
- Linux: Docker Engine, network mode considerations
- macOS: Docker Desktop, ARM64 support for M1/M2
- Raspberry Pi: ARM64 image support

### 10. Scaling Considerations
- Single container handles multiple printers via concurrent workers
- Each printer configuration runs independently
- Disable unused printers by setting `Enabled: false`
- Monitor logs for per-printer status

## Acceptance Criteria
- Clear step-by-step installation instructions
- Multi-printer configuration clearly documented with examples
- All configuration options documented
- Troubleshooting covers common issues including multi-printer scenarios
- Works for users with basic Docker knowledge
- Includes examples for all platforms
