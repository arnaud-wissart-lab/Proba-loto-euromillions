namespace Web.Models;

public sealed class ApiGameStatusResponse
{
    public int DrawsCount { get; init; }

    public DateOnly? LastDrawDate { get; init; }

    public DateOnly NextDrawDate { get; init; }
}
