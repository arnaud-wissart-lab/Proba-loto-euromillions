namespace Web.Models;

public sealed class ApiStatusResponse
{
    public DateTimeOffset DerniereMiseAJourUtc { get; init; } = DateTimeOffset.UtcNow;

    public int NbTiragesLoto { get; init; }

    public int NbTiragesEuroMillions { get; init; }

    public string Avertissement { get; init; } =
        "Aucune prédiction n'est proposée : données purement informatives.";
}
