# ADR 0001: Architecture baseline

- **Status:** Accepted for initial implementation
- **Date:** 2026-11-19

## Context

The product requirements describe a role-based airfreight-forwarding TMS with tenant-scoped master data, shipments, HAWB/MAWB workflows, partner messaging, finance visibility, and audit history. A browser SaaS is the primary product. A Windows client may be added later.

## Decision

- Use a React + TypeScript SPA for the SaaS client and C# + ASP.NET Core on .NET 10 for the API.
- Use a modular monolith backed by managed PostgreSQL. Add a separate .NET worker for asynchronous integrations.
- Use Azure Container Apps, Azure Database for PostgreSQL, Azure Service Bus, private Blob Storage, Key Vault, and Azure Monitor for the initial Azure deployment.
- Treat a freight-forwarding company as the tenant and branches as operational scopes within a tenant.
- Use tenant IDs on tenant-owned records, application policies, and PostgreSQL row-level security as defense in depth.
- If a Windows desktop client is built, it uses the same API and identity system. Offline operation is not part of the initial release.

## Consequences

- All domain rules and authorization stay server-side and are shared by future clients.
- The initial system has one deployable API and one worker, avoiding distributed transactions between business modules.
- Tenant isolation is required from the first persisted domain feature.
- A WPF client adds a second UI codebase and will be sequenced after the web workflows stabilize.
- Partner standards and message contracts are isolated behind adapters; the first partner integration is a Phase 0 validation item.
