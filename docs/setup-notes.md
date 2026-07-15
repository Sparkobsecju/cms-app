# Setup Notes & Design Decisions

One-time context and rationale for the CMS full-stack app. Durable working conventions
live in the root `CLAUDE.md`; this file holds the background it links to.

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
