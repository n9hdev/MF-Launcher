using AntiCheat.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AntiCheat.Core.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<SessionEntity> Sessions => Set<SessionEntity>();
    public DbSet<DeviceEntity> Devices => Set<DeviceEntity>();
    public DbSet<DetectionEventEntity> DetectionEvents => Set<DetectionEventEntity>();
    public DbSet<PlayerReportEntity> PlayerReports => Set<PlayerReportEntity>();
    public DbSet<ActivityEventEntity> ActivityEvents => Set<ActivityEventEntity>();
    public DbSet<TimelineEventEntity> TimelineEvents => Set<TimelineEventEntity>();
    public DbSet<BanEntryEntity> BanEntries => Set<BanEntryEntity>();
    public DbSet<AppealEntity> Appeals => Set<AppealEntity>();
    public DbSet<AppealMessageEntity> AppealMessages => Set<AppealMessageEntity>();
    public DbSet<WhitelistEntryEntity> WhitelistEntries => Set<WhitelistEntryEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<AuditLogEntryEntity> AuditLogEntries => Set<AuditLogEntryEntity>();
    public DbSet<FileReputationEntity> FileReputation => Set<FileReputationEntity>();
    public DbSet<GameFileHashEntity> GameFileHashes => Set<GameFileHashEntity>();
    public DbSet<EngineRuleEntity> EngineRules => Set<EngineRuleEntity>();
    public DbSet<CertificateReputationEntity> CertificateReputations => Set<CertificateReputationEntity>();
    public DbSet<ClamAvResultEntity> ClamAvResults => Set<ClamAvResultEntity>();
    public DbSet<TeamCymruResultEntity> TeamCymruResults => Set<TeamCymruResultEntity>();
    public DbSet<VerdictEntity> Verdicts => Set<VerdictEntity>();
    public DbSet<SandboxResultEntity> SandboxResults => Set<SandboxResultEntity>();
    public DbSet<DetectionFingerprintEntity> DetectionFingerprints => Set<DetectionFingerprintEntity>();
    public DbSet<ReportMessageEntity> ReportMessages => Set<ReportMessageEntity>();
    public DbSet<ModChatMessageEntity> ModChatMessages => Set<ModChatMessageEntity>();
    public DbSet<ScreenshotEntity> Screenshots => Set<ScreenshotEntity>();
    public DbSet<StreamSessionEntity> StreamSessions => Set<StreamSessionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserEntity>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Status);
            e.Property(u => u.Username).HasMaxLength(50);
            e.Property(u => u.DisplayName).HasMaxLength(100);
            e.Property(u => u.Role).HasMaxLength(20);
            e.Property(u => u.Status).HasMaxLength(20);
            e.HasIndex(u => u.HardwareId);
            e.Property(u => u.HardwareId).HasMaxLength(256);
            e.Property(u => u.SerialNumber).HasMaxLength(128);
            e.Property(u => u.GamePath).HasMaxLength(1024);
        });

        modelBuilder.Entity<RefreshTokenEntity>(e =>
        {
            e.HasIndex(t => t.Token).IsUnique();
            e.HasIndex(t => t.UserId);
            e.HasIndex(t => new { t.UserId, t.IsRevoked });
            e.Property(t => t.Token).HasMaxLength(128);
        });

        modelBuilder.Entity<SessionEntity>(e =>
        {
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => new { s.SessionId, s.UserId });
            e.Property(s => s.DeviceId).HasMaxLength(100);
            e.Property(s => s.IpAddress).HasMaxLength(50);
        });

        modelBuilder.Entity<DeviceEntity>(e =>
        {
            e.HasIndex(d => d.UserId);
            e.HasIndex(d => d.DeviceId).IsUnique();
            e.Property(d => d.DeviceName).HasMaxLength(100);
            e.Property(d => d.OsVersion).HasMaxLength(50);
            e.Property(d => d.Fingerprint).HasMaxLength(256);
        });

        modelBuilder.Entity<DetectionEventEntity>(e =>
        {
            e.HasIndex(de => de.PlayerId);
            e.HasIndex(de => de.Timestamp);
            e.Property(de => de.Type).HasMaxLength(50);
            e.Property(de => de.Severity).HasMaxLength(20);
        });

        modelBuilder.Entity<ActivityEventEntity>(e =>
        {
            e.HasIndex(ae => ae.Timestamp);
            e.Property(ae => ae.Type).HasMaxLength(50);
            e.Property(ae => ae.Title).HasMaxLength(200);
            e.Property(ae => ae.Severity).HasMaxLength(20);
            e.Property(ae => ae.Icon).HasMaxLength(50);
        });

        modelBuilder.Entity<TimelineEventEntity>(e =>
        {
            e.HasIndex(te => te.Timestamp);
            e.HasIndex(te => te.Severity);
            e.Property(te => te.Type).HasMaxLength(50);
            e.Property(te => te.Title).HasMaxLength(200);
            e.Property(te => te.Severity).HasMaxLength(20);
            e.Property(te => te.Category).HasMaxLength(50);
        });

        modelBuilder.Entity<BanEntryEntity>(e =>
        {
            e.HasIndex(be => be.Player);
            e.HasIndex(be => be.PlayerId)
                .HasDatabaseName("IX_BanEntries_PlayerId_Active")
                .IsUnique()
                .HasFilter("[Active] = 1");
            e.Property(be => be.Player).HasMaxLength(100);
            e.Property(be => be.Type).HasMaxLength(20);
            e.Property(be => be.IssuedBy).HasMaxLength(100);
            e.Property(be => be.SerialNumber).HasMaxLength(128);
            e.Property(be => be.IpAddress).HasMaxLength(50);
            e.Property(be => be.ProofUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<AppealEntity>(e =>
        {
            e.HasIndex(ae => ae.Player);
            e.HasIndex(ae => ae.Status);
            e.HasIndex(ae => ae.PlayerId);
            e.Property(ae => ae.Player).HasMaxLength(100);
            e.Property(ae => ae.BanType).HasMaxLength(20);
            e.Property(ae => ae.Status).HasMaxLength(20);
            e.Property(ae => ae.Reviewer).HasMaxLength(100);
            e.HasMany(ae => ae.Messages)
                .WithOne()
                .HasForeignKey(am => am.AppealId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppealMessageEntity>(e =>
        {
            e.HasIndex(am => am.AppealId);
            e.HasIndex(am => am.CreatedAt);
            e.Property(am => am.SenderName).HasMaxLength(100);
            e.Property(am => am.Message).HasMaxLength(4000);
        });

        modelBuilder.Entity<WhitelistEntryEntity>(e =>
        {
            e.Property(we => we.Entry).HasMaxLength(500);
            e.Property(we => we.Type).HasMaxLength(20);
            e.Property(we => we.AddedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<PlayerReportEntity>(e =>
        {
            e.HasIndex(pr => pr.ReporterId);
            e.HasIndex(pr => pr.Status);
            e.Property(pr => pr.TicketType).HasMaxLength(20);
            e.Property(pr => pr.PlayerName).HasMaxLength(100);
            e.Property(pr => pr.Reason).HasMaxLength(200);
            e.Property(pr => pr.Status).HasMaxLength(20);
            e.Property(pr => pr.AttachmentUrl).HasMaxLength(2048);
            e.HasMany(pr => pr.Messages)
                .WithOne(rm => rm.Report)
                .HasForeignKey(rm => rm.ReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportMessageEntity>(e =>
        {
            e.HasIndex(rm => rm.ReportId);
            e.HasIndex(rm => rm.CreatedAt);
            e.Property(rm => rm.SenderName).HasMaxLength(100);
            e.Property(rm => rm.Message).HasMaxLength(4000);
            e.Property(rm => rm.AttachmentUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<AlertEntity>(e =>
        {
            e.HasIndex(ae => ae.Resolved);
            e.Property(ae => ae.Title).HasMaxLength(200);
            e.Property(ae => ae.Severity).HasMaxLength(20);
            e.Property(ae => ae.ProcessName).HasMaxLength(100);
        });

        modelBuilder.Entity<AuditLogEntryEntity>(e =>
        {
            e.HasIndex(al => al.Timestamp);
            e.HasIndex(al => al.Action);
            e.Property(al => al.Action).HasMaxLength(50);
            e.Property(al => al.User).HasMaxLength(100);
            e.Property(al => al.Target).HasMaxLength(100);
        });

        modelBuilder.Entity<FileReputationEntity>(e =>
        {
            e.HasKey(f => f.Sha256);
            e.Property(f => f.Sha256).HasMaxLength(64);
            e.Property(f => f.Md5).HasMaxLength(32);
            e.Property(f => f.ProductName).HasMaxLength(256);
            e.Property(f => f.FileVersion).HasMaxLength(64);
            e.Property(f => f.Signer).HasMaxLength(512);
            e.Property(f => f.Verdict).HasMaxLength(32).HasDefaultValue("unknown");
            e.Property(f => f.AnalysisNotes).HasMaxLength(1024);
            e.HasIndex(f => f.Verdict);
            e.HasIndex(f => f.LastSeen);
        });

        modelBuilder.Entity<GameFileHashEntity>(e =>
        {
            e.HasKey(g => g.Sha256);
            e.Property(g => g.Sha256).HasMaxLength(64);
            e.Property(g => g.FilePath).HasMaxLength(1024);
            e.Property(g => g.Md5).HasMaxLength(32);
            e.Property(g => g.FileName).HasMaxLength(256);
            e.HasIndex(g => g.FileName);
            e.HasIndex(g => g.LastVerified);
        });

        modelBuilder.Entity<CertificateReputationEntity>(e =>
        {
            e.HasKey(c => c.Thumbprint);
            e.Property(c => c.Thumbprint).HasMaxLength(64);
            e.Property(c => c.Subject).HasMaxLength(512);
            e.Property(c => c.Issuer).HasMaxLength(512);
            e.Property(c => c.SerialNumber).HasMaxLength(128);
            e.Property(c => c.ChainStatus).HasMaxLength(2000);
            e.Property(c => c.Verdict).HasMaxLength(32);
            e.HasIndex(c => c.LastVerified);
        });

        modelBuilder.Entity<ClamAvResultEntity>(e =>
        {
            e.HasKey(c => c.Sha256);
            e.Property(c => c.Sha256).HasMaxLength(64);
            e.Property(c => c.VirusName).HasMaxLength(256);
            e.Property(c => c.ScanResult).HasMaxLength(32);
            e.HasIndex(c => c.ScannedAt);
        });

        modelBuilder.Entity<TeamCymruResultEntity>(e =>
        {
            e.HasKey(t => t.Sha256);
            e.Property(t => t.Sha256).HasMaxLength(64);
            e.Property(t => t.ScanResult).HasMaxLength(64);
            e.HasIndex(t => t.ScannedAt);
        });

        modelBuilder.Entity<VerdictEntity>(e =>
        {
            e.HasKey(v => v.Id);
            e.Property(v => v.Id).HasMaxLength(36);
            e.Property(v => v.PlayerId).HasMaxLength(64);
            e.Property(v => v.Verdict).HasMaxLength(32);
            e.Property(v => v.SuggestedAction).HasMaxLength(32);
            e.Property(v => v.Explanation).HasMaxLength(2000);
            e.Property(v => v.ContributionsJson).HasMaxLength(2000);
            e.HasIndex(v => v.AssessedAt);
            e.HasIndex(v => v.PlayerId);
        });

        modelBuilder.Entity<SandboxResultEntity>(e =>
        {
            e.HasKey(s => s.Sha256);
            e.Property(s => s.Sha256).HasMaxLength(64);
            e.Property(s => s.Verdict).HasMaxLength(32);
            e.Property(s => s.DetailsJson).HasMaxLength(4000);
            e.HasIndex(s => s.AnalysedAt);
        });

        modelBuilder.Entity<DetectionFingerprintEntity>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasMaxLength(36);
            e.Property(f => f.Fingerprint).HasMaxLength(64);
            e.Property(f => f.PlayerId).HasMaxLength(64);
            e.Property(f => f.Category).HasMaxLength(32);
            e.HasIndex(f => f.Fingerprint);
            e.HasIndex(f => f.LastSeenAt);
            e.HasIndex(f => new { f.Fingerprint, f.LastSeenAt });
        });

        modelBuilder.Entity<ModChatMessageEntity>(e =>
        {
            e.HasIndex(m => m.CreatedAt);
            e.Property(m => m.Username).HasMaxLength(100);
            e.Property(m => m.Role).HasMaxLength(20);
            e.Property(m => m.Message).HasMaxLength(4000);
            e.Property(m => m.AttachmentUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<ScreenshotEntity>(e =>
        {
            e.HasIndex(s => s.PlayerId);
            e.HasIndex(s => s.HardwareId);
            e.HasIndex(s => s.CapturedAt);
            e.HasIndex(s => s.RequestId);
            e.Property(s => s.PlayerId).HasMaxLength(100);
            e.Property(s => s.HardwareId).HasMaxLength(256);
            e.Property(s => s.DetectionEventId).HasMaxLength(36);
            e.Property(s => s.Reason).HasMaxLength(500);
            e.Property(s => s.CloudinaryUrl).HasMaxLength(2048);
            e.Property(s => s.CloudinaryPublicId).HasMaxLength(256);
            e.Property(s => s.RequestId).HasMaxLength(100);
            e.Property(s => s.Status).HasMaxLength(20);
            e.Property(s => s.CapturedBy).HasMaxLength(100);
            e.Property(s => s.HmacSignature).HasMaxLength(512);
        });

        modelBuilder.Entity<StreamSessionEntity>(e =>
        {
            e.HasIndex(s => s.PlayerId);
            e.HasIndex(s => s.HardwareId);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.StartedAt);
            e.Property(s => s.PlayerId).HasMaxLength(100);
            e.Property(s => s.HardwareId).HasMaxLength(256);
            e.Property(s => s.Status).HasMaxLength(20);
            e.Property(s => s.EndedReason).HasMaxLength(100);
            e.Property(s => s.StartedBy).HasMaxLength(100);
        });
    }
}
