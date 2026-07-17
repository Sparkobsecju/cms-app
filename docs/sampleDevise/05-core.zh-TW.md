> 本文件為 [05-core.md](./05-core.md) 的繁體中文翻譯版本。

# 05 — ASP.NET Core（.NET 6–9）：100 則警示範例

一份快速參照型錄，收錄 **100 則 ASP.NET Core 專屬陷阱**，教學精神與
[01](./01-password-hashing-unsalted-sha256.zh-TW.md) 相同。**本專案（`CMS.API`）是一個 .NET 9 Web API — 一個 Core 應用程式 —
因此這裡的每一則都直接適用**，並且有數則點出這個程式碼庫已經做對的地方。範圍是
目前的 minimal-hosting + MVC/Web API + EF Core；不涵蓋 Web Forms，也不涵蓋傳統的 MVC 5。

**圖例：** ❌ = 別這麼做 · ✅ = 該這麼做 · 🎯 = 使其危險的情境 · 📏 = 規則。

- [A. Middleware 管線與 hosting（1–10）](#a-middleware-管線與-hosting)
- [B. 驗證與授權（11–22）](#b-驗證與授權)
- [C. 模型繫結與驗證（23–32）](#c-模型繫結與驗證)
- [D. CSRF、CORS 與 cookies（33–42）](#d-csrfcors-與-cookies)
- [E. Data Protection 與機密（43–51）](#e-data-protection-與機密)
- [F. EF Core 與資料存取（52–61）](#f-ef-core-與資料存取)
- [G. DI、生命週期與資源（62–69）](#g-di生命週期與資源)
- [H. 非同步與效能（70–79）](#h-非同步與效能)
- [I. 錯誤、記錄與可觀測性（80–89）](#i-錯誤記錄與可觀測性)
- [J. 組態、強化、部署與密碼學（90–100）](#j-組態強化部署與密碼學)

---

## A. Middleware 管線與 hosting

### 1. Auth middleware 註冊在 routing 之前
❌ 在 `UseRouting()` 之前呼叫 `UseAuthorization()`。✅ 順序：`UseRouting` → `UseAuthentication` → `UseAuthorization` → endpoints。
🎯 授權在 endpoint（及其 `[Authorize]` metadata）被選定之前就執行了，因此政策會靜默地不生效。📏 先 routing，再 authN，再 authZ，最後 endpoints。

### 2. `UseCors` 放錯位置
❌ 把 `UseCors()` 放在 `UseAuthorization` 之後或 endpoints 之後。✅ 放在 `UseRouting` 之後、`UseAuthentication`/`UseAuthorization` 之前。
🎯 預檢的 `OPTIONS` 請求先撞上 auth 並回 401，導致合法的跨來源呼叫失敗 — 或者 CORS 標頭永遠不會附上。📏 CORS 位於 routing 與 auth 之間。

### 3. 在正式環境使用開發者例外頁面
❌ 無條件呼叫 `app.UseDeveloperExceptionPage()`。✅ 用 `app.Environment.IsDevelopment()` 加以限制；否則使用 `UseExceptionHandler`。
🎯 匿名呼叫者在任何未處理錯誤上都會拿到完整的堆疊追蹤、原始碼片段與組態值。📏 開發者診斷僅限開發環境。

### 4. 正式環境完全沒有例外處理器
❌ 什麼都不註冊，仰賴框架預設的 500。✅ `UseExceptionHandler` / 一個處理用的 middleware（本應用程式有 `ExceptionHandlingMiddleware`，回傳通用的 500）。
🎯 未處理的例外可能洩漏框架細節，並繞過你的記錄／關聯機制。📏 每個應用程式都有一個包山包海的處理器，記錄細節並回傳通用的內容。

### 5. 終端 middleware 短路了整條管線
❌ 在鏈條前段使用 `app.Run(...)`（或忘了 `await next()`）。✅ 用 `app.Use(async (ctx, next) => { …; await next(); })` 做傳遞。
🎯 一個用 `Run` 加入的記錄／標頭 middleware 吞掉了請求；auth 與真正的 endpoint 永遠不會執行。📏 只有最後一個 middleware 是終端；其餘都要呼叫 `next`。

### 6. 開發環境外缺少 `UseHttpsRedirection`/`UseHsts`
❌ 在正式環境以純 HTTP 提供 API。✅ 一律使用 `UseHttpsRedirection()`，開發環境外使用 `UseHsts()`（本應用程式兩者都有）。
🎯 Token 與密碼以明文傳輸；路徑上的攻擊者可讀取或將其降級。📏 除了本機開發外處處使用 HTTPS；正式環境使用 HSTS。

### 7. 位於反向代理後方卻沒有 `UseForwardedHeaders`
❌ 在 nginx/YARP/ingress 後方直接信任 `HttpContext.Connection.RemoteIpAddress`/`Request.Scheme`。✅ 使用 `UseForwardedHeaders`（放在早期），搭配 `ForwardedHeaders.XForwardedFor | XForwardedProto` 以及 `KnownProxies`/`KnownNetworks`。
🎯 應用程式看到的是代理的 IP 與 `http`，於是 HTTPS 重導向出現迴圈，而以 IP 為基礎的速率限制／記錄都怪罪到代理身上。📏 從轉發標頭還原用戶端的 scheme/IP — 但只從受信任的代理來還原。

### 8. 回應快取放在授權之前
❌ 對使用者專屬的回應，在 auth 之前使用 `UseResponseCaching()`。✅ 保持在 auth 之後；正確標記為 private/`VaryBy`，否則就不要快取已驗證的回應。
🎯 因為快取金鑰忽略了身分，某位使用者被快取的回應被送給了另一位使用者。📏 絕不用共用金鑰快取每位使用者專屬的回應。

### 9. `UseStaticFiles` 放在 auth 之前而暴露受保護的檔案
❌ 在任何 auth 之前用 `UseStaticFiles()` 對映一個同時存放敏感檔案的資料夾。✅ 把機密檔案放在 web root 之外；讓受保護的下載走已授權的 endpoint。
🎯 靜態檔案 middleware 會把 root 底下任何東西提供給任何人 — 上面不會執行 `[Authorize]`。📏 靜態 root 只放公開資產。

### 10. 自訂 middleware 從不呼叫或 await `next`
❌ `public Task InvokeAsync(HttpContext ctx) { DoWork(); return next(ctx); }` 遺失了例外，或是射後不理。✅ 在 `async` 方法內 `await next(ctx);`。
🎯 下游的例外未被觀察到，或是回應在工作完成之前就結束了。📏 自訂 middleware 是 `async` 且會 `await` `next`。

---

## B. 驗證與授權

### 11. Endpoints 預設允許
❌ 每個 action 都是公開的，除非有人記得加 `[Authorize]`。✅ 全域的 `FallbackPolicy` 要求已驗證的使用者（本應用程式有設定）／在 minimal-API 群組上使用 `.RequireAuthorization()`。
🎯 一個新加入的 controller 因為屬性被忘記而未受保護就出貨了。📏 預設安全；用 `[AllowAnonymous]` 明確地選擇*退出*。

### 12. 角色與政策混淆
❌ 到處灑 `[Authorize(Roles="Admin")]` 並把角色字串寫死。✅ 對任何超出簡單角色的情況使用具名政策（`AddAuthorizationBuilder().AddPolicy("CanManage", …)`）。
🎯 一次角色改名或一條複合規則（「admin *且*同租戶」）在二十個屬性其中一個被遺漏。📏 把真正的授權規則以政策形式集中表達。

### 13. `[AllowAnonymous]` 加在 controller 層級
❌ 把 `[AllowAnonymous]` 加在整個 controller 上。✅ 只把它限縮到需要的那一個 action（本應用程式在 `CoursePdfController.GetPdf` 上正是這麼做的）。
🎯 之後加進該 controller 的 action 會靜默地繼承匿名存取。📏 逐 action 授予匿名權限，範圍盡可能窄。

### 14. Minimal-API endpoints 沒有 `.RequireAuthorization()`
❌ `app.MapPost("/admin/x", …)` 什麼都不倚靠。✅ `.RequireAuthorization("AdminOnly")`，或一個涵蓋 map 群組的全域 fallback。
🎯 Minimal API 沒有 controller 層級的屬性可繼承；該 endpoint 是完全敞開的。📏 每一條 minimal-API 路由都明確聲明其授權。

### 15. JWT：未驗證 issuer/audience/lifetime/key
❌ 接受任何簽章正確的 token。✅ `TokenValidationParameters` 中 `ValidateIssuer`、`ValidateAudience`、`ValidateLifetime`、`ValidateIssuerSigningKey` 全部設為 `true`（本應用程式四項全設，金鑰 ≥32 位元組）。
🎯 一個為另一個共用金鑰的服務所鑄造的 token，或是一個已過期的 token，被接受了。📏 驗證你所倚賴的每一項 claim 與簽章。

### 16. 預設 `ClockSkew` 掩蓋了過期
❌ 讓 `ClockSkew` 維持其 5 分鐘的預設值，卻假設 `exp` 是精確的。✅ 當你需要嚴格的過期時，設定 `ClockSkew = TimeSpan.Zero`（或一個小而刻意的值）。
🎯 一個「已過期」的 token 還能繼續運作長達 5 分鐘 — 在登出／輪替之後這是個令人意外的時間窗。📏 了解並設定你可接受的時鐘偏差。

### 17. 把 `[ApiController]` 誤認為 auth 關卡
❌ 假設 `[ApiController]` 意味著已驗證。✅ 它只加上繫結／400 慣例；請另外加上 `[Authorize]`（或 fallback 政策）。
🎯 一個標了 `[ApiController]` 但沒有 `[Authorize]` 的 controller 是完全匿名的。📏 `[ApiController]` 關乎模型繫結，不是存取控制。

### 18. 盲目信任 `IClaimsTransformation` / 傳入的 claims
❌ 把 `X-Roles` 標頭或未驗證的 claim 複製進 principal。✅ 在 transformation 內從已驗證的 token 或伺服器儲存推導角色／claims。
🎯 攻擊者透過你的 transformation 所信任的一個標頭自我宣稱 `role=Admin`。📏 授權事實由伺服器驗證，絕不由用戶端宣稱。

### 19. 略過所有權檢查（沒有以資源為基礎的授權）
❌ 只有 `[Authorize]`，然後 `GET /orders/{id}` 回傳任何訂單。✅ 以資源為基礎的授權（`IAuthorizationService.AuthorizeAsync(user, resource, "Owner")`）或 `WHERE Id=@id AND UserId=@me`。
🎯 一個已驗證的使用者把 `id` 遞增就讀到其他使用者的紀錄（IDOR）。📏 有 token ≠ 有權限存取*這個*物件。

### 20. 授權只在用戶端強制執行
❌ 在 Angular 裡把 admin 按鈕藏起來就停手。✅ 在 API 上強制執行（`[Authorize(Roles="Admin")]`，如本應用程式所做）。
🎯 攻擊者直接呼叫 endpoint；那個藏起來的按鈕只是裝飾。📏 UI 的把關是 UX；由伺服器強制執行。

### 21. `MapInboundClaims` / claim 類型的意外
❌ 當 token 用的是 `sub` 而預設對映把它改掉了，你卻去查 `ClaimTypes.NameIdentifier`。✅ 決定 `JwtBearerOptions.MapInboundClaims` 並讀取你所簽發的確切 claim 類型。
🎯 一項授權檢查讀到的 claim 在重新對映後是 `null`，於是失敗開放（fail open）或拋出例外。📏 在一組議定的類型下簽發並讀取 claims。

### 22. 多個 auth scheme，預設值不明確
❌ 註冊了 cookie + JWT 卻沒有明確的預設 scheme。✅ 設定 `DefaultAuthenticateScheme`/`DefaultChallengeScheme`，並在 `[Authorize(AuthenticationSchemes=…)]` 中指名 scheme。
🎯 一個受保護的 API endpoint 用 cookie 重導向來 challenge 而不是回 401，或是對錯誤的 scheme 進行驗證。📏 明確地說明哪個 scheme 守護哪個 endpoint。

---

## C. 模型繫結與驗證

### 23. 直接對 EF entity 過度張貼（over-posting）
❌ `[FromBody] AppUser user` 繫結到含 `IsAdmin`、`RoleIds` 的 EF entity。✅ 繫結一個窄的請求 DTO；只對映白名單欄位。
🎯 使用者張貼了表單從未顯示的額外欄位（`isActive`、roles）並提升權限。📏 接受一個恰好擁有該 action 所編輯欄位的 DTO。

### 24. 停用自動的 400
❌ `SuppressModelStateInvalidFilter = true` 之後忘了檢查 `ModelState`。✅ 保持 `[ApiController]` 的自動 400 開啟，或自己檢查 `ModelState.IsValid`。
🎯 無效／缺漏的欄位越過驗證，以預設值進入你的邏輯。📏 如果你抑制了自動 400，那每一項 `ModelState` 檢查都由你負責。

### 25. 繫結來源混淆／偽造
❌ `[FromRoute]` 對 `[FromQuery]` 對 `[FromBody]` 含糊不清；身分從 body 讀取。✅ 對每個來源都明確；身分取自 `User`，而非 payload。
🎯 攻擊者在 body 裡提供 `userId` 並編輯了別人的帳戶。📏 釘死每一個繫結來源；絕不從請求資料信任身分。

### 26. `[Bind]` include/exclude 誤用
❌ 在共用 entity 上把 `[Bind("Name,Email")]` 當作你唯一的過度張貼防線。✅ 使用 DTO；若要繫結 entity，把伺服器擁有的屬性標上 `[BindNever]`。
🎯 之後新增的一個屬性不在排除清單中，於是變得可被過度張貼。📏 優先用 DTO；防禦性地為伺服器控制的欄位標上 `[BindNever]`。

### 27. 缺少驗證屬性
❌ 傳入的 DTO 上沒有 `[Required]`、`[Range]`、`[StringLength]`。✅ 為每個欄位標註；自動 400 便會強制執行它們。
🎯 一個直接的 API 呼叫送出了 Angular 表單本會阻擋的空／過大／負值。📏 每個傳入欄位都有明確的約束。

### 28. 無界的被繫結集合
❌ 在 body 中接受一個任意大的 `List<T>`。✅ 驗證數量；調整 `MvcOptions.MaxModelBindingCollectionSize`（預設 1024）並限制請求大小。
🎯 一個 10⁶ 項的陣列在繫結過程中就耗盡記憶體，還輪不到你的程式碼執行。📏 在繫結層界定集合大小，而不只是在下游。

### 29. 不可為 null 的實值型別靜默取預設值
❌ DTO 上有個沒加 `[Required]` 的 `int quantity`。✅ 用 `int?` + `[Required]`，或 `[Range]`，來區分「缺漏」與 `0`。
🎯 一個被省略的數值欄位繫結成 `0`，於是跳過了一個只拒絕負值的限制。📏 讓實值型別的「缺漏」可被偵測。

### 30. `[JsonPropertyName]` / 大小寫不符
❌ 假設 JSON 欄位名稱與 C# 屬性相符。✅ 設定 `[JsonPropertyName]` 與一致的 `PropertyNamingPolicy`。
🎯 一個靜默未繫結的欄位被當成其預設值,一個與安全相關的值就此遺失。📏 讓傳輸合約明確且經過測試。

### 31. 從輸入而來的 System.Text.Json 多型
❌ 讓 payload 透過攻擊者可控輸入上的 `[JsonPolymorphic]`/`[JsonDerivedType]` 挑選 CLR 型別。✅ 繫結到一個具體的 DTO；用允許清單驗證判別式（discriminator）。
🎯 攻擊者指定一個危險的衍生型別給反序列化器去建構。📏 由伺服器選擇型別；JSON 不能選。

### 32. 假設 `System.Text.Json` 行為和 `Newtonsoft` 一樣
❌ 預期預設就有不分大小寫比對、容忍註解，或 `$type` 處理。✅ 明確地設定 `JsonSerializerOptions`；絕不重新啟用 `TypeNameHandling` 式的型別內嵌。
🎯 從舊 Newtonsoft 習慣照抄的寬鬆設定重新引入了不安全的反序列化。📏 了解 STJ 的預設值；刻意地選擇加入，絕不加入「由輸入決定型別」。

---

## D. CSRF、CORS 與 cookies

### 33. Cookie 驗證卻沒有防偽（antiforgery）
❌ 以 cookie 驗證的 `POST`/`PUT`/`DELETE` 沒有防偽 token。✅ `[ValidateAntiForgeryToken]` / `AutoValidateAntiforgeryTokenAttribute` 搭配 `AddAntiforgery`。
🎯 一個惡意頁面自動送出表單；瀏覽器附上你的 cookie，寫入就成功了（CSRF）。📏 會改變狀態的 cookie 驗證請求需要防偽 token。

### 34. 把防偽加到純 bearer-token 的 API 上
❌ 把 CSRF token 硬套在一個只用 `Authorization: Bearer` 的 API 上。✅ 倚靠一個事實：JS 必須*明確地*附上 bearer;沒有環境憑證就沒有 CSRF（本應用程式以 bearer 為基礎）。
🎯 白費的複雜度，或更糟 — 一種「這個應用程式已 CSRF 強化」的錯覺,而別處某條 cookie 路徑其實沒有。📏 CSRF 防護針對的是環境憑證（cookies），不是 bearer 標頭。

### 35. `AllowAnyOrigin()` + `AllowCredentials()`
❌ 把萬用字元來源與憑證結合。✅ 一份明確的來源允許清單（本應用程式把 CORS 限制在 loopback）。
🎯 它在執行期拋出例外 — 或者若被硬繞過,任何網站都能以你的使用者身分發出已驗證的呼叫。📏 萬用字元來源與憑證在設計上互斥。

### 36. `SetIsOriginAllowed(_ => true)`
❌ 反射任何來源以躲開「萬用字元加憑證」的錯誤。✅ 對照一份設定好的精確來源允許清單。
🎯 你重建了「允許任何來源加憑證」,每個來源都被信任。📏 回射任意來源就是同一個漏洞多繞了幾步。

### 37. Auth cookie 缺少 `SameSite`
❌ 無正當理由地讓 `SameSite` 未設定/`None`。✅ auth cookie 用 `SameSite=Lax`（或 `Strict`）；只有在有 `Secure` 且確有跨站需求時才用 `None`。
🎯 一個跨站請求攜帶了 auth cookie,使 CSRF 成為可能。📏 auth cookie 預設為 `Lax`/`Strict`。

### 38. Auth cookie 沒有 `Secure`
❌ 在正式環境用 `Secure=false` 的 `CookieOptions`。✅ `Secure=true`（或 `CookieSecurePolicy.Always`）。
🎯 該 cookie 在一次 HTTP 降級中被送出並在路徑上被嗅探。📏 auth cookie 僅限 HTTPS。

### 39. Auth cookie 可被 JS 讀取
❌ session/auth cookie 上 `HttpOnly=false`。✅ `HttpOnly=true`。
🎯 任何 XSS 都能透過 `document.cookie` 讀取該 cookie 並劫持 session。📏 auth cookie 為 `HttpOnly`。

### 40. 把 CORS 當成安全邊界
❌ 假設一份限制性的 CORS 政策能阻止非瀏覽器用戶端。✅ 無論來源為何,都在伺服器端強制執行 auth/authz。
🎯 `curl`/Postman 完全無視 CORS,直接命中 endpoint。📏 CORS 約束的是瀏覽器,不是攻擊者;它不是授權。

### 41. 過度廣泛地暴露標頭／憑證
❌ `WithExposedHeaders("*")` 或在一份寬鬆政策上開啟憑證。✅ 只暴露 SPA 所需的標頭;把憑證限縮到受信任的來源。
🎯 敏感的回應標頭變得可被跨來源讀取。📏 對暴露的標頭列白名單;把帶憑證的表面積降到最小。

### 42. 沒有考慮到預檢（preflight）
❌ 自訂 auth/middleware 對 `OPTIONS` 預檢回了 401 或 500。✅ 讓（正確排序的）CORS middleware 在 auth 之前回應預檢。
🎯 每個非簡單的跨來源請求都失敗,團隊便把 CORS 放寬到萬用字元來「修好」它。📏 在 CORS middleware 中處理預檢,並排在 auth 之前。

---

## E. Data Protection 與機密

### 43. Data Protection 金鑰環未持久化
❌ 在一個沒有持久化的容器裡使用預設的記憶體內／短暫金鑰。✅ 在一個耐久、有備份的儲存上使用 `PersistKeysToFileSystem`/`PersistKeysToDbContext`。
🎯 每次重啟應用程式就鑄造新金鑰 — 所有防偽 token 與 auth cookie 立刻全部失效。📏 把金鑰環持久化到耐久儲存。

### 44. 金鑰環未在多個執行個體間共用
❌ 每個橫向擴充的執行個體各自保有自己的金鑰。✅ 共用的金鑰儲存 + 在所有執行個體上一致的 `SetApplicationName(...)`。
🎯 在負載平衡器後方,執行個體 A 簽發的 cookie/token 被執行個體 B 拒絕 — 隨機登出。📏 同一個應用程式的所有執行個體共用同一個金鑰環與應用程式名稱。

### 45. 機密放在已提交的 `appsettings.json`
❌ 真實的連線字串／金鑰放在 git 裡的 `appsettings.json`。✅ 開發時用 user-secrets,正式環境用環境變數/KeyVault（本應用程式從 DB `SysConfig` 讀取其 JWT 金鑰,不提交任何機密）。
🎯 複製 repo → 跨整段歷史擁有那些憑證。📏 絕不提交任何真實機密。

### 46. 把 user-secrets 與正式環境組態搞混
❌ 把 `dotnet user-secrets` 的值當成你的正式機密儲存出貨。✅ user-secrets 僅限開發;正式環境用環境變數／KeyVault／一個真正的儲存。
🎯 一個只設在開發設定檔的機密在正式環境不存在,於是組態退回一個不安全的預設值。📏 user-secrets 是開發便利工具,不是部署機制。

### 47. 機密經由組態繫結被記錄
❌ 把組態繫結到一個 options 物件然後記錄它,或傾印 `IConfiguration`。✅ 遮蔽;絕不序列化整個 options/config 圖。
🎯 連線字串與金鑰落入記錄與錯誤追蹤器。📏 持有機密的組態物件絕不整個被記錄。

### 48. 對遠端 DB 使用 `Encrypt=false` / `TrustServerCertificate=true`
❌ 把開發用的連線設定（未加密／憑證未驗證）出貨到一個遠端資料庫。✅ 對任何非本機的 DB 加密並驗證憑證。
🎯 路徑上的攻擊者可讀取或竄改所有 DB 流量（只有對同主機的 SQLEXPRESS 才安全）。📏 對非本機資料庫加密 + 驗證憑證。

### 49. 金鑰環已持久化但未在靜態時加密
❌ 在一個沒有 `ProtectKeysWith*` 的共用磁碟區上使用 `PersistKeysToFileSystem`。✅ 用 `ProtectKeysWithCertificate` / DPAPI / KeyVault 在靜態時加密金鑰。
🎯 任何讀取該磁碟區／備份的人都能拿到金鑰,並偽造 cookie 與防偽 token。📏 持久化的金鑰在靜態時也要加密。

### 50. 把 Data Protection 當成通用機密保險庫
❌ 用 `IDataProtector` 去「加密」你必須在金鑰輪替後解密的長壽資料。✅ 用它處理短壽的 payload（token、cookie）;耐久的機密用一個真正的 KMS。
🎯 金鑰依其 90 天週期輪替,舊的密文變得無法解密,或者你停用輪替而削弱了一切。📏 Data Protection 用於短暫、自己擁有的 payload。

### 51. 錯誤／重用的 protector 用途字串（purpose string）
❌ 在不相關的功能之間共用一個 `IDataProtector` 用途。✅ 每個用途使用獨立、穩定的用途字串（`CreateProtector("Course.Pdf.Link")`）。
🎯 為某個用途鑄造的 token 被另一個共用該 protector 的功能所接受。📏 每個隔離的用途一個用途字串。

---

## F. EF Core 與資料存取

### 52. `FromSqlRaw` 搭配字串串接
❌ `FromSqlRaw($"SELECT * FROM Course WHERE Name = '{name}'")`。✅ `FromSqlInterpolated($"… WHERE Name = {name}")` 或參數化（本 repo 全程使用 Dapper 搭配 `@`-參數）。
🎯 `name = "' OR 1=1; --"` 會傾印或刪除資料表。📏 絕不把輸入串接進原始 SQL;使用內插／參數化的形式。

### 53. `ExecuteSqlRaw` 沿用內插習慣
❌ `ExecuteSqlRaw($"UPDATE … SET X = '{v}'")`。✅ `ExecuteSqlInterpolated($"… SET X = {v}")` 或明確的 `SqlParameter`。
🎯 與讀取相同的注入,現在發生在寫入路徑上。📏 `Raw` 意味著*你*必須自行參數化;優先用內插的多載。

### 54. 沒有並行 token
❌ 沒有版本欄位的盲目 `UPDATE`/`SaveChanges`。✅ 一個 `[Timestamp]` `byte[]` rowversion（`IsRowVersion()`）;處理 `DbUpdateConcurrencyException` → 409。
🎯 兩位管理員編輯同一列;第二位靜默地覆寫了第一位（遺失更新）。📏 用 rowversion 防護並行更新。

### 55. 缺少 `Include` 造成 N+1
❌ 迴圈遍歷 entity 並逐項延遲載入一個導覽屬性。✅ `Include(...)` 積極載入;當單一 join 扇出（fan out）時用 `AsSplitQuery()`。
🎯 一個清單檢視在真實資料下發出數千次來回。📏 用盡可能少而正確的查詢載入你將走訪的資料。

### 56. 只讀卻做了追蹤
❌ 唯讀查詢被變更追蹤器追蹤。✅ 唯讀路徑用 `AsNoTracking()`。
🎯 大型讀取 endpoint 浪費記憶體/CPU 去追蹤永遠不會被儲存的 entity。📏 唯讀查詢用 `AsNoTracking`。

### 57. 無界查詢（沒有分頁）
❌ `context.Course.ToListAsync()` 回傳每一列。✅ `Skip/Take`（伺服器分頁）搭配有上限的頁大小與一個總計數。
🎯 一張長大的資料表回傳數百萬列,讓 API OOM,並拖垮 UI。📏 每個清單 endpoint 都分頁並限制其最大值。

### 58. 直接回傳 EF entity
❌ 把 entity（含 `PasswordHash`、內部 FK、導覽屬性）序列化給用戶端。✅ 投影到一個只含你打算暴露欄位的 DTO。
🎯 一個機密欄位或一個延遲導覽屬性搭上了 JSON 回應,或在序列化過程中觸發額外查詢。📏 在邊界把 entity 對映成 DTO。

### 59. 跨多個彙總（aggregate）的寫入沒有交易
❌ 跨彙總的數個 `SaveChanges`/命令,沒有交易。✅ 一個 `IDbContextTransaction`（本應用程式把 create/update/delete 包在一個交易裡）。
🎯 寫入之間的當機留下半套用、不一致的狀態。📏 相關的寫入以原子方式提交。

### 60. 篩選在用戶端評估
❌ `.AsEnumerable().Where(x => Expensive(x))` 或一個 EF 無法翻譯的述詞,把整張表拉進記憶體。✅ 讓 `Where`/`OrderBy` 可翻譯,好讓它們在 SQL 中執行。
🎯 在真實資料下,整張表被具體化並在應用程式中篩選。📏 把篩選與排序推到資料庫去做。

### 61. 正式環境在啟動時自動遷移／`EnsureCreated`
❌ 在正式環境每次應用程式啟動都 `context.Database.Migrate()` 或 `EnsureCreated()`。✅ 把遷移當成一個受控的部署步驟來套用;`EnsureCreated` 只用於測試/原型。
🎯 一個競態的多執行個體推出並行套用結構描述變更,或一個壞的遷移對正式環境自動執行。📏 結構描述變更是一個刻意、有把關的步驟,不是啟動的副作用。

---

## G. DI、生命週期與資源

### 62. 俘虜依賴（captive dependency,scoped 被 singleton 俘虜）
❌ 把一個 scoped 服務（例如 `DbContext`）注入到一個 singleton。✅ 注入 `IServiceScopeFactory` 並為每個工作單位建立一個 scope。
🎯 該 scoped 服務在 singleton 內永久存活 — 陳舊資料、跨請求滲漏、執行緒錯誤。📏 一個 singleton 絕不可持有一個較短壽的依賴。

### 63. 把 `IServiceProvider` 當成服務定位器
❌ 在商業程式碼中到處灑 `provider.GetService<T>()`。✅ 建構式注入依賴;把 `GetRequiredService` 保留給組合根/工廠。
🎯 隱藏的依賴與容器無法驗證的生命週期錯誤。📏 優先用建構式注入,而非從 provider 拉取。

### 64. 每次呼叫都 `new HttpClient()`
❌ 每個請求都建構一個 `HttpClient`。✅ `IHttpClientFactory`（`AddHttpClient`）或一個共用、長壽的用戶端。
🎯 Socket 堆積在 `TIME_WAIT`,機器在負載下用盡連接埠。📏 從工廠取得 `HttpClient`。

### 65. 從根 provider 解析 scoped 服務
❌ 啟動時 `app.Services.GetRequiredService<IScopedThing>()`。✅ `using var scope = provider.CreateScope();` 然後從 `scope.ServiceProvider` 解析。
🎯 從根解析 scoped 會拋出例外,或（更糟）把該服務變成應用程式生命週期的 singleton。📏 scoped 服務在一個 scope 內解析。

### 66. 把 scoped 注入 middleware 建構式
❌ 建構式注入一個 scoped 服務到 middleware。✅ 把它當成 `InvokeAsync(HttpContext, IScopedThing)` 的一個參數注入。
🎯 Middleware 是 singleton,因此該 scoped 服務只被俘虜一次並在所有請求間共用。📏 Middleware 在 `InvokeAsync` 中逐請求取得 scoped 依賴。

### 67. 用錯 `IOptions` 種類
❌ 在你需要逐請求或即時重載組態時用 `IOptions<T>`。✅ `IOptionsSnapshot<T>`（逐請求、scoped）或 `IOptionsMonitor<T>`（singleton + 變更通知）。
🎯 組態編輯或逐租戶的選項永不生效,因為 `IOptions<T>` 是一個凍結的 singleton。📏 讓 options 介面對應你需要的重載/scope。

### 68. `DbContext` 註冊成 singleton
❌ `AddSingleton<AppDbContext>()`。✅ `AddDbContext<AppDbContext>()`（scoped）或給背景工作用的 `DbContextFactory`。
🎯 `DbContext` 不是執行緒安全的;並行請求會破壞它的變更追蹤器與連線。📏 `DbContext` 是 scoped（或 pooled/factory）,絕不是 singleton。

### 69. 未檢查 `GetService` 回傳 null
❌ 對一個未註冊的服務 `provider.GetService<T>().DoThing()`。✅ 當依賴是必要的時候用 `GetRequiredService<T>()`（會清楚拋出例外）。
🎯 一個缺漏的註冊在請求深處以一個含糊的 `NullReferenceException` 浮現。📏 必要的依賴用 `GetRequiredService`。

---

## H. 非同步與效能

### 70. `async void`
❌ 在非事件程式碼中用 `async void Handler()`。✅ `async Task`。
🎯 例外無法被呼叫者捕捉並使行程當機;沒有東西能 await 其完成。📏 用 `async Task`,絕不用 `async void`（UI 事件處理器除外）。

### 71. `.Result` / `.Wait()` / `.GetAwaiter().GetResult()`
❌ 在 controller/handler 中對非同步進行阻塞。✅ 一路 `await` 上去。
🎯 負載下的執行緒池饑餓 → 死結與停滯。📏 別在伺服器程式碼中 sync-over-async。

### 72. 未流動 `CancellationToken`
❌ 在非同步 DB/HTTP 呼叫上忽略 token。✅ 在 action 上接受 `CancellationToken` 並一路貫穿（本應用程式把它傳給每一個 `CommandDefinition`）。
🎯 用戶端斷線了但查詢繼續執行,浪費一條 DB 連線與 CPU。📏 把請求的取消 token 傳播到每一處。

### 73. 熱門函式庫路徑中的 sync-over-async
❌ 把一個非同步呼叫包在 `Task.Run(...).Result` 裡好「弄成同步」。✅ 把整條路徑做成非同步。
🎯 每次呼叫消耗兩條執行緒（被阻塞的那條 + 池工作執行緒）使吞吐量崩潰。📏 非同步是會傳染的 — 從頭到尾都非同步。

### 74. 阻塞 Kestrel 的請求執行緒
❌ 在 handler 中用 `Thread.Sleep`、阻塞式 I/O,或跨一個值得 `await` 的呼叫持有鎖。✅ 非同步 I/O;讓鎖保持微小並遠離請求執行緒。
🎯 少數幾個慢請求耗盡執行緒池,整台伺服器就停止回應。📏 絕不阻塞請求執行緒。

### 75. 在伺服器上用 `Task.Run` 來「卸載」
❌ 在一個請求內用 `Task.Run(() => Work())` 來看起來非同步。✅ 就直接 `await` 真正的非同步 I/O;真正的背景工作用一個背景/hosted 服務。
🎯 `Task.Run` 偷走了伺服器接受請求所需的一條執行緒池執行緒 — 它並不增加容量。📏 在伺服器上,`Task.Run` 不是免費的並行。

### 76. 緩衝而不是串流大型結果
❌ 為一個巨大的回應建立一個龐大的 `List<T>`/字串。✅ `IAsyncEnumerable<T>` / 串流寫入,讓列在產生時就沖出。
🎯 一個數百 MB 的回應被完整具體化在記憶體中並使行程 OOM。📏 串流大型或無界的回應。

### 77. `async` lambda 被強制轉成 `Action`
❌ 在預期 `Action` 之處傳一個 `async` lambda（它會變成 `async void`）。✅ 使用一個接受 `Func<Task>` 的 API。
🎯 lambda 內的例外無法被觀察並可能使行程當機。📏 非同步回呼必須回傳 `Task`,而不是塞進 `Action`。

### 78. 射後不理（fire-and-forget）的 task
❌ 啟動一個 `Task` 卻從不 await/觀察它。✅ await 它,或把長工作交給有錯誤處理的 `IHostedService`/`BackgroundService`。
🎯 該工作在關機/回收時被靜默丟棄,其例外也消失無蹤。📏 每個 task 都被 await,或由一個受監督的背景服務擁有。

### 79. `ValueTask` 被 await 兩次／被儲存
❌ 快取或多次 `await` 一個 `ValueTask`。✅ 恰好 await 它一次,若你必須重用就用 `.AsTask()` 轉換。
🎯 重複 await 一個 `ValueTask` 是未定義行為 — 錯誤的結果或例外。📏 一個 `ValueTask` 是單次 await;別儲存或重複 await 它。

---

## I. 錯誤、記錄與可觀測性

### 80. 正式環境經由 `ProblemDetails` 洩漏堆疊追蹤
❌ 在正式環境用例外文字填入 `ProblemDetails.Detail`/`Extensions`。✅ 通用的 problem 回應;完整細節只進伺服器記錄（本應用程式的 `ExceptionHandlingMiddleware` 回傳通用的 500）。
🎯 錯誤揭露資料表名稱、路徑與框架版本 — 供攻擊者偵察。📏 詳細記錄,通用回應。

### 81. 字串內插的記錄訊息
❌ `_logger.LogInformation($"User {id} did {x}")`。✅ 訊息範本:`_logger.LogInformation("User {UserId} did {Action}", id, x)`。
🎯 你失去了可供查詢的結構化欄位,且未跳脫的值可偽造記錄行。📏 用範本與具名屬性記錄,而不是內插。

### 82. 記錄機密／PII
❌ `_logger.LogInformation("token={Token}", jwt)` 或記錄完整的使用者紀錄。✅ 記錄一個 id 或一個遮蔽的標記。
🎯 機密與 PII 落入記錄儲存／彙整器,並隨其一同外洩。📏 絕不記錄 token、密碼、金鑰或不必要的 PII。

### 83. 從未消毒的輸入偽造記錄
❌ 把含 CRLF 的原始使用者輸入記錄到一個文字 sink。✅ 結構化記錄（sink 會編碼欄位）;絕不用串接輸入來建立記錄行。
🎯 輸入中的換行注入假的記錄項目以掩蓋攻擊者的行蹤。📏 在記錄中把輸入視為資料,絕不視為訊息文字。

### 84. 一個請求全程沒有關聯
❌ 不相關的記錄行,沒有請求/追蹤 id。✅ `TraceIdentifier`/W3C 追蹤內容 / 一個帶關聯 id 的 scope。
🎯 事故期間你無法跨服務把一個請求的事件串在一起。📏 每一行記錄都攜帶一個關聯/追蹤 id。

### 85. 健康檢查 endpoint 洩漏內部資訊
❌ `MapHealthChecks("/health")` 回傳依賴名稱、版本、連線字串 — 且未驗證。✅ 一個最小的公開 liveness 探針;詳細/`/ready` 檢查則需驗證或僅限內部。
🎯 匿名呼叫者列舉出你的後端服務與版本。📏 公開的健康檢查只說 up/down,別無其他。

### 86. `UseStatusCodePages` 資訊洩漏
❌ 在正式環境用囉嗦的狀態碼頁面回射路徑/原因。✅ 通用的狀態回應;把細節留在記錄裡。
🎯 不同的 401/403/404 內容與訊息洩漏了哪些資源存在以及為何存取失敗。📏 狀態回應對攻擊者而言一致且無資訊量。

### 87. 吞掉錯誤的例外篩選器
❌ 一個記錄後回傳 200/空的 `IExceptionFilter`/try-catch。✅ 處理或重新拋出;操作失敗時要失敗關閉（fail closed）。
🎯 一個失敗的安全檢查或寫入被掩蓋,請求卻「成功」了。📏 一個失敗的操作絕不可回傳成功。

### 88. `IncludeErrorDetails` / 開發診斷未關閉
❌ 在正式環境的回應中留著 `JwtBearerOptions.IncludeErrorDetails = true`（或類似項）。✅ 在開發環境外抑制詳細的 auth 錯誤原因。
🎯 `WWW-Authenticate`/錯誤內容告訴攻擊者一個 token 究竟為何被拒（過期 vs. 簽章錯誤 vs. audience 錯誤）。📏 對用戶端而言,auth 失敗是通用的。

### 89. 在熱門路徑上過度記錄
❌ 逐列或逐請求以大 payload `LogInformation`/`LogDebug`。✅ 以適當的層級記錄;對高量事件取樣或彙整。
🎯 記錄量膨脹了成本、掩蓋了真正的訊號,並可能對記錄管線本身造成 DoS。📏 記錄層級與量對應事件的重要性。

---

## J. 組態、強化、部署與密碼學

### 90. 正式環境暴露 Swagger/OpenAPI
❌ 無條件呼叫 `UseSwagger()`/`UseSwaggerUI()`。✅ 用 `app.Environment.IsDevelopment()` 加以限制（本應用程式有做）。
🎯 完整的 API 表面、結構描述,有時還有一個「試試看」用戶端,被交給匿名呼叫者。📏 API 探索器留在開發環境。

### 91. 沒有請求大小限制
❌ 接受任意大的 body/上傳。✅ Kestrel `MaxRequestBodySize`、`[RequestSizeLimit]` 與 multipart 限制;避免全面的 `[DisableRequestSizeLimit]`。
🎯 單一個巨大上傳耗盡記憶體/磁碟並使行程倒下。📏 每個 endpoint 都有一個明確的最大 body 大小。

### 92. Auth endpoint 沒有速率限制
❌ 無限制的登入/token/重設嘗試。✅ 在 auth 路由上用內建的速率限制器（`AddRateLimiter` + `UseRateLimiter`,.NET 7+）,加上鎖定/退避。
🎯 憑證填充（credential stuffing）與暴力破解暢行無阻。📏 對驗證及其他易遭濫用的 endpoint 加以節流。

### 93. 對含機密的回應在 HTTPS 上做回應壓縮
❌ 對混合了機密與攻擊者可影響內容的回應設 `EnableForHttps = true`。✅ 別壓縮同時含機密與被回射輸入的回應（BREACH）。
🎯 壓縮比的旁通道讓攻擊者逐位元組還原一個 CSRF token/機密。📏 別在 TLS 上把機密 + 攻擊者可控資料一起壓縮。

### 94. 缺少安全標頭 / CSP
❌ 沒有 `Content-Security-Policy`、`X-Content-Type-Options: nosniff`、`X-Frame-Options`/`frame-ancestors`。✅ 在邊緣或 middleware 發出一組基準標頭集。
🎯 XSS payload 暢行無阻;MIME 嗅探把一個上傳變成腳本;應用程式在框架中被點擊劫持。📏 出貨一組基準安全標頭集。

### 95. 正式環境 `AllowedHosts` 用萬用字元
❌ `"AllowedHosts": "*"`。✅ 列出應用程式所服務的確切主機名稱。
🎯 主機標頭攻擊污染絕對 URL（密碼重設連結、快取金鑰）。📏 把 `AllowedHosts` 釘死在你真實的網域。

### 96. 正式環境用 `ASPNETCORE_ENVIRONMENT=Development`
❌ 部署時把環境留在 Development。✅ 設成 `Production`;在機器上驗證 `IsDevelopment()` 為 false。
🎯 每個僅限開發的關卡一次全部打開 — Swagger、開發者例外頁面、囉嗦錯誤。📏 部署的環境名稱符合現實。

### 97. 用 `Random` 產生安全 token
❌ 用 `new Random().Next()` / `Guid.NewGuid()` 當重設或下載 token。✅ `RandomNumberGenerator.GetBytes(32)`。
🎯 `Random` 從少數幾個輸出就可預測;GUID 也不保證無法猜測。📏 任何攻擊者不得預測的東西都用 CSPRNG。

### 98. 用 `==` 比較 token/HMAC
❌ 對一個 token 或簽章 `if (provided == expected)`。✅ `CryptographicOperations.FixedTimeEquals(a, b)`。
🎯 字串 `==` 在第一個相異位元組就短路;時序逐位元組洩漏機密。📏 對所有機密材料採用常數時間比較。

### 99. `DateTime.Now` 與對文化敏感的剖析
❌ 用 `DateTime.Now` 當 token 過期/時間戳;用環境文化 `decimal.Parse(s)`。✅ 處處用 `DateTime.UtcNow`;機器面向的剖析/格式化用 `CultureInfo.InvariantCulture`。
🎯 DST/時區漂移破壞 `exp` 檢查;一個逗號小數的地區設定把一個金額剖析錯誤。📏 時間用 UTC,機器邏輯用 InvariantCulture。

### 100. 寫死的金鑰與給機密用的快速雜湊
❌ 原始碼中 `var key = "s3cr3t";`;密碼用裸 `SHA256`。✅ 金鑰用執行期組態（本應用程式從 DB `SysConfig` 讀取 JWT 金鑰）;密碼用 PBKDF2（見 [範例 01](./01-password-hashing-unsalted-sha256.zh-TW.md)),任何牽涉金錢的用 `decimal`。
🎯 任何有 repo/歷史存取權的人可永久偽造 token;一張外洩的雜湊表在數分鐘內破解。📏 金鑰活在組態裡,密碼用慢速加鹽 KDF,金錢用 `decimal`。

---

## 使用這份清單

- 在你動筆之前**先**瀏覽符合你即將撰寫內容的類別 — 這其中大多是
  在 `Program.cs` 裡一次做成、且容易靜默地做錯的接線／組態決策。
- 在審查中,那些 📏 行兼作檢查清單 — 任何一項為「否」就是一個發現。
- 用慘痛方式發現了一個新的?把它加到這裡;若它夠鋒利、足以拿來教學,就給它一個
  完整的前後對照檔案,如 [01](./01-password-hashing-unsalted-sha256.zh-TW.md),並從
  [README.md](./README.zh-TW.md) 連結它。
