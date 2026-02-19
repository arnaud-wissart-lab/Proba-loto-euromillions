namespace Web.Models;

public sealed class ApiStatusResponse
{
    public DateTimeOffset LastSyncAt { get; init; } = DateTimeOffset.MinValue;

    public ApiGameStatusResponse Loto { get; init; } = new();

    public ApiGameStatusResponse EuroMillions { get; init; } = new();

    public string Disclaimer { get; init; } =
        "Chaque combinaison reste equiprobable. Les contenus sont purement informatifs.";
}
