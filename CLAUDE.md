# CLAUDE.md

Full-stack CMS, code-generated from `database/*.sql`. Stack: `CMS.API` (.NET 9, Dapper, :5000),
`CMS.API.Tests` (xUnit+Moq), `CMS.NG` (Angular 20 + PrimeNG, :4200); solution `src/CMS.slnx`.

## Always-needed
- **Build/test**: `dotnet build|test CMS.slnx`; in `src/CMS.NG`, `npx ng test --watch=false --browsers=ChromeHeadless`.
  Backend tests need no DB. Run/serve, e2e DB, full command reference → [setup-notes](docs/setup-notes.md).
- **Dev login**: `Admin` / `Admin` (rotated dev pwd `Admin+-*/`) → 系統管理 menu.
- **Web browsing**: `/browse` skill only — **never** `mcp__claude-in-chrome__*`.

## Task-specific — read the match first; detail + gotchas in [docs/reference-index.md](docs/reference-index.md)
| When you're… | Read |
|---|---|
| Adding/changing a feature | `docs/conventions.md` |
| Touching auth / JWT / roles / My Profile / password | `spec/auth/Auth.md` |
| Working on Course PDF export | `spec/course/CoursePdf.md` |
| Setting up / commands / DB & date refresh / dev seed | `docs/setup-notes.md` |
| Checking security findings, fixed vs. deferred | `docs/reviews/` |
| Writing security-sensitive code (auth, crypto, SQL, XSS) | `docs/sampleDevise/` |
| Seeing what the app/UI looks like | `docs/screenshots/` |
| Wanting what gstack learned here | `docs/learnings.md` |
