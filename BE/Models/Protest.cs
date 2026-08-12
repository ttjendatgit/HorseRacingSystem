using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("Protests")]
public class Protest
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid FiledByUserId { get; set; }

    public User? FiledByUser { get; set; }

    [Required]
    public Guid AgainstEntryId { get; set; }

    public RaceEntry? AgainstEntry { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Evidence { get; set; }

    public ProtestStatus Status { get; set; } = ProtestStatus.Pending;

    [MaxLength(2000)]
    public string? Ruling { get; set; }

    public Guid? RuledByUserId { get; set; }

    public User? RuledByUser { get; set; }

    [MaxLength(1000)]
    public string? Resolution { get; set; }

    [MaxLength(500)]
    public string? AdminNotes { get; set; }

    public DateTime FiledAt { get; set; } = DateTime.UtcNow;

    public DateTime? RuledAt { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
