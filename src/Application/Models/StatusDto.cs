namespace Application.Models;

public sealed record StatusDto(
    DateTimeOffset DerniereMiseAJourUtc,
    int NbTiragesLoto,
    int NbTiragesEuroMillions,
    string Avertissement);
