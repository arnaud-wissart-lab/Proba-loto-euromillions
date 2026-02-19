namespace Worker.Email;

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; init; } = "localhost";

    public int Port { get; init; } = 1025;

    public bool UseStartTls { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public string SenderName { get; init; } = "Probabilites Loto & EuroMillions";

    public string SenderAddress { get; init; } = "no-reply@example.local";
}
