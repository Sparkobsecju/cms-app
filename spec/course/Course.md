# Build Spec for Course
- database schema: `.\database\course.sql`

## Summary

`Course` is the central entity of the course sub-system: a training course offered by a
partner. It carries descriptive content (objective, outline, prerequisites…), scheduling
dates, pricing, and display metadata. It links to `Partner`, `CourseGroup` (nullable), and
`PublishStatus` as foreign keys, and has N-N relationships with `Certification` and
`JobCategory`. Several child entities reference it (CourseFAQ, CourseRelatedLink, HotCourse,
CourseRecomm) — those features are not yet scaffolded.

| Item | Detail |
|------|--------|
| Primary Key | `pkid` int IDENTITY |
| Foreign Keys | `Partner_pkid` → `Partner.pkid`; `CourseGroup_pkid` → `CourseGroup.pkid` (nullable); `PublishStatus_pkid` → `PublishStatus.pkid` |
| Required Fields | `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`, `Partner_pkid`, `PublishStatus_pkid`, `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` |
| N-N Relationships | `CourseInCertification` (Course ↔ Certification); `CourseJobCategories` (Course ↔ JobCategory) |
| Primary-Foreign Links | CourseFAQ, CourseRelatedLink, HotCourse, CourseRecomm reference Course — not scaffolded, documented only |
| Query Filters | keyword (Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl), Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn range, ScheduleOff range, CanRepeat |
| Default Sort | `DisplayOrder ASC` |

---

## Localization

### Chinese Table Name

- Course: 課程
- Description: 訓練課程主資料

### Chinese Column Names

- pkid: 主代碼
- Title: 課程名稱
- OfficialTitle: 官方課程名稱
- CourseId: 簡介代碼
- ProdCourseId: 科目代碼
- FriendlyUrl: 友善網址
- DisplayOrder: 顯示順序
- Partner_pkid: 原廠
- CourseGroup_pkid: 課程群組
- PublishStatus_pkid: 上架狀態
- ScheduleOn: 上架日期
- ScheduleOff: 下架日期
- Hour: 時數
- ListPrice: 定價
- LearningCredit: 點數
- Material: 教材
- Objective: 課程目標
- Target: 適合對象
- Prerequisites: 先備知識
- Outline: 課程大綱
- TowardCertOrExam: 考試／認證說明
- Note: 備註
- OtherInfo: 其他資訊
- CanRepeat: 允許重聽

---

## Required Fields

Required (NOT NULL, excluding IDENTITY PK):

- `Title`, `CourseId`, `ProdCourseId`, `FriendlyUrl`, `DisplayOrder`
- `Partner_pkid`, `PublishStatus_pkid`
- `ScheduleOn`, `ScheduleOff`, `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat`

Optional (nullable): `OfficialTitle`, `CourseGroup_pkid`, `Material`, `Objective`,
`Target`, `Prerequisites`, `Outline`, `TowardCertOrExam`, `Note`, `OtherInfo`.

Note: `Hour`, `ListPrice`, `LearningCredit`, `CanRepeat` have DB DEFAULT (0). They are
NOT NULL, so the form supplies them (defaulting to 0 in add mode).

---

## Foreign Keys

List/detail resolve the FK to a display label via JOIN; form loads select options via lookups.

- **Partner_pkid** → `Partner.pkid` (required)
  - Alias `p.pkid AS PartnerPkid` for the model property; JOIN exposes `p.Name AS PartnerName`.
  - Option label = `Name`; order by `DisplayOrder ASC`.
  - Lookup: `GET /api/lookups/partners`.

- **CourseGroup_pkid** → `CourseGroup.pkid` (nullable — allow null / "無" option)
  - Alias `c.CourseGroup_pkid AS CourseGroupPkid`; LEFT JOIN exposes `g.Description AS CourseGroupDescription`.
  - Option label = `Description`; order by `pkid ASC`.
  - Lookup: `GET /api/lookups/course-groups`.

