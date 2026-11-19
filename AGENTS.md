# Repository guidance

- Follow `TMS_BUILD_PLAN.md`; the product is an airfreight-forwarding TMS, not a truck-fleet dispatch product.
- Keep the backend a modular monolith. Modules own their business rules and persistence writes; do not create microservices without a documented architecture decision.
- The API is the authorization boundary. Do not rely on hidden controls in the web or desktop clients.
- Every tenant-owned read, write, file, background job, and integration callback must be tenant-scoped. Fail closed when tenant context is missing.
- Never use production or customer data in local development. Do not commit credentials, tokens, personal data, or raw partner payloads.
- Keep issued AWB documents immutable. Corrections create a new version and an audit trail.
- Keep business transactions in PostgreSQL. Use an outbox for durable integration work; consumers must be idempotent.
- Prefer small vertical slices with domain behavior and acceptance criteria. Avoid adding Redis, PostGIS, microservices, or AI without measured requirements and a recorded decision.
- Run `dotnet build Tms.sln` and `npm run build` from `src/web` before proposing a code change as complete. Add focused tests with business behavior as implementation begins.
