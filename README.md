# Airfreight TMS

Multi-tenant airfreight-forwarding TMS. The product and architecture decisions are in [TMS_BUILD_PLAN.md](TMS_BUILD_PLAN.md).

## Repository layout

```text
docs/                         Architecture decisions and team workflow
src/server/Tms.Api/           ASP.NET Core API
src/server/Tms.Application/   Use cases and application contracts
src/server/Tms.Domain/        Domain model and rules
src/server/Tms.Infrastructure/Database and external service adapters
src/server/Tms.Worker/        Background integration processing
src/web/                      React + TypeScript web client
```

The backend is a modular monolith. Keep domain modules in the same deployable API until a measured business or scaling need justifies a service boundary. The optional Windows client is planned as a later WPF client of the same API.

## Prerequisites

- .NET 10 SDK
- Node.js 22.12 or later with npm
- Docker Desktop or another Docker Engine with Compose

On macOS with Homebrew, install the .NET 10 SDK with `brew install dotnet@10`. Homebrew installs it as a keg-only formula. Add its `libexec` directory to your shell PATH as shown by `brew info dotnet@10`, then verify `dotnet --version` reports a 10.0 SDK.

## Start local development

From the repository root:

1. Start PostgreSQL:

   ```sh
   docker compose up -d postgres
   ```

2. Restore and build the backend:

   ```sh
   dotnet restore Tms.sln
   dotnet build Tms.sln --no-restore
   ```

3. Start the API in one terminal. The connection string below is for the local Docker database only:

   ```sh
   ConnectionStrings__Tms='Host=localhost;Port=5432;Database=tms_dev;Username=tms_dev;Password=tms_dev_only' \
     dotnet run --project src/server/Tms.Api --urls http://localhost:5080
   ```

   OpenAPI is available at `http://localhost:5080/openapi/v1.json` in Development. Liveness and database readiness are at `/health/live` and `/health/ready`.

4. Install and run the web client in another terminal:

   ```sh
   cd src/web
   npm ci
   npm run dev
   ```

   The Vite development server runs at `http://localhost:5173`.

5. The worker is scaffolded but has no message consumers yet. Start it when working on background processing:

   ```sh
   dotnet run --project src/server/Tms.Worker
   ```

## Current scaffold status

This repository has the solution structure, local PostgreSQL setup, health endpoints, OpenAPI, and a minimal web shell. Authentication, tenant enforcement, domain entities, migrations, partner integrations, and business workflows are not implemented yet. The first implementation slice should establish tenant-aware persistence and backend authorization before exposing operational write endpoints.

Never place real credentials, customer data, partner payloads, or production connection strings in the repository. Production secrets belong in Azure Key Vault and are supplied through managed identity and environment configuration.

## Team workflow

- Branch from `main` using `feature/<short-name>` or `fix/<short-name>`.
- Keep pull requests focused and include workflow-level acceptance criteria.
- Require at least one teammate review before merging to `main` once branch protection is enabled on the hosted repository.
- Record architecture changes in `docs/decisions/` and update the build plan when a decision changes scope or security boundaries.
