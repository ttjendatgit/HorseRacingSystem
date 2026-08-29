using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HorseRacing.Services;

public class TransactionService : ITransactionService
{
    private readonly ITransactionRepository _transactionRepo;
    private readonly IUserRepository _userRepo;
    private readonly IWalletService _walletService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    private readonly ILogger<TransactionService> _logger;

    public TransactionService(
        ITransactionRepository transactionRepo,
        IUserRepository userRepo,
        IWalletService walletService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        IConfiguration config,
        ILogger<TransactionService> logger)
    {
        _transactionRepo = transactionRepo;
        _userRepo = userRepo;
        _walletService = walletService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Khởi tạo một giao dịch nạp tiền đang chờ (pending) qua SePay với mã nội dung chuyển khoản ngẫu nhiên.
    /// </summary>
    /// <param name="userId">Mã định danh người dùng nạp tiền.</param>
    /// <param name="amount">Số tiền VNĐ muốn nạp.</param>
    /// <returns>Thông tin giao dịch kèm mã nội dung và tài khoản nhận tiền.</returns>
    public async Task<ServiceResult<object>> CreatePendingAsync(Guid userId, decimal amount)
    {
        if (amount <= 0)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số tiền không hợp lệ.");
        }

        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy người dùng");
        }

        var bankCode = _config["Sepay:BankId"] ?? "MB";
        var accountNumber = _config["Sepay:AccountNumber"] ?? "0787542456";
        var accountHolder = _config["Sepay:AccountHolder"] ?? "NGUYEN XUAN TAN";

        var reference = GenerateReference();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BankCode = bankCode,
            AccountNumber = accountNumber,
            Amount = amount,
            Description = $"Nap tien {reference}",
            Reference = reference,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        await _transactionRepo.AddAsync(transaction);
        await _unitOfWork.SaveChangesAsync();