- **PublishStatus_pkid** → `PublishStatus.pkid` (required)
  - Alias `c.PublishStatus_pkid AS PublishStatusPkid`; JOIN exposes `s.Description AS PublishStatusDescription`.
  - Option label = `Description`; order by `pkid ASC`.
  - Lookup: `GET /api/lookups/publish-statuses`.

---

## Foreign-Primary Links

Outbound navigation links (list, detail, form) to the referenced primary table's detail page.
The target features are not yet scaffolded, so these are documented but **not wired** in this
build (consistent with the CourseGroup scaffold decision).

- **Partner_pkid** → `/partners/{Partner_pkid}` (not scaffolded)
- **CourseGroup_pkid** → `/course-groups/{CourseGroup_pkid}` (scaffolded; link may be added later)
- **PublishStatus_pkid** → `/publish-statuses/{PublishStatus_pkid}` (scaffolded; link may be added later)

---

## Primary-Foreign Links

Child tables referencing `Course.pkid` (or `Course.CourseId`): CourseFAQ, CourseRelatedLink,
HotCourse, CourseRecomm. None of these features exist yet, so no inbound nav buttons are
rendered. Documented for when those features are scaffolded.

**N/A (documented, not wired)**

---

## N-N Relationships

### CourseInCertification — Course ↔ Certification

Junction `CourseInCertification` (`Course_pkid`, `Certification_pkid`), composite PK.

- Form (add + edit): `p-multiselect` of Certifications. Option label = `Partner.Name + ' - ' + Certification.Title` ordered by Partner then Title. `Certification.Title` is `nchar(100)` → `RTRIM()` in the lookup.
- Detail: show associated Certification titles.
- Request field: `CertificationPkids` — `List<int>`.
- Lookup: `GET /api/lookups/certifications`.
- Sync on save: `DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid`, then bulk INSERT.

### CourseJobCategories — Course ↔ JobCategory

Junction `CourseJobCategories` (`Course_pkid`, `JobCategory_pkid`), composite PK.

- Form (add + edit): `p-multiselect` of JobCategories. Option label = `Description` ordered by `Description`.
- Detail: show associated JobCategory descriptions.
- Request field: `JobCategoryPkids` — `List<short>`.
- Lookup: `GET /api/lookups/job-categories`.
- Sync on save: `DELETE FROM CourseJobCategories WHERE Course_pkid = @Pkid`, then bulk INSERT.

Both syncs run inside the same transaction as the INSERT/UPDATE.

---

## Query Filters

- **keyword**: LIKE on `Title`, `OfficialTitle`, `CourseId`, `ProdCourseId`, `FriendlyUrl`.
- **Partner_pkid** (`short?`): exact match; lookup `partners`.
- **CourseGroup_pkid** (`short?`): exact match; lookup `course-groups`.
- **PublishStatus_pkid** (`byte?`): exact match; lookup `publish-statuses`.
- **ScheduleOn** date range: `ScheduleOnFrom` / `ScheduleOnTo` (inclusive).
- **ScheduleOff** date range: `ScheduleOffFrom` / `ScheduleOffTo` (inclusive).
- **CanRepeat** (`bool?`): tri-state exact match.

---

## Lookup Endpoints Required

| Route | Status | Returns |
|-------|--------|---------|
| `GET /api/lookups/partners` | **New** | Partner list (`pkid`, `Name`), order `DisplayOrder ASC` |
| `GET /api/lookups/course-groups` | Exists | CourseGroup list |
| `GET /api/lookups/publish-statuses` | Exists | PublishStatus list |
| `GET /api/lookups/certifications` | **New** | Certification list (`pkid`, `partnerName`, `title` via RTRIM), order Partner then Title |
| `GET /api/lookups/job-categories` | **New** | JobCategory list (`pkid`, `Description`), order `Description ASC` |

New lookup models: `PartnerLookup`, `CertificationLookup`, `JobCategoryLookup` (in `Models/Lookups/`).

---

