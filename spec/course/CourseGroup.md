# Build Spec for CourseGroup
- database schema: `.\database\course.sql`

## Summary

`CourseGroup` is a small lookup/category entity that groups courses into a named
group (課程群組). It has a single meaningful column, `Description`. It is referenced
as a foreign key target by `Course` (`CourseGroup_pkid`, nullable) and by
`PartnerCourseGroup` (`CourseGroup_pkid`, NOT NULL). It has no foreign keys of its
own and no N-N relationships.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` smallint IDENTITY |
| Foreign Keys | None |
| Required Fields | `Description` |
| N-N Relationships | N/A |
| Primary-Foreign Links | `Course` and `PartnerCourseGroup` reference `CourseGroup.pkid` |
| Query Filters | keyword (Description) |
| Default Sort | `pkid DESC` |

---

## Localization

### Chinese Table Name

- CourseGroup: 課程群組
- Description: 課程群組分類主資料

### Chinese Column Names

- pkid: 主代碼
- Description: 群組名稱

---

## Required Fields

Required (NOT NULL):
- `Description` NOT NULL — `nvarchar(100)`

Optional (nullable):
- None

---

## Foreign Keys

`CourseGroup` has no foreign key columns.

**N/A**

---

## Foreign-Primary Links

`CourseGroup` has no foreign key columns.

**N/A**

---

## Primary-Foreign Links

The following tables reference `CourseGroup.pkid` as a foreign key target. Show
navigation links in list and detail views.

- **Course** (`Course_pkid` → via `CourseGroup_pkid`)
  - Column header: 對應課程
  - Button label: 查看課程 (icon: `pi pi-book`)
  - Link target: `/courses?courseGroupPkid={pkid}`
  - Query param name the Course list accepts: `courseGroupPkid`

- **PartnerCourseGroup** (`CourseGroup_pkid`)
  - Column header: 對應合作廠商群組
  - Button label: 查看合作廠商群組 (icon: `pi pi-list`)
  - Link target: `/partner-course-groups?courseGroupPkid={pkid}`
  - Query param name the PartnerCourseGroup list accepts: `courseGroupPkid`
  - Note: `PartnerCourseGroup` is not yet scaffolded — the link is defined here for
    completeness; wire it up when that feature exists.

---

## N-N Relationships

`CourseGroup` participates in no pure junction tables. `PartnerCourseGroup` carries
its own identity PK plus extra columns (`Partner_pkid`, `DisplayOrder`, `Description`),
so it is a first-class entity, not an N-N junction.

**N/A**

---

## Query Filters

- **keyword**: string
  - LIKE on `Description`

No FK filters, bool filters, or date-range filters apply (the table has no such columns).

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/course-groups` | Exists | CourseGroup slim list (`pkid`, `Description`), ordered by `pkid ASC` |

This feature also **provides** the `course-groups` lookup consumed by the `Course`
feature. No other lookups are needed by `CourseGroup` itself.

---

## API Endpoints

