# CLAUDE.md

Full-stack CMS generated from `database/*.sql` per `spec/code-gen.convention.md`.

Stack: `CMS.API` (.NET 9, Dapper, port 5000), `CMS.API.Tests` (xUnit+Moq), `CMS.NG`
(Angular 20 + PrimeNG, port 4200); solution `src/CMS.slnx`.

## Commands
- `dotnet build|test CMS.slnx`; `dotnet run --project CMS.API` (Swagger `/swagger`).
- In `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
- Start API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.

## Building a feature
Backend + frontend rules, the reference-feature catalog (which existing feature to
mirror), and build order live in **[docs/conventions.md](docs/conventions.md)**.
One-time setup, layout, and reference-feature rationale: [docs/setup-notes.md](docs/setup-notes.md).

## gstack
For **all web browsing**, use the `/browse` skill; **never** the `mcp__claude-in-chrome__*` tools.
The available gstack skills are listed in the harness's skill context each session.
