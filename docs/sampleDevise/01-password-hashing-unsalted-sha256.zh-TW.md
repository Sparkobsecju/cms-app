> 本文件為 [01-password-hashing-unsalted-sha256.md](./01-password-hashing-unsalted-sha256.md) 的繁體中文翻譯版本。

# 01 — 密碼雜湊：未加鹽的 SHA-256 → 加鹽的 PBKDF2

**類別：** OWASP A02（加密失效 Cryptographic Failures）· **上線後嚴重性：** HIGH · **偵測來源：** `/cso` 2026-07-17
**涉及檔案：** `Security/PasswordHasher.cs`、`Repositories/AuthRepository.cs`、`Repositories/AppUserRepository.cs`
**修復紀錄：** [`docs/reviews/2026-07-17-fixes-applied.md`](../reviews/2026-07-17-fixes-applied.md) → 「🔐 CSO audit pass」

---

## TL;DR

密碼原本以單一回合的**未加鹽 SHA-256**進行雜湊。那是一種*快速、通用型*的
雜湊 (hash) —— 恰恰與密碼儲存所需要的相反。若 `AppUser` 資料表曾經外洩，每一組
密碼都能在幾分鐘內被破解，而且相同的密碼在資料庫中會明顯呈現為相同的樣貌。

這次修復包含**三個各自獨立的教訓**，每個更動的檔案對應一個：

1. **使用緩慢、加鹽的密碼 KDF（PBKDF2），而不是快速的雜湊（SHA-256/MD5）。**
2. **加鹽後的雜湊無法在 SQL 中比對 —— 要在應用程式碼中驗證，並將雜湊保留在伺服器端。**
3. **當你變更儲存格式時，要安全地遷移舊資料列（此處：在下次登入時遷移），而不是強制重設。**

---

## 教訓 1 —— 快速雜湊 vs. 密碼 KDF

### ❌ 有漏洞的程式碼（`Security/PasswordHasher.cs`）

```csharp
public static string Hash(string password)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
    return Convert.ToHexString(hash).ToLowerInvariant();   // 64-char lowercase hex, no salt
}
```

### 為什麼它看起來沒問題

SHA-256 *確實*是一種加密上強健的雜湊。它是單向的、抗碰撞的，而且經過 NIST 認可。
一位合理的開發者會想「我沒有儲存明文，我儲存的是一個強健的雜湊 —— 搞定了。」
這個直覺對於**完整性**（校驗碼、簽章）是正確的，但對於**密碼**卻是錯的。

有兩項特性使它不適用於密碼：

- **沒有鹽 (salt)。** 相同的密碼永遠產生相同的雜湊。`SHA256("P@ssw0rd")` 是一個固定的、
  任何人都能事先計算的公開值。
- **快速。** SHA-256 *就是被設計*來跑得快的（每秒數 GB）。對於密碼，你反而希望它*緩慢*，如此每次猜測
  都會讓攻擊者付出實質的時間成本。

### 🎯 攻擊情境 —— 請牢記在心

密碼儲存的威脅模型是**「假設資料庫終有一天會外洩」**。這正是我們之所以要做雜湊的
全部理由 —— 讓被竊取的 `AppUser` 資料表不會直接交出每個人的密碼。

1. 攻擊者取得 `AppUser.PasswordHash` 欄位。在現實世界中發生的途徑：放在設定錯誤共享資料夾上
   外洩的資料庫備份、心懷不滿或遭入侵的 DBA/內部人員、某個明年才新增的端點裡*未來*的
   SQL 隱碼注入或 IDOR、遺失且內含資料庫副本的筆電，或是託管該資料庫的
   第三方發生資料外洩。
2. 由於這些雜湊是**未加鹽的**，攻擊者可以使用**彩虹表 (rainbow table)** —— 一份事先計算好、
   涵蓋數十億組常見密碼的 `hash → password` 查找表。常見密碼根本不需要破解；
   那只是一次字典查找。
3. 至於其餘的密碼，由於 SHA-256 **快速**，單一顆消費級 GPU 每秒可以嘗試**數十億次猜測**。
   大多數由人挑選的密碼在幾分鐘到幾小時內就會失守。
