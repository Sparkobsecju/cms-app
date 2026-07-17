# Setup Notes & Design Decisions

One-time context and rationale for the CMS full-stack app. Recurring build rules live
in [conventions.md](conventions.md); this file holds the background they rest on.

## Repository layout

```
database/*.sql            Source-of-truth schema (auth, admin, course, promotion)
spec/                     Conventions + feature-spec templates + UI mockups
  code-gen.convention.md  Backend/frontend code-gen patterns (READ FIRST)
  sample1/2.spec.md       Worked feature specs (Course, SkillTrain)
  ui-sample-*.png         Visual style reference (list/view/add/edit)
src/
  CMS.slnx                .NET solution (note: .slnx, not .sln)
  CMS.API/                .NET 9 Web API — Dapper, Swagger, CORS — port 5000
  CMS.API.Tests/          xUnit + Moq (mock-based, no live DB)
  CMS.NG/                 Angular 20 standalone + PrimeNG — port 4200
README.md                 Run instructions
```

## Full command reference

```bash
# Backend (from src/)
dotnet build CMS.slnx
dotnet test  CMS.slnx                       # controller unit tests (mocked repos)
dotnet run --project CMS.API                # http://localhost:5000/swagger

# Frontend (from src/CMS.NG/)
npm install
npm start                                   # ng serve → http://localhost:4200
npm test                                    # Karma + Jasmine
npx ng test --watch=false --browsers=ChromeHeadless   # headless CI run
npx ng build                                # production build
```

Run the API before `ng serve`; the dev environment points the SPA at `http://localhost:5000/api`.
Backend tests need no database. End-to-end use needs the `CMS` DB on `.\SQLEXPRESS`.

## Database data & date refresh

`database/*.sql` are **schema only** (CREATE TABLE + FK constraints); they carry **no seed
`INSERT`s**. The `CMS` database is populated separately (restore/import) — e.g. it ships
~31k `FeaturedPromoItem` rows and ~1.1k `Promotion2` rows spanning 2019–2026.

Consequence: a **date-windowed list can look empty even though rows exist**, because the data
predates the window. The FeaturedPromoItem list defaults to the current Mon–Sun week, so if
the newest `ScheduleOn` is in the past, the default view shows nothing.

Fix — `database/refresh-promo-dates.sql` shifts every `FeaturedPromoItem.ScheduleOn` so the
newest row lands ~30 days from today, preserving relative spacing. **Idempotent** (re-running
re-anchors the newest to today+30 with no drift):

```bash
sqlcmd -S ".\SQLEXPRESS" -d CMS -E -C -i database/refresh-promo-dates.sql
```

## Dev login seed

The authenticated path needs a real `AppUser`. Dev-only login `Admin` / password `Admin`
(`IsActive=1`, `Admin` role → 系統管理 menu) is seeded in the local `CMS` DB — **not** in
`database/*.sql`. Recreate via upsert into `AppUser` (set `PasswordHash`, see below) plus a row in
`AppUserRole('Admin','Admin')`.

**Password hash scheme:** the canonical scheme is now **salted PBKDF2** (`Security/PasswordHasher.cs`),
stored as `PBKDF2$SHA256$<iterations>$<salt>$<hash>` — a random salt per call, so it can't be written as
a fixed SQL literal. For a quick dev seed you can still upsert the **legacy lowercase-hex SHA-256** of the
password (e.g. `Admin`): login accepts it and **auto-migrates the row to PBKDF2 on the first successful
sign-in**. To seed a PBKDF2 hash directly instead, compute it with `PasswordHasher.Hash("Admin")` (LINQPad
/ a throwaway test) and paste the resulting string.

Note: **Change Password** rejects `Admin` as a *new* password (needs ≥8 chars, 3-of-4 classes), so
a rotated dev password looks like `Admin+-*/`.

## Backend detail

- `IDbConnectionFactory` opens `SqlConnection` from connection string `CMS` (`appsettings.json`).
- Models: `{Table}.cs` carries FK aliases, subquery counts, and N-N id lists; `{Table}Request.cs`
  is the write DTO; `{Table}Query.cs` is the filter DTO.
- `PUT` takes the pkid/key from the body (no route param). Lookups: `GET /api/lookups/{plural}`.
- N-N reads use a second query (`QueryMultiple`) in `GetByIdAsync`; the Dapper `DateOnly`/`TimeOnly`
  type handlers are registered in `Program.cs` (`Data/DapperTypeHandlers.cs`).
- CORS allows any localhost origin (`Program.cs`); Swagger UI at `/swagger`.
- Tests assert controller results (Ok/Created/Conflict/NotFound/BadRequest). Repository SQL is only
  exercised against the real database, not in unit tests.

## Frontend detail

- List filter drawer: `p-select` uses `appendTo="body"`, `[filter]` for 10+ options, virtual scroll
  for 100+. Form: string PK is `disable()`d in edit mode and read via `getRawValue()`; `p-multiselect`
  N-N uses `[maxSelectedLabels]="9999"`; `p-datepicker` for `date`/`time` (ISO ↔ `Date`).
- Component specs also provide `provideRouter([])`, `provideNoopAnimations()`, `ConfirmationService`,
  `MessageService` alongside the `jasmine.createSpyObj` service stubs.

### Sidebar styling

`app.ts`/`app.html`/`app.scss` are styled after the PrimeNG **Ultima** admin template (light rounded
sidebar, uppercase section caption, rounded menu rows, rotating submenu chevron, primary-tinted active
"pill"). Colors use PrimeNG `--p-*` design tokens so the sidebar tracks the active theme (light/dark).
Only 系統管理 Admin → 角色 AppRole is wired; other groups are visual placeholders.

## AppRole reference decisions (feature `app-roles`)

Schema `database/auth.sql`: `AppRole(pkid IDENTITY, RoleId PK, RoleName, PermissionLevel, Description?)`,
N-N to `AppUser` via `AppUserRole`.

- **Route key = `RoleId`** (the string PK); `pkid` is display-only (主代碼).
- `Description` is **optional** (DB is `NULL`) even though the mockup shows an asterisk — the DB schema
  wins over the UI sample for required/optional.
- N-N users drive the 使用者數 count column (subquery), the users `p-multiselect` (label
  `UserName (UserId)`), and the view-page chips. Options come from `GET /api/lookups/app-users`.
