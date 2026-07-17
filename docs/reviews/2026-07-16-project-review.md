# Project-Wide Code Review — CMS

**Date:** 2026-07-16
**Branch reviewed:** `docs/course-pdf-and-claude-reorg` (audit scoped to the **entire project**, not just this branch's diff)
**Reason for full scope:** the shipped features — Partner/FeaturedPromoItem CRUD, the generic row-audit trail, centralized exception handling, JWT auth, Change Password / My Profile, and Course PDF export — were merged to `develop` without ever being code-reviewed.
**Method:** six parallel specialist reviewers (auth/security, SQL/data layer, Course PDF, controllers/middleware, Angular frontend, test coverage). Every finding is anchored to quoted source. Headline findings were re-verified by hand against the source after the reviewers reported.

> This is a read-only audit. **Nothing in the codebase was modified.** It records findings so they can be triaged, ticketed, and fixed in follow-up work.

---

## 1. Executive summary

The codebase is **fundamentally well-built**. The parts that are most commonly broken are handled correctly: no committed secrets, no SQL injection, disciplined Dapper parameterization, atomic multi-write transactions, a clean exception middleware that never leaks internals, generic login responses (no user enumeration), self-scoped profile/password endpoints (no IDOR), and no DOM-XSS sinks in the Angular app. The controller/HTTP layer and the reflection audit writer are genuinely well tested.

**No P0 (exploitable-right-now) issues were found.**

The real risk sits in three themes:

1. **Authorization is authenticate-only.** The backend requires a valid token on every endpoint but checks **no role**; the frontend only *hides* admin menu items. Net effect: any logged-in low-privilege user can call the admin APIs (create users, reset passwords, delete). This is the single most important finding and was confirmed independently by both the backend and frontend reviewers.
2. **Credential storage is weak.** Passwords are stored as unsalted, single-iteration SHA-256 — a fast hash, trivially brute-forced if the DB ever leaks.
3. **The real SQL data-access layer is almost entirely untested.** 10 of 12 repositories never execute their SQL in a test; controller tests mock the repositories, so query filters, junction sync, rollback, the password-comparison SQL, and the PDF "published-only" gate are all unproven.

Everything else is hardening: no pagination, thin input validation, Swagger exposed in production, no HTTPS enforcement, PDF resource/concurrency hygiene, and last-writer-wins updates.

### Overall posture

| Area | Posture |
|---|---|
| Auth architecture (secrets, IDOR, enumeration, token validation core) | **Strong** |
| Authorization (role enforcement, defense-in-depth) | **Weak — top priority** |
| Credential storage | **Weak** |
| Data layer (injection, atomicity) | **Strong** |
| Data layer (concurrency, scale) | **Needs work** |
| API layer (status codes, error safety) | **Strong** |
| Course PDF (correctness, injection) | **Strong**; resource/concurrency hygiene needs work |
| Frontend (XSS, secrets) | **Strong**; client-side authz weak |
| Test coverage (HTTP/controller layer) | **Strong** |
| Test coverage (SQL/repository layer) | **Weak — large gap** |

---

## 2. Severity tally

| Severity | Count |
|---|---|
| P0 (exploitable now / data loss) | 0 |
| P1 (serious weakness — fix before relying on this in production) | 12 |
| P2 (hardening / hygiene) | 13 |

**Cross-confirmed (found independently by 2+ reviewers — highest confidence):**
- **Missing role-based authorization** — backend (authenticate-only fallback) + frontend (menu-only hiding).
- **Unbounded result sets / no pagination** — data layer + controllers.
- **No HTTPS enforcement** — backend (no `UseHttpsRedirection`) + frontend (`http://localhost` production URL).

---

## 3. Prioritized remediation roadmap

**Fix first (P1, security-blocking):**
1. Add role-based authorization to admin controllers (`AppUsers`, `AppRoles`, `PublishStatuses`, and other management endpoints) — `[Authorize(Roles = "Admin")]` or a policy. Do **not** rely on the authenticate-only fallback. *(Program.cs / AppUsersController.cs)*
2. Add a matching client-side `roleGuard('Admin')` on admin routes; keep the menu filter as UX only. *(app.routes.ts)*
3. Replace SHA-256 password hashing with a salted KDF (ASP.NET Core `PasswordHasher<T>` / Argon2id / bcrypt); migrate existing hashes on next successful login. *(PasswordHasher.cs)*
4. Guard Swagger behind `app.Environment.IsDevelopment()` (or authorization). *(Program.cs)*

**Fix next (P1, correctness/robustness):**
5. Dispose the `PdfDocument`; serialize or pre-warm PDFsharp font state for the public endpoint. *(CoursePdfDocument.cs)*
6. Make the auth guard reject expired tokens (decode `exp`); scope the HTTP interceptor to the API origin. *(auth.guard.ts, auth.interceptor.ts)*
7. Add optimistic concurrency (`rowversion`) to updates; lock the slot-swap. *(CourseRepository.cs, FeaturedPromoItemRepository.cs)*

**Close the test gap (P1):**
8. Add SQLite-backed repository tests mirroring the existing `PublishStatusRepositoryAuditTests` pattern for the 10 untested repositories, prioritizing `AuthRepository` (password SQL) and `CoursePdfRepository` (published-only gate).
9. Add expired-token and wrong-key rejection tests to `AuthorizationTests`.

**Hardening (P2):** pagination, input validation attributes, HTTPS/HSTS, security headers, rate limiting on login, shorter token lifetime + revocation, issuer/audience validation, blob-URL revoke timing, production env URL.

---

## 4. Findings by area

### 4.1 Authentication & Authorization

#### [P1] (confidence 10/10) `Security/PasswordHasher.cs:17` — Passwords hashed with unsalted, single-iteration SHA-256
**Evidence:**
```csharp
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
return Convert.ToHexString(hash).ToLowerInvariant();
```
Same scheme in `AuthRepository.cs` (26/71/85) and `AppUserRepository.cs:242`.
**Problem:** SHA-256 is a fast hash with no salt and no work factor. Identical passwords produce identical hashes (visible across the `AppUser` table), and a DB leak means the whole table is crackable at GPU speed via rainbow tables. Comparison happens in SQL (`WHERE PasswordHash = @PasswordHash`), which a real KDF cannot do.
**Fix:** Use `PasswordHasher<T>` (PBKDF2), Argon2id, or bcrypt with per-hash salt+params. Fetch the stored hash and verify in code. Migrate on next login.

#### [P1] (confidence 8/10) `Program.cs:77-79` / `Controllers/AppUsersController.cs` — No role-based authorization; any authenticated user can manage all users
> **Cross-confirmed** with the frontend finding in §4.5.

**Evidence:**
```csharp
options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser()
    .Build();
```
`[Authorize]` appears only twice in the whole controller set — both on the self-service profile/change-password actions in `AuthController`. No `[Authorize(Roles=…)]` anywhere.
**Problem:** The fallback requires *authentication* but not any *role*. Any logged-in account can `POST/PUT /api/appusers`, assign arbitrary `RoleId`s, `POST /api/appusers/{id}/reset-password`, and `DELETE /api/appusers/{id}`. The role model (`AppRole.PermissionLevel`) shows the system intends differentiated privilege, so this is broken access control / privilege escalation.
**Fix:** Gate administrative controllers with `[Authorize(Roles = "Admin")]` or a dedicated policy.

#### [P2] (confidence 10/10) `Security/ConfigureJwtBearerOptions.cs:32-33` — Issuer and audience validation disabled
**Evidence:** `ValidateIssuer = false, ValidateAudience = false,`
**Problem:** Any token signed with the shared symmetric key is honored regardless of `iss`/`aud`. If the key is reused across environments/services, foreign tokens are accepted. Signature + lifetime *are* validated, which limits blast radius.
**Fix:** Emit and validate `iss`/`aud`; use a per-environment signing key.

#### [P2] (confidence 9/10) `Controllers/AuthController.cs:41-52` — No rate limiting / lockout on login
**Problem:** Unlimited online password guessing against `/api/Auth/login`; no failed-attempt lockout. Compounds the fast-hash weakness.
**Fix:** `AddRateLimiter` partitioned per-IP/UserId on login, and/or a lockout counter on `AppUser`.

#### [P2] (confidence 9/10) `Services/JwtTokenService.cs:16` — 24-hour token, no revocation; password change doesn't invalidate issued tokens
**Evidence:** `public static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);`
**Problem:** Stateless tokens valid for a full day with no revocation. A stolen token is usable for 24h; changing a password after compromise does not invalidate existing tokens.
**Fix:** Shorter access token + refresh token, and/or a per-user security-stamp claim re-validated and bumped on password change/logout.

#### [P2] (confidence 8/10) `Program.cs:82-99` — No HTTPS redirection or HSTS
> **Cross-confirmed** with the frontend production-URL finding in §4.5.

**Problem:** No `app.UseHttpsRedirection()` / `app.UseHsts()` in the pipeline. Without an enforcing reverse proxy, bearer tokens and plaintext passwords can traverse cleartext HTTP.
**Fix:** Add `UseHttpsRedirection()` + `UseHsts()` (outside Development); enforce TLS in deployment.

#### [P2] (confidence 6/10) `Services/JwtTokenService.cs:42` — HMAC signing-key length not validated
**Evidence:** `var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));`
**Problem:** `SigningKeyProvider` rejects only empty, not short, secrets. HMAC-SHA256 needs ≥256-bit keys; a short/low-entropy configured key yields brute-forceable signatures.
**Fix:** Enforce a minimum key length (≥32 bytes); provision a 256-bit random key per environment.

**Cleared (no action):** no committed secrets (signing key + default password live at runtime in the `SysConfig` DB table, not in source); no IDOR on self-service endpoints (`UserId` taken from the validated JWT, never the body); current-password verification is present and ordered first; login is generic (no user enumeration); `PasswordHash` is never projected to clients; CORS is loopback-only with no `AllowCredentials`; token signature + lifetime validation with tightened 1-minute clock skew are correct.

---

### 4.2 SQL & Data Layer (Dapper / SQL Server)

**Headline: no SQL injection.** Every interpolated SQL fragment uses only compile-time `const` strings (`SelectColumns`, `FromJoins`, `OrderBy`); all user values flow through named parameters; `ORDER BY` is hardcoded everywhere; the reflection audit writer parameterizes cleanly.

#### [P1] (confidence 7/10) `Repositories/CourseRepository.cs:167,199` — Read-modify-write updates have no optimistic concurrency (last-writer-wins)
**Evidence:**
```csharp
var before = await GetByIdAsync(connection, transaction, request.Pkid, cancellationToken);
...
WHERE pkid = @Pkid;
```
Same pattern in `AppUserRepository` (141-145), `AppRoleRepository` (133-138), `PartnerRepository` (116-124), `FeaturedPromoItemRepository` (120-128).
**Problem:** Update reads the current row then writes keyed only on PK — no `rowversion` check. Two admins editing the same record concurrently: the second commit silently overwrites the first, and the audit "before" snapshot may not reflect the first's committed change.
**Fix:** Add a `rowversion` column, select it in `before`, add `AND RowVer = @ExpectedRowVer` to the UPDATE, and return 409 when `affected == 0` (distinguish from not-found).

#### [P1] (confidence 5/10) `Repositories/FeaturedPromoItemRepository.cs:165-201` — `MoveSlotAsync` read-then-swap races under READ COMMITTED
**Problem:** The `SELECT Slot` doesn't hold its shared lock to the UPDATE. Two concurrent moves in the same (ScheduleOn, TrainingCenter) group can interleave; the middle UPDATE may match zero rows, parking the moving row at `Slot = 0` or garbling order. The `UNIQUE` index prevents duplicates but not a lost/garbled move.
**Fix:** Raise isolation (`SERIALIZABLE`) or take `UPDLOCK, HOLDLOCK` on the initial SELECT so the group is locked for the swap.

#### [P2] (confidence 8/10) `Repositories/CourseRepository.cs:62` (all GetAll/Query methods) — Unbounded result sets, no paging
> **Cross-confirmed** with the controller finding in §4.4.

**Evidence:** `var sql = $"SELECT {SelectColumns} {FromJoins} ORDER BY c.DisplayOrder ASC;";`
**Problem:** `GetAll`/`Query` return every matching row (no `OFFSET/FETCH`/`TOP`), materialized into memory. Memory/latency scale with table size, no ceiling.
**Fix:** Server-side paging (`OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY`) with a total-count query, or at minimum a `TOP (@Max)` cap.

#### [P2] (confidence 7/10) `Repositories/CourseRepository.cs:74` (every keyword filter) — Non-sargable leading-wildcard LIKE + unescaped LIKE metacharacters
**Evidence:** `OR c.Title LIKE '%' + @Keyword + '%'`
**Problem:** Not injection (value is parameterized). But the leading `%` forces an index scan across up to five OR'd columns, and `%`/`_`/`[` in the keyword act as wildcards (a user searching `%` matches everything — a correctness/DoS annoyance).
**Fix:** Escape wildcards before binding (`[[]`, `[%]`, `[_]` + `ESCAPE`); consider trailing-wildcard or full-text for scale.

#### [P2] (confidence 6/10) `Data/SqlConnectionFactory.cs:19-21` — Connection not disposed if `OpenAsync` throws
**Evidence:**
```csharp
var connection = new SqlConnection(_connectionString);
await connection.OpenAsync(cancellationToken);
return connection;
```
**Problem:** If `OpenAsync` throws (transient fault, cancellation), the connection is neither disposed nor returned to the pool. Under load with transient faults this can slowly exhaust the pool.
**Fix:** `try { await connection.OpenAsync(ct); } catch { await connection.DisposeAsync(); throw; }`.

#### [P2] (confidence 6/10) `Services/RowAuditReflection.cs:20-23` — Per-operation reflection with no caching
**Problem:** Every insert/update/delete re-runs `GetProperties` + LINQ sort and reflective `GetValue` on every property of `before` and `after` (~60 calls for a wide entity). Negligible at admin-CRUD volume, wasteful if audit ever moves onto a hot path.
**Fix:** Cache the ordered `PropertyInfo[]` per `Type` in a `ConcurrentDictionary`; optionally compile accessors. Not urgent.

**Cleared (no action):** RowAuditWriter is injection-safe (changed column names go into the `@ActionDesc` *value*, `TableName` is a per-repo `const`); all multi-write mutations share one transaction with explicit rollback on no-op paths (audit + business write are atomic); connections/transactions are consistently `using`-scoped; `GetByIdAsync` batches row + junction with `QueryMultipleAsync` (no N+1).

---

### 4.3 Course PDF Export (PDFsharp-MigraDoc 6.1.1)

#### [P1] (confidence 6/10) `Pdf/CoursePdfDocument.cs:32-37` — `PdfDocument` / renderer never disposed (per-request leak)
**Evidence:**
```csharp
var renderer = new PdfDocumentRenderer { Document = document };
renderer.RenderDocument();
using var stream = new MemoryStream();
renderer.PdfDocument.Save(stream, closeStream: false);
return stream.ToArray();
```
**Problem:** `renderer.PdfDocument` is `IDisposable` and owns internal streams/state; it's never disposed. The `MemoryStream` is scoped, but the heavier PDF graph leaks until GC. Under sustained download traffic this raises GC pressure and handle retention.
**Fix:** `using var pdf = renderer.PdfDocument; pdf.Save(stream, closeStream: false);`.

#### [P1] (confidence 5/10) `Pdf/CoursePdfDocument.cs:16-24, 32-33` — Concurrent rendering shares process-global PDFsharp font/render state
**Problem:** `Render` is `static` and reachable concurrently on the anonymous public endpoint. PDFsharp 6.x keeps process-global font state (`GlobalFontSettings`, glyph caches) populated lazily during `RenderDocument`; parallel first-renders can race on cache population and throw intermittently (DoS-adjacent on a public endpoint).
**Fix:** Serialize rendering behind a `SemaphoreSlim`, or pre-warm the font cache once at startup. Load-test concurrent `/pdf` calls.

#### [P2] (confidence 7/10) `Controllers/CoursePdfController.cs:42` — Download filename uses raw route value, not the canonical DB `CourseId`
**Evidence:** `return File(bytes, "application/pdf", $"{courseId}.pdf");`
**Problem:** `courseId` is the un-canonicalized route string. A case-insensitive match serves course `NINS` but names the file `nins.pdf`, deviating from the spec's `filename={CourseId}.pdf`. Header-injection is **not** exploitable (`File(...)` percent-encodes CRLF/quotes), but it's poor hygiene and the wrong filename.
**Fix:** `File(bytes, "application/pdf", $"{course.CourseId}.pdf");`.

#### [P2] (confidence 4/10) `Pdf/CoursePdfDocument.cs:113-124` / `Pdf/RichText.cs:40-58` — Unbounded rich-text fields copied several times → memory spikes
**Problem:** Each field passes through several full-string regex passes plus `AddCjkBreaks` (~2× alloc), doubling again for CJK zero-width-space insertion, then the whole PDF is buffered in memory. Fields are admin-curated (low abuse likelihood) but uncapped.
**Fix:** Cap field length before rendering; consider streaming the response instead of `ToArray()`.

#### [P2] (confidence 3/10) `Pdf/NotoFontResolver.cs:16-27` — Variable font with simulated bold/italic
**Problem:** `NotoSansTC-VF.ttf` is a variable font; PDFsharp 6.1 has limited variable-font handling and synthesizes bold via simulation, which can bloat embedded font data or render slightly off weight. It does render (spec shows a valid ~192 KB `%PDF-1.7`), so this is an output-quality note, not a crash.
**Fix:** Prefer static `-Regular.ttf` / `-Bold.ttf` instances, or confirm subsetting is acceptable for the largest courses.

**Cleared (no action):** null course → 404; anonymous access is intentional and spec-matched (published-only gate); rich text is HTML-stripped to plain text and added as literal (no MigraDoc injection); strip regexes are linear (no ReDoS); font loaded once into a static `byte[]` (no per-request IO); missing font file guarded with a clear exception; PDFsharp-MigraDoc is MIT (no QuestPDF license-mode gate). Implementation otherwise matches `spec/course/CoursePdf.md`.

---

### 4.4 Controllers, Exception Handling & App Wiring

#### [P1] (confidence 7/10) `Program.cs:88-92` — Swagger UI + JSON exposed unconditionally in every environment, unauthenticated
**Evidence:**
```csharp
app.UseSwagger();
app.UseSwaggerUI(options => { options.SwaggerEndpoint("/swagger/v1/swagger.json", "CMS API v1"); });
```
**Problem:** No `IsDevelopment()` guard, and it sits *before* `UseAuthentication`/`UseAuthorization` as terminal middleware, so the fallback policy never covers it. In production any anonymous caller can pull the full API surface (routes, verbs, DTO shapes) — reconnaissance for the mutating endpoints.
**Fix:** Wrap both calls in `if (app.Environment.IsDevelopment())`, or place Swagger behind authorization.

#### [P2] (confidence 8/10) `Controllers/CoursesController.cs:21-27` (pattern-wide) — List endpoints return unbounded result sets
> **Cross-confirmed** with the data-layer finding in §4.2. See fix there.

#### [P2] (confidence 7/10) `Models/CourseRequest.cs:10-49` — Request DTOs carry no length/range validation; oversized input surfaces as 500
**Evidence:** `public string Title { get; set; } = string.Empty;` … `public decimal ListPrice { get; set; }` — no `[StringLength]`/`[Range]`/`[Required]`.
**Problem:** A `Title` longer than the DB column, or a negative `ListPrice`/`Hour`, passes controller validation, reaches Dapper, and the DB throws → generic **500** instead of **400**. Same across Partner/FeaturedPromoItem/CourseGroup/PublishStatus request models.
**Fix:** Add DataAnnotations matching the SQL column definitions so bad input is rejected as 400 at the boundary.

#### [P2] (confidence 6/10) `Controllers/CoursesController.cs:62` (all Create/Update) — DB constraint violations map to 500 rather than 409/400
**Problem:** Unique-index or FK violations bubble up as generic 500. Inconsistent: AppRoles/AppUsers/PublishStatuses pre-check with `ExistsAsync` and return 409; Courses/Partners/FeaturedPromoItems/CourseGroups do not.
**Fix:** Catch `SqlException` for 2601/2627 (unique) and 547 (FK) → 409/400, or pre-validate before insert.

#### [P2] (confidence 5/10) `Program.cs:33-43` — CORS policy is dev-shaped but applied in all environments
**Problem:** `AllowAnyHeader().AllowAnyMethod()` with a loopback-origin predicate ships to production unchanged; no explicit production origin allow-list, and no security response headers (HSTS, X-Content-Type-Options) anywhere. Risk is limited today (loopback-only, no credentials) but it's a config smell.
**Fix:** Gate the loopback policy to Development; define an explicit prod origin allow-list; add standard security headers.

#### [P2] (confidence 4/10) `Controllers/CoursePdfController.cs:15-17` — Anonymous controller shares the `api/courses` route prefix
**Problem:** `[AllowAnonymous]` is class-level. Today it wraps only the deliberately-public, guarded PDF GET, but any future action added to this class silently inherits anonymous access, and it shares the exact `api/courses` prefix with the auth-required `CoursesController`.
**Fix:** Put `[AllowAnonymous]` on the single action, and/or move the endpoint to a distinct route (`api/public/courses/...`).

**Cleared (no action):** exception middleware is wired first, logs full detail server-side, returns only a generic `ErrorResponse` (no stack/SQL/connection leak), respects `Response.HasStarted`, and preserves CORS headers on 500s; the global fallback requires auth on every endpoint (no mutation is accidentally anonymous); RowAudit `tableName`/`pkid` fully parameterized; `AppUserRequest` omits `PasswordHash` (no over-posting); all actions are correctly `async`/`await`ed with `CancellationToken` propagation (no sync-over-async / async-void).

---

### 4.5 Angular Frontend (Angular 20 + PrimeNG)

#### [P1] (confidence 8/10) `app/app.routes.ts:13` — Admin pages have no role guard; only the menu is hidden
> **Cross-confirmed** with the backend authorization finding in §4.1. Together these are the top-priority issue.

**Evidence:** admin routes (`app-users`, `app-roles/:id`, `publish-statuses`) sit under `canActivateChild: [authGuard]` with no role check; admin authorization exists only as a menu filter in `app.ts:76-79` (`hasRole('Admin')`).
**Problem:** `authGuard` checks only that a session exists. A logged-in non-admin who manually navigates to `/app-users` reaches the admin UI — the `adminOnly` filter only hides sidebar links (security through obscurity). Real protection then depends entirely on the backend, which (per §4.1) is authenticate-only.
**Fix:** Add a `roleGuard('Admin')` (`CanActivateChildFn` calling `auth.hasRole('Admin')`); wrap admin routes in a child group applying it. Keep the menu filter as UX only.

#### [P1] (confidence 7/10) `app/core/guards/auth.guard.ts:12` — Guard checks token presence, not expiry/validity
**Evidence:** `return auth.isAuthenticated() ? true : router.createUrlTree(['/login']);` where `isAuthenticated()` is `this._profile() !== null`.
**Problem:** Any stored profile passes the guard regardless of the JWT's `exp`. An expired/tampered token still admits the user; they only get bounced later on a 401. `decodeJwtPayload` exists but never reads `exp`.
**Fix:** Decode `exp` and treat an expired token as unauthenticated (and clear the stored session in `readSession`).

#### [P1] (confidence 6/10) `app/core/interceptors/auth.interceptor.ts:14` — Bearer token attached to every request, including cross-origin
**Evidence:** `return next(req.clone({ setHeaders: { Authorization: \`Bearer ${token}\` } }));` — no allow-list check against `environment.apiBaseUrl`.
**Problem:** Today all requests target the API origin, so nothing leaks, but the guard is absent: the day any request goes to a third-party host, the session token is sent in its `Authorization` header. A latent token-exfiltration path.
**Fix:** Only attach the header when the request URL is same-origin with `environment.apiBaseUrl`.

#### [P1] (confidence 6/10) `app/core/services/auth.service.ts:94` — JWT stored in sessionStorage (JS-readable), not an httpOnly cookie
**Evidence:** `sessionStorage.setItem(SESSION_KEY, JSON.stringify(profile));` with the full `accessToken` in the persisted object.
**Problem:** The token is readable by any JS on the page — one XSS foothold (or a compromised third-party script) can exfiltrate a valid bearer token. Mitigating: `sessionStorage` clears on tab close, and there are **no** DOM-XSS sinks anywhere (Angular escaping intact), so the practical surface is small. Recorded as an architectural tradeoff.
**Fix:** Prefer an httpOnly + Secure + SameSite cookie issued by the backend; if token-in-storage is retained, keep the strict no-`innerHTML` discipline and add a CSP.

#### [P2] (confidence 8/10) `src/environments/environment.ts:2-4` — Production env points to plaintext `http://localhost`
> **Cross-confirmed** with the backend HTTPS finding in §4.1.

**Evidence:** `production: true, apiBaseUrl: 'http://localhost:5000/api',`
**Problem:** The production build ships a hardcoded plaintext localhost API URL — non-functional for a real deploy, and `http://` means the interceptor-attached bearer token travels unencrypted.
**Fix:** Point production at the real `https://` origin; consider failing the build if a `production` env contains `localhost`/`http://`.

#### [P2] (confidence 6/10) `app/core/utils/download.ts:7-8` — Object URL revoked synchronously after `click()` may abort the download
**Evidence:** `link.click(); URL.revokeObjectURL(url);`
**Problem:** `revokeObjectURL` runs on the same tick as `click()`; the download is async, so some browsers invalidate the URL before the download starts, aborting it. This is the PDF/blob download path for both course list and detail. (No leak — just possibly too early. Filename via the `download` attribute is browser-sanitized, low injection risk.)
**Fix:** Revoke on the next tick (`setTimeout(() => URL.revokeObjectURL(url), 0)`); optionally append the anchor to the DOM before clicking.

#### [P2] (confidence 5/10) `app/core/interceptors/error.interceptor.ts:32` — Raw server `message` from 5xx shown to the user
**Problem:** On a 500-class response the interceptor surfaces the server's `message` verbatim in a toast (`safeMessage`). If the backend ever leaks internal detail in that field, it's shown to the user. Rendered as text (not HTML) so no XSS — info-leak only, dependent on backend discipline. Same pattern in `change-password.ts:127-132`.
**Fix:** For 5xx prefer the generic message; only trust `message` fields the backend explicitly designates user-safe.

**Cleared (no action):** no DOM-XSS sinks anywhere (`innerHTML`/`bypassSecurityTrust`/`DomSanitizer` all absent); no hardcoded secrets; Bearer-in-header (CSRF is a non-issue, no auth cookies); 401 correctly clears the session and redirects.

---

### 4.6 Test Coverage (xUnit + Moq)

The controller/HTTP layer and the reflection audit writer are **genuinely well tested** — behavior-asserting tests (`OkObjectResult`/`NotFoundResult`/`BadRequestObjectResult`), `Times.Never` guard-clause checks, real JWT decode with claim/expiry/no-secret assertions, exact audit `ActionDesc` strings. No trivial not-null-only tests. No skipped/commented tests. The **dominant gap is the real SQL data-access layer.**

#### [P1] (confidence 10/10) 10 of 12 repositories have zero execution-level tests
Only `PublishStatusRepository` and `RowAuditRepository` run real SQL (against SQLite). `Course`, `AppUser`, `Partner`, `AppRole`, `CourseGroup`, `FeaturedPromoItem`, `Lookup`, `CoursePdf`, `Auth` repositories never execute their SQL — controller tests mock `I*Repository`, so dynamic WHERE-builders, N-N junction sync, `SCOPE_IDENTITY` read-back, and rollback-on-missing are all unproven.
**Fix:** SQLite-backed tests mirroring `PublishStatusRepositoryAuditTests`: assert query filters (keyword hit/miss, null-guarded params), identity return + junction dedup, rollback + `false` on missing pkid.

#### [P1] (confidence 9/10) `AuthRepository.cs:20-94` — Password-comparison SQL and the "hash never projected" guarantee untested
Security-critical behavior lives in SQL (password matched in `WHERE`, `IsActive = 1` filter, `PasswordHash` absent from SELECT, `PasswordUpdatedTime = GETDATE()`), tested only through a mocked repo. A regression that projects the hash, drops `IsActive = 1` (deactivated users could log in), or breaks equality would pass every test.
**Fix:** Seed active + inactive users with known hashes; assert correct/wrong/inactive/unknown outcomes and that the returned object never carries a hash.

#### [P1] (confidence 8/10) `ConfigureJwtBearerOptions.cs:33-38` — Expired-token and wrong-key rejection untested
`AuthorizationTests` covers no-token→401, malformed→401, valid→200, but never a wrong-key-signed token (forged signature) nor an expired token (`ValidateLifetime`/`ClockSkew`). If either flag were flipped off, no test would catch the resulting auth bypass.
**Fix:** Add two cases — wrong secret → 401, past-expiry → 401.

#### [P1] (confidence 8/10) `FeaturedPromoItemRepository.cs:160-205` — `MoveSlotAsync` swap logic untested
Controller tests only verify the `MoveResult` enum → HTTP mapping. The `±1` boundary math, `<1 or >3` range check, and the park-at-slot-0 three-statement swap run only in production.
**Fix:** SQLite test seeding slots 1-3; assert up/down swaps, `OutOfRange` at boundaries, `NotFound` for missing pkid.

#### [P1] (confidence 7/10) `SigningKeyProvider.cs` — Key load, cache, and error paths fully untested
`AuthorizationTests` stubs it out. Untested: double-checked-lock cache, `JsonDocument` parse of `SysConfig['appConfig']`, the three `InvalidOperationException` paths, and `GetAwaiter().GetResult()`. `AuthRepository.ReadSigningSecretAsync` duplicates this parse and is equally untested.
**Fix:** Unit-test JSON extraction (valid/missing/empty → specific exceptions) and cache-once behavior.

**P2 test gaps:** `CoursePdfRepository` published-only filter untested (dropping `ps.IsPublished = 1` would leak unpublished course PDFs); `LookupRepository` only pass-through-covered; `PasswordPolicy.IsCompliant` only indirectly tested (CJK-as-symbol edge uncovered); `JwtTokenService` whitespace-role filtering not directly asserted.

#### Coverage table

| Subsystem | Tested? |
|---|---|
| Auth controller (login/profile/change-pwd logic) | yes |
| Auth E2E authorization (401/200, JWT anon) | partial (no expired / wrong-key) |
| JWT issuance (`JwtTokenService`) | partial (whitespace-role filter untested) |
| JWT validation config (`ConfigureJwtBearerOptions`) | partial (lifetime/key rejection untested) |
| `SigningKeyProvider` (load/cache/errors) | **no** |
| Password hashing (`PasswordHasher`) | partial (one input, via controller) |
| Password policy (`PasswordPolicy`) | partial (indirect) |
| `AuthRepository` (password SQL, roles, hash-guard) | **no** |
| Course/AppUser/Partner/AppRole/CourseGroup/FeaturedPromo/Lookup/CoursePdf repos (real SQL) | **no** |
| `PublishStatusRepository` (audit path) | yes |
| `FeaturedPromoItemRepository.MoveSlotAsync` (swap SQL) | **no** (only enum→HTTP) |
| `RowAuditRepository` (read/filter) | yes |
| RowAuditWriter / reflection audit | yes |
| Exception middleware | yes |
| Course PDF generation (`CoursePdfDocument`, `RichText`) | yes |
| CoursePdf controller | yes (repo mocked) |
| All 11 controllers (request→response mapping) | yes |

---

## 5. What's genuinely good

Worth stating plainly, because it's the larger part of the picture:

- **No committed secrets.** Signing key + default password are runtime `SysConfig` values; `database/*.sql` are schema-only.
- **No SQL injection** anywhere, despite hand-built dynamic queries — parameterization discipline is consistent.
- **Atomic writes.** Business row + junction sync + audit row share one transaction with correct rollback.
- **Clean exception middleware** — full detail logged server-side, only a generic `ErrorResponse` to clients, CORS-header-preserving.
- **Auth fundamentals** — generic login (no enumeration), self-scoped profile/password (no IDOR), current-password verification, correct signature+lifetime token validation.
- **No frontend XSS sinks** and no hardcoded secrets; 401 handling clears session and redirects.
- **Meaningful tests** at the controller layer and for the audit writer — behavior assertions, not smoke tests.

---

## 6. Scope & method

- **Reviewed:** `src/CMS.API` (98 `.cs` files), `src/CMS.NG` (91 `.ts` files), `src/CMS.API.Tests` (24 files), `database/*.sql`, and the Course PDF spec.
- **Not modified:** nothing — read-only audit.
- **Reviewers:** six parallel specialist passes (auth/security, SQL/data, Course PDF, controllers/middleware, frontend, tests). Each read source directly and anchored findings to quoted code; headline P1s were re-verified by hand.
- **Confidence scores** are per-finding (9-10 = verified against specific code; 5-6 = pattern match, verify before acting). Findings below the reporting bar were dropped.
- **Not covered:** runtime/integration behavior against a live DB, load testing (flagged as needed for the PDF concurrency finding), and dependency/CVE scanning of NuGet/npm packages — recommended as follow-ups.
