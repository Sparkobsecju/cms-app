# Course PDF is generated server-side in CMS.API

The Course PDF is produced by a server-side endpoint (`GET /api/courses/{id}/pdf`)
in CMS.API, not client-side. The primary consumer is a student on the public
site (a separate codebase) who needs a document for employer approval, so
generation must live somewhere every front-end can call and must render
identically regardless of caller. A server endpoint keyed by course satisfies
both; the admin CMS can reuse the same endpoint for a preview. Client-side
generation was rejected because it would tie the output to one front-end and
duplicate rendering logic.
