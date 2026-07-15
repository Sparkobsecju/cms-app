# Build Conventions

Durable code-gen rules for adding/changing a feature. Read this before touching
backend or frontend feature code. Canonical source: `spec/code-gen.convention.md`;
one-time rationale + reference-feature decisions live in [setup-notes.md](setup-notes.md).

## Backend

- Dapper only (no EF), async, thread `CancellationToken`; one repo per aggregate via `IDbConnectionFactory`.
- Models `{Table}.cs`/`{Table}Request.cs`/`{Table}Query.cs`; lookups in `Models/Lookups/`.
- Controller `/api/{plural}`: `GET /`, `POST /query`, `GET /{id}`, `POST /`, `PUT /` (key in body), `DELETE /{id}`; lookups `GET /api/lookups/{plural}`.
- String PK: `{id}` route (no `:int`), immutable on update. N-N: delete-then-reinsert in one transaction (multiple N-N → one txn syncs all, see `CourseRepository`). `nchar`→`RTRIM()`, `date`/`time`→`DateOnly`/`TimeOnly`.
- FK display: resolve via **flat JOIN aliases** (`p.Name AS PartnerName`) into flat read-only model props — not Dapper multi-map/nav objects. Nullable FK → `LEFT JOIN`. N-N pkid lists read via `QueryMultiple` in `GetByIdAsync`.
- PK style: **IDENTITY** → `SCOPE_IDENTITY()` on insert, no create-form key, no conflict check; **caller-supplied** (string/tinyint) → check `Exists`, `409` on conflict, key immutable on update. No RowAudit in this codebase.
- **Server-managed / secret columns** (e.g. `AppUser.PasswordHash`): exclude from the response model, `{Table}Request`, and every Angular model — never SELECT them into a client-facing projection. Set them server-side in the repo (`AppUser` create reads `SysConfig['appConfig'].defaultPassword`, SHA-256 → hex); leave them untouched on `UPDATE`. Mutate them only through a dedicated action endpoint that carries **no** value in the body.
- **Action endpoints** beyond the standard six: `POST /api/{plural}/{id}/{action}` (e.g. `reset-password`) return `204`/`404`; repo method returns `bool` (found → acted). See `AppUsersController` + `spec/auth/AppUser.md`.
- Tests mock `I{Table}Repository` with Moq.

## Frontend

- Standalone + signals + `inject()`, PrimeNG. API URL in `environment*.ts` (no proxy); aliases `@env`/`@core`/`@features`.
- `core/models`, `core/services` (per aggregate + `lookup.service.ts`); `features/{plural}/{table}-list|-detail|-form/`.
- Service `get(id)`/`delete(id)` must `encodeURIComponent` for string PKs (numeric IDENTITY PKs need none). List: sortable `p-table` + `p-drawer` filter, session keys `{table}-list-filters|-sort|-page`. Filter drawer FK dropdowns use `p-select appendTo="body"`; date columns use `p-datepicker` range pairs (ISO↔Date via local parts). Form: Reactive Forms, `forkJoin` lookups, string PK disabled in edit, FK `p-select`, N-N `p-multiselect`.
- Specs: `HttpTestingController` (services), spy-object stubs (components).

## Reference features

Mirror the closest existing one:
- **AppRole** (`features/app-roles`) — string PK + N-N.
- **PublishStatus** (`features/publish-statuses`) — caller-supplied tinyint PK.
- **CourseGroup** (`features/course-groups`) — IDENTITY PK + provides a lookup.
- **Course** (`features/courses`) — IDENTITY PK + FK-JOIN display + two N-N + date fields; the most complete example.
- **AppUser** (`features/app-users`) — string PK + N-N + a server-managed secret field (`PasswordHash`, never in any DTO) + an action endpoint (`POST /{id}/reset-password`). Spec: `spec/auth/AppUser.md`.

## New feature build order

Spec from `spec/feature-spec.template.md`, then:
1. Backend: models → repo → controller → tests.
2. Frontend: model → service → components → sidebar → specs.
3. Verify both (`dotnet test`, `ng test`).

Mirror the closest existing reference feature (see **Reference features** above).
