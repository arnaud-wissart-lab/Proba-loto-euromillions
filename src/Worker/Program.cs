using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Serilog;
using Worker.Email;
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
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<SyncDrawsJobOptions>(builder.Configuration.GetSection(SyncDrawsJobOptions.SectionName));
builder.Services.Configure<SendSubscriptionsJobOptions>(builder.Configuration.GetSection(SendSubscriptionsJobOptions.SectionName));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var syncJobOptions = builder.Configuration.GetSection(SyncDrawsJobOptions.SectionName).Get<SyncDrawsJobOptions>() ?? new SyncDrawsJobOptions();
var sendIntervalMinutes = builder.Configuration.GetValue<int?>("Jobs:SendSubscriptions:IntervalMinutes") ?? 60;
var syncTimeZone = ResolveParisTimeZone(syncJobOptions.TimeZoneId);

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
    quartz.AddTrigger(options => options
        .ForJob(sendJobKey)
        .WithIdentity($"{nameof(SendSubscriptionsJob)}-trigger")
        .StartNow()
        .WithSimpleSchedule(schedule => schedule
            .WithInterval(TimeSpan.FromMinutes(sendIntervalMinutes))
            .RepeatForever()));
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
