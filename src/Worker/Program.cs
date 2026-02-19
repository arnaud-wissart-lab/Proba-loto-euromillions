using Infrastructure;
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
builder.Services.Configure<SendSubscriptionsJobOptions>(builder.Configuration.GetSection(SendSubscriptionsJobOptions.SectionName));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

var syncIntervalMinutes = builder.Configuration.GetValue<int?>("Jobs:SyncDraws:IntervalMinutes") ?? 30;
var sendIntervalMinutes = builder.Configuration.GetValue<int?>("Jobs:SendSubscriptions:IntervalMinutes") ?? 60;

builder.Services.AddQuartz(quartz =>
{
    quartz.UseSimpleTypeLoader();
    quartz.UseInMemoryStore();

    var syncJobKey = new JobKey(nameof(SyncDrawsJob));
    quartz.AddJob<SyncDrawsJob>(options => options.WithIdentity(syncJobKey));
    quartz.AddTrigger(options => options
        .ForJob(syncJobKey)
        .WithIdentity($"{nameof(SyncDrawsJob)}-trigger")
        .StartNow()
        .WithSimpleSchedule(schedule => schedule
            .WithInterval(TimeSpan.FromMinutes(syncIntervalMinutes))
            .RepeatForever()));

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
host.Run();
