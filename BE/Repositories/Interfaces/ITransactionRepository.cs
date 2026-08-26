using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HorseRacing.Models;

namespace HorseRacing.Repositories.Interfaces;

public interface ITransactionRepository
{
    /// <summary>
    /// Thêm mới một giao dịch nạp tiền vào cơ sở dữ liệu.
    /// </summary>
    Task AddAsync(Transaction transaction);

    /// <summary>
    /// Lấy thông tin giao dịch theo mã tham chiếu nội dung chuyển khoản.
    /// </summary>
    Task<Transaction?> GetByReferenceAsync(string reference);

    /// <summary>
    /// Lấy giao dịch mới nhất của người dùng theo mã định danh.
    /// </summary>
    Task<Transaction?> GetLatestByUserAsync(Guid userId);

    /// <summary>
    /// Lấy toàn bộ lịch sử các giao dịch nạp tiền của người dùng.
    /// </summary>
    Task<List<Transaction>> GetHistoryByUserAsync(Guid userId);

    /// <summary>
    /// Lấy thông tin giao dịch theo mã định danh (ID).
    /// </summary>
    Task<Transaction?> GetByIdAsync(Guid id);

    /// <summary>
    /// Lấy giao dịch đang ở trạng thái chờ (Pending) theo mã nội dung chuyển khoản.
    /// </summary>
    Task<Transaction?> GetPendingByRefAsync(string reference);

    /// <summary>
    /// Kiểm tra xem giao dịch SePay này đã từng được xử lý cộng điểm chưa (chống cộng điểm trùng).
    /// </summary>
    Task<bool> ExistsBySepayIdAsync(long sepayTransactionId);

    /// <summary>
    /// Thử hoàn tất nguyên tử giao dịch nạp tiền khi nhận Webhook từ SePay.
    /// </summary>
    Task<bool> TryCompleteByRefAsync(string reference, long sepayTransactionId);

    /// <summary>
    /// Lấy danh sách tất cả các mã tham chiếu giao dịch nạp tiền đang ở trạng thái chờ (Pending).
    /// </summary>
    Task<List<string>> GetPendingReferencesAsync();
}
