# 05 — ASP.NET Core (.NET 6–9): 100 cautionary samples

A fast-reference catalogue of **100 ASP.NET Core-specific pitfalls**, in the same teaching spirit as
[01](./01-password-hashing-unsalted-sha256.md). **This repo (`CMS.API`) is a .NET 9 Web API — a Core app —
so every entry here applies directly**, and several call out what this codebase already gets right. Scope is
current minimal-hosting + MVC/Web API + EF Core; not Web Forms, not classic MVC 5.

**Legend:** ❌ = don't · ✅ = do · 🎯 = the scenario that makes it dangerous · 📏 = the rule.

- [A. Middleware pipeline & hosting (1–10)](#a-middleware-pipeline--hosting)
- [B. Authentication & authorization (11–22)](#b-authentication--authorization)
- [C. Model binding & validation (23–32)](#c-model-binding--validation)
- [D. CSRF, CORS & cookies (33–42)](#d-csrf-cors--cookies)
- [E. Data Protection & secrets (43–51)](#e-data-protection--secrets)
- [F. EF Core & data access (52–61)](#f-ef-core--data-access)
- [G. DI, lifetimes & resources (62–69)](#g-di-lifetimes--resources)
- [H. Async & performance (70–79)](#h-async--performance)
- [I. Errors, logging & observability (80–89)](#i-errors-logging--observability)
- [J. Config, hardening, deploy & crypto (90–100)](#j-config-hardening-deploy--crypto)

---

## A. Middleware pipeline & hosting

### 1. Auth middleware registered before routing
❌ `UseAuthorization()` before `UseRouting()`. ✅ Order: `UseRouting` → `UseAuthentication` → `UseAuthorization` → endpoints.
🎯 Authorization runs before the endpoint (and its `[Authorize]` metadata) is selected, so policies silently don't apply. 📏 Routing first, then authN, then authZ, then endpoints.

### 2. `UseCors` in the wrong slot
❌ `UseCors()` after `UseAuthorization` or after the endpoints. ✅ Place it after `UseRouting` and before `UseAuthentication`/`UseAuthorization`.
🎯 Preflight `OPTIONS` requests hit auth first and 401, so legitimate cross-origin calls break — or CORS headers never attach. 📏 CORS goes between routing and auth.

### 3. Developer exception page in production
❌ `app.UseDeveloperExceptionPage()` unconditionally. ✅ Gate on `app.Environment.IsDevelopment()`; use `UseExceptionHandler` otherwise.
🎯 Anonymous callers get full stack traces, source snippets, and config values on any unhandled error. 📏 Developer diagnostics are dev-only.

### 4. No exception handler at all in production
❌ Rely on the framework's default 500 with nothing registered. ✅ `UseExceptionHandler` / a handling middleware (this app has `ExceptionHandlingMiddleware` returning generic 500s).
🎯 Unhandled exceptions can leak framework detail and bypass your logging/correlation. 📏 Every app has one catch-all handler that logs detail and returns a generic body.

### 5. Terminal middleware short-circuits the pipeline
❌ `app.Run(...)` (or forgetting `await next()`) early in the chain. ✅ Use `app.Use(async (ctx, next) => { …; await next(); })` for pass-through.
🎯 A logging/header middleware added with `Run` swallows the request; auth and the real endpoint never execute. 📏 Only the last middleware is terminal; everything else calls `next`.

### 6. `UseHttpsRedirection`/`UseHsts` missing outside dev
❌ Serve the API over plain HTTP in prod. ✅ `UseHttpsRedirection()` always, `UseHsts()` outside dev (this app does both).
🎯 Tokens and passwords travel cleartext; an on-path attacker reads or downgrades them. 📏 HTTPS everywhere but local dev; HSTS in prod.

### 7. No `UseForwardedHeaders` behind a reverse proxy
❌ Trust `HttpContext.Connection.RemoteIpAddress`/`Request.Scheme` directly behind nginx/YARP/ingress. ✅ `UseForwardedHeaders` (early) with `ForwardedHeaders.XForwardedFor | XForwardedProto` and `KnownProxies`/`KnownNetworks`.
🎯 The app sees the proxy's IP and `http`, so HTTPS redirect loops, and IP-based rate limits/logs blame the proxy. 📏 Restore client scheme/IP from forwarded headers — but only from trusted proxies.

### 8. Response caching in front of authorization
❌ `UseResponseCaching()` before auth on user-specific responses. ✅ Keep it after auth; mark private/`VaryBy` correctly, or don't cache authenticated responses.
🎯 One user's cached response is served to another because the cache key ignored identity. 📏 Never cache per-user responses on a shared key.

### 9. `UseStaticFiles` before auth exposing protected files
❌ `UseStaticFiles()` mapping a folder that also holds sensitive files, before any auth. ✅ Keep secrets out of the web root; gate protected downloads behind an authorized endpoint.
🎯 Static-file middleware serves anything under the root to anyone — no `[Authorize]` runs on it. 📏 The static root contains only public assets.

### 10. Custom middleware that never calls or awaits `next`
❌ `public Task InvokeAsync(HttpContext ctx) { DoWork(); return next(ctx); }` losing exceptions, or fire-and-forget. ✅ `await next(ctx);` inside an `async` method.
🎯 An exception downstream is unobserved, or the response completes before work finishes. 📏 Custom middleware is `async` and `await`s `next`.

---

## B. Authentication & authorization

### 11. Endpoints default-allow
❌ Every action public unless someone remembers `[Authorize]`. ✅ Global `FallbackPolicy` requiring an authenticated user (this app sets it) / `.RequireAuthorization()` on minimal-API groups.
🎯 A newly added controller ships unprotected because the attribute was forgotten. 📏 Secure by default; opt *out* explicitly with `[AllowAnonymous]`.

### 12. Roles vs. policies confusion
❌ Scatter `[Authorize(Roles="Admin")]` everywhere and hard-code role strings. ✅ Named policies (`AddAuthorizationBuilder().AddPolicy("CanManage", …)`) for anything beyond a simple role.
🎯 A role rename or a compound rule ("admin *and* same tenant") gets missed in one of twenty attributes. 📏 Express real authorization rules as policies, centrally.

### 13. `[AllowAnonymous]` at controller scope
❌ `[AllowAnonymous]` on the whole controller. ✅ Scope it to the one action that needs it (this app does exactly this on `CoursePdfController.GetPdf`).
🎯 A later action added to that controller silently inherits anonymous access. 📏 Grant anonymity per-action, as narrowly as possible.

### 14. Minimal-API endpoints without `.RequireAuthorization()`
❌ `app.MapPost("/admin/x", …)` relying on nothing. ✅ `.RequireAuthorization("AdminOnly")`, or a global fallback that covers map groups.
🎯 Minimal APIs have no controller-level attribute to inherit; the endpoint is wide open. 📏 Every minimal-API route states its authorization explicitly.

### 15. JWT: not validating issuer/audience/lifetime/key
❌ Accept any well-signed token. ✅ `TokenValidationParameters` with `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` all `true` (this app sets all four, ≥32-byte key).
🎯 A token minted for another service that shares the key, or an expired one, is accepted. 📏 Validate every claim and the signature you rely on.

### 16. Default `ClockSkew` masking expiry
❌ Leave `ClockSkew` at its 5-minute default and assume `exp` is exact. ✅ Set `ClockSkew = TimeSpan.Zero` (or a small, deliberate value) when you need tight expiry.
🎯 A "expired" token keeps working for up to 5 extra minutes — a surprising window after logout/rotation. 📏 Know and set your acceptable clock skew.

### 17. `[ApiController]` mistaken for an auth gate
❌ Assume `[ApiController]` implies authentication. ✅ It only adds binding/400 conventions; add `[Authorize]` (or the fallback policy) separately.
🎯 A controller marked `[ApiController]` but not `[Authorize]` is fully anonymous. 📏 `[ApiController]` is about model binding, not access control.

### 18. Trusting `IClaimsTransformation` / incoming claims blindly
❌ Copy an `X-Roles` header or unvalidated claim into the principal. ✅ Derive roles/claims from the validated token or a server store inside transformation.
🎯 Attacker self-asserts `role=Admin` via a header your transformation trusts. 📏 Authorization facts are server-verified, never client-asserted.

### 19. Ownership checks skipped (no resource-based authz)
❌ `[Authorize]` only, then `GET /orders/{id}` returns any order. ✅ Resource-based authorization (`IAuthorizationService.AuthorizeAsync(user, resource, "Owner")`) or `WHERE Id=@id AND UserId=@me`.
🎯 An authenticated user increments `id` and reads other users' records (IDOR). 📏 Presence of a token ≠ permission for *this* object.

### 20. Authorization enforced only in the client
❌ Hide the admin button in Angular and stop. ✅ Enforce on the API (`[Authorize(Roles="Admin")]`, as this app does).
🎯 Attacker calls the endpoint directly; the hidden button was decoration. 📏 UI gating is UX; the server enforces.

### 21. `MapInboundClaims` / claim-type surprises
❌ Look up `ClaimTypes.NameIdentifier` when the token used `sub` and default mapping changed it. ✅ Decide `JwtBearerOptions.MapInboundClaims` and read the exact claim type you issue.
🎯 An authorization check reads a claim that's `null` after remapping and fails open or throws. 📏 Issue and read claims under one agreed set of types.

### 22. Multiple auth schemes, ambiguous default
❌ Register cookie + JWT with no clear default scheme. ✅ Set `DefaultAuthenticateScheme`/`DefaultChallengeScheme`, and name the scheme in `[Authorize(AuthenticationSchemes=…)]`.
🎯 A protected API endpoint challenges with a cookie redirect instead of 401, or authenticates against the wrong scheme. 📏 Be explicit about which scheme guards which endpoint.

---

## C. Model binding & validation

### 23. Over-posting straight onto EF entities
❌ `[FromBody] AppUser user` bound to the EF entity incl. `IsAdmin`, `RoleIds`. ✅ Bind a narrow request DTO; map only whitelisted fields.
🎯 User posts extra fields the form never showed (`isActive`, roles) and escalates privilege. 📏 Accept a DTO that owns exactly the fields the action edits.

### 24. Disabling the automatic 400
❌ `SuppressModelStateInvalidFilter = true` then forgetting to check `ModelState`. ✅ Leave `[ApiController]`'s automatic 400 on, or check `ModelState.IsValid` yourself.
🎯 Invalid/missing fields sail past validation into your logic as defaults. 📏 If you suppress the auto-400, you own every `ModelState` check.

### 25. Binding-source confusion / spoofing
❌ Ambiguous `[FromRoute]` vs `[FromQuery]` vs `[FromBody]`; identity read from the body. ✅ Be explicit about each source; take identity from `User`, not the payload.
🎯 Attacker supplies `userId` in the body and edits someone else's account. 📏 Pin every binding source; never trust identity from request data.

### 26. `[Bind]` include/exclude misused
❌ Rely on `[Bind("Name,Email")]` as your only over-posting guard on a shared entity. ✅ Use a DTO; if binding an entity, mark server-owned props `[BindNever]`.
🎯 A property added later isn't in the exclude list and becomes over-postable. 📏 Prefer DTOs; `[BindNever]` server-controlled fields defensively.

### 27. Missing validation attributes
❌ No `[Required]`, `[Range]`, `[StringLength]` on inbound DTOs. ✅ Annotate every field; the automatic 400 then enforces them.
🎯 A direct API call sends an empty/oversized/negative value the Angular form would have blocked. 📏 Every inbound field has explicit constraints.

### 28. Unbounded bound collections
❌ Accept an arbitrarily large `List<T>` in the body. ✅ Validate count; tune `MvcOptions.MaxModelBindingCollectionSize` (default 1024) and cap request size.
🎯 A 10⁶-item array exhausts memory during binding before your code runs. 📏 Bound collection sizes at the binding layer, not just downstream.

### 29. Non-nullable value types silently defaulting
❌ `int quantity` on a DTO with no `[Required]`. ✅ Use `int?` + `[Required]`, or `[Range]`, to distinguish "missing" from `0`.
🎯 An omitted numeric field binds to `0` and skips a limit that only rejects negatives. 📏 Make "absent" detectable for value types.

### 30. `[JsonPropertyName]` / casing mismatches
❌ Assume the JSON field name matches the C# property. ✅ Set `[JsonPropertyName]` and a consistent `PropertyNamingPolicy`.
🎯 A silently-unbound field is treated as its default and a security-relevant value is lost. 📏 Make the wire contract explicit and tested.

### 31. System.Text.Json polymorphism from input
❌ Let the payload pick the CLR type via `[JsonPolymorphic]`/`[JsonDerivedType]` on attacker-controlled input. ✅ Bind to a concrete DTO; validate a discriminator against an allow-list.
🎯 Attacker names a dangerous derived type for the deserializer to construct. 📏 The server chooses types; the JSON doesn't.

### 32. Assuming `System.Text.Json` behaves like `Newtonsoft`
❌ Expect case-insensitive matching, comment tolerance, or `$type` handling by default. ✅ Configure `JsonSerializerOptions` explicitly; never re-enable `TypeNameHandling`-style type embedding.
🎯 Relaxed settings copied from old Newtonsoft habits reintroduce insecure deserialization. 📏 Know STJ's defaults; opt in deliberately, never to type-from-input.

---

## D. CSRF, CORS & cookies

### 33. Cookie auth without antiforgery
❌ Cookie-authenticated `POST`/`PUT`/`DELETE` with no antiforgery token. ✅ `[ValidateAntiForgeryToken]` / `AutoValidateAntiforgeryTokenAttribute` with `AddAntiforgery`.
🎯 A malicious page auto-submits a form; the browser attaches your cookie and the write succeeds (CSRF). 📏 State-changing cookie-auth requests need an antiforgery token.

### 34. Antiforgery added to pure bearer-token APIs
❌ Bolt CSRF tokens onto an API that only uses `Authorization: Bearer`. ✅ Rely on the fact that JS must *explicitly* attach the bearer; no ambient credential means no CSRF (this app is bearer-based).
🎯 Wasted complexity, or worse, a false sense the app is "CSRF-hardened" while a cookie path elsewhere isn't. 📏 CSRF protection targets ambient credentials (cookies), not bearer headers.

### 35. `AllowAnyOrigin()` + `AllowCredentials()`
❌ Combine wildcard origin with credentials. ✅ An explicit origin allow-list (this app restricts CORS to loopback).
🎯 It throws at runtime — or if forced, any site makes authenticated calls as your users. 📏 Wildcard origin and credentials are mutually exclusive, by design.

### 36. `SetIsOriginAllowed(_ => true)`
❌ Reflect any origin to dodge the wildcard-with-credentials error. ✅ Compare against a configured allow-list of exact origins.
🎯 You've rebuilt "allow any origin with credentials" and every origin is trusted. 📏 Echoing arbitrary origins is the same hole with extra steps.

### 37. Missing `SameSite` on auth cookies
❌ Leave `SameSite` unset/`None` without cause. ✅ `SameSite=Lax` (or `Strict`) for auth cookies; `None` only with `Secure` and a real cross-site need.
🎯 A cross-site request carries the auth cookie, enabling CSRF. 📏 Auth cookies default to `Lax`/`Strict`.

### 38. Auth cookie without `Secure`
❌ `CookieOptions` with `Secure=false` in prod. ✅ `Secure=true` (or `CookieSecurePolicy.Always`).
🎯 The cookie is sent over an HTTP downgrade and sniffed on-path. 📏 Auth cookies are HTTPS-only.

### 39. Auth cookie readable from JS
❌ `HttpOnly=false` on the session/auth cookie. ✅ `HttpOnly=true`.
🎯 Any XSS reads the cookie via `document.cookie` and hijacks the session. 📏 Auth cookies are `HttpOnly`.

### 40. CORS treated as a security boundary
❌ Assume a restrictive CORS policy stops non-browser clients. ✅ Enforce auth/authz server-side regardless of origin.
🎯 `curl`/Postman ignore CORS entirely and hit the endpoint directly. 📏 CORS constrains browsers, not attackers; it is not authorization.

### 41. Exposing headers/credentials too broadly
❌ `WithExposedHeaders("*")` or credentials on a broad policy. ✅ Expose only the headers the SPA needs; scope credentials to trusted origins.
🎯 Sensitive response headers become readable cross-origin. 📏 Whitelist exposed headers; minimize the credentialed surface.

### 42. Preflight not accounted for
❌ Custom auth/middleware that 401s or 500s the `OPTIONS` preflight. ✅ Let the CORS middleware (correctly ordered) answer preflight before auth.
🎯 Every non-simple cross-origin request fails, and teams "fix" it by loosening CORS to wildcard. 📏 Handle preflight in CORS middleware, ordered before auth.

---

## E. Data Protection & secrets

### 43. Data Protection key ring not persisted
❌ Default in-memory/ephemeral keys in a container with no persistence. ✅ `PersistKeysToFileSystem`/`PersistKeysToDbContext` on a durable, backed-up store.
🎯 On every restart the app mints new keys — all antiforgery tokens and auth cookies instantly become invalid. 📏 Persist the key ring to durable storage.

### 44. Key ring not shared across instances
❌ Each scaled-out instance keeps its own keys. ✅ Shared key store + `SetApplicationName(...)` identical on all instances.
🎯 Behind a load balancer, a cookie/token issued by instance A is rejected by instance B — random logouts. 📏 All instances of one app share one key ring and application name.

### 45. Secrets in committed `appsettings.json`
❌ Real connection strings/keys in `appsettings.json` in git. ✅ User-secrets in dev, environment/KeyVault in prod (this app reads its JWT key from DB `SysConfig`, commits no secrets).
🎯 Clone the repo → own the credentials, across all history. 📏 No real secret is ever committed.

### 46. Confusing user-secrets with production config
❌ Ship `dotnet user-secrets` values as your prod secret store. ✅ User-secrets are dev-only; use env vars / KeyVault / a real store in prod.
🎯 A secret set only in a dev profile is absent in prod, so config falls back to an insecure default. 📏 User-secrets is a dev convenience, not a deployment mechanism.

### 47. Secrets logged via config binding
❌ Bind config to an options object then log it, or dump `IConfiguration`. ✅ Redact; never serialize the whole options/config graph.
🎯 Connection strings and keys land in logs and error trackers. 📏 Config objects that hold secrets are never logged whole.

### 48. `Encrypt=false` / `TrustServerCertificate=true` to a remote DB
❌ Ship dev connection settings (unencrypted / cert-not-validated) to a remote database. ✅ Encrypt and validate the certificate for any off-box DB.
🎯 An on-path attacker reads or tampers with all DB traffic (only safe for same-host SQLEXPRESS). 📏 Encrypt + verify certs for non-local databases.

### 49. Key ring persisted but not encrypted at rest
❌ `PersistKeysToFileSystem` on a shared volume with no `ProtectKeysWith*`. ✅ `ProtectKeysWithCertificate` / DPAPI / KeyVault to encrypt the keys at rest.
🎯 Anyone reading the volume/backup gets the keys and can forge cookies and antiforgery tokens. 📏 Persisted keys are also encrypted at rest.

### 50. Data Protection used as a general secret vault
❌ `IDataProtector` to "encrypt" long-lived data you must decrypt after key rotation. ✅ Use it for short-lived payloads (tokens, cookies); use a real KMS for durable secrets.
🎯 Keys roll on their 90-day cycle and old ciphertext becomes undecryptable, or you disable rotation and weaken everything. 📏 Data Protection is for transient, self-owned payloads.

### 51. Wrong/reused protector purpose strings
❌ Share one `IDataProtector` purpose across unrelated features. ✅ Distinct, stable purpose strings per use (`CreateProtector("Course.Pdf.Link")`).
🎯 A token minted for one purpose is accepted by another feature that shares the protector. 📏 One purpose string per isolated use.

---

## F. EF Core & data access

### 52. `FromSqlRaw` with string concatenation
❌ `FromSqlRaw($"SELECT * FROM Course WHERE Name = '{name}'")`. ✅ `FromSqlInterpolated($"… WHERE Name = {name}")` or parameters (this repo uses Dapper with `@`-parameters throughout).
🎯 `name = "' OR 1=1; --"` dumps or drops the table. 📏 Never concatenate input into raw SQL; use interpolated/parameterized forms.

### 53. `ExecuteSqlRaw` with interpolation habits
❌ `ExecuteSqlRaw($"UPDATE … SET X = '{v}'")`. ✅ `ExecuteSqlInterpolated($"… SET X = {v}")` or explicit `SqlParameter`s.
🎯 The same injection as reads, now on a write path. 📏 `Raw` means *you* must parameterize; prefer the interpolated overload.

### 54. No concurrency token
❌ Blind `UPDATE`/`SaveChanges` with no version column. ✅ A `[Timestamp]` `byte[]` rowversion (`IsRowVersion()`); handle `DbUpdateConcurrencyException` → 409.
🎯 Two admins edit the same row; the second silently overwrites the first (lost update). 📏 Guard concurrent updates with a rowversion.

### 55. N+1 from missing `Include`
❌ Loop entities and lazy-load a navigation per item. ✅ `Include(...)` eagerly; `AsSplitQuery()` when a single join fans out.
🎯 One list view fires thousands of round-trips under real data. 📏 Load what you'll traverse in as few queries as correct.

### 56. Tracking when you only read
❌ Read-only queries tracked by the change tracker. ✅ `AsNoTracking()` for read-only paths.
🎯 Large read endpoints waste memory/CPU tracking entities that are never saved. 📏 Read-only queries are `AsNoTracking`.

### 57. Unbounded queries (no paging)
❌ `context.Course.ToListAsync()` returning every row. ✅ `Skip/Take` (server paging) with a capped page size and a total count.
🎯 A grown table returns millions of rows, OOMs the API, and stalls the UI. 📏 Every list endpoint pages and caps its maximum.

### 58. Returning EF entities directly
❌ Serialize the entity (incl. `PasswordHash`, internal FKs, navigations) to the client. ✅ Project to a DTO with only the fields you mean to expose.
🎯 A secret column or a lazy navigation rides into the JSON response or triggers extra queries during serialization. 📏 Map entities to DTOs at the boundary.

### 59. Multi-aggregate writes without a transaction
❌ Several `SaveChanges`/commands across aggregates with no transaction. ✅ One `IDbContextTransaction` (this app wraps create/update/delete in a transaction).
🎯 A crash between writes leaves half-applied, inconsistent state. 📏 Related writes commit atomically.

### 60. Client-side evaluation of filters
❌ `.AsEnumerable().Where(x => Expensive(x))` or a predicate EF can't translate, pulling the table into memory. ✅ Keep `Where`/`OrderBy` translatable so they run in SQL.
🎯 The full table is materialized and filtered in the app under real data. 📏 Push filtering and sorting to the database.

### 61. Auto-migrating/`EnsureCreated` at startup in prod
❌ `context.Database.Migrate()` or `EnsureCreated()` on every app start in production. ✅ Apply migrations as a controlled deploy step; `EnsureCreated` is for tests/prototypes only.
🎯 A racing multi-instance rollout applies schema changes concurrently, or a bad migration auto-runs against prod. 📏 Schema changes are a deliberate, gated step, not a startup side effect.

---

## G. DI, lifetimes & resources

### 62. Captive dependency (scoped captured by singleton)
❌ Inject a scoped service (e.g. `DbContext`) into a singleton. ✅ Inject `IServiceScopeFactory` and create a scope per unit of work.
🎯 The scoped service lives forever inside the singleton — stale data, cross-request bleed, threading bugs. 📏 A singleton must not hold a shorter-lived dependency.

### 63. `IServiceProvider` as a service locator
❌ Sprinkle `provider.GetService<T>()` through business code. ✅ Constructor-inject dependencies; reserve `GetRequiredService` for composition roots/factories.
🎯 Hidden dependencies and lifetime bugs that the container can't validate. 📏 Prefer constructor injection over pulling from the provider.

### 64. `new HttpClient()` per call
❌ Construct `HttpClient` per request. ✅ `IHttpClientFactory` (`AddHttpClient`) or a shared, long-lived client.
🎯 Sockets pile up in `TIME_WAIT` and the box runs out of ports under load. 📏 Obtain `HttpClient` from the factory.

### 65. Resolving scoped services from the root provider
❌ `app.Services.GetRequiredService<IScopedThing>()` at startup. ✅ `using var scope = provider.CreateScope();` then resolve from `scope.ServiceProvider`.
🎯 Resolving scoped from root throws, or (worse) turns the service into an app-lifetime singleton. 📏 Scoped services are resolved inside a scope.

### 66. Injecting scoped into middleware constructor
❌ Constructor-inject a scoped service into middleware. ✅ Inject it as a parameter of `InvokeAsync(HttpContext, IScopedThing)`.
🎯 Middleware is a singleton, so the scoped service is captured once and shared across all requests. 📏 Middleware takes scoped deps per-request in `InvokeAsync`.

### 67. Wrong `IOptions` flavour
❌ `IOptions<T>` where you need per-request or live-reload config. ✅ `IOptionsSnapshot<T>` (per-request, scoped) or `IOptionsMonitor<T>` (singleton + change notifications).
🎯 Config edits or per-tenant options never take effect because `IOptions<T>` is a frozen singleton. 📏 Match the options interface to the reload/scope you need.

### 68. `DbContext` registered as a singleton
❌ `AddSingleton<AppDbContext>()`. ✅ `AddDbContext<AppDbContext>()` (scoped) or a `DbContextFactory` for background work.
🎯 `DbContext` isn't thread-safe; concurrent requests corrupt its change tracker and connection. 📏 `DbContext` is scoped (or pooled/factory), never singleton.

### 69. Unchecked `GetService` returning null
❌ `provider.GetService<T>().DoThing()` on an unregistered service. ✅ `GetRequiredService<T>()` (throws clearly) when the dependency is mandatory.
🎯 A missing registration surfaces as a vague `NullReferenceException` deep in a request. 📏 Required dependencies use `GetRequiredService`.

---

## H. Async & performance

### 70. `async void`
❌ `async void Handler()` in non-event code. ✅ `async Task`.
🎯 Exceptions can't be caught by the caller and crash the process; nothing can await completion. 📏 `async Task`, never `async void` (except UI event handlers).

### 71. `.Result` / `.Wait()` / `.GetAwaiter().GetResult()`
❌ Block on async in a controller/handler. ✅ `await` all the way up.
🎯 Thread-pool starvation under load → deadlocks and stalls. 📏 Don't sync-over-async in server code.

### 72. Not flowing `CancellationToken`
❌ Ignore the token on async DB/HTTP calls. ✅ Accept `CancellationToken` on actions and thread it through (this app passes it to every `CommandDefinition`).
🎯 A client disconnects but the query keeps running, wasting a DB connection and CPU. 📏 Propagate the request's cancellation token everywhere.

### 73. Sync-over-async in a hot library path
❌ Wrap an async call in `Task.Run(...).Result` to "make it sync". ✅ Make the whole path async.
🎯 Two threads consumed per call (the blocked one + the pool worker) collapse throughput. 📏 Async is contagious — go async end to end.

### 74. Blocking Kestrel request threads
❌ `Thread.Sleep`, blocking I/O, or a lock held across an `await`-worthy call in a handler. ✅ Async I/O; keep locks tiny and off the request thread.
🎯 A handful of slow requests exhaust the thread pool and the whole server stops responding. 📏 Never block the request thread.

### 75. `Task.Run` to "offload" on the server
❌ `Task.Run(() => Work())` inside a request to look async. ✅ Just `await` real async I/O; use a background/hosted service for genuine background work.
🎯 `Task.Run` steals a thread-pool thread the server needs to accept requests — it doesn't add capacity. 📏 On the server, `Task.Run` is not free concurrency.

### 76. Buffering instead of streaming large results
❌ Build a giant `List<T>`/string for a huge response. ✅ `IAsyncEnumerable<T>` / streamed writes so rows flush as produced.
🎯 A multi-hundred-MB response is fully materialized in memory and OOMs the process. 📏 Stream large or unbounded responses.

### 77. `async` lambda coerced to `Action`
❌ Pass an `async` lambda where an `Action` is expected (it becomes `async void`). ✅ Use an API that takes `Func<Task>`.
🎯 Exceptions inside the lambda are unobservable and can crash the process. 📏 Async callbacks must return `Task`, not fit `Action`.

### 78. Fire-and-forget tasks
❌ Start a `Task` and never await/observe it. ✅ Await it, or hand long work to `IHostedService`/`BackgroundService` with error handling.
🎯 The work is silently dropped on shutdown/recycle, and its exceptions vanish. 📏 Every task is awaited or owned by a supervised background service.

### 79. `ValueTask` awaited twice / stored
❌ Cache or `await` a `ValueTask` more than once. ✅ Await it exactly once, or convert with `.AsTask()` if you must reuse it.
🎯 Re-awaiting a `ValueTask` is undefined behavior — wrong results or exceptions. 📏 A `ValueTask` is single-await; don't store or double-await it.

---

## I. Errors, logging & observability

### 80. Stack traces leaked via `ProblemDetails` in prod
❌ Populate `ProblemDetails.Detail`/`Extensions` with exception text in production. ✅ Generic problem responses; full detail to server logs only (this app's `ExceptionHandlingMiddleware` returns generic 500s).
🎯 Errors reveal table names, paths, and framework versions — recon for an attacker. 📏 Detailed logs, generic responses.

### 81. String-interpolated log messages
❌ `_logger.LogInformation($"User {id} did {x}")`. ✅ Message templates: `_logger.LogInformation("User {UserId} did {Action}", id, x)`.
🎯 You lose structured fields for querying, and unescaped values can forge log lines. 📏 Log with templates and named properties, not interpolation.

### 82. Logging secrets / PII
❌ `_logger.LogInformation("token={Token}", jwt)` or logging full user records. ✅ Log an id or a redacted marker.
🎯 Secrets and PII land in log stores/aggregators and breach with them. 📏 Never log tokens, passwords, keys, or unnecessary PII.

### 83. Log forging from unsanitized input
❌ Log raw user input containing CRLF into a text sink. ✅ Structured logging (the sink encodes fields); never build log lines by concatenating input.
🎯 Newlines in input inject fake log entries to hide an attacker's trail. 📏 Treat input as data in logs, never as message text.

### 84. No correlation across a request
❌ Unrelated log lines with no request/trace id. ✅ `TraceIdentifier`/W3C trace context / a scope with a correlation id.
🎯 During an incident you can't stitch one request's events together across services. 📏 Every log line carries a correlation/trace id.

### 85. Health-check endpoint leaking internals
❌ `MapHealthChecks("/health")` returning dependency names, versions, connection strings — unauthenticated. ✅ A minimal public liveness probe; detailed/`/ready` checks authenticated or internal-only.
🎯 Anonymous callers enumerate your backing services and versions. 📏 Public health checks say up/down and nothing more.

### 86. `UseStatusCodePages` information leak
❌ Verbose status-code pages echoing paths/reasons in prod. ✅ Generic status responses; keep detail in logs.
🎯 Distinct 401/403/404 bodies and messages leak which resources exist and why access failed. 📏 Status responses are uniform and uninformative to attackers.

### 87. Exception filters that swallow
❌ An `IExceptionFilter`/try-catch that logs and returns a 200/empty. ✅ Handle or rethrow; fail closed on a failed operation.
🎯 A failed security check or write is masked and the request "succeeds". 📏 A failed operation must not return success.

### 88. `IncludeErrorDetails` / dev diagnostics left on
❌ `JwtBearerOptions.IncludeErrorDetails = true` (or similar) in prod responses. ✅ Suppress detailed auth error reasons outside dev.
🎯 The `WWW-Authenticate`/error body tells an attacker exactly why a token was rejected (expired vs. bad signature vs. wrong audience). 📏 Auth failures are generic to the client.

### 89. Over-logging on hot paths
❌ `LogInformation`/`LogDebug` per row or per request with large payloads. ✅ Log at appropriate levels; sample or aggregate high-volume events.
🎯 Log volume balloons cost, hides real signals, and can itself DoS the log pipeline. 📏 Log level and volume match the event's importance.

---

## J. Config, hardening, deploy & crypto

### 90. Swagger/OpenAPI exposed in production
❌ `UseSwagger()`/`UseSwaggerUI()` unconditionally. ✅ Gate on `app.Environment.IsDevelopment()` (this app does).
🎯 The full API surface, schemas, and sometimes a "try it" client are handed to anonymous callers. 📏 API explorers stay in dev.

### 91. No request-size limits
❌ Accept arbitrarily large bodies/uploads. ✅ Kestrel `MaxRequestBodySize`, `[RequestSizeLimit]`, and multipart limits; avoid blanket `[DisableRequestSizeLimit]`.
🎯 A single huge upload exhausts memory/disk and takes the process down. 📏 Every endpoint has an explicit maximum body size.

### 92. No rate limiting on auth endpoints
❌ Unlimited login/token/reset attempts. ✅ The built-in rate limiter (`AddRateLimiter` + `UseRateLimiter`, .NET 7+) on auth routes, plus lockout/backoff.
🎯 Credential stuffing and brute force run unimpeded. 📏 Throttle authentication and other abuse-prone endpoints.

### 93. Response compression over HTTPS for secret-bearing responses
❌ `EnableForHttps = true` on responses that mix secrets with attacker-influenced content. ✅ Don't compress responses containing secrets alongside reflected input (BREACH).
🎯 Compression-ratio side channels let an attacker recover a CSRF token/secret byte by byte. 📏 Don't compress secret + attacker-controlled data together over TLS.

### 94. Missing security headers / CSP
❌ No `Content-Security-Policy`, `X-Content-Type-Options: nosniff`, `X-Frame-Options`/`frame-ancestors`. ✅ Emit a baseline header set at the edge or in middleware.
🎯 XSS payloads run freely; MIME-sniffing turns an upload into script; the app is clickjacked in a frame. 📏 Ship a baseline security-header set.

### 95. `AllowedHosts` wildcard in production
❌ `"AllowedHosts": "*"`. ✅ List the exact host names the app serves.
🎯 Host-header attacks poison absolute URLs (password-reset links, cache keys). 📏 Pin `AllowedHosts` to your real domains.

### 96. `ASPNETCORE_ENVIRONMENT=Development` in production
❌ Deploy with the environment left as Development. ✅ Set `Production`; verify `IsDevelopment()` is false on the box.
🎯 Every dev-only gate flips open at once — Swagger, developer exception page, verbose errors. 📏 The deployed environment name matches reality.

### 97. `Random` for security tokens
❌ `new Random().Next()` / `Guid.NewGuid()` as a reset or download token. ✅ `RandomNumberGenerator.GetBytes(32)`.
🎯 `Random` is predictable from a few outputs; GUIDs aren't guaranteed unguessable. 📏 CSPRNG for anything an attacker must not predict.

### 98. `==` to compare tokens/HMACs
❌ `if (provided == expected)` on a token or signature. ✅ `CryptographicOperations.FixedTimeEquals(a, b)`.
🎯 String `==` short-circuits on the first differing byte; timing leaks the secret byte by byte. 📏 Constant-time compare for all secret material.

### 99. `DateTime.Now` and culture-sensitive parsing
❌ `DateTime.Now` for token expiry/timestamps; `decimal.Parse(s)` with ambient culture. ✅ `DateTime.UtcNow` everywhere; `CultureInfo.InvariantCulture` for machine-facing parse/format.
🎯 DST/timezone drift breaks `exp` checks; a comma-decimal locale mis-parses an amount. 📏 UTC for time, InvariantCulture for machine logic.

### 100. Hardcoded keys & fast hashes for secrets
❌ `var key = "s3cr3t";` in source; bare `SHA256` for passwords. ✅ Runtime config for keys (this app reads the JWT key from DB `SysConfig`); PBKDF2 for passwords (see [sample 01](./01-password-hashing-unsalted-sha256.md)) with `decimal` for any money involved.
🎯 Anyone with repo/history access forges tokens forever; a leaked hash table cracks in minutes. 📏 Keys live in config, passwords use a slow salted KDF, money is `decimal`.

---

## Using this list

- Skim the category that matches what you're about to write **before** you write it — most of these are
  wiring/config decisions made once in `Program.cs` and easy to get wrong quietly.
- In review, the 📏 lines double as a checklist — a "no" on any is a finding.
- Found a new one the hard way? Add it here; if it's sharp enough to teach from, give it a full
  before/after file like [01](./01-password-hashing-unsalted-sha256.md) and link it from
  [README.md](./README.md).
