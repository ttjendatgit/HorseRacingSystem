using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý tài khoản ngân hàng thụ hưởng và xử lý yêu cầu rút tiền về ngân hàng.
/// </summary>
public interface IWithdrawalService
{
    /// <summary>Thêm hoặc cập nhật tài khoản ngân hàng nhận tiền của người dùng.</summary>
    Task<ServiceResult<object>> SaveBankAccountAsync(Guid userId, BankAccountRequest request);

    /// <summary>Lấy danh sách các tài khoản ngân hàng thụ hưởng của người dùng.</summary>
    Task<ServiceResult<object>> GetBankAccountsAsync(Guid userId);

    /// <summary>Tạo yêu cầu rút tiền từ số dư ví về tài khoản ngân hàng cá nhân.</summary>
    Task<ServiceResult<object>> CreateWithdrawalAsync(Guid userId, WithdrawalRequestDto request);

    /// <summary>Lấy lịch sử tất cả các đơn yêu cầu rút tiền của người dùng.</summary>
    Task<ServiceResult<object>> GetHistoryAsync(Guid userId);

    /// <summary>Lấy danh sách các đơn yêu cầu rút tiền đang chờ Admin phê duyệt.</summary>
    Task<ServiceResult<object>> GetPendingAsync();

    /// <summary>Lấy toàn bộ danh sách đơn rút tiền toàn hệ thống dành cho Admin.</summary>
    Task<ServiceResult<object>> GetAllAsync();

    /// <summary>Admin xử lý duyệt (Approve) hoặc từ chối (Reject) lệnh rút tiền của người dùng.</summary>
    Task<ServiceResult<object>> ProcessWithdrawalAsync(Guid adminId, AdminProcessWithdrawalRequest request);
}
