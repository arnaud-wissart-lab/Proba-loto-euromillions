namespace Application.Models;

public sealed record StatusDto(
    DateTimeOffset LastSyncAt,
    GameStatusDto Loto,
    GameStatusDto EuroMillions,
    string Disclaimer);
