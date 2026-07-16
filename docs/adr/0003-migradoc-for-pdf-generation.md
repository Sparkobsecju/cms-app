# Use MigraDoc (on PDFsharp) for PDF generation

The Course PDF is generated with MigraDoc, the higher-level document API built on
PDFsharp. Hard requirement: the library must be free at any company size, forever
— QuestPDF's Community license is free only under ~$1M revenue, so it was
rejected despite the nicer API. MigraDoc/PDFsharp is MIT-licensed with no revenue
threshold. MigraDoc's flowing-document model (paragraphs, tables, automatic page
breaks) also fits variable-length course fields like Objective and Outline better
than PDFsharp's raw coordinate drawing. Font management for CJK content is our
responsibility — the specific font choice and why is recorded in ADR-0004.
