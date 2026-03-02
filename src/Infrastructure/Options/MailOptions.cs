namespace Infrastructure.Options;

public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public bool Enabled { get; set; } = true;

    public string From { get; set; } = "no-reply@example.local";

    public string FromName { get; set; } = "Proba Loto";

    public string BaseUrl { get; set; } = "http://localhost:8080";

    public MailSmtpOptions Smtp { get; set; } = new();

    public MailScheduleOptions Schedule { get; set; } = new();
}

public sealed class MailSmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseSsl { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class MailScheduleOptions
{
    public int SendHourLocal { get; set; } = 8;

    public int SendMinuteLocal { get; set; }

    public string TimeZone { get; set; } = "Europe/Paris";

    public bool Force { get; set; }
}
