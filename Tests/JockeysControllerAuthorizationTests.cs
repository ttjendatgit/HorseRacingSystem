using System.Reflection;
using HorseRacing.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Tests;

public class JockeysControllerAuthorizationTests
{
    [Fact]
    public void GetAvailableJockeys_AllowsJockeyOwnersToChooseAnotherJockey()
    {
        var method = typeof(JockeysController).GetMethod(
            nameof(JockeysController.GetAvailableJockeys),
            BindingFlags.Instance | BindingFlags.Public);

        var authorize = Assert.Single(
            method!.GetCustomAttributes<AuthorizeAttribute>());
        var roles = (authorize.Roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Contains("HorseOwner", roles);
        Assert.Contains("Jockey", roles);
        Assert.Contains("Admin", roles);
    }
}
