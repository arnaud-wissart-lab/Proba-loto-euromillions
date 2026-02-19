namespace Infrastructure.Services.DrawSync;

public sealed record FdjArchiveDescriptor(
    Uri DownloadUrl,
    Uri SourcePageUrl,
    string Label,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd);
