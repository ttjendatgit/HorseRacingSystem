using HorseRacing.Models;
using Microsoft.EntityFrameworkCore;

namespace HorseRacing.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Horse> Horses => Set<Horse>();
    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Jockey> Jockeys => Set<Jockey>();
    public DbSet<Tournament> Tournaments => Set<Tournament>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<Race> Races => Set<Race>();
    public DbSet<RaceEntry> RaceEntries => Set<RaceEntry>();
    public DbSet<TournamentHorseRegistration> TournamentHorseRegistrations => Set<TournamentHorseRegistration>();
    public DbSet<JockeyInvitation> JockeyInvitations => Set<JockeyInvitation>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<RaceResult> RaceResults => Set<RaceResult>();
    public DbSet<Referee> Referees => Set<Referee>();
    public DbSet<RefereeAssignment> RefereeAssignments => Set<RefereeAssignment>();
    public DbSet<HorseHealthCheck> HorseHealthChecks => Set<HorseHealthCheck>();
    public DbSet<ViolationRecord> ViolationRecords => Set<ViolationRecord>();
    public DbSet<RaceReport> RaceReports => Set<RaceReport>();
    public DbSet<UserRegistration> UserRegistrations => Set<UserRegistration>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Prize> Prizes => Set<Prize>();
    public DbSet<Protest> Protests => Set<Protest>();
    public DbSet<HorseTransfer> HorseTransfers => Set<HorseTransfer>();
    public DbSet<InjuryRecord> InjuryRecords => Set<InjuryRecord>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<Track> Tracks => Set<Track>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<Race>()
            .Property(r => r.Status)
            .HasConversion<string>();

        modelBuilder.Entity<RaceEntry>()
            .Property(e => e.Status)
            .HasConversion<string>();

        modelBuilder.Entity<JockeyInvitation>()
            .Property(i => i.Status)
            .HasConversion<string>();

        modelBuilder.Entity<JockeyInvitation>()
            .HasIndex(i => new { i.HorseId, i.JockeyId, i.RaceId })
            .IsUnique();

        modelBuilder.Entity<Prediction>()
            .Property(p => p.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Owner>()
            .HasIndex(o => o.OwnerCode)
            .IsUnique();

        modelBuilder.Entity<Owner>()
            .HasIndex(o => o.UserId)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasOne(u => u.OwnerProfile)
            .WithOne(o => o.User)
            .HasForeignKey<Owner>(o => o.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Owner>()
            .HasMany(o => o.Horses)
            .WithOne(h => h.Owner)
            .HasForeignKey(h => h.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasOne(u => u.JockeyProfile)
            .WithOne(j => j.User)
            .HasForeignKey<Jockey>(j => j.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tournament>()
            .HasMany(t => t.Races)
            .WithOne(r => r.Tournament)
            .HasForeignKey(r => r.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Race>()
            .HasMany(r => r.Entries)
            .WithOne(e => e.Race)
            .HasForeignKey(e => e.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RaceEntry>()
            .HasOne(e => e.Horse)
            .WithMany(h => h.RaceEntries)
            .HasForeignKey(e => e.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RaceEntry>()
            .HasOne(e => e.Jockey)
            .WithMany(j => j.RaceEntries)
            .HasForeignKey(e => e.JockeyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JockeyInvitation>()
            .HasOne(i => i.Horse)
            .WithMany(h => h.JockeyInvitations)
            .HasForeignKey(i => i.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JockeyInvitation>()
            .HasOne(i => i.Jockey)
            .WithMany(j => j.Invitations)
            .HasForeignKey(i => i.JockeyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<JockeyInvitation>()
            .HasOne(i => i.Race)
            .WithMany()
            .HasForeignKey(i => i.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Prediction>()
            .HasOne(p => p.Race)
            .WithMany(r => r.Predictions)
            .HasForeignKey(p => p.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Prediction>()
            .HasOne(p => p.PredictedHorse)
            .WithMany()
            .HasForeignKey(p => p.PredictedHorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Prediction>()
            .HasOne(p => p.Spectator)
            .WithMany()
            .HasForeignKey(p => p.SpectatorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RaceResult>()
            .HasOne(rr => rr.Race)
            .WithOne(r => r.Result)
            .HasForeignKey<RaceResult>(rr => rr.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RaceResult>()
            .HasOne(rr => rr.WinningHorse)
            .WithMany()
            .HasForeignKey(rr => rr.WinningHorseId)
            .OnDelete(DeleteBehavior.Restrict);

        // BE2 Model Configurations
        modelBuilder.Entity<Round>()
            .HasMany(r => r.Races)
            .WithOne(race => race.Round)
            .HasForeignKey(race => race.RoundId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tournament>()
            .HasMany(t => t.Rounds)
            .WithOne(r => r.Tournament)
            .HasForeignKey(r => r.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Referee>()
            .HasOne(r => r.User)
            .WithOne()
            .HasForeignKey<Referee>(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefereeAssignment>()
            .HasOne(a => a.Race)
            .WithMany(r => r.RefereeAssignments)
            .HasForeignKey(a => a.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefereeAssignment>()
            .HasOne(a => a.Referee)
            .WithMany(r => r.Assignments)
            .HasForeignKey(a => a.RefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RefereeAssignment>()
            .Property(a => a.Status)
            .HasConversion<string>();

        modelBuilder.Entity<HorseHealthCheck>()
            .HasOne(h => h.Horse)
            .WithMany()
            .HasForeignKey(h => h.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseHealthCheck>()
            .HasOne(h => h.Race)
            .WithMany(r => r.HealthChecks)
            .HasForeignKey(h => h.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseHealthCheck>()
            .HasOne(h => h.Referee)
            .WithMany()
            .HasForeignKey(h => h.RefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseHealthCheck>()
            .Property(h => h.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ViolationRecord>()
            .HasOne(v => v.Race)
            .WithMany(r => r.Violations)
            .HasForeignKey(v => v.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ViolationRecord>()
            .HasOne(v => v.RaceEntry)
            .WithMany()
            .HasForeignKey(v => v.RaceEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ViolationRecord>()
            .HasOne(v => v.Referee)
            .WithMany()
            .HasForeignKey(v => v.RefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ViolationRecord>()
            .Property(v => v.ViolationType)
            .HasConversion<string>();

        modelBuilder.Entity<RaceReport>()
            .HasOne(r => r.Race)
            .WithMany(race => race.Reports)
            .HasForeignKey(r => r.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RaceReport>()
            .HasOne(r => r.Referee)
            .WithMany()
            .HasForeignKey(r => r.RefereeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserRegistration>()
            .Property(u => u.Status)
            .HasConversion<string>();

        modelBuilder.Entity<UserRegistration>()
            .Property(u => u.RequestedRole)
            .HasConversion<string>();

        modelBuilder.Entity<UserRegistration>()
            .HasOne(u => u.ReviewedByUser)
            .WithMany()
            .HasForeignKey(u => u.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Notification Model Configuration
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .Property(n => n.Type)
            .HasConversion<string>();

        modelBuilder.Entity<Notification>()
            .Property(n => n.Category)
            .HasConversion<string>();

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.UserId);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.IsRead);

        // AuditLog Model Configuration
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Admin)
            .WithMany()
            .HasForeignKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.AffectedUser)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<AuditLog>()
            .Property(a => a.Action)
            .HasConversion<string>();

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.AdminId);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.EntityType);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(a => a.CreatedAt);

        // Tournament enhanced
        modelBuilder.Entity<Tournament>()
            .Property(t => t.SurfaceType)
            .HasConversion<string>();

        // Prize Model
        modelBuilder.Entity<Prize>()
            .HasOne(p => p.Tournament)
            .WithMany()
            .HasForeignKey(p => p.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Prize>()
            .HasOne(p => p.Race)
            .WithMany()
            .HasForeignKey(p => p.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Protest Model
        modelBuilder.Entity<Protest>()
            .HasOne(p => p.Race)
            .WithMany()
            .HasForeignKey(p => p.RaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Protest>()
            .HasOne(p => p.FiledByUser)
            .WithMany()
            .HasForeignKey(p => p.FiledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Protest>()
            .HasOne(p => p.AgainstEntry)
            .WithMany()
            .HasForeignKey(p => p.AgainstEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Protest>()
            .HasOne(p => p.RuledByUser)
            .WithMany()
            .HasForeignKey(p => p.RuledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Protest>()
            .Property(p => p.Status)
            .HasConversion<string>();

        // HorseTransfer Model
        modelBuilder.Entity<HorseTransfer>()
            .HasOne(t => t.Horse)
            .WithMany()
            .HasForeignKey(t => t.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseTransfer>()
            .HasOne(t => t.FromOwner)
            .WithMany()
            .HasForeignKey(t => t.FromOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseTransfer>()
            .HasOne(t => t.ToOwner)
            .WithMany()
            .HasForeignKey(t => t.ToOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseTransfer>()
            .HasOne(t => t.ApprovedByUser)
            .WithMany()
            .HasForeignKey(t => t.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<HorseTransfer>()
            .Property(t => t.Status)
            .HasConversion<string>();

        modelBuilder.Entity<HorseTransfer>()
            .Property(t => t.TransferType)
            .HasConversion<string>();

        // InjuryRecord Model
        modelBuilder.Entity<InjuryRecord>()
            .HasOne(i => i.Horse)
            .WithMany()
            .HasForeignKey(i => i.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InjuryRecord>()
            .HasOne(i => i.ReportedByUser)
            .WithMany()
            .HasForeignKey(i => i.ReportedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InjuryRecord>()
            .Property(i => i.Severity)
            .HasConversion<string>();

        modelBuilder.Entity<InjuryRecord>()
            .Property(i => i.Status)
            .HasConversion<string>();

        // Contract Model
        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Owner)
            .WithMany()
            .HasForeignKey(c => c.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Jockey)
            .WithMany()
            .HasForeignKey(c => c.JockeyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .HasOne(c => c.Horse)
            .WithMany()
            .HasForeignKey(c => c.HorseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Contract>()
            .Property(c => c.Status)
            .HasConversion<string>();

        // ── Unique constraints ──

        modelBuilder.Entity<RaceEntry>()
            .HasIndex(e => new { e.RaceId, e.HorseId })
            .IsUnique();

        // ── Transaction indexes ──
        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.Reference);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.SepayTransactionId)
            .IsUnique();

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => new { t.UserId, t.Status, t.CompletedAt });

        // ── Cascade delete → Restrict for financial entities ──
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Wallet>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Wallet>()
            .HasIndex(w => w.UserId)
            .IsUnique();

        modelBuilder.Entity<BankAccount>()
            .HasOne(b => b.User)
            .WithMany()
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<WithdrawalRequest>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
