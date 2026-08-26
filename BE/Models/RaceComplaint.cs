using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

[Table("RaceComplaints")]
public class RaceComplaint
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceId { get; set; }

    public Race? Race { get; set; }

    [Required]
    public Guid FiledByUserId { get; set; }

    public User? FiledByUser { get; set; }

    public RaceComplaintType Type { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? EvidenceDescription { get; set; }

    public RaceComplaintStatus Status { get; set; } = RaceComplaintStatus.Pending;

    public Guid? AssignedRefereeAssignmentId { get; set; }

    public RefereeAssignment? AssignedRefereeAssignment { get; set; }

    public DateTime? ResponseRequestedAt { get; set; }

    [MaxLength(2000)]
    public string? RefereeResponse { get; set; }

    public DateTime? RefereeRespondedAt { get; set; }

    public Guid? RuledByUserId { get; set; }

    public User? RuledByUser { get; set; }

    [MaxLength(2000)]
    public string? Ruling { get; set; }

    public bool? AffectsResult { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<RaceComplaintEvidence> Evidence { get; set; } = new List<RaceComplaintEvidence>();
}