        return ServiceResult<object>.Ok(new
        {
            transactionId = transaction.Id,
            reference,
            description = transaction.Description,
            amount,
            bankCode,
            accountNumber,
            accountHolder
        });
    }

    /// <summary>
    /// Xử lý Webhook biến động số dư tự động từ cổng thanh toán SePay để cộng điểm ví cho khán giả.
    /// </summary>
    /// <param name="request">Dữ liệu Webhook nhận từ SePay.</param>
    /// <returns>Kết quả xử lý giao dịch nạp tiền.</returns>
    public async Task<ServiceResult<object>> HandleWebhookAsync(SepayWebhookRequest request)
    {
        // ── Validate amount ──
        var amount = request.TransferAmount;
        if (amount == null || amount <= 0)
        {
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Số tiền không hợp lệ");
        }

        // ── Only process incoming transfers ──
        if (!string.Equals(request.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<object>.Ok(new { message = "Ignored — not an incoming transfer." });
        }

        // ── Idempotency: check if this Sepay transaction was already processed ──
        if (request.Id != null && await _transactionRepo.ExistsBySepayIdAsync(request.Id.Value))
        {
            _logger.LogInformation("Sepay webhook {SepayId} already processed — skipping", request.Id);
            return ServiceResult<object>.Ok(new { message = "Đã được xử lý" });
        }

        // ── Extract our reference from transfer content ──
        var content = (request.Content ?? "").Trim();
        var pendingRefs = await _transactionRepo.GetPendingReferencesAsync();
        var reference = FindReference(content, pendingRefs);

        if (reference == null)
        {
            _logger.LogWarning("No reference found in transfer content: {Content}", content);
            return ServiceResult<object>.Ok(new { message = "Không tìm thấy mã tham chiếu" });
        }

        // ── Get the pending transaction to know userId + expected amount ──
        var tx = await _transactionRepo.GetByReferenceAsync(reference);
        if (tx == null || tx.UserId == Guid.Empty)
        {
            return ServiceResult<object>.Ok(new { message = "Không tìm thấy giao dịch (không có người dùng)" });
        }

        // ── Validate transfer amount BEFORE completing the transaction ──
        var transferAmount = request.TransferAmount ?? 0;
        if (transferAmount < tx.Amount)
        {
            _logger.LogWarning("Transfer amount {TransferAmount} is less than pending amount {PendingAmount} for ref {Ref}",
                transferAmount, tx.Amount, reference);
            return ServiceResult<object>.Fail(StatusCodes.Status400BadRequest,
                $"Số tiền chuyển ({transferAmount:N0}đ) ít hơn số tiền yêu cầu ({tx.Amount:N0}đ). Vui lòng chuyển đủ số tiền.");
        }

        // ── Atomic complete: only ONE request can succeed (fixes race condition) ──
        var sepayId = request.Id ?? 0;
        var completed = await _transactionRepo.TryCompleteByRefAsync(reference, sepayId);

        if (!completed)
        {
            _logger.LogWarning("Reference {Ref} not found or already completed", reference);
            return ServiceResult<object>.Ok(new { message = "Giao dịch đã hoàn thành hoặc không tìm thấy" });
        }

        // ── Add funds to wallet (atomic, no race condition) ──
        try
        {
            await _walletService.AddFundsAsync(tx.UserId, tx.Amount, $"deposit_{reference}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add funds for reference {Ref}, user {UserId}", reference, tx.UserId);
            return ServiceResult<object>.Fail(StatusCodes.Status500InternalServerError, "Thêm tiền thất bại - liên hệ hỗ trợ");
        }

        _logger.LogInformation("Deposit completed: ref={Ref}, userId={UserId}, amount={Amount}", reference, tx.UserId, tx.Amount);

        // Notify user of successful deposit
        try
        {
            await _notificationService.CreateNotificationAsync(new Dtos.CreateNotificationDto
            {
                UserId = tx.UserId,
                Title = "Nạp tiền thành công",
                Message = $"Bạn vừa nạp thành công {tx.Amount:N0} VNĐ vào ví. Số điểm đã được cập nhật.",
                Type = NotificationType.InApp,
                Category = NotificationCategory.DepositSuccess
            });
        }
        catch { /* non-critical: notification failure doesn't affect deposit */ }

        return ServiceResult<object>.Ok(new { message = "Giao dịch hoàn thành", userId = tx.UserId });
    }

    public async Task<ServiceResult<object>> CheckTransactionAsync(Guid userId, Guid transactionId)
    {
        var transaction = await _transactionRepo.GetByIdAsync(transactionId);
        if (transaction == null || transaction.UserId != userId)
        {
                        return ServiceResult<object>.Fail(StatusCodes.Status404NotFound, "Không tìm thấy giao dịch");
        }

        if (transaction.Status == "completed")
        {
            return ServiceResult<object>.Ok(new { completed = true, amount = transaction.Amount });
        }
        return ServiceResult<object>.Ok(new { completed = false });
    }

    public async Task<ServiceResult<object>> GetHistoryAsync(Guid userId)
    {
        var transactions = await _transactionRepo.GetHistoryByUserAsync(userId);
        var items = transactions.Select(t => new DepositHistoryItem
        {
            Id = t.Id,
            Amount = t.Amount,
            Status = t.Status,
            Reference = t.Reference ?? string.Empty,
            Description = t.Description ?? string.Empty,
            // DB lưu UTC trong cột timestamp; Npgsql đọc ra với Kind=Unspecified
            // nên phải gán lại Kind=Utc để JSON serialize kèm "Z", trình duyệt hiển thị đúng giờ địa phương.
            CreatedAt = DateTime.SpecifyKind(t.CreatedAt, DateTimeKind.Utc),
            CompletedAt = t.CompletedAt.HasValue ? DateTime.SpecifyKind(t.CompletedAt.Value, DateTimeKind.Utc) : null
        }).ToList();

        return ServiceResult<object>.Ok(items);
    }

    private static string GenerateReference()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        return Convert.ToHexString(bytes); // 8 hex chars
    }

    /// <summary>
    /// Tìm reference trong nội dung chuyển khoản bằng cách so khớp với danh sách pending references.
    /// </summary>
    private static string? FindReference(string content, List<string> pendingRefs)
    {
        if (string.IsNullOrWhiteSpace(content) || pendingRefs.Count == 0) return null;
        var upper = content.ToUpperInvariant();
        foreach (var r in pendingRefs)
        {
            if (upper.Contains(r)) return r;
        }
        return null;
    }
}
