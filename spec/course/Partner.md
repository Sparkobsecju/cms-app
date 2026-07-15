# Build Spec for Partner
- database schema: `.\database\course.sql`

## Summary

`Partner` (原廠) is a small master/lookup entity holding the training partners (原廠 /
合作廠商) whose courses and certifications the site lists. It has a handful of display
columns (`Name`, `AppKey`, two menu/page display names, `DisplayOrder`) plus an optional
`ImageFilename`. It has no foreign keys of its own. It is referenced as a foreign-key
target by `Course` (`Partner_pkid`, NOT NULL), `Certification` (`Partner_pkid`, NOT NULL),
and `PartnerCourseGroup` (`Partner_pkid`, NOT NULL). It already **provides** the
`partners` lookup consumed by the `Course` feature.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` smallint IDENTITY |
| Foreign Keys | None |
| Required Fields | `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`, `DisplayOrder` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course`, `Certification`, and `PartnerCourseGroup` reference `Partner.pkid` |
| Query Filters | keyword (Name / AppKey / NameOnPartnerMenu / NameOnCourseDetailPage) |
| Default Sort | `DisplayOrder ASC, pkid ASC` |

---

## Localization

### Chinese Table Name

- Partner: 原廠
- Description: 原廠（合作廠商）主資料，課程與認證皆歸屬於某一原廠。

### Chinese Column Names

- pkid: 主代碼
- Name: 原廠名稱
- AppKey: 應用金鑰
- NameOnPartnerMenu: 選單顯示名稱
- NameOnCourseDetailPage: 課程頁顯示名稱
- DisplayOrder: 顯示順序
- ImageFilename: 圖片檔名

---

## Required Fields

Required (NOT NULL):
- `Name` — `nvarchar(50)`
- `AppKey` — `varchar(10)`
- `NameOnPartnerMenu` — `nvarchar(200)`
- `NameOnCourseDetailPage` — `nvarchar(50)`
- `DisplayOrder` — `int`

Optional (nullable):
- `ImageFilename` — `varchar(50)` NULL

---

## Foreign Keys

`Partner` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`Partner` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

The following tables reference `Partner.pkid` as a foreign-key target. Only the `Course`
child is scaffolded today, so it is the only wired link; the others are documented for
completeness and can be wired when those features exist.

