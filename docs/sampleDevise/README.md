# sampleDevise — cautionary code samples (學一次，別再犯)

This folder is a **teaching archive of real mistakes found in this codebase and how they were fixed.**
Each file takes one dangerous pattern, shows the *before* and *after* code side by side, and — most
importantly — spells out **the concrete scenario in which the danger actually bites**, so the next person
recognises the shape of the mistake before they type it.

These are not abstract "best practices". Every sample here was a real defect in `CMS.API` that a security
review (`/cso`) or code review caught. Read the scenario, not just the diff: the point is to remember
*when* the bad code is safe-looking but wrong.

## How to read a sample

1. **The vulnerable code** — the exact lines that were wrong.
2. **Why it looks fine** — the reason a competent developer writes it anyway.
3. **The attack scenario** — step by step, who the attacker is and what they do. This is the part to memorise.
4. **The fix** — the corrected code.
5. **The rule** — a one-line "don't do X, do Y" you can apply without re-reading the whole file.

## Index

| # | Sample | Category | Caught by |
|---|--------|----------|-----------|
| 01 | [Password hashing: unsalted SHA-256 → PBKDF2](./01-password-hashing-unsalted-sha256.md) | OWASP A02 Cryptographic Failures | `/cso` 2026-07-17 |
| 02 | [100 cautionary samples (extra learning)](./02-extra-learning.md) | Broad catalogue (crypto, auth, injection, .NET, Dapper, Angular, infra) | Collected reference |
| 03 | [ASP.NET Web Forms (.NET Framework): 100 samples](./03-webform.md) | Framework-specific (ViewState, request validation, web.config, FormsAuth) | Collected reference |
| 04 | [ASP.NET MVC 5 (.NET Framework): 100 samples](./04-mvc5.md) | Framework-specific (anti-forgery, over-posting, Razor, Identity/OWIN) | Collected reference |
| 05 | [ASP.NET Core (.NET 6–9): 100 samples](./05-core.md) | Framework-specific — **this repo's stack** (pipeline, DI, EF Core, JWT) | Collected reference |

> When you fix another finding worth teaching from, add a numbered file here and a row above.
> Cross-reference the remediation log in [`docs/reviews/`](../reviews/) for the "what changed" record;
> this folder is the "why it was dangerous, don't repeat it" record.
