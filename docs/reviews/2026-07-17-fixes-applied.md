# Remediation Log — CMS Project Review Follow-up

**Date:** 2026-07-17
**Branch:** `docs/course-pdf-and-claude-reorg`
**Source review:** [2026-07-16-project-review.md](./2026-07-16-project-review.md)
**Author:** automated fix pass (Claude Code)

> Companion to the read-only audit. This file records **what was actually changed**, so the next
> pass can pick up the remaining findings without re-deriving the list. Update it every time more
> findings are fixed. Section numbers (§) refer to the source review.

## Verification status after this pass
- **Backend:** `dotnet build CMS.slnx` → 0 warnings / 0 errors. `dotnet test` → **168 passed**.
- **Frontend:** `npx ng build` → clean. `npx ng test` → **205 passed** (was 199 passing + 6 broken).
- Every change below was compiled and tested; nothing is speculative.

---

## 🔐 CSO audit pass (2026-07-17) — full-project security review

Ran `/cso` (daily mode, 8/10 confidence gate) over the **entire project** — first CSO-scoped review.
Report saved to `.gstack/security-reports/2026-07-17-033421.json` (gitignored).

**Surface checked clean:** SQL injection (all Dapper-parameterized; interpolated fragments are
compile-time constants), JWT validation (iss/aud/lifetime/≥32-byte key all enforced, symmetric-only),
role-based access control on admin controllers, committed secrets (none — key/password seeded to DB at
setup, git history clean), XSS (Angular auto-escape, no `innerHTML`/trust-bypass sinks), SSRF (PDF path
strips HTML to plain text, no remote fetch), CORS (loopback-only origins). No CI/CD, containers, or
webhooks in the repo.

**One HIGH finding — fixed this pass:**

