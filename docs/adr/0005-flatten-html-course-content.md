# Course content is HTML; the PDF flattens it to clean plain text

The long course fields (Objective, Outline, Prerequisites, …) store **HTML rich
text** in the database — `<font>`, `<sup>`, `<br>`, `<li>`, entities like `&amp;`.
The Course PDF strips that markup to clean plain text (via `RichText.ToPlainText`)
rather than reproducing the formatting: rendering the raw tags looked
unprofessional, and faithfully re-rendering arbitrary editor HTML in MigraDoc is
out of proportion for an approval document. Block tags become newlines and `<li>`
becomes a bullet, so structure survives; colours/fonts are dropped by design.

Separately, MigraDoc only wraps lines at whitespace, so long Traditional Chinese
runs (which have none) overflowed the page margin and were clipped. `RichText.AddCjkBreaks`
inserts a zero-width space (U+200B) after each CJK character to give MigraDoc
legal break points. Both behaviours were confirmed by rasterising a real
published course PDF and inspecting it.
