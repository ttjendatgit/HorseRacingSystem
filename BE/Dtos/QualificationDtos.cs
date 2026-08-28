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

    // Chỉ có giá trị khi phản hồi là lỗi (409) mô tả tình trạng thiếu hụt; khi tạo vòng thành công
    // (200) các field này giữ giá trị mặc định (false/null).
    public bool RequiresShortfallConfirmation { get; set; } = false;
    public bool RequiresTournamentLevelAction { get; set; } = false;
    public int? EligibleCount { get; set; }
    public int? RequiredAdvanceCount { get; set; }

    // Walkover (eligible == 1): phản hồi thành công (200), không phải lỗi — Tournament đã tự chuyển
    // sang Finished vì chỉ còn đúng 1 ngựa đủ điều kiện, không cần tổ chức vòng tiếp theo nữa.
    public bool IsWalkover { get; set; } = false;
    public Guid? WalkoverWinnerHorseId { get; set; }
}

public class GenerateNextRoundRaceAssignmentDto
{
    public Guid RaceId { get; set; }
    public string RaceName { get; set; } = string.Empty;
    public List<Guid> HorseIds { get; set; } = new();
}
