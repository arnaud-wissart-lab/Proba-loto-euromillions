namespace Worker.Jobs;

public sealed class SendSubscriptionsJobOptions
{
    public const string SectionName = "Jobs:SendSubscriptions";

    public bool DryRun { get; init; } = true;

    public string? TestRecipient { get; init; }
}
