# 03 — ASP.NET Web Forms (.NET Framework): 100 cautionary samples

A fast-reference catalogue of **100 pitfalls specific to ASP.NET Web Forms on the classic .NET Framework
(4.x)** — ViewState, the page lifecycle, `<%# %>` templates, `web.config`, FormsAuthentication, Membership,
ASMX, and the rest of the Web Forms world. Same teaching spirit as [01](./01-password-hashing-unsalted-sha256.md):
each shows the dangerous shape, the scenario in which it bites, and the rule. These are Web Forms idioms —
not ASP.NET Core, not MVC.

Compressed on purpose. When one bites you for real, promote it to its own numbered file with a full
before/after + attack walkthrough, like sample 01.

**Legend:** ❌ = don't · ✅ = do · 🎯 = the scenario that makes it dangerous · 📏 = the rule.

- [A. ViewState & control state (1–10)](#a-viewstate--control-state)
- [B. Request validation & XSS (11–22)](#b-request-validation--xss)
- [C. web.config hardening (23–34)](#c-webconfig-hardening)
- [D. Forms auth, Membership & web services (35–46)](#d-forms-auth-membership--web-services)
- [E. Data access & redirects (47–56)](#e-data-access--redirects)
- [F. Page lifecycle & session (57–66)](#f-page-lifecycle--session)
- [G. Client script, UpdatePanel & event validation (67–73)](#g-client-script-updatepanel--event-validation)
- [H. File handling & uploads (74–80)](#h-file-handling--uploads)
- [I. Error handling & logging (81–87)](#i-error-handling--logging)
- [J. General .NET Framework pitfalls (88–100)](#j-general-net-framework-pitfalls)

---

## A. ViewState & control state

### 1. `EnableViewStateMac="false"`
❌ Turning off the ViewState MAC on a page or in `<pages enableViewStateMac="false">`. ✅ Leave it on (it's mandatory and non-disableable since .NET 4.5.2).
🎯 With no MAC, an attacker edits the base64 ViewState in the `__VIEWSTATE` field, and the server deserializes their tampered payload as trusted — a classic ViewState RCE gadget path. 📏 Never disable the ViewState MAC.

### 2. Sensitive data in unencrypted ViewState
❌ Stashing a price, a discount, or PII into `ViewState["cost"]` on a page with `ViewStateEncryptionMode="Never"`. ✅ Keep secrets server-side; if it must ride in ViewState, set `ViewStateEncryptionMode="Always"`.
🎯 ViewState is only base64-encoded, not encrypted — anyone decodes `__VIEWSTATE` and reads the plaintext value straight out of the page source. 📏 ViewState is readable by the client; never put secrets in it.

### 3. `ViewStateUserKey` unset (CSRF)
❌ Leaving `Page.ViewStateUserKey` at its default empty value. ✅ In `Page_Init`, set `ViewStateUserKey = Session.SessionID` (or the user id).
🎯 Without a per-user key, an attacker can craft a valid ViewState + event target and CSRF a victim into a postback that performs a state-changing action. 📏 Bind ViewState to the user with `ViewStateUserKey` in `Page_Init`.

### 4. Oversized ViewState
❌ Binding a 10k-row `GridView` with `EnableViewState="true"` so `__VIEWSTATE` balloons to megabytes. ✅ Disable ViewState on read-only data controls; re-bind from the source on postback.
🎯 Every postback ships and re-parses the whole payload, so a large grid becomes a self-inflicted DoS and a slow page for every user. 📏 ViewState off for data you can re-fetch; keep the payload small.

### 5. Trusting ViewState as tamper-proof authorization
❌ `ViewState["IsAdmin"] = true;` then gating an action on it later. ✅ Re-check the role from the server identity on every postback.
🎯 If MAC is off (or a gadget bypasses it), the attacker flips the stored flag and the postback runs the admin branch — the "check" was client-controlled all along. 📏 Authorization facts come from the server per request, never from round-tripped page state.

### 6. Secrets stored in ViewState/hidden fields
❌ Round-tripping an API key, a connection string, or a signed token through `ViewState` or a `<asp:HiddenField>`. ✅ Hold them in `Session` server-side or refetch as needed.
🎯 The value is in the rendered HTML; View Source or Fiddler reveals it, and it also travels on the wire every postback. 📏 Nothing secret goes into ViewState or hidden fields.

### 7. Business values trusted from ViewState/hidden fields
❌ Reading `hdnPrice.Value` back on postback and charging it. ✅ Re-look-up authoritative values (price, quantity limits) from the DB on the server.
🎯 The user edits the hidden field in the browser and buys a $999 item for $0.01 — the round-tripped value was never authoritative. 📏 Recompute money/limits server-side; never trust a value the client held.

### 8. Control state abused for large or sensitive data
❌ Overriding `SaveControlState`/`LoadControlState` to stash big or secret objects (ControlState ignores `EnableViewState="false"`). ✅ Keep ControlState tiny — only what the control truly needs to function.
🎯 ControlState can't be turned off, so it bloats every postback and, if secret, leaks like ViewState does. 📏 ControlState is for minimal, non-sensitive control plumbing only.

### 9. Shared/default `machineKey` weakening ViewState & tickets
❌ Relying on `IsolateApps`/auto-generated keys across a web farm, or copy-pasting one `machineKey` into many unrelated apps. ✅ Set an explicit, per-app, high-entropy `machineKey` and keep it secret.
🎯 If two apps share a key, a ViewState/forms-ticket forged on the low-value app is accepted by the high-value one; a leaked key lets attackers sign their own ViewState. 📏 One strong, secret, app-specific `machineKey`; sync it deliberately across a farm.

### 10. Turning ViewState off where the page depends on it
❌ Blanket `EnableViewState="false"` on a page whose controls need posted-back state to behave. ✅ Disable selectively per control, and re-populate anything you can't round-trip.
🎯 Event args and prior selections vanish, so a "save" handler silently operates on default/empty values and corrupts data. 📏 Understand which controls need ViewState before you switch it off.

---

## B. Request validation & XSS

### 11. `ValidateRequest="false"`
❌ Disabling request validation at page (`<%@ Page ValidateRequest="false" %>`) or app scope to "allow HTML input". ✅ Keep it on; accept rich input through a vetted allow-list sanitizer instead.
🎯 Turning off the guard lets `<script>` in a `TextBox` flow straight to storage and render, opening stored XSS. 📏 Leave request validation on; sanitize deliberately, don't blanket-disable.

### 12. `<%= %>` (unencoded) instead of `<%: %>` (encoded)
❌ `<%= Model.Name %>` in the .aspx markup. ✅ `<%: Model.Name %>` — the `<%:` syntax HTML-encodes automatically (.NET 4+).
🎯 `<%= %>` writes raw, so a name of `<img onerror=…>` executes in every viewer's browser. 📏 Use `<%: %>` for any value that reaches HTML; reserve `<%= %>` for known-safe markup.

### 13. `Response.Write(Request[...])`
❌ `Response.Write(Request.QueryString["msg"]);` in code-behind. ✅ `Response.Write(Server.HtmlEncode(Request.QueryString["msg"]));` or bind through an encoded control.
🎯 The query value reflects straight into the page — `?msg=<script>…` is textbook reflected XSS. 📏 Encode every request value before writing it to the response.

### 14. `Server.HtmlEncode` used in the wrong context
❌ Using `Server.HtmlEncode` for a value going into a JS string or an HTML attribute. ✅ Use the context-correct encoder — `AntiXssEncoder`/`Encoder.JavaScriptEncode`, `Encoder.UrlEncode`, `HtmlAttributeEncode`.
🎯 HTML-encoding doesn't neutralize a `</script>` break-out or an `onclick` attribute injection, so the payload still fires. 📏 Match the encoder to the sink: HTML body vs. attribute vs. JS vs. URL.

### 15. `Literal.Mode = PassThrough` on untrusted data
❌ `litComment.Mode = LiteralMode.PassThrough; litComment.Text = userComment;`. ✅ Default `Encode` mode, or encode before assigning.
🎯 PassThrough emits the string verbatim, so stored markup in a comment runs as script. 📏 Only use `LiteralMode.PassThrough` for HTML you generated and trust.

### 16. Unencoded `Eval()`/`Bind()` in templates
❌ `<ItemTemplate><%# Eval("Comment") %></ItemTemplate>`. ✅ `<%# HttpUtility.HtmlEncode(Eval("Comment")) %>` or bind into a control that encodes.
🎯 Databound expressions write raw HTML, so one malicious row XSSes everyone who views the list. 📏 HTML-encode databound output in `ItemTemplate`/`EditItemTemplate`.

### 17. `GridView` column with `HtmlEncode="false"`
❌ A `BoundField`/`TemplateField` rendering user text with `HtmlEncode="false"`. ✅ Leave `HtmlEncode="true"` (the default) on any user-sourced column.
🎯 Disabling it makes the grid a stored-XSS surface for whatever a user typed into that field. 📏 Keep `HtmlEncode` on for user data in grid columns.

### 18. `requestValidationMode="2.0"` misunderstanding
❌ Flipping `<httpRuntime requestValidationMode="2.0">` to "make things work" without knowing why. ✅ Keep 4.5 (lazy, per-value validation) and handle the specific field that needs raw input explicitly.
🎯 In 2.0 mode validation runs eagerly at BeginRequest and some code paths get exempted, so devs then disable it entirely and lose the XSS guard. 📏 Understand `requestValidationMode` before changing it; don't downgrade to dodge an error.

### 19. `HyperLink.NavigateUrl` from user input
❌ `lnk.NavigateUrl = Request["next"];`. ✅ Validate the scheme/host; allow only relative paths or an allow-list.
🎯 `javascript:alert(document.cookie)` as the URL executes on click, and `https://evil.tld` powers phishing off your domain. 📏 Validate URL scheme and target before assigning it to a link.

### 20. `Label.Text`/`Literal.Text` set from the request without encoding
❌ `lblWelcome.Text = "Hi " + Request.QueryString["u"];`. ✅ Encode first: `lblWelcome.Text = "Hi " + Server.HtmlEncode(...)`.
🎯 A `Label` renders its `Text` as HTML, so the reflected query value becomes reflected XSS. 📏 `Label`/`Literal` `Text` is HTML — encode anything user-sourced.

### 21. Encoding once but in the wrong layer
❌ Encoding on input (storing `&lt;b&gt;`) then also encoding on output, or vice-versa. ✅ Store raw, encode at the point of output, contextually.
🎯 Double-encoding mangles data, and "encoded on input" gives false confidence that leads to a raw sink elsewhere. 📏 Store raw, encode on output — once, at the sink.

### 22. Re-displaying posted HTML in a `TextBox` unencoded
❌ Putting untrusted HTML into `txt.Text` and rendering a multiline `TextBox`. ✅ Rely on the control's default encoding; never build the `<textarea>` body yourself with raw input.
🎯 If you bypass the control and write the markup manually, a `</textarea><script>` break-out injects script. 📏 Let the control render values; don't hand-emit input into markup.

---

## C. web.config hardening

### 23. `<compilation debug="true">` in production
❌ Shipping `debug="true"`. ✅ `debug="false"` in prod (and set `<deployment retail="true">` in machine.config to force it).
🎯 Debug builds disable timeouts, leak richer errors, generate non-batched assemblies, and can expose PDB-level detail to attackers. 📏 `debug="false"` for every deployed site.

### 24. `<customErrors mode="Off">`
❌ Leaving `<customErrors mode="Off">` in prod. ✅ `mode="RemoteOnly"` (or `On`) with a `defaultRedirect`/error page.
🎯 The ASP.NET "yellow screen of death" hands anonymous users full stack traces, file paths, and framework versions — a recon goldmine. 📏 Custom error pages on; detailed errors only locally.

### 25. `<trace enabled="true">` / public `trace.axd`
❌ App-level tracing on, or `trace.axd` reachable in prod. ✅ Disable tracing and block/remove `trace.axd`.
🎯 `trace.axd` dumps recent requests: headers, cookies, session, ViewState, server variables — often including auth tokens. 📏 No page/app tracing and no reachable `trace.axd` in production.

### 26. Hardcoded / shared / weak `machineKey`
❌ A short, guessable `machineKey`, or the same one across untrusted apps, or none in a web farm. ✅ Generate a strong per-app key; sync it across farm nodes on purpose.
🎯 A known key lets an attacker forge ViewState and FormsAuth tickets the server will trust. 📏 One strong, secret `machineKey` per app; farm nodes share the same explicit key.

### 27. Connection strings in plaintext
❌ SQL credentials sitting in `<connectionStrings>` as clear text. ✅ Encrypt the section with `aspnet_regiis -pe "connectionStrings"` (or use Windows integrated auth — no password at all).
🎯 Anyone who reads the file on the server (backup, misconfig, LFI) gets the DB credentials directly. 📏 Encrypt config secrets with `aspnet_regiis`, or use integrated auth.

### 28. `<httpCookies requireSSL="false">`
❌ Leaving auth/session cookies without the Secure flag. ✅ `<httpCookies requireSSL="true">` and serve over HTTPS.
🎯 The cookie travels over any plain-HTTP request, so an on-path attacker sniffs the session/auth token. 📏 Mark cookies Secure; require SSL for them.

### 29. `<httpCookies httpOnlyCookies="false">`
❌ Cookies readable from JavaScript. ✅ `<httpCookies httpOnlyCookies="true">`.
🎯 Any XSS reads `document.cookie` and exfiltrates the session/auth cookie. 📏 Set `httpOnlyCookies="true"` so script can't read cookies.

### 30. No `<httpRuntime maxRequestLength>` cap
❌ Leaving the request size unbounded/high. ✅ Set a sensible `maxRequestLength` (KB) and `requestLengthDiskThreshold`.
🎯 An attacker POSTs huge bodies/uploads to exhaust memory and disk — a cheap DoS. 📏 Cap request size to what the app actually needs.

### 31. Wrong `targetFramework` disabling 4.5 behaviors
❌ Omitting or downgrading `<httpRuntime targetFramework="4.x">`. ✅ Set it to your real target so 4.5+ defaults (validation mode, quirks) apply.
🎯 Missing the attribute silently reverts to 4.0 quirks — eager request validation off-by-default behaviors and other footguns. 📏 Set `targetFramework` to the framework you actually run on.

### 32. `<deployment retail="true">` never set
❌ Trusting every site's own `web.config` to have debug/trace/customErrors right. ✅ Set `<deployment retail="true">` in the server's machine.config.
🎯 One forgotten `debug="true"` or `customErrors="Off"` on any app leaks internals; retail mode forces them all closed. 📏 Enforce production settings server-wide with `deployment retail`.

### 33. `<sessionState cookieless="UseUri">`
❌ Putting the session id into the URL. ✅ `cookieless="UseCookies"` with a secure, HttpOnly cookie.
🎯 The session id lands in logs, `Referer` headers, and shared links, so it's trivially hijacked. 📏 Keep the session id in a cookie, never the URL.

### 34. Directory browsing / leftover `.axd` handlers exposed
❌ IIS directory browsing on, or `elmah.axd`/`webresource.axd` diagnostics reachable anonymously. ✅ Disable directory browsing; lock diagnostic handlers behind auth or remove them.
🎯 Attackers enumerate files and read error/diagnostic dumps that leak paths, queries, and stack traces. 📏 No directory listing and no unauthenticated diagnostic handlers in prod.

---

## D. Forms auth, Membership & web services

### 35. FormsAuth cookie without `requireSSL`
❌ `<forms requireSSL="false" …>`. ✅ `<forms requireSSL="true">` and serve login over HTTPS.
🎯 The forms auth ticket rides a plain-HTTP request and an on-path attacker replays it to impersonate the user. 📏 `requireSSL="true"` on the forms auth cookie.

### 36. No sensible forms timeout / unbounded sliding expiration
❌ `<forms timeout="525600">` or a very long sliding session. ✅ Short `timeout` (e.g. 20–30 min); understand `slidingExpiration` extends it on activity.
🎯 A stolen ticket on a shared/public machine stays valid for days because the session never really expires. 📏 Keep the forms timeout short; bound how long a ticket lives.

### 37. `<forms protection="None">`
❌ Setting `protection="None"` (or `Validation` only) on the forms ticket. ✅ `protection="All"` — encrypt **and** validate the ticket.
🎯 An unencrypted/unsigned ticket can be read and tampered, letting an attacker forge identity/roles inside it. 📏 `protection="All"` so the auth ticket is both encrypted and integrity-checked.

### 38. Membership `passwordFormat="Clear"` (or `Encrypted`)
❌ `SqlMembershipProvider` with `passwordFormat="Clear"`/`"Encrypted"`. ✅ `passwordFormat="Hashed"` — and prefer a modern salted KDF (see [01](./01-password-hashing-unsalted-sha256.md)).
🎯 A DB leak hands over every password in plaintext (Clear) or reversibly (Encrypted, one key away). 📏 Store passwords hashed, never clear or reversibly encrypted.

### 39. `enablePasswordRetrieval="true"`
❌ Allowing "email me my password" via `enablePasswordRetrieval`, which forces a reversible format. ✅ `enablePasswordRetrieval="false"`; offer a one-time reset link instead.
🎯 Retrieval requires storing recoverable passwords, and the reset-by-security-question flow is itself easily guessed/phished. 📏 Never support password retrieval; only time-limited single-use resets.

### 40. Authorization only via `<location>`, not enforced in code
❌ Relying solely on `<location path="Admin"><authorization>…` and doing sensitive work with no code check. ✅ Also verify `User.IsInRole(...)`/identity in the code-behind before acting.
🎯 A path-mapping quirk, a new page outside the `<location>`, or a direct handler call bypasses the config-only gate. 📏 Enforce authorization in code at the action, not just by URL config.

### 41. `Page_Load` doing sensitive work without an auth check
❌ Trusting global config and performing privileged operations unconditionally in `Page_Load`. ✅ Assert the user's identity/role at the top of the handler (and check `IsPostBack`).
🎯 If the page ever slips out of the protected `<location>`, `Page_Load` runs its privileged code for anyone. 📏 Re-verify authorization inside the handler that does the work.

### 42. ASMX `[WebMethod]` exposed without auth
❌ A `*.asmx` `[WebMethod] GetAllUsers()` with no authorization. ✅ Check identity/role inside the method (or front it with an authenticated endpoint).
🎯 The service is callable directly — SOAP/GET/POST — bypassing every page-level `<location>` rule. 📏 Authorize inside web methods; page auth doesn't cover ASMX.

### 43. `[WebMethod(EnableSession=true)]` pitfalls
❌ Enabling session on web methods and relying on it for identity/state casually. ✅ Enable it only when needed; serialize access and don't trust it as an auth boundary.
🎯 Session access serializes requests (perf) and a hijacked session cookie now also drives the web service surface. 📏 Only enable web-method session when required; never treat it as authorization.

### 44. Cookieless forms authentication (ticket in URL)
❌ `<forms cookieless="UseUri">` putting the auth ticket in the URL. ✅ `cookieless="UseCookies"`.
🎯 The auth ticket leaks via `Referer`, logs, and copy-pasted links, so it's trivially replayed. 📏 Keep the FormsAuth ticket in a cookie, never the URL.

### 45. Persistent auth cookie on shared machines
❌ `FormsAuthentication.SetAuthCookie(user, true)` (persistent) by default. ✅ Non-persistent by default; offer "remember me" only with a short, revocable token.
🎯 On a kiosk/shared PC the persistent cookie survives, and the next person is logged in as the victim. 📏 Default to session cookies; make persistence an explicit, bounded opt-in.

### 46. Weak Membership password policy / no lockout
❌ Default `minRequiredPasswordLength`, no `passwordStrengthRegularExpression`, generous `maxInvalidPasswordAttempts`. ✅ Tighten length/complexity and lockout thresholds.
🎯 Short/simple passwords plus lenient lockout let credential stuffing and brute force run cheaply. 📏 Enforce password strength and a real lockout on the provider.

---

## E. Data access & redirects

### 47. `SqlDataSource` with concatenated SQL
❌ Building `SelectCommand` by string concat from a control's value. ✅ Use `<SelectParameters>`/`<asp:ControlParameter>` so values bind as parameters.
🎯 `'; DROP TABLE …--` in the bound control injects into the query the data source runs. 📏 Bind `SqlDataSource` inputs as parameters, never concatenate.

### 48. Raw `SqlCommand` string concatenation in code-behind
❌ `new SqlCommand("SELECT * FROM U WHERE Name='" + txtName.Text + "'", con)`. ✅ `cmd.Parameters.AddWithValue("@Name", txtName.Text)` with a parameterized query.
🎯 Classic SQL injection dumps or destroys data through a text box. 📏 Always parameterize; never build SQL from control text.

### 49. Dynamic `ORDER BY` from a control
❌ `"… ORDER BY " + gridView.SortExpression`. ✅ Whitelist the sort column/direction against a fixed set.
🎯 Parameters can't bind identifiers, so the sort expression becomes an injection point. 📏 Validate sort columns against an allow-list.

### 50. Trusting `Request.QueryString`/`Request.Form` in code-behind
❌ Using `Request["id"]` directly in a query or a file path. ✅ Parse, range-check, and parameterize/confine it first.
🎯 The raw request value drives injection, IDOR, or traversal depending on the sink. 📏 Validate and bind every request value before use.

### 51. `SqlDataSource.FilterExpression` from user input
❌ Interpolating a control value into `FilterExpression`. ✅ Use `FilterParameters` with placeholders.
🎯 `FilterExpression` is a `DataView` RowFilter and unsanitized input can break out of the intended filter. 📏 Parameterize `FilterExpression`; don't concatenate into it.

### 52. Output caching without auth-aware variance
❌ `<%@ OutputCache Duration="60" VaryByParam="none" %>` on a per-user page. ✅ `VaryByParam`/`VaryByCustom="…"` (e.g. by user) or don't cache user-specific output.
🎯 One user's cached page (their name, their data) is served to the next visitor. 📏 Never cache per-user content without varying by the user.

### 53. `Response.Redirect(Request["url"])` open redirect
❌ Redirecting to a URL taken from the query string. ✅ Allow only relative paths or an allow-list of hosts.
🎯 `?url=https://evil.tld` turns your domain into a phishing springboard. 📏 Validate redirect targets before sending them.

### 54. `Response.Redirect` without `endResponse:false`
❌ `Response.Redirect(url)` inside a `try`/catch or after work you need to finish. ✅ `Response.Redirect(url, false); Context.ApplicationInstance.CompleteRequest();`.
🎯 The default overload calls `Response.End()`, which raises `ThreadAbortException` — swallowed by a broad catch or aborting pending work mid-flight. 📏 Redirect with `endResponse:false` and complete the request cleanly.

### 55. `SqlDataSource` with no conflict detection
❌ Update/delete with `ConflictDetection="OverwriteChanges"` (the default). ✅ `ConflictDetection="CompareAllValues"` and handle the zero-rows case.
🎯 Two editors save the same row and the second silently overwrites the first (lost update). 📏 Use optimistic concurrency on data-source updates.

### 56. Connection string / credentials built or stored in code
❌ `"Server=…;User Id=sa;Password=…"` hardcoded in a code-behind. ✅ Read from an encrypted `<connectionStrings>` (or integrated auth); never in source.
🎯 The credential is in source control and the compiled DLL, one decompile from the attacker. 📏 Keep connection secrets out of code entirely.

---

## F. Page lifecycle & session

### 57. Not checking `IsPostBack`
❌ Binding data / re-running side effects in `Page_Load` on every request. ✅ Guard first-load work with `if (!IsPostBack) { … }`.
🎯 A non-idempotent action (insert, email, charge) runs again on every postback, double-processing. 📏 Do one-time load work only when `!IsPostBack`.

### 58. Dynamic controls recreated in the wrong event
❌ Adding controls in `Page_Load`/a click handler and expecting their events + state. ✅ Recreate dynamic controls in `Page_Init` (or `CreateChildControls`) on every request with stable IDs.
🎯 Controls created too late don't get their posted values or events, so handlers silently no-op and data is lost. 📏 Rebuild dynamic controls early (`Init`) every request with consistent IDs.

### 59. Session as a security boundary, no id regeneration on login
❌ Reusing the same `Session.SessionID` before and after authentication. ✅ On successful login, `Session.Abandon()` and issue a fresh session, then set identity.
🎯 An attacker fixes a known session id on the victim; after the victim logs in, the attacker rides the authenticated session (session fixation). 📏 Regenerate the session on privilege change (login).

### 60. Storing identity in Session and trusting it
❌ `Session["UserId"] = id;` then authorizing purely on that. ✅ Derive identity from the authenticated principal (`User.Identity`) and re-check roles server-side.
🎯 If the session id is fixated/hijacked, the attacker inherits whatever `Session["UserId"]` claims. 📏 Trust the auth ticket/principal for identity, not a session slot alone.

### 61. `Session_Start` assumptions
❌ Assuming `Session_Start` means "a new user arrived" or seeding auth state there. ✅ Treat it as best-effort; do auth logic at login, not session start.
🎯 A hijacked/reused session or a crawler skews the assumption, and seeding trust in `Session_Start` becomes a fixation vector. 📏 Don't attach identity or security decisions to `Session_Start`.

### 62. Non-idempotent postbacks with no PRG
❌ Performing an insert on a button click and leaving the page posted (F5 replays it). ✅ Post-Redirect-Get: redirect after the mutation.
🎯 The user refreshes or hits Back and re-submits the order/payment. 📏 Redirect after a state-changing postback to prevent replay.

### 63. Large objects parked in Session (InProc)
❌ Stashing big datasets/DataTables in `Session` per user. ✅ Keep session small; refetch or cache with limits.
🎯 With InProc session, memory balloons and a recycle/farm hop drops the data, breaking flows and inviting OOM. 📏 Session holds small keys, not large payloads.

### 64. Out-of-process session state without securing it
❌ Moving to StateServer/SQL session for a farm but ignoring the transport/serialization. ✅ Secure the state channel and store only serializable, non-sensitive data.
🎯 Session data (possibly identity-related) crosses the wire/DB where it can be read or tampered. 📏 Protect and minimize what goes into out-of-proc session.

### 65. Static page fields shared across requests
❌ A `static` field on a `Page`/control holding per-user data. ✅ Use instance fields, `Session`, or per-request storage.
🎯 Statics are shared across all requests/threads, so one user's data leaks into another's page under load. 📏 Never keep per-request/per-user data in a static field.

### 66. Auth checked in `Page_Load` but child events fire later
❌ Assuming a `Page_Load` guard covers control events raised afterward in the lifecycle. ✅ Also authorize inside the specific event handler that performs the action.
🎯 Control events (`Click`, `RowCommand`) run after `Load`; a code path reached only via the event skips the `Page_Load` check. 📏 Authorize at the handler that does the work, not only in `Load`.

---

## G. Client script, UpdatePanel & event validation

### 67. `EnableEventValidation="false"`
❌ Disabling event validation on a page. ✅ Leave it on (the default).
🎯 An attacker forges a postback targeting a value/command never rendered — e.g. selecting a disabled/hidden option or a list item that wasn't shown — and the server accepts it. 📏 Keep event validation enabled so postback targets must match what was rendered.

### 68. `RegisterStartupScript` with unencoded data
❌ `ClientScript.RegisterStartupScript(GetType(),"k","alert('"+userInput+"');",true);`. ✅ JavaScript-encode the value (`Encoder.JavaScriptEncode`) before embedding it.
🎯 `userInput = "');document.location=…//"` breaks out of the string and runs attacker script (DOM/stored XSS). 📏 JS-encode any server value injected into an emitted script.

### 69. Inline `<script>` built from server data
❌ `<script>var u = '<%= userName %>';</script>` in markup. ✅ Emit data into a JSON/`data-` attribute and read it with encoding, or use `<%: %>`/`Encoder.JavaScriptEncode`.
🎯 A quote or `</script>` in the value breaks the script block and injects code. 📏 Never drop raw server strings into an inline script literal.

### 70. `UpdatePanel` async postback with no auth re-check
❌ Assuming an async (partial) postback is safer or already authorized. ✅ Authorize the triggered handler exactly as a full postback.
🎯 The async postback hits the same server handlers directly, so a missing check is just as exploitable — and easier to script. 📏 Partial postbacks get the same authorization as full ones.

### 71. Trusting `__EVENTTARGET`/`__EVENTARGUMENT`
❌ Acting on the raw `__doPostBack` arguments without validation. ✅ Validate the target/argument against expected values server-side.
🎯 An attacker crafts a postback naming a control/command they shouldn't reach and drives an unintended action. 📏 Treat postback event fields as untrusted input.

### 72. `RegisterClientScriptBlock` without JS-encoding user data
❌ Concatenating request/db values into a script block registered at render. ✅ Encode for the JavaScript context (or serialize as JSON) before registering.
🎯 The unencoded value executes as script for every viewer — reflected or stored XSS via the script sink. 📏 Encode server data for JS before registering a client script block.

### 73. Leaking data into rendered client script
❌ Dumping a whole user/DTO (incl. internal fields) into a JS variable for the page to use. ✅ Emit only the fields the client needs, encoded.
🎯 Internal ids, flags, or PII sit in page source for anyone to read via View Source. 📏 Send the client the minimum, and encode it.

---

## H. File handling & uploads

### 74. `FileUpload` with no size limit
❌ Accepting `FileUpload.PostedFile` of any size. ✅ Check `PostedFile.ContentLength` and cap `maxRequestLength`/`requestLengthDiskThreshold`.
🎯 A huge upload exhausts memory/disk — a cheap DoS. 📏 Enforce an explicit maximum upload size.

### 75. `FileUpload` with no type/extension allow-list
❌ Saving whatever extension the user sends. ✅ Allow-list extensions (and verify content), reject everything else.
🎯 An `.aspx`/`.ashx`/`.config` upload becomes executable code or config tampering on the server. 📏 Allow-list upload extensions; deny by default.

### 76. Saving uploads into a web-accessible folder
❌ `SaveAs(Server.MapPath("~/uploads/" + name))` under the site root. ✅ Store outside the webroot (or in a non-executing, download-only path) and serve via a gated handler.
🎯 The attacker uploads `shell.aspx` and browses to it, getting code execution. 📏 Never let uploaded files land where IIS can execute them.

### 77. Path traversal via `Server.MapPath` + user input
❌ `Server.MapPath("~/files/" + Request["name"])` then read/write. ✅ Strip to `Path.GetFileName`, canonicalize, and confirm the resolved path stays under the root.
🎯 `name = "..\\..\\web.config"` reads or overwrites files outside the intended folder. 📏 Confine resolved paths to the allowed directory.

### 78. Trusting `PostedFile.ContentType`
❌ Deciding a file is an image because `ContentType == "image/jpeg"`. ✅ Sniff magic bytes / re-encode; treat the header as a hint only.
🎯 The client sets `ContentType` freely, so an executable claims to be a JPEG and slips past the check. 📏 Verify file type by content, not the client-supplied MIME.

### 79. Trusting `PostedFile.FileName` verbatim
❌ Using the raw `FileName` (which can include a client path) as the save name. ✅ `Path.GetFileName(...)`, then generate your own safe, unique name.
🎯 A crafted `FileName` injects path segments or overwrites an existing file. 📏 Never persist the client-supplied filename as-is.

### 80. Serving files via `TransmitFile`/`WriteFile` with user path
❌ `Response.TransmitFile(Server.MapPath("~/docs/" + Request["f"]))`. ✅ Map the request to an id, look up the real path server-side, and authorize it.
🎯 `f=..\\..\\web.config` streams arbitrary server files to the attacker (path traversal / arbitrary file read). 📏 Resolve downloads through an authorized id-to-path lookup, not raw input.

---

## I. Error handling & logging

### 81. `Application_Error` that swallows
❌ `Application_Error` calling `Server.ClearError()` and continuing without logging. ✅ Log the exception (with context) via `Server.GetLastError()`, then show a safe page.
🎯 Real failures — including security ones — vanish silently and can't be investigated after a breach. 📏 Log in `Application_Error`; never clear-and-forget.

### 82. Yellow-screen-of-death shown to users
❌ Unhandled exceptions reaching the client because `customErrors` is off. ✅ `customErrors mode="RemoteOnly"` with a friendly error page.
🎯 The stack trace leaks source paths, SQL, and framework versions to attackers. 📏 Users see a generic page; details go to logs only.

### 83. Empty `catch` in code-behind
❌ `try { … } catch { }` around a data op or a security check. ✅ Handle or rethrow; log with context.
🎯 A failed authorization/validation is silently ignored and the request proceeds as if it passed. 📏 Never empty-catch; a swallowed failure is a hidden vulnerability.

### 84. Logging ViewState / tickets / secrets
❌ Dumping `Request.Form` (which contains `__VIEWSTATE`) or the auth cookie into logs. ✅ Log ids and redacted markers only.
🎯 ViewState, session ids, and tokens pile up in log files and breach along with them. 📏 Never log ViewState, cookies, tokens, or full request bodies.

### 85. Catch-and-continue on the `Page.Error` event
❌ Handling `Page_Error`/`Application_Error` to hide the failure and keep rendering. ✅ Fail closed — stop the operation and return an error.
🎯 A security or integrity check throws, gets "handled", and the dangerous path still completes. 📏 A failed check must block the operation, not be smoothed over.

### 86. `trace.axd` / ELMAH exposed in production
❌ Leaving `trace.axd` or `elmah.axd` reachable without auth. ✅ Restrict them by auth/IP or remove them in prod.
🎯 These dashboards expose recent requests, cookies, ViewState, and full stack traces to anyone. 📏 Lock down or remove diagnostic endpoints in production.

### 87. Verbose framework headers
❌ Shipping `X-AspNet-Version`, `X-AspNetMvc-Version`, `X-Powered-By`, `Server`. ✅ Remove them (`<httpRuntime enableVersionHeader="false">`, strip `X-Powered-By` in `<customHeaders>`).
🎯 Version headers tell an attacker exactly which framework CVEs to try. 📏 Suppress version/technology headers.

---

## J. General .NET Framework pitfalls

### 88. `Response.End()` and `ThreadAbortException`
❌ Calling `Response.End()` to "stop" a page. ✅ `Context.ApplicationInstance.CompleteRequest()` (and return), avoiding the abort.
🎯 `Response.End()` throws `ThreadAbortException`; a surrounding broad catch swallows it and the code keeps running past where you meant to stop. 📏 End requests with `CompleteRequest()`, not `Response.End()`.

### 89. `Server.Transfer` preserving state to an unauthorized page
❌ `Server.Transfer("Admin.aspx", true)` carrying form/query across an auth boundary. ✅ Redirect through the normal pipeline so authorization runs, or re-check on the target.
🎯 `Server.Transfer` bypasses a fresh request, so the target's URL-based authorization may not re-evaluate for the transferred user. 📏 Don't use `Server.Transfer` to skip authorization; re-check on the target.

### 90. `async void` page event handlers
❌ `protected async void Button_Click(...)`. ✅ Register async work with `RegisterAsyncTask(new PageAsyncTask(...))` and `Async="true"` on the page.
🎯 The page completes before the awaited work finishes (or an exception crashes the worker thread), producing partial/incorrect responses. 📏 Use `PageAsyncTask`, not `async void`, for async page work.

### 91. Sync-over-async on the request thread
❌ `SomeAsync().Result` / `.Wait()` in a Web Forms page. ✅ `await` through a `PageAsyncTask`.
🎯 The legacy `AspNetSynchronizationContext` deadlocks when you block on a task that needs the same thread. 📏 Don't block on async in the ASP.NET request pipeline.

### 92. `DateTime.Now` instead of `DateTime.UtcNow`
❌ Stamping/comparing with server-local time. ✅ Store and compare in UTC; convert at the display edge.
🎯 DST/timezone drift breaks ticket expiry, scheduling, and audit ordering. 📏 Persist and compare time in UTC.

### 93. Culture-sensitive parsing of user input
❌ `double.Parse(txt.Text)` / `DateTime.Parse(...)` using the request's `CurrentCulture`. ✅ Parse with `CultureInfo.InvariantCulture` (and `TryParse`).
🎯 A visitor whose `Accept-Language` sets a comma-decimal culture makes `"1,5"` parse differently — wrong amounts or exceptions. 📏 Use invariant culture for machine parsing.

### 94. `Random`/`Guid` for security tokens
❌ `new Random().Next()` or `Guid.NewGuid()` as a reset/CSRF/download token. ✅ `RandomNumberGenerator.GetBytes(32)`.
🎯 `Random` is predictable from a few outputs and GUIDs aren't guaranteed unguessable, so the token is forgeable. 📏 Use a CSPRNG for anything an attacker must not predict.

### 95. MD5/SHA1/unsalted hash for passwords
❌ `FormsAuthentication.HashPasswordForStoringInConfigFile(pw,"SHA1")` or bare MD5/SHA-256. ✅ Salted PBKDF2/bcrypt/Argon2 — see [01](./01-password-hashing-unsalted-sha256.md).
🎯 A DB leak means fast, unsalted hashes fall to rainbow tables/GPU cracking in minutes. 📏 Password hashing must be slow and salted.

### 96. Hardcoded secrets in code-behind
❌ `const string ApiKey = "sk_live_…";` in a `.aspx.cs`. ✅ Read from encrypted config; never in source.
🎯 The secret is in source control and the compiled assembly, one decompile away. 📏 No secrets in code or history.

### 97. `HttpContext.Current` on a background/async thread
❌ Spawning a `Task`/thread and reading `HttpContext.Current` there. ✅ Capture the values you need up front and pass them in.
🎯 `HttpContext.Current` is thread/request-bound; off-thread it's `null` or belongs to another request, leaking or crashing. 📏 Don't touch `HttpContext.Current` off the request thread.

### 98. Per-request data in a `static` field
❌ Caching the "current user" or a request value in a `static`/singleton for reuse. ✅ Keep it in the request/session scope.
🎯 Under concurrency, requests overwrite each other's static value and one user acts as another. 📏 Never hold per-request state in shared static state.

### 99. Trusting the Host header / `Request.Url` to build links
❌ Building password-reset or absolute links from `Request.Url.Host`/the `Host` header. ✅ Use a configured canonical base URL.
🎯 An attacker sends a spoofed `Host` header so your reset email links to their domain (host-header poisoning). 📏 Build absolute URLs from trusted config, not the request host.

### 100. Trusting `X-Forwarded-For`/`Request.UserHostAddress` for security
❌ Allow-listing or rate-limiting on a client-supplied `X-Forwarded-For`. ✅ Use the real connection IP, and only trust forwarded headers from a known proxy.
🎯 The attacker sets `X-Forwarded-For` to a trusted IP and bypasses the IP restriction/throttle. 📏 Only trust forwarding headers from a proxy you control.

---

## Using this list

- Skim the category that matches what you're about to write **before** you write it.
- In review, the 📏 lines double as a checklist — a "no" on any is a finding.
- Found a new one the hard way? Add it here; if it's sharp enough to teach from, give it a full
  before/after file like [01](./01-password-hashing-unsalted-sha256.md) and link it from
  [README.md](./README.md).