## API Endpoints

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses` | List all (with FK display names) |
| `POST` | `/api/courses/query` | Filtered query (body: `CourseQuery`) |
| `GET` | `/api/courses/{id}` | Get by pkid (includes N-N pkid lists) |
| `POST` | `/api/courses` | Create |
| `PUT` | `/api/courses` | Update (pkid from body) |
| `DELETE` | `/api/courses/{id}` | Delete |

`{id:int}` route (int IDENTITY PK). No special endpoints in this scaffold (copy/QR/PDF from the
legacy system are out of scope). No auth exceptions.

---

## Backend Notes

### Models

```csharp
public class Course
{
    public int Pkid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? OfficialTitle { get; set; }
    public string CourseId { get; set; } = string.Empty;
    public string ProdCourseId { get; set; } = string.Empty;
    public string FriendlyUrl { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public short PartnerPkid { get; set; }
    public short? CourseGroupPkid { get; set; }
    public byte PublishStatusPkid { get; set; }
    public DateOnly ScheduleOn { get; set; }
    public DateOnly ScheduleOff { get; set; }
    public short Hour { get; set; }
    public decimal ListPrice { get; set; }
    public decimal LearningCredit { get; set; }
    public string? Material { get; set; }
    public string? Objective { get; set; }
    public string? Target { get; set; }
    public string? Prerequisites { get; set; }
    public string? Outline { get; set; }
    public string? TowardCertOrExam { get; set; }
    public string? Note { get; set; }
    public string? OtherInfo { get; set; }
    public bool CanRepeat { get; set; }
    // FK display names (JOIN, read-only):
    public string PartnerName { get; set; } = string.Empty;
    public string? CourseGroupDescription { get; set; }
    public string PublishStatusDescription { get; set; } = string.Empty;
    // N-N (populated on GET by id):
    public List<int> CertificationPkids { get; set; } = [];
    public List<short> JobCategoryPkids { get; set; } = [];
}
```

`CourseRequest`: all writable scalar fields (Pkid for update) + `CertificationPkids` /
`JobCategoryPkids`; excludes the JOIN display names.

`CourseQuery`: `Keyword?`, `PartnerPkid?` (short), `CourseGroupPkid?` (short),
`PublishStatusPkid?` (byte), `ScheduleOnFrom/To?` (DateOnly), `ScheduleOffFrom/To?` (DateOnly),
`CanRepeat?` (bool).

### SQL — SELECT

```sql
SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder, c.Partner_pkid AS PartnerPkid, c.CourseGroup_pkid AS CourseGroupPkid,
       c.PublishStatus_pkid AS PublishStatusPkid, c.ScheduleOn, c.ScheduleOff, c.Hour,
       c.ListPrice, c.LearningCredit, c.Material, c.Objective, c.Target, c.Prerequisites,
       c.Outline, c.TowardCertOrExam, c.Note, c.OtherInfo, c.CanRepeat,
       p.Name AS PartnerName,
       g.Description AS CourseGroupDescription,
       s.Description AS PublishStatusDescription
FROM Course c
JOIN Partner p ON p.pkid = c.Partner_pkid
LEFT JOIN CourseGroup g ON g.pkid = c.CourseGroup_pkid
JOIN PublishStatus s ON s.pkid = c.PublishStatus_pkid
```

`GetByIdAsync` additionally reads the two N-N pkid lists (via `QueryMultipleAsync`):
`SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid;`
`SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid;`

`QueryAsync` appends the filter WHERE clauses; default `ORDER BY c.DisplayOrder ASC`.

### SQL — INSERT / UPDATE

INSERT writable columns (exclude IDENTITY pkid); `SELECT CAST(SCOPE_IDENTITY() AS int)`.
UPDATE the same columns `WHERE pkid = @Pkid`. Both wrapped in a transaction that also runs the
two N-N delete-then-reinsert syncs.

### Special Column Notes

- `ScheduleOn` / `ScheduleOff` are `date` → `DateOnly`; handlers already registered in `Program.cs`.
- `ListPrice` `decimal(9,0)`, `LearningCredit` `decimal(9,1)` → `decimal`.
- `Certification.Title` is `nchar(100)` → `RTRIM()` in the certifications lookup.
- No RowAudit in this codebase (sibling features don't use it) — skip.

---

## Frontend Notes

### Route table

| Path | Component |
|------|-----------|
| `/courses` | `CourseList` |
| `/courses/new` | `CourseForm` |
| `/courses/:id` | `CourseDetail` |
| `/courses/:id/edit` | `CourseForm` |

### Angular model

`Course`, `CourseRequest`, `CourseQuery` interfaces mirroring the C# models (camelCase),
plus `PartnerLookup`, `CertificationLookup`, `JobCategoryLookup`.

### List component

Columns (from the supplied list): `pkid`, `displayOrder`, `courseId`, `prodCourseId`,
`title` (link), `partnerName`, `courseGroupDescription`, `publishStatusDescription`,
`scheduleOn`, `scheduleOff`, `hour`, `listPrice`, `learningCredit`, `canRepeat` (tag).
FK display names come straight from the JOINed API response (no frontend lookup mapping needed
for the list). Default sort `displayOrder ASC`.

Filter drawer: keyword input; partner / courseGroup / publishStatus `p-select`
(`appendTo="body"`, `[filter]="true"`); scheduleOn & scheduleOff `p-datepicker` range pairs;
canRepeat tri-state `p-select`. Session keys: `course-list-filters|-sort|-page`.

### Form component

Reactive Forms; `forkJoin` of partners, course-groups, publish-statuses, certifications,
job-categories on init. `p-datepicker` for ScheduleOn/ScheduleOff (ISO ↔ Date). `p-inputNumber`
for DisplayOrder/Hour/ListPrice/LearningCredit. `p-select` for the three FKs (course group has a
"無/null" option). `p-multiselect` (`[maxSelectedLabels]="9999"`) for Certifications and
JobCategories. `p-checkbox` for CanRepeat. Content fields (`Objective`, `Outline`,
`TowardCertOrExam`, …) as `textarea`. String content only — `toIso()` uses local date parts.

### Delete Confirmation

```
確定要刪除主代碼 <b>${item.pkid}</b>「${item.courseId}」？
```

### Sidebar placement

Nav group **課程管理 Course** (already created by the CourseGroup scaffold). Add entry
課程 Course → `/courses`.

---

## Files to Create / Modify

### Backend (`CMS.API`)

| File | Action |
|------|--------|
| `Models/Course.cs`, `CourseRequest.cs`, `CourseQuery.cs` | Create |
| `Models/Lookups/PartnerLookup.cs`, `CertificationLookup.cs`, `JobCategoryLookup.cs` | Create |
| `Repositories/ICourseRepository.cs`, `CourseRepository.cs` | Create |
| `Controllers/CoursesController.cs` | Create |
| `Program.cs` | Modify — register `ICourseRepository` |
| `Repositories/ILookupRepository.cs` + `LookupRepository.cs` + `Controllers/LookupsController.cs` | Modify — add partners / certifications / job-categories lookups |

### Frontend (`CMS.NG`)

| File | Action |
|------|--------|
| `core/models/course.model.ts` | Create |
| `core/services/course.service.ts` | Create |
| `features/courses/course-list/`, `course-detail/`, `course-form/` | Create |
| `app.routes.ts` | Modify — add lazy routes |
| `app.ts` | Modify — sidebar entry under 課程管理 Course |
| `core/services/lookup.service.ts` | Modify — add partners / certifications / jobCategories |

### Tests

| File | Action |
|------|--------|
| `CMS.API.Tests/CoursesControllerTests.cs` | Create |
| `CMS.API.Tests/LookupsControllerTests.cs` | Modify — add new lookup tests |
| `course.service.spec.ts`, `course-list/-detail/-form.spec.ts` | Create |
