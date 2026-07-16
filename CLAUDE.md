# CLAUDE.md

Full-stack CMS, code-generated from `database/*.sql`. Stack: `CMS.API` (.NET 9, Dapper, :5000),
`CMS.API.Tests` (xUnit+Moq), `CMS.NG` (Angular 20 + PrimeNG, :4200); solution `src/CMS.slnx`.

## Always-needed
- **Commands**: `dotnet build|test CMS.slnx`, `dotnet run --project CMS.API` (Swagger `/swagger`);
  in `src/CMS.NG`: `npm start`, `npx ng test --watch=false --browsers=ChromeHeadless`, `npx ng build`.
  Start the API before `ng serve`; backend tests need no DB, e2e needs `CMS` on `.\SQLEXPRESS`.
- **Dev login**: `Admin` / `Admin` (rotated dev pwd: `Admin+-*/`) → 系統管理 menu.
- **Web browsing**: use the `/browse` skill for *all* web browsing — **never** `mcp__claude-in-chrome__*`.

## Sometimes-needed — read the match before the task → [docs/reference-index.md](docs/reference-index.md)
- Add/change a feature → `docs/conventions.md`
- Auth / JWT / My Profile / Change Password → `spec/auth/Auth.md`
- Course PDF export → `spec/course/CoursePdf.md`
- Setup / layout / commands / DB data & date refresh / dev login seed → `docs/setup-notes.md`
