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

## ⛔ Remaining — pick these up next (ranked)

### P1 still open (each needs a schema/DB change, a load test, or a large test harness — deliberately deferred)

- **Password hashing → salted KDF** (§4.1, PasswordHasher.cs / AuthRepository.cs / AppUserRepository.cs).
  Biggest change. Comparison currently happens *in SQL* (`WHERE PasswordHash = @PasswordHash`); a real
  KDF (PBKDF2 `PasswordHasher<T>` / Argon2id / bcrypt) needs per-hash salt, so the SQL must change to
  *fetch* the hash and verify **in code**, plus a migrate-on-next-login path for existing SHA-256 hashes.
  Touches login, reset-password, change-password, create-user. Do this behind tests (see below) first.
- **Optimistic concurrency (rowversion)** on updates (§4.2) — needs a `rowversion` column + migration;
  `Course/AppUser/AppRole/Partner/FeaturedPromoItem` repos; return 409 on `affected == 0`.
- **`MoveSlotAsync` race** (§4.2) — `UPDLOCK, HOLDLOCK` on the initial SELECT or `SERIALIZABLE`.
- **PDFsharp concurrent font/render state** (§4.3) — serialize `Render` behind a `SemaphoreSlim`
  or pre-warm the font cache at startup; load-test concurrent `/pdf`.
- **SQL repository test gap** (§4.6) — 10 of 12 repos have no execution-level tests. Add SQLite-backed
  tests mirroring `PublishStatusRepositoryAuditTests`. Prioritize `AuthRepository` (password SQL,
  `IsActive=1`, hash-never-projected) and `CoursePdfRepository` (published-only gate).
- **JWT validation tests** (§4.6) — wrong-key-signed → 401, past-expiry → 401 (backend integration).
- **`SigningKeyProvider` tests** (§4.6) — JSON extraction valid/missing/empty + cache-once.
- **`FeaturedPromoItemRepository.MoveSlotAsync` swap tests** (§4.6).

### P2 still open (hardening / hygiene)

- Pagination on all list endpoints (§4.2/§4.4) — `OFFSET/FETCH` + count, or `TOP (@Max)` cap.
- Request DTO validation attributes (`[StringLength]`/`[Range]`/`[Required]`) so bad input → 400 not 500 (§4.4).
- Map unique/FK `SqlException` (2601/2627/547) → 409/400 for Courses/Partners/FeaturedPromo/CourseGroups (§4.4).
- Issuer/audience emission + validation; per-environment signing key; min key length ≥32 bytes (§4.1).
- Rate limiting / lockout on `/api/Auth/login` (§4.1).
- Shorter access token + refresh/revocation; security-stamp invalidation on password change (§4.1).
- Gate CORS loopback policy to Development; explicit prod origin allow-list; security headers
  (X-Content-Type-Options, etc.) (§4.4).
- LIKE wildcard escaping in keyword filters (§4.2).
- `SqlConnectionFactory`: dispose connection if `OpenAsync` throws (§4.2).
- `RowAuditReflection`: cache `PropertyInfo[]` per type (§4.2, low priority).
- Cap rich-text field length before PDF render; consider streaming (§4.3).
- Prefer static Noto `-Regular`/`-Bold` fonts over the VF (§4.3).
- Error interceptor: prefer generic message for 5xx instead of raw server `message` (§4.5).
- JWT-in-sessionStorage → httpOnly+Secure+SameSite cookie (§4.5, architectural) + CSP.
- **`environment.ts` production URL** (§4.5) — still `http://localhost:5000/api`.
  *Deferred deliberately:* the real deployment origin is unknown; guessing a `https://…` host would be
  wrong. Set this when the deploy target is known (and consider failing the build if a `production`
  env contains `localhost`/`http://`).

---

## Notes for the next pass
- The `Admin` role id is seeded via `AppUserRole('Admin','Admin')` (see `docs/setup-notes.md`); dev
  login `Admin`/`Admin` holds it, so the new role gates don't lock the dev user out.
- Before the KDF change, add the `AuthRepository` SQL tests first — they are the safety net that lets
  you refactor password verification from SQL into code with confidence.
- Keep this log current: when you fix a "Remaining" item, move it up to "Fixed" with its file(s).
