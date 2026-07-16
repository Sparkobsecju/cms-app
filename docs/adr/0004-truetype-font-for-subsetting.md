# Embed Noto Sans TC as a variable TrueType font, not static OTF

The CJK font is embedded as the variable **TrueType** build of Noto Sans TC
(`NotoSansTC-VF.ttf`, glyf outlines), resolved for every family via a custom
`IFontResolver` with simulated bold/italic. We first shipped the "cleaner" static
**OTF** (CFF) weights, but PDFsharp does **not** subset CFF fonts — it embedded
the full ~11 MB face into every PDF, producing ~10 MB documents. Since the whole
point of the Course PDF is to be emailed to an employer, that size can bounce off
attachment limits. PDFsharp *does* subset TrueType (glyf) fonts to just the
glyphs used, which dropped a real course PDF from ~10 MB to ~0.2 MB. A future
reader will wonder why we picked a variable TTF over a static OTF — this is why.
Noto Sans TC is SIL OFL, so redistribution is fine.
