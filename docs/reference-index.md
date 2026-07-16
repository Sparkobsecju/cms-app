# Reference Index

Read the matching reference **before** starting the task. `CLAUDE.md` carries only
always-needed context; every task-specific pointer lives here so it loads on demand,
not every session.

## Add or change a feature
[conventions.md](conventions.md): backend + frontend rules, cross-cutting concerns
(RowAudit, global exception / HTTP-error handling), reference-feature catalog, build
order. Canonical patterns: [spec/code-gen.convention.md](../spec/code-gen.convention.md).

## Auth / login / JWT / My Profile / Change Password
[spec/auth/Auth.md](../spec/auth/Auth.md).

## Course PDF export
[spec/course/CoursePdf.md](../spec/course/CoursePdf.md) — full feature spec. Design
rationale in [spec/notes/course-pdf.md](../spec/notes/course-pdf.md) + [adr/](adr)
0001-0005, glossary [CONTEXT.md](../CONTEXT.md).
`GET /api/courses/{courseId}/pdf` — anonymous, published-only (404 otherwise),
MigraDoc-rendered, keyed by the `CourseId` business key. Frontend 下載 PDF trigger on
the course list rows + detail header (`core/utils/download.ts` `saveBlob`); a 404
surfaces a warn toast, not a broken download.

## Setup, layout, full command reference, DB data & date refresh, dev login seed, design rationale
[setup-notes.md](setup-notes.md). `database/*.sql` is schema only — an empty
date-windowed list usually means the data predates the window (→ *Database data*).