- **Unsalted SHA-256 password hashing → salted PBKDF2 + lazy migration** (OWASP A02).
  `PasswordHasher.Hash` was single-round unsalted SHA-256 (`SHA256.HashData(...)`), a fast hash with no
  salt — on any DB exposure, all hashes fall to rainbow tables / GPU cracking, and identical passwords
  produced identical hashes. Replaced with **PBKDF2** (HMAC-SHA256, 128-bit random per-user salt, 100k
  iterations), stored self-describing as `PBKDF2$SHA256$<iterations>$<salt>$<hash>`.
  - `PasswordHasher.Verify(password, stored, out needsRehash)` handles both formats; deprecated SHA-256
    hex hashes still verify and set `needsRehash`. Constant-time compares (`CryptographicOperations.FixedTimeEquals`).
  - Password checks moved **out of SQL into code** (salted hashes can't be compared in a `WHERE`):
    `ValidateCredentialsAsync` and `VerifyCurrentPasswordAsync` now fetch the hash (server-side only,
    never projected to the client) and verify in-process.
  - **Migrate-on-next-login:** a successful login against a legacy hash re-hashes the verified plaintext
    under PBKDF2 and persists it — no forced reset. `PasswordUpdatedTime` is left untouched (format
    changed, not the password).
  - Admin create/reset (`AppUserRepository.ReadDefaultPasswordHashAsync`) now uses the shared PBKDF2
    hasher, so two users on the same default password no longer share a stored hash.
  - *Files: `Security/PasswordHasher.cs`, `Repositories/AuthRepository.cs`, `Repositories/AppUserRepository.cs`,
    `Repositories/IAuthRepository.cs` (doc comments), `CMS.API.Tests/AuthControllerTests.cs` (tests updated:
    PBKDF2 round-trip + legacy-verify-flags-rehash).*
  - **Verification:** `dotnet build CMS.slnx` → 0 warnings / 0 errors; `dotnet test` → **184 passed**.

**Below the reporting bar (verify intent, not fixed):** content controllers are auth-gated but not
`Roles="Admin"` (fine if all users are Admin-class); DB connection `Encrypt=False` is safe for local
SQLEXPRESS but must not carry to a remote prod DB; 24h JWT has no revocation. All three already tracked
in the "Remaining" list below.

---

## ✅ Fixed in this pass

### Security-blocking (P1)

1. **Role-based authorization on admin controllers** — §4.1 / §4.5 (top-priority, cross-confirmed).
   Added `[Authorize(Roles = "Admin")]` to `AppUsersController`, `AppRolesController`,
   `PublishStatusesController`. The global fallback still enforces authentication; these now also
   enforce the `Admin` role (seed role id = `Admin`, carried in the JWT role claim).
   *Files: `Controllers/AppUsersController.cs`, `AppRolesController.cs`, `PublishStatusesController.cs`.*

2. **Client-side role guard** — §4.5.
   New `roleGuard(role)` (`core/guards/role.guard.ts`); admin routes (`app-roles*`, `app-users*`,
   `publish-statuses*`) wrapped in a componentless group under `canActivateChild: [roleGuard('Admin')]`.
   The menu filter in `app.ts` remains UX-only. A denied non-admin is redirected to `/courses`
   (NOT `/` — `/` redirects to the admin `app-roles`, which would loop). *Files: `app.routes.ts`,
   `core/guards/role.guard.ts`.*

3. **Auth guard rejects expired tokens** — §4.5.
   `AuthService.isAuthenticated()` now decodes the JWT `exp` and treats an expired (or exp-less,
   fail-closed) token as signed out, clearing the stale session. `readSession()` drops expired
   tokens on load. *File: `core/services/auth.service.ts` (`isTokenExpired`).*

4. **Interceptor no longer leaks the token cross-origin** — §4.5.
   `authInterceptor` attaches the bearer token only when the request origin matches
   `environment.apiBaseUrl`. Third-party requests go through unauthenticated.
   *File: `core/interceptors/auth.interceptor.ts` (`isApiRequest`).*

5. **Swagger dev-only + HTTPS/HSTS in production** — §4.4 / §4.1 / §4.5.
   Swagger UI + JSON now wrapped in `if (app.Environment.IsDevelopment())`. Outside Development,
   `UseHsts()` + `UseHttpsRedirection()` are added. *File: `Program.cs`.*

6. **PdfDocument disposed per request** — §4.3.
   `renderer.PdfDocument` is now `using`-scoped so the PDF object graph is released promptly.
   *File: `Pdf/CoursePdfDocument.cs`.*

7. **Course PDF endpoint hygiene** — §4.3 / §4.4.
   `[AllowAnonymous]` moved from the controller class to the single PDF action (so future actions
   don't silently inherit anonymous access). Download filename now uses the canonical DB
   `course.CourseId`, not the raw route value. *File: `Controllers/CoursePdfController.cs`.*

### Hardening (P2)

8. **Blob download revoke timing** — §4.5.
   `saveBlob` revokes the object URL on the next tick (`setTimeout(…, 0)`) instead of synchronously
   after `click()`, so the download isn't aborted. *File: `core/utils/download.ts`.*

### Tests added / fixed

9. **New backend test:** `AdminEndpoint_Returns403_ForAuthenticatedNonAdmin` proves the role gate
   rejects an authenticated non-admin with 403. *File: `CMS.API.Tests/AuthorizationTests.cs`.*

10. **New frontend specs:** expired-token → unauthenticated (+ session cleared); exp-less token →
    unauthenticated (fail closed); interceptor does NOT attach the token to a cross-origin host.
    *Files: `auth.service.spec.ts`, `auth.interceptor.spec.ts`.*

11. **Fixed a pre-existing broken test** (NOT in the original review — the audit never ran `ng test`):
    `CourseDetail` spec was missing the PrimeNG `MessageService` provider, failing 6 tests with
    `NG0201`. Added the provider. *File: `course-detail.spec.ts`.*
    → **Lesson for next review: actually run `dotnet test` and `ng test`; the audit asserted the
    frontend was "genuinely well tested" while 6 specs were red.**

---

## ✅ Fixed — Pass 2 (2026-07-17, hardening batch)

> Second remediation pass over the **still-open, self-contained** findings — no schema change, no
> load test, no large harness. Backend `dotnet build` → 0/0; `dotnet test` → **183 passed** (was
> 168, +15 new). Frontend `ng test` → **205 passed**, `ng build` → clean.

### Backend

12. **JWT issuer/audience now emitted + validated** — §4.1 (confidence 10/10).
    `JwtTokenService` emits `iss`/`aud` (constants `JwtTokenService.Issuer`/`.Audience` = `CMS.API`);
    `ConfigureJwtBearerOptions` sets `ValidateIssuer`/`ValidateAudience` = true with matching
    `ValidIssuer`/`ValidAudience`. A token signed with the same key but minted for another service
    (wrong `iss`) is now rejected. *Files: `Services/JwtTokenService.cs`, `Security/ConfigureJwtBearerOptions.cs`.*
    New test: `ProtectedEndpoint_Returns401_ForTokenWithWrongIssuer` (`AuthorizationTests.cs`).

13. **Minimum signing-key length enforced (≥32 bytes)** — §4.1 (confidence 6/10).
    `SigningKeyProvider` (new `MinKeyBytes = 32`) throws if the configured key is shorter than a
    256-bit HMAC-SHA256 key — fail-closed rather than sign with a brute-forceable key.
    *File: `Services/SigningKeyProvider.cs`.*

14. **LIKE wildcard escaping on every keyword filter** — §4.2 (confidence 7/10).
    New `SqlLike.EscapeWildcards` escapes `%`/`_`/`[`/`\` before binding; each keyword `LIKE` carries
    `ESCAPE '\'`. A search for `%` now matches the literal character, not every row (correctness /
    index-scan-DoS annoyance). *Files: `Data/SqlLike.cs` (new) + `Course`/`AppRole`/`AppUser`/`CourseGroup`/`Partner`/`PublishStatus` repositories.*
    New test: `SqlLikeTests.cs`.

15. **`SqlConnectionFactory` disposes the connection if `OpenAsync` throws** — §4.2 (confidence 6/10).
    A transient-fault/cancelled open no longer leaks a half-built connection out of the pool.
    *File: `Data/SqlConnectionFactory.cs`.*

16. **`RowAuditReflection` caches the ordered `PropertyInfo[]` per type** — §4.2 (confidence 6/10).
    `ConcurrentDictionary<Type, PropertyInfo[]>` replaces the per-operation `GetProperties` + LINQ
    sort, so audit no longer re-reflects on every insert/update/delete. *File: `Services/RowAuditReflection.cs`.*

17. **Request DTO validation attributes** — §4.4 (confidence 7/10).
    Added `[Required]`/`[MaxLength]`/`[Range]` matching the SQL column definitions to
    `Course`/`FeaturedPromoItem`/`CourseGroup`/`PublishStatus`/`AppRole`/`AppUser` request models
    (matching the pattern `PartnerRequest` already used). Oversized/negative input is now rejected as
    **400** at the `[ApiController]` boundary instead of surfacing as a **500** from the DB.
    *Files: the six `*Request.cs` models in `Models/`.*

### Frontend

18. **Error interceptor no longer leaks the raw 5xx server message** — §4.5 (confidence 5/10).
    A 500-class response now always shows a fixed generic toast; the server `message` (which can carry
    SQL/stack fragments) is only re-thrown, never displayed. *File: `core/interceptors/error.interceptor.ts`;
    spec updated to assert the raw message is not surfaced (`error.interceptor.spec.ts`).*

### Tests added (closes part of the §4.6 gap)

19. **`SigningKeyProvider` now has execution-level tests** — §4.6 (P1, was "no").
    SQLite-backed `SigningKeyProviderTests.cs`: valid-secret load, cache-once, short-key rejection,
    missing-row/missing-property/empty-secret error paths.

---

## ⛔ Remaining — pick these up next (ranked)

### P1 still open (each needs a schema/DB change, a load test, or a large test harness — deliberately deferred)

- ~~**Password hashing → salted KDF** (§4.1)~~ — **done in the CSO pass (2026-07-17), see the
  "🔐 CSO audit pass" section above.** Replaced unsalted SHA-256 with salted PBKDF2 (100k iterations),
  moved verification from SQL into code, added migrate-on-next-login for existing hashes. 184 tests pass.
- **Optimistic concurrency (rowversion)** on updates (§4.2) — needs a `rowversion` column + migration;
  `Course/AppUser/AppRole/Partner/FeaturedPromoItem` repos; return 409 on `affected == 0`.
- **`MoveSlotAsync` race** (§4.2) — `UPDLOCK, HOLDLOCK` on the initial SELECT or `SERIALIZABLE`.
- **PDFsharp concurrent font/render state** (§4.3) — serialize `Render` behind a `SemaphoreSlim`
  or pre-warm the font cache at startup; load-test concurrent `/pdf`.
- **SQL repository test gap** (§4.6) — 10 of 12 repos have no execution-level tests. Add SQLite-backed
  tests mirroring `PublishStatusRepositoryAuditTests`. Prioritize `AuthRepository` (password SQL,
  `IsActive=1`, hash-never-projected) and `CoursePdfRepository` (published-only gate).
- **JWT validation tests** (§4.6) — *partially done in Pass 2:* wrong-issuer → 401 is now covered.
  Still open: wrong-key-signed → 401, past-expiry → 401 (backend integration).
- ~~**`SigningKeyProvider` tests** (§4.6)~~ — **done in Pass 2** (`SigningKeyProviderTests.cs`).
- **`FeaturedPromoItemRepository.MoveSlotAsync` swap tests** (§4.6).

### P2 still open (hardening / hygiene)

- Pagination on all list endpoints (§4.2/§4.4) — `OFFSET/FETCH` + count, or `TOP (@Max)` cap.
- Map unique/FK `SqlException` (2601/2627/547) → 409/400 for Courses/Partners/FeaturedPromo/CourseGroups (§4.4).
- **Per-environment signing key** (§4.1) — *issuer/audience validation + min key length ≥32 bytes done in Pass 2;*
  a per-environment (not shared) key is still a deployment concern to resolve when the target is known.
- Rate limiting / lockout on `/api/Auth/login` (§4.1).
- Shorter access token + refresh/revocation; security-stamp invalidation on password change (§4.1).
- Gate CORS loopback policy to Development; explicit prod origin allow-list; security headers
  (X-Content-Type-Options, etc.) (§4.4).
- Cap rich-text field length before PDF render; consider streaming (§4.3).
- Prefer static Noto `-Regular`/`-Bold` fonts over the VF (§4.3).
- JWT-in-sessionStorage → httpOnly+Secure+SameSite cookie (§4.5, architectural) + CSP.
- **`environment.ts` production URL** (§4.5) — still `http://localhost:5000/api`.
  *Deferred deliberately:* the real deployment origin is unknown; guessing a `https://…` host would be
  wrong. Set this when the deploy target is known (and consider failing the build if a `production`
  env contains `localhost`/`http://`).

---

## 🕒 Deferred — remaining security findings (decision 2026-07-17)

**Decision:** the security findings still open after the CSO pass are **deferred**, not dropped. The one
HIGH finding (password hashing) is fixed; everything below is a conscious defer with a review trigger.

**Review trigger:** revisit **before the first production deployment**, or by **2026-08-17** (one month),
whichever comes first. Production-gated items *must* be closed before go-live — they are not optional, only
not-yet-actionable while the app runs local/dev only.

| # | Finding | Why deferred | Gate |
|---|---------|--------------|------|
| D1 | **Login rate-limiting / account lockout** (§4.1) | Brute-force risk; excluded from the CSO gate as rate-limit/DoS, but real. Accepted for now (internal, single-admin dev use). | Before prod |
| D2 | **Shorter access token + refresh/revocation + security-stamp on password change** (§4.1) | 24h stateless JWT has no revocation; a leaked token stays valid. Accepted for now. | Before prod |
| D3 | **Per-environment (non-shared) JWT signing key** (§4.1) | Can't finalize until the deploy target/secret store exists. | Before prod (blocker) |
| D4 | **Gate CORS to Development + explicit prod origin allow-list + security headers** (CSP, X-Content-Type-Options) (§4.4) | Loopback-only CORS is safe in dev; prod origins are unknown until deploy. | Before prod (blocker) |
| D5 | **JWT in `sessionStorage` → httpOnly+Secure+SameSite cookie + CSP** (§4.5) | Architectural change; no XSS sink exists today, so risk is bounded. | Before prod |
| D6 | **Remote-DB TLS**: `Encrypt=False;TrustServerCertificate=True` is safe for local `.\SQLEXPRESS` only | Must not carry to a remote prod DB (cleartext/MITM). | Before prod (blocker) |
| D7 | **`environment.ts` production URL** still `http://localhost:5000/api` (§4.5) | Real origin unknown; guessing would be wrong. Consider failing the build if a `production` env contains `localhost`/`http://`. | Before prod (blocker) |
| D8 | **Content controllers Admin-gating** (CSO observation) — Courses/CourseGroups/Partners/FeaturedPromoItems/Lookups/RowAudit are auth-gated but not `[Authorize(Roles="Admin")]` | Fine **iff** every user is Admin-class. Needs a product decision on whether a lower-privilege role should be denied catalogue writes. | Needs intent decision |

**Security test gaps** (verify the above behaviours; deferred with them): `AuthRepository` execution-level
tests (fetch-and-verify + migrate-on-next-login), and JWT wrong-key-signed → 401 / past-expiry → 401
integration tests (§4.6).

> The individual bullets remain in the P1/P2 lists below with their implementation notes; this table is the
> decision record. When a deferred item is picked up, move it to "Fixed" and strike its row here.

---

## Notes for the next pass
- The `Admin` role id is seeded via `AppUserRole('Admin','Admin')` (see `docs/setup-notes.md`); dev
  login `Admin`/`Admin` holds it, so the new role gates don't lock the dev user out.
- ~~Before the KDF change, add the `AuthRepository` SQL tests first~~ — the KDF change shipped in the
  CSO pass, verified by `PasswordHasher` unit tests + the existing 184 controller/mock tests.
  **Residual gap (honest):** there are still no execution-level (SQLite-backed) tests for
  `AuthRepository`'s new *fetch-and-verify-in-code* path or the *migrate-on-next-login* UPDATE. Add these
  next to lock in that (a) a legacy hash upgrades to PBKDF2 exactly once on login, (b) `PasswordHash` is
  never projected to a DTO, (c) `IsActive=0` still returns null.
- Keep this log current: when you fix a "Remaining" item, move it up to "Fixed" with its file(s).
