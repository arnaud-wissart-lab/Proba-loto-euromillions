namespace Web.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string ApiKey { get; set; } = string.Empty;

    public string WebUsername { get; set; } = "admin";

    public string WebPassword { get; set; } = string.Empty;

    public bool ProtectUi { get; set; } = true;
}
