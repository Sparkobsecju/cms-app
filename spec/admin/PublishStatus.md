# Build Spec for PublishStatus
- database schema: `.\database\admin.sql`

---

## Summary

`PublishStatus` is a small reference/lookup table describing the publishing lifecycle
state of content (draft / published / discontinued). It is a **flag-carrying lookup
table**: each row has a display `Description` plus three `bit` flags. It has **no foreign
keys** and **no N-N relationships**. It **is** referenced as a FK target by `Course`
(`course.sql`) and `Promotion2` (`promotion.sql`) via their `PublishStatus_pkid` columns.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` **tinyint, NOT IDENTITY** — manually assigned (`byte`), immutable on update |
| Foreign Keys | None |
| Required Fields | `Description`, `IsDraft`, `IsPublished`, `IsDiscontinued` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course.PublishStatus_pkid`, `Promotion2.PublishStatus_pkid` (target features not yet built — documented only) |
| Query Filters | keyword (`Description`), `IsDraft`, `IsPublished`, `IsDiscontinued` (tri-state bool) |
| Default Sort | `pkid ASC` |

> **Key deviation from the AppRole reference:** `pkid` is a **caller-supplied numeric PK**,
> not an IDENTITY. So it behaves like AppRole's string `RoleId`: present in the Request,
> existence-checked on create (409 on conflict), immutable/disabled on edit — but typed
> `byte` instead of `string`. INSERT sets `pkid` explicitly (no `SCOPE_IDENTITY()`).

---

## Localization

### Chinese Table Name

- PublishStatus: 發布狀態
- Description: 內容發布狀態代碼表（草稿／已發布／已停用）

### Chinese Column Names

- pkid: 主代碼
- Description: 狀態說明
- IsDraft: 草稿
- IsPublished: 已發布
- IsDiscontinued: 已停用

---

## Required Fields

Required (NOT NULL):
- `pkid` (caller-supplied numeric PK — required on create, disabled on edit)
- `Description`
- `IsDraft`
- `IsPublished`
- `IsDiscontinued`

Optional (nullable): none.

---

## Foreign Keys

`PublishStatus` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`PublishStatus` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

The following tables reference `PublishStatus.pkid` as a FK target:

- **Course** (`Course.PublishStatus_pkid` → `PublishStatus.pkid`) — `FK_Course_PublishStatus`
- **Promotion2** (`Promotion2.PublishStatus_pkid` → `PublishStatus.pkid`) — `FK_Promotion2_PublishStatus`

Navigation links to child list pages are **deferred**: the `courses` and `promotions`
frontend features do not exist yet in this lab (only `app-roles` is built). These links
are documented here so they can be wired when those features land. No child-navigation
buttons are added to the PublishStatus detail/list pages in this pass.

---

## N-N Relationships

**N/A** — no junction tables reference `PublishStatus`.

---

## Query Filters

`POST /api/publishstatuses/query` accepts:

- **keyword**: string — LIKE on `Description`.
- **isDraft**: bool? — exact match on `IsDraft` (tri-state: null = no filter).
- **isPublished**: bool? — exact match on `IsPublished` (tri-state).
- **isDiscontinued**: bool? — exact match on `IsDiscontinued` (tri-state).

---

## Lookup Endpoints Required

This feature needs **no** lookup endpoints of its own (no FKs). It **provides** a new
lookup that future `Course` / `Promotion2` forms will consume:

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/publish-statuses` | **New** | Slim `{ pkid, description }` list, ordered by `pkid ASC` |

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/publishstatuses` | List all (ordered `pkid ASC`) |
| `POST` | `/api/publishstatuses/query` | Filtered query (body: `PublishStatusQuery`) |
| `GET` | `/api/publishstatuses/{id:int}` | Get by pkid (`byte`) |
| `POST` | `/api/publishstatuses` | Create — 409 if `pkid` already exists |
| `PUT` | `/api/publishstatuses` | Update (pkid from body; immutable) |
| `DELETE` | `/api/publishstatuses/{id:int}` | Delete |
| `GET` | `/api/lookups/publish-statuses` | Slim lookup list |

No auth exceptions.

---

## Backend Notes

### Models

```csharp
// PublishStatus.cs
public class PublishStatus
{
    public byte Pkid { get; set; }                 // tinyint PK (caller-supplied)
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// PublishStatusRequest.cs
public class PublishStatusRequest
{
    public byte Pkid { get; set; }                 // required on create, immutable on update
    public string Description { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsPublished { get; set; }
    public bool IsDiscontinued { get; set; }
}

// PublishStatusQuery.cs
public class PublishStatusQuery
{
    public string? Keyword { get; set; }
    public bool? IsDraft { get; set; }
    public bool? IsPublished { get; set; }
    public bool? IsDiscontinued { get; set; }
}

// Models/Lookups/PublishStatusLookup.cs
public class PublishStatusLookup
{
    public byte Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}
```

### SQL — SELECT

```sql
SELECT s.pkid AS Pkid, s.Description AS Description,
       s.IsDraft AS IsDraft, s.IsPublished AS IsPublished, s.IsDiscontinued AS IsDiscontinued
FROM PublishStatus s
ORDER BY s.pkid ASC;
```

