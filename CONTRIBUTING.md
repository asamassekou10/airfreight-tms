# Contributing

## Local workflow

1. Read `TMS_BUILD_PLAN.md` and the relevant architecture decisions.
2. Create a focused branch from `main`, for example `feature/master-data-parties`.
3. Implement one vertical workflow slice and include acceptance criteria in the pull request.
4. Run `dotnet build Tms.sln` and `npm run build` from `src/web`.
5. Request a teammate review before merging.

## Design constraints

- Keep business rules and authorization in the API.
- Keep module boundaries explicit; do not add cross-module database writes.
- Scope tenant-owned data and files by tenant from the first domain feature.
- Use database migrations for schema changes; never edit shared or production databases manually.
- Add migrations and seed data only when the associated domain feature is implemented.
- Never store real secrets or operational customer data in source control.
