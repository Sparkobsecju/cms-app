# Build Spec for AppUser
- database schema: `.\database\auth.sql`

## Summary

`AppUser` is a system login account. It has a string business key (`UserId`), a display
name, an active flag, and a password stored only as a hash. It has an N-N relationship
with `AppRole` via the `AppUserRole` junction (a user holds many roles). `PasswordHash`
is **backend-only** — it is never accepted from nor returned to the client; it is set from
a configured default on create and only changed through a dedicated reset-password endpoint.
This entity mirrors the **AppRole** reference feature (string PK + N-N), inverted across the
same junction table.

| Item | Detail |
|------|--------|
| Primary Key | `UserId` nvarchar(200) (business PK); `pkid` int IDENTITY is display-only (主代碼) |
| Foreign Keys | None on the table itself |
| Required Fields | `UserId`, `UserName`, `IsActive` (`PasswordHash` is required in DB but set server-side, never by the client) |
| N-N Relationships | `AppUserRole` — AppUser ↔ AppRole (keys `UserId`, `RoleId`) |
| Primary-Foreign Links | N/A — the only referencing table is the `AppUserRole` junction, managed as N-N |
| Query Filters | keyword (UserId, UserName); IsActive (tri-state bool) |
| Default Sort | `UserId ASC` |

---

## Localization

### Chinese Table Name

- AppUser: 使用者
- Description: 系統登入帳號主資料

### Chinese Column Names

- pkid: 主代碼
- UserId: 帳號
- UserName: 使用者名稱
- IsActive: 啟用狀態
- PasswordHash: 密碼雜湊（後端專用，不顯示）
- PasswordUpdatedTime: 密碼更新時間
- (N-N) Roles: 角色

---

## Required Fields

Required (NOT NULL, client-supplied):
- `UserId` NOT NULL (business PK; immutable on update)
- `UserName` NOT NULL
- `IsActive` NOT NULL (DB default `1`)

Server-managed (NOT NULL in DB, never supplied by the client):
- `PasswordHash` — set on create from the configured default password; changed only via reset-password.

Optional (nullable):
- `PasswordUpdatedTime` — set by the server when the password is (re)set; display-only.

---

## Foreign Keys

`AppUser` has no foreign key columns. **N/A**

---

## Foreign-Primary Links

`AppUser` has no foreign key columns. **N/A**

---

## Primary-Foreign Links

The only table referencing `AppUser` is the `AppUserRole` junction (`UserId`), which is
managed inline through the N-N Relationships section. No separate child list-page links.

**N/A**

---

## N-N Relationships

### AppUser ↔ AppRole via `AppUserRole`

Junction table `AppUserRole` (`UserId` nvarchar(200), `RoleId` nvarchar(200)) — composite PK,
FKs to `AppUser.UserId` and `AppRole.RoleId`. **Keys are the string business keys, not pkids**
(same junction the AppRole feature writes from the other side).

- **List / Detail view**: show the associated roles. List shows a 角色數 (`RoleCount`) count
  column (subquery); Detail shows the role ids as chips (label enriched with `RoleName` from the
  `app-roles` lookup).
- **Form (edit + new)**: `p-multiselect` of roles; option label = `RoleName (RoleId)`,
  loaded from `GET /api/lookups/app-roles`.
- **Request field**: `RoleIds` — `List<string>` (the selected `RoleId` values).
- **Sync pattern on save** (create & update), inside the same transaction:
  1. `DELETE FROM AppUserRole WHERE UserId = @UserId`
  2. Bulk `INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId)` for each distinct role.
- **Read** (`GetByIdAsync`): second result set `SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId`.

---

## Query Filters

- **keyword**: string — LIKE on `UserId`, `UserName`.
- **IsActive**: bool? — tri-state. null = no filter, true = active only, false = inactive only.

No FK dropdown filters (no FK columns). Role-based filtering is intentionally out of scope to
mirror AppRole (which does not filter on its N-N side).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/app-roles` | **New** | `{ roleId, roleName }[]` ordered by `RoleName ASC` — for the roles multi-select |

(The existing `GET /api/lookups/app-users` is unrelated — it returns users for the AppRole form.)

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/appusers` | List all |
| `POST` | `/api/appusers/query` | Filtered query (body: `AppUserQuery`) |
| `GET` | `/api/appusers/{id}` | Get by UserId (string PK; includes `RoleIds`) |
| `POST` | `/api/appusers` | Create (hashes default password server-side) |
| `PUT` | `/api/appusers` | Update (UserId from body; never touches PasswordHash) |
| `DELETE` | `/api/appusers/{id}` | Delete (removes junction rows first) |
| `POST` | `/api/appusers/{id}/reset-password` | **Special** — resets password to the configured default; body empty |

