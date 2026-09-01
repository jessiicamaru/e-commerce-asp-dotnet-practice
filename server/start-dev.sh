#!/usr/bin/env bash
# E-Commerce Microservices Development Startup Script (Git Bash / Linux)

echo "🚀 Starting E-Commerce Microservices Infrastructure..."

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
    start "Identity Service (5056)" cmd /k "dotnet run --project src/Services/Identity/Ecommerce.Identity.WebApi/"
    start "Catalog Service (5057)" cmd /k "dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/"
    start "API Gateway (5000)" cmd /k "dotnet run --project src/ApiGateway/Ecommerce.ApiGateway/"
else
    dotnet run --project src/Services/Identity/Ecommerce.Identity.WebApi/ &
    dotnet run --project src/Services/Catalog/Ecommerce.Catalog.WebApi/ &
    dotnet run --project src/ApiGateway/Ecommerce.ApiGateway/ &
fi

echo "✨ All services launched successfully!"
echo "  - API Gateway:  http://localhost:5000"
echo "  - Identity API: http://localhost:5056"
echo "  - Catalog API:  http://localhost:5057"
echo "  - pgAdmin:      http://localhost:5050"
echo "  - RabbitMQ UI:  http://localhost:15672"
