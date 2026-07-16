namespace CMS.API.Security;

/// <summary>
/// The password-complexity rule enforced when a user changes their own password: at least 8 characters
/// and at least 3 of the 4 character classes (uppercase / lowercase / digit / symbol). The same rule is
/// mirrored client-side in the Angular change-password form, but the server is authoritative.
/// </summary>
public static class PasswordPolicy
{
    /// <summary>Minimum length.</summary>
    public const int MinLength = 8;

    /// <summary>Minimum number of distinct character classes (of the four) that must be present.</summary>
    public const int MinCharacterClasses = 3;

    /// <summary>
    /// The bilingual message shown when the new password fails the policy. Kept as a single constant so the
    /// controller returns exactly the wording the spec requires.
    /// </summary>
    public const string Requirement =
        "密碼長度至少需 8 碼，且內容須至少包含四種字元的其中三種：大寫英文／小寫英文／數字／符號 " +
        "(Password must be at least 8 characters and contain at least 3 of the 4 classes: " +
        "uppercase / lowercase / digit / symbol.)";

    /// <summary>
    /// Returns <c>true</c> when <paramref name="password"/> is at least <see cref="MinLength"/> characters
    /// long and contains at least <see cref="MinCharacterClasses"/> of the four character classes. A
    /// "symbol" is any character that is neither a letter nor a digit.
    /// </summary>
    public static bool IsCompliant(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return false;
        }

        bool hasUpper = false, hasLower = false, hasDigit = false, hasSymbol = false;
        foreach (var c in password)
        {
            if (char.IsUpper(c)) hasUpper = true;
            else if (char.IsLower(c)) hasLower = true;
            else if (char.IsDigit(c)) hasDigit = true;
            else hasSymbol = true;
        }

        var classes = (hasUpper ? 1 : 0) + (hasLower ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0);
        return classes >= MinCharacterClasses;
    }
}
