using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HorseRacing.Models;

// COMPLAINT-EVIDENCE-V1: attached image/video for a RaceComplaint. Deliberately a separate child
// table (not another RaceComplaint text column) so an unlimited number of files, from either the
// filer or the assigned Referee, can be attached and told apart by UploadedByUserId.
[Table("RaceComplaintEvidence")]
public class RaceComplaintEvidence
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid RaceComplaintId { get; set; }

    public RaceComplaint? RaceComplaint { get; set; }

    [Required]
    public Guid UploadedByUserId { get; set; }

    public User? UploadedByUser { get; set; }

    [Required]
    [MaxLength(1000)]
    public string FileUrl { get; set; } = string.Empty;

    public ComplaintEvidenceMediaType MediaType { get; set; }

    // COMPLAINT-EVIDENCE-V1.1: typed, persisted at upload time from the caller's verified
    // relationship to the complaint — never inferred at read time from UploadedByUser.Role, and
    // the single source of truth for both grouping (FE gallery) and mutation rules (who may
    // delete this row, and until when).
    public EvidenceSource EvidenceSource { get; set; }

    [MaxLength(260)]
    public string? FileName { get; set; }

    // Cloudinary identifiers captured directly from the upload response — required for a
    // reliable remote delete (DeleteResourcesAsync needs both the public_id and the correct
    // resource_type; parsing them back out of FileUrl would be fragile and was avoided).
    [MaxLength(500)]
    public string? PublicId { get; set; }

    public long? FileSizeBytes { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
