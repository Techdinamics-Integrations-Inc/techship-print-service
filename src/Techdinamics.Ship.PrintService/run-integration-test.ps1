# Integration Test Script for Print Service
param(
    [switch]$Cleanup,
    [int]$WaitSeconds = 30
)

$ErrorActionPreference = "Stop"

Write-Host "=== Print Service Integration Test ===" -ForegroundColor Cyan

# Start services
Write-Host "`n1. Starting Docker services..." -ForegroundColor Yellow
docker compose -f docker-compose.integration.yml up -d --build

# Wait for services to be ready
Write-Host "`n2. Waiting for services to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 20

# Add test jobs via mock server API
Write-Host "`n3. Adding test print jobs..." -ForegroundColor Yellow

$testJobs = @(
    @{
        OrderId = "ORD-001"
        BatchNumber = "BATCH-2024-001"
        ClientName = "Test Client"
        CarrierName = "FedEx"
        Labels = @(
            @{ Type = "DIRECT"; Purpose = "LABEL"; Data = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("^XA^FO50,50^ADN,36,20^FDOrder: ORD-001^FS^XZ")) }
        )
    },
    @{
        OrderId = "ORD-002"
        BatchNumber = "BATCH-2024-001"
        ClientName = "Test Client"
        CarrierName = "UPS"
        Labels = @(
            @{ Type = "DIRECT"; Purpose = "LABEL"; Data = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("^XA^FO50,50^ADN,36,20^FDOrder: ORD-002^FS^XZ")) },
            @{ Type = "PDF"; Purpose = "PACKINGSLIP"; Data = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes("../Techdinamics.Ship.PrintService.MockServer/TestData/sample_packingslip.pdf")) }
        )
    }
)

foreach ($job in $testJobs) {
    $body = $job | ConvertTo-Json -Depth 5
    Invoke-RestMethod -Uri "http://localhost:5080/api/test/add-job" -Method Post -Body $body -ContentType "application/json"
    Write-Host "  Added job: $($job.OrderId)" -ForegroundColor Green
}

# Wait for print service to process
Write-Host "`n4. Waiting $WaitSeconds seconds for print service to process jobs..." -ForegroundColor Yellow
Start-Sleep -Seconds $WaitSeconds

# Check results
Write-Host "`n5. Checking results..." -ForegroundColor Yellow

# Verify job marked as printed on mock server
Write-Host "`n6. Server print log:" -ForegroundColor Yellow
$printedJobs = Invoke-RestMethod -Uri "http://localhost:5080/api/test/printed-log" -Method Get
$printedJobs | ForEach-Object { Write-Host "  $_" -ForegroundColor Green }

if ($printedJobs.Count -ge 2) {
    Write-Host "  SUCCESS: At least 2 jobs were processed and confirmed." -ForegroundColor Green
} else {
    Write-Host "  FAILED: Not all jobs were processed." -ForegroundColor Red
}

# Cleanup
if ($Cleanup) {
    Write-Host "`n7. Cleaning up..." -ForegroundColor Yellow
    docker compose -f docker-compose.integration.yml down
}

Write-Host "`n=== Integration Test Complete ===" -ForegroundColor Cyan
