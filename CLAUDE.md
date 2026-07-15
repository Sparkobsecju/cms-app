# CLAUDE.md

Full-stack CMS, code-generated from `database/*.sql`. Stack: `CMS.API` (.NET 9, Dapper, :5000),
`CMS.API.Tests` (xUnit+Moq), `CMS.NG` (Angular 20 + PrimeNG, :4200); solution `src/CMS.slnx`.

## Always-needed
- **Commands**: `dotnet build|test CMS.slnx`, `dotnet run --project CMS.API` (Swagger `/swagger`);
  in `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
  Start the API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.
- **Web browsing**: use the `/browse` skill for *all* web browsing — **never** `mcp__claude-in-chrome__*`.

## Reference docs — read the matching one before the task
- **Add/change a feature** → [docs/conventions.md](docs/conventions.md): backend + frontend rules,
  cross-cutting concerns (RowAudit write + history, global exception/HTTP-error handling),
  reference-feature catalog, build order. Canonical patterns: `spec/code-gen.convention.md`.
- **Setup, layout, full command reference, DB data & date refresh, design rationale** (one-time
  background) → [docs/setup-notes.md](docs/setup-notes.md). `database/*.sql` is schema only — an
  empty date-windowed list usually means the data predates the window (→ *Database data*).
