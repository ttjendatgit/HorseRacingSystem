using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IPredictionService
{
    Task<ServiceResult<object>> CreatePredictionAsync(Guid userId, PredictionCreateRequest request);
    Task<ServiceResult<object>> GetMyPredictionsAsync(Guid userId);
    Task<ServiceResult<object>> SettlePredictionAsync(Guid raceId, Guid winningHorseId);
}
