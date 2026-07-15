# CLAUDE.md

Full-stack CMS generated from `database/*.sql` per `spec/code-gen.convention.md`.

Stack: `CMS.API` (.NET 9, Dapper, port 5000), `CMS.API.Tests` (xUnit+Moq), `CMS.NG`
(Angular 20 + PrimeNG, port 4200); solution `src/CMS.slnx`.

## Commands
- `dotnet build|test CMS.slnx`; `dotnet run --project CMS.API` (Swagger `/swagger`).
- In `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
- Start API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.

## Building a feature
Mirror the closest existing reference feature:
- **AppRole** (`features/app-roles`) — string PK + N-N.
- **PublishStatus** (`features/publish-statuses`) — caller-supplied tinyint PK.
- **CourseGroup** (`features/course-groups`) — IDENTITY PK + provides a lookup.
- **Course** (`features/courses`) — IDENTITY PK + FK-JOIN display + two N-N + date fields; the most complete example.
- **AppUser** (`features/app-users`) — string PK + N-N + a server-managed secret field (`PasswordHash`, never in any DTO) + an action endpoint (`POST /{id}/reset-password`). Spec: `spec/auth/AppUser.md`.

Backend + frontend build rules: **[docs/conventions.md](docs/conventions.md)**.
One-time setup, layout, and reference-feature rationale: [docs/setup-notes.md](docs/setup-notes.md).

## gstack
For **all web browsing**, use the `/browse` skill; **never** the `mcp__claude-in-chrome__*` tools.
The available gstack skills are listed in the harness's skill context each session.
