# Build Spec for Auth (Login)

- database schema: `.\database\auth.sql` (reads `AppUser`, `AppUserRole`, `SysConfig`)

## Summary

`Auth` is the **login** feature. It exposes a single stateless endpoint that verifies a
`UserId` + `password` against the `AppUser` table and, on success, returns a user profile
carrying a signed **JWT access token** (24-hour lifetime). It owns no table of its own — it
**reads** `AppUser` (credentials + active flag), `AppUserRole` (roles → token claims), and
`SysConfig` (the JWT signing secret). It is the counterpart to the **AppUser** CRUD feature:
AppUser *writes* `PasswordHash` (server-side, from the default password); Auth *verifies* it.

`PasswordHash` is **backend-only** — never accepted from nor returned to the client. The
plaintext password is only ever hashed and compared server-side; it is never stored, logged,
or echoed back. On any failure the endpoint returns a single generic `401` that does not reveal
which check failed.

| Item | Detail |
|------|--------|
| Endpoint | `POST /api/Auth/login` |
| Request | `{ userId, password }` (`LoginRequest`) |
| Success | `200` `{ userId, userName, accessToken }` (`LoginResponse`) |
| Failure | `401` `{ message: "Invalid credentials." }` (`ErrorResponse`) — generic, non-revealing |
| Reads | `AppUser` (credential check), `AppUserRole` (role claims), `SysConfig['appConfig']` (signing key) |
| Writes | None (no rows created/updated; **not** row-audited) |
| Token | JWT, HMAC-SHA256, 24h lifetime; claims `UserId`, `UserName`, `sub`, one role claim per `RoleId` |

---

## Credential Check (against `AppUser`, via Dapper)

All three checks live in **one `WHERE` clause** so a failure of any single one returns no row —
the caller cannot tell which part failed:

- **UserId**: match the supplied `userId` exactly.
- **IsActive**: must be `1`.
- **PasswordHash**: must equal `SHA256(supplied password)`, encoded as **lowercase hex** —
  `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant()`
  (64-char hex; the **same scheme** `AppUserRepository` uses when it *writes* `PasswordHash`).

```sql
SELECT UserId AS UserId, UserName AS UserName
FROM AppUser
WHERE UserId = @UserId AND IsActive = 1 AND PasswordHash = @PasswordHash;
```

`PasswordHash` is passed only as a parameter — **never selected** into any projection. On a
match, load the user's roles:

```sql
SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId;
```

If no row matches (unknown UserId, inactive, or wrong password) → return `null` → controller
answers `401`. Missing/blank `userId` or `password` in the request also short-circuits to `401`
**without** touching the DB.

---

## JWT Access Token

Built by `JwtTokenService` (`Services/JwtTokenService.cs`, `IJwtTokenService`):

- **Signing secret**: the `symmetricSecurityKey` **property of the JSON** stored in
  `SysConfig` where `configKey = 'appConfig'`. Read at **runtime** on every login (via
  `IAuthRepository.GetSigningSecretAsync`) — **never hard-coded**. Missing row / JSON property /
  empty value → `InvalidOperationException` → surfaces as `500` through the existing
  `ExceptionHandlingMiddleware`.
  - ⚠️ Must be **≥ 32 bytes (256 bits)** or HMAC-SHA256 signing throws.
  - Mirrors `AppUserRepository.ReadDefaultPasswordHashAsync`'s SysConfig read (same row,
    sibling property `defaultPassword`).
- **Claims**:
  - `UserId` (constant `JwtTokenService.UserIdClaimType`) = `AppUser.UserId`
  - `UserName` (constant `JwtTokenService.UserNameClaimType`) = `AppUser.UserName` — **same claim
    name `RowAuditWriter` reads** to attribute audit rows once tokens are validated.
  - `sub` (`JwtRegisteredClaimNames.Sub`) = `UserId`
  - one role claim per `RoleId`, type `ClaimTypes.Role` (constant `JwtTokenService.RoleClaimType`)
    — the .NET default so `[Authorize(Roles=…)]` works once validation is wired up.
- **Lifetime**: `JwtTokenService.TokenLifetime = 24h`; the token expires 24h after issue
  (`notBefore = UtcNow`, `expires = UtcNow + 24h`).
- **Claim-name fidelity**: the handler's **per-instance** `OutboundClaimTypeMap` is `Clear()`ed
  before `WriteToken`, so claim types are emitted verbatim (e.g. the role URI is not shortened).
  No global/static state is mutated.

