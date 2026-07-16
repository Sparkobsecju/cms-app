# Feature Note: Save Course Detail as PDF

Status: implemented & verified on branch `worktree-feature-course-pdf`.
Design settled in a grilling session; decisions recorded in `docs/adr/0001–0005`
and `CONTEXT.md` (glossary: **Course Detail**, **Course PDF**).

## Context

Students browsing the public course catalogue need a clean, self-contained
document to submit to an employer for training approval / reimbursement. Today
they Ctrl+P the marketing page or copy-paste into email — noisy, inconsistent,
unprofessional. This feature adds a server-side endpoint that returns a tidy,
branded PDF of a course's detail, callable by the public site (a separate
codebase) and reusable by the admin CMS.

## Decisions (locked)

- **Server-side endpoint**, not client-side: `GET /api/courses/{courseId}/pdf` in CMS.API (ADR-0001).
- **Anonymous** access — matches the API's current no-auth posture; consumer is public students.
- **Published-only**, keyed by the `CourseId` business key. Draft/discontinued/unknown → **404** (never 403 — don't confirm existence).
- **Clean branded document via MigraDoc (MIT)**, not a headless-browser render of the marketing page (ADR-0002, ADR-0003). QuestPDF rejected on licensing (must be free at any company size).
- **Content = curated approval-essentials.** Include: header (Title/OfficialTitle, CourseId, Partner name), at-a-glance (Hour, ListPrice, LearningCredit), Objective, Target, Prerequisites, Outline, Material, TowardCertOrExam, linked Certifications, Note/OtherInfo. **Exclude**: CourseFAQ, CourseRelatedLink, JobCategories, HotCourse, and internal fields (DisplayOrder, FriendlyUrl, ScheduleOn/Off).
- **Font = Noto Sans TC, variable TrueType** (SIL OFL), embedded and resolved via a PDFsharp `IFontResolver`. TrueType (not OTF) so PDFsharp subsets it — see "Findings" (ADR-0004).
- **Filename**: `{CourseId}.pdf` via `Content-Disposition`.
- **Course fields are HTML rich text**; the PDF flattens them to clean plain text and inserts CJK break opportunities (ADR-0005).

## Implementation

Self-contained backend vertical slice using dedicated `CoursePdf*` types so it
merges cleanly with the sibling Course-CRUD branch (both controllers contribute
to the `api/courses` route prefix; action templates differ). No `CMS.NG` work —
the consuming page is the public site (separate repo).

New files (`src/CMS.API`):
- `Controllers/CoursePdfController.cs` — `GET api/courses/{courseId}/pdf`, anonymous, `File(...)` or 404.
- `Repositories/ICoursePdfRepository.cs` + `CoursePdfRepository.cs` — `GetPublishedForPdfAsync` using the `QueryMultipleAsync` composite-read pattern (course+partner+publish-status, then cert titles; `RTRIM` on nchar).
- `Models/CoursePdf.cs` — curated read model.
- `Pdf/CoursePdfDocument.cs` — MigraDoc builder, `Render(CoursePdf) -> byte[]`.
- `Pdf/NotoFontResolver.cs` — embedded-font resolver, simulates bold/italic.
- `Pdf/RichText.cs` — HTML→plain-text flattening + CJK break insertion.
- `Assets/Fonts/NotoSansTC-VF.ttf` — embedded font (SIL OFL).

Edits: `CMS.API.csproj` (PDFsharp-MigraDoc 6.1.1, embedded font, InternalsVisibleTo),
`Program.cs` (repo DI). Font resolver registered once via `CoursePdfDocument` static ctor.

Tests (`src/CMS.API.Tests`, xUnit + Moq, no DB): `CoursePdfControllerTests` (200 pdf / 404),
`CoursePdfDocumentTests` (valid `%PDF`, CJK + minimal), `RichTextTests` (tag stripping, entities, bullets, CJK breaks).

## Findings from verification (fed back into the design)

Caught by rasterising a real published course PDF and inspecting it:

1. **10 MB PDFs → 197 KB.** PDFsharp does not subset CFF/OTF fonts, so the full
   ~11 MB face embedded into every file — a problem for a document meant to be
   emailed. Switching to the TrueType (glyf) variable font let PDFsharp subset to
   the used glyphs (~50× smaller). (ADR-0004)
2. **HTML content + CJK wrapping.** Course fields hold HTML (`<font>`, `<sup>`,
   `<br>`, entities); rendered raw, tags showed literally. And MigraDoc only wraps
   at whitespace, so long Chinese lines overflowed the margin and clipped. Fixed
   by `RichText` (flatten HTML, insert U+200B after CJK chars). (ADR-0005)

## Verification

- `dotnet build src/CMS.slnx` clean; `dotnet test src/CMS.slnx` → 22/22 pass (no DB).
- Live against `CMS` on `.\SQLEXPRESS`: `GET /api/courses/CISSP/pdf` → 200,
  `application/pdf`, `filename=CISSP.pdf`, valid `%PDF-1.7`, CJK renders correctly
  (visually confirmed via PDFium rasterisation); bogus/unpublished id → 404.

## Follow-ups (out of scope)

- Partner logo: text-only header until the Partner image asset location is known.
- Optional admin "Preview PDF" link in `CMS.NG`.
- On merge with Course-CRUD, optionally fold the pdf action into `CoursesController`.