Query adds:
```sql
WHERE (@Keyword IS NULL OR s.Description LIKE '%' + @Keyword + '%')
  AND (@IsDraft IS NULL OR s.IsDraft = @IsDraft)
  AND (@IsPublished IS NULL OR s.IsPublished = @IsPublished)
  AND (@IsDiscontinued IS NULL OR s.IsDiscontinued = @IsDiscontinued)
```

### SQL — INSERT

`pkid` **is** written (caller-supplied). No `SCOPE_IDENTITY()`.
```sql
INSERT INTO PublishStatus (pkid, Description, IsDraft, IsPublished, IsDiscontinued)
VALUES (@Pkid, @Description, @IsDraft, @IsPublished, @IsDiscontinued);
```

### SQL — UPDATE

`pkid` is the immutable key (WHERE clause only).
```sql
UPDATE PublishStatus
SET Description = @Description, IsDraft = @IsDraft,
    IsPublished = @IsPublished, IsDiscontinued = @IsDiscontinued
WHERE pkid = @Pkid;
```

### Repository shape (mirrors `AppRoleRepository`)

- `GetAllAsync`, `QueryAsync(PublishStatusQuery)`, `GetByIdAsync(byte)`,
  `ExistsAsync(byte)`, `CreateAsync(request) → byte`, `UpdateAsync(request) → bool`,
  `DeleteAsync(byte) → bool`.
- No transaction/junction sync needed (no N-N), but keep the same async +
  `CancellationToken` + `IDbConnectionFactory` style.

### Special Column Notes

- `pkid` is `tinyint` → C# `byte`. **Not IDENTITY** — supply on INSERT, immutable on UPDATE.
- No `nchar`, `date`, or `time` columns → no `RTRIM()` / type-handler concerns.

---

## Frontend Notes

### Files

- `core/models/publish-status.model.ts` — `PublishStatus`, `PublishStatusRequest`,
  `PublishStatusQuery`, `PublishStatusLookup`.
- `core/services/publish-status.service.ts` — standard CRUD; numeric PK (no `encodeURIComponent`).
- `core/services/lookup.service.ts` — add `publishStatuses()` → `GET /api/lookups/publish-statuses`.
- `features/publish-statuses/publish-status-list|-detail|-form/`.
- Route path base: `/publish-statuses`. Sidebar under **系統管理 Admin**.

### List page

- Sortable/paginated `p-table`; columns: 主代碼, 狀態說明, 草稿, 已發布, 已停用, 操作.
- Bool columns rendered with a check/`—` (or PrimeNG tag).
- Filter drawer: keyword input + three tri-state selects (是／否／不限) for the bit flags.
- Session keys: `publish-status-list-filters`, `publish-status-list-sort`, `publish-status-list-page`.
- Default sort: `{ sortField: 'pkid', sortOrder: 1 }`.

### Detail page

- Read-only `dl` of all five fields; bools shown as 是／否.

### Form page

- Reactive Forms. **No `forkJoin` lookups** (no FKs/N-N).
- `pkid`: `p-inputNumber`, required, **disabled in edit mode** (immutable PK) — mirrors
  AppRole's `roleId.disable()`.
- `description`: `pInputText`, required.
- `isDraft` / `isPublished` / `isDiscontinued`: `p-checkbox` (binary) or `p-inputSwitch`.
- On create-conflict (`409`) show 「主代碼已存在」.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

### Sidebar placement

Add under the existing **系統管理 Admin** group in `app.ts` / `app.html`:
`{ label: '發布狀態 PublishStatus', icon: 'pi pi-flag', route: '/publish-statuses' }`.

### Sub-panels

**N/A**

---

## Files to Create / Modify

### Backend (`CMS.API`)
| File | Action |
|------|--------|
| `Models/PublishStatus.cs` | create |
| `Models/PublishStatusRequest.cs` | create |
| `Models/PublishStatusQuery.cs` | create |
| `Models/Lookups/PublishStatusLookup.cs` | create |
| `Repositories/IPublishStatusRepository.cs` | create |
| `Repositories/PublishStatusRepository.cs` | create |
| `Controllers/PublishStatusesController.cs` | create |
| `Repositories/ILookupRepository.cs` | modify (add `GetPublishStatusesAsync`) |
| `Repositories/LookupRepository.cs` | modify |
| `Controllers/LookupsController.cs` | modify (add `publish-statuses`) |
| `Program.cs` | modify (register `IPublishStatusRepository`) |

### Frontend (`CMS.NG`)
| File | Action |
|------|--------|
| `core/models/publish-status.model.ts` | create |
| `core/services/publish-status.service.ts` | create |
| `core/services/lookup.service.ts` | modify |
| `features/publish-statuses/publish-status-list/*` | create (ts/html/scss) |
| `features/publish-statuses/publish-status-detail/*` | create (ts/html/scss) |
| `features/publish-statuses/publish-status-form/*` | create (ts/html/scss) |
| `app.routes.ts` | modify (4 routes; `/new` before `/:id`) |
| `app.ts` / `app.html` | modify (sidebar entry) |

### Tests
| File | Action |
|------|--------|
| `CMS.API.Tests/PublishStatusesControllerTests.cs` | create |
| `CMS.API.Tests/LookupsControllerTests.cs` | modify (add publish-statuses test) |
| `publish-status.service.spec.ts` | create |
| `publish-status-list.spec.ts` | create |
| `publish-status-detail.spec.ts` | create |
| `publish-status-form.spec.ts` | create |
