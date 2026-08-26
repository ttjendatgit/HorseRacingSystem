using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Options;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

/// <summary>
/// J-REG-VALIDATION: Jockey identity (Phone/IdCardNumber) and age (&gt;18, strictly) validation.
/// Two layers are tested: the pure JockeyIdentityValidator (unit-level, exhaustive format/boundary
/// coverage) and AuthService.RegisterAsync (integration-level, proving the rules are actually
/// wired in and that HorseOwner/Spectator registration is unaffected). Uses the same direct-
/// service-instantiation pattern as JRegFileUploadTests.cs — no WebApplicationFactory/HTTP
/// pipeline exists anywhere in this suite.
/// </summary>
public class JRegValidationTests
{
    // ── Pure JockeyIdentityValidator — Phone ────────────────────────────────────────────────

    [Theory]
    [InlineData("0353545355", true)]
    [InlineData("035-354-5355", false)]
    [InlineData("+84353545355", false)]
    [InlineData("abc123", false)]
    [InlineData("035 354 5355", false)]
    [InlineData("èeertt", false)]
    [InlineData("(035)3545355", false)]
    public void IsValidPhone_MatchesBusinessRule(string phone, bool expected)
    {
        Assert.Equal(expected, JockeyIdentityValidator.IsValidPhone(phone));
    }

