using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

public interface IWithdrawalService
{
    Task<ServiceResult<object>> SaveBankAccountAsync(Guid userId, BankAccountRequest request);
    Task<ServiceResult<object>> GetBankAccountsAsync(Guid userId);
    Task<ServiceResult<object>> CreateWithdrawalAsync(Guid userId, WithdrawalRequestDto request);
    Task<ServiceResult<object>> GetHistoryAsync(Guid userId);
    Task<ServiceResult<object>> GetPendingAsync();
    Task<ServiceResult<object>> GetAllAsync();
    Task<ServiceResult<object>> ProcessWithdrawalAsync(Guid adminId, AdminProcessWithdrawalRequest request);
}
