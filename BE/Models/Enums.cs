using System;

namespace HorseRacing.Models;

public enum UserRole
{
    HorseOwner = 1,
    Jockey = 2,
    Spectator = 3,
    Admin = 4,
    Referee = 5
}

public enum RaceStatus
{
    Scheduled = 1,
    InProgress = 2,
    Finished = 3,
    Cancelled = 4,
    AwaitingResult = 5,
    ResultPendingApproval = 6,
    RegistrationOpen = 7,
    RegistrationClosed = 8,
    ResultApproved = 9
}

public enum TournamentStatus
{
    Draft = 0,
    Published = 1,
    Ongoing = 2,
    Finished = 3,
    Cancelled = 4
}

public enum RegistrationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum JockeyInvitationStatus
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Withdrawn = 4
}

public enum PredictionStatus
{
    Pending = 1,
    Won = 2,
    Lost = 3
}

public enum HealthCheckStatus
{
    Passed = 1,
    Failed = 2,
    RequiresRecheck = 3
}

public enum ViolationType
{
    DangerousBehavior = 1,
    FalseStart = 2,
    Interference = 3,
    AnimalWelfare = 4,
    EquipmentViolation = 5,
    Other = 6
}

public enum RefereeAssignmentStatus
{
    Assigned = 1,
    Confirmed = 2,
    Completed = 3,
    Cancelled = 4
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum NotificationType
{
    Email = 1,
    SMS = 2,
    PushNotification = 3,
    InApp = 4
}

public enum NotificationCategory
{
    RegistrationApproval = 1,
    HorseApproval = 2,
    JockeyApproval = 3,
    JockeyInvitation = 4,
    RaceAssignment = 5,
    RaceUpdate = 6,
    RaceResult = 7,
    ViolationRecord = 8,
    HealthCheckResult = 9,
    SystemAlert = 10,
    TournamentCreated = 12,
    BetWon = 13,
    DepositSuccess = 14,
    Other = 11
}

public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3,
    Approve = 4,
    Reject = 5,
    Activate = 6,
    Deactivate = 7,
    Assign = 8,
    Login = 9,
    Export = 10,
    Other = 11
}

public enum SurfaceType
{
    Dirt = 1,
    Turf = 2,
    Synthetic = 3,
    Sand = 4
}

public enum ProtestStatus
{
    Pending = 1,
    UnderReview = 2,
    Upheld = 3,
    Rejected = 4,
    Withdrawn = 5
}

public enum BetStatus
{
    Placed = 1,
    Won = 2,
    Lost = 3,
    Refunded = 4
}

public enum TransferStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

public enum TransferType
{
    Sale = 1,
    Gift = 2,
    Lease = 3
}

public enum InjurySeverity
{
    Minor = 1,
    Moderate = 2,
    Severe = 3,
    Critical = 4
}

public enum InjuryStatus
{
    Active = 1,
    Recovering = 2,
    Recovered = 3,
    Chronic = 4,
    CareerEnding = 5
}

public enum ContractStatus
{
    Draft = 1,
    Active = 2,
    Expired = 3,
    Terminated = 4,
    Renewed = 5
}
