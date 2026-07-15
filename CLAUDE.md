# CLAUDE.md

Full-stack CMS (code-generated from `database/*.sql`). Stack: `CMS.API` (.NET 9,
Dapper, :5000), `CMS.API.Tests` (xUnit+Moq), `CMS.NG` (Angular 20 + PrimeNG, :4200);
solution `src/CMS.slnx`.

## Commands
- `dotnet build|test CMS.slnx`; `dotnet run --project CMS.API` (Swagger `/swagger`).
- In `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
- Start API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.

## Reference docs — read before the matching task
- **Build/change a feature** — backend + frontend rules, reference-feature catalog,
  build order: [docs/conventions.md](docs/conventions.md) (canonical patterns:
  `spec/code-gen.convention.md`).
- **Setup, layout, design rationale** (one-time background): [docs/setup-notes.md](docs/setup-notes.md).

## Web browsing
For **all** web browsing use the `/browse` skill — **never** the `mcp__claude-in-chrome__*` tools.