- String PK: `{id}` route has **no** `:int` constraint.
- `409 Conflict` on create when `UserId` already exists; `404` on update/delete/reset when missing;
  `400` when `UserId`/`UserName` missing on create/update.

### Reset-password action

`POST /api/appusers/{id}/reset-password` — **no request body**. Re-reads the default password from
`SysConfig`, SHA-256 hashes it, writes `PasswordHash`, and sets `PasswordUpdatedTime = GETDATE()`.
Returns `204 No Content` (or `404` if the user does not exist). No password value ever crosses the
wire — this is the *only* path that mutates `PasswordHash` after creation.

---

## Backend Notes

### Models

```csharp
public class AppUser                    // response — NO PasswordHash
{
    public int Pkid { get; set; }                       // 主代碼, display-only
    public string UserId { get; set; } = string.Empty;  // business PK
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? PasswordUpdatedTime { get; set; }   // display-only
    public int RoleCount { get; set; }                   // subquery from AppUserRole
    public List<string> RoleIds { get; set; } = [];      // populated on GET by id
}

public class AppUserRequest             // write DTO — NO PasswordHash, NO PasswordUpdatedTime
{
    public string UserId { get; set; } = string.Empty;   // immutable on update
    public string UserName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<string> RoleIds { get; set; } = [];
}

public class AppUserQuery
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
}
```

### SQL — SELECT

Shared projection (`RoleCount` via junction subquery); `PasswordHash` is never selected:
```sql
SELECT u.pkid AS Pkid,
       u.UserId AS UserId,
       u.UserName AS UserName,
       u.IsActive AS IsActive,
       u.PasswordUpdatedTime AS PasswordUpdatedTime,
       (SELECT COUNT(*) FROM AppUserRole ur WHERE ur.UserId = u.UserId) AS RoleCount
FROM AppUser u
```
- `GetAllAsync`: `... ORDER BY u.UserId ASC`
- `QueryAsync`: `WHERE (@Keyword IS NULL OR u.UserId LIKE '%'+@Keyword+'%' OR u.UserName LIKE '%'+@Keyword+'%') AND (@IsActive IS NULL OR u.IsActive = @IsActive) ORDER BY u.UserId ASC`
- `GetByIdAsync`: projection filtered by `@UserId`, then `SELECT RoleId FROM AppUserRole WHERE UserId = @UserId ORDER BY RoleId` via `QueryMultiple`.

### SQL — INSERT

Writable columns: `UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime`.
```sql
INSERT INTO AppUser (UserId, UserName, IsActive, PasswordHash, PasswordUpdatedTime)
VALUES (@UserId, @UserName, @IsActive, @PasswordHash, GETDATE());
```
`@PasswordHash` = SHA-256 of the configured default password (see Password Handling). No IDENTITY
returned to the caller (business PK is `UserId`; the create response is fetched via `GetByIdAsync`).

### SQL — UPDATE

Writable columns only — **never** `PasswordHash`/`PasswordUpdatedTime`:
```sql
UPDATE AppUser SET UserName = @UserName, IsActive = @IsActive WHERE UserId = @UserId;
```

### N-N Sync Pattern

After INSERT/UPDATE, in the same transaction:
```sql
DELETE FROM AppUserRole WHERE UserId = @UserId;
-- then for each RoleId: INSERT INTO AppUserRole (UserId, RoleId) VALUES (@UserId, @RoleId);
```
DELETE also removes junction rows first (FK) before deleting the AppUser.

### Password Handling (special)

- **Never** exposed: `PasswordHash` is absent from `AppUser`, `AppUserRequest`, and all Angular models.
- **On create**: read `SysConfig.configValue WHERE configKey = 'appConfig'`; parse it as a JSON object;
  extract the `defaultPassword` string property; SHA-256 hash it; store as `PasswordHash`;
  set `PasswordUpdatedTime = GETDATE()`.
  - Hash: `Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(defaultPassword))).ToLowerInvariant()` (64-char hex).
  - If the `appConfig` row or the `defaultPassword` property is missing → throw `InvalidOperationException`
    (surfaces as `500`); a defensive controller check is not required.