`CreateAccessToken(userId, userName, roles, signingSecret)` returns
`AccessToken(string Token, DateTime ExpiresAtUtc)`.

---

## API Endpoint

| Method | Route | Notes |
|--------|-------|-------|
| `POST` | `/api/Auth/login` | Body `LoginRequest`; `200` `LoginResponse` on success, `401` `ErrorResponse` otherwise |

Route comes from `[Route("api/[controller]")]` on `AuthController` (controller name `Auth`
→ `api/Auth`; routing is case-insensitive). No auth required to reach *login* (it *issues* tokens);
`[AllowAnonymous]` sits on the `login` action only, so every other action requires a token.

---

## My Profile (self-service UserName update)

Lets a signed-in user update **only their own** display name.

| Method | Route | Notes |
|--------|-------|-------|
| `PUT` | `/api/Auth/profile` | `[Authorize]`; body `UpdateProfileRequest`; `200` `ProfileResponse`, `400` on blank name, `401` without a token |

- **Identity from the token, never the body**: the target `UserId` is read from the JWT via
  `User.FindFirstValue(JwtTokenService.UserIdClaimType)`. `UpdateProfileRequest` carries **only**
  `UserName` (no `UserId` property), so a client can neither rename another account nor change roles.
- **Validation**: `UserName` is trimmed; blank/whitespace → `400` `ErrorResponse("UserName is
  required.")` **without** hitting the repo.
- **Repo**: `IAuthRepository.UpdateUserNameAsync(userId, userName, ct)` → `UPDATE AppUser SET
  UserName = @UserName WHERE UserId = @UserId`, returns `affected > 0` (`false` → controller `404`).
  Touches only `UserName`; `UserId`, roles, `IsActive`, `PasswordHash` untouched.
- **Not row-audited** — a self-service action endpoint (like `reset-password`), consistent with the
  rest of `AuthController`/`AuthRepository` staying audit- and dependency-light.
- **Models**: `Models/UpdateProfileRequest.cs` (`{ UserName }`), `Models/ProfileResponse.cs`
  (`{ UserId, UserName }` — no token, no secrets).
- **Note**: the JWT still carries the pre-change `UserName` until the next login, so `RowAudit`
  attribution uses the old name until the token refreshes.
- **Frontend**: `features/profile/` page (`/profile`, guarded) shows `UserId` + roles read-only and
  an editable name; **Save** calls `AuthService.updateUserName(name)`, which `PUT`s `{ userName }`
  and on success merges the returned name into the session profile (refreshes the shell `userName`
  signal + session storage, **keeps the token**). Linked as 個人資料 My Profile in the sidebar footer.
- **Tests**: `AuthControllerTests` (unit — updates for the JWT user + trims; targets the JWT user
  not a body id; blank → `400` without the repo). `AuthorizationTests` (WebApplicationFactory —
  a body-supplied `userId` is ignored end-to-end, update targets the token subject; `401` without a
  token). Angular `profile.spec.ts`, `auth.service.spec.ts` (`updateUserName`), `app.spec.ts` (link).

---

## Change Password (self-service)

Lets a signed-in user change **their own** password. Plaintext only crosses the wire; **no password
hash is ever accepted from or returned to the client**.

| Method | Route | Notes |
|--------|-------|-------|
| `POST` | `/api/Auth/change-password` | `[Authorize]`; body `ChangePasswordRequest`; `204` on success, `400` `ErrorResponse` on any validation failure, `401` without a token |

- **Identity from the token, never the body**: the target `UserId` is read from the JWT via
  `User.FindFirstValue(JwtTokenService.UserIdClaimType)`. `ChangePasswordRequest` carries only
  `CurrentPassword` / `NewPassword` / `ConfirmPassword` (no `UserId`), so a client can only ever change
  their own password.
- **Order of checks** (any failure returns `400` and changes nothing):
  1. **Current password** — `IAuthRepository.VerifyCurrentPasswordAsync` compares
     `SHA256(currentPassword)` against the stored `PasswordHash` **in the SQL `WHERE`** (the hash is a
     parameter, never selected — the stored hash never leaves the repo). Mismatch →
     `400 "目前密碼不正確。 Current password is incorrect."`.
  2. **Complexity** — `Security/PasswordPolicy.IsCompliant`: length ≥ 8 **and** ≥ 3 of the 4 classes
     (uppercase / lowercase / digit / symbol; a "symbol" is any non-letter, non-digit char). Fail →
     `400` with the exact bilingual `PasswordPolicy.Requirement` message.
  3. **Match** — `NewPassword` must equal `ConfirmPassword` (ordinal). Fail →
     `400 "兩次輸入的新密碼不一致。 New password and confirmation do not match."`.
