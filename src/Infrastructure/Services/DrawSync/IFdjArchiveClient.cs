using Domain.Enums;

namespace Infrastructure.Services.DrawSync;

public interface IFdjArchiveClient
{
    Task<IReadOnlyCollection<FdjArchiveDescriptor>> DiscoverArchivesAsync(LotteryGame game, CancellationToken cancellationToken);

    Task<byte[]> DownloadArchiveAsync(Uri archiveUrl, CancellationToken cancellationToken);
}
