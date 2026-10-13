# MallSense

.NET 10 ASP.NET Core backend foundation for the AI Builder Cup mall discovery prototype.
The current scope includes the relational data model, a seeded fictional demo mall and
a Razor Pages shopper home page with keyword discovery and category filtering.

## Structure

- `MallSense.slnx`: solution.
- `src/MallSense/Domain`: mall, tenant and shopper entities and enums.
- `src/MallSense/Data/MallDbContext.cs`: EF Core relationships, indexes and constraints.
- `docs/data-model.md`: model explanation, diagram and write-service rules.
- `src/MallSense/Data/DemoSeeder.cs`: transactional, repeat-safe demo seeding.
- `src/MallSense/Pages/Index.cshtml`: responsive home page.
- `src/MallSense/wwwroot/images`: generated logo and promotion posters.

SQLite is the local development provider. EF Core packages are pinned to 10.0.0;
`global.json` selects a stable .NET 10 SDK with feature-band roll-forward.
Configuration uses `ConnectionStrings: MallDatabase` in `appsettings.json`.
The default database file is `mallsense.db` relative to the process working directory.

## Current state

The host registers the database context, ProblemDetails and a liveness endpoint at `/health`.
The root serves the shopper home page. Liveness does not check database readiness.
The page reads seeded directory and promotion data; search uses keyword matching, not an LLM.
No tenant write endpoints, authentication, LLM integrations or migrations exist yet.
Search and browsing do not yet record real shopper sessions or analytics events.

The scaffold has not been restored, compiled or run, following the workspace instruction
not to compile the .NET application. Database mapping is source-reviewed, not runtime-verified.
No database is created automatically on normal startup. The seed routine has not been executed.

## Seed and launch when compilation is authorized

From the repository root in PowerShell:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/MallSense -- --seed-demo
dotnet run --project src/MallSense --launch-profile http
```

Open `http://localhost:5007`. Both commands can restore and compile the app.
The first command explicitly creates a fresh prototype SQLite schema with `EnsureCreated`,
seeds it in one transaction, then exits. Running it again preserves existing demo data.
Seeding is rejected outside Development. A missing demo database shows a friendly setup
state on the home page.

Seed contents: fictional Meridian Mall, 2 floors, 12 tenants/outlets, 2 facilities,
2 published promotions and matching poster submissions, 1 event, a directed step-free
navigation graph and 6 simulated shopper sessions with intent and engagement events.
The page intentionally evaluates offers at the fixed demo date **13 October 2026**;
the date is visible in the UI. It is not a live promotion feed.

Before retaining real data or evolving this schema, replace the prototype initialization
with reviewed EF migrations. `EnsureCreated` does not update an existing schema, and
must not be mixed with migration-managed databases. Preserve needed data before replacing
any prototype database. No existing database is deleted or reset by the seed routine.

## Next implementation slice

Add validated application services and request/response DTOs for tenant promotion review,
then shopper session/intent writes. Keep persistence entities out of
public API responses. Follow the ownership and cross-record checks in `docs/data-model.md`.
Add LLM extraction only after the draft-review-publish path is working.
