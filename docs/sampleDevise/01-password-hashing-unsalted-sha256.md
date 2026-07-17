# 01 — Password hashing: unsalted SHA-256 → salted PBKDF2

**Category:** OWASP A02 (Cryptographic Failures) · **Severity when live:** HIGH · **Caught by:** `/cso` 2026-07-17
**Files involved:** `Security/PasswordHasher.cs`, `Repositories/AuthRepository.cs`, `Repositories/AppUserRepository.cs`
**Fix record:** [`docs/reviews/2026-07-17-fixes-applied.md`](../reviews/2026-07-17-fixes-applied.md) → "🔐 CSO audit pass"

---

## TL;DR

Passwords were hashed with a single round of **unsalted SHA-256**. That is a *fast, general-purpose*
hash — the exact opposite of what password storage needs. If the `AppUser` table ever leaked, every
password would be crackable in minutes, and identical passwords were visibly identical in the database.

The fix has **three separate lessons**, one per changed file:

1. **Use a slow, salted password KDF (PBKDF2), not a fast hash (SHA-256/MD5).**
2. **A salted hash can't be compared in SQL — verify in application code, and keep the hash server-side.**
3. **When you change a storage format, migrate the old rows safely (here: on next login), don't force a reset.**

---

## Lesson 1 — Fast hash vs. password KDF

### ❌ The vulnerable code (`Security/PasswordHasher.cs`)

```csharp
public static string Hash(string password)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
    return Convert.ToHexString(hash).ToLowerInvariant();   // 64-char lowercase hex, no salt
}
```

### Why it looks fine

SHA-256 *is* a cryptographically strong hash. It is one-way, collision-resistant, and NIST-approved.
A reasonable developer thinks "I'm not storing the plaintext, I'm storing a strong hash — done."
That intuition is correct for **integrity** (checksums, signatures) and wrong for **passwords**.

Two properties make it wrong for passwords:

- **No salt.** The same password always produces the same hash. `SHA256("P@ssw0rd")` is a fixed,
  public value anyone can precompute.
- **Fast.** SHA-256 is *designed* to be fast (GBs/sec). For passwords you want it *slow*, so each guess
  costs the attacker real time.

### 🎯 The attack scenario — memorise this

The threat model for password storage is **"assume the database will one day leak."** That is the whole
reason we hash at all — so a stolen `AppUser` table doesn't hand over everyone's password.

1. An attacker obtains the `AppUser.PasswordHash` column. Ways this happens in the real world: a leaked
   database backup on a misconfigured share, a disgruntled or compromised DBA/insider, a *future*
   SQL-injection or IDOR in some endpoint added next year, a stolen laptop with a DB copy, or a
   third-party breach at whoever hosts the DB.
2. Because the hashes are **unsalted**, the attacker runs a **rainbow table** — a precomputed
   `hash → password` lookup for billions of common passwords. No cracking needed for common passwords;
   it's a dictionary lookup.
3. For the rest, because SHA-256 is **fast**, a single consumer GPU tries **billions of guesses per
   second**. Most human-chosen passwords fall in minutes to hours.
4. **Bonus leak from missing salt:** two users with the same stored hash *have the same password*.
   The attacker instantly sees password reuse across accounts — and every account an admin reset to the
   shared `defaultPassword` has an *identical* hash, flagging them as a batch.
5. A cracked **Admin** password → full 系統管理 access; the JWT is then minted through the normal login.

> The danger is **latent**: the code runs perfectly and every test passes. Nothing looks broken until
> the day the DB is exposed — and then *all* accounts fall at once. You cannot "notice it in QA".

### ✅ The fix — salted PBKDF2

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

Why each piece matters:

- **Random per-user salt** → rainbow tables are useless (the attacker would need a fresh table per user),
  and identical passwords now produce *different* stored hashes, so reuse is no longer visible.
- **100,000 iterations** → each guess is ~100k times more expensive; GPU cracking drops from billions/sec
  to a trickle. Tune this up as hardware improves.
- **Self-describing string** (`algo$iterations$salt$hash`) → you can raise the iteration count later and
  old rows still verify with their own stored parameters. No schema change, no big-bang migration.

`Convert.ToHexString(...).ToLowerInvariant()` on a raw SHA-256 is the tell-tale shape of this bug. If you
see it feeding a `PasswordHash` column, stop.

### The rule

> **Passwords use a slow, salted KDF — PBKDF2 / bcrypt / Argon2 — never a bare SHA-256/SHA-1/MD5.**
> If your hash function is fast, it is wrong for passwords.

---

## Lesson 2 — A salted hash can't be compared in SQL

Salting breaks a pattern this codebase used everywhere for login: comparing the hash *inside the SQL
`WHERE` clause*. That pattern only works because an unsalted hash is deterministic. Once you salt, you
**must** fetch the stored hash and verify in application code.

### ❌ The vulnerable pattern (`AuthRepository.ValidateCredentialsAsync`)

```csharp
// Hash the input, then let SQL do the comparison.
var passwordHash = HashPassword(password);
var user = await connection.QuerySingleOrDefaultAsync<AuthenticatedUser>(new CommandDefinition(@"
    SELECT UserId, UserName
    FROM AppUser
    WHERE UserId = @UserId AND IsActive = 1 AND PasswordHash = @PasswordHash;",   // <-- compare in SQL
    new { UserId = userId, PasswordHash = passwordHash }));
```

