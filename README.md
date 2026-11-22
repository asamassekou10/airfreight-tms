# Airfreight TMS

Multi-tenant airfreight-forwarding TMS. The product and architecture decisions are in [TMS_BUILD_PLAN.md](TMS_BUILD_PLAN.md).

## Current goal

The first milestone is a secure shipment-draft vertical slice: a manager approves party data, an authorized operator creates and validates a straight-shipment draft, tenant/branch permissions are enforced by the API, and the actions are auditable. Track progress in the GitHub milestone **M1: Secure shipment draft vertical slice**. The full product outcome and milestone exit criteria are in the build plan.

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

The issue #3 branch lookup is available at `GET /api/tenants/{tenantId}/branches/{branchId}` when both `Authentication__Authority` (the HTTPS identity provider authority) and `Authentication__Audience` (the API audience) are configured. Use an API access token. The lookup also requires PostgreSQL tables and active test memberships that match the checked-in tenancy diagram. The scaffold database does not provision those tables yet.

An active membership can read an active branch in its tenant through an explicit assignment or the membership's `all_branches` flag. The endpoint returns 401 for unauthenticated requests and 404 for inaccessible or absent branches. It treats `all_branches` as granting access to every active branch, including branches added later. The lookup uses membership and branch scope only. Role policies and migrations for operational data remain follow-up work under issue #3.

### Exercise the branch lookup locally

1. In a disposable PostgreSQL instance, create an empty `tms_m1_fixture` database and a separate login role named `tms_runtime` with no elevated privileges. As the schema owner, load [the synthetic fixture](docs/fixtures/m1-branch-access.sql) with `psql -d tms_m1_fixture -v runtime_role=tms_runtime -f docs/fixtures/m1-branch-access.sql`. Point `ConnectionStrings__Tms` at that database using the runtime login.
2. Configure the authority and audience of a development identity provider. In the fixture database, update the demo user's `identity_issuer` and `identity_subject` to match the `iss` and `sub` of a test user's API access token.
3. Call the branch endpoint with the test bearer token. Use the tenant and branch IDs from the fixture. Expect Chicago to return 200, Dallas to return 404 for the same tenant, and Frankfurt to return 404 even with its correct tenant ID. The configured endpoint should return 401 without a token.

To check the PostgreSQL lookup and HTTP endpoint, load a fresh fixture and run `dotnet run --project tests/Tms.BranchAccess.Checks` with `TMS_BRANCH_FIXTURE_CONNECTION` set to the schema owner's connection and `TMS_BRANCH_RUNTIME_CONNECTION` set to the runtime login's connection. The owner connection is used only to change fixture membership flags during the checks. HTTP checks use the API's bearer handler and synthetic identity metadata. Use the manual steps above to verify discovery and sign-in with the development provider.

For a database without demo data, apply the SQL files in [db/migrations](db/migrations) in numeric order as the schema owner, passing `-v runtime_role=<runtime-login>` to each `psql` invocation. The runtime login must have no membership in the owner role and no `BYPASSRLS` privilege. The API supplies verified identity and the requested tenant as transaction settings. PostgreSQL checks active membership and branch access before returning rows. Only the API should hold the runtime login.

The consignee approval slice uses the same branch route prefix. Operators create requests with `POST /party-requests` and a JSON `name`. Managers use `POST /party-requests/{requestId}/approve` or `/reject` with a JSON `reason`. `GET /consignees` lists approved records for the branch, and managers can read the latest branch events with `GET /audit-events`. The HTTP checks use separate synthetic operator and manager identities to exercise these actions.

This repository has the solution structure, local PostgreSQL setup, health endpoints, OpenAPI, and a minimal web shell. Authentication, tenant enforcement, domain entities, migrations, partner integrations, and business workflows are not implemented yet. The first implementation slice should establish tenant-aware persistence and backend authorization before exposing operational write endpoints.

Never place real credentials, customer data, partner payloads, or production connection strings in the repository. Production secrets belong in Azure Key Vault and are supplied through managed identity and environment configuration.

## Team workflow

- Branch from `main` using `feature/<short-name>` or `fix/<short-name>`.
- Keep pull requests focused and include workflow-level acceptance criteria.
- Require at least one teammate review before merging to `main` once branch protection is enabled on the hosted repository.
- Record architecture changes in `docs/decisions/` and update the build plan when a decision changes scope or security boundaries.
