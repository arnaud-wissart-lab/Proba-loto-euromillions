namespace Infrastructure.Options;

public sealed class SubscriptionOptions
{
    public const string SectionName = "Subscriptions";

    public string PublicBaseUrl { get; set; } = "http://localhost:8080";

    public string TokenSecret { get; set; } = "dev-only-change-me";
}
