using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IBankAccountRepository _bankAccountRepo;
    private readonly IWithdrawalRepository _withdrawalRepo;
    private readonly IWalletService _walletService;
    private readonly IWalletRepository _walletRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;

    public WithdrawalService(
        IBankAccountRepository bankAccountRepo,
        IWithdrawalRepository withdrawalRepo,
        IWalletService walletService,
        IWalletRepository walletRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork)
    {
        _bankAccountRepo = bankAccountRepo;
        _withdrawalRepo = withdrawalRepo;
        _walletService = walletService;
        _walletRepo = walletRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceResult<object>> SaveBankAccountAsync(Guid userId, BankAccountRequest request)
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankName = request.BankName.Trim(),
            AccountNumber = request.AccountNumber.Trim(),
            AccountHolder = request.AccountHolder.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        await _bankAccountRepo.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();
        return ServiceResult<object>.Ok(new { account.Id, account.BankName, account.AccountNumber, account.AccountHolder });
    }

    public async Task<ServiceResult<object>> GetBankAccountsAsync(Guid userId)
    {
        var accounts = await _bankAccountRepo.GetByUserIdAsync(userId);
        return ServiceResult<object>.Ok(accounts.Select(a => new
        {
            a.Id, a.BankName,
            AccountNumber = MaskAccountNumber(a.AccountNumber),
            a.AccountHolder,
            RawAccountNumber = a.AccountNumber
        }));
    }

    public async Task<ServiceResult<object>> CreateWithdrawalAsync(Guid userId, WithdrawalRequestDto request)
    {
        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        var balance = wallet?.Balance ?? 0;

        if (balance < request.Amount)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số điểm không đủ.");

        var bankAccount = await _bankAccountRepo.GetByIdAsync(request.BankAccountId);
        if (bankAccount == null || bankAccount.UserId != userId)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Tài khoản ngân hàng không hợp lệ.");

        // request.Amount is in points
        var withdrawal = new WithdrawalRequest
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankAccountId = request.BankAccountId,
            Amount = request.Amount, // points
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _withdrawalRepo.AddAsync(withdrawal);

        var deductResult = await _walletService.DeductFundsAsync(userId, request.Amount, $"withdrawal_{withdrawal.Id}");
        if (!deductResult.IsSuccess)
        {
            return deductResult;
        }

        await _unitOfWork.SaveChangesAsync();

        var vndAmount = request.Amount * _walletService.GetPointsPerVnd();
        return ServiceResult<object>.Ok(new { withdrawal.Id, points = withdrawal.Amount, vndAmount, withdrawal.Status, withdrawal.CreatedAt });
    }

    public async Task<ServiceResult<object>> GetHistoryAsync(Guid userId)
    {
        var list = await _withdrawalRepo.GetByUserIdAsync(userId);
        return ServiceResult<object>.Ok(list.Select(w => new
        {
            w.Id,
            w.Amount,
            w.Status,
            CreatedAt = ToUtc(w.CreatedAt),
            ProcessedAt = ToUtc(w.ProcessedAt),
            BankName = w.BankAccount?.BankName,
            AccountNumber = w.BankAccount != null ? MaskAccountNumber(w.BankAccount.AccountNumber) : null,
            AccountHolder = w.BankAccount?.AccountHolder,
            Note = w.Note
        }));
    }

    public async Task<ServiceResult<object>> GetPendingAsync()
    {
        var list = await _withdrawalRepo.GetPendingAsync();
        return ServiceResult<object>.Ok(list.Select(w => new
        {
            w.Id,
            w.Amount,
            w.Status,
            CreatedAt = ToUtc(w.CreatedAt),
            UserName = w.User?.FullName ?? w.User?.Email ?? "",
            BankName = w.BankAccount?.BankName,
            AccountNumber = w.BankAccount != null ? MaskAccountNumber(w.BankAccount.AccountNumber) : null,
            AccountHolder = w.BankAccount?.AccountHolder
        }));
    }

    public async Task<ServiceResult<object>> GetAllAsync()
    {
        var list = await _withdrawalRepo.GetAllAsync();
        return ServiceResult<object>.Ok(list.Select(w => new
        {
            w.Id,
            w.Amount,
            w.Status,
            CreatedAt = ToUtc(w.CreatedAt),
            ProcessedAt = ToUtc(w.ProcessedAt),
            UserName = w.User?.FullName ?? w.User?.Email ?? "",
            BankName = w.BankAccount?.BankName,
            AccountNumber = w.BankAccount != null ? MaskAccountNumber(w.BankAccount.AccountNumber) : null,
            AccountHolder = w.BankAccount?.AccountHolder,
            Note = w.Note
        }));
    }

    public async Task<ServiceResult<object>> ProcessWithdrawalAsync(Guid adminId, AdminProcessWithdrawalRequest request)
    {
        // Validate status value
        if (request.Status != "completed" && request.Status != "rejected")
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Trạng thái không hợp lệ. Chỉ chấp nhận 'completed' hoặc 'rejected'.");

        var withdrawal = await _withdrawalRepo.GetByIdAsync(request.WithdrawalId);
        if (withdrawal == null)
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy đơn rút tiền.");

        if (withdrawal.Status != "pending")
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Đơn đã được xử lý.");

        withdrawal.Status = request.Status;
        withdrawal.ProcessedAt = DateTime.UtcNow;
        withdrawal.ProcessedByUserId = adminId;
        withdrawal.Note = request.Note;

        if (request.Status == "rejected")
        {
            await _walletService.AddPointsAsync(withdrawal.UserId, withdrawal.Amount, $"refund_{withdrawal.Id}");
        }

        await _withdrawalRepo.UpdateAsync(withdrawal);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(new { message = "Đã xử lý đơn rút tiền." });
    }

    private static string MaskAccountNumber(string number)
    {
        if (string.IsNullOrEmpty(number) || number.Length <= 6) return number ?? "";
        return number[..3] + new string('*', number.Length - 6) + number[^3..];
    }

    // DB lưu UTC trong cột timestamp; Npgsql đọc ra với Kind=Unspecified
    // nên phải gán lại Kind=Utc để JSON serialize kèm "Z", trình duyệt hiển thị đúng giờ địa phương.
    private static DateTime ToUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? ToUtc(DateTime? value) => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : null;
}
