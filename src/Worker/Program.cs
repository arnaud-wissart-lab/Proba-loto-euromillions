using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using Worker.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSerilog(
    (services, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "Worker")
            .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<SyncDrawsJobOptions>(builder.Configuration.GetSection(SyncDrawsJobOptions.SectionName));
builder.Services.Configure<SendSubscriptionsJobOptions>(builder.Configuration.GetSection(SendSubscriptionsJobOptions.SectionName));

var syncJobOptions = builder.Configuration.GetSection(SyncDrawsJobOptions.SectionName).Get<SyncDrawsJobOptions>() ?? new SyncDrawsJobOptions();
var sendJobOptions = builder.Configuration.GetSection(SendSubscriptionsJobOptions.SectionName).Get<SendSubscriptionsJobOptions>() ?? new SendSubscriptionsJobOptions();
var syncTimeZone = ResolveParisTimeZone(syncJobOptions.TimeZoneId);
var sendTimeZone = ResolveParisTimeZone(sendJobOptions.TimeZoneId);

builder.Services.AddQuartz(quartz =>
{
    quartz.UseSimpleTypeLoader();
    quartz.UseInMemoryStore();

    var syncJobKey = new JobKey(nameof(SyncDrawsJob));
    quartz.AddJob<SyncDrawsJob>(options => options.WithIdentity(syncJobKey));

    quartz.AddTrigger(trigger =>
    {
        trigger.ForJob(syncJobKey)
            .WithIdentity($"{nameof(SyncDrawsJob)}-cron-trigger")
            .WithCronSchedule(
                syncJobOptions.Cron,
                cron => cron.InTimeZone(syncTimeZone));
    });

    if (syncJobOptions.RunOnStartup)
    {
        quartz.AddTrigger(trigger => trigger
            .ForJob(syncJobKey)
            .WithIdentity($"{nameof(SyncDrawsJob)}-startup-trigger")
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithRepeatCount(0)));
    }

    var sendJobKey = new JobKey(nameof(SendSubscriptionsJob));
    quartz.AddJob<SendSubscriptionsJob>(options => options.WithIdentity(sendJobKey));
    quartz.AddTrigger(trigger =>
    {
        trigger.ForJob(sendJobKey)
            .WithIdentity($"{nameof(SendSubscriptionsJob)}-cron-trigger")
            .WithCronSchedule(
                sendJobOptions.Cron,
                cron => cron.InTimeZone(sendTimeZone));
    });

    if (sendJobOptions.RunOnStartup)
    {
        quartz.AddTrigger(trigger => trigger
            .ForJob(sendJobKey)
            .WithIdentity($"{nameof(SendSubscriptionsJob)}-startup-trigger")
            .StartNow()
            .WithSimpleSchedule(schedule => schedule.WithRepeatCount(0)));
    }
});

builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
});

var host = builder.Build();

await using (var scope = host.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LotteryDbContext>();
    await dbContext.Database.MigrateAsync();
}

var startupLogger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("WorkerStartup");
startupLogger.LogInformation(
    "Planification Quartz active pour {JobName}: cron={Cron}, timezone={TimeZoneId}, runOnStartup={RunOnStartup}",
    nameof(SyncDrawsJob),
    syncJobOptions.Cron,
    syncTimeZone.Id,
    syncJobOptions.RunOnStartup);
startupLogger.LogInformation(
    "Planification Quartz active pour {JobName}: cron={Cron}, timezone={TimeZoneId}, runOnStartup={RunOnStartup}",
    nameof(SendSubscriptionsJob),
    sendJobOptions.Cron,
    sendTimeZone.Id,
    sendJobOptions.RunOnStartup);

host.Run();

static TimeZoneInfo ResolveParisTimeZone(string configuredTimeZoneId)
{
    var candidates = new[]
    {
        configuredTimeZoneId,
        "Europe/Paris",
        "Romance Standard Time"
    };

    foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(candidate);
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }
    }

    return TimeZoneInfo.Utc;
}
