# CLAUDE.md

Full-stack CMS from `database/*.sql` per `spec/code-gen.convention.md`. Mirror the
closest existing reference feature: **AppRole** (`features/app-roles`, string PK + N-N),
**PublishStatus** (`features/publish-statuses`, caller-supplied tinyint PK), or
**CourseGroup** (`features/course-groups`, IDENTITY PK + provides a lookup).

Stack: `CMS.API` (.NET 9, Dapper, port 5000), `CMS.API.Tests` (xUnit+Moq), `CMS.NG`
(Angular 20 + PrimeNG, port 4200); solution `src/CMS.slnx`.

## Commands
- `dotnet build|test CMS.slnx`; `dotnet run --project CMS.API` (Swagger `/swagger`).
- In `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
- Start API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.

## Backend conventions
- Dapper only (no EF), async, thread `CancellationToken`; one repo per aggregate via `IDbConnectionFactory`.
- Models `{Table}.cs`/`{Table}Request.cs`/`{Table}Query.cs`; lookups in `Models/Lookups/`.
- Controller `/api/{plural}`: `GET /`, `POST /query`, `GET /{id}`, `POST /`, `PUT /` (key in body), `DELETE /{id}`; lookups `GET /api/lookups/{plural}`.
- String PK: `{id}` route (no `:int`), immutable on update. N-N: delete-then-reinsert in one transaction. `nchar`→`RTRIM()`, `date`/`time`→`DateOnly`/`TimeOnly`.
- PK style: **IDENTITY** → `SCOPE_IDENTITY()` on insert, no create-form key, no conflict check; **caller-supplied** (string/tinyint) → check `Exists`, `409` on conflict, key immutable on update. No RowAudit in this codebase.
- Tests mock `I{Table}Repository` with Moq.

## Frontend conventions
- Standalone + signals + `inject()`, PrimeNG. API URL in `environment*.ts` (no proxy); aliases `@env`/`@core`/`@features`.
- `core/models`, `core/services` (per aggregate + `lookup.service.ts`); `features/{plural}/{table}-list|-detail|-form/`.
- Service `get(id)`/`delete(id)` must `encodeURIComponent`. List: sortable `p-table` + `p-drawer` filter, session keys `{table}-list-filters|-sort|-page`. Form: Reactive Forms, `forkJoin` lookups, string PK disabled in edit, N-N `p-multiselect`.
- Specs: `HttpTestingController` (services), spy-object stubs (components).

## New feature
Spec from `spec/feature-spec.template.md`; backend models→repo→controller→tests, frontend model→service→components→sidebar→specs; verify both.

Details: [docs/setup-notes.md](docs/setup-notes.md).

## gstack
- For **all web browsing**, use the `/browse` skill from gstack. **Never** use the `mcp__claude-in-chrome__*` tools.
- Available gstack skills: `/office-hours`, `/plan-ceo-review`, `/plan-eng-review`, `/plan-design-review`, `/design-consultation`, `/design-shotgun`, `/design-html`, `/review`, `/ship`, `/land-and-deploy`, `/canary`, `/benchmark`, `/browse`, `/connect-chrome`, `/qa`, `/qa-only`, `/design-review`, `/setup-browser-cookies`, `/setup-deploy`, `/setup-gbrain`, `/retro`, `/investigate`, `/document-release`, `/document-generate`, `/codex`, `/cso`, `/autoplan`, `/plan-devex-review`, `/devex-review`, `/careful`, `/freeze`, `/guard`, `/unfreeze`, `/gstack-upgrade`, `/learn`.
