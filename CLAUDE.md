# CLAUDE.md

Full-stack CMS, code-generated from `database/*.sql`. Stack: `CMS.API` (.NET 9, Dapper, :5000),
`CMS.API.Tests` (xUnit+Moq), `CMS.NG` (Angular 20 + PrimeNG, :4200); solution `src/CMS.slnx`.

## Always-needed
- **Commands**: `dotnet build|test CMS.slnx`, `dotnet run --project CMS.API` (Swagger `/swagger`);
  in `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
  Start the API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.
- **Web browsing**: use the `/browse` skill for *all* web browsing — **never** `mcp__claude-in-chrome__*`.

## Reference docs — read before the matching task
- **Add/change a feature** — backend + frontend rules (incl. the cross-cutting **RowAudit**: every
  repo write emits one via `IRowAuditWriter`; `GET /api/rowaudit` + the `RowAuditBadge` toolbar
  component surface a record's history on detail/form pages), reference-feature catalog, build
  order: [docs/conventions.md](docs/conventions.md) (canonical patterns: `spec/code-gen.convention.md`).
- **Setup, layout, full command reference, DB data & date refresh, design rationale** (one-time
  background): [docs/setup-notes.md](docs/setup-notes.md). Note: `database/*.sql` is schema only —
  an empty date-windowed list usually means the data predates the window (→ *Database data*).
