using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HorseRacing.Controllers;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Options;
using HorseRacing.Repositories;
using HorseRacing.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Tests;

/// <summary>
/// J-REG-FILE: root cause was POST /api/auth/upload-document being marked [Authorize] even
/// though RegisterJockeyPage calls it BEFORE the Jockey account/JWT exists — every registration-
/// time file upload attempt failed with 401 regardless of file validity, size, or type. The fix
/// removes that attribute (AuthController has no class-level [Authorize], so it was the only
/// gate). Following the existing HorseOwnerAuthorizationTests convention: this test suite has no
/// WebApplicationFactory/HTTP pipeline anywhere, so [Authorize] presence is verified via
/// reflection (the actual gate lives in ASP.NET Core's authorization middleware, which never runs
/// when a controller is instantiated directly in-process). AuthService.RegisterAsync itself was
/// never broken — the behavioral tests below lock in that it still sets the correct Jockey state
/// regardless of whether a LicenseFile URL is present.
/// </summary>
public class JRegFileUploadTests
{
    private static AuthorizeAttribute? GetMethodAuthorize(Type controllerType, string methodName)
    {
        var method = controllerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
        return method?.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().FirstOrDefault();
    }

    [Fact]
    public void UploadDocument_NoAuthorizeRequirement_ReachableAnonymously()
    {
        Assert.Null(GetMethodAuthorize(typeof(AuthController), nameof(AuthController.UploadDocument)));
        Assert.False(typeof(AuthController).GetCustomAttributes(typeof(AuthorizeAttribute), false).Any());
    }

    [Fact]
    public void Register_and_Login_RemainAnonymouslyReachable()
    {
        // Unchanged by this fix — asserted here so a future regression on the sibling pre-auth
        // endpoints is caught alongside the upload-document fix.
        Assert.Null(GetMethodAuthorize(typeof(AuthController), nameof(AuthController.Register)));
        Assert.Null(GetMethodAuthorize(typeof(AuthController), nameof(AuthController.Login)));
    }

    [Fact]
    public void GetProfile_StillRequiresAuthorize()
    {
        // Proves the fix is scoped to UploadDocument only — GetProfile (an authenticated,
        // post-login endpoint) must still be protected.
        Assert.NotNull(GetMethodAuthorize(typeof(AuthController), nameof(AuthController.GetProfile)));
    }

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

    [Fact]
    public async Task RegisterAsync_JockeyWithUploadedLicenseFileUrl_Succeeds_SetsApprovalPending()
    {
        // Simulates the corrected flow at the service layer: FE has already uploaded the file via
        // the now-anonymous /api/auth/upload-document and received a URL string back — that URL,
        // never a raw file/base64, is what RegisterRequest.LicenseFile actually carries.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var service = BuildAuthService(f);

        var result = await service.RegisterAsync(new RegisterRequest
        {
            Email = $"jockey-{Guid.NewGuid():N}@test.local",
            Password = "Password123!",
            Role = UserRole.Jockey,
            FullName = "Test Jockey",
            LicenseNumber = "LIC-0001",
            LicenseFile = "https://res.cloudinary.com/demo/documents/license-abc123.pdf",
            Phone = "0900000001",
            IdCardNumber = "079000000001",
            DateOfBirth = DateTime.UtcNow.AddYears(-25),
        });

        Assert.True(result.Result.Success, result.Result.Message);
        var userId = result.Result.Data!.UserId;

        var user = await f.Db.Users.SingleAsync(u => u.Id == userId);
        Assert.Equal(UserRole.Jockey, user.Role);
        Assert.True(user.IsActive);

        var jockey = await f.Db.Jockeys.SingleAsync(j => j.UserId == userId);
        Assert.Equal(ApprovalStatus.Pending, jockey.ApprovalStatus);
        Assert.Equal("https://res.cloudinary.com/demo/documents/license-abc123.pdf", jockey.LicenseFile);
    }

    [Fact]
    public async Task RegisterAsync_JockeyWithoutLicenseFile_StillSucceeds()
    {
        // LicenseFile is `string?` end-to-end (DTO and DB column) — optional at the backend
        // contract level. Whether the FE form requires a file before submit is a UI concern, not
        // a backend requirement, and is unaffected by this fix.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var service = BuildAuthService(f);

        var result = await service.RegisterAsync(new RegisterRequest
        {
            Email = $"jockey-{Guid.NewGuid():N}@test.local",
            Password = "Password123!",
            Role = UserRole.Jockey,
            FullName = "Test Jockey No File",
            LicenseNumber = "LIC-0002",
            LicenseFile = null,
            Phone = "0900000002",
            IdCardNumber = "079000000002",
            DateOfBirth = DateTime.UtcNow.AddYears(-25),
        });

        Assert.True(result.Result.Success, result.Result.Message);
        var jockey = await f.Db.Jockeys.SingleAsync(j => j.LicenseNumber == "LIC-0002");
        Assert.Equal(ApprovalStatus.Pending, jockey.ApprovalStatus);
        Assert.Null(jockey.LicenseFile);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_Rejected_NoPartialJockeyCreated()
    {
        // Failure-safety check (Part 7): a rejected registration must not leave a partially
        // created Jockey/User behind.
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var service = BuildAuthService(f);
        var email = $"jockey-{Guid.NewGuid():N}@test.local";

        var first = await service.RegisterAsync(new RegisterRequest
        {
            Email = email, Password = "Password123!", Role = UserRole.Jockey,
            FullName = "First", LicenseNumber = "LIC-DUP",
            LicenseFile = "https://res.cloudinary.com/demo/documents/dup.pdf",
            Phone = "0900000003", IdCardNumber = "079000000003", DateOfBirth = DateTime.UtcNow.AddYears(-25),
        });
        Assert.True(first.Result.Success, first.Result.Message);

        var second = await service.RegisterAsync(new RegisterRequest
        {
            Email = email, Password = "Password123!", Role = UserRole.Jockey,
            FullName = "Second", LicenseNumber = "LIC-DUP-2",
            LicenseFile = "https://res.cloudinary.com/demo/documents/dup2.pdf",
            Phone = "0900000004", IdCardNumber = "079000000004", DateOfBirth = DateTime.UtcNow.AddYears(-25),
        });

        Assert.False(second.Result.Success);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(1, await f.Db.Users.CountAsync(u => u.Email == email));
        Assert.Equal(1, await f.Db.Jockeys.CountAsync(j => j.LicenseNumber == "LIC-DUP"));
        Assert.Equal(0, await f.Db.Jockeys.CountAsync(j => j.LicenseNumber == "LIC-DUP-2"));
    }
}
