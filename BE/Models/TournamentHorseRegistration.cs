using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

/// <summary>
/// Đăng ký ngựa tham gia một giải đấu (cấp giải, không phải cấp cuộc đua).
/// Admin duyệt → ngựa được phép tham gia các cuộc đua trong giải.
/// </summary>
[Table("TournamentHorseRegistrations")]
public class TournamentHorseRegistration
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid TournamentId { get; set; }

    public Tournament? Tournament { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    public Horse? Horse { get; set; }

    [Required]
    public Guid OwnerId { get; set; }

    public Owner? Owner { get; set; }

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ApprovedAt { get; set; }
}
