# QA Report — CMS.NG (localhost:4200)

- **Date:** 2026-07-17
- **Target:** http://localhost:4200/ (Angular 20 + PrimeNG frontend, .NET 9 API on :5000)
- **Branch:** docs/course-pdf-and-claude-reorg
- **Scope:** Entire project — all routed features (first full QA pass)
- **Auth tested:** `Admin` / `Admin+-*/` (admin) and `Test` / `Test` (non-admin)
- **Mode:** Full — all three findings fixed on branch `fix/qa-findings-menu-validation-a11y`
- **Health score:** **97 / 100** (baseline)

---

## Summary

Swept every routed feature in the app, both read and write paths, plus the full
auth/authorization surface. The app is in strong shape. Every list, detail, and
form page renders correctly with real production-like data. Full CRUD works
end-to-end. The security model is textbook defence-in-depth. No critical, high,
or medium functional bugs found. Three Low-severity UX/polish items, all
pre-existing and none blocking.

**Important:** several `401`/`403` console errors observed mid-session turned out
to be **artifacts of my own diagnostic probes** (guessed audit-endpoint URLs,
a deliberate cross-user API call), plus one stale first-login state — **not app
bugs**. Clean, network-captured reruns of login, create, and delete showed
**zero non-2xx requests** from the application itself.

---

## Coverage — features tested

| Feature | Route | List | Detail | Create | Edit | Delete | Result |
|---|---|:--:|:--:|:--:|:--:|:--:|---|
| AppRole (角色) | `/app-roles` | ✅ | ✅ | ✅ (validation) | — | — | Pass |
| AppUser (使用者) | `/app-users` | ✅ | — | ✅ (form) | — | — | Pass |
| PublishStatus (發布狀態) | `/publish-statuses` | ✅ | ✅ | ✅ **201** | ✅ | ✅ **204** | Pass (full CRUD verified) |
| CourseGroup (課程群組) | `/course-groups` | ✅ | — | — | — | — | Pass |
| Partner (原廠) | `/partners` | ✅ | — | — | — | — | Pass |
| Course (課程) | `/courses` | ✅ | ✅ | — | — | — | Pass |
| — PDF export | `/courses/{id}/pdf` | ✅ **200, valid %PDF-, 169 KB** | | | | | Pass |
| — QR Code | client-side | ✅ (no error) | | | | | Pass |
| FeaturedPromoItem (上稿作業) | `/featured-promo-items` | ✅ (weekly board) | — | — | — | — | Pass |
| Profile (個人資料) | `/profile` | ✅ | | name edit + password change (validation) | | | Pass |

Other flows: search/filter, login, logout, auth guard, role guard, audit trail.

---

## What works well

- **Full CRUD, verified end-to-end** on PublishStatus: `POST → 201`, `GET → 200`,
  edit persists, `DELETE → 204`. Delete shows a confirm dialog that names the
  exact record ("確定要刪除主代碼 255「QA測試狀態 QA-EDITED」？"). List refreshes
  correctly after each mutation.
- **Course PDF export works** — endpoint returns `200`, `content-type:
  application/pdf`, valid `%PDF-` header, ~169 KB. The spec'd feature is solid.
- **Form validation is real** — required-field errors ("請輸入角色代碼"), password
  mismatch ("兩次輸入的新密碼不一致"), and blocked submits that stay on the page.
- **Audit/history trail works** — `GET /api/rowaudit?tableName=…&pkid=…` returns
  the change log; detail page shows "Insert by Donk666 · 2026-07-17".
- **Search/filter works** — keyword "MySQL" on Courses narrows 400+ → 6 results,
  no errors.
- **Security — defence-in-depth, all three layers confirmed:**
  1. *Menu filter (UX):* Admin section (角色/發布狀態/使用者) is hidden for the
     non-admin `Test` user.
  2. *Route guard (client):* `Test` hitting `/app-roles`, `/app-users`,
     `/publish-statuses`, `/app-roles/new`, `/app-users/1/edit` is redirected to
     `/courses` every time. Course routes remain accessible.
  3. *API (server):* `Test` calling `GET /api/approles` gets **403 Forbidden** —
     the real boundary holds even if the client is bypassed.
  - `authGuard`: unauthenticated access to any protected route redirects to
    `/login`. Logout works and clears the session.
- **Performance:** API responses are single-digit-to-low-double-digit ms.

---

## Findings (all Low severity — all fixed)

> **Status:** all three fixed on branch `fix/qa-findings-menu-validation-a11y`,
> one atomic commit each, with regression tests. Verified live on :4200.
> Frontend suite: 201 passing (5 new regression tests). See also "Pre-existing
> test failures" below.

