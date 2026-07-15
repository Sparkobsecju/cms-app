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
- **Auth / login (JWT)** → [spec/auth/Auth.md](spec/auth/Auth.md): `POST /api/Auth/login` verifies
  `AppUser` credentials (UserId + `IsActive=1` + SHA-256 password hash, all in one WHERE → generic
  `401`) and issues a 24h JWT signed with `SysConfig['appConfig'].symmetricSecurityKey` (read at
  runtime), carrying `UserId`/`UserName` + one `ClaimTypes.Role` per `AppUserRole`. Stateless
  `AuthController`/`AuthRepository`/`JwtTokenService`; no writes, not row-audited; `PasswordHash`
  never leaves the repo. **End-to-end auth is wired**: backend `AddJwtBearer` validates against the
  same key (via `ISigningKeyProvider`) with a global fallback policy requiring auth on every
  controller except `[AllowAnonymous]` `AuthController`; frontend has a real login page, a Bearer
  `authInterceptor`, an `authGuard` on all routes but `/login`, session-storage profile in
  `AuthService`, logout in the shell, and the 系統管理 Admin menu gated on the `Admin` role.
  Browser-smoke-tested on :4200 against the live API: guard redirects (`/` and protected routes →
  `/login`), invalid creds → generic banner with no session stored, Admin group hidden when signed
  out, and the cross-origin `POST /api/Auth/login` 401 reaches the browser (CORS intact). The
  authenticated success path (valid login → session/username/logout, Admin visible) was not
  smoke-tested (needs a real active `AppUser` credential; don't read it from the DB).
  **Known issue**: `/login` renders *inside* the app shell (root `App` always paints the sidebar +
  Logout around `<router-outlet>`), so a signed-out visitor sees app nav and a Logout button on the
  login page. Fix by moving the sidebar into a layout route that wraps only the guarded routes.
- **Setup, layout, full command reference, DB data & date refresh, design rationale** (one-time
  background) → [docs/setup-notes.md](docs/setup-notes.md). `database/*.sql` is schema only — an
  empty date-windowed list usually means the data predates the window (→ *Database data*).
