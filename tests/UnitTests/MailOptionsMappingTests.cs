using Infrastructure;
using Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace UnitTests;

public sealed class MailOptionsMappingTests
{
    [Fact]
    public void AddInfrastructureShouldBindMailOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["Mail:Enabled"] = "true",
                ["Mail:From"] = "contact@example.local",
                ["Mail:FromName"] = "Proba Loto",
                ["Mail:BaseUrl"] = "https://loto.example.local",
                ["Mail:Smtp:Host"] = "smtp-relay.brevo.com",
                ["Mail:Smtp:Port"] = "587",
                ["Mail:Smtp:UseSsl"] = "true",
                ["Mail:Smtp:Username"] = "smtp-user",
                ["Mail:Smtp:Password"] = "smtp-pass",
                ["Mail:Schedule:SendHourLocal"] = "8",
                ["Mail:Schedule:SendMinuteLocal"] = "0",
                ["Mail:Schedule:TimeZone"] = "Europe/Paris",
                ["Mail:Schedule:Force"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Equal("contact@example.local", options.From);
        Assert.Equal("Proba Loto", options.FromName);
        Assert.Equal("https://loto.example.local", options.BaseUrl);
        Assert.Equal("smtp-relay.brevo.com", options.Smtp.Host);
        Assert.Equal(587, options.Smtp.Port);
        Assert.True(options.Smtp.UseSsl);
        Assert.Equal("smtp-user", options.Smtp.Username);
        Assert.Equal("smtp-pass", options.Smtp.Password);
        Assert.Equal(8, options.Schedule.SendHourLocal);
        Assert.Equal(0, options.Schedule.SendMinuteLocal);
        Assert.Equal("Europe/Paris", options.Schedule.TimeZone);
        Assert.False(options.Schedule.Force);
    }

    [Fact]
    public void AddInfrastructureShouldMapLegacySmtpEnvironmentKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=test;Username=test;Password=test",
                ["SMTP_HOST"] = "legacy-smtp.local",
                ["SMTP_PORT"] = "2525",
                ["SMTP_USER"] = "legacy-user",
                ["SMTP_PASS"] = "legacy-pass",
                ["SMTP_FROM"] = "Proba Loto <legacy@example.local>",
                ["SMTP_USE_STARTTLS"] = "true",
                ["PUBLIC_BASE_URL"] = "https://legacy.example.local"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MailOptions>>().Value;

        Assert.Equal("legacy-smtp.local", options.Smtp.Host);
        Assert.Equal(2525, options.Smtp.Port);
        Assert.True(options.Smtp.UseSsl);
        Assert.Equal("legacy-user", options.Smtp.Username);
        Assert.Equal("legacy-pass", options.Smtp.Password);
        Assert.Equal("legacy@example.local", options.From);
        Assert.Equal("Proba Loto", options.FromName);
        Assert.Equal("https://legacy.example.local", options.BaseUrl);
    }
}
