using System;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HorseRacing.Controllers;

[ApiController]
[Route("api/predictions")]
[Authorize(Roles = "Spectator")]
public class PredictionsController : ControllerBase
{
    private readonly IPredictionService _predictionService;

    public PredictionsController(IPredictionService predictionService)
    {
        _predictionService = predictionService;
    }

    [HttpPost]
    public async Task<ActionResult> CreatePrediction(PredictionCreateRequest request)
    {
        var userId = GetUserId();
        var result = await _predictionService.CreatePredictionAsync(userId, request);
        return StatusCode(result.StatusCode, result.Result);
    }

    [HttpGet("mine")]
    public async Task<ActionResult> GetMyPredictions()
    {
        var userId = GetUserId();
        var result = await _predictionService.GetMyPredictionsAsync(userId);
        return StatusCode(result.StatusCode, result.Result);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return value == null ? Guid.Empty : Guid.Parse(value);
    }
}
