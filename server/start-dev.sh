#!/usr/bin/env bash
# E-Commerce Microservices Development Startup Script (Git Bash / Linux)

echo "🚀 Starting E-Commerce Microservices Infrastructure..."

# Prevent MSYS path conversion on Windows for cmd flags
export MSYS_NO_PATHCONV=1

# 1. Start Docker Containers
echo "📦 Starting Docker containers (PostgreSQL Identity, PostgreSQL Catalog, RabbitMQ, pgAdmin)..."
docker compose up -d

# 2. Apply EF Core Migrations
echo "🔄 Applying EF Core Migrations for Identity DB (Port 5432)..."
dotnet ef database update --project src/Services/Identity/Ecommerce.Identity.Infrastructure/ --startup-project src/Services/Identity/Ecommerce.Identity.WebApi/

echo "🔄 Applying EF Core Migrations for Catalog DB (Port 5433)..."
dotnet ef database update --project src/Services/Catalog/Ecommerce.Catalog.Infrastructure/ --startup-project src/Services/Catalog/Ecommerce.Catalog.WebApi/

# 3. Launch Microservices in Separate Windows
echo "🌐 Launching Microservices and API Gateway..."

if [[ "$OSTYPE" == "msys" || "$OSTYPE" == "win32" ]]; then
    powershell.exe -Command "Start-Process powershell -ArgumentList '-NoExit', '-Command', 'cd \"$PWD\"; Write-Host \"Identity Service (Port 5056)\" -ForegroundColor Green; dotnet run --project src/Services/Identity/Ecommerce.Identity.WebApi/'"
    powershell.exe -Command "Start-Process powershell -ArgumentList '-NoExit', '-Command', 'cd \"$PWD\"; Write-Host \"Catalog Service (Port 5057)\" -ForegroundColor Green; dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/'"
    powershell.exe -Command "Start-Process powershell -ArgumentList '-NoExit', '-Command', 'cd \"$PWD\"; Write-Host \"API Gateway (Port 5000)\" -ForegroundColor Green; dotnet run --project src/ApiGateway/Ecommerce.ApiGateway/'"
else
    dotnet run --project src/Services/Identity/Ecommerce.Identity.WebApi/ &
    dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/ &
    dotnet run --project src/ApiGateway/Ecommerce.ApiGateway/ &
fi

echo "✨ All 3 services launched successfully in separate windows!"
echo "  - API Gateway:  http://localhost:5000"
echo "  - Identity API: http://localhost:5056"
echo "  - Catalog API:  http://localhost:5057"
echo "  - pgAdmin:      http://localhost:5050"
echo "  - RabbitMQ UI:  http://localhost:15672"
