using System.Reflection;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Services;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public class RaceComplaintEvidenceTests
{
    private static IFormFile MakeFile(string fileName, string contentType, int sizeBytes = 128)
    {
        var stream = new MemoryStream(new byte[sizeBytes]);
        return new FormFile(stream, 0, stream.Length, "file", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private static async Task<List<RaceEntry>> EntriesAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId) =>
        await f.EntryRepo.GetByRaceAsync(raceId);

    private static async Task<RefereeAssignment> ConfirmedAssignmentAsync(RaceLifecycleTests.LifecycleFixture f, Guid raceId) =>
        await f.Db.RefereeAssignments.FirstAsync(a => a.RaceId == raceId && a.Status == RefereeAssignmentStatus.Confirmed);

    private static async Task<RaceComplaint> AddComplaintAsync(
        RaceLifecycleTests.LifecycleFixture f,
        Guid raceId,
        Guid filedByUserId,
        RaceComplaintStatus status,
        Guid? assignedRefereeAssignmentId = null)
    {
        var complaint = new RaceComplaint
        {
            Id = Guid.NewGuid(),
            RaceId = raceId,
            FiledByUserId = filedByUserId,
            Type = RaceComplaintType.ResultJudging,
            Reason = $"seeded {status}",
            Status = status,
            AssignedRefereeAssignmentId = assignedRefereeAssignmentId,
            CreatedAt = DateTime.UtcNow,
        };
        f.Db.RaceComplaints.Add(complaint);
        await f.Db.SaveChangesAsync();
        return complaint;
    }

    private static async Task ProgressToFinishedWithProvisionalResultAsync(
        RaceLifecycleTests.LifecycleFixture f,
        RaceLifecycleTests.SeededRace race)
    {
        await f.RaceManagement.OpenRegistrationAsync(race.Id);
        await f.RaceManagement.CloseRegistrationAsync(race.Id);
        await f.RaceManagement.StartRaceAsync(race.Id);
        await f.RaceManagement.EndRaceAsync(race.Id);
        await f.LiveResult.UpdateRaceResultAsync(
            race.Id,
            RaceLifecycleTests.WinnerLoserRanking(race.WinnerHorseId, race.LoserHorseId));
        await f.AddMandatoryReportAsync(race.Id);
    }

    [Fact]
    public async Task FilerCanUploadEvidenceWhileComplaintActive()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal("Image", result.Result.Data!.MediaType);
        Assert.Equal(filerUserId, result.Result.Data.UploadedByUserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Result.Data.FileUrl));
    }

    [Fact]
    public async Task VideoContentTypeIsClassifiedAsVideo()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("clip.mp4", "video/mp4"), filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal("Video", result.Result.Data!.MediaType);
    }

    [Fact]
    public async Task UnrelatedUserCannotUploadEvidence()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);
        var unrelatedUserId = Guid.NewGuid();

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), unrelatedUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task FilerCannotUploadAfterComplaintTerminal()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Rejected);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task AssignedRefereeCanUploadDuringAwaitingResponse()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("explain.png", "image/png"), f.RefereeUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal(f.RefereeUserId, result.Result.Data!.UploadedByUserId);
    }

    // COMPLAINT-EVIDENCE-V1.1: narrowed from "AwaitingRefereeResponse OR UnderReview" — once the
    // referee has submitted their response, admin review must operate on a stable evidence set.
    [Fact]
    public async Task AssignedRefereeCannotUploadDuringUnderReview()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.UnderReview, assignment.Id);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("explain.png", "image/png"), f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UnassignedRefereeCannotUploadEvenIfComplaintIsActive()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        // Pending: no referee routed yet at all.
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task EmptyFileIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("empty.jpg", "image/jpeg", 0), filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task UploadedEvidenceAppearsInTheComplaintsReturnedToAdmin()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);
        await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        var all = await f.RaceComplaintSvc.GetAllAsync();
        var mapped = all.Result.Data!.Single(c => c.Id == complaint.Id);

        Assert.Single(mapped.Evidence);
        Assert.Equal("photo.jpg", mapped.Evidence[0].FileName);
        Assert.Equal("Image", mapped.Evidence[0].MediaType);
    }

    [Fact]
    public async Task BothFilerAndRefereeEvidenceAppearTogetherForAdminToReviewBothSides()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);

        await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("filer.jpg", "image/jpeg"), filerUserId);
        await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("referee.jpg", "image/jpeg"), f.RefereeUserId);

        var all = await f.RaceComplaintSvc.GetAllAsync();
        var mapped = all.Result.Data!.Single(c => c.Id == complaint.Id);

        Assert.Equal(2, mapped.Evidence.Count);
        Assert.Contains(mapped.Evidence, e => e.UploadedByUserId == filerUserId);
        Assert.Contains(mapped.Evidence, e => e.UploadedByUserId == f.RefereeUserId);
    }

    // ── COMPLAINT-EVIDENCE-V1.1 ──

    [Fact]
    public async Task FilerUploadStoresEvidenceSourceFiler()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal("Filer", result.Result.Data!.EvidenceSource);
    }

    [Fact]
    public async Task RefereeUploadStoresEvidenceSourceReferee()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);

        var result = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("explain.png", "image/png"), f.RefereeUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        Assert.Equal("Referee", result.Result.Data!.EvidenceSource);
    }

    // Source is derived server-side purely from the caller's verified relationship to the complaint
    // (complaint.FiledByUserId / AssignedRefereeAssignment.Referee.UserId) — UploadEvidenceAsync's
    // signature has no source-like input at all, so there is no field a client could set to spoof it.
    [Fact]
    public void ClientCannotSpoofEvidenceSourceViaUploadSignature()
    {
        var method = typeof(IRaceComplaintService).GetMethod(nameof(IRaceComplaintService.UploadEvidenceAsync))!;
        var parameters = method.GetParameters();

        Assert.Equal(3, parameters.Length);
        Assert.DoesNotContain(parameters, p =>
            p.ParameterType == typeof(EvidenceSource) ||
            p.Name!.Contains("Source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RefereeDeleteAfterResponseIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);
        var upload = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("explain.png", "image/png"), f.RefereeUserId);

        await f.RaceComplaintSvc.RespondAsync(complaint.Id, new RespondRaceComplaintRequest { Response = "My explanation." }, f.RefereeUserId);

        var result = await f.RaceComplaintSvc.DeleteEvidenceAsync(complaint.Id, upload.Result.Data!.Id, f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task FilerActiveStateDeleteOwnEvidenceSucceeds()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);
        var upload = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        var result = await f.RaceComplaintSvc.DeleteEvidenceAsync(complaint.Id, upload.Result.Data!.Id, filerUserId);

        Assert.True(result.Result.Success, result.Result.Message);
        var stillThere = await f.RaceComplaintEvidenceRepo.GetByIdAsync(upload.Result.Data.Id);
        Assert.Null(stillThere);
    }

    [Fact]
    public async Task FilerTerminalDeleteIsRejected()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);
        var upload = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        await f.RaceComplaintSvc.RuleAsync(complaint.Id, new RuleRaceComplaintRequest { Outcome = RaceComplaintStatus.Rejected, Ruling = "No merit." }, Guid.NewGuid());

        var result = await f.RaceComplaintSvc.DeleteEvidenceAsync(complaint.Id, upload.Result.Data!.Id, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task FilerCannotDeleteRefereeEvidence()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);
        var upload = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("explain.png", "image/png"), f.RefereeUserId);

        var result = await f.RaceComplaintSvc.DeleteEvidenceAsync(complaint.Id, upload.Result.Data!.Id, filerUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task RefereeCannotDeleteFilerEvidence()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);
        var upload = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        var result = await f.RaceComplaintSvc.DeleteEvidenceAsync(complaint.Id, upload.Result.Data!.Id, f.RefereeUserId);

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task UnrelatedUserCannotDeleteEvidence()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);
        var upload = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo.jpg", "image/jpeg"), filerUserId);

        var result = await f.RaceComplaintSvc.DeleteEvidenceAsync(complaint.Id, upload.Result.Data!.Id, Guid.NewGuid());

        Assert.False(result.Result.Success);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public async Task FilerMaxFiveEvidenceEnforcedServerSide()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.Pending);

        for (var i = 0; i < 5; i++)
        {
            var ok = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile($"photo{i}.jpg", "image/jpeg"), filerUserId);
            Assert.True(ok.Result.Success, ok.Result.Message);
        }

        var sixth = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("photo6.jpg", "image/jpeg"), filerUserId);

        Assert.False(sixth.Result.Success);
        Assert.Equal(400, sixth.StatusCode);
    }

    [Fact]
    public async Task RefereeMaxFiveEvidenceEnforcedServerSide()
    {
        await using var f = await RaceLifecycleTests.LifecycleFixture.CreateAsync();
        var race = await f.CreateReadyToStartRaceAsync();
        await ProgressToFinishedWithProvisionalResultAsync(f, race);
        var entries = await EntriesAsync(f, race.Id);
        var filerUserId = entries[0].Horse!.Owner!.UserId;
        var assignment = await ConfirmedAssignmentAsync(f, race.Id);
        var complaint = await AddComplaintAsync(f, race.Id, filerUserId, RaceComplaintStatus.AwaitingRefereeResponse, assignment.Id);

        for (var i = 0; i < 5; i++)
        {
            var ok = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile($"explain{i}.png", "image/png"), f.RefereeUserId);
            Assert.True(ok.Result.Success, ok.Result.Message);
        }

        var sixth = await f.RaceComplaintSvc.UploadEvidenceAsync(complaint.Id, MakeFile("explain6.png", "image/png"), f.RefereeUserId);

        Assert.False(sixth.Result.Success);
        Assert.Equal(400, sixth.StatusCode);
    }

    [Fact]
    public void SupportedImageMimeIsAccepted()
    {
        var mediaType = ComplaintEvidenceValidator.Validate("image/jpeg", 1024);
        Assert.Equal(ComplaintEvidenceMediaType.Image, mediaType);
    }

    [Fact]
    public void SupportedMp4MimeIsAccepted()
    {
        var mediaType = ComplaintEvidenceValidator.Validate("video/mp4", 1024);
        Assert.Equal(ComplaintEvidenceMediaType.Video, mediaType);
    }

    [Fact]
    public void UnsupportedMimeIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ComplaintEvidenceValidator.Validate("application/pdf", 1024));
    }

    [Fact]
    public void OversizedImageIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ComplaintEvidenceValidator.Validate("image/jpeg", ComplaintEvidenceValidator.MaxImageBytes + 1));
    }

    [Fact]
    public void OversizedVideoIsRejected()
    {
        Assert.Throws<ArgumentException>(() => ComplaintEvidenceValidator.Validate("video/mp4", ComplaintEvidenceValidator.MaxVideoBytes + 1));
    }
}
