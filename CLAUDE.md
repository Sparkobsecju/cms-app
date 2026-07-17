# CLAUDE.md

Full-stack CMS, code-generated from `database/*.sql`. Stack: `CMS.API` (.NET 9, Dapper, :5000),
`CMS.API.Tests` (xUnit+Moq), `CMS.NG` (Angular 20 + PrimeNG, :4200); solution `src/CMS.slnx`.

## Always-needed
- **Build/test**: `dotnet build|test CMS.slnx`; in `src/CMS.NG`, `npx ng test --watch=false --browsers=ChromeHeadless`.
  Backend tests need no DB. Run/serve, e2e DB, full command reference → [setup-notes](docs/setup-notes.md).
- **Dev login**: `Admin` / `Admin` (rotated dev pwd `Admin+-*/`) → 系統管理 menu.
- **Web browsing**: `/browse` skill only — **never** `mcp__claude-in-chrome__*`.

## Task-specific — read the match before starting → [docs/reference-index.md](docs/reference-index.md)
- Add/change a feature → `docs/conventions.md`
- Auth / JWT / role authz / My Profile / Change Password → `spec/auth/Auth.md`.
  Passwords: **salted PBKDF2** (`Security/PasswordHasher.cs`); legacy unsalted SHA-256 hashes still
  verify and are auto-migrated on next login. Verify in code, not SQL (hashes are salted).
- Course PDF export → `spec/course/CoursePdf.md`
- Setup / layout / commands / DB data & date refresh / dev login seed → `docs/setup-notes.md`
- Code review + CSO security findings, fixed vs. deferred → `docs/reviews/` (fix log:
  `2026-07-17-fixes-applied.md` — password-hash fix done; other security items deferred to before-prod, see the Deferred table)
- Writing security-sensitive code (auth, crypto, SQL, XSS) → `docs/sampleDevise/` (cautionary
  before/after samples; per-framework catalogues for Web Forms / MVC 5 / ASP.NET Core)
- What gstack learned here → `docs/learnings.md`
