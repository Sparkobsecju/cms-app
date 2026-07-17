# Project Learnings — CMS

Knowledge gstack has captured across sessions on this project. Exported from the
learnings store (`~/.gstack/projects/Sparkobsecju-cms-app/learnings.jsonl`) so it's
readable in-repo. Regenerate with `/learn export`.

**Last exported:** 2026-07-17 · **3 entries** (2 operational, 1 pitfall)

---

## Operational

### Course PDF is backend-only
**Key:** `course-pdf-smoke` · confidence 8/10 · observed 2026-07-16

No UI button — it's a direct endpoint. Fetch `GET /api/courses/{CourseId}/pdf` on the
**API host `:5000`** (not the Angular dev server `:4200`). Gated on
`PublishStatus.IsPublished = 1`, keyed by the `CourseId` business key:
- published (上架中) → `200 application/pdf` attachment
- unpublished (已下架) / unknown → `404`

### gstack browse element refs are unstable
**Key:** `browse-refs-unstable` · confidence 8/10 · observed 2026-07-16

`@eNN` element refs are reassigned on every snapshot/navigation; a ref from an earlier
snapshot may point at a different element (a stale `@e17` clicked the menu-collapse
instead of a row button). **Re-snapshot immediately before each click**, and prefer
**labeled** buttons (`snapshot -i` shows the label) over icon-only ones, which render as
bare `[button]`.

---

## Pitfalls

### Admin routes were not role-guarded  ✅ RESOLVED 2026-07-17
**Key:** `routes-not-role-guarded` · confidence 7/10 · observed 2026-07-16

**Original finding:** `authGuard` only checked `isAuthenticated`, with no route-level role
gating. A non-Admin (Client) user was redirected to `/app-roles` after login and could
load admin pages by direct URL; only the sidebar 系統管理 group was hidden via
`hasRole('Admin')` (security through obscurity).

**Resolution (2026-07-17, commit `1105726`):** Added backend `[Authorize(Roles="Admin")]`
on `AppUsers`/`AppRoles`/`PublishStatuses`, plus a client-side `roleGuard('Admin')`
wrapping the admin routes (denied non-admins redirect to `/courses`). See
[reviews/2026-07-17-fixes-applied.md](reviews/2026-07-17-fixes-applied.md) §1–2.
