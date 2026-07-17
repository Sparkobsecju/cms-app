# 02 — 100 cautionary samples (extra learning)

A fast-reference catalogue of **100 coding pitfalls**, in the same spirit as
[01](./01-password-hashing-unsalted-sha256.md): each shows the dangerous shape, the scenario in which it
bites, and the rule. Tuned to this stack — **.NET 9 Web API, Dapper, SQL Server, JWT, Angular + PrimeNG.**

These are compressed on purpose. When one bites you for real, promote it to its own numbered file with a
full before/after + attack walkthrough, like sample 01.

**Legend:** ❌ = don't · ✅ = do · 🎯 = the scenario that makes it dangerous · 📏 = the rule.

- [A. Cryptography & secrets (1–12)](#a-cryptography--secrets)
- [B. Authentication, sessions & JWT (13–24)](#b-authentication-sessions--jwt)
- [C. Authorization & access control (25–33)](#c-authorization--access-control)
- [D. Injection (34–46)](#d-injection)
- [E. Input validation & deserialization (47–54)](#e-input-validation--deserialization)
- [F. Error handling & logging (55–62)](#f-error-handling--logging)
- [G. .NET / C# pitfalls (63–74)](#g-net--c-pitfalls)
- [H. Dapper / SQL Server / data layer (75–82)](#h-dapper--sql-server--data-layer)
- [I. Angular / frontend / TypeScript (83–92)](#i-angular--frontend--typescript)
- [J. Config, deployment, infra & supply chain (93–100)](#j-config-deployment-infra--supply-chain)

---

## A. Cryptography & secrets

### 1. Bare fast hash for passwords
❌ `SHA256.HashData(pw)` / MD5 / SHA1 for password storage. ✅ PBKDF2/bcrypt/Argon2 with per-user salt (see sample 01).
🎯 DB leaks → GPU cracks billions/sec, rainbow tables on unsalted hashes. 📏 Password hash must be **slow + salted**.

### 2. `==` to compare secrets/hashes/tokens
❌ `if (userToken == storedToken)`. ✅ `CryptographicOperations.FixedTimeEquals(a, b)`.
🎯 String `==` short-circuits on first differing byte; timing leaks the secret byte-by-byte. 📏 Constant-time compare for all secret material.

### 3. `Random`/`Guid.NewGuid()` for security tokens
❌ `new Random().Next()` or a GUID as a password-reset token. ✅ `RandomNumberGenerator.GetBytes(32)`.
🎯 `Random` is predictable from a few outputs; GUIDs aren't guaranteed unguessable. 📏 CSPRNG for anything an attacker must not predict.

### 4. Hardcoded key/secret in source
❌ `var key = "s3cr3t-signing-key";`. ✅ Runtime config (this app reads the JWT key from `SysConfig` in the DB).
🎯 Anyone with repo/history read access forges tokens forever. 📏 Secrets never in code or git history.

### 5. Encryption without integrity (unauthenticated mode)
❌ AES-CBC with no MAC. ✅ AES-GCM (`AesGcm`) or encrypt-then-MAC.
🎯 Padding-oracle / bit-flipping attacks silently tamper ciphertext. 📏 Use authenticated encryption (AEAD).

### 6. Reusing a nonce/IV
❌ Fixed or counter-reset IV with GCM/CTR. ✅ Fresh random nonce per message, stored alongside ciphertext.
🎯 Nonce reuse in GCM leaks the auth key; in CTR it XORs two plaintexts. 📏 Never reuse a nonce under the same key.

### 7. Rolling your own crypto
❌ Custom "encryption" by XOR/base64/shifting. ✅ Vetted primitives from `System.Security.Cryptography`.
🎯 Home-grown schemes fall to standard cryptanalysis in minutes. 📏 Don't invent crypto; use the library.

### 8. Logging secrets
❌ `_logger.LogInformation("token={Token}", jwt)`. ✅ Log an id or a redacted marker.
🎯 Secrets land in log files / aggregators / screenshots and outlive rotation. 📏 Never log tokens, passwords, keys, or full PII.

### 9. Secret in a URL / query string
❌ `GET /reset?token=abc`. ✅ Token in the request body or a short-lived header.
🎯 Query strings hit access logs, browser history, `Referer` headers. 📏 Secrets go in bodies/headers, never the URL.

### 10. Too-short / low-entropy signing key
❌ 8-char HMAC key. ✅ ≥32 bytes (this app enforces `MinKeyBytes = 32`, fail-closed).
🎯 Short keys are brute-forceable, letting attackers sign valid tokens. 📏 HMAC-SHA256 needs ≥256-bit keys.

### 11. Secrets committed then "removed" in a later commit
❌ Delete the key in a new commit and assume it's gone. ✅ Rotate the secret; scrub history (BFG/filter-repo).
🎯 Git keeps every historical version; the old key is one `git log -p` away. 📏 A leaked secret is burned — rotate it.

### 12. Comparing password hashes case-insensitively / with culture
❌ `hash.Equals(other, StringComparison.OrdinalIgnoreCase)`. ✅ Byte-level ordinal / fixed-time compare.
🎯 Case-folding widens the match space; culture rules cause locale-dependent bugs. 📏 Ordinal, byte-exact, constant-time.

---

## B. Authentication, sessions & JWT

### 13. Not validating issuer/audience/lifetime
❌ Accept any well-signed JWT. ✅ Validate `iss`, `aud`, `exp`, signing key (this app sets all four).
🎯 A token minted for another service that shares the key is accepted. 📏 Validate every claim you rely on.

### 14. Accepting `alg: none` or algorithm confusion
❌ Trusting the token's `alg` header. ✅ Pin allowed algorithms; symmetric-only avoids RS256→HS256 confusion.
🎯 Attacker sets `alg:none` or signs HS256 with the public key as the secret. 📏 Server decides the algorithm, not the token.

### 15. Long-lived tokens with no revocation
❌ 30-day JWT, no way to invalidate. ✅ Short access token + refresh/rotation; security-stamp on password change.
🎯 A leaked token is valid for its whole lifetime; a compromised account can't be cut off. 📏 Short expiry + a revocation path.

### 16. Password-change doesn't invalidate old sessions
❌ Change password, existing JWTs still work. ✅ Bump a per-user "security stamp" checked at validation.
🎯 Victim changes password after a breach; attacker's stolen token keeps working. 📏 Credential change must kill live sessions.

### 17. User-enumeration via distinct errors
❌ "No such user" vs "wrong password"; different timing for unknown users. ✅ One generic 401 for all failures.
🎯 Attacker maps valid usernames, then targets them for spraying. 📏 Auth failures are indistinguishable.

### 18. No throttling/lockout on login
❌ Unlimited login attempts. ✅ Rate-limit + backoff/lockout per account and per IP.
🎯 Credential stuffing / brute force runs unimpeded (worse with a fast hash). 📏 Cap and slow repeated auth attempts.

### 19. Trusting identity from the request body
❌ `UpdateProfile(userId from body)`. ✅ Take identity from the validated token (this app uses `User.FindFirstValue`).
🎯 Attacker sets someone else's `userId` and edits their account. 📏 Identity comes from the token, never the payload.

### 20. Storing JWT in `localStorage`
❌ `localStorage.setItem('jwt', token)`. ✅ `sessionStorage` at least; ideally httpOnly+Secure+SameSite cookie + CSP.
🎯 Any XSS reads `localStorage` and exfiltrates the token; it also survives tab close. 📏 Keep tokens out of JS-readable storage where feasible.

### 21. No password policy on the server
❌ Only the Angular form checks strength. ✅ Enforce on the server (this app has `PasswordPolicy.IsCompliant`).
🎯 Attacker calls the API directly, bypassing client validation, sets "123". 📏 The server is always authoritative.

### 22. Verbose auth error surfaces internals
❌ Return "DB timeout connecting to sql-prod-02". ✅ Generic message; detail to server logs only.
🎯 Errors leak infra topology and aid targeting. 📏 Clients get generic; logs get detail.

### 23. Session fixation / not rotating token on login
❌ Reuse a pre-auth session id after login. ✅ Issue a fresh token/session at successful authentication.
🎯 Attacker plants a known session id, victim logs in, attacker rides it. 📏 New credential on privilege change.

### 24. Reset tokens: no expiry / reusable / not single-use
❌ Reset link valid forever, works twice. ✅ Short TTL, one-time, invalidated on use.
🎯 An old email/link in a mailbox is replayed months later. 📏 Reset tokens are short-lived and single-use.

---

## C. Authorization & access control

### 25. Authentication ≠ authorization
❌ "User is logged in" → allow. ✅ Check they're allowed to touch *this* resource/action.
🎯 Any logged-in user hits an admin action that only checked for a token. 📏 Verify permission, not just presence of a token.

### 26. IDOR — no ownership check
❌ `GET /orders/{id}` returns any order. ✅ Scope by owner: `WHERE Id=@id AND UserId=@me`.
🎯 User increments `id` and reads other users' records. 📏 Every object access is scoped to the caller's rights.

### 27. Client-side-only authorization
❌ Hide the admin button in Angular and stop there. ✅ Enforce roles on the API (`[Authorize(Roles="Admin")]`).
🎯 Attacker calls the endpoint directly; the hidden button was decoration. 📏 UI gating is UX; the server enforces.

### 28. Missing default-deny
❌ Endpoints public unless you remember `[Authorize]`. ✅ Global fallback requires auth (this app's `FallbackPolicy`).
🎯 A newly added controller ships unprotected because someone forgot the attribute. 📏 Secure by default; opt *out* explicitly.

### 29. `[AllowAnonymous]` at controller scope leaking to new actions
❌ Anonymous on the whole controller. ✅ Scope it to the single action (this app does on `CoursePdfController.GetPdf`).
🎯 A later action added to the controller silently inherits anonymous access. 📏 Grant anonymity per-action, narrowly.

### 30. Trusting a role/claim from the client
❌ Read `isAdmin` from a request header/body. ✅ Roles come from the signed token / server store.
🎯 Attacker sets `X-Is-Admin: true`. 📏 Authorization facts must be server-verified, not self-asserted.

### 31. Mass assignment / over-posting
❌ Bind the request straight onto the entity incl. `IsAdmin`, `RoleIds`. ✅ Bind a narrow DTO; whitelist fields.
🎯 User posts extra fields (`isActive`, roles) the form never showed and escalates. 📏 Accept only the fields the action owns.

### 32. Horizontal vs. vertical privilege blur
❌ Same handler for "edit self" and "edit anyone". ✅ Separate paths; admin path role-gated.
🎯 A self-service edit endpoint is reused to edit others by changing an id. 📏 Separate self-service from admin operations.

### 33. Forgotten authorization on secondary actions
❌ Protect `GET` list but not the `export`/`bulk-delete` sibling. ✅ Authorize every verb/action on the resource.
🎯 The overlooked bulk endpoint becomes the breach path. 📏 Audit *all* actions, not just the obvious read.

---

## D. Injection

### 34. String-concatenated SQL
❌ `"...WHERE Name='" + name + "'"`. ✅ Parameters (`@Name`) — Dapper does this throughout this app.
🎯 `name = "' OR 1=1;--"` dumps/drops the table. 📏 Never build SQL by concatenating input.

### 35. Interpolating identifiers (table/column/sort) from input
❌ `$"ORDER BY {sortColumn}"`. ✅ Whitelist to a fixed set of allowed columns/directions.
🎯 Parameters can't bind identifiers, so devs interpolate them → injection via `sortColumn`. 📏 Validate identifiers against an allow-list.

### 36. Unescaped `LIKE` wildcards
❌ `LIKE '%'+@kw+'%'` with raw `%`/`_`. ✅ Escape wildcards + `ESCAPE '\'` (this app's `SqlLike.EscapeWildcards`).
🎯 A search for `%` matches everything; crafted patterns cause pathological scans. 📏 Escape LIKE metacharacters.

### 37. OS command injection
❌ `Process.Start("cmd", "/c convert " + userFile)`. ✅ Pass args as an array; validate/whitelist inputs.
🎯 `userFile = "a & del /q C:\\*"` runs arbitrary commands. 📏 Never pass untrusted input to a shell string.

### 38. Path traversal on file access
❌ `File.ReadAllBytes(base + userPath)`. ✅ Canonicalize + verify the resolved path stays under the root.
🎯 `userPath = "..\\..\\appsettings.json"` reads secrets. 📏 Resolve then confine to the allowed directory.

### 39. XSS via raw HTML sink
❌ `[innerHTML]="userContent"` / `dangerouslySetInnerHTML`. ✅ Interpolation (Angular escapes by default).
🎯 Stored `<img onerror=...>` runs in every viewer's session. 📏 Don't hand untrusted strings to HTML sinks.

### 40. Bypassing the framework's XSS guard
❌ `DomSanitizer.bypassSecurityTrustHtml(x)` on untrusted input. ✅ Only bypass for content you generated/trust.
🎯 The one "trust me" call is the XSS hole in an otherwise safe app. 📏 Treat every bypass as a security review item.

### 41. Open redirect
❌ `return Redirect(returnUrl)` from a query param. ✅ Allow only relative paths / an allow-list of hosts.
🎯 `returnUrl=https://evil.tld` powers convincing phishing off your domain. 📏 Validate redirect targets.

### 42. Server-side template / expression injection
❌ Rendering a template string built from user input. ✅ Pass data as parameters, not into the template source.
🎯 Input becomes template code and executes on the server. 📏 Data is data; never compile user input as a template.

### 43. LDAP / NoSQL / header injection
❌ Concatenate input into an LDAP filter, Mongo query, or HTTP header. ✅ Encode/parameterize per the target grammar.
🎯 `*)(uid=*` bypasses an LDAP auth filter; CRLF in a header splits the response. 📏 Escape for the specific sink, always.

### 44. `eval`/`Function`/dynamic code on input
❌ `eval(userExpr)`, `new Function(userStr)`. ✅ A parser/whitelist for the allowed grammar.
🎯 Arbitrary code runs in the server or the victim's browser. 📏 Never execute strings derived from input.

### 45. XML external entities (XXE)
❌ Default `XmlReader`/parser with DTD processing on untrusted XML. ✅ Disable DTD/external entities.
🎯 `<!ENTITY xxe SYSTEM "file:///...">` reads local files or triggers SSRF. 📏 Turn off external entities on parsers.

### 46. CSV/formula injection in exports
❌ Write a user cell starting with `=`, `+`, `-`, `@` straight to CSV. ✅ Prefix a `'` or sanitize leading operators.
🎯 Excel executes the "formula" on open → data exfiltration on the victim's machine. 📏 Neutralize formula triggers in exports.

---

## E. Input validation & deserialization

### 47. Trusting client validation only
❌ Angular checks it, server assumes it's clean. ✅ Re-validate every field server-side.
🎯 Direct API calls skip the form entirely. 📏 Client validation is UX; server validation is truth.

### 48. Unbounded input sizes
❌ Accept an arbitrarily long string/array/file. ✅ Cap lengths, counts, and request/body size.
🎯 A 500 MB "name" or 10⁶-item array exhausts memory / balloons the PDF. 📏 Every input has an explicit maximum.

### 49. Insecure deserialization
❌ `BinaryFormatter` / type-embedding deserializers on untrusted data. ✅ `System.Text.Json` with known types.
🎯 A crafted payload instantiates gadget types → RCE. 📏 Deserialize to fixed DTOs; never polymorphic-from-input.

### 50. `JsonSerializer` with `TypeNameHandling`/polymorphism from input
❌ Let the payload pick the CLR type. ✅ Bind to a concrete DTO; validate a discriminator against an allow-list.
🎯 Attacker names a dangerous type to construct. 📏 The server chooses types, not the JSON.

### 51. Missing null/again-check after parse
❌ Assume `dict[key]`/`FindFirst(...)` is non-null. ✅ Check for null and fail closed.
🎯 A missing claim/field throws or, worse, is treated as an empty-but-valid value. 📏 Validate presence before use.

### 52. Numeric overflow / unchecked casts
❌ `(int)longValue`, silent wraparound in `unchecked`. ✅ Validate ranges; use `checked` where it matters.
🎯 A huge quantity wraps negative and skips a limit check. 📏 Bound and range-check numbers from input.

### 53. Regex on untrusted input with catastrophic backtracking (ReDoS)
❌ `(a+)+$` style patterns on user strings. ✅ Linear patterns; timeouts; anchored, possessive where available.
🎯 One crafted string pins a CPU core for seconds/minutes. 📏 Keep input-facing regexes linear + time-bounded.

### 54. Content-type / file-type trust
❌ Trust the uploaded `Content-Type` or extension. ✅ Sniff magic bytes; store outside webroot; re-encode images.
🎯 A `.jpg` that's actually an executable/HTML gets served and run. 📏 Verify file type by content, not by label.

---

## F. Error handling & logging

### 55. Leaking stack traces / SQL errors to clients
❌ Return the exception message. ✅ Generic message to client, full detail to server logs (this app's `ExceptionHandlingMiddleware`).
🎯 Errors reveal table names, paths, versions — a recon goldmine. 📏 Detailed logs, generic responses.

### 56. Swallowing exceptions
❌ `catch { }`. ✅ Handle or rethrow; log with context.
🎯 A failed security check is silently ignored and the request proceeds. 📏 Never empty-catch; failures must be visible.

### 57. Catching `Exception` too broadly and continuing
❌ Blanket catch that masks `OperationCanceledException`, auth failures. ✅ Catch specific types; let fatal ones surface.
🎯 A cancellation or authz exception is treated as a recoverable hiccup. 📏 Catch narrowly; don't hide the wrong thing.

### 58. Logging unsanitized input (log forging)
❌ `LogInformation(userInput)` with raw newlines. ✅ Log structured fields; the sink escapes them.
🎯 CRLF in input injects fake log lines to hide tracks. 📏 Use structured logging, not string-concatenated messages.

### 59. No audit trail on sensitive mutations
❌ Silent create/update/delete on privileged data. ✅ Record who/what/when (this app's `RowAuditWriter`).
🎯 A breach can't be reconstructed; insider actions are undeniable-free. 📏 Audit privileged state changes.

### 60. Error-driven side channels
❌ Different responses/timing for "exists" vs "forbidden". ✅ Uniform 404/response for both.
🎯 Response differences leak the existence of hidden resources. 📏 Don't let errors reveal what you're hiding.

### 61. Logging full request/response bodies
❌ Dump every payload at Info. ✅ Log metadata; redact bodies, especially auth/PII.
🎯 Passwords/tokens/PII pile up in logs and breach with them. 📏 Bodies are sensitive; don't log them wholesale.

### 62. Continuing after a failed integrity/authz check
❌ Log "check failed" then proceed anyway. ✅ Fail closed — stop and return an error.
🎯 The check becomes decorative; the dangerous path still runs. 📏 A failed security check must block the operation.

---

## G. .NET / C# pitfalls

### 63. `async void`
❌ `async void DoWork()`. ✅ `async Task` (except event handlers).
🎯 Exceptions can't be caught by the caller and crash the process; nothing can await it. 📏 `async Task`, never `async void`.

### 64. `.Result` / `.Wait()` / `.GetAwaiter().GetResult()` on hot paths
❌ Blocking on async in request handling. ✅ `await` all the way up.
🎯 Thread-pool starvation → deadlocks and stalls under load. 📏 Don't sync-over-async in server code.

### 65. Not passing `CancellationToken`
❌ Ignore the token in async DB/HTTP calls. ✅ Thread it through (this app passes it to every `CommandDefinition`).
🎯 Abandoned requests keep running, wasting DB connections and CPU. 📏 Propagate the caller's cancellation token.

### 66. `HttpClient` per request (socket exhaustion)
❌ `new HttpClient()` each call. ✅ `IHttpClientFactory` / a shared client.
🎯 Sockets stuck in `TIME_WAIT`; the box runs out of ports under load. 📏 Reuse `HttpClient` via the factory.

### 67. Disposing/holding a scoped resource from a singleton
❌ Capturing a scoped `DbConnection`/DbContext in a singleton. ✅ Resolve per-operation; open/close per use.
🎯 Cross-request state bleed, threading bugs, leaked connections. 📏 Respect DI lifetimes; don't capture shorter-lived services.

### 68. Not disposing `IDisposable`
❌ Forgetting to dispose connections/streams. ✅ `using var ...` (this app uses it for connections).
🎯 Leaked connections exhaust the pool; the app hangs waiting for one. 📏 `using` every disposable.

### 69. Mutable `static` shared state
❌ A `static` dictionary/list mutated per request. ✅ Per-request scope or a thread-safe/immutable structure.
🎯 Race conditions corrupt data or leak one user's data to another. 📏 No unsynchronized mutable statics in a server.

### 70. `DateTime.Now` instead of `DateTime.UtcNow`
❌ Store/compare local time. ✅ UTC everywhere; convert only at the edge for display.
🎯 DST/timezone drift breaks token expiry, scheduling, and comparisons. 📏 Persist and compare in UTC.

### 71. Culture-sensitive parsing/formatting/compare
❌ `double.Parse(s)` / `ToUpper()` with the ambient culture. ✅ `InvariantCulture`; `StringComparison.Ordinal` for logic.
🎯 A comma-decimal locale mis-parses; Turkish-I breaks identifier compares. 📏 Invariant/ordinal for machine logic.

### 72. Floating point for money
❌ `double price`. ✅ `decimal`.
🎯 Rounding drift makes totals wrong by cents that add up and fail reconciliation. 📏 Money is `decimal`.

### 73. `.Select().Where()` after materializing (N+1 / client eval)
❌ Load all rows then filter in memory / query per item in a loop. ✅ Filter in SQL; batch.
🎯 Thousands of round-trips or full-table loads under real data. 📏 Push filtering/joins to the database.

### 74. Ignoring `ConfigureAwait` in libraries / capturing context needlessly
❌ Library code that assumes a sync context. ✅ `ConfigureAwait(false)` in library layers.
🎯 Deadlocks when consumed from a context-bound caller. 📏 Library awaits don't capture the context.

---

## H. Dapper / SQL Server / data layer

### 75. Multi-statement writes without a transaction
❌ Two `ExecuteAsync` calls, no transaction. ✅ Wrap in one transaction (this app does for create/update/delete).
🎯 A crash between statements leaves half-written, inconsistent data. 📏 Related writes are atomic.

### 76. No optimistic concurrency
❌ Blind `UPDATE ... WHERE Id=@id`. ✅ `rowversion`/timestamp check; return 409 on `affected == 0`.
🎯 Two admins edit the same row; the second silently overwrites the first (lost update). 📏 Guard updates with a version.

### 77. Reading then writing without a lock (race)
❌ `SELECT slot; compute; UPDATE` under contention. ✅ `UPDLOCK, HOLDLOCK` / `SERIALIZABLE` / a unique constraint.
🎯 Two concurrent requests both pass the check and both write, violating an invariant. 📏 Serialize check-then-act on shared rows.

### 78. Trusting DB unique/FK errors to never happen
❌ No handling for 2601/2627/547. ✅ Map to 409/400 with a clean message.
🎯 A duplicate key surfaces as a raw 500 leaking SQL detail. 📏 Translate known SQL errors to HTTP semantics.

### 79. `SELECT *` / projecting sensitive columns
❌ `SELECT *` pulls `PasswordHash`. ✅ Explicit column lists; never select secrets (this app omits `PasswordHash`).
🎯 A hash/secret rides along into a DTO/log/audit. 📏 Project only the columns you mean to expose.

### 80. Unbounded list endpoints (no paging)
❌ Return every row. ✅ `OFFSET/FETCH` or `TOP (@max)` + total count.
🎯 A grown table returns millions of rows, OOMs the API, and stalls the UI. 📏 Page every list; cap the max.

### 81. `TrustServerCertificate=True` / `Encrypt=False` to a remote DB
❌ Ship dev connection settings to prod. ✅ Encrypt + validate the cert for non-local DBs.
🎯 On-path attacker reads/tampers DB traffic (safe only for same-host SQLEXPRESS). 📏 Encrypt + verify certs off-box.

### 82. Over-privileged DB account
❌ App connects as `sa`/db_owner. ✅ Least-privilege login (CRUD on needed tables only).
🎯 One injection or bug becomes full server compromise instead of scoped damage. 📏 The app's DB user has the minimum rights.

---

## I. Angular / frontend / TypeScript

### 83. Secrets in the frontend bundle
❌ API keys/secrets in `environment.ts`. ✅ Keep secrets server-side; the SPA only holds public config.
🎯 Anyone opens DevTools / reads the JS bundle and takes the key. 📏 Nothing secret ships to the browser.

### 84. Attaching the bearer token to cross-origin requests
❌ Interceptor adds `Authorization` to every URL. ✅ Same-origin/allow-listed API only (this app scopes it).
🎯 A request to a third-party URL leaks your token to them. 📏 Send credentials only to your own API.

### 85. `any` erasing type safety
❌ `data: any` through the app. ✅ Real interfaces/DTOs.
🎯 A backend shape change compiles fine and blows up at runtime in users' faces. 📏 Type your API contracts.

### 86. Non-null assertion (`!`) hiding real nulls
❌ `this.user!.name`. ✅ Guard, optional chaining, or handle the null.
🎯 A genuinely-absent value throws `undefined is not an object` in production. 📏 `!` is a promise you must actually keep.

### 87. Subscriptions that never unsubscribe
❌ `.subscribe(...)` in a component with no teardown. ✅ `takeUntilDestroyed` / async pipe / `DestroyRef`.
🎯 Memory leaks + duplicate handlers firing after navigation. 📏 Every manual subscription needs a teardown.

### 88. Business rules enforced only in the UI
❌ "Disabled" button as the only guard. ✅ The API rejects the invalid operation too.
🎯 A crafted request ignores the disabled state. 📏 The UI can't enforce anything; the server must.

### 89. Trusting server data as safe HTML
❌ Render backend strings via `[innerHTML]` assuming they're clean. ✅ Escape by default; sanitize if HTML is required.
🎯 Stored XSS from data an *earlier* endpoint accepted unsanitized. 📏 Treat all data as untrusted at the sink.

### 90. Building URLs by string concat (frontend)
❌ `` `/api/x/${id}` `` with unencoded `id`. ✅ `encodeURIComponent` / `HttpParams` (this app encodes route ids).
🎯 Special characters break routing or enable parameter smuggling. 📏 Encode every dynamic URL segment.

### 91. Leaking data via verbose client logging
❌ `console.log(response)` with tokens/PII left in prod. ✅ Strip debug logging from production builds.
🎯 Sensitive data sits in the browser console and error trackers. 📏 No sensitive `console.*` in production.

### 92. Storing derived auth state you can forge client-side
❌ `isAdmin` flag in a store the user can edit. ✅ Derive UI from the token; the server re-checks every call.
🎯 User flips the flag in memory and unlocks admin UI (still blocked server-side — but don't rely on that alone). 📏 Client auth state is a hint, not a gate.

---

## J. Config, deployment, infra & supply chain

### 93. Debug/verbose errors enabled in production
❌ Developer exception page / Swagger open in prod. ✅ Dev-only (this app gates Swagger on `IsDevelopment()`).
🎯 The full API surface + stack traces are handed to anonymous callers. 📏 Debug tooling stays in dev.

### 94. Wildcard CORS with credentials
❌ `AllowAnyOrigin()` + `AllowCredentials()`. ✅ Explicit origin allow-list (this app restricts to loopback).
🎯 Any site makes authenticated calls as your users. 📏 Never pair wildcard origins with credentials.

### 95. No TLS / HSTS in production
❌ Serve the API over HTTP. ✅ `UseHttpsRedirection` + `UseHsts` outside dev (this app does).
🎯 Tokens/passwords travel cleartext; on-path attackers read them. 📏 Enforce HTTPS everywhere but local dev.

### 96. Missing security headers / CSP
❌ No `Content-Security-Policy`, `X-Content-Type-Options`, etc. ✅ Add them at the edge/app.
🎯 XSS payloads run freely; MIME-sniffing turns an upload into script. 📏 Ship a baseline security-header set.

### 97. Unpinned / untrusted CI actions & dependencies
❌ `uses: some/action@main`, floating dep ranges. ✅ SHA-pin actions; pin + audit dependencies; commit the lockfile.
🎯 A compromised upstream tag runs in your pipeline with your secrets. 📏 Pin what runs; audit what you pull.

### 98. Secrets as plaintext env in CI logs / images
❌ `echo $SECRET`, baking secrets into a Docker layer. ✅ Masked secret stores; runtime injection; no secrets in images.
🎯 Secrets leak in build logs or `docker history`. 📏 Secrets are injected at runtime, never logged or layered.

### 99. Containers running as root / `latest` base images
❌ No `USER`, `FROM node:latest`. ✅ Non-root `USER`; pinned, patched base image digests.
🎯 A container escape lands as root; `latest` silently pulls a vulnerable/backdoored image. 📏 Least-privilege + pinned bases.

### 100. `.env` / config with real credentials tracked in git
❌ Commit `.env` / prod `appsettings` with secrets. ✅ Gitignore them; use a secret store (this app uses DB `SysConfig` + Windows auth, no committed secrets).
🎯 Clone the repo → own the credentials, forever, across history. 📏 Never commit real secrets; ignore secret files.

---

## Using this list

- Skim the category that matches what you're about to write **before** you write it.
- In review, the 📏 lines double as a checklist — a "no" on any is a finding.
- Found a new one the hard way? Add it here; if it's sharp enough to teach from, give it a full
  before/after file like [01](./01-password-hashing-unsalted-sha256.md) and link it from
  [README.md](./README.md).
