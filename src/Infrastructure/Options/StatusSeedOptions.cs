namespace Infrastructure.Options;

public sealed class StatusSeedOptions
{
    public const string SectionName = "StatusSeed";

    public int LotoDrawCount { get; init; }

    public int EuroMillionsDrawCount { get; init; }
}