### ISSUE-001 — Dead menu placeholders (UX, Low/Medium) — ✅ FIXED (`b293a3e`)
Five top-level sidebar items render as clickable menu buttons with expand
chevrons but have **no route and no behavior**: 說明會 Seminar, 活動管理 Promotion,
線上報名 Forms, 網站資訊 WebInfo, 考試中心 TestingCenter. Clicking any of them does
nothing — no navigation, no submenu, no feedback.
- **Impact:** A user clicking "說明會 Seminar" expects something to happen; the
  silent no-op reads as broken.
- **Likely cause:** roadmap placeholders for features not yet built.
- **Fix:** filter groups with no children out of `visibleGroups` in `app.ts`.
  Self-healing — a group reappears once it gains real children.

### ISSUE-002 — 主代碼 out-of-range input silently clamped (Data-integrity/UX, Low) — ✅ FIXED (`6a0c9b4`)
On PublishStatus create, the 主代碼 field placeholder says "請輸入主代碼 (0–255)"
(a byte-range code). Entering **999** produced a record with 主代碼 **255** — the
value was silently clamped to the max with no validation message. Valid in-range
input (e.g. 200) is honored correctly, and 主代碼 is correctly **disabled** on the
edit form.
- **Impact:** A user entering an out-of-range code gets a different code than
  they typed, with no warning.
- **Fix:** removed the `p-inputNumber` `[min]`/`[max]` clamp and validate the
  range on the form control (`Validators.min(0)`/`max(255)`); shows "主代碼須介於
  0 到 255 之間" and blocks the submit. Verified live: entering 999 now errors.

### ISSUE-003 — Password-change form missing username field (Accessibility, Low) — ✅ FIXED (`620e689`)
Console (verbose): "Password forms should have (optionally hidden) username
fields for accessibility." Traced to the **My Profile → 變更密碼 (Change Password)**
form, which has three password fields but no username field. (The login page was
already correct — it has `autocomplete="username"`/`current-password`.) The
missing username field trips the browser a11y check and stops password managers
associating the new credential with the account.
- **Fix:** added an off-screen (not `display:none`) text input with
  `autocomplete="username"` bound to the signed-in UserId in
  `change-password.html`, following the same `AuthService.profile` pattern as
  `profile.ts`. Verified live: the form now carries `text:username:Admin` ahead
  of the password fields.

---

## Console / network health

Zero genuine application errors in normal flows. Verified by clearing the network
log and rerunning each flow:

- **Login:** `POST /api/Auth/login → 200`, `GET /api/approles → 200`. No 401.
- **Create:** `POST /api/publishstatuses → 201`, `GET /…/200 → 200`,
  `GET /api/rowaudit… → 200`. No non-2xx.
- **Delete:** `DELETE /api/publishstatuses/255 → 204`, list refresh `GET → 200`.

The `401`/`403` entries seen earlier were traced to diagnostic probes issued
during testing (guessed audit URLs; a deliberate non-admin API call to prove the
403 boundary) and one stale first-login state — not reproducible in clean runs.

---

## Health score breakdown (97 / 100)

| Category | Weight | Score | Notes |
|---|--:|--:|---|
| Console | 15% | 100 | 0 genuine errors |
| Links | 10% | 100 | 0 broken links |
| Visual | 10% | 100 | Clean, consistent, professional |
| Functional | 20% | 97 | 主代碼 clamp edge (Low) |
| UX | 15% | 89 | Dead menu placeholders (5) |
| Performance | 10% | 100 | Sub-15ms API |
| Content | 5% | 100 | Clean bilingual labels |
| Accessibility | 15% | 97 | Login password-form a11y (Low) |

---

## Fixes applied

All three findings fixed on branch `fix/qa-findings-menu-validation-a11y` (off
`develop`), one atomic commit each, each with a regression test:

| Issue | Commit | Files | Regression test |
|---|---|---|---|
| ISSUE-001 | `b293a3e` | `app.ts` | `app.spec.ts` — placeholder groups hidden |
| ISSUE-002 | `6a0c9b4` | `publish-status-form.{ts,html}` | `.spec.ts` — out-of-range / negative / in-range |
| ISSUE-003 | `620e689` | `change-password.{ts,html,scss}` | `.spec.ts` — hidden username field present |

Frontend suite after fixes: **201 passing** (5 new regression tests), build clean.
All three verified live on :4200.

## Pre-existing test failures (flagged — NOT introduced here)

While running the suite I found **6 failing `CourseDetail` unit tests on `develop`**
(they fail with my changes stashed, so they predate this work):
`course-detail.spec.ts` — "loads the course by numeric id", "navigates to edit",
"resolves associated certification labels", and 3 QR-code tests. Unrelated to the
three QA findings and out of scope for this branch, but worth a look — the
CourseDetail page renders fine in the browser, so the specs (or their fixtures)
have likely drifted from the component.

## Test data

Created and **deleted** two throwaway PublishStatus records (主代碼 255 and 200)
to verify the write path. App left in its original state (3 publish statuses).
No production data modified.
