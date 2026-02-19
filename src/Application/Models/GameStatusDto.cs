namespace Application.Models;

public sealed record GameStatusDto(
    int DrawsCount,
    DateOnly? LastDrawDate,
    DateOnly NextDrawDate);
