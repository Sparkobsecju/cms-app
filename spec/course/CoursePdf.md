# Build Spec for Course PDF (課程 PDF 匯出)

Not a CRUD table — a read-only export feature layered on top of `Course`.
Backend endpoint + a frontend "下載 PDF" trigger. For the underlying course
entity see [Course.md](Course.md); for the original design rationale see
[../notes/course-pdf.md](../notes/course-pdf.md) and `docs/adr/0001–0005`.

---

## Summary

| Item | Detail |
|------|--------|
| Feature | Render a course's detail as a clean, branded PDF and let a user download it |
| Backend | `GET /api/courses/{courseId}/pdf` — anonymous, published-only, MigraDoc-rendered |
| Frontend | 下載 PDF action on the course **list** rows + course **detail** header |
| Key | `CourseId` **business key** (簡介代碼), e.g. `NINS` — NOT the numeric `pkid` |
| Gate | `PublishStatus.IsPublished = 1`. Draft / discontinued / unknown → **404** |
| Auth | `[AllowAnonymous]` — no login or role required |
| Output | `application/pdf`, `Content-Disposition: attachment; filename={CourseId}.pdf` |

---

## Context

Students submitting a course to an employer for training approval need a tidy,
self-contained document. The backend endpoint (shipped earlier) produces it;
this spec also covers the **frontend trigger** added on `feature/course-pdf`
(merged to `develop` → `main`) so CMS users can pull the same PDF without
hand-crafting the API URL.

---

## API Endpoint

| Method | Route | Notes |
|--------|-------|-------|
| `GET` | `/api/courses/{courseId}/pdf` | `[AllowAnonymous]`. Published course → `200 application/pdf`. Unpublished / unknown → `404` (never 403 — do not confirm existence). |

- `{courseId}` is the `Course.CourseId` string (簡介代碼), URL-encoded.
- Response body is the rendered PDF; filename set via `Content-Disposition`.
- Backend slice (unchanged by this spec): `Controllers/CoursePdfController.cs`,
  `Repositories/CoursePdfRepository.cs` (`GetPublishedForPdfAsync`),
  `Models/CoursePdf.cs`, `Pdf/CoursePdfDocument.cs` (MigraDoc),
  `Pdf/NotoFontResolver.cs`, `Pdf/RichText.cs`.

### PDF content (curated approval-essentials)

Include: header (Title / OfficialTitle, CourseId, Partner name), at-a-glance
(Hour, ListPrice, LearningCredit), Objective, Target, Prerequisites, Outline,
Material, TowardCertOrExam, linked Certifications, Note / OtherInfo.
Exclude: CourseFAQ, CourseRelatedLink, JobCategories, HotCourse, and internal
fields (DisplayOrder, FriendlyUrl, ScheduleOn/Off). See ADR-0001..0005.

---

## Frontend Notes

The consuming production page is the public marketing site (separate repo). This
adds an in-CMS trigger so authenticated staff can download the same document.

### Service

`CourseService.downloadPdf(courseId: string): Observable<Blob>`
- `GET ${apiBaseUrl}/courses/{encodeURIComponent(courseId)}/pdf`, `responseType: 'blob'`.
- File: `src/CMS.NG/src/app/core/services/course.service.ts`.

### Shared util

`saveBlob(blob: Blob, filename: string): void` — creates an object URL, clicks a
synthetic `<a download>`, revokes the URL.
- File: `src/CMS.NG/src/app/core/utils/download.ts` (new; reused by list + detail).

### Course list (課程) — row action

- New 📄 icon button (`pi pi-file-pdf`) as the **first** action in the 操作
  column, before 檢視 / 編輯 / 刪除. Column width widened to `12rem`.
- `pTooltip="下載 PDF"`, disabled while that row's download is in flight
  (`pdfDownloading() === course.pkid`).
- Files: `features/courses/course-list/course-list.html` + `.ts`.

### Course detail (檢視課程) — header button

- Labeled `下載 PDF` outlined button (`pi pi-file-pdf`) between 返回 and 編輯,
  rendered only when the course has loaded. Disabled while `pdfDownloading()`.
- Files: `features/courses/course-detail/course-detail.html` + `.ts`
  (injects `MessageService`).

### Behavior — published gate is server-authoritative

The frontend does **not** pre-check published state (the slim `PublishStatusLookup`
carries no `isPublished` flag; a description heuristic like "上架中" is fragile).
Instead it trusts the endpoint:

- **Success** → `saveBlob(blob, '{CourseId}.pdf')`, no toast.
- **404 (unpublished / unknown)** → warn toast, no download:
  summary `匯出失敗`, detail `此課程尚未上架或不存在，無法匯出 PDF`.
- **Other error** → warn toast, detail `無法匯出 PDF，請稍後再試`.

A per-button busy flag blocks double-clicks (`pdfDownloading` signal:
`number | null` keyed by pkid in the list, `boolean` on the detail page).

Requires the global `<p-toast />` in `app.html` (already present).

---

## Out of scope

- No new list column, filter, or sort — the button reuses the existing row data.
- No backend change — endpoint and rendering are unchanged.
- Partner logo in the PDF header (text-only until the asset location is known).

---

## Verification

- `npx ng build` clean; backend `dotnet test src/CMS.slnx` → PDF tests
  (`CoursePdfControllerTests` 200/404, `CoursePdfDocumentTests`, `RichTextTests`).
- Live against `CMS` on `.\SQLEXPRESS`, logged in as a non-admin client:
  - Published `NINS` (上架中) → `NINS.pdf` (~192 KB, valid `%PDF-1.7`) downloads,
    no toast.
  - Unpublished `OCEJWCD` (已下架) → warn toast, no file.
  - Direct `GET /api/courses/NINS/pdf` → `200 application/pdf`; `OCEJWCD` and a
    bogus id → `404`.
