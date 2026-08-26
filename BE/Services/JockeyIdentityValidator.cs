using System;
using System.Text.RegularExpressions;

namespace HorseRacing.Services;

/// <summary>
/// J-REG-VALIDATION: shared Jockey identity/age validation.
///
/// Phone is REQUIRED (null/empty/whitespace-only rejected) and must be literal ASCII digits only
/// (<c>^[0-9]+$</c>) — no letters, spaces, +, -, parentheses, punctuation, or Unicode-numeric
/// characters (e.g. fullwidth/Arabic-Indic digits). IdCardNumber is REQUIRED and must be exactly
/// 9 (CMND, legacy) or 12 (CCCD) ASCII digits (<c>^([0-9]{9}|[0-9]{12})$</c>), validated as a
/// STRING so a leading zero (e.g. "012345678901") is never lost to numeric parsing.
///
/// Both patterns use an explicit <c>[0-9]</c> character class, never .NET's <c>\d</c> — by
/// default (no RegexOptions.ECMAScript) <c>\d</c> matches every Unicode category-Nd digit, which
/// includes non-ASCII digits (e.g. fullwidth U+FF10-FF19, Arabic-Indic U+0660-0669), silently
/// defeating an "ASCII digits only" business rule.
///
/// DateOfBirth must make the Jockey STRICTLY older than 18 — Age &gt; 18, never &gt;= 18 — computed
/// by first finding the actual 18th-birthday DATE (<c>dateOfBirth.Date.AddYears(18)</c>), then
/// requiring <c>referenceDate.Date &gt; eighteenthBirthday</c> (never &gt;=). Never TotalDays/365
/// (wrong around leap years).
///
/// IMPORTANT: the 18-year shift is applied to the DOB, never to the reference date. An earlier
/// version of this rule computed <c>dateOfBirth &lt; referenceDate.AddYears(-18)</c> instead, which
/// looks equivalent but is NOT: on a leap-day reference date (e.g. 2028-02-29), shifting the
/// reference back 18 years lands in a non-leap year (2010) and .NET clamps that result to
/// 2010-02-28 — colliding with a DOB of exactly 2010-02-28, even though that person is genuinely
/// 18 years + 1 day old on 2028-02-29 and must pass. Computing the 18th birthday from the DOB
/// instead (2010-02-28.AddYears(18) = 2028-02-28, no clamp needed since Feb 28 always exists)
/// avoids that false rejection entirely. .NET's DateTime.AddYears still clamps Feb 29 -> Feb 28
/// when the DOB itself is a leap day and the +18 target year isn't a leap year — the FE helper
/// (jockeyRegistrationValidation.js) replicates that exact clamp on the same DOB-forward direction
/// so the two never disagree at any boundary.
///
/// Currently the only live write path for these three Jockey fields is
/// AuthService.RegisterAsync — Jockey Profile edit only touches User.PhoneNumber (a different
/// field, never shown in Admin Jockey Review), and Admin has no direct edit endpoint for
/// Phone/IdCardNumber/DateOfBirth (Approve/Reject/Lock/Unlock never touch them). This lives as a
/// standalone static class specifically so a future write path can reuse the exact same rules
/// instead of re-deriving them.
/// </summary>
public static class JockeyIdentityValidator
{
    private static readonly Regex PhonePattern = new(@"^[0-9]+$", RegexOptions.Compiled);
    private static readonly Regex IdCardNumberPattern = new(@"^([0-9]{9}|[0-9]{12})$", RegexOptions.Compiled);

    public static bool IsValidPhone(string? phone)
        => !string.IsNullOrWhiteSpace(phone) && PhonePattern.IsMatch(phone);

    public static bool IsValidIdCardNumber(string? idCardNumber)
        => !string.IsNullOrWhiteSpace(idCardNumber) && IdCardNumberPattern.IsMatch(idCardNumber);

    public static bool IsOlderThan18(DateTime dateOfBirth, DateTime referenceDate)
    {
        var eighteenthBirthday = dateOfBirth.Date.AddYears(18);
        return referenceDate.Date > eighteenthBirthday;
    }
}
