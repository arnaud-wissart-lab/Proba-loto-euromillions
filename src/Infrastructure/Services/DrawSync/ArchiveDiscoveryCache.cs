namespace Infrastructure.Services.DrawSync;

public sealed record ArchiveDiscoveryCache(
    string? ETag,
    DateTimeOffset? LastModifiedUtc,
    IReadOnlyCollection<FdjArchiveDescriptor> CachedArchives);
