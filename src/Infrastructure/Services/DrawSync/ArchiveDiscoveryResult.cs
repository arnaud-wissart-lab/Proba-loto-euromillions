namespace Infrastructure.Services.DrawSync;

public sealed record ArchiveDiscoveryResult(
    IReadOnlyCollection<FdjArchiveDescriptor> Archives,
    string? ETag,
    DateTimeOffset? LastModifiedUtc,
    bool IsFromCache);
