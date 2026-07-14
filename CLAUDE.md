# CLAUDE.md

Guidance for working in this repository. A full-stack CMS generated from an existing SQL
Server schema, following `spec/code-gen.convention.md`. First implemented feature:
**CRUD AppRole** — use it as the reference pattern for every subsequent table.

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

## Commands

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

## Backend conventions (CMS.API)

- **Dapper only, no EF.** Async throughout; pass `CancellationToken` into `CommandDefinition`.
- **Data access:** `IDbConnectionFactory` opens `SqlConnection` from connection string `CMS`
  (`appsettings.json`). One repository per aggregate under `Repositories/`.
- **Models** (`Models/`): `{Table}.cs` (response, FK aliases + subquery counts + N-N id lists),
  `{Table}Request.cs` (write DTO), `{Table}Query.cs` (filter DTO). Lookups under `Models/Lookups/`.
- **Controllers** (`Controllers/`): route `/api/{tablePlural}`. Endpoint shape:
  `GET /` · `POST /query` · `GET /{id}` · `POST /` · `PUT /` (pkid/key in body, no route param) ·
  `DELETE /{id}`. Lookups: `GET /api/lookups/{plural}`.
- **String PK** (e.g. `AppRole.RoleId`): route `{id}` with **no** `:int` constraint; the key is
  immutable on update and comes from the body.
- **N-N:** delete-then-reinsert the junction rows inside the same transaction on create/update;
  read the id list with a second query (`QueryMultiple`) in `GetByIdAsync`.
- **`nchar(n)`** columns: `RTRIM()` in every SELECT. **`date`/`time`** → `DateOnly`/`TimeOnly` via
  the Dapper type handlers registered in `Program.cs` (`Data/DapperTypeHandlers.cs`).
- **CORS:** any localhost origin is allowed (`Program.cs`). Swagger UI at `/swagger`.
- **Tests:** mock `I{Table}Repository` with Moq and assert controller results (Ok/Created/Conflict/
  NotFound/BadRequest). No live DB — repository SQL is only exercised against the real database.

## Frontend conventions (CMS.NG)

- **Standalone components**, signals, `inject()`. PrimeNG (Aura theme) for all UI controls.
- **Config:** API base URL in `src/environments/environment*.ts` (no proxy). `ng serve` swaps in
  `environment.development.ts` via `angular.json` fileReplacements. Path aliases in `tsconfig.json`:
  `@env/*`, `@core/*`, `@features/*`.
- **Structure:** `core/models`, `core/services` (one service per aggregate + `lookup.service.ts`);
  features under `features/{table-plural}/{table}-list|-detail|-form/`.
- **Service:** `list`, `query`, `get(id)` (**`encodeURIComponent`** the id), `create`, `update`,
  `delete(id)` against `@env` `apiBaseUrl`.
- **List page:** sortable/paginated `p-table`; filter `p-drawer` (`p-select` uses `appendTo="body"`,
  `[filter]` for 10+ options, virtual scroll for 100+); confirm-delete; session-storage keys
  `{table}-list-filters` / `-sort` / `-page`.
- **Form page:** Reactive Forms; `forkJoin` for parallel lookups on init; immutable string PK is
  `disable()`d in edit mode and read via `getRawValue()`; N-N via `p-multiselect`
  (`[maxSelectedLabels]="9999"`). `p-datepicker` for `date`/`time` (ISO ↔ `Date`).
- **Sidebar nav** (`app.ts`/`app.html`/`app.scss`): styled after the PrimeNG **Ultima** admin
  template (light rounded sidebar, uppercase section caption, rounded menu rows, rotating submenu
  chevron, primary-tinted active "pill"). Colors are driven by PrimeNG `--p-*` design tokens so the
  sidebar tracks the active theme (light/dark). Add new entries under the appropriate group; only
  系統管理 Admin → 角色 AppRole is currently wired, other groups are visual placeholders.
- **Tests:** Karma + Jasmine. Service specs use `HttpTestingController`; component specs stub the
  data services with `jasmine.createSpyObj` returning `of(...)`, and provide `provideRouter([])`,
  `provideNoopAnimations()`, `ConfirmationService`, `MessageService`.

## AppRole reference (feature `app-roles`)

Schema `database/auth.sql`: `AppRole(pkid IDENTITY, RoleId PK, RoleName, PermissionLevel, Description?)`,
N-N to `AppUser` via `AppUserRole`.

- **Route key = `RoleId`** (the string PK); `pkid` is display-only (主代碼).
- `Description` is **optional** (DB is `NULL`), even though the mockup shows an asterisk — the DB
  schema wins over the UI sample for required/optional.
- N-N users drive the 使用者數 count column (subquery), the users `p-multiselect` (label
  `UserName (UserId)`), and the chips on the view page. Options come from `GET /api/lookups/app-users`.

## Adding a new feature

1. Read the table's `database/*.sql` and write a spec from `spec/feature-spec.template.md`
   (see `spec/sample1.spec.md` / `sample2.spec.md` for worked examples).
2. Backend: models → repository (+ lookups if it's an FK target) → controller → Moq tests.
3. Frontend: model → service → list/detail/form components → sidebar entry → specs.
4. Verify: `dotnet test CMS.slnx` and `npx ng test --watch=false --browsers=ChromeHeadless`.
