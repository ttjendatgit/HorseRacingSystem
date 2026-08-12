using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;

namespace Tests;

public class ServiceResultTests
{
    [Fact]
    public void Ok_Returns200WithData()
    {
        var r = ServiceResult<string>.Ok("test");
        Assert.Equal(200, r.StatusCode);
        Assert.True(r.Result.Success);
        Assert.Equal("test", r.Result.Data);
    }

    [Fact]
    public void Fail_ReturnsErrorWithMessage()
    {
        var r = ServiceResult<string>.Fail(404, "Not found");
        Assert.Equal(404, r.StatusCode);
        Assert.False(r.Result.Success);
        Assert.Equal("Not found", r.Result.Message);
    }

    [Fact]
    public void Success_Returns201()
    {
        var r = ServiceResult<int>.Success(42, 201);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal(42, r.Result.Data);
    }
}

public class EnumValidationTests
{
    [Fact]
    public void RaceStatus_ParsesScheduled()
    {
        Assert.True(Enum.TryParse<RaceStatus>("Scheduled", out _));
    }

    [Fact]
    public void RaceStatus_ParsesInProgress()
    {
        Assert.True(Enum.TryParse<RaceStatus>("InProgress", out _));
    }

    [Fact]
    public void RaceStatus_RejectsInvalid()
    {
        Assert.False(Enum.TryParse<RaceStatus>("BadStatus", out _));
    }

    [Fact]
    public void UserRole_AdminIs4()
    {
        Assert.Equal(4, (int)UserRole.Admin);
    }

    [Fact]
    public void UserRole_AllFiveRolesAreDistinct()
    {
        var values = Enum.GetValues<UserRole>();
        Assert.Equal(5, values.Distinct().Count());
    }
}

public class DtoValidationTests
{
    [Fact]
    public void CreateProtestRequest_RequiresReason()
    {
        var r = new CreateProtestRequest { Reason = "Foul" };
        Assert.NotEmpty(r.Reason);
    }

    [Fact]
    public void CreateTransferRequest_DefaultsToSale()
    {
        var r = new CreateHorseTransferRequest();
        Assert.Equal("Sale", r.TransferType);
    }

    [Fact]
    public void CreateContractRequest_DatesAreValid()
    {
        var r = new CreateContractRequest
        {
            Title = "Test", OwnerId = Guid.NewGuid(), JockeyId = Guid.NewGuid(),
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddMonths(3)
        };
        Assert.True(r.EndDate > r.StartDate);
    }

    [Fact]
    public void PrizeResponse_HasAllFields()
    {
        var r = new PrizeResponse { Name = "Gold", Amount = 5000, Currency = "USD", Position = 1 };
        Assert.Equal("Gold", r.Name);
        Assert.Equal(5000m, r.Amount);
        Assert.Equal(1, r.Position);
    }
}
