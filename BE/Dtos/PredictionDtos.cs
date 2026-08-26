using System;
using System.ComponentModel.DataAnnotations;

namespace HorseRacing.Dtos;

/// <summary>
/// DTO chứa dữ liệu yêu cầu đặt cược / dự đoán cuộc đua.
/// </summary>
public class PredictionCreateRequest
{
    /// <summary>
    /// Mã cuộc đua khán giả muốn dự đoán.
    /// </summary>
    [Required]
    public Guid RaceId { get; set; }

    /// <summary>
    /// Mã con ngựa khán giả dự đoán thắng cuộc.
    /// </summary>
    [Required]
    public Guid PredictedHorseId { get; set; }

    /// <summary>
    /// Số tiền / điểm muốn đặt cược (phải lớn hơn 0 và nhỏ hơn hoặc bằng số dư ví).
    /// </summary>
    public decimal BetAmount { get; set; } = 0;
}
