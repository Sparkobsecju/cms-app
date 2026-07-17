# Reference Index

Read the matching reference **before** starting the task. `CLAUDE.md` carries only
always-needed context; every task-specific pointer lives here so it loads on demand,
not every session.

## Add or change a feature
[conventions.md](conventions.md): backend + frontend rules, cross-cutting concerns
(RowAudit, global exception / HTTP-error handling), reference-feature catalog, build
order. Canonical patterns: [spec/code-gen.convention.md](../spec/code-gen.convention.md).

## Auth / login / JWT / role authz / My Profile / Change Password
[spec/auth/Auth.md](../spec/auth/Auth.md). **Role-based authorization** (added 2026-07-17):
the global fallback policy enforces *authentication* on every endpoint; admin controllers
(`AppUsers`/`AppRoles`/`PublishStatuses`) additionally carry `[Authorize(Roles="Admin")]`,
and the Angular admin routes are wrapped in `roleGuard('Admin')` (`core/guards/role.guard.ts`) —
the sidebar 系統管理 filter is UX-only. Seed role id `Admin` (via `AppUserRole('Admin','Admin')`);
dev login `Admin`/`Admin` holds it. Auth guard also rejects expired JWTs (fail-closed), and the
HTTP interceptor attaches the bearer token only to same-origin API requests.
**Passwords**: salted PBKDF2 (`Security/PasswordHasher.cs`); legacy unsalted SHA-256 hashes still
verify and are auto-migrated on next login. Verify in code, **not SQL** — hashes are salted.

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

## Code review findings + remediation status
[reviews/2026-07-16-project-review.md](reviews/2026-07-16-project-review.md) — full-project
audit (0 P0, 12 P1, 13 P2, by area §4.x). [reviews/2026-07-17-fixes-applied.md](reviews/2026-07-17-fixes-applied.md)
— remediation log: what's fixed vs. what's still open, ranked. **Read the fix log before
touching auth, the data layer, or Course PDF** so you don't redo done work or reopen a
deferred item without its context. Update the log each remediation pass.

## Cautionary code samples — "don't code like this"
[sampleDevise/](sampleDevise/README.md) — a teaching archive of real defects found here, each with the
before/after code, the **concrete attack scenario** that makes the pattern dangerous, and a one-line rule.
Read before writing security-sensitive code. Per-framework catalogues for Web Forms / MVC 5 /
ASP.NET Core; each doc has a `.zh-TW.md` 繁體中文 translation alongside it. First entry:
[01 — unsalted SHA-256 password hashing](sampleDevise/01-password-hashing-unsalted-sha256.md) (why a fast
hash is wrong for passwords, why salted hashes can't be compared in SQL, and safe lazy migration).

## What the app / UI looks like
[screenshots/](screenshots/) — 12 captioned shots incl. a role-gated view; embedded in the root
[README](../README.md) Screenshots section.

## Project learnings (cross-session)
[learnings.md](learnings.md) — patterns/pitfalls gstack captured (Course PDF is backend-only,
browse ref instability, the now-resolved missing role guard). Export from the store with
`/learn export`; add with `/learn add`.
