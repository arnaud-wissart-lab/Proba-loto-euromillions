using Domain.Enums;

namespace Infrastructure.Services.DrawSync;

public interface IFdjArchiveClient
{
    Task<ArchiveDiscoveryResult> DiscoverArchivesAsync(
        LotteryGame game,
        ArchiveDiscoveryCache? cache,
        CancellationToken cancellationToken);

    Task<byte[]> DownloadArchiveAsync(Uri archiveUrl, CancellationToken cancellationToken);
}