4. **缺少鹽帶來的額外洩漏：** 兩個儲存雜湊相同的使用者*擁有相同的密碼*。
   攻擊者立刻就能看出跨帳號的密碼重複使用 —— 而且每一個被管理員重設為
   共用 `defaultPassword` 的帳號都會有*完全相同*的雜湊，等於把它們標示成同一批。
5. 一個被破解的 **Admin** 密碼 → 完整的 系統管理 存取權限；接著 JWT 便會透過正常的登入流程被鑄造出來。

> 危險是**潛伏的**：程式碼運作完美、每個測試都通過。在資料庫被暴露的那一天之前，
> 一切看起來都毫無異狀 —— 然後*所有*帳號會一次全部淪陷。你無法「在 QA 時察覺它」。

### ✅ 修復方式 —— 加鹽的 PBKDF2

```csharp
public const string Pbkdf2Prefix = "PBKDF2";
private const int Iterations = 100_000;
private const int SaltBytes = 16;   // 128-bit random salt, per user
private const int HashBytes = 32;   // 256-bit derived key
private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

public static string Hash(string password)
{
    var salt = RandomNumberGenerator.GetBytes(SaltBytes);          // fresh salt every call
    var derived = Rfc2898DeriveBytes.Pbkdf2(
        Encoding.UTF8.GetBytes(password), salt, Iterations, Algorithm, HashBytes);

    // Self-describing: the salt + cost travel WITH the hash, so params can change later per-row.
    return string.Join('$', Pbkdf2Prefix, "SHA256", Iterations.ToString(),
        Convert.ToBase64String(salt), Convert.ToBase64String(derived));
    // e.g. PBKDF2$SHA256$100000$Umsb...==$9f3k...==
}
```

每一部分為什麼重要：

- **每位使用者隨機的鹽** → 彩虹表變得毫無用處（攻擊者必須為每位使用者準備一份全新的表），
  而且相同的密碼現在會產生*不同*的儲存雜湊，因此密碼重複使用不再可見。
- **100,000 次迭代** → 每次猜測的成本約提高 10 萬倍；GPU 破解速度從每秒數十億次
  降到涓涓細流。隨著硬體進步，可將此數值往上調。
- **自我描述的字串**（`algo$iterations$salt$hash`）→ 你日後可以提高迭代次數，
  而舊資料列仍能用它們自己儲存的參數完成驗證。不需變更綱要，也不需一次性的大爆炸式遷移。

在原始 SHA-256 上使用 `Convert.ToHexString(...).ToLowerInvariant()` 就是這個 bug 的典型特徵。如果你
看到它被餵入某個 `PasswordHash` 欄位，請立即停下。

### 規則

> **密碼要使用緩慢、加鹽的 KDF —— PBKDF2 / bcrypt / Argon2 —— 絕不用單純的 SHA-256/SHA-1/MD5。**
> 如果你的雜湊函式很快，那它對密碼而言就是錯的。

---

## 教訓 2 —— 加鹽的雜湊無法在 SQL 中比對

加鹽破壞了這個程式碼庫在登入時到處使用的一種模式：在 SQL `WHERE` 子句*內部*比對雜湊。
那個模式之所以有效，只是因為未加鹽的雜湊是決定性的。一旦加鹽，你就
**必須**取出儲存的雜湊，並在應用程式碼中驗證。

### ❌ 有漏洞的模式（`AuthRepository.ValidateCredentialsAsync`）

```csharp
// Hash the input, then let SQL do the comparison.
var passwordHash = HashPassword(password);
var user = await connection.QuerySingleOrDefaultAsync<AuthenticatedUser>(new CommandDefinition(@"
    SELECT UserId, UserName
    FROM AppUser
    WHERE UserId = @UserId AND IsActive = 1 AND PasswordHash = @PasswordHash;",   // <-- compare in SQL
    new { UserId = userId, PasswordHash = passwordHash }));
```

### 為什麼它看起來沒問題（甚至還挺聰明的）

