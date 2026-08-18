using System;

namespace HorseRacing.Dtos;

public class TrackResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Length { get; set; }
    public int? Capacity { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTrackRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? Length { get; set; }
    public int? Capacity { get; set; }
}
