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
  `AuthController`/`AuthRepository`/`JwtTokenService`; login writes nothing; `PasswordHash` never
  leaves the repo. **My Profile** (個人資料): `PUT /api/Auth/profile` (`[Authorize]`) updates **only**
  `UserName` for the user in the JWT — `UserId` comes from the token, never the body (the DTO has no
  `UserId`), roles are immutable; name is trimmed and blank → `400`. It is a non-audited self-service
  action (like `reset-password`). Frontend `/profile` page shows `UserId` + roles read-only and an
  editable name; `AuthService.updateUserName` PUTs `{ userName }` and on success refreshes the shell
  name + session storage (token kept); linked as 個人資料 My Profile in the sidebar footer.
  **Change Password** (變更密碼): `POST /api/Auth/change-password` (`[Authorize]`) on the `/profile` page —
  `UserId` from the JWT; verifies `SHA256(current)` == stored `PasswordHash` (compared in SQL, hash never
  leaves the repo), enforces complexity (`Security/PasswordPolicy`: len ≥ 8 **and** ≥ 3 of 4 classes
  upper/lower/digit/symbol), and new == confirm; on success one `UPDATE` sets `PasswordHash =
  SHA256(new)` (via `Security/PasswordHasher`) + `PasswordUpdatedTime`, returns `204`. Any failure →
  `400 ErrorResponse` (changes nothing); **no hash crosses the wire**. Non-audited, session/token kept.
  **End-to-end auth is wired**: backend `AddJwtBearer` validates against the
  same key (via `ISigningKeyProvider`) with a global fallback policy requiring auth on every endpoint
  except the `[AllowAnonymous]` `login` action (every other `AuthController` action — e.g. `profile` —
  needs a token); frontend has a real login page, a Bearer
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