在 SQL 中比對其實有一個真正的安全*優點*：儲存的 `PasswordHash` 從未被從資料庫中
`SELECT` 出來，所以它不會不小心跑進某個 DTO、日誌或稽核資料列。作者是刻意
倚賴這一點的。問題在於：**它在結構上依賴雜湊是決定性的。** 同一密碼的加鹽 PBKDF2
雜湊每次都不一樣，所以 `PasswordHash = @PasswordHash` 永遠不會相符 —— 對所有人來說登入
都會壞掉。一旦加鹽，你就*無法*保留這個模式。

### ✅ 修復方式 —— 取出後在程式碼中驗證（雜湊留在伺服器端）

```csharp
// Fetch the stored hash for an ACTIVE user. It is read into this method only — never returned to the
// client (AuthenticatedUser has no PasswordHash field), so the "never leaks" property is preserved.
var record = await connection.QuerySingleOrDefaultAsync<CredentialRecord>(new CommandDefinition(@"
    SELECT UserId, UserName, PasswordHash
    FROM AppUser
    WHERE UserId = @UserId AND IsActive = 1;",
    new { UserId = userId }));

// A missing row (unknown user OR IsActive = 0) is indistinguishable from a wrong password: both -> null.
if (record is null || !PasswordHasher.Verify(password, record.PasswordHash, out var needsRehash))
{
    return null;
}
```

修復時特別留意的兩件事 —— 兩者都很容易做錯：

- **讓雜湊留在伺服器端。** `CredentialRecord` 是一個私有的儲存庫型別；`AuthenticatedUser`
  （會流向外部的那個值）依然**沒有** `PasswordHash`。雜湊被讀取以供驗證，之後即被
  丟棄。不要為了「簡化」而把 `PasswordHash` 加進回傳的模型中。
- **不要洩漏*是哪一項*檢查失敗。** 未知的使用者、停用的使用者、以及錯誤的密碼全都回傳
  相同的 `null` → 控制器回傳單一的通用 `401 Invalid credentials`。若你把它們拆成
  各自不同的錯誤（「無此使用者」vs「密碼錯誤」），就等於交給攻擊者一個**使用者列舉 (user-enumeration) 的預言機 (oracle)**。

`VerifyCurrentPasswordAsync`（供變更密碼使用）有著完全相同的 SQL 比對模式，也得到了
完全相同的處理：取出雜湊、`PasswordHasher.Verify(...)`、回傳布林值。

### 規則

> **雜湊一旦加鹽，就無法在 `WHERE` 子句中比對。取出它並在程式碼中驗證 ——
> 並讓它遠離每一個模型、DTO、日誌與稽核資料列。** 此外：對未知使用者／停用／
> 密碼錯誤都回傳單一的通用失敗，如此登入就無法被用來列舉帳號。

---

## 教訓 3 —— 安全地遷移既有的儲存格式

資料庫裡早已充滿舊的未加鹽 SHA-256 雜湊。你無法離線重新雜湊它們（你並沒有
那些明文），而強迫每位使用者重設密碼既不友善又往往窒礙難行。

### 若你草率遷移的危險

- **強制重設所有人** → 使用者被鎖在門外、客服被灌爆，而且（更糟的是）一個匆促趕出來的「重設」流程
  正是新的認證 bug 被引入的溫床。
- **永遠繼續驗證舊雜湊** → 那個脆弱的機制永遠不會真正消失；某個「暫時性」的
  相容分支變成永久性存在，而這項發現也就從未真正被關閉。

### ✅ 修復方式 —— 同時驗證兩種格式，在下次登入時惰性升級

`PasswordHasher.Verify` 接受**兩種**格式，並在它比對到舊格式時回報：

```csharp
public static bool Verify(string password, string? storedHash, out bool needsRehash)
{
    needsRehash = false;
    if (string.IsNullOrEmpty(storedHash)) return false;

    if (storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal))
        return VerifyPbkdf2(password, storedHash);                 // current format

    if (VerifyLegacySha256(password, storedHash))                  // deprecated 64-char hex
    {
        needsRehash = true;                                        // matched — but it's the weak scheme
        return true;
    }
    return false;
}
```

