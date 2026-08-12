using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HorseRacing.Models;

[Table("Owners")]
public class Owner
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    public User? User { get; set; }

    [Required]
    [MaxLength(50)]
    public string OwnerCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? OrganizationName { get; set; }

    [MaxLength(100)]
    public string? BusinessLicenseNumber { get; set; }

    [MaxLength(50)]
    public string OwnerType { get; set; } = "Cá nhân";

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public DateTime JoinDate { get; set; } = DateTime.UtcNow;

    [MaxLength(50)]
    public string Status { get; set; } = "Đang hoạt động";

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public ICollection<Horse> Horses { get; set; } = new List<Horse>();
}