### Why it looks fine (and was even clever)

Comparing in SQL had a real security *upside*: the stored `PasswordHash` was never `SELECT`ed out of the
database, so it couldn't accidentally end up in a DTO, a log, or an audit row. The author leaned on that
on purpose. The catch: **it structurally depends on the hash being deterministic.** A salted PBKDF2 hash
of the same password is different every time, so `PasswordHash = @PasswordHash` would never match — login
would break for everyone. You *cannot* keep this pattern once you salt.

### ✅ The fix — fetch, then verify in code (hash stays server-side)

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

Two things the fix is careful about — both are easy to get wrong:

- **Keep the hash server-side.** `CredentialRecord` is a private repository type; `AuthenticatedUser`
  (the value that flows out) still has **no** `PasswordHash`. The hash is read for verification and
  discarded. Don't "simplify" by adding `PasswordHash` to the returned model.
- **Don't leak *which* check failed.** Unknown user, inactive user, and wrong password all return the
  same `null` → the controller returns one generic `401 Invalid credentials`. If you split these into
  distinct errors ("no such user" vs "wrong password"), you hand attackers a **user-enumeration oracle**.

`VerifyCurrentPasswordAsync` (used by change-password) had the identical SQL-compare pattern and got the
identical treatment: fetch the hash, `PasswordHasher.Verify(...)`, return the boolean.

### The rule

> **The moment a hash is salted, it can't be matched in a `WHERE` clause. Fetch it and verify in code —
> and keep it out of every model, DTO, log, and audit row.** Also: one generic failure for
> unknown-user / inactive / wrong-password, so login can't be used to enumerate accounts.

---

## Lesson 3 — Migrating an existing storage format safely

The database was already full of old unsalted SHA-256 hashes. You can't re-hash them offline (you don't
have the plaintexts), and forcing every user to reset their password is hostile and often infeasible.

### The danger if you migrate carelessly

- **Force-reset everyone** → users locked out, support flooded, and (worse) a rushed "reset" flow is
  where new auth bugs get introduced.
- **Keep verifying old hashes forever** → the weak scheme never actually goes away; one "temporary"
  compatibility branch becomes permanent, and the finding is never really closed.

### ✅ The fix — verify both formats, upgrade lazily on next login

`PasswordHasher.Verify` accepts **both** formats and reports when it matched an old one:

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

Then the login path upgrades the row **once**, transparently, using the plaintext it just verified:

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

Details that matter:

- **Constant-time comparison.** Both `VerifyPbkdf2` and `VerifyLegacySha256` compare with
  `CryptographicOperations.FixedTimeEquals`, not `==`. A naïve byte/string `==` returns as soon as the
  first byte differs, and that timing difference is a real (if fiddly) side channel for forging a hash.
- **Don't stamp `PasswordUpdatedTime` on a format upgrade.** The user didn't change their password;
  stamping it would lie to any "password age" policy or audit.
- **The upgrade runs once.** After the first login post-migration, the row is PBKDF2 and the legacy branch
  is never taken again for that user. When the last legacy row is gone, delete `VerifyLegacySha256`.

### The rule

> **To retire a weak hash: verify old + new, and re-hash to the new scheme on the user's next successful
> login. Never force a mass reset, and never keep the weak path as "temporary" without a plan to delete it.**

---

## Bonus — the same mistake hid in a second place

The admin "create user / reset password" path had its own **inline** copy of the unsalted SHA-256 hash,
separate from `PasswordHasher`:

```csharp
// AppUserRepository.ReadDefaultPasswordHashAsync — BEFORE
var hash = SHA256.HashData(Encoding.UTF8.GetBytes(defaultPassword));
return Convert.ToHexString(hash).ToLowerInvariant();
```

```csharp
// AFTER — route through the one shared hasher
return PasswordHasher.Hash(defaultPassword);
```

The lesson: **a security-critical primitive must live in exactly one place.** Because the hashing was
*also* copy-pasted here, fixing `PasswordHasher` alone would have left every admin-created and
reset-password account still on the weak scheme. Grep for the *pattern* (`SHA256.HashData`,
`Convert.ToHexString`), not just the function name, when you fix a crypto bug — duplicates are how a
"fixed" vulnerability quietly survives.

### The rule

> **Define password hashing once and call it everywhere. If you find a second copy, that's a second bug.**

---

## Quick checklist — before you write auth code

- [ ] Password hashing uses a **slow, salted KDF** (PBKDF2/bcrypt/Argon2), never bare SHA/MD5.
- [ ] The hash carries its **salt + cost parameters** with it (self-describing string).
- [ ] Verification happens **in code**, not in a SQL `WHERE`, and the stored hash **never** enters a
      model / DTO / log / audit row.
- [ ] Hash comparison is **constant-time** (`CryptographicOperations.FixedTimeEquals`).
- [ ] Login returns **one generic failure** for unknown-user / inactive / wrong-password (no enumeration).
- [ ] There is **one** hashing helper, called from every write/verify site (no inline copies).
- [ ] Format changes migrate **lazily on next login**, with a plan to delete the legacy branch.
