using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("JockeyInvitations")]
public class JockeyInvitation
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid HorseId { get; set; }

    public Horse? Horse { get; set; }

    [Required]
    public Guid JockeyId { get; set; }

    public Jockey? Jockey { get; set; }

    public Guid? RaceId { get; set; }

    public Race? Race { get; set; }

    public JockeyInvitationStatus Status { get; set; }

    [MaxLength(500)]
    public string? Message { get; set; }

    [MaxLength(500)]
    public string? ResponseNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RespondedAt { get; set; }
}
