namespace Web.Models;

public sealed class ApiNumberStatResponse
{
    public int Number { get; init; }

    public int Occurrences { get; init; }

    public double FrequencyPct { get; init; }

    public DateOnly? LastSeenDate { get; init; }
}
