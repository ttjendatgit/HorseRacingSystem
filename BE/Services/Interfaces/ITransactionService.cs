using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface ITransactionService
{
    Task<ServiceResult<object>> CreatePendingAsync(Guid userId, decimal amount);
    Task<ServiceResult<object>> HandleWebhookAsync(SepayWebhookRequest request);
    Task<ServiceResult<object>> CheckTransactionAsync(Guid userId, DateTime since);
    Task<ServiceResult<object>> GetHistoryAsync(Guid userId);
}