- **Persist** — `IAuthRepository.ChangePasswordAsync` runs a single `UPDATE AppUser SET PasswordHash =
  @Hash, PasswordUpdatedTime = GETDATE() WHERE UserId = @UserId` where `@Hash = SHA256(newPassword)` hex
  (hashed inside the repo). Mirrors `AppUserRepository.ResetPasswordAsync`. Returns `false` (→ `404`) if
  no such user. **Not row-audited** — a self-service action like the UserName update.
- **Hashing** — the SHA-256-hex scheme lives in one place, `Security/PasswordHasher.Hash`, reused by
  `AuthRepository` (verify + change) so a computed hash always compares equal to a stored one.
- **Models**: `Models/ChangePasswordRequest.cs` (`{ CurrentPassword, NewPassword, ConfirmPassword }` —
  no hash, no `UserId`). Success returns `204 No Content` (no body → nothing to leak).
- **Frontend**: a **Change Password** card (`features/profile/change-password/`) on the `/profile` page.
  A reactive form (current / new / confirm) mirrors the server policy client-side —
  `passwordComplexityValidator` (length ≥ 8 + ≥ 3 classes, showing the same bilingual message) and a
  group-level `passwordsMatchValidator`; the button stays disabled-in-effect until valid.
  `AuthService.changePassword` posts the three plaintext fields and **keeps the session/token untouched**
  on success (a success toast; the form resets). A server `400` message is surfaced inline. The server
  remains authoritative — client validation is only a convenience.
- **Tests**: `AuthControllerTests` (unit — valid change persists for the JWT user and returns `204`;
  wrong current password changes nothing; complexity rejects length < 8 and < 3 classes and accepts the
  3-of-4 boundary; new/confirm mismatch rejected; `PasswordHasher.Hash` == SHA-256 hex of the new
  password). `AuthorizationTests` (WebApplicationFactory — `401` without a token; a valid request targets
  the token subject end-to-end). Angular `change-password.spec.ts` (the two validators + form validity +
  submit gating + inline server error) and `auth.service.spec.ts` (`changePassword` sends only plaintext,
  keeps the token).

---

## Backend Notes

### Models

```csharp
public sealed class LoginRequest        // request — plaintext password, hashed server-side, never stored
{
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse       // success — NO PasswordHash, NO secrets
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;   // the signed JWT
}

public sealed class AuthenticatedUser   // backend-only credential-check result — NO PasswordHash
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public List<string> RoleIds { get; set; } = [];
}
```

Failure body reuses the existing `ErrorResponse(string Message)` record.

### Components

- `Controllers/AuthController.cs` — orchestrates: guard blank input → `ValidateCredentialsAsync`
  → (on success) `GetSigningSecretAsync` → `CreateAccessToken` → `LoginResponse`. The generic
  `401` message is a single shared `static readonly ErrorResponse("Invalid credentials.")`.
- `Repositories/IAuthRepository.cs` + `AuthRepository.cs` — Dapper credential check + signing-key
  read (no writes, no audit). Injects `IDbConnectionFactory` only.
- `Services/IJwtTokenService.cs` + `JwtTokenService.cs` — token minting (stateless).

### DI (`Program.cs`)

```csharp
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();   // stateless
```

### Package

`CMS.API.csproj` references `System.IdentityModel.Tokens.Jwt` **8.16.0** — pinned to match the
version already pulled in transitively by `Microsoft.Data.SqlClient` (a lower version triggers
an `NU1605` downgrade error).

### Cross-cutting