    [Fact]
    public void IsValidPhone_NeverSanitizesInvalidInput()
    {
        // "abc123" must be rejected outright — never silently transformed to "123".
        Assert.False(JockeyIdentityValidator.IsValidPhone("abc123"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void IsValidPhone_Required_NullEmptyWhitespaceRejected(string? phone)
    {
        Assert.False(JockeyIdentityValidator.IsValidPhone(phone));
    }

    [Theory]
    [InlineData("０３５３５４５３５５")] // fullwidth digits (Unicode Nd, not ASCII 0-9)
    [InlineData("٠٣٥٣٥٤٥٣٥٥")]         // Arabic-Indic digits (Unicode Nd, not ASCII 0-9)
    public void IsValidPhone_UnicodeNumericRejected_AsciiOnly(string unicodePhone)
    {
        // .NET's \d (without RegexOptions.ECMAScript) matches any Unicode category-Nd digit,
        // which would wrongly accept these — proves the [0-9] character class is actually in use.
        Assert.False(JockeyIdentityValidator.IsValidPhone(unicodePhone));
    }

    // ── Pure JockeyIdentityValidator — IdCardNumber ─────────────────────────────────────────

    [Theory]
    [InlineData("123456789", true)]      // 9-digit CMND
    [InlineData("012345678901", true)]   // 12-digit CCCD, leading zero
    [InlineData("12345678", false)]      // 8
    [InlineData("1234567890", false)]    // 10
    [InlineData("12345678901", false)]   // 11
    [InlineData("1234567890123", false)] // 13
    [InlineData("ABC123456", false)]
    [InlineData("123-456-789", false)]
    [InlineData("123 456 789", false)]
    [InlineData("!@#$%", false)]
    public void IsValidIdCardNumber_MatchesBusinessRule(string idCardNumber, bool expected)
    {
        Assert.Equal(expected, JockeyIdentityValidator.IsValidIdCardNumber(idCardNumber));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void IsValidIdCardNumber_Required_NullEmptyWhitespaceRejected(string? idCardNumber)
    {
        Assert.False(JockeyIdentityValidator.IsValidIdCardNumber(idCardNumber));
    }

    [Theory]
    [InlineData("０１２３４５６７８")] // fullwidth digits, 9 chars
    [InlineData("٠١٢٣٤٥٦٧٨")]         // Arabic-Indic digits, 9 chars
    public void IsValidIdCardNumber_UnicodeNumericRejected_AsciiOnly(string unicodeId)
    {
        Assert.False(JockeyIdentityValidator.IsValidIdCardNumber(unicodeId));
    }

    // ── Pure JockeyIdentityValidator — Age (reference date 2026-08-23) ──────────────────────

    private static readonly DateTime ReferenceDate = new(2026, 8, 23);

    [Theory]
    [InlineData("2008-08-22", true)]  // one day older than 18
    [InlineData("2008-08-23", false)] // exactly 18 — must be rejected (Age > 18, not >=)
    [InlineData("2008-08-24", false)] // one day under 18
    [InlineData("2027-01-01", false)] // future DOB
    [InlineData("2000-02-29", true)]  // leap-year DOB — proves calendar (not TotalDays/365) arithmetic
    public void IsOlderThan18_CalendarBoundary(string dob, bool expected)
    {
        Assert.Equal(expected, JockeyIdentityValidator.IsOlderThan18(DateTime.Parse(dob), ReferenceDate));
    }

    [Fact]
    public void DotNetAddYears_Feb29DateOfBirth_ClampsToFeb28InNonLeapTargetYear()
    {
        // Locks in the exact .NET runtime behavior IsOlderThan18 relies on: AddYears(18) applied
        // to a Feb-29 DOB, landing in a non-leap target year (2026), clamps to Feb 28 rather than
        // throwing or rolling forward into March.
        var dateOfBirth = new DateTime(2008, 2, 29);
        var eighteenthBirthday = dateOfBirth.AddYears(18);
        Assert.Equal(new DateTime(2026, 2, 28), eighteenthBirthday);
    }

    [Theory]
    [InlineData("2028-02-29", "2010-02-28", true)]  // reference is one day after the true 18th birthday (2028-02-28) => PASS
    [InlineData("2028-02-28", "2010-02-28", false)] // reference is exactly the true 18th birthday => FAIL
    public void IsOlderThan18_LeapReferenceDate_UsesDobDerivedBirthday_NotReferenceShift(string referenceDate, string dob, bool expected)
    {
        // Counter-example that proves shifting the REFERENCE date back 18 years (the previous,
        // buggy implementation) is wrong: DOB 2010-02-28 turns exactly 18 on 2028-02-28 (Feb 28
        // exists every year, no ambiguity). On 2028-02-29 (one day later) they are genuinely
        // 18 years + 1 day old and must pass. The old formula
        // (referenceDate.AddYears(-18), clamped to 2010-02-28) collided both dates at
        // 2010-02-28 and wrongly rejected the first case. The FE helper's addYearsClampedUtc() in
        // jockeyRegistrationValidation.js mirrors this exact corrected DOB-forward direction so BE
        // and FE never disagree at this boundary.
        Assert.Equal(expected, JockeyIdentityValidator.IsOlderThan18(DateTime.Parse(dob), DateTime.Parse(referenceDate)));
    }

    [Theory]
    [InlineData("2026-02-28", false)] // exactly the computed (clamped) 18th birthday
    [InlineData("2026-03-01", true)]  // the following calendar day
    public void IsOlderThan18_LeapDayDateOfBirth_ClampedEighteenthBirthday(string referenceDate, bool expected)
    {
        // DOB 2008-02-29 (leap day) + 18 years lands in 2026, not a leap year, so the 18th
        // birthday clamps to 2026-02-28 (mirrors DateTime.AddYears' own documented clamp).
        var dateOfBirth = new DateTime(2008, 2, 29);
        Assert.Equal(expected, JockeyIdentityValidator.IsOlderThan18(dateOfBirth, DateTime.Parse(referenceDate)));
    }

    // ── AuthService.RegisterAsync integration ────────────────────────────────────────────────

    private static AuthService BuildAuthService(RaceLifecycleTests.LifecycleFixture f)
    {
        var jwt = new JwtTokenService(Options.Create(new JwtOptions
        {
            Key = "test-signing-key-at-least-32-characters-long!!",
            Issuer = "test-issuer",
            Audience = "test-audience",
            ExpiresMinutes = 60,
        }));
        return new AuthService(new UserRepository(f.Db), new OwnerRepository(f.Db), new JockeyRepository(f.Db), f.UnitOfWork, jwt);
    }

    private static RegisterRequest ValidJockeyRequest(string tag, Action<RegisterRequest>? mutate = null)
    {
        var request = new RegisterRequest
        {
            Email = $"jockey-{tag}-{Guid.NewGuid():N}@test.local",
            Password = "Password123!",
            Role = UserRole.Jockey,
            FullName = $"Jockey {tag}",
            LicenseNumber = $"LIC-{tag}",
            Phone = "0900000000",
            IdCardNumber = "079000000000",
            DateOfBirth = new DateTime(1995, 1, 1), // safely > 18 regardless of when the suite runs
        };
        mutate?.Invoke(request);
        return request;
    }

    [Fact]
    public async Task RegisterAsync_Jockey_ValidPhone_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await BuildAuthService(f).RegisterAsync(ValidJockeyRequest("phone-valid"));
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("035-354-5355")]
    [InlineData("035 354 5355")]
    public async Task RegisterAsync_Jockey_InvalidPhone_Rejected(string badPhone)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest($"phone-invalid-{Math.Abs(badPhone.GetHashCode())}", r => r.Phone = badPhone);
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Số điện thoại chỉ được chứa chữ số.", result.Result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_Jockey_NullEmptyWhitespacePhone_Rejected(string? phone)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest($"phone-blank-{(phone ?? "null").Length}", r => r.Phone = phone);
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Số điện thoại chỉ được chứa chữ số.", result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_UnicodeNumericPhone_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("phone-unicode", r => r.Phone = "０３５３５４５３５５");
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Số điện thoại chỉ được chứa chữ số.", result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_9DigitCmnd_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("cmnd-9", r => r.IdCardNumber = "123456789");
        var result = await BuildAuthService(f).RegisterAsync(request);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_12DigitCccd_Succeeds_LeadingZeroPreserved()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("cccd-12", r => r.IdCardNumber = "012345678901");
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.True(result.Result.Success, result.Result.Message);
        var jockey = await f.Db.Jockeys.SingleAsync(j => j.UserId == result.Result.Data!.UserId);
        Assert.Equal("012345678901", jockey.IdCardNumber);
    }

    [Theory]
    [InlineData("ABC123456")]
    [InlineData("123-456-789")]
    [InlineData("12345678")]
    [InlineData("1234567890")]
    public async Task RegisterAsync_Jockey_InvalidIdCardNumber_Rejected(string badId)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest($"id-invalid-{Math.Abs(badId.GetHashCode())}", r => r.IdCardNumber = badId);
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("CCCD/CMND phải gồm 9 hoặc 12 chữ số.", result.Result.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterAsync_Jockey_NullEmptyWhitespaceIdCardNumber_Rejected(string? idCardNumber)
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest($"id-blank-{(idCardNumber ?? "null").Length}", r => r.IdCardNumber = idCardNumber);
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("CCCD/CMND phải gồm 9 hoặc 12 chữ số.", result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_UnicodeNumericIdCardNumber_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("id-unicode", r => r.IdCardNumber = "０１２３４５６７８");
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("CCCD/CMND phải gồm 9 hoặc 12 chữ số.", result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_OlderThan18_Succeeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("age-ok", r => r.DateOfBirth = DateTime.UtcNow.AddYears(-19));
        var result = await BuildAuthService(f).RegisterAsync(request);
        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_ExactlyEighteen_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var today = DateTime.UtcNow.Date;
        var request = ValidJockeyRequest("age-exact18", r => r.DateOfBirth = today.AddYears(-18));
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Kỵ sĩ phải trên 18 tuổi.", result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_UnderEighteen_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("age-under18", r => r.DateOfBirth = DateTime.UtcNow.AddYears(-17));
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_FutureDateOfBirth_Rejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("age-future", r => r.DateOfBirth = DateTime.UtcNow.AddYears(1));
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_MissingDateOfBirth_Rejected()
    {
        // The age rule cannot be evaluated without a DateOfBirth, so a missing one fails the same
        // way an under-18 one does rather than silently passing.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var request = ValidJockeyRequest("age-missing", r => r.DateOfBirth = null);
        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Kỵ sĩ phải trên 18 tuổi.", result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_Jockey_CalendarBoundary_ExactDayFlipsResult()
    {
        // Regression lock proving the service-level integration uses the exact
        // dateOfBirth < today.AddYears(-18) formula, not an approximation — one day on either
        // side of the exact 18-year boundary flips the outcome.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var today = DateTime.UtcNow.Date;

        var oneDayOlder = ValidJockeyRequest("boundary-older", r => r.DateOfBirth = today.AddYears(-18).AddDays(-1));
        var olderResult = await BuildAuthService(f).RegisterAsync(oneDayOlder);
        Assert.True(olderResult.Result.Success, olderResult.Result.Message);

        var oneDayYounger = ValidJockeyRequest("boundary-younger", r => r.DateOfBirth = today.AddYears(-18).AddDays(1));
        var youngerResult = await BuildAuthService(f).RegisterAsync(oneDayYounger);
        Assert.False(youngerResult.Result.Success);
    }

    // ── Safety: no partial persistence, ApprovalStatus.Pending preserved ────────────────────

    [Fact]
    public async Task RegisterAsync_Jockey_InvalidData_NoPartialUserOrJockeyCreated()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var email = $"jockey-partial-{Guid.NewGuid():N}@test.local";
        var request = ValidJockeyRequest("partial", r =>
        {
            r.Email = email;
            r.Phone = "abcxyz";
            r.IdCardNumber = "bad";
            r.DateOfBirth = DateTime.UtcNow.AddYears(-10);
        });

        var result = await BuildAuthService(f).RegisterAsync(request);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(0, await f.Db.Users.CountAsync(u => u.Email == email));
        Assert.Equal(0, await f.Db.Jockeys.CountAsync());
        Assert.Equal(0, await f.Db.Owners.CountAsync());
    }

    [Fact]
    public async Task RegisterAsync_Jockey_ValidData_RemainsApprovalStatusPending()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await BuildAuthService(f).RegisterAsync(ValidJockeyRequest("pending-check"));

        Assert.True(result.Result.Success, result.Result.Message);
        var jockey = await f.Db.Jockeys.SingleAsync(j => j.UserId == result.Result.Data!.UserId);
        Assert.Equal(ApprovalStatus.Pending, jockey.ApprovalStatus);
    }

    // ── HorseOwner/Spectator must never be subject to Jockey-only rules ─────────────────────

    [Fact]
    public async Task RegisterAsync_HorseOwner_InvalidPhoneFormat_StillSucceeds_RuleNotApplied()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await BuildAuthService(f).RegisterAsync(new RegisterRequest
        {
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            Password = "Password123!",
            Role = UserRole.HorseOwner,
            FullName = "Owner Garbage Phone",
            Phone = "abc-not-a-phone", // would fail the Jockey phone rule — must not matter here
        });

        Assert.True(result.Result.Success, result.Result.Message);
    }

    [Fact]
    public async Task RegisterAsync_HorseOwner_NoDateOfBirth_StillSucceeds_AgeRuleNotApplied()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var result = await BuildAuthService(f).RegisterAsync(new RegisterRequest
        {
            Email = $"owner-{Guid.NewGuid():N}@test.local",
            Password = "Password123!",
            Role = UserRole.HorseOwner,
            FullName = "Owner No DOB",
        });

        Assert.True(result.Result.Success, result.Result.Message);
    }
}
