using System.Text;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using AntiCheat.Api.Hubs;
using AntiCheat.Api.Services;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Data;
using AntiCheat.Core.Data.Entities;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Models;
using AntiCheat.Core.Services;
using AntiCheat.Launcher;

Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/anticheat-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();
if (!Environment.UserInteractive)
{
    builder.Host.UseWindowsService();
}

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtSecretValue = jwtSection["Secret"];
var jwtIssuer = jwtSection["Issuer"] ?? "AntiCheatV6";
var jwtAudience = jwtSection["Audience"] ?? "AntiCheatV6.Client";

if (string.IsNullOrWhiteSpace(jwtSecretValue) || jwtSecretValue.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase) || jwtSecretValue.Contains("ac6-super-secret", StringComparison.OrdinalIgnoreCase))
{
    Log.Fatal("JWT Secret is not configured or is set to the default value. Set Jwt__Secret in configuration.");
    throw new InvalidOperationException("JWT Secret must be configured for production. Set Jwt__Secret in appsettings.json or environment variables.");
}

var jwtSecret = Encoding.UTF8.GetBytes(jwtSecretValue);
builder.Services.Configure<JwtSettings>(jwtSection);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(jwtSecret),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
    };
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();
var remoteApiSection = builder.Configuration.GetSection("RemoteApi");
builder.Services.Configure<RemoteApiSettings>(remoteApiSection);
var clamAvSection = builder.Configuration.GetSection("ClamAv");
builder.Services.Configure<ClamAvSettings>(clamAvSection);
var teamCymruSection = builder.Configuration.GetSection("TeamCymru");
builder.Services.Configure<TeamCymruSettings>(teamCymruSection);
var cloudinarySection = builder.Configuration.GetSection("Cloudinary");
builder.Services.Configure<CloudinarySettings>(cloudinarySection);

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddSingleton<IGameLauncher, GameLauncherService>();
builder.Services.AddScoped<IModeratorService, ModeratorService>();
builder.Services.AddScoped<IModChatService, ModChatService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISuperAdminService, SuperAdminService>();

builder.Services.AddSingleton<IConfidenceScorer, ConfidenceScorer>();
builder.Services.AddSingleton<IRiskEngine, RiskEngine>();
builder.Services.AddSingleton<IEvidenceCollector, EvidenceCollector>();
builder.Services.AddSingleton<IRuleManagerService, RuleManagerService>();
builder.Services.AddSingleton<ICorrelationEngine, CorrelationEngine>();
builder.Services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
builder.Services.AddSingleton<IScreenStreamService, ScreenStreamService>();
builder.Services.AddSingleton<DetectorLoader>();
builder.Services.AddSingleton<IUserConnectionTracker, UserConnectionTracker>();

builder.Services.AddSingleton<IHardwareIdProvider, HardwareIdProvider>();
builder.Services.AddSingleton<IMtasaSerialReader, MtasaSerialReader>();
builder.Services.AddSingleton<IMtasaPathFinder, MtasaPathFinder>();
builder.Services.AddSingleton<IDesktopCaptureService, DesktopCaptureService>();
builder.Services.AddSingleton<IWhitelistProvider, WhitelistProvider>();
builder.Services.AddSingleton<IMtaBaselineProvider, MtaBaselineProvider>();
builder.Services.AddScoped<IReputationService, ReputationService>();
builder.Services.AddSingleton<IPeAnalysisService, PeAnalysisService>();
builder.Services.AddSingleton<ISignatureEngine, SignatureEngineService>();
builder.Services.AddSingleton<IBehavioralMonitorService, BehavioralMonitorService>();
builder.Services.AddSingleton<IBaselineService, BaselineService>();
builder.Services.AddSingleton<IDeltaMonitorService, DeltaMonitorService>();
builder.Services.AddScoped<ICertificateReputationService, CertificateReputationService>();
builder.Services.AddSingleton<IClamAvService, ClamAvService>();
builder.Services.AddSingleton<ITeamCymruService, TeamCymruService>();
builder.Services.AddSingleton<IVerdictService, VerdictService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddSingleton<IDedupService, DedupService>();
builder.Services.AddHttpClient<IRemoteApiService, RemoteApiService>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<RemoteApiSettings>>().Value;
    if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
        client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/'));
    if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", settings.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds > 0 ? settings.RequestTimeoutSeconds : 10);
});

