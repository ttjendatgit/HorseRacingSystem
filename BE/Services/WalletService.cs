using System;
using System.Threading.Tasks;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace HorseRacing.Services;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly decimal _pointsPerVnd;

    public WalletService(
        IWalletRepository walletRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IConfiguration config)
    {
        _walletRepo = walletRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _pointsPerVnd = config.GetValue<decimal>("Wallet:PointsPerVnd", 1000);
    }

    /// <summary>
    /// Lấy thông tin số dư ví hiện tại và tỷ lệ quy đổi điểm/VNĐ của người dùng.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng.</param>
    /// <returns>Số dư ví hiện tại và tỷ lệ quy đổi điểm.</returns>
    public async Task<ServiceResult<object>> GetBalanceAsync(Guid userId)
    {
        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        return ServiceResult<object>.Ok(new { balance = wallet?.Balance ?? 0, pointsPerVnd = _pointsPerVnd });
    }

    /// <summary>Nạp tiền VNĐ → quy đổi thành điểm</summary>
    public async Task<ServiceResult<object>> AddFundsAsync(Guid userId, decimal amountVnd, string reference)
    {
        if (amountVnd <= 0)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số tiền không hợp lệ.");

        var wallet = await GetOrCreateWalletAsync(userId);
        if (wallet == null)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Chỉ khán giả mới có ví để giao dịch.");

        var points = amountVnd / _pointsPerVnd;
        var updated = await _walletRepo.AddBalanceAsync(userId, points);
        if (!updated)
            return ServiceResult<object>.Fail(StatusCodes.Status500InternalServerError, "Không thể cập nhật số dư.");

        wallet = await _walletRepo.GetByUserIdAsync(userId);
        return ServiceResult<object>.Ok(new { balance = wallet!.Balance, addedVnd = amountVnd, addedPoints = points, pointsPerVnd = _pointsPerVnd });
    }

    /// <summary>Cộng điểm trực tiếp (thắng cược, hoàn tiền, trao thưởng)</summary>
    public async Task<ServiceResult<object>> AddPointsAsync(Guid userId, decimal points, string reference)
    {
        if (points <= 0)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số điểm không hợp lệ.");

        var wallet = await GetOrCreateWalletAsync(userId);
        if (wallet == null)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Chỉ Khán giả hoặc Chủ ngựa mới có ví để giao dịch.");

        var updated = await _walletRepo.AddBalanceAsync(userId, points);
        if (!updated)
            return ServiceResult<object>.Fail(StatusCodes.Status500InternalServerError, "Không thể cập nhật số dư.");

        wallet = await _walletRepo.GetByUserIdAsync(userId);
        return ServiceResult<object>.Ok(new { balance = wallet!.Balance, addedPoints = points });
    }

    /// <summary>Trừ điểm (đặt cược)</summary>
    public async Task<ServiceResult<object>> DeductFundsAsync(Guid userId, decimal amount, string reference)
    {
        if (amount <= 0)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số điểm không hợp lệ.");

        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        if (wallet == null)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Chỉ khán giả mới có ví để giao dịch.");

        var updated = await _walletRepo.DeductBalanceAsync(userId, amount);
        if (!updated)
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số dư không đủ.");

        wallet = await _walletRepo.GetByUserIdAsync(userId);
        return ServiceResult<object>.Ok(new { balance = wallet!.Balance, deducted = amount });
    }

    /// <summary>Đổi điểm → VNĐ khi rút tiền</summary>
    public async Task<ServiceResult<decimal>> ConvertPointsToVndAsync(Guid userId, decimal points)
    {
        if (points <= 0)
            return ServiceResult<decimal>.Fail(StatusCodes.Status400BadRequest, "Số điểm không hợp lệ.");

        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        if (wallet == null)
            return ServiceResult<decimal>.Fail(StatusCodes.Status400BadRequest, "Không tìm thấy ví.");

        if (wallet.Balance < points)
            return ServiceResult<decimal>.Fail(StatusCodes.Status400BadRequest, "Số dư không đủ.");

        var vnd = points * _pointsPerVnd;
        return ServiceResult<decimal>.Ok(vnd);
    }

    /// <summary>
    /// Lấy tỷ lệ quy đổi số điểm trên 1 VNĐ từ cấu hình hệ thống.
    /// </summary>
    /// <returns>Tỷ lệ quy đổi điểm trên 1 VNĐ.</returns>
    public decimal GetPointsPerVnd() => _pointsPerVnd;

    /// <summary>
    /// Lấy ví hiện tại của người dùng hoặc tự động tạo mới ví nếu là tài khoản Khán giả hoặc Chủ ngựa.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng.</param>
    /// <returns>Đối tượng ví giao dịch của người dùng.</returns>
    private async Task<Wallet?> GetOrCreateWalletAsync(Guid userId)
    {
        var wallet = await _walletRepo.GetByUserIdAsync(userId);
        if (wallet != null) return wallet;

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || user.Role is not (UserRole.Spectator or UserRole.HorseOwner))
            return null;

        wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Balance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _walletRepo.AddAsync(wallet);
        await _unitOfWork.SaveChangesAsync();
        return wallet;
    }
}
