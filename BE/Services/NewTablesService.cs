using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using HorseRacing.Dtos;
using HorseRacing.Models;
using HorseRacing.Repositories.Interfaces;
using HorseRacing.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace HorseRacing.Services;

public class PrizeService : IPrizeService
{
    private readonly IPrizeRepository _repo;
    private readonly IUnitOfWork _uow;
    public PrizeService(IPrizeRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<PrizeResponse>> CreateAsync(CreatePrizeRequest r)
    {
        var prize = new Prize
        {
            Id = Guid.NewGuid(), TournamentId = r.TournamentId, RaceId = r.RaceId,
            Name = r.Name, Amount = r.Amount, Currency = r.Currency, Position = r.Position,
            PercentageOfPool = r.PercentageOfPool, SponsorName = r.SponsorName, Description = r.Description,
            CreatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(prize);
        await _uow.SaveChangesAsync();
        return ServiceResult<PrizeResponse>.Success(Map(prize), 201);
    }

    public async Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByTournamentAsync(Guid tid) =>
        ServiceResult<IEnumerable<PrizeResponse>>.Ok((await _repo.GetByTournamentAsync(tid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<PrizeResponse>>> GetByRaceAsync(Guid rid) =>
        ServiceResult<IEnumerable<PrizeResponse>>.Ok((await _repo.GetByRaceAsync(rid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<PrizeResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<PrizeResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id) { await _repo.DeleteAsync(id); await _uow.SaveChangesAsync(); return ServiceResult<bool>.Ok(true); }

    private static PrizeResponse Map(Prize p) => new()
    {
        Id = p.Id, TournamentId = p.TournamentId, RaceId = p.RaceId, Name = p.Name,
        Amount = p.Amount, Currency = p.Currency, Position = p.Position,
        PercentageOfPool = p.PercentageOfPool, SponsorName = p.SponsorName, Description = p.Description,
        IsDistributed = p.IsDistributed, CreatedAt = p.CreatedAt
    };
}

public class ProtestService : IProtestService
{
    private readonly IProtestRepository _repo;
    private readonly IUnitOfWork _uow;
    public ProtestService(IProtestRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<ProtestResponse>> FileAsync(CreateProtestRequest r, Guid userId)
    {
        var protest = new Protest
        {
            Id = Guid.NewGuid(), RaceId = r.RaceId, FiledByUserId = userId,
            AgainstEntryId = r.AgainstEntryId, Reason = r.Reason, Evidence = r.Evidence,
            Status = ProtestStatus.Pending, FiledAt = DateTime.UtcNow
        };
        await _repo.AddAsync(protest);
        await _uow.SaveChangesAsync();
        return ServiceResult<ProtestResponse>.Success(Map(protest), 201);
    }

    public async Task<ServiceResult<IEnumerable<ProtestResponse>>> GetPendingAsync() =>
        ServiceResult<IEnumerable<ProtestResponse>>.Ok((await _repo.GetPendingAsync()).Select(Map));

    public async Task<ServiceResult<IEnumerable<ProtestResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<ProtestResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<ProtestResponse>> RuleAsync(Guid id, RuleProtestRequest r, Guid ruledByUserId)
    {
        var protest = await _repo.GetByIdAsync(id);
        if (protest == null) return ServiceResult<ProtestResponse>.Fail(404, "Không tìm thấy khiếu nại");

        protest.Status = r.Ruling.Contains("Upheld", StringComparison.OrdinalIgnoreCase) ? ProtestStatus.Upheld : ProtestStatus.Rejected;
        protest.Ruling = r.Ruling;
        protest.Resolution = r.Resolution;
        protest.RuledByUserId = ruledByUserId;
        protest.RuledAt = DateTime.UtcNow;
        await _repo.UpdateAsync(protest);
        await _uow.SaveChangesAsync();
        return ServiceResult<ProtestResponse>.Ok(Map(protest));
    }

    private static ProtestResponse Map(Protest p) => new()
    {
        Id = p.Id, RaceId = p.RaceId, RaceName = p.Race?.Name, FiledByUserId = p.FiledByUserId,
        FiledByName = p.FiledByUser?.FullName, AgainstEntryId = p.AgainstEntryId,
        AgainstHorseName = p.AgainstEntry?.Horse?.Name, Reason = p.Reason, Evidence = p.Evidence,
        Status = p.Status.ToString(), Ruling = p.Ruling, Resolution = p.Resolution,
        RuledByUserId = p.RuledByUserId, FiledAt = p.FiledAt, RuledAt = p.RuledAt
    };
}

public class HorseTransferService : IHorseTransferService
{
    private readonly IHorseTransferRepository _repo;
    private readonly IUnitOfWork _uow;
    public HorseTransferService(IHorseTransferRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<HorseTransferResponse>> CreateAsync(CreateHorseTransferRequest r, Guid fromOwnerId)
    {
        var transfer = new HorseTransfer
        {
            Id = Guid.NewGuid(), HorseId = r.HorseId, FromOwnerId = fromOwnerId, ToOwnerId = r.ToOwnerId,
            TransferType = Enum.Parse<TransferType>(r.TransferType), Price = r.Price, Reason = r.Reason,
            Status = TransferStatus.Pending, RequestedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(transfer);
        await _uow.SaveChangesAsync();
        return ServiceResult<HorseTransferResponse>.Success(Map(transfer), 201);
    }

    public async Task<ServiceResult<IEnumerable<HorseTransferResponse>>> GetPendingAsync() =>
        ServiceResult<IEnumerable<HorseTransferResponse>>.Ok((await _repo.GetPendingAsync()).Select(Map));

    public async Task<ServiceResult<IEnumerable<HorseTransferResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<HorseTransferResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<HorseTransferResponse>> ApproveAsync(Guid id, ApproveHorseTransferRequest r, Guid approvedByUserId)
    {
        var t = await _repo.GetByIdAsync(id);
        if (t == null) return ServiceResult<HorseTransferResponse>.Fail(404, "Không tìm thấy chuyển nhượng");
        t.Status = TransferStatus.Approved;
        t.ApprovedByUserId = approvedByUserId;
        t.ApprovedAt = DateTime.UtcNow;
        t.CompletedAt = DateTime.UtcNow;
        t.AdminNotes = r.AdminNotes;
        await _repo.UpdateAsync(t);
        await _uow.SaveChangesAsync();
        return ServiceResult<HorseTransferResponse>.Ok(Map(t));
    }

    public async Task<ServiceResult<HorseTransferResponse>> RejectAsync(Guid id, string reason, Guid approvedByUserId)
    {
        var t = await _repo.GetByIdAsync(id);
        if (t == null) return ServiceResult<HorseTransferResponse>.Fail(404, "Không tìm thấy chuyển nhượng");
        t.Status = TransferStatus.Rejected;
        t.ApprovedByUserId = approvedByUserId;
        t.ApprovedAt = DateTime.UtcNow;
        t.AdminNotes = reason;
        await _repo.UpdateAsync(t);
        await _uow.SaveChangesAsync();
        return ServiceResult<HorseTransferResponse>.Ok(Map(t));
    }

    private static HorseTransferResponse Map(HorseTransfer t) => new()
    {
        Id = t.Id, HorseId = t.HorseId, HorseName = t.Horse?.Name, FromOwnerId = t.FromOwnerId,
        FromOwnerName = t.FromOwner?.User?.FullName, ToOwnerId = t.ToOwnerId,
        ToOwnerName = t.ToOwner?.User?.FullName, TransferType = t.TransferType.ToString(),
        Price = t.Price, Reason = t.Reason, Status = t.Status.ToString(), AdminNotes = t.AdminNotes,
        RequestedAt = t.RequestedAt, CompletedAt = t.CompletedAt
    };
}

public class ContractService : IContractService
{
    private readonly IContractRepository _repo;
    private readonly IUnitOfWork _uow;
    public ContractService(IContractRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<ContractResponse>> CreateAsync(CreateContractRequest r)
    {
        var c = new Contract
        {
            Id = Guid.NewGuid(), OwnerId = r.OwnerId, JockeyId = r.JockeyId, HorseId = r.HorseId,
            Title = r.Title, Status = ContractStatus.Draft, StartDate = r.StartDate, EndDate = r.EndDate,
            BaseFee = r.BaseFee, WinBonusPercent = r.WinBonusPercent, PerRaceFee = r.PerRaceFee,
            TermsAndConditions = r.TermsAndConditions, CreatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(c);
        await _uow.SaveChangesAsync();
        return ServiceResult<ContractResponse>.Success(Map(c), 201);
    }

    public async Task<ServiceResult<ContractResponse>> SignByOwnerAsync(Guid id, Guid ownerId)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c == null || c.OwnerId != ownerId) return ServiceResult<ContractResponse>.Fail(404, "Không tìm thấy hợp đồng");
        c.SignedByOwnerAt = DateTime.UtcNow;
        if (c.SignedByJockeyAt != null) c.Status = ContractStatus.Active;
        await _repo.UpdateAsync(c);
        await _uow.SaveChangesAsync();
        return ServiceResult<ContractResponse>.Ok(Map(c));
    }

    public async Task<ServiceResult<ContractResponse>> SignByJockeyAsync(Guid id, Guid jockeyId)
    {
        var c = await _repo.GetByIdAsync(id);
        if (c == null || c.JockeyId != jockeyId) return ServiceResult<ContractResponse>.Fail(404, "Không tìm thấy hợp đồng");
        c.SignedByJockeyAt = DateTime.UtcNow;
        if (c.SignedByOwnerAt != null) c.Status = ContractStatus.Active;
        await _repo.UpdateAsync(c);
        await _uow.SaveChangesAsync();
        return ServiceResult<ContractResponse>.Ok(Map(c));
    }

    public async Task<ServiceResult<IEnumerable<ContractResponse>>> GetByOwnerAsync(Guid oid) =>
        ServiceResult<IEnumerable<ContractResponse>>.Ok((await _repo.GetByOwnerAsync(oid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<ContractResponse>>> GetByJockeyAsync(Guid jid) =>
        ServiceResult<IEnumerable<ContractResponse>>.Ok((await _repo.GetByJockeyAsync(jid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<ContractResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<ContractResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    private static ContractResponse Map(Contract c) => new()
    {
        Id = c.Id, OwnerId = c.OwnerId, OwnerName = c.Owner?.User?.FullName, JockeyId = c.JockeyId,
        JockeyName = c.Jockey?.User?.FullName, HorseId = c.HorseId, HorseName = c.Horse?.Name,
        Title = c.Title, Status = c.Status.ToString(), StartDate = c.StartDate, EndDate = c.EndDate,
        BaseFee = c.BaseFee, WinBonusPercent = c.WinBonusPercent, PerRaceFee = c.PerRaceFee,
        TermsAndConditions = c.TermsAndConditions, SignedByOwnerAt = c.SignedByOwnerAt,
        SignedByJockeyAt = c.SignedByJockeyAt, CreatedAt = c.CreatedAt
    };
}

public class InjuryRecordService : IInjuryRecordService
{
    private readonly IInjuryRecordRepository _repo;
    private readonly IUnitOfWork _uow;
    public InjuryRecordService(IInjuryRecordRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<ServiceResult<InjuryRecordResponse>> CreateAsync(CreateInjuryRecordRequest r, Guid reportedByUserId)
    {
        var record = new InjuryRecord
        {
            Id = Guid.NewGuid(), HorseId = r.HorseId, InjuryType = r.InjuryType,
            Description = r.Description, Severity = Enum.Parse<InjurySeverity>(r.Severity),
            BodyPart = r.BodyPart, Treatment = r.Treatment, Medication = r.Medication,
            VeterinarianName = r.VeterinarianName, ExpectedRecoveryDate = r.ExpectedRecoveryDate,
            RequiresSurgery = r.RequiresSurgery, ReportedByUserId = reportedByUserId,
            DiagnosedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        };
        await _repo.AddAsync(record);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjuryRecordResponse>.Success(Map(record), 201);
    }

    public async Task<ServiceResult<IEnumerable<InjuryRecordResponse>>> GetByHorseAsync(Guid hid) =>
        ServiceResult<IEnumerable<InjuryRecordResponse>>.Ok((await _repo.GetByHorseAsync(hid)).Select(Map));

    public async Task<ServiceResult<IEnumerable<InjuryRecordResponse>>> GetAllAsync() =>
        ServiceResult<IEnumerable<InjuryRecordResponse>>.Ok((await _repo.GetAllAsync()).Select(Map));

    public async Task<ServiceResult<InjuryRecordResponse>> MarkRecoveredAsync(Guid id)
    {
        var r = await _repo.GetByIdAsync(id);
        if (r == null) return ServiceResult<InjuryRecordResponse>.Fail(404, "Không tìm thấy bản ghi");
        r.Status = InjuryStatus.Recovered;
        r.RecoveredAt = DateTime.UtcNow;
        await _repo.UpdateAsync(r);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjuryRecordResponse>.Ok(Map(r));
    }

    public async Task<ServiceResult<InjuryRecordResponse>> ClearToRaceAsync(Guid id)
    {
        var r = await _repo.GetByIdAsync(id);
        if (r == null) return ServiceResult<InjuryRecordResponse>.Fail(404, "Không tìm thấy bản ghi");
        r.ClearedToRace = true;
        r.ClearedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(r);
        await _uow.SaveChangesAsync();
        return ServiceResult<InjuryRecordResponse>.Ok(Map(r));
    }

    private static InjuryRecordResponse Map(InjuryRecord r) => new()
    {
        Id = r.Id, HorseId = r.HorseId, HorseName = r.Horse?.Name, Severity = r.Severity.ToString(),
        Status = r.Status.ToString(), InjuryType = r.InjuryType, Description = r.Description,
        BodyPart = r.BodyPart, Treatment = r.Treatment, VeterinarianName = r.VeterinarianName,
        DiagnosedAt = r.DiagnosedAt, ExpectedRecoveryDate = r.ExpectedRecoveryDate,
        RecoveredAt = r.RecoveredAt, ClearedToRace = r.ClearedToRace, ClearedAt = r.ClearedAt
    };
}
