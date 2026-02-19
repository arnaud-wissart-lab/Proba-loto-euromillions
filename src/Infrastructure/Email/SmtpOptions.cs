namespace Infrastructure.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public bool UseStartTls { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string SenderName { get; set; } = "Probabilites Loto & EuroMillions";

    public string SenderAddress { get; set; } = "no-reply@example.local";
}
