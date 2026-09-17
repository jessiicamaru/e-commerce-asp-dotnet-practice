# One Dockerfile for all seven services, selected by the PROJECT build argument.
#
#   docker build --build-arg PROJECT=src/Services/Catalog/Ecommerce.Catalog.WebApi -t ecommerce-catalog .
#
# Seven near-identical files would drift: a fix applied to six of them is
# invisible and nothing fails. This repository has already shipped a defect of
# exactly that shape - a pattern applied to one service and not another - and
# that was two files, not seven. See specs/005-containerise-services/research.md D1.
#
# The build context is this directory. server/.dockerignore is what keeps .env
# out of the image, and it is not optional: Docker does not read .gitignore.

# ---------------------------------------------------------------- build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /src

# Copy only the project files first and restore. A one-line code change then
# reuses this layer instead of re-downloading every package - which is the
# difference between a second build measured in seconds and one measured in
# minutes, and therefore the difference between this path being used and abandoned.
COPY Ecommerce.slnx ./
COPY src/BuildingBlocks/Ecommerce.Contracts/*.csproj      src/BuildingBlocks/Ecommerce.Contracts/
COPY src/BuildingBlocks/Ecommerce.Shared/*.csproj         src/BuildingBlocks/Ecommerce.Shared/
COPY src/ApiGateway/Ecommerce.ApiGateway/*.csproj         src/ApiGateway/Ecommerce.ApiGateway/

COPY src/Services/Catalog/Ecommerce.Catalog.Domain/*.csproj             src/Services/Catalog/Ecommerce.Catalog.Domain/
COPY src/Services/Catalog/Ecommerce.Catalog.Application/*.csproj        src/Services/Catalog/Ecommerce.Catalog.Application/
COPY src/Services/Catalog/Ecommerce.Catalog.Infrastructure/*.csproj     src/Services/Catalog/Ecommerce.Catalog.Infrastructure/
COPY src/Services/Catalog/Ecommerce.Catalog.WebApi/*.csproj             src/Services/Catalog/Ecommerce.Catalog.WebApi/

COPY src/Services/Identity/Ecommerce.Identity.Domain/*.csproj           src/Services/Identity/Ecommerce.Identity.Domain/
COPY src/Services/Identity/Ecommerce.Identity.Application/*.csproj      src/Services/Identity/Ecommerce.Identity.Application/
COPY src/Services/Identity/Ecommerce.Identity.Infrastructure/*.csproj   src/Services/Identity/Ecommerce.Identity.Infrastructure/
COPY src/Services/Identity/Ecommerce.Identity.WebApi/*.csproj           src/Services/Identity/Ecommerce.Identity.WebApi/

COPY src/Services/Inventory/Ecommerce.Inventory.Domain/*.csproj         src/Services/Inventory/Ecommerce.Inventory.Domain/
COPY src/Services/Inventory/Ecommerce.Inventory.Application/*.csproj    src/Services/Inventory/Ecommerce.Inventory.Application/
COPY src/Services/Inventory/Ecommerce.Inventory.Infrastructure/*.csproj src/Services/Inventory/Ecommerce.Inventory.Infrastructure/
COPY src/Services/Inventory/Ecommerce.Inventory.WebApi/*.csproj         src/Services/Inventory/Ecommerce.Inventory.WebApi/

COPY src/Services/Order/Ecommerce.Order.Domain/*.csproj                 src/Services/Order/Ecommerce.Order.Domain/
COPY src/Services/Order/Ecommerce.Order.Application/*.csproj            src/Services/Order/Ecommerce.Order.Application/
COPY src/Services/Order/Ecommerce.Order.Infrastructure/*.csproj         src/Services/Order/Ecommerce.Order.Infrastructure/
COPY src/Services/Order/Ecommerce.Order.WebApi/*.csproj                 src/Services/Order/Ecommerce.Order.WebApi/

COPY src/Services/Payment/Ecommerce.Payment.Domain/*.csproj             src/Services/Payment/Ecommerce.Payment.Domain/
COPY src/Services/Payment/Ecommerce.Payment.Application/*.csproj        src/Services/Payment/Ecommerce.Payment.Application/
COPY src/Services/Payment/Ecommerce.Payment.Infrastructure/*.csproj     src/Services/Payment/Ecommerce.Payment.Infrastructure/
COPY src/Services/Payment/Ecommerce.Payment.WebApi/*.csproj             src/Services/Payment/Ecommerce.Payment.WebApi/

COPY src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/*.csproj   src/Services/Orchestrator/Ecommerce.Orchestrator.WebApi/

RUN dotnet restore "${PROJECT}/$(basename ${PROJECT}).csproj"

# Now the source. Only src/ - the test projects are not needed to run a service,
# and .dockerignore keeps bin/, obj/ and every .env out regardless.
COPY src/ src/

RUN dotnet publish "${PROJECT}/$(basename ${PROJECT}).csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

# ---------------------------------------------------------------- runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Not root. A service that serves HTTP and talks to Postgres needs no privilege
# beyond that, and the aspnet image ships this user for the purpose.
USER $APP_UID

# Every service binds the same port inside its own container; compose maps it to
# whichever host port that service uses today. Program.cs honours this and falls
# back to its pinned localhost address when it is absent, so start-dev.sh is
# unaffected.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

# Resolved at build time so one Dockerfile can produce seven different services.
ARG PROJECT
ENV SERVICE_DLL="${PROJECT}"
ENTRYPOINT ["/bin/sh", "-c", "exec dotnet $(basename ${SERVICE_DLL}).dll"]