- **On update**: `PasswordHash`/`PasswordUpdatedTime` are untouched.
- **On reset-password**: same default-password read + hash as create; update `PasswordHash` and
  `PasswordUpdatedTime = GETDATE()`.
- Helper: a private `ReadDefaultPasswordHashAsync(connection, transaction, ct)` on the repository
  centralizes the SysConfig read + hash (used by create and reset).

### Special Column Notes

- `UserId` string PK → controller route `{id}` (no `:int`); immutable on update; service `encodeURIComponent`.
- `PasswordUpdatedTime` is `datetime` → C# `DateTime?`. (No `DateOnly`/`TimeOnly` handlers needed.)
- No `nchar` columns → no `RTRIM()` needed.

---

## Frontend Notes

### Angular model (`app-user.model.ts`) — NO password fields

```ts
export interface AppUser {
  pkid: number;
  userId: string;
  userName: string;
  isActive: boolean;
  passwordUpdatedTime?: string | null;  // display-only ISO datetime
  roleCount: number;
  roleIds: string[];
}
export interface AppUserRequest {
  userId: string;
  userName: string;
  isActive: boolean;
  roleIds: string[];
}
export interface AppUserQuery {
  keyword?: string | null;
  isActive?: boolean | null;
}
export interface AppRoleLookup { roleId: string; roleName: string; }
```

### Service (`app-user.service.ts`)

- Base `${apiBaseUrl}/appusers`; `get(id)`/`delete(id)` use `encodeURIComponent` (string PK).
- Extra method `resetPassword(userId)` → `POST /appusers/{enc}/reset-password` with empty body.
- `LookupService.appRoles()` → `GET /lookups/app-roles`.

### List component

- Sortable/paginated `p-table`; columns: 主代碼(pkid), 帳號(userId), 使用者名稱(userName),
  啟用(isActive as tag/boolean), 角色數(roleCount), 密碼更新時間(passwordUpdatedTime, `+ 'Z'` before date pipe).
- Filter drawer (`p-drawer`): keyword input; IsActive tri-state (`p-select` 全部/啟用/停用, `appendTo="body"`).
- Session keys: `appuser-list-filters`, `appuser-list-sort`, `appuser-list-page`.
- Delete confirm: `確定要刪除主代碼 <b>${item.pkid}</b>「${item.userName}」？`

### Detail component

- Shows pkid, userId, userName, isActive, passwordUpdatedTime, and role chips
  (label `RoleName (RoleId)` resolved from the `app-roles` lookup).
- **重設密碼** button → confirm → `resetPassword(userId)` → toast on success. No password field shown.
- Never displays any password/hash value.

### Form component

- Reactive Forms; `forkJoin` loads the `app-roles` lookup (and the record in edit mode).
- Fields: `userId` (text; **disabled in edit**, read via `getRawValue()`), `userName` (text),
  `isActive` (checkbox/switch, default `true`), `roleIds` (`p-multiselect`, `[maxSelectedLabels]="9999"`).
- **No password field of any kind.**

### Sidebar

Add under existing 系統管理 Admin group, beneath 角色 AppRole: 使用者 AppUser → `/app-users`.

---

## Session Storage Keys

| Key | Contents |
|-----|----------|
| `appuser-list-filters` | Last query filter values |
| `appuser-list-sort` | `{ sortField, sortOrder }` |
| `appuser-list-page` | `{ first, rows }` |

---

## Files to Create / Modify

**Backend (CMS.API)** — create: `Models/AppUser.cs`, `AppUserRequest.cs`, `AppUserQuery.cs`,
`Models/Lookups/AppRoleLookup.cs`, `Repositories/IAppUserRepository.cs`, `AppUserRepository.cs`,
`Controllers/AppUsersController.cs`. Modify: `ILookupRepository.cs` + `LookupRepository.cs`
(add `GetAppRolesAsync`), `LookupsController.cs` (add `app-roles`), `Program.cs` (DI registration).

**Frontend (CMS.NG)** — create: `core/models/app-user.model.ts`, `core/services/app-user.service.ts`,
`features/app-users/app-user-list/`, `app-user-detail/`, `app-user-form/`. Modify:
`core/services/lookup.service.ts` (add `appRoles()`), `app.routes.ts`, `app.html` + `app.ts` (sidebar).

**Tests** — create: `CMS.API.Tests/AppUsersControllerTests.cs`; frontend
`app-user.service.spec.ts` + `*-list`/`*-detail`/`*-form` component specs.
