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
- PK style: **IDENTITY** → `SCOPE_IDENTITY()` on insert, no create-form key, no conflict check; **caller-supplied** (string/tinyint) → check `Exists`, `409` on conflict, key immutable on update. Every write is row-audited (see **Row audit** below).
- **Server-managed / secret columns** (e.g. `AppUser.PasswordHash`): exclude from the response model, `{Table}Request`, and every Angular model — never SELECT them into a client-facing projection. Set them server-side in the repo (`AppUser` create reads `SysConfig['appConfig'].defaultPassword`, SHA-256 → hex); leave them untouched on `UPDATE`. Mutate them only through a dedicated action endpoint that carries **no** value in the body.
- **Action endpoints** beyond the standard six: `POST /api/{plural}/{id}/{action}` (e.g. `reset-password`) return `204`/`404`; repo method returns `bool` (found → acted). See `AppUsersController` + `spec/auth/AppUser.md`.
- **Row audit (cross-cutting)**: every repo writes one `RowAudit` row per successful Insert/Update/Delete via injected `IRowAuditWriter` (`Services/RowAuditWriter.cs`; generic — reflects over any entity), on the **same connection + transaction** as the change so a rollback/failed change leaves no audit row. Pattern: **Insert** → after pkid is known, re-read the new row, `LogInsert`; **Update** → load *before*, mutate, re-read *after*, `LogUpdate` (logs the changed property names; writes nothing when unchanged); **Delete** → load the row, delete, `LogDelete`. Non-transactional repos wrap op+audit in a `BeginTransaction`; add a private `GetByIdAsync(conn, tx, …)` overload for the before/after snapshots (public one delegates to it). Columns: `TableName` = real DB table name; `UserName` from the request JWT `UserName`/name claim via `IHttpContextAccessor`, else `"system"`; `PrimaryKeyValues` = entity `pkid`; `ActionDesc` = first string property for Insert/Delete, changed-property-name list for Update, truncated to 1000. **Action endpoints** (e.g. slot-move, reset-password) are **not** audited. Tests: reflection is unit-tested (`RowAuditWriterTests`); one repo is proven end-to-end against in-memory SQLite (`PublishStatusRepositoryAuditTests` — Insert/Update/Delete rows + no-audit-on-failure). Register `AddHttpContextAccessor()` + `IRowAuditWriter` in `Program.cs`.
- Tests mock `I{Table}Repository` with Moq.

## Frontend

- Standalone + signals + `inject()`, PrimeNG. API URL in `environment*.ts` (no proxy); aliases `@env`/`@core`/`@features`.
- `core/models`, `core/services` (per aggregate + `lookup.service.ts`); `features/{plural}/{table}-list|-detail|-form/`.
- Service `get(id)`/`delete(id)` must `encodeURIComponent` for string PKs (numeric IDENTITY PKs need none). List: sortable `p-table` + `p-drawer` filter, session keys `{table}-list-filters|-sort|-page`. Filter drawer FK dropdowns use `p-select appendTo="body"`; date columns use `p-datepicker` range pairs (ISO↔Date via local parts). Form: Reactive Forms, `forkJoin` lookups, string PK disabled in edit, FK `p-select`, N-N `p-multiselect`. On long forms, pin the `.page-header` action toolbar (Save/Cancel) with `position: sticky; top: 0; z-index` in the component `.scss` (scoped — leave the global `.page-header` alone) so it stays reachable while the form body scrolls under it; the scroll container is `main.layout-content`. Reference: Course form (`course-form` → `.sticky-toolbar`).
- Specs: `HttpTestingController` (services), spy-object stubs (components).
- **Client-side generation via a third-party lib** (e.g. QR codes): wrap the lib in an injectable `core/services/{x}.service.ts` seam that returns a data URL — components stay testable with a plain spy, never touching the lib. Drive (re)generation with an `effect` on a `computed` source signal; **download** by clicking a transient `<a>` whose `href` is the data URL and `download` is the filename. Register CommonJS libs in `angular.json > allowedCommonJsDependencies` to keep the build warning-free. Reference: Course detail QR (`QrService` + `qrcode`).

## Reference features

Mirror the closest existing one:
- **AppRole** (`features/app-roles`) — string PK + N-N.
- **PublishStatus** (`features/publish-statuses`) — caller-supplied tinyint PK.
- **CourseGroup** (`features/course-groups`) — IDENTITY PK + provides a lookup (single meaningful column).
- **Partner** (`features/partners`) — IDENTITY PK + provides a lookup, but with several required string columns + a nullable one + `DisplayOrder` sort; the reference for a plain multi-field master table (no FKs, no N-N).
- **Course** (`features/courses`) — IDENTITY PK + FK-JOIN display + two N-N + date fields; the most complete example. The **detail** page also renders a **QR code** inside the 課程資料 card: encodes `https://www.uuu.com.tw/Course/Show/{pkid}/{CourseId}`, titled by `CourseId`, downloadable as `{CourseId}.png` — the reference for the client-side-generation pattern above (`QrService`).
- **AppUser** (`features/app-users`) — string PK + N-N + a server-managed secret field (`PasswordHash`, never in any DTO) + an action endpoint (`POST /{id}/reset-password`). Spec: `spec/auth/AppUser.md`.
- **FeaturedPromoItem** (`features/featured-promo-items`) — IDENTITY PK + two FK-JOIN displays (`PromoCode` from Promotion2, `TrainingCenterName`) + a **bespoke non-tabular list** instead of the standard `p-table`: a training-center tab bar, a Mon–Sun week navigator, and a day×slot grid with **inline** add/edit (no separate detail/form routes), Copy/Paste, and a slot-move **action endpoint** (`POST /{id}/move/{up|down}` swaps neighbouring slots in one transaction, parking at slot 0 to dodge the `UNIQUE(ScheduleOn, TrainingCenter_pkid, Slot)` index). The form resolves an entered PromoCode → `Promotion_pkid` client-side via `GET /api/lookups/promotions`. The reference for a **custom-UI feature** (inline editing, action endpoint, lookup-driven FK). Spec: `spec/custom/FeaturedPromoItem/`.

## New feature build order

Spec from `spec/feature-spec.template.md`, then:
1. Backend: models → repo → controller → tests.
2. Frontend: model → service → components → sidebar → specs.
3. Verify both (`dotnet test`, `ng test`).

Mirror the closest existing reference feature (see **Reference features** above).
