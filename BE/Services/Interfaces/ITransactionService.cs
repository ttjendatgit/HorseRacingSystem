using System;
using System.Threading.Tasks;
using HorseRacing.Dtos;

namespace HorseRacing.Services.Interfaces;

/// <summary>
/// Giao diện dịch vụ quản lý các giao dịch nạp tiền qua cổng thanh toán QR SePay và Webhook tự động.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Tạo yêu cầu nạp tiền ở trạng thái Đang chờ (Pending) kèm mã chuyển khoản reference duy nhất.
    /// </summary>
    Task<ServiceResult<object>> CreatePendingAsync(Guid userId, decimal amount);

    /// <summary>
    /// Xử lý dữ liệu chuyển khoản tự động gửi về từ Webhook SePay và cộng tiền ví cho người dùng.
    /// </summary>
    Task<ServiceResult<object>> HandleWebhookAsync(SepayWebhookRequest request);
<<<<<<< HEAD

    /// <summary>
    /// Kiểm tra trạng thái xử lý của một đơn nạp tiền cụ thể theo ID.
    /// </summary>
    Task<ServiceResult<object>> CheckTransactionAsync(Guid userId, Guid transactionId);

    /// <summary>
    /// Lấy lịch sử biến động tất cả các đơn nạp tiền của người dùng đang đăng nhập.
    /// </summary>
=======
    Task<ServiceResult<object>> CheckTransactionAsync(Guid userId, Guid transactionId);
>>>>>>> origin/huyhoang
    Task<ServiceResult<object>> GetHistoryAsync(Guid userId);
}
