> 本文件為 [03-webform.md](./03-webform.md) 的繁體中文翻譯版本。

# 03 — ASP.NET Web Forms（.NET Framework）：100 個警世範例

一份快速參考型錄，收錄 **100 個專屬於傳統 .NET Framework（4.x）上 ASP.NET Web Forms 的陷阱** —— ViewState、頁面生命週期、`<%# %>` 範本、`web.config`、FormsAuthentication、Membership、ASMX，以及 Web Forms 世界的其餘一切。教學精神與 [01](./01-password-hashing-unsalted-sha256.zh-TW.md) 相同：每一則都呈現危險的寫法、它會咬人的情境，以及規則。這些是 Web Forms 的慣用寫法 —— 不是 ASP.NET Core，也不是 MVC。

刻意壓縮過。當其中一則真的咬到你時，就把它升級成獨立的編號檔案，附上完整的前後對照與攻擊演練，就像範例 01 那樣。

**圖例：** ❌ = 別這麼做 · ✅ = 該這麼做 · 🎯 = 使其危險的情境 · 📏 = 規則。

- [A. ViewState 與控制項狀態（1–10）](#a-viewstate-與控制項狀態)
- [B. 請求驗證與 XSS（11–22）](#b-請求驗證與-xss)
- [C. web.config 強化（23–34）](#c-webconfig-強化)
- [D. Forms 驗證、Membership 與 Web 服務（35–46）](#d-forms-驗證、membership-與-web-服務)
- [E. 資料存取與重新導向（47–56）](#e-資料存取與重新導向)
- [F. 頁面生命週期與 Session（57–66）](#f-頁面生命週期與-session)
- [G. 用戶端指令碼、UpdatePanel 與事件驗證（67–73）](#g-用戶端指令碼、updatepanel-與事件驗證)
- [H. 檔案處理與上傳（74–80）](#h-檔案處理與上傳)
- [I. 錯誤處理與記錄（81–87）](#i-錯誤處理與記錄)
- [J. 一般 .NET Framework 陷阱（88–100）](#j-一般-net-framework-陷阱)

---

## A. ViewState 與控制項狀態

### 1. `EnableViewStateMac="false"`
❌ 在某個頁面上或在 `<pages enableViewStateMac="false">` 中關閉 ViewState MAC。 ✅ 讓它保持開啟（自 .NET 4.5.2 起它是強制且無法停用的）。
🎯 沒有 MAC 時，攻擊者可編輯 `__VIEWSTATE` 欄位裡以 base64 編碼的 ViewState，伺服器便會把他們竄改過的酬載當成可信任的內容反序列化 —— 這是典型的 ViewState RCE gadget 路徑。 📏 絕不要停用 ViewState MAC。

### 2. 未加密的 ViewState 中放敏感資料
❌ 在 `ViewStateEncryptionMode="Never"` 的頁面上把價格、折扣或個資塞進 `ViewState["cost"]`。 ✅ 讓機密留在伺服器端；若非得隨 ViewState 傳送不可，就設 `ViewStateEncryptionMode="Always"`。
🎯 ViewState 只是 base64 編碼，並非加密 —— 任何人解碼 `__VIEWSTATE` 就能直接從頁面原始碼讀出明文值。 📏 ViewState 用戶端可讀；絕不要把機密放進去。

### 3. `ViewStateUserKey` 未設定（CSRF）
❌ 讓 `Page.ViewStateUserKey` 維持其預設的空值。 ✅ 在 `Page_Init` 中設定 `ViewStateUserKey = Session.SessionID`（或使用者 id）。
🎯 沒有針對每位使用者的金鑰，攻擊者可製作一份有效的 ViewState 加上事件目標，並以 CSRF 誘使受害者觸發一次執行狀態變更動作的回傳（postback）。 📏 在 `Page_Init` 中用 `ViewStateUserKey` 把 ViewState 綁定到使用者。

### 4. 過大的 ViewState
❌ 用 `EnableViewState="true"` 綁定一個一萬列的 `GridView`，讓 `__VIEWSTATE` 膨脹到數 MB。 ✅ 對唯讀資料控制項停用 ViewState；在回傳時從來源重新繫結。
🎯 每次回傳都會傳送並重新剖析整個酬載，於是一個大型 grid 就成了自作自受的 DoS，也讓每位使用者的頁面變慢。 📏 對可重新抓取的資料關閉 ViewState；讓酬載保持精簡。

### 5. 把 ViewState 當成防竄改的授權依據來信任
❌ `ViewState["IsAdmin"] = true;` 然後之後靠它來把關某個動作。 ✅ 在每次回傳時，從伺服器端身分重新檢查角色。
🎯 若 MAC 關閉（或某個 gadget 繞過它），攻擊者翻轉那個儲存的旗標，回傳就會執行 admin 分支 —— 那個「檢查」從頭到尾都由用戶端控制。 📏 授權事實每個請求都由伺服器提供，絕不來自往返傳遞的頁面狀態。

### 6. 機密存放在 ViewState／隱藏欄位
❌ 讓 API 金鑰、連線字串或已簽章權杖往返於 `ViewState` 或 `<asp:HiddenField>`。 ✅ 把它們留在伺服器端的 `Session`，或視需要重新抓取。
🎯 該值就在算繪出的 HTML 裡；檢視原始碼或用 Fiddler 就能揭露它，而且它每次回傳也都在線路上傳輸。 📏 任何機密都不進 ViewState 或隱藏欄位。

### 7. 從 ViewState／隱藏欄位信任商業數值
❌ 在回傳時讀回 `hdnPrice.Value` 並照它收費。 ✅ 在伺服器端從資料庫重新查詢具權威性的值（價格、數量上限）。
🎯 使用者在瀏覽器裡編輯隱藏欄位，用 0.01 美元買下一件 999 美元的商品 —— 那個往返的值從來就不具權威性。 📏 在伺服器端重新計算金額／上限；絕不信任用戶端曾持有的值。

### 8. 控制項狀態（control state）被濫用來裝大量或敏感資料
❌ 覆寫 `SaveControlState`／`LoadControlState` 來塞進龐大或機密的物件（ControlState 會忽略 `EnableViewState="false"`）。 ✅ 讓 ControlState 保持極小 —— 只放控制項真正運作所需的內容。
🎯 ControlState 無法關閉，所以它會讓每次回傳都變臃腫，而且若含機密，就會像 ViewState 一樣外洩。 📏 ControlState 只用於最精簡、非敏感的控制項底層管線。

### 9. 共用／預設的 `machineKey` 削弱 ViewState 與票證
❌ 在網頁伺服器陣列（web farm）中倚賴 `IsolateApps`／自動產生的金鑰，或把同一組 `machineKey` 複製貼上到多個彼此無關的應用程式。 ✅ 設定一組明確、每個應用程式各自、高熵的 `machineKey`，並保密。
🎯 若兩個應用程式共用一把金鑰，在低價值應用程式上偽造的 ViewState／forms 票證會被高價值的那個接受；金鑰一旦外洩，攻擊者就能簽署自己的 ViewState。 📏 每個應用程式一把強而保密、專屬的 `machineKey`；在陣列間刻意同步它。

### 10. 在頁面倚賴 ViewState 之處把它關掉
❌ 在一個控制項需要回傳狀態才能正常運作的頁面上一律 `EnableViewState="false"`。 ✅ 逐一針對個別控制項選擇性停用，並重新填入任何無法往返的內容。
🎯 事件引數與先前的選取會消失，於是「儲存」處理常式默默地對預設／空值運作並毀損資料。 📏 在關閉 ViewState 之前，先弄清楚哪些控制項需要它。

---

## B. 請求驗證與 XSS

### 11. `ValidateRequest="false"`
❌ 在頁面層級（`<%@ Page ValidateRequest="false" %>`）或應用程式範圍停用請求驗證來「允許 HTML 輸入」。 ✅ 讓它保持開啟；改為透過一個經審核的允許清單（allow-list）淨化器來接受豐富輸入。
🎯 關掉這道防護，就讓 `TextBox` 裡的 `<script>` 直接流入儲存與算繪，開啟儲存型 XSS。 📏 讓請求驗證保持開啟；刻意地淨化，別一律停用。

### 12. `<%= %>`（未編碼）而非 `<%: %>`（已編碼）
❌ 在 .aspx 標記中寫 `<%= Model.Name %>`。 ✅ `<%: Model.Name %>` —— `<%:` 語法會自動做 HTML 編碼（.NET 4+）。
🎯 `<%= %>` 會寫出原始內容，於是一個名為 `<img onerror=…>` 的名字會在每位觀看者的瀏覽器裡執行。 📏 對任何會進到 HTML 的值使用 `<%: %>`；把 `<%= %>` 保留給已知安全的標記。

### 13. `Response.Write(Request[...])`
❌ 在程式碼後置（code-behind）中寫 `Response.Write(Request.QueryString["msg"]);`。 ✅ `Response.Write(Server.HtmlEncode(Request.QueryString["msg"]));`，或透過一個會編碼的控制項來繫結。
🎯 查詢值直接反射進頁面 —— `?msg=<script>…` 是教科書等級的反射型 XSS。 📏 在把每個請求值寫進回應之前先編碼它。

### 14. `Server.HtmlEncode` 用在錯誤的情境
❌ 對一個要進到 JS 字串或 HTML 屬性的值使用 `Server.HtmlEncode`。 ✅ 使用符合情境的編碼器 —— `AntiXssEncoder`／`Encoder.JavaScriptEncode`、`Encoder.UrlEncode`、`HtmlAttributeEncode`。
🎯 HTML 編碼無法中和 `</script>` 的跳脫或 `onclick` 屬性注入，所以酬載照樣觸發。 📏 讓編碼器對應到接收點（sink）：HTML 主體 vs. 屬性 vs. JS vs. URL。

### 15. 對不可信資料使用 `Literal.Mode = PassThrough`
❌ `litComment.Mode = LiteralMode.PassThrough; litComment.Text = userComment;`。 ✅ 使用預設的 `Encode` 模式，或在指派前先編碼。
🎯 PassThrough 會原封不動地輸出字串，於是留言中儲存的標記便當成指令碼執行。 📏 只對你自己產生且信任的 HTML 使用 `LiteralMode.PassThrough`。

### 16. 範本中未編碼的 `Eval()`／`Bind()`
❌ `<ItemTemplate><%# Eval("Comment") %></ItemTemplate>`。 ✅ `<%# HttpUtility.HtmlEncode(Eval("Comment")) %>`，或繫結進一個會編碼的控制項。
🎯 資料繫結運算式會寫出原始 HTML，於是一列惡意資料就對所有檢視清單的人發動 XSS。 📏 在 `ItemTemplate`／`EditItemTemplate` 中對資料繫結輸出做 HTML 編碼。

### 17. `GridView` 欄位設 `HtmlEncode="false"`
❌ 用 `HtmlEncode="false"` 算繪使用者文字的 `BoundField`／`TemplateField`。 ✅ 對任何來源為使用者的欄位保留 `HtmlEncode="true"`（預設值）。
🎯 停用它會讓 grid 成為使用者在該欄位所輸入內容的儲存型 XSS 攻擊面。 📏 對 grid 欄位中的使用者資料保持 `HtmlEncode` 開啟。

### 18. 誤解 `requestValidationMode="2.0"`
❌ 在不知其所以然的情況下，把 `<httpRuntime requestValidationMode="2.0">` 翻成 2.0 來「讓東西動起來」。 ✅ 維持 4.5（延遲、逐值驗證），並明確處理那個確實需要原始輸入的特定欄位。
🎯 在 2.0 模式下，驗證會在 BeginRequest 時積極執行，而某些程式碼路徑會被豁免，於是開發者乾脆整個停用它，失去 XSS 防護。 📏 在變更 `requestValidationMode` 之前先弄懂它；別為了閃避錯誤而降級。

### 19. `HyperLink.NavigateUrl` 來自使用者輸入
❌ `lnk.NavigateUrl = Request["next"];`。 ✅ 驗證 scheme／host；只允許相對路徑或一份允許清單。
🎯 把 `javascript:alert(document.cookie)` 當成 URL，點擊時就會執行；而 `https://evil.tld` 則以你的網域為跳板進行釣魚。 📏 在把 URL 指派給連結之前，先驗證其 scheme 與目標。

### 20. `Label.Text`／`Literal.Text` 從請求設定卻未編碼
❌ `lblWelcome.Text = "Hi " + Request.QueryString["u"];`。 ✅ 先編碼：`lblWelcome.Text = "Hi " + Server.HtmlEncode(...)`。
🎯 `Label` 會把它的 `Text` 當成 HTML 算繪，於是反射的查詢值就成了反射型 XSS。 📏 `Label`／`Literal` 的 `Text` 就是 HTML —— 對任何來源為使用者的內容都要編碼。

### 21. 有編碼但編在錯誤的層
❌ 在輸入端編碼（儲存 `&lt;b&gt;`）然後在輸出端又編碼一次，反之亦然。 ✅ 儲存原始內容，在輸出點依情境編碼。
🎯 雙重編碼會弄壞資料，而「輸入端已編碼」會帶來錯誤的安全感，導致他處出現一個原始接收點。 📏 儲存原始內容，在輸出時編碼 —— 在接收點編碼一次。

### 22. 在 `TextBox` 中未編碼地重新顯示回傳的 HTML
❌ 把不可信 HTML 放進 `txt.Text` 並算繪一個多行 `TextBox`。 ✅ 倚賴控制項的預設編碼；絕不要自己用原始輸入去建構 `<textarea>` 主體。
🎯 若你繞過控制項並手動寫出標記，一個 `</textarea><script>` 的跳脫就會注入指令碼。 📏 讓控制項去算繪值；別自己把輸入手工吐進標記裡。

---

## C. web.config 強化

### 23. 正式環境中的 `<compilation debug="true">`
❌ 帶著 `debug="true"` 上線。 ✅ 正式環境用 `debug="false"`（並在 machine.config 中設 `<deployment retail="true">` 強制執行）。
🎯 debug 組建會停用逾時、洩漏更豐富的錯誤、產生非批次的組件，並可能把 PDB 層級的細節暴露給攻擊者。 📏 每個已部署的站台都用 `debug="false"`。

### 24. `<customErrors mode="Off">`
❌ 在正式環境保留 `<customErrors mode="Off">`。 ✅ 用 `mode="RemoteOnly"`（或 `On`）搭配 `defaultRedirect`／錯誤頁面。
🎯 ASP.NET 的「黃色死亡畫面」會把完整的堆疊追蹤、檔案路徑與框架版本交給匿名使用者 —— 一座偵察金礦。 📏 開啟自訂錯誤頁面；詳細錯誤只在本機顯示。

### 25. `<trace enabled="true">`／公開的 `trace.axd`
❌ 開啟應用程式層級追蹤，或在正式環境可存取 `trace.axd`。 ✅ 停用追蹤並封鎖／移除 `trace.axd`。
🎯 `trace.axd` 會傾印近期請求：標頭、cookie、session、ViewState、伺服器變數 —— 常常包含驗證權杖。 📏 正式環境不開頁面／應用程式追蹤，也不留可存取的 `trace.axd`。

### 26. 寫死／共用／脆弱的 `machineKey`
❌ 一組短、易猜的 `machineKey`，或在互不信任的應用程式間共用同一組，或在網頁伺服器陣列裡完全不設。 ✅ 為每個應用程式產生一把強金鑰；刻意在陣列節點間同步它。
🎯 已知的金鑰讓攻擊者能偽造伺服器將信任的 ViewState 與 FormsAuth 票證。 📏 每個應用程式一把強而保密的 `machineKey`；陣列節點共用同一把明確的金鑰。

### 27. 明文的連線字串
❌ SQL 認證以明文放在 `<connectionStrings>` 裡。 ✅ 用 `aspnet_regiis -pe "connectionStrings"` 加密該區段（或使用 Windows 整合式驗證 —— 根本沒有密碼）。
🎯 任何能讀到伺服器上該檔案的人（備份、設定失誤、LFI）都能直接取得資料庫認證。 📏 用 `aspnet_regiis` 加密設定機密，或使用整合式驗證。

### 28. `<httpCookies requireSSL="false">`
❌ 讓驗證／session cookie 沒有 Secure 旗標。 ✅ `<httpCookies requireSSL="true">` 並透過 HTTPS 提供服務。
🎯 cookie 會隨任何純 HTTP 請求傳輸，於是路徑中的攻擊者就能嗅探到 session／驗證權杖。 📏 把 cookie 標記為 Secure；要求它們走 SSL。

### 29. `<httpCookies httpOnlyCookies="false">`
❌ cookie 可被 JavaScript 讀取。 ✅ `<httpCookies httpOnlyCookies="true">`。
🎯 任何 XSS 都能讀 `document.cookie` 並把 session／驗證 cookie 外傳。 📏 設 `httpOnlyCookies="true"`，讓指令碼無法讀取 cookie。

### 30. 沒有 `<httpRuntime maxRequestLength>` 上限
❌ 讓請求大小無上限／過高。 ✅ 設定合理的 `maxRequestLength`（KB）與 `requestLengthDiskThreshold`。
🎯 攻擊者 POST 巨大的主體／上傳來耗盡記憶體與磁碟 —— 一種廉價的 DoS。 📏 把請求大小限制在應用程式實際所需的範圍。

### 31. 錯誤的 `targetFramework` 停用了 4.5 行為
❌ 省略或降級 `<httpRuntime targetFramework="4.x">`。 ✅ 把它設成你真正的目標，讓 4.5+ 的預設值（驗證模式、相容性怪癖）生效。
🎯 缺少該屬性會默默地回退到 4.0 的怪癖 —— 積極請求驗證的預設關閉行為以及其他自傷武器。 📏 把 `targetFramework` 設成你實際執行所在的框架。

### 32. 從未設定 `<deployment retail="true">`
❌ 信任每個站台自己的 `web.config` 都把 debug／trace／customErrors 設對了。 ✅ 在伺服器的 machine.config 中設 `<deployment retail="true">`。
🎯 任一應用程式上一個被遺忘的 `debug="true"` 或 `customErrors="Off"` 就會洩漏內部；retail 模式會強制把它們全部關閉。 📏 用 `deployment retail` 在整台伺服器層級強制執行正式環境設定。

### 33. `<sessionState cookieless="UseUri">`
❌ 把 session id 放進 URL。 ✅ `cookieless="UseCookies"` 搭配一個安全、HttpOnly 的 cookie。
🎯 session id 會落入記錄檔、`Referer` 標頭與分享的連結，於是輕易就被劫持。 📏 把 session id 放在 cookie，絕不放 URL。

### 34. 目錄瀏覽／殘留的 `.axd` 處理常式被暴露
❌ 開啟 IIS 目錄瀏覽，或 `elmah.axd`／`webresource.axd` 診斷可被匿名存取。 ✅ 停用目錄瀏覽；把診斷處理常式鎖在驗證之後或移除它們。
🎯 攻擊者列舉檔案並讀取洩漏路徑、查詢與堆疊追蹤的錯誤／診斷傾印。 📏 正式環境不留目錄清單，也不留未經驗證的診斷處理常式。

---

## D. Forms 驗證、Membership 與 Web 服務

### 35. FormsAuth cookie 沒有 `requireSSL`
❌ `<forms requireSSL="false" …>`。 ✅ `<forms requireSSL="true">` 並透過 HTTPS 提供登入。
🎯 forms 驗證票證會隨純 HTTP 請求傳輸，路徑中的攻擊者重放它就能冒充該使用者。 📏 對 forms 驗證 cookie 設 `requireSSL="true"`。

### 36. 沒有合理的 forms 逾時／無上限的滑動到期
❌ `<forms timeout="525600">` 或極長的滑動 session。 ✅ 短逾時（例如 20–30 分鐘）；理解 `slidingExpiration` 會在活動時延長它。
🎯 在共用／公用機器上被偷走的票證能維持有效好幾天，因為 session 從未真正到期。 📏 讓 forms 逾時保持短；限制一張票證能存活多久。

### 37. `<forms protection="None">`
❌ 對 forms 票證設 `protection="None"`（或只設 `Validation`）。 ✅ `protection="All"` —— 對票證加密**並**驗證。
🎯 未加密／未簽章的票證可被讀取與竄改，讓攻擊者能在其中偽造身分／角色。 📏 用 `protection="All"`，讓驗證票證同時被加密與完整性檢查。

### 38. Membership `passwordFormat="Clear"`（或 `Encrypted`）
❌ 帶 `passwordFormat="Clear"`／`"Encrypted"` 的 `SqlMembershipProvider`。 ✅ `passwordFormat="Hashed"` —— 並優先採用現代的加鹽 KDF（見 [01](./01-password-hashing-unsalted-sha256.zh-TW.md)）。
🎯 資料庫一旦外洩就把每個密碼以明文（Clear）或可還原（Encrypted，只差一把金鑰）的形式拱手讓出。 📏 把密碼以雜湊儲存，絕不用明文或可逆加密。

### 39. `enablePasswordRetrieval="true"`
❌ 透過 `enablePasswordRetrieval` 允許「把我的密碼寄給我」，這會強制採用可逆格式。 ✅ `enablePasswordRetrieval="false"`；改為提供一次性的重設連結。
🎯 取回功能需要儲存可還原的密碼，而「用安全問題重設」的流程本身也很容易被猜中／釣魚。 📏 絕不支援密碼取回；只提供有時限的單次重設。

### 40. 授權只靠 `<location>`，未在程式碼中強制執行
❌ 只倚賴 `<location path="Admin"><authorization>…` 而在做敏感工作時沒有任何程式碼檢查。 ✅ 在動作前，於程式碼後置中同時驗證 `User.IsInRole(...)`／身分。
🎯 一個路徑對應的怪癖、一個落在 `<location>` 之外的新頁面，或一次直接的處理常式呼叫，都能繞過這個僅靠設定的把關。 📏 在動作處於程式碼中強制授權，而不只靠 URL 設定。

### 41. `Page_Load` 未做驗證檢查就進行敏感工作
❌ 信任全域設定，在 `Page_Load` 中無條件執行特權操作。 ✅ 在處理常式最上方主張使用者的身分／角色（並檢查 `IsPostBack`）。
🎯 一旦該頁面滑出受保護的 `<location>`，`Page_Load` 就會為任何人執行它的特權程式碼。 📏 在真正做事的那個處理常式內重新驗證授權。

### 42. ASMX `[WebMethod]` 未加驗證就暴露
❌ 一個沒有授權的 `*.asmx` `[WebMethod] GetAllUsers()`。 ✅ 在方法內檢查身分／角色（或在其前面加一個已驗證的端點）。
🎯 該服務可被直接呼叫 —— SOAP／GET／POST —— 繞過所有頁面層級的 `<location>` 規則。 📏 在 web 方法內授權；頁面驗證涵蓋不到 ASMX。

### 43. `[WebMethod(EnableSession=true)]` 的陷阱
❌ 在 web 方法上啟用 session，並隨意倚賴它來作為身分／狀態。 ✅ 只在需要時啟用；序列化存取，且別把它當成驗證邊界。
🎯 session 存取會把請求序列化（效能問題），而一個被劫持的 session cookie 現在也能驅動 web 服務攻擊面。 📏 只在必要時才對 web 方法啟用 session；絕不把它當成授權。

### 44. 無 cookie 的 forms 驗證（票證在 URL 裡）
❌ `<forms cookieless="UseUri">` 把驗證票證放進 URL。 ✅ `cookieless="UseCookies"`。
🎯 驗證票證會透過 `Referer`、記錄檔與複製貼上的連結外洩，於是輕易被重放。 📏 把 FormsAuth 票證放在 cookie，絕不放 URL。

### 45. 共用機器上的持久驗證 cookie
❌ 預設就用 `FormsAuthentication.SetAuthCookie(user, true)`（持久）。 ✅ 預設用非持久；只在搭配短期、可撤銷的權杖時才提供「記住我」。
🎯 在自助終端／共用電腦上，持久 cookie 會留下來，於是下一個人就以受害者身分登入了。 📏 預設用 session cookie；把持久性設成明確、有界限的選擇加入。

### 46. 脆弱的 Membership 密碼原則／無鎖定
❌ 預設的 `minRequiredPasswordLength`、沒有 `passwordStrengthRegularExpression`、過於寬鬆的 `maxInvalidPasswordAttempts`。 ✅ 收緊長度／複雜度與鎖定門檻。
🎯 短／簡單的密碼加上寬鬆的鎖定，讓憑證填充與暴力破解能廉價運作。 📏 在 provider 上強制密碼強度與真正的鎖定。

---

## E. 資料存取與重新導向

### 47. `SqlDataSource` 用串接的 SQL
❌ 用控制項的值以字串串接建構 `SelectCommand`。 ✅ 使用 `<SelectParameters>`／`<asp:ControlParameter>`，讓值以參數繫結。
🎯 繫結控制項裡的 `'; DROP TABLE …--` 會注入資料來源所執行的查詢。 📏 把 `SqlDataSource` 的輸入以參數繫結，絕不串接。

### 48. 程式碼後置中原始的 `SqlCommand` 字串串接
❌ `new SqlCommand("SELECT * FROM U WHERE Name='" + txtName.Text + "'", con)`。 ✅ `cmd.Parameters.AddWithValue("@Name", txtName.Text)` 搭配參數化查詢。
🎯 典型的 SQL 注入透過一個文字方塊就能傾印或摧毀資料。 📏 一律參數化；絕不用控制項文字建構 SQL。

### 49. 從控制項取得的動態 `ORDER BY`
❌ `"… ORDER BY " + gridView.SortExpression`。 ✅ 對照一組固定集合，把排序欄位／方向列入白名單。
🎯 參數無法繫結識別項，於是排序運算式就成了注入點。 📏 對照允許清單驗證排序欄位。

### 50. 在程式碼後置中信任 `Request.QueryString`／`Request.Form`
❌ 在查詢或檔案路徑中直接使用 `Request["id"]`。 ✅ 先剖析、範圍檢查、並參數化／限制它。
🎯 原始請求值會依接收點驅動注入、IDOR 或路徑穿越。 📏 在使用前驗證並繫結每個請求值。

### 51. `SqlDataSource.FilterExpression` 來自使用者輸入
❌ 把控制項值內插進 `FilterExpression`。 ✅ 使用帶預留位置的 `FilterParameters`。
🎯 `FilterExpression` 是 `DataView` 的 RowFilter，未淨化的輸入可以跳脫出原本預期的篩選。 📏 把 `FilterExpression` 參數化；別串接進去。

### 52. 輸出快取未考量驗證的變化
❌ 在每位使用者各異的頁面上用 `<%@ OutputCache Duration="60" VaryByParam="none" %>`。 ✅ 用 `VaryByParam`／`VaryByCustom="…"`（例如依使用者變化），或別快取使用者專屬的輸出。
🎯 某位使用者的快取頁面（他的名字、他的資料）會被提供給下一位訪客。 📏 未依使用者變化就絕不快取每位使用者各異的內容。

### 53. `Response.Redirect(Request["url"])` 開放式重新導向
❌ 重新導向到取自查詢字串的 URL。 ✅ 只允許相對路徑或一份 host 允許清單。
🎯 `?url=https://evil.tld` 把你的網域變成釣魚跳板。 📏 在送出之前驗證重新導向的目標。

### 54. `Response.Redirect` 沒有帶 `endResponse:false`
❌ 在 `try`/catch 內，或在你需要完成的工作之後才呼叫 `Response.Redirect(url)`。 ✅ `Response.Redirect(url, false); Context.ApplicationInstance.CompleteRequest();`。
🎯 預設多載會呼叫 `Response.End()`，進而丟出 `ThreadAbortException` —— 被一個過寬的 catch 吞掉，或在進行中把未完成的工作中止。 📏 用 `endResponse:false` 重新導向，並乾淨地完成請求。

### 55. `SqlDataSource` 沒有衝突偵測
❌ 更新／刪除時用 `ConflictDetection="OverwriteChanges"`（預設值）。 ✅ `ConflictDetection="CompareAllValues"` 並處理零列的情況。
🎯 兩位編輯者儲存同一列，第二位默默地覆蓋掉第一位（遺失更新）。 📏 在資料來源更新上使用樂觀並行控制。

### 56. 連線字串／認證在程式碼中建構或儲存
❌ 在程式碼後置中寫死 `"Server=…;User Id=sa;Password=…"`。 ✅ 從加密的 `<connectionStrings>`（或整合式驗證）讀取；絕不放在原始碼。
🎯 認證就在原始碼控管與編譯後的 DLL 裡，攻擊者只差一次反編譯。 📏 把連線機密完全排除在程式碼之外。

---

## F. 頁面生命週期與 Session

### 57. 沒有檢查 `IsPostBack`
❌ 在每次請求都於 `Page_Load` 繫結資料／重新執行副作用。 ✅ 用 `if (!IsPostBack) { … }` 把首次載入的工作保護起來。
🎯 一個非等冪的動作（新增、寄信、收費）會在每次回傳時再度執行，重複處理。 📏 只在 `!IsPostBack` 時做一次性的載入工作。

### 58. 動態控制項在錯誤的事件中重建
❌ 在 `Page_Load`／某個 click 處理常式中新增控制項，卻期待它們的事件與狀態。 ✅ 在每次請求都於 `Page_Init`（或 `CreateChildControls`）中以穩定的 ID 重建動態控制項。
🎯 建立得太晚的控制項拿不到它們回傳的值或事件，於是處理常式默默地什麼都不做而資料遺失。 📏 每次請求都及早（在 `Init`）以一致的 ID 重建動態控制項。

### 59. 把 Session 當成安全邊界，登入時不重新產生 id
❌ 在驗證前後重複使用同一個 `Session.SessionID`。 ✅ 登入成功時，`Session.Abandon()` 並發出一個全新的 session，然後才設定身分。
🎯 攻擊者在受害者身上固定一個已知的 session id；受害者登入後，攻擊者便搭上這個已驗證的 session（session fixation）。 📏 在權限變更（登入）時重新產生 session。

### 60. 把身分存進 Session 並信任它
❌ `Session["UserId"] = id;` 然後純粹靠它來授權。 ✅ 從已驗證的主體（`User.Identity`）推導身分，並在伺服器端重新檢查角色。
🎯 若 session id 被固定／劫持，攻擊者就繼承了 `Session["UserId"]` 所宣稱的一切。 📏 身分要信任驗證票證／主體，而不是單獨的一個 session 欄位。

### 61. 對 `Session_Start` 的假設
❌ 假設 `Session_Start` 代表「有新使用者到來」，或在那裡植入驗證狀態。 ✅ 把它當成盡力而為；在登入處做驗證邏輯，而非 session 開始時。
🎯 被劫持／重複使用的 session 或一個爬蟲會扭曲這個假設，而在 `Session_Start` 植入信任就成了一個 fixation 向量。 📏 別把身分或安全決策掛在 `Session_Start` 上。

### 62. 非等冪的回傳且無 PRG
❌ 在按鈕點擊時執行一次新增，並讓頁面維持回傳狀態（F5 會重放它）。 ✅ Post-Redirect-Get：在變更之後重新導向。
🎯 使用者重新整理或按上一頁，於是重新送出訂單／付款。 📏 在會變更狀態的回傳之後重新導向，以防止重放。

### 63. 大型物件停放在 Session（InProc）
❌ 為每位使用者把龐大的資料集／DataTable 塞進 `Session`。 ✅ 讓 session 保持精小；重新抓取或以上限做快取。
🎯 用 InProc session 時，記憶體會膨脹，而一次回收／陣列跳轉會丟掉資料，破壞流程並招來 OOM。 📏 Session 只放小型鍵值，不放大型酬載。

### 64. 未加以保護的處理序外 session 狀態
❌ 為了陣列而改用 StateServer／SQL session，卻忽略傳輸／序列化。 ✅ 保護狀態通道，並只儲存可序列化、非敏感的資料。
🎯 session 資料（可能與身分相關）會跨越線路／資料庫，在那裡可能被讀取或竄改。 📏 保護並最小化放進處理序外 session 的內容。

### 65. 靜態頁面欄位跨請求共用
❌ 在 `Page`／控制項上用一個 `static` 欄位持有每位使用者的資料。 ✅ 使用執行個體欄位、`Session` 或每個請求各自的儲存。
🎯 靜態成員在所有請求／執行緒間共用，於是在負載下某位使用者的資料就洩漏進另一位的頁面。 📏 絕不在靜態欄位中保留每個請求／每位使用者的資料。

### 66. 在 `Page_Load` 檢查驗證，但子事件較晚才觸發
❌ 假設 `Page_Load` 的把關涵蓋了之後在生命週期中引發的控制項事件。 ✅ 在真正執行動作的那個特定事件處理常式內也做授權。
🎯 控制項事件（`Click`、`RowCommand`）在 `Load` 之後執行；一條只透過該事件抵達的程式碼路徑會跳過 `Page_Load` 的檢查。 📏 在做事的那個處理常式授權，而不只在 `Load`。

---

## G. 用戶端指令碼、UpdatePanel 與事件驗證

### 67. `EnableEventValidation="false"`
❌ 在某個頁面上停用事件驗證。 ✅ 讓它保持開啟（預設值）。
🎯 攻擊者偽造一個以從未算繪過的值／命令為目標的回傳 —— 例如選取一個被停用／隱藏的選項或一個未曾顯示的清單項目 —— 而伺服器竟然接受它。 📏 讓事件驗證保持啟用，使回傳目標必須符合曾算繪過的內容。

### 68. `RegisterStartupScript` 帶未編碼的資料
❌ `ClientScript.RegisterStartupScript(GetType(),"k","alert('"+userInput+"');",true);`。 ✅ 在嵌入前對值做 JavaScript 編碼（`Encoder.JavaScriptEncode`）。
🎯 `userInput = "');document.location=…//"` 會跳脫出字串並執行攻擊者的指令碼（DOM／儲存型 XSS）。 📏 對任何注入進所吐出指令碼的伺服器值做 JS 編碼。

### 69. 用伺服器資料建構的行內 `<script>`
❌ 在標記中寫 `<script>var u = '<%= userName %>';</script>`。 ✅ 把資料吐進 JSON／`data-` 屬性並以編碼方式讀取，或使用 `<%: %>`／`Encoder.JavaScriptEncode`。
🎯 值裡的一個引號或 `</script>` 會打斷指令碼區塊並注入程式碼。 📏 絕不把原始伺服器字串丟進行內指令碼字面值。

### 70. `UpdatePanel` 非同步回傳未重新檢查驗證
❌ 假設非同步（部分）回傳比較安全或已被授權。 ✅ 對被觸發的處理常式做授權，就跟完整回傳一模一樣。
🎯 非同步回傳會直接打到相同的伺服器處理常式，於是一個缺漏的檢查同樣可被利用 —— 而且更容易寫成腳本。 📏 部分回傳跟完整回傳受相同的授權。

### 71. 信任 `__EVENTTARGET`／`__EVENTARGUMENT`
❌ 未經驗證就依原始的 `__doPostBack` 引數行動。 ✅ 在伺服器端對照預期值驗證目標／引數。
🎯 攻擊者製作一個指名一個他們不該抵達之控制項／命令的回傳，並驅動一個非預期的動作。 📏 把回傳事件欄位當成不可信輸入來對待。

### 72. `RegisterClientScriptBlock` 未對使用者資料做 JS 編碼
❌ 把請求／資料庫的值串接進一個在算繪時註冊的指令碼區塊。 ✅ 在註冊前為 JavaScript 情境編碼（或序列化為 JSON）。
🎯 未編碼的值會作為指令碼為每位觀看者執行 —— 透過指令碼接收點的反射型或儲存型 XSS。 📏 在註冊用戶端指令碼區塊前，為 JS 編碼伺服器資料。

### 73. 把資料洩漏進算繪出的用戶端指令碼
❌ 把整個使用者／DTO（含內部欄位）傾倒進一個 JS 變數供頁面使用。 ✅ 只吐出用戶端需要的欄位，並編碼。
🎯 內部 id、旗標或個資就坐在頁面原始碼裡，任何人透過檢視原始碼都能讀到。 📏 只給用戶端最少的內容，並編碼它。

---

## H. 檔案處理與上傳

### 74. `FileUpload` 沒有大小限制
❌ 接受任意大小的 `FileUpload.PostedFile`。 ✅ 檢查 `PostedFile.ContentLength` 並設上限 `maxRequestLength`／`requestLengthDiskThreshold`。
🎯 一次巨大的上傳耗盡記憶體／磁碟 —— 一種廉價的 DoS。 📏 強制一個明確的上傳大小上限。

### 75. `FileUpload` 沒有型別／副檔名允許清單
❌ 使用者送什麼副檔名就存什麼。 ✅ 把副檔名列入允許清單（並驗證內容），其餘全部拒絕。
🎯 一個 `.aspx`／`.ashx`／`.config` 上傳會在伺服器上變成可執行程式碼或設定竄改。 📏 把上傳副檔名列入允許清單；預設拒絕。

### 76. 把上傳存進 web 可存取的資料夾
❌ 在站台根目錄下的 `SaveAs(Server.MapPath("~/uploads/" + name))`。 ✅ 存到 webroot 之外（或存到一個不執行、僅供下載的路徑），並透過一個有把關的處理常式提供。
🎯 攻擊者上傳 `shell.aspx` 並瀏覽到它，取得程式碼執行。 📏 絕不讓上傳的檔案落到 IIS 能執行它的地方。

### 77. 透過 `Server.MapPath` + 使用者輸入的路徑穿越
❌ `Server.MapPath("~/files/" + Request["name"])` 然後讀／寫。 ✅ 縮減為 `Path.GetFileName`、正規化，並確認解析後的路徑仍在根目錄之下。
🎯 `name = "..\\..\\web.config"` 會讀取或覆寫預期資料夾之外的檔案。 📏 把解析後的路徑限制在允許的目錄內。

### 78. 信任 `PostedFile.ContentType`
❌ 因為 `ContentType == "image/jpeg"` 就判定某檔案是圖片。 ✅ 嗅探魔術位元組／重新編碼；把該標頭只當成提示。
🎯 用戶端可自由設定 `ContentType`，於是一個可執行檔宣稱自己是 JPEG 並溜過檢查。 📏 依內容驗證檔案型別，而非用戶端提供的 MIME。

### 79. 原封不動地信任 `PostedFile.FileName`
❌ 把原始的 `FileName`（可能包含一個用戶端路徑）當成儲存名稱使用。 ✅ `Path.GetFileName(...)`，然後產生你自己的安全、唯一的名稱。
🎯 一個精心製作的 `FileName` 會注入路徑片段或覆寫既有檔案。 📏 絕不原樣保存用戶端提供的檔名。

### 80. 用帶使用者路徑的 `TransmitFile`／`WriteFile` 提供檔案
❌ `Response.TransmitFile(Server.MapPath("~/docs/" + Request["f"]))`。 ✅ 把請求對應到一個 id，在伺服器端查出真正的路徑並授權它。
🎯 `f=..\\..\\web.config` 把任意的伺服器檔案串流給攻擊者（路徑穿越／任意檔案讀取）。 📏 透過一個經授權的 id-對-路徑查詢來解析下載，而非原始輸入。

---

## I. 錯誤處理與記錄

### 81. 會吞掉例外的 `Application_Error`
❌ `Application_Error` 呼叫 `Server.ClearError()` 並在不記錄的情況下繼續。 ✅ 透過 `Server.GetLastError()` 記錄例外（連同情境），然後顯示一個安全的頁面。
🎯 真正的失敗 —— 包括安全性的 —— 會默默消失，事後在遭入侵後也無從調查。 📏 在 `Application_Error` 中記錄；絕不清除後遺忘。

### 82. 向使用者顯示黃色死亡畫面
❌ 因為 `customErrors` 關閉，未處理的例外抵達用戶端。 ✅ `customErrors mode="RemoteOnly"` 搭配一個友善的錯誤頁面。
🎯 堆疊追蹤把原始碼路徑、SQL 與框架版本洩漏給攻擊者。 📏 使用者看到一個一般性頁面；細節只進記錄檔。

### 83. 程式碼後置中的空 `catch`
❌ 在一個資料操作或安全檢查外圍寫 `try { … } catch { }`。 ✅ 處理或重新丟出；連同情境記錄。
🎯 一次失敗的授權／驗證被默默忽略，請求就好像通過了一樣繼續進行。 📏 絕不空 catch；被吞掉的失敗就是一個隱藏的漏洞。

### 84. 記錄 ViewState／票證／機密
❌ 把 `Request.Form`（其中含 `__VIEWSTATE`）或驗證 cookie 傾倒進記錄檔。 ✅ 只記錄 id 與去識別化的標記。
🎯 ViewState、session id 與權杖在記錄檔中堆積，並隨其一同外洩。 📏 絕不記錄 ViewState、cookie、權杖或完整的請求主體。

### 85. 在 `Page.Error` 事件上攔截後繼續
❌ 處理 `Page_Error`／`Application_Error` 來隱藏失敗並繼續算繪。 ✅ 失敗即關閉（fail closed）—— 停止該操作並回傳一個錯誤。
🎯 一個安全或完整性檢查丟出例外、被「處理」掉，而危險的路徑卻依然完成。 📏 一次失敗的檢查必須阻擋該操作，而不是被抹平帶過。

### 86. 正式環境暴露 `trace.axd`／ELMAH
❌ 讓 `trace.axd` 或 `elmah.axd` 未經驗證即可存取。 ✅ 用驗證／IP 加以限制，或在正式環境移除它們。
🎯 這些儀表板把近期請求、cookie、ViewState 與完整堆疊追蹤暴露給任何人。 📏 在正式環境鎖住或移除診斷端點。

### 87. 冗長的框架標頭
❌ 傳送 `X-AspNet-Version`、`X-AspNetMvc-Version`、`X-Powered-By`、`Server`。 ✅ 移除它們（`<httpRuntime enableVersionHeader="false">`，並在 `<customHeaders>` 中剝除 `X-Powered-By`）。
🎯 版本標頭剛好告訴攻擊者該試哪些框架的 CVE。 📏 抑制版本／技術標頭。

---

## J. 一般 .NET Framework 陷阱

### 88. `Response.End()` 與 `ThreadAbortException`
❌ 呼叫 `Response.End()` 來「停止」一個頁面。 ✅ `Context.ApplicationInstance.CompleteRequest()`（並 return），避開中止。
🎯 `Response.End()` 會丟出 `ThreadAbortException`；一個外圍的過寬 catch 會吞掉它，於是程式碼繼續執行到你原本想停止之處之後。 📏 用 `CompleteRequest()` 結束請求，而非 `Response.End()`。

### 89. `Server.Transfer` 把狀態保留到一個未授權的頁面
❌ `Server.Transfer("Admin.aspx", true)` 帶著 form／query 跨越一個驗證邊界。 ✅ 透過正常管線重新導向讓授權執行，或在目標頁重新檢查。
🎯 `Server.Transfer` 繞過了一次全新的請求，於是目標的以 URL 為基礎的授權可能不會為被轉移的使用者重新評估。 📏 別用 `Server.Transfer` 來跳過授權；在目標頁重新檢查。

### 90. `async void` 的頁面事件處理常式
❌ `protected async void Button_Click(...)`。 ✅ 用 `RegisterAsyncTask(new PageAsyncTask(...))` 註冊非同步工作，並在頁面上設 `Async="true"`。
🎯 頁面在等待的工作完成之前就結束（或一個例外使工作執行緒崩潰），產生部分／不正確的回應。 📏 對非同步的頁面工作使用 `PageAsyncTask`，而非 `async void`。

### 91. 在請求執行緒上以同步方式等待非同步（sync-over-async）
❌ 在一個 Web Forms 頁面中用 `SomeAsync().Result` / `.Wait()`。 ✅ 透過 `PageAsyncTask` 用 `await`。
🎯 當你在一個需要同一執行緒的工作上進行封鎖時，舊有的 `AspNetSynchronizationContext` 會死結。 📏 別在 ASP.NET 請求管線中封鎖非同步。

### 92. `DateTime.Now` 而非 `DateTime.UtcNow`
❌ 用伺服器本地時間蓋章／比較。 ✅ 以 UTC 儲存與比較；在顯示的邊緣才轉換。
🎯 DST／時區的漂移會弄壞票證到期、排程與稽核排序。 📏 以 UTC 保存並比較時間。

### 93. 對使用者輸入做具文化敏感性的剖析
❌ 用請求的 `CurrentCulture` 執行 `double.Parse(txt.Text)` / `DateTime.Parse(...)`。 ✅ 用 `CultureInfo.InvariantCulture` 剖析（並用 `TryParse`）。
🎯 一位 `Accept-Language` 設定為以逗號為小數點之文化的訪客，會讓 `"1,5"` 剖析出不同結果 —— 錯誤的金額或例外。 📏 對機器剖析使用不變文化（invariant culture）。

### 94. 用 `Random`／`Guid` 當安全權杖
❌ 拿 `new Random().Next()` 或 `Guid.NewGuid()` 當重設／CSRF／下載權杖。 ✅ `RandomNumberGenerator.GetBytes(32)`。
🎯 `Random` 從幾筆輸出就可預測，而 GUID 並不保證無法猜測，於是該權杖可被偽造。 📏 對任何攻擊者不得預測之物使用 CSPRNG。

### 95. 用 MD5／SHA1／未加鹽的雜湊處理密碼
❌ `FormsAuthentication.HashPasswordForStoringInConfigFile(pw,"SHA1")` 或裸的 MD5／SHA-256。 ✅ 加鹽的 PBKDF2／bcrypt／Argon2 —— 見 [01](./01-password-hashing-unsalted-sha256.zh-TW.md)。
🎯 資料庫一旦外洩，快速、未加鹽的雜湊會在數分鐘內敗給彩虹表／GPU 破解。 📏 密碼雜湊必須慢且加鹽。

### 96. 程式碼後置中寫死的機密
❌ 在 `.aspx.cs` 中寫 `const string ApiKey = "sk_live_…";`。 ✅ 從加密的設定讀取；絕不放在原始碼。
🎯 機密就在原始碼控管與編譯後的組件裡，只差一次反編譯。 📏 程式碼或歷史紀錄中都不放機密。

### 97. 在背景／非同步執行緒上使用 `HttpContext.Current`
❌ 產生一個 `Task`／執行緒並在那裡讀取 `HttpContext.Current`。 ✅ 事先擷取你需要的值並把它們傳入。
🎯 `HttpContext.Current` 是綁定執行緒／請求的；離開該執行緒時它是 `null` 或屬於另一個請求，會洩漏或崩潰。 📏 別在請求執行緒之外碰觸 `HttpContext.Current`。

### 98. 把每個請求各異的資料放進 `static` 欄位
❌ 把「目前使用者」或一個請求值快取在一個 `static`／單例中以便重用。 ✅ 把它保留在請求／session 範圍內。
🎯 在並行下，請求會互相覆寫彼此的靜態值，於是一位使用者以另一位的身分行事。 📏 絕不把每個請求各異的狀態保留在共用的靜態狀態中。

### 99. 信任 Host 標頭／`Request.Url` 來建構連結
❌ 用 `Request.Url.Host`／`Host` 標頭建構密碼重設或絕對連結。 ✅ 使用一個設定好的標準基底 URL。
🎯 攻擊者送出一個偽造的 `Host` 標頭，讓你的重設信件連到他們的網域（host-header poisoning）。 📏 從可信的設定建構絕對 URL，而非請求的 host。

### 100. 信任 `X-Forwarded-For`／`Request.UserHostAddress` 作為安全依據
❌ 依一個用戶端提供的 `X-Forwarded-For` 做允許清單或速率限制。 ✅ 使用真正的連線 IP，並只信任來自已知代理伺服器的轉送標頭。
🎯 攻擊者把 `X-Forwarded-For` 設成一個受信任的 IP，就繞過了 IP 限制／節流。 📏 只信任來自你所掌控之代理伺服器的轉送標頭。

---

## 使用這份清單

- 在動筆之前，**先**略讀符合你即將撰寫內容的類別。
- 在審查時，那些 📏 行同時充當一份檢查清單 —— 任何一項答「否」就是一個發現。
- 用慘痛的方式又發現了新的一則？把它加到這裡；若它夠鋒利、足以拿來教學，就給它一個像 [01](./01-password-hashing-unsalted-sha256.zh-TW.md) 那樣完整的前後對照檔案，並從 [README.md](./README.zh-TW.md) 連結它。
