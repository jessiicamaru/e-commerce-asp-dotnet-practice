# E-Commerce Microservices Development Startup Script (PowerShell)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "🚀 Starting E-Commerce Microservices Infrastructure..." -ForegroundColor Cyan

# 1. Start Docker Containers
Write-Host "📦 Starting Docker containers (Identity DB, Catalog DB, Orchestrator DB, RabbitMQ, pgAdmin)..." -ForegroundColor Yellow
docker compose up -d

# 2. Apply EF Core Migrations
Write-Host "🔄 Applying EF Core Migrations for Identity DB (Port 5432)..." -ForegroundColor Yellow
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/

Write-Host "🔄 Applying EF Core Migrations for Catalog DB (Port 5433)..." -ForegroundColor Yellow
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/

Write-Host "🔄 Applying EF Core Migrations for Orchestrator DB (Port 5436)..." -ForegroundColor Yellow
dotnet ef database update --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/ --startup-project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/

# 3. Launch Microservices in Separate Terminal Windows
Write-Host "🌐 Launching Microservices and API Gateway..." -ForegroundColor Green

Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$ScriptDir'; Write-Host 'Starting Identity Service (Port 5056)...' -ForegroundColor Green; dotnet run --project src/Services/Identity/Ecommerce.Identity.WebApi/"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$ScriptDir'; Write-Host 'Starting Catalog Service (Port 5057)...' -ForegroundColor Green; dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$ScriptDir'; Write-Host 'Starting Orchestrator Service (Port 5058)...' -ForegroundColor Green; dotnet run --project src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/"
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$ScriptDir'; Write-Host 'Starting API Gateway (Port 5000)...' -ForegroundColor Green; dotnet run --project src/ApiGateway/Ecommerce.ApiGateway/"

Write-Host "✨ All services launched successfully!" -ForegroundColor Green
Write-Host "  - API Gateway:      http://localhost:5000" -ForegroundColor Cyan
Write-Host "  - Identity API:     http://localhost:5056" -ForegroundColor Cyan
Write-Host "  - Catalog API:      http://localhost:5057" -ForegroundColor Cyan
Write-Host "  - Orchestrator API: http://localhost:5058" -ForegroundColor Cyan
Write-Host "  - pgAdmin:          http://localhost:5050" -ForegroundColor Cyan
Write-Host "  - RabbitMQ UI:      http://localhost:15672" -ForegroundColor Cyan