- **Course** (`Partner_pkid`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link target: `/courses?partnerPkid={pkid}`
  - Query param name the Course list accepts: `partnerPkid`
  - Note: the Course list does not yet read `partnerPkid`; the link is defined here and
    degrades gracefully (Course list simply ignores the unknown param).

- **Certification** (`Partner_pkid`) — not yet scaffolded. Future link:
  `/certifications?partnerPkid={pkid}`.
- **PartnerCourseGroup** (`Partner_pkid`) — not yet scaffolded. Future link:
  `/partner-course-groups?partnerPkid={pkid}`.

---

## N-N Relationships

`Partner` participates in no pure junction tables. `PartnerCourseGroup` carries its own
identity PK plus extra columns (`CourseGroup_pkid`, `DisplayOrder`, `Description`), so it
is a first-class entity, not an N-N junction.

**N/A**

---

## Query Filters

- **keyword**: string
  - LIKE on `Name`, `AppKey`, `NameOnPartnerMenu`, `NameOnCourseDetailPage`
    (short identifying columns; no large text fields exist on this table).

No FK filters, bool filters, or date-range filters apply (the table has no such columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | Exists | Partner slim list (`pkid`, `Name`), ordered by `DisplayOrder ASC` |

This feature **provides** the `partners` lookup (already implemented in
`LookupRepository.GetPartnersAsync` / `LookupsController` / `lookup.service.ts`) consumed
by the `Course` feature. `Partner` itself needs no lookups.

---

## API Endpoints

Standard CRUD:

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/partners` | List all |
| `POST` | `/api/partners/query` | Filtered query (body: `PartnerQuery`) |
| `GET` | `/api/partners/{id}` | Get by pkid |
| `POST` | `/api/partners` | Create |
| `PUT` | `/api/partners` | Update (pkid from body) |
| `DELETE` | `/api/partners/{id}` | Delete |

No special endpoints. No auth exceptions.

Note on DELETE: `Course`, `Certification`, and `PartnerCourseGroup` all FK to `Partner`
with no cascade, so deleting a Partner that still has children fails with a SQL FK
violation. The repository surfaces the SQL error; the controller returns the default 500
unless a friendlier guard is added later.

---

## Backend Notes

### Models

`pkid` is `smallint` IDENTITY → C# `short`. Excluded from create (IDENTITY), included on
update (pkid from body). `ImageFilename` is nullable → `string?`.

```csharp
public class Partner
{
    public short Pkid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AppKey { get; set; } = string.Empty;
    public string NameOnPartnerMenu { get; set; } = string.Empty;
    public string NameOnCourseDetailPage { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string? ImageFilename { get; set; }
}

public class PartnerRequest
{
    public short Pkid { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string AppKey { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string NameOnPartnerMenu { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string NameOnCourseDetailPage { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    [MaxLength(50)]
    public string? ImageFilename { get; set; }
}

public class PartnerQuery
{
    public string? Keyword { get; set; }
}
```

Models go in `Models/Partner.cs`, `Models/PartnerRequest.cs`, `Models/PartnerQuery.cs`.

### SQL — SELECT

No FK aliases, no JOINs, no `nchar` (all text columns are `nvarchar`/`varchar`, no `RTRIM`
needed).

```sql
-- shared projection
p.pkid AS Pkid, p.Name AS Name, p.AppKey AS AppKey,
p.NameOnPartnerMenu AS NameOnPartnerMenu, p.NameOnCourseDetailPage AS NameOnCourseDetailPage,
p.DisplayOrder AS DisplayOrder, p.ImageFilename AS ImageFilename

-- GetAllAsync / QueryAsync
SELECT {cols} FROM Partner p
WHERE (@Keyword IS NULL OR p.Name LIKE '%' + @Keyword + '%'
       OR p.AppKey LIKE '%' + @Keyword + '%'
       OR p.NameOnPartnerMenu LIKE '%' + @Keyword + '%'
       OR p.NameOnCourseDetailPage LIKE '%' + @Keyword + '%')
ORDER BY p.DisplayOrder ASC, p.pkid ASC;

-- GetByIdAsync
SELECT {cols} FROM Partner p WHERE p.pkid = @Pkid;
```

### SQL — INSERT

`pkid` is IDENTITY → excluded.

```sql
INSERT INTO Partner (Name, AppKey, NameOnPartnerMenu, NameOnCourseDetailPage, DisplayOrder, ImageFilename)
VALUES (@Name, @AppKey, @NameOnPartnerMenu, @NameOnCourseDetailPage, @DisplayOrder, @ImageFilename);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

`pkid` immutable (key), from body.

```sql
UPDATE Partner
SET Name = @Name, AppKey = @AppKey, NameOnPartnerMenu = @NameOnPartnerMenu,
    NameOnCourseDetailPage = @NameOnCourseDetailPage, DisplayOrder = @DisplayOrder,
    ImageFilename = @ImageFilename
WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM Partner WHERE pkid = @Pkid;
```

### N-N Sync Pattern

N/A — no junction tables.

### Special Column Notes

- `pkid` is `smallint` IDENTITY → C# `short`; `SCOPE_IDENTITY()` cast to `smallint`.
- No `nchar` columns → no `RTRIM()`.
- No `date`/`time` columns → no DateOnly/TimeOnly handlers needed.
- `ImageFilename` nullable → send `null` (not empty string) when blank.

---

## Frontend Notes

### Route table

| Path | Component |
|------|-----------|
| `/partners` | `PartnerList` |
| `/partners/new` | `PartnerForm` |
| `/partners/:id` | `PartnerDetail` |
| `/partners/:id/edit` | `PartnerForm` |

Register lazily in `app.routes.ts` with `/new` before `/:id`.

### Angular model

```ts
export interface Partner {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename?: string | null;
}

export interface PartnerRequest {
  pkid: number;
  name: string;
  appKey: string;
  nameOnPartnerMenu: string;
  nameOnCourseDetailPage: string;
  displayOrder: number;
  imageFilename?: string | null;
}

export interface PartnerQuery {
  keyword?: string | null;
}
```

Kebab file name: `partner.model.ts`. Service: `partner.service.ts` (numeric pkid → no
`encodeURIComponent` needed).

### List component

- Columns: `pkid` (主代碼), `displayOrder` (顯示順序), `name` (原廠名稱), `appKey`
  (應用金鑰), `nameOnCourseDetailPage` (課程頁顯示名稱), plus a Primary-Foreign link
  button (查看課程) and standard actions (view / edit / delete).
- Sortable, paginated `p-table`; default sort `displayOrder ASC`.
- Filter drawer (`p-drawer`): a single keyword input (原廠名稱／金鑰關鍵字).
- Session storage keys: `partner-list-filters`, `partner-list-sort`, `partner-list-page`.

### Detail component

- Read-only view of all columns.
- Primary-Foreign link button 查看課程 → `/courses?partnerPkid={pkid}`.

### Form component

- Reactive Forms. `pkid` disabled in edit mode (immutable key), absent in add mode.
- `name`: text, required, maxlength 50.
- `appKey`: text, required, maxlength 10.
- `nameOnPartnerMenu`: text, required, maxlength 200.
- `nameOnCourseDetailPage`: text, required, maxlength 50.
- `displayOrder`: `p-inputNumber` (or number input), required, default 0.
- `imageFilename`: text, optional, maxlength 50; submit `null` when blank.
- No lookups → no `forkJoin` needed; sticky `p-toolbar` for save/cancel.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.name}」？
```

### Sidebar placement

- Nav group: **課程管理 Course** (`MENU_GROUP`). The group already exists.
- Entry label: 原廠 Partner, route `/partners`, icon `pi pi-building`.

---

## Files to Create / Modify

### Backend (`CMS.API`)

| File | Action |
|------|--------|
| `Models/Partner.cs` | Create |
| `Models/PartnerRequest.cs` | Create |
| `Models/PartnerQuery.cs` | Create |
| `Repositories/IPartnerRepository.cs` | Create |
| `Repositories/PartnerRepository.cs` | Create |
| `Controllers/PartnersController.cs` | Create |
| `Program.cs` | Modify — register `IPartnerRepository` / `PartnerRepository` |
| `Controllers/LookupsController.cs` | Verify `partners` lookup exists (already present) |

### Frontend (`CMS.NG`)

| File | Action |
|------|--------|
| `core/models/partner.model.ts` | Create |
| `core/services/partner.service.ts` | Create |
| `features/partners/partner-list/` | Create |
| `features/partners/partner-detail/` | Create |
| `features/partners/partner-form/` | Create |
| `app.routes.ts` | Modify — add lazy routes |
| `app.ts` | Modify — sidebar entry under 課程管理 Course |
| `core/services/lookup.service.ts` | Verify `getPartners()` exists (already present) |

### Tests

| File | Action |
|------|--------|
| `CMS.API.Tests/PartnersControllerTests.cs` | Create — list/query, get (found + 404), create, update, delete, required-field 400 |
| `CMS.NG/.../partner.service.spec.ts` | Create — URL/verb per method |
| `CMS.NG/.../partner-list.spec.ts` | Create |
| `CMS.NG/.../partner-detail.spec.ts` | Create |
| `CMS.NG/.../partner-form.spec.ts` | Create — required-field enforcement |
