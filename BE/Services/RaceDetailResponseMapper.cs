using System.Linq;
using HorseRacing.Dtos;
using HorseRacing.Models;

namespace HorseRacing.Services;

/// <summary>
/// Shared pure mapper so both RaceManagementService and RaceService can build a
/// RaceDetailResponse without one service depending on the other's internals.
/// Requires the source Race to have Round/Track/Result/Entries/RefereeAssignments
/// loaded where those fields are needed — callers are responsible for the Includes.
/// </summary>
public static class RaceDetailResponseMapper
{
    public static RaceDetailResponse ToDetailResponse(Race race)
    {
        return new RaceDetailResponse
        {
            Id = race.Id,
            Name = race.Name,
            TournamentId = race.TournamentId,
            RoundId = race.RoundId,
            RoundNumber = race.Round?.RoundNumber,
            RoundName = race.Round?.Name,
            ScheduledAt = race.ScheduledAt,
            ScheduledEndAt = race.ScheduledEndAt,
            TrackId = race.TrackId,
            TrackName = race.Track?.Name,
            ActualStartTime = race.ActualStartTime,
            ActualEndTime = race.ActualEndTime,
            Status = race.Status.ToString(),
            ResultStatus = race.Result?.Status.ToString(),
            RejectedReason = race.Result?.RejectedReason,
            Location = race.Location,
            Description = race.Description,
            MaxParticipants = race.MaxParticipants,
            QualificationSlots = race.QualificationSlots,
            Distance = race.Distance,
            EntriesCount = race.Entries?.Count ?? 0,
            ActiveRefereesCount = race.RefereeAssignments?.Count(a => a.Status != RefereeAssignmentStatus.Cancelled) ?? 0,
            RoundNames = race.RoundNames
        };
    }
}