- **Not row-audited** — Auth performs no Insert/Update/Delete (consistent with the convention
  that action-style endpoints aren't audited).
- **Exception handling** — relies on the global `ExceptionHandlingMiddleware`; no hand-rolled
  try/catch. A misconfigured `SysConfig` throws and returns the safe generic `500`.

---

## Security Rules (must hold)

1. `PasswordHash` never appears in `LoginResponse`, `AuthenticatedUser`, any SELECT projection,
   the JWT payload, or logs.
2. The plaintext password is only hashed for the `WHERE` comparison — never persisted or returned.
3. Every failure mode (blank input, unknown UserId, `IsActive = 0`, wrong password) returns the
   **same** generic `401` message; the signing secret is **not** read on a failed login.
4. The signing secret is read from `SysConfig` at runtime — never hard-coded or cached in config.

---

## Tests

`CMS.API.Tests/AuthControllerTests.cs` — mocks `IAuthRepository` (strict) but uses the **real**
`JwtTokenService`, so issued tokens are decoded (`JwtSecurityTokenHandler.ReadJwtToken`) and
asserted. A ≥32-byte stand-in `SigningSecret` constant is used for signing. Covers:

- Valid active user → `200`, correct `userId`/`userName`, a non-empty decodable JWT with
  `UserId`/`UserName` claims.
- Token carries **every** role claim, in order.
- Token `ValidTo` sits ~24h after issue (asserted within a ±30s window around the call).
- Response body **and** token payload contain no `password`/`hash` (serialized-string +
  claim-type checks).
- Wrong password, unknown UserId, `IsActive = 0`, and blank input each → generic `401`
  `"Invalid credentials."`; the wrong-password and blank-input cases verify the signing secret /
  repository are **not** hit.

Run: `dotnet test src/CMS.API.Tests/CMS.API.Tests.csproj` (no DB required).

---

## Files Created / Modified

**Created (CMS.API)**: `Models/LoginRequest.cs`, `Models/LoginResponse.cs`,
`Models/AuthenticatedUser.cs`, `Models/UpdateProfileRequest.cs`, `Models/ProfileResponse.cs`,
`Models/ChangePasswordRequest.cs`, `Repositories/IAuthRepository.cs`, `Repositories/AuthRepository.cs`,
`Services/IJwtTokenService.cs`, `Services/JwtTokenService.cs`, `Controllers/AuthController.cs`,
`Security/PasswordPolicy.cs`, `Security/PasswordHasher.cs`.

**Created (CMS.NG)**: `features/profile/profile.{ts,html,scss,spec.ts}`,
`features/profile/change-password/change-password.{ts,html,scss,spec.ts}`.

**Modified (CMS.API)**: `Program.cs` (DI for `IAuthRepository` + `IJwtTokenService`),
`CMS.API.csproj` (`System.IdentityModel.Tokens.Jwt` 8.16.0). `AuthController` later gained the
`[Authorize]` `PUT /profile` action (and `[AllowAnonymous]` moved onto the `login` action).

**Modified (CMS.NG)**: `core/services/auth.service.ts` (`updateUserName`), `app.routes.ts`
(`/profile` route), `app.html` + `app.scss` (My Profile footer link).

**Tests**: `CMS.API.Tests/AuthControllerTests.cs`, `CMS.API.Tests/AuthorizationTests.cs`;
`CMS.NG` `profile.spec.ts`, `auth.service.spec.ts`, `app.spec.ts`.

---

## Authorization (end-to-end, built)

- **Token validation** — `AddAuthentication().AddJwtBearer()` configured via
  `Security/ConfigureJwtBearerOptions` (an `IConfigureNamedOptions<JwtBearerOptions>`). The
  validation key is resolved at runtime from `ISigningKeyProvider`/`SigningKeyProvider`, which reads
  the **same** `SysConfig['appConfig'].symmetricSecurityKey` (cached after first read). Params:
  `ValidateIssuer/Audience = false`, `ValidateLifetime = true`, `MapInboundClaims = false`,
  `NameClaimType = "UserName"`, `RoleClaimType = ClaimTypes.Role`.
- **Global policy** — `AddAuthorization` sets a **fallback policy** of `RequireAuthenticatedUser()`,
  so every endpoint needs a valid bearer token except the `[AllowAnonymous]` **`login` action** (the
  attribute sits on the action, not the controller, so every other `AuthController` action — e.g.
  `profile` below — still requires a token). `app.UseAuthentication()` precedes `app.UseAuthorization()`. Tests:
  `AuthorizationTests` (WebApplicationFactory) — protected endpoint 401 without / 401 invalid / 200
  with a valid token; `/api/Auth/login` reachable anonymously.
- **Frontend** — real `/login` page posts to `POST /api/Auth/login`; `AuthService` stores the
  `{ userId, userName, accessToken }` profile in **session** storage and exposes roles decoded from
  the token; `authInterceptor` attaches `Authorization: Bearer`; `authGuard` (on all routes but
  `/login`) redirects to `/login` when signed out; a 401 clears the session and redirects (error
  interceptor); the shell shows the signed-in name + logout and gates the 系統管理 Admin menu on the
  `Admin` role. Specs cover each of these.
