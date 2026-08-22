using System;
using System.Collections.Generic;

namespace HorseRacing.Dtos;

/// <summary>
/// Q1: bounded summary of a "generate next round" run — deliberately not an EF navigation graph,
/// just the identifiers/counts an Admin UI needs to confirm what happened.
/// </summary>
public class GenerateNextRoundResultDto
{
    public int SourceRoundNumber { get; set; }
    public int TargetRoundNumber { get; set; }
    public int GeneratedEntries { get; set; }
    public List<GenerateNextRoundRaceAssignmentDto> Assignments { get; set; } = new();
}

public class GenerateNextRoundRaceAssignmentDto
{
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public List<Guid> HorseIds { get; set; } = new();
}
