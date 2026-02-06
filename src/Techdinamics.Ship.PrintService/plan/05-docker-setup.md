# Subtask 05: Docker Setup for Multi-Platform Support

## Objective
Create Dockerfile and docker-compose configuration for multi-platform deployment (Windows, Linux, macOS on x64 and ARM) with support for multiple printer configurations.

## Requirements

### 1. Update Dockerfile
Create multi-stage Dockerfile for optimized builds:

```dockerfile
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore -a $TARGETARCH
COPY . .
RUN dotnet publish -a $TARGETARCH -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Techdinamics.Ship.PrintService.dll"]
```

### 2. Create docker-compose.yml
Support multiple printers via JSON config file mount or indexed environment variables:

```yaml
version: '3.8'
services:
  print-service:
    build: .
    image: techship-print-service:latest
    container_name: techship-print-service
    restart: unless-stopped
    volumes:
      - ./config/appsettings.json:/app/appsettings.json:ro
    network_mode: host  # For printer access
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"
```

Alternative using indexed environment variables:
```yaml
    environment:
      # Printer 0
      - PrintService__Printers__0__Name=Warehouse1
      - PrintService__Printers__0__Enabled=true
      - PrintService__Printers__0__Portal=techship.example.com
      - PrintService__Printers__0__ApiSecret=secret1
      - PrintService__Printers__0__ConnectionName=client1
      - PrintService__Printers__0__PrinterId=printer1
      - PrintService__Printers__0__ZplPrinterAddress=192.168.1.100:9100
      # Printer 1
      - PrintService__Printers__1__Name=Warehouse2
      - PrintService__Printers__1__Enabled=true
      - PrintService__Printers__1__Portal=techship.example.com
      - PrintService__Printers__1__ApiSecret=secret2
      - PrintService__Printers__1__ConnectionName=client2
      - PrintService__Printers__1__PrinterId=printer2
      - PrintService__Printers__1__ZplPrinterAddress=192.168.1.101:9100
```

### 3. Create config/appsettings.example.json
Template for multi-printer configuration:
```json
{
  "PrintService": {
    "Printers": [
      {
        "Name": "Warehouse1-ZPL",
        "Enabled": true,
        "Portal": "techship.example.com",
        "ApiSecret": "",
        "ConnectionName": "your_client_key",
        "PrinterId": "unique_printer_id_1",
        "ZplPrinterAddress": "192.168.1.100:9100",
        "PollingIntervalMs": 2000
      },
      {
        "Name": "Warehouse2-PDF",
        "Enabled": true,
        "Portal": "techship2.example.com",
        "ConnectionName": "another_client_key",
        "PrinterId": "unique_printer_id_2",
        "PdfPrinterName": "network-pdf-printer",
        "PollingIntervalMs": 3000
      }
    ]
  }
}
```

### 4. Multi-Architecture Build
Create build script for multi-arch images:
```bash
docker buildx build --platform linux/amd64,linux/arm64 -t techship-print-service:latest .
```

### 5. Update .dockerignore
Ensure proper exclusions:
```
**/bin/
**/obj/
**/.git
**/node_modules
*.md
.env
config/appsettings.json
```

### 6. Health Check
Add health check to Dockerfile:
```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/health || exit 1
```

Or implement simple file-based health check if no HTTP endpoint.

## Acceptance Criteria
- Docker image builds successfully
- Image runs on Linux x64 and ARM64
- Configuration via JSON file or environment variables works
- Supports multiple printer configurations in single container
- Container can reach multiple network printers
- Container restarts on failure
- Logs are accessible via `docker logs` with printer context
