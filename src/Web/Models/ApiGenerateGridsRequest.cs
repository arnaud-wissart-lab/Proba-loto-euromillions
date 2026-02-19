namespace Web.Models;

public sealed class ApiGenerateGridsRequest
{
    public string Game { get; init; } = "Loto";

    public int Count { get; init; } = 5;

    public string Strategy { get; init; } = "uniform";
}