接著登入路徑會**一次性**、透明地，使用它剛剛驗證過的明文升級該資料列：

```csharp
if (needsRehash)
{
    // We hold the verified plaintext right now — re-hash it under PBKDF2 and persist. No reset needed.
    // PasswordUpdatedTime is deliberately NOT stamped: the password didn't change, only its storage format.
    await connection.ExecuteAsync(new CommandDefinition(
        "UPDATE AppUser SET PasswordHash = @PasswordHash WHERE UserId = @UserId;",
        new { UserId = record.UserId, PasswordHash = PasswordHasher.Hash(password) }));
}
```

值得注意的細節：

- **常數時間 (constant-time) 比對。** `VerifyPbkdf2` 與 `VerifyLegacySha256` 都以
  `CryptographicOperations.FixedTimeEquals` 比對，而非 `==`。天真的位元組/字串 `==` 一旦
  第一個位元組不同就會立刻回傳，而那個時間差是一個真實（雖然難以利用）的側通道，可用來偽造雜湊。
- **格式升級時不要蓋上 `PasswordUpdatedTime`。** 使用者並沒有變更密碼；
  蓋上它會對任何「密碼年齡」政策或稽核說謊。
- **升級只執行一次。** 遷移後的第一次登入之後，該資料列就是 PBKDF2，對該使用者而言那條舊有分支
  再也不會被走到。當最後一個舊資料列消失後，就刪除 `VerifyLegacySha256`。

### 規則

> **要淘汰一個脆弱的雜湊：同時驗證舊 + 新，並在使用者下次成功登入時重新雜湊成新機制。
> 絕不強制大規模重設，也絕不在沒有刪除計畫的情況下把脆弱路徑當成「暫時性」保留。**

---

## 附帶 —— 同樣的錯誤藏在第二個地方

管理員的「建立使用者／重設密碼」路徑有它自己的**行內 (inline)** 未加鹽 SHA-256 雜湊副本，
與 `PasswordHasher` 各自獨立：

```csharp
// AppUserRepository.ReadDefaultPasswordHashAsync — BEFORE
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(defaultPassword));
return Convert.ToHexString(hash).ToLowerInvariant();
```

```csharp
// AFTER — route through the one shared hasher
return PasswordHasher.Hash(defaultPassword);
```

教訓是：**一個攸關安全的原語 (primitive) 必須恰好存在於一個地方。** 因為雜湊在這裡*也*被
複製貼上了一份，單只修好 `PasswordHasher` 會讓每一個由管理員建立與
重設密碼的帳號依舊停留在脆弱機制上。修復加密 bug 時，要用 grep 搜尋那個*模式*（`SHA256.HashData`、
`Convert.ToHexString`），而不只是函式名稱 —— 重複副本正是一個
「已修復」的漏洞悄悄存活下來的方式。

### 規則

> **只定義一次密碼雜湊，並在每個地方呼叫它。如果你找到第二份副本，那就是第二個 bug。**

---

## 快速檢查清單 —— 在你撰寫認證程式碼之前

- [ ] 密碼雜湊使用**緩慢、加鹽的 KDF**（PBKDF2/bcrypt/Argon2），絕不用單純的 SHA/MD5。
- [ ] 雜湊隨身攜帶它的**鹽 + 成本參數**（自我描述的字串）。
- [ ] 驗證發生**在程式碼中**，而非在 SQL `WHERE` 裡，且儲存的雜湊**絕不**進入
      任何模型 / DTO / 日誌 / 稽核資料列。
- [ ] 雜湊比對是**常數時間**（`CryptographicOperations.FixedTimeEquals`）。
- [ ] 登入對未知使用者 / 停用 / 密碼錯誤都回傳**單一的通用失敗**（不可列舉）。
- [ ] 只有**一個**雜湊輔助程式，供每一處寫入/驗證的位置呼叫（沒有行內副本）。
- [ ] 格式變更在**下次登入時惰性遷移**，並有刪除舊有分支的計畫。
