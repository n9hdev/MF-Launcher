using Microsoft.Extensions.Caching.Memory;
using Serilog;
using AntiCheat.Core.Configuration;
using AntiCheat.Core.Interfaces;
using AntiCheat.Core.Services;
using AntiCheat.Detection.Detectors;
using AntiCheat.Service;
using AntiCheat.Service.Services;

var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "service-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var host = Host.CreateDefaultBuilder(args)
        .UseContentRoot(AppContext.BaseDirectory)
        .UseWindowsService(options =>
        {
            options.ServiceName = "MafiaCityAntiCheatV6";
        })
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.AddSingleton<IConfidenceScorer, ConfidenceScorer>();
            services.AddSingleton<IRiskEngine, RiskEngine>();
            services.AddSingleton<IEvidenceCollector, EvidenceCollector>();
            services.AddSingleton<ICorrelationEngine, CorrelationEngine>();
            services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
            services.AddSingleton<IScreenStreamService, ScreenStreamService>();
            services.AddSingleton<IWhitelistProvider, StaticWhitelistProvider>();
            services.AddScoped<IReputationService, ReputationService>();
            services.AddSingleton<IPeAnalysisService, PeAnalysisService>();
            services.AddSingleton<ISignatureEngine, SignatureEngineService>();
            services.AddSingleton<IBehavioralMonitorService, BehavioralMonitorService>();
            services.AddSingleton<IBaselineService, BaselineService>();
            services.AddSingleton<IDeltaMonitorService, DeltaMonitorService>();
            services.AddSingleton<ICertificateReputationService>(sp =>
            {
                var cache = sp.GetRequiredService<IMemoryCache>();
                var pathFinder = sp.GetRequiredService<IMtasaPathFinder>();
                var logger = sp.GetRequiredService<ILogger<CertificateReputationService>>();
                return new CertificateReputationService(cache, pathFinder, logger);
            });
            var clamAvSection = context.Configuration.GetSection("ClamAv");
            services.Configure<ClamAvSettings>(clamAvSection);
            services.AddSingleton<IClamAvService, ClamAvService>();
            var teamCymruSection = context.Configuration.GetSection("TeamCymru");
            services.Configure<TeamCymruSettings>(teamCymruSection);
            services.AddSingleton<ITeamCymruService, TeamCymruService>();
            services.AddSingleton<IVerdictService, VerdictService>();
            services.AddSingleton<IDedupService, DedupService>();
            services.AddMemoryCache();

            services.AddSingleton<IDesktopCaptureService, DesktopCaptureService>();
            services.AddSingleton<ICloudinaryService, CloudinaryService>();
            var cloudinarySection = context.Configuration.GetSection("Cloudinary");
            services.Configure<CloudinarySettings>(cloudinarySection);

            services.AddSingleton<IHardwareIdProvider, HardwareIdProvider>();
            services.AddSingleton<IMtasaSerialReader, MtasaSerialReader>();
            services.AddSingleton<IMtaBaselineProvider, MtaBaselineProvider>();
            services.AddSingleton<IMtasaPathFinder, MtasaPathFinder>();

            services.AddSingleton<ApiClientService>();
            services.AddSingleton<NamedPipeService>();
            services.AddHostedService(sp => sp.GetRequiredService<NamedPipeService>());
            services.AddSingleton<ServiceScreenCapture>();
            services.AddSingleton<IGameHashVerifier, GameHashVerifier>();

            services.AddSingleton<IEnumerable<IDetector>>(sp =>
            {
                var wl = sp.GetRequiredService<IWhitelistProvider>();
                var mta = sp.GetRequiredService<IMtaBaselineProvider>();
                return new IDetector[]
                {
                    new ProcessAnalyzer(sp.GetRequiredService<ILogger<ProcessAnalyzer>>(), wl),
                    new InjectionDetector(sp.GetRequiredService<ILogger<InjectionDetector>>(), mta),
                    new KernelScanner(sp.GetRequiredService<ILogger<KernelScanner>>(), wl),
                    new YaraDetector(
                        sp.GetRequiredService<ILogger<YaraDetector>>(),
                        sp.GetRequiredService<ISignatureEngine>(),
                        sp.GetRequiredService<IPeAnalysisService>(),
                        sp.GetRequiredService<IWhitelistProvider>()),
                    new AntiInjectionMonitor(sp.GetRequiredService<ILogger<AntiInjectionMonitor>>(), mta),
                    new ModuleIntegrityScanner(sp.GetRequiredService<ILogger<ModuleIntegrityScanner>>(), mta),
                    new AntiTamperService(sp.GetRequiredService<ILogger<AntiTamperService>>()),
                    new GameIntegrityDetector(
                        sp.GetRequiredService<ILogger<GameIntegrityDetector>>(),
                        sp.GetRequiredService<IMtasaPathFinder>(),
                        sp.GetRequiredService<IWhitelistProvider>(),
                        sp.GetRequiredService<IGameHashVerifier>()),
                    new BehavioralDetector(
                        sp.GetRequiredService<ILogger<BehavioralDetector>>(),
                        sp.GetRequiredService<IBehavioralMonitorService>()),
                    new CertificateReputationDetector(
                        sp.GetRequiredService<ILogger<CertificateReputationDetector>>(),
                        sp.GetRequiredService<ICertificateReputationService>()),
                    new ClamAvDetector(
                        sp.GetRequiredService<ILogger<ClamAvDetector>>(),
                        sp.GetRequiredService<IClamAvService>()),
                    new TeamCymruDetector(
                        sp.GetRequiredService<ILogger<TeamCymruDetector>>(),
                        sp.GetRequiredService<ITeamCymruService>()),
                };
            });

            services.AddSingleton<IDetectionEngine, DetectionEngine>();
            services.AddHostedService<AntiCheatWorker>();
        })
        .Build();

    Log.Information("Anti-Cheat Service starting");
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