var updateSection = builder.Configuration.GetSection("UpdateInfo");
builder.Services.Configure<UpdateOptions>(updateSection);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<INotificationService, SignalRNotificationService>();
    builder.Services.AddScoped<BanService>();
    builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddHostedService<HeartbeatService>();

var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connStr))
{
    connStr = "Server=localhost;Database=mafia_security;User=root;Password=;";
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connStr, ServerVersion.AutoDetect(connStr)));

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
builder.Services.AddCors(options =>
{
    if (corsOrigins is { Length: > 0 })
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
    }
    else
    {
        // Allow any origin while still permitting credentials (required by SignalR).
        // AllowAnyOrigin() cannot be combined with AllowCredentials(), so use SetIsOriginAllowed.
        options.AddDefaultPolicy(policy =>
        {
            policy.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
    }
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Ensure DetectionFingerprints table exists (added after the initial migration was generated)
    try
    {
        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS `DetectionFingerprints` (
            `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
            `Fingerprint` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
            `PlayerId` varchar(64) CHARACTER SET utf8mb4 NULL,
            `Category` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
            `FirstSeenAt` datetime(6) NOT NULL,
            `LastSeenAt` datetime(6) NOT NULL,
            `HitCount` int NOT NULL,
            CONSTRAINT `PK_DetectionFingerprints` PRIMARY KEY (`Id`),
            INDEX `IX_DetectionFingerprints_Fingerprint` (`Fingerprint`),
            INDEX `IX_DetectionFingerprints_LastSeenAt` (`LastSeenAt`),
            INDEX `IX_DetectionFingerprints_Fingerprint_LastSeenAt` (`Fingerprint`, `LastSeenAt`)
        ) CHARACTER SET=utf8mb4;");
        Log.Information("DetectionFingerprints table ensured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create DetectionFingerprints table (may already exist)");
    }

    // Add PlayerId & BanId columns to Appeals if missing (added after initial migration)
    try
    {
        db.Database.ExecuteSqlRaw(@"ALTER TABLE `Appeals` ADD COLUMN IF NOT EXISTS `PlayerId` varchar(255) NULL");
        db.Database.ExecuteSqlRaw(@"ALTER TABLE `Appeals` ADD COLUMN IF NOT EXISTS `BanId` varchar(255) NULL");
        Log.Information("Appeals schema updated (PlayerId, BanId)");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not update Appeals schema (columns may already exist)");
    }

    // Ensure AppealMessages table exists (added after initial migration)
    try
    {
        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS `AppealMessages` (
            `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `AppealId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `SenderId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `SenderName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `Message` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
            `CreatedAt` datetime(6) NOT NULL,
            CONSTRAINT `PK_AppealMessages` PRIMARY KEY (`Id`),
            INDEX `IX_AppealMessages_AppealId` (`AppealId`),
            INDEX `IX_AppealMessages_CreatedAt` (`CreatedAt`)
        ) CHARACTER SET=utf8mb4;");
        Log.Information("AppealMessages table ensured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create AppealMessages table (may already exist)");
    }

    // Ensure ReportMessages table exists + ChatEnabled column on PlayerReports
    EnsureColumn(db, "PlayerReports", "TicketType", "varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'report_player'");
    EnsureColumn(db, "PlayerReports", "AttachmentUrl", "varchar(2048) CHARACTER SET utf8mb4 NULL");
    EnsureColumn(db, "PlayerReports", "ChatEnabled", "tinyint(1) NOT NULL DEFAULT 0");
    EnsureColumn(db, "PlayerReports", "IsFlagged", "tinyint(1) NOT NULL DEFAULT 0");

    try
    {
        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS `ReportMessages` (
            `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `ReportId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `SenderId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `SenderName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `Message` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
            `AttachmentUrl` varchar(2048) CHARACTER SET utf8mb4 NULL,
            `CreatedAt` datetime(6) NOT NULL,
            CONSTRAINT `PK_ReportMessages` PRIMARY KEY (`Id`),
            INDEX `IX_ReportMessages_ReportId` (`ReportId`),
            INDEX `IX_ReportMessages_CreatedAt` (`CreatedAt`)
        ) CHARACTER SET=utf8mb4;");
        Log.Information("ReportMessages table ensured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create ReportMessages table (may already exist)");
    }

    // Drop ModeratorReports table — it was dead data (moderators don't submit reports, players do)
    try
    {
        db.Database.ExecuteSqlRaw(@"DROP TABLE IF EXISTS `ModeratorReports`");
        Log.Information("ModeratorReports table dropped (obsolete)");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not drop ModeratorReports table");
    }

    // Ensure ModChatMessages table exists (added after initial migration)
    try
    {
        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS `ModChatMessages` (
            `Id` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `UserId` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
            `Username` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `Role` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'moderator',
            `Message` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
            `AttachmentUrl` varchar(2048) CHARACTER SET utf8mb4 NULL,
            `CreatedAt` datetime(6) NOT NULL,
            CONSTRAINT `PK_ModChatMessages` PRIMARY KEY (`Id`),
            INDEX `IX_ModChatMessages_CreatedAt` (`CreatedAt`)
        ) CHARACTER SET=utf8mb4;");
        Log.Information("ModChatMessages table ensured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create ModChatMessages table (may already exist)");
    }

    // Ensure Screenshots table exists
    try
    {
        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS `Screenshots` (
            `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
            `PlayerId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `HardwareId` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
            `DetectionEventId` varchar(36) CHARACTER SET utf8mb4 NULL,
            `Reason` varchar(500) CHARACTER SET utf8mb4 NOT NULL,
            `CloudinaryUrl` varchar(2048) CHARACTER SET utf8mb4 NOT NULL,
            `CloudinaryPublicId` varchar(256) CHARACTER SET utf8mb4 NULL,
            `FileSize` bigint NOT NULL,
            `HmacSignature` varchar(512) CHARACTER SET utf8mb4 NULL,
            `RequestId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
            `CapturedAt` datetime(6) NOT NULL,
            `CapturedBy` varchar(100) CHARACTER SET utf8mb4 NULL,
            CONSTRAINT `PK_Screenshots` PRIMARY KEY (`Id`),
            INDEX `IX_Screenshots_PlayerId` (`PlayerId`),
            INDEX `IX_Screenshots_HardwareId` (`HardwareId`),
            INDEX `IX_Screenshots_CapturedAt` (`CapturedAt`),
            INDEX `IX_Screenshots_RequestId` (`RequestId`)
        ) CHARACTER SET=utf8mb4;");
        Log.Information("Screenshots table ensured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create Screenshots table (may already exist)");
    }

    // Ensure StreamSessions table exists
    try
    {
        db.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS `StreamSessions` (
            `Id` varchar(36) CHARACTER SET utf8mb4 NOT NULL,
            `PlayerId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
            `HardwareId` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
            `Status` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
            `TargetFps` double NOT NULL,
            `JpegQuality` int NOT NULL,
            `TotalFrames` int NOT NULL,
            `EndedReason` varchar(100) CHARACTER SET utf8mb4 NULL,
            `StartedBy` varchar(100) CHARACTER SET utf8mb4 NULL,
            `StartedAt` datetime(6) NOT NULL,
            `EndedAt` datetime(6) NULL,
            CONSTRAINT `PK_StreamSessions` PRIMARY KEY (`Id`),
            INDEX `IX_StreamSessions_PlayerId` (`PlayerId`),
            INDEX `IX_StreamSessions_HardwareId` (`HardwareId`),
            INDEX `IX_StreamSessions_Status` (`Status`),
            INDEX `IX_StreamSessions_StartedAt` (`StartedAt`)
        ) CHARACTER SET=utf8mb4;");
        Log.Information("StreamSessions table ensured");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not create StreamSessions table (may already exist)");
    }

    // Reset all staff users to offline on startup — their SignalR connection
    // will set them back to online when they connect
    try
    {
        db.Database.ExecuteSqlRaw(@"UPDATE `Users` SET `Status` = 'offline' WHERE `Role` IN ('moderator', 'admin', 'superadmin')");
        Log.Information("Staff users reset to offline");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not reset staff status (may not be needed)");
    }

    // if (!db.Users.Any())
    // {
    //     db.Users.AddRange(
    //         new UserEntity { Id = "1", Username = "Player1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("player"), DisplayName = "ShadowStrike", Role = "player", TrustScore = 85, Level = 42, Xp = 4200, NextLevelXp = 5000 },
    //         new UserEntity { Id = "2", Username = "Mod1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("mod"), DisplayName = "NightWatch", Role = "moderator", TrustScore = 92, Level = 67, Xp = 6700, NextLevelXp = 7000 },
    //         new UserEntity { Id = "3", Username = "Admin1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin"), DisplayName = "Overseer", Role = "admin", TrustScore = 98, Level = 99, Xp = 9900, NextLevelXp = 10000 },
    //         new UserEntity { Id = "4", Username = "Super1", PasswordHash = BCrypt.Net.BCrypt.HashPassword("super"), DisplayName = "Architect", Role = "superadmin", TrustScore = 100, Level = 100, Xp = 10000, NextLevelXp = 10000 }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created default users");
    // }

    // if (!db.TimelineEvents.Any())
    // {
    //     var now = DateTime.UtcNow;
    //     db.TimelineEvents.AddRange(
    //         new TimelineEventEntity { Id = "e1", Type = "injection", Title = "DLL Injection Attempt Blocked", Description = "Blocked suspicious DLL injection into MTA process from unknown module", Timestamp = now.AddMinutes(-15), Severity = "critical", Category = "injection", Confidence = 0.98 },
    //         new TimelineEventEntity { Id = "e2", Type = "memory", Title = "Memory Pattern Match", Description = "Detected known cheat pattern in game memory region", Timestamp = now.AddHours(-1), Severity = "high", Category = "memory", Confidence = 0.92 },
    //         new TimelineEventEntity { Id = "e3", Type = "process", Title = "Suspicious Process Detected", Description = "Unknown process attempting to attach to game", Timestamp = now.AddHours(-2), Severity = "high", Category = "process", Confidence = 0.85 },
    //         new TimelineEventEntity { Id = "e4", Type = "network", Title = "Abnormal Network Traffic", Description = "Unusual packet pattern detected from game client", Timestamp = now.AddHours(-4), Severity = "medium", Category = "network", Confidence = 0.72 },
    //         new TimelineEventEntity { Id = "e5", Type = "kernel", Title = "Kernel Driver Verification", Description = "Signed driver check passed for all loaded kernel modules", Timestamp = now.AddHours(-6), Severity = "info", Category = "kernel", Confidence = 1.0 },
    //         new TimelineEventEntity { Id = "e6", Type = "yara", Title = "YARA Rule Match: generic_cheat", Description = "Pattern matching flagged suspicious memory region", Timestamp = now.AddDays(-1), Severity = "medium", Category = "yara", Confidence = 0.65 },
    //         new TimelineEventEntity { Id = "e7", Type = "injection", Title = "Thread Hijacking Attempt", Description = "Remote thread creation detected in target process", Timestamp = now.AddDays(-1), Severity = "critical", Category = "injection", Confidence = 0.95 },
    //         new TimelineEventEntity { Id = "e8", Type = "process", Title = "Whitelisted Process Verified", Description = "Game executable hash matched whitelist entry", Timestamp = now.AddDays(-2), Severity = "info", Category = "process", Confidence = 1.0 },
    //         new TimelineEventEntity { Id = "e9", Type = "memory", Title = "Memory Scan Completed", Description = "Full memory scan completed with no threats found", Timestamp = now.AddDays(-2), Severity = "low", Category = "memory", Confidence = 1.0 },
    //         new TimelineEventEntity { Id = "e10", Type = "network", Title = "Known Bad IP Blocked", Description = "Connection attempt to blacklisted IP address blocked", Timestamp = now.AddDays(-3), Severity = "high", Category = "network", Confidence = 0.99 }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created timeline events");
    // }

    // if (!db.ActivityEvents.Any())
    // {
    //     var now = DateTime.UtcNow;
    //     db.ActivityEvents.AddRange(
    //         new ActivityEventEntity { Type = "scan", Title = "Full System Scan", Description = "Memory and process scan completed — 0 threats found", Timestamp = now.AddMinutes(-10), Severity = "info", Icon = "shield" },
    //         new ActivityEventEntity { Type = "protection", Title = "Protection Module Updated", Description = "Memory Scanner signature database updated to v2.4.1", Timestamp = now.AddHours(-1), Severity = "info", Icon = "refresh" },
    //         new ActivityEventEntity { Type = "threat", Title = "Threat Neutralized", Description = "DLL injection attempt blocked", Timestamp = now.AddHours(-3), Severity = "high", Icon = "alert" },
    //         new ActivityEventEntity { Type = "game", Title = "Game Session Ended", Description = "MTA: San Andreas session lasted 2h 34m", Timestamp = now.AddHours(-5), Severity = "info", Icon = "gamepad" },
    //         new ActivityEventEntity { Type = "scan", Title = "Quick Scan", Description = "Process integrity check — all signatures valid", Timestamp = now.AddHours(-6), Severity = "info", Icon = "shield" },
    //         new ActivityEventEntity { Type = "achievement", Title = "Protection Milestone", Description = "1000 clean scans achieved — 99.7% detection rate maintained", Timestamp = now.AddDays(-1), Severity = "success", Icon = "award" },
    //         new ActivityEventEntity { Type = "system", Title = "System Health Check", Description = "All 6 protection modules running normally", Timestamp = now.AddDays(-1), Severity = "info", Icon = "activity" },
    //         new ActivityEventEntity { Type = "game", Title = "Game Launched", Description = "MTA: San Andreas started — protection engaged", Timestamp = now.AddDays(-2), Severity = "info", Icon = "gamepad" },
    //         new ActivityEventEntity { Type = "threat", Title = "Suspicious Process Terminated", Description = "Cheat engine process forcefully terminated", Timestamp = now.AddDays(-3), Severity = "high", Icon = "alert" },
    //         new ActivityEventEntity { Type = "update", Title = "Anti-Cheat Updated", Description = "Version 6.0.1 installed — 3 new detection rules added", Timestamp = now.AddDays(-7), Severity = "info", Icon = "download" }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created activity events");
    // }

    // if (!db.PlayerReports.Any())
    // {
    //     var now = DateTime.UtcNow;
    //     db.PlayerReports.AddRange(
    //         new PlayerReportEntity { Id = "r1", TicketType = "report_player", PlayerName = "GamerX99", Reason = "Speed Hacking", Description = "Observed player moving at impossible speeds during chase", Status = "resolved", CreatedAt = now.AddDays(-5), Result = "Confirmed - 7 day ban", ReporterId = "1" },
    //         new PlayerReportEntity { Id = "r2", TicketType = "report_player", PlayerName = "ShadowKill", Reason = "Aimbot", Description = "100% headshot accuracy across 3 consecutive rounds", Status = "investigating", CreatedAt = now.AddDays(-2), ReporterId = "1" },
    //         new PlayerReportEntity { Id = "r3", TicketType = "report_player", PlayerName = "NightHawk", Reason = "Wallhack", Description = "Player tracked enemies through walls consistently", Status = "pending", CreatedAt = now.AddHours(-6), ReporterId = "1" },
    //         new PlayerReportEntity { Id = "r4", TicketType = "report_player", PlayerName = "ProSniper", Reason = "ESP", Description = "Suspicious awareness of hidden player positions", Status = "dismissed", CreatedAt = now.AddDays(-10), Result = "Insufficient evidence", ReporterId = "1" }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created player reports");
    // }

    // if (!db.Alerts.Any())
    // {
    //     db.Alerts.AddRange(
    //         new AlertEntity { Id = "a1", Title = "Suspicious Process Injection", Description = "Detected CreateRemoteThread call targeting gta_sa.exe", Severity = "critical", Confidence = 98, Timestamp = "5m ago", ProcessName = "unknown_injector.exe", Resolved = false },
    //         new AlertEntity { Id = "a2", Title = "Multiple Login Attempts", Description = "Player_456 logged in from 3 different IPs in 2 minutes", Severity = "high", Confidence = 85, Timestamp = "12m ago", ProcessName = "gta_sa.exe", Resolved = false },
    //         new AlertEntity { Id = "a3", Title = "Unsigned Driver Detected", Description = "A previously unseen kernel driver was loaded: custom_drv.sys", Severity = "medium", Confidence = 72, Timestamp = "25m ago", ProcessName = "System", Resolved = true },
    //         new AlertEntity { Id = "a4", Title = "Memory RWX Page", Description = "Executable writeable memory region detected in game process", Severity = "high", Confidence = 88, Timestamp = "1h ago", ProcessName = "gta_sa.exe", Resolved = false }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created alerts");
    // }

    // if (!db.BanEntries.Any())
    // {
    //     db.BanEntries.AddRange(
    //         new BanEntryEntity { Id = "b1", Player = "HackerOne", Reason = "Memory manipulation — RWX injection detected", Type = "Permanent", IssuedBy = "Admin_01", IssuedAt = "2026-06-27", Active = true, Appeals = 0, BannedAt = new DateTime(2026, 6, 27, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b2", Player = "SpeedDemon", Reason = "Speed hack — modified game memory", Type = "Temporary", IssuedBy = "Mod_Alpha", IssuedAt = "2026-06-26", Active = true, Appeals = 1, BannedAt = new DateTime(2026, 6, 26, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b3", Player = "WallWatcher", Reason = "Wallhack — suspicious pattern detected", Type = "Permanent", IssuedBy = "Admin_01", IssuedAt = "2026-06-25", Active = true, Appeals = 2, BannedAt = new DateTime(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b4", Player = "InjectorPro", Reason = "DLL injection — unauthorized module", Type = "Temporary", IssuedBy = "Mod_Beta", IssuedAt = "2026-06-24", Active = true, Appeals = 0, BannedAt = new DateTime(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b5", Player = "ScriptKid", Reason = "Script abuse — automated play", Type = "Temporary", IssuedBy = "Mod_Alpha", IssuedAt = "2026-06-23", Active = false, Appeals = 0, BannedAt = new DateTime(2026, 6, 23, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b6", Player = "AimGod", Reason = "Aimbot — impossible accuracy", Type = "Permanent", IssuedBy = "Admin_01", IssuedAt = "2026-06-22", Active = true, Appeals = 1, BannedAt = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b7", Player = "TeleportUser", Reason = "Teleport hack — position manipulation", Type = "Permanent", IssuedBy = "Super1", IssuedAt = "2026-06-21", Active = true, Appeals = 0, BannedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b8", Player = "WallHack_22", Reason = "Wallhack — repeated offenses", Type = "Temporary", IssuedBy = "Mod_Gamma", IssuedAt = "2026-06-20", Active = false, Appeals = 0, BannedAt = new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b9", Player = "Player_999", Reason = "Toxic behavior — harassment", Type = "Temporary", IssuedBy = "Mod_Alpha", IssuedAt = "2026-06-19", Active = true, Appeals = 2, BannedAt = new DateTime(2026, 6, 19, 0, 0, 0, DateTimeKind.Utc) },
    //         new BanEntryEntity { Id = "b10", Player = "CheaterPro", Reason = "Multiple violations — final warning ignored", Type = "Permanent", IssuedBy = "Admin_01", IssuedAt = "2026-06-18", Active = true, Appeals = 1, BannedAt = new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc) }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created ban entries");
    // }

    // if (!db.Appeals.Any())
    // {
    //     db.Appeals.AddRange(
    //         new AppealEntity { Id = "ap1", Player = "CheaterPro", Reason = "False positive — claimed software conflict", BanType = "Permanent", Status = "Pending", Date = "2026-06-27", Reviewer = "\u2014" },
    //         new AppealEntity { Id = "ap2", Player = "SpeedDemon", Reason = "Says it was a one-time mistake", BanType = "Temporary", Status = "Pending", Date = "2026-06-26", Reviewer = "\u2014" },
    //         new AppealEntity { Id = "ap3", Player = "WallWatcher", Reason = "Claims someone else used their account", BanType = "Permanent", Status = "Approved", Date = "2026-06-25", Reviewer = "Admin_01" },
    //         new AppealEntity { Id = "ap4", Player = "AimGod", Reason = "Denies using any cheats", BanType = "Permanent", Status = "Pending", Date = "2026-06-24", Reviewer = "\u2014" },
    //         new AppealEntity { Id = "ap5", Player = "Player_999", Reason = "Says they've already apologized", BanType = "Temporary", Status = "Denied", Date = "2026-06-23", Reviewer = "Mod_Alpha" }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created appeals");
    // }

    // if (!db.WhitelistEntries.Any())
    // {
    //     db.WhitelistEntries.AddRange(
    //         new WhitelistEntryEntity { Id = "w1", Entry = "gta_sa.exe", Type = "Process", AddedBy = "Admin_01", AddedAt = "2026-01-15", Reason = "Game executable" },
    //         new WhitelistEntryEntity { Id = "w2", Entry = "C:\\Program Files\\MTA San Andreas 1.6\\*", Type = "Path", AddedBy = "Super1", AddedAt = "2026-01-15", Reason = "MTA:SA installation" },
    //         new WhitelistEntryEntity { Id = "w3", Entry = "discord.exe", Type = "Process", AddedBy = "Admin_01", AddedAt = "2026-02-20", Reason = "Voice communication" },
    //         new WhitelistEntryEntity { Id = "w4", Entry = "C:\\Windows\\System32\\*", Type = "Path", AddedBy = "System", AddedAt = "2026-01-01", Reason = "System directory" },
    //         new WhitelistEntryEntity { Id = "w5", Entry = "steam.exe", Type = "Process", AddedBy = "Admin_01", AddedAt = "2026-03-10", Reason = "Steam client" },
    //         new WhitelistEntryEntity { Id = "w6", Entry = "C:\\Program Files\\Common Files\\*", Type = "Path", AddedBy = "System", AddedAt = "2026-01-01", Reason = "Common files" }
    //     );
    //     db.SaveChanges();
    //     Log.Information("Development seed: created whitelist entries");
    // }

    if (!db.EngineRules.Any())
    {
        var seedRules = SignatureRuleLoader.LoadFromAssembly();
        if (seedRules.Count > 0)
        {
            foreach (var rule in seedRules)
            {
                db.EngineRules.Add(new EngineRuleEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = rule.Name,
                    Description = rule.Description ?? "",
                    Severity = rule.Severity ?? "medium",
                    Category = rule.Category ?? "",
                    MatchType = rule.MatchType,
                    ConditionsJson = rule.Conditions != null ? JsonSerializer.Serialize(rule.Conditions) : null,
                    PatternsJson = rule.Patterns.Count > 0 ? JsonSerializer.Serialize(rule.Patterns) : "[]",
                    TagsJson = rule.Tags.Count > 0 ? JsonSerializer.Serialize(rule.Tags) : "[]",
                    Enabled = true,
                    HitCount = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            db.SaveChanges();
            Log.Information("Engine rules seeded from embedded resources ({Count} rules)", seedRules.Count);
        }
    }

    var engineRules = db.EngineRules.Where(r => r.Enabled).ToList();
    if (engineRules.Count > 0)
    {
        var sigEngine = scope.ServiceProvider.GetRequiredService<ISignatureEngine>();
        var ruleModels = engineRules.Select(e => new SignatureRuleModel
        {
            Name = e.Name,
            Description = e.Description,
            Severity = e.Severity,
            Category = e.Category,
            MatchType = e.MatchType,
            Conditions = !string.IsNullOrEmpty(e.ConditionsJson)
                ? JsonSerializer.Deserialize<RuleConditions>(e.ConditionsJson)
                : null,
            Patterns = !string.IsNullOrEmpty(e.PatternsJson)
                ? JsonSerializer.Deserialize<List<string>>(e.PatternsJson) ?? new()
                : new(),
            Tags = !string.IsNullOrEmpty(e.TagsJson)
                ? JsonSerializer.Deserialize<List<string>>(e.TagsJson) ?? new()
                : new(),
        }).ToList();
        sigEngine.ReloadRules(ruleModels);
        Log.Information("SignatureEngine reloaded with {Count} rules from database", ruleModels.Count);
    }

    /*
    if (!db.AuditLogEntries.Any())
    {
        db.AuditLogEntries.AddRange(
            new AuditLogEntryEntity { Id = "al1", Action = "Ban Issued", User = "Admin_01", Target = "HackerOne", Details = "Permanent ban — memory manipulation", Timestamp = "2026-06-27 14:32:22", Ip = "192.168.1.100" },
            new AuditLogEntryEntity { Id = "al2", Action = "Config Changed", User = "Super1", Target = "Detection Engine", Details = "Scan interval changed to 30s", Timestamp = "2026-06-27 12:15:00", Ip = "192.168.1.1" },
            new AuditLogEntryEntity { Id = "al3", Action = "Appeal Reviewed", User = "Admin_01", Target = "CheaterPro", Details = "Appeal denied — evidence conclusive", Timestamp = "2026-06-27 11:45:33", Ip = "192.168.1.100" },
            new AuditLogEntryEntity { Id = "al4", Action = "Rule Updated", User = "Super1", Target = "RWX Memory Rule", Details = "Confidence threshold raised to 80%", Timestamp = "2026-06-26 23:10:15", Ip = "192.168.1.1" },
            new AuditLogEntryEntity { Id = "al5", Action = "User Login", User = "Admin_01", Target = "\u2014", Details = "Login from new IP address", Timestamp = "2026-06-26 09:00:00", Ip = "203.0.113.50" },
            new AuditLogEntryEntity { Id = "al6", Action = "Whitelist Added", User = "Super1", Target = "Discord.exe", Details = "Added to process whitelist", Timestamp = "2026-06-25 16:20:45", Ip = "192.168.1.1" },
            new AuditLogEntryEntity { Id = "al7", Action = "Service Restarted", User = "System", Target = "WebSocket Gateway", Details = "Automatic restart after update", Timestamp = "2026-06-25 03:00:00", Ip = "127.0.0.1" },
            new AuditLogEntryEntity { Id = "al8", Action = "Report Investigated", User = "Mod_Alpha", Target = "GamerXYZ", Details = "Speed hack confirmed — ban issued", Timestamp = "2026-06-24 18:30:12", Ip = "192.168.1.50" },
            new AuditLogEntryEntity { Id = "al9", Action = "YARA Update", User = "Super1", Target = "Rule Set v42", Details = "12 new rules deployed", Timestamp = "2026-06-24 14:00:00", Ip = "192.168.1.1" },
            new AuditLogEntryEntity { Id = "al10", Action = "Ban Expired", User = "System", Target = "FairPlayer99", Details = "Temporary ban expired — account reinstated", Timestamp = "2026-06-24 10:00:00", Ip = "127.0.0.1" }
        );
        db.SaveChanges();
        Log.Information("Development seed: created audit log entries");
    }
    */
}

// HTTPS redirect disabled for production LAN deployment over plain HTTP (http://<server-ip>:5000).
static void EnsureColumn(AppDbContext db, string table, string column, string columnDef)
{
    try
    {
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
        var exists = Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        if (!exists)
        {
            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {columnDef}";
            alter.ExecuteNonQuery();
            Log.Information("{Table}.{Column} column added", table, column);
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not ensure {Table}.{Column} column", table, column);
    }
}

// Re-enable if serving over HTTPS with a certificate.
// app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ServiceApiKeyMiddleware>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "updates")),
    RequestPath = "/updates",
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream",
});
app.MapControllers();
app.MapHub<AntiCheatHub>("/hub/anticheat");
app.MapHub<ScreenStreamHub>("/hub/screenstream");

try
{
    Log.Information("Anti-Cheat API starting");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
