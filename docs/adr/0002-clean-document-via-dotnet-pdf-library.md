# Course PDF is a clean document built with a .NET PDF library, not a rendered web page

The Course PDF is a clean, professional, branded document generated directly
from course data with a .NET PDF library — not a pixel replica of the public
marketing page. A headless-browser approach (Playwright/Puppeteer) was rejected:
the public page's HTML/CSS lives in a different codebase, so there is nothing to
"just render" here, and running Chromium on the .NET/Windows host adds a heavy
runtime dependency. Generating from data also produces a better approval
artifact (no marketing nav, CTAs, or ads) and keeps CJK text rendering under our
control. The specific library is decided in ADR-0003 (MigraDoc, chosen over QuestPDF on
licensing grounds).
