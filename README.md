# Mall Discovery

.NET 10 ASP.NET Core backend foundation for the AI Builder Cup mall discovery prototype.
The current scope is the relational data model for mall management, tenants and shoppers.

## Structure

- `MallDiscovery.slnx`: solution.
- `src/MallDiscovery/Domain`: mall, tenant and shopper entities and enums.
- `src/MallDiscovery/Data/MallDbContext.cs`: EF Core relationships, indexes and constraints.
- `docs/data-model.md`: model explanation, diagram and write-service rules.

SQLite is the local development provider. EF Core packages are pinned to 10.0.0;
`global.json` selects a stable .NET 10 SDK with feature-band roll-forward.
Configuration uses `ConnectionStrings: MallDatabase` in `appsettings.json`.
The default database file is `mall-discovery.db` relative to the process working directory.

## Current state

The host registers the database context, ProblemDetails and a liveness endpoint at `/health`.
The root endpoint identifies the application. Liveness does not check database readiness.
There are no UI, CRUD endpoints, authentication, LLM integrations, seed data or migrations yet.
No data endpoints are exposed before ownership and authorization checks are implemented.

The scaffold has not been restored, compiled or run, following the workspace instruction
not to compile the .NET application. Database mapping is source-reviewed, not runtime-verified.
No database is created automatically on startup.

When compilation is authorized, restore dependencies, add an initial EF migration,
review its generated schema and apply it explicitly. This will require a compatible
EF Core 10 CLI tool. Do not mix `EnsureCreated` with migration-based schema management.

## Next implementation slice

Add validated application services and request/response DTOs for mall seed data and tenant
promotion review, then shopper session/intent writes. Keep persistence entities out of
public API responses. Follow the ownership and cross-record checks in `docs/data-model.md`.
Add LLM extraction only after the draft-review-publish path is working.