Standard CRUD:

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/coursegroups` | List all |
| `POST` | `/api/coursegroups/query` | Filtered query (body: `CourseGroupQuery`) |
| `GET` | `/api/coursegroups/{id}` | Get by pkid |
| `POST` | `/api/coursegroups` | Create |
| `PUT` | `/api/coursegroups` | Update (pkid from body) |
| `DELETE` | `/api/coursegroups/{id}` | Delete |

No special endpoints. No auth exceptions.

Note on DELETE: the DB FK `FK_Course_CourseGroup` is `ON DELETE CASCADE`, so deleting
a CourseGroup cascades to its Courses. `FK_PartnerCourseGroup_CourseGroup` has no
cascade, so a delete may fail with a FK violation if PartnerCourseGroup rows exist.
The repository surfaces the SQL error; the controller returns the default 500 unless a
friendlier guard is added later.

---

## Backend Notes

### Models

`pkid` is `smallint` IDENTITY → C# `short`. Excluded from `CourseGroupRequest` on create
but included for update (pkid from body).

```csharp
public class CourseGroup
{
    public short Pkid { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class CourseGroupRequest
{
    public short Pkid { get; set; }

    [Required]
    [MaxLength(100)]
    public string Description { get; set; } = string.Empty;
}

public class CourseGroupQuery
{
    public string? Keyword { get; set; }
}
```

Models go in `Models/CourseGroup.cs`, `Models/CourseGroupRequest.cs`,
`Models/CourseGroupQuery.cs`.

### SQL — SELECT

No FK aliases, no JOINs, no `nchar` (Description is `nvarchar`, no `RTRIM` needed).

```sql
-- GetAllAsync
SELECT pkid, Description FROM CourseGroup ORDER BY pkid DESC;

-- GetByIdAsync
SELECT pkid, Description FROM CourseGroup WHERE pkid = @Pkid;

-- QueryAsync
SELECT pkid, Description FROM CourseGroup
WHERE (@Keyword IS NULL OR Description LIKE '%' + @Keyword + '%')
ORDER BY pkid DESC;
```

### SQL — INSERT

`pkid` is IDENTITY → excluded.

```sql
INSERT INTO CourseGroup (Description) VALUES (@Description);
SELECT CAST(SCOPE_IDENTITY() AS smallint);
```

### SQL — UPDATE

`pkid` immutable (key), from body.

```sql
UPDATE CourseGroup SET Description = @Description WHERE pkid = @Pkid;
```

### SQL — DELETE

```sql
DELETE FROM CourseGroup WHERE pkid = @Pkid;
```

### N-N Sync Pattern

N/A — no junction tables.

### Special Column Notes

- `pkid` is `smallint` IDENTITY → C# `short`; `SCOPE_IDENTITY()` cast to `smallint`.
- No `nchar` columns → no `RTRIM()`.
- No `date`/`time` columns → no DateOnly/TimeOnly handlers needed.

---

## Frontend Notes

### Route table

| Path | Component |
|------|-----------|
| `/course-groups` | `CourseGroupListComponent` |
| `/course-groups/new` | `CourseGroupFormComponent` |
| `/course-groups/:id` | `CourseGroupDetailComponent` |
| `/course-groups/:id/edit` | `CourseGroupFormComponent` |

Register lazily in `app.routes.ts` with `/new` before `/:id`.

### Angular model

```ts
export interface CourseGroup {
  pkid: number;
  description: string;
}

export interface CourseGroupRequest {
  pkid: number;
  description: string;
}

export interface CourseGroupQuery {
  keyword?: string | null;
}
```

Kebab file name: `course-group.model.ts`. Service: `course-group.service.ts`.

### List component

- Columns: `pkid` (主代碼), `description` (群組名稱), plus Primary-Foreign link
  buttons (查看課程) and standard actions (view / edit / delete).
- Sortable, paginated `p-table`; default sort `pkid DESC`.
- Filter drawer (`p-drawer`): a single keyword input (群組名稱關鍵字).
- Session storage keys: `course-group-list-filters`, `course-group-list-sort`,
  `course-group-list-page`.

### Detail component

- Read-only view of `pkid` and `description`.
- Primary-Foreign link button 查看課程 → `/courses?courseGroupPkid={pkid}`.

### Form component

- Reactive Forms. `pkid` disabled in edit mode (immutable key), hidden/absent in new mode.
- `description`: `input` text, `required`, maxlength 100.
- No lookups → no `forkJoin` needed; sticky `p-toolbar` for save/cancel.

### Delete Confirmation Message

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.description}」？
```

### Sidebar placement

- Nav group: **課程管理 Course** (`MENU_GROUP`). Create the group if it does not exist.
- Entry label: 課程群組, route `/course-groups`.

---

## Files to Create / Modify

### Backend (`CMS.API`)

| File | Action |
|------|--------|
| `Models/CourseGroup.cs` | Create |
| `Models/CourseGroupRequest.cs` | Create |
| `Models/CourseGroupQuery.cs` | Create |
| `Repositories/ICourseGroupRepository.cs` | Create |
| `Repositories/CourseGroupRepository.cs` | Create |
| `Controllers/CourseGroupsController.cs` | Create |
| `Program.cs` | Modify — register `ICourseGroupRepository` / `CourseGroupRepository` |
| `Controllers/LookupsController.cs` | Verify `course-groups` lookup exists (add if missing) |

### Frontend (`CMS.NG`)

| File | Action |
|------|--------|
| `core/models/course-group.model.ts` | Create |
| `core/services/course-group.service.ts` | Create |
| `features/course-groups/course-group-list/` | Create |
| `features/course-groups/course-group-detail/` | Create |
| `features/course-groups/course-group-form/` | Create |
| `app.routes.ts` | Modify — add lazy routes |
| `app.html` / `app.ts` | Modify — sidebar entry under 課程管理 Course |
| `core/services/lookup.service.ts` | Verify `getCourseGroups()` exists (add if missing) |

### Tests

| File | Action |
|------|--------|
| `CMS.API.Tests/CourseGroupsControllerTests.cs` | Create — list/query, get (found + 404), create, update, delete, required-field 400 |
| `CMS.NG/.../course-group.service.spec.ts` | Create — URL/verb per method |
| `CMS.NG/.../course-group-list.component.spec.ts` | Create |
| `CMS.NG/.../course-group-detail.component.spec.ts` | Create |
| `CMS.NG/.../course-group-form.component.spec.ts` | Create — required-field enforcement |
