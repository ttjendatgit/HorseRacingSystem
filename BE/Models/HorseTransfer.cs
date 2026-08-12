using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("HorseTransfers")]
public class HorseTransfer
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    public Horse? Horse { get; set; }

    [Required]
    public Guid FromOwnerId { get; set; }

    public Owner? FromOwner { get; set; }

    [Required]
    public Guid ToOwnerId { get; set; }

    public Owner? ToOwner { get; set; }

    public TransferType TransferType { get; set; } = TransferType.Sale;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Price { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public TransferStatus Status { get; set; } = TransferStatus.Pending;

    public Guid? ApprovedByUserId { get; set; }

    public User? ApprovedByUser { get; set; }

    [MaxLength(500)]
    public string? AdminNotes { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }
}
