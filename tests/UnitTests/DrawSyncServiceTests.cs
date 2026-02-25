using System.Text;
using Domain.Enums;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Services.DrawSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace UnitTests;

public sealed class DrawSyncServiceTests
{
    [Fact]
    public async Task SyncGameShouldFallbackToDirectFileWhenPayloadIsNotZip()
    {
        await using var dbContext = CreateDbContext(nameof(SyncGameShouldFallbackToDirectFileWhenPayloadIsNotZip));
        var sourcePageUrl = new Uri("https://example.test/loto/historique");
        var archiveUrl = new Uri("https://example.test/loto.csv");

        const string csv = """
date_tirage;numero1;numero2;numero3;numero4;numero5;num_chance
19/02/2026;1;2;3;4;5;9
""";

        var archiveClient = new FakeArchiveClient(
            new ArchiveDiscoveryResult(
                [
                    new FdjArchiveDescriptor(
                        archiveUrl,
                        sourcePageUrl,
                        "archive directe",
                        null,
                        null)
                ],
                ETag: null,
                LastModifiedUtc: null,
                IsFromCache: false),
            new Dictionary<string, byte[]>
            {
                [archiveUrl.AbsoluteUri] = Encoding.UTF8.GetBytes(csv)
            });

        var options = Options.Create(new DrawSyncOptions
        {
            Loto = new DrawSyncOptions.GameOptions
            {
                HistoryUrl = sourcePageUrl.AbsoluteUri,
                RuleStartDate = new DateOnly(2019, 11, 4)
            },
            EuroMillions = new DrawSyncOptions.GameOptions
            {
                HistoryUrl = "https://example.test/euromillions/historique",
                RuleStartDate = new DateOnly(2016, 9, 1)
            }
        });

        var service = new DrawSyncService(
            dbContext,
            archiveClient,
            new FdjDrawFileParser(NullLogger<FdjDrawFileParser>.Instance),
            options,
            NullLogger<DrawSyncService>.Instance);

        var result = await service.SyncGameAsync(LotteryGame.Loto, "unit-test", CancellationToken.None);

        Assert.Equal(SyncRunStatus.Success, result.Status);
        Assert.Equal(1, result.DrawsUpsertedCount);
        Assert.Null(result.Error);

        var draw = await dbContext.Draws.SingleAsync();
        Assert.Equal(LotteryGame.Loto, draw.Game);
        Assert.Equal(new DateOnly(2026, 2, 19), draw.DrawDate);
        Assert.Equal([1, 2, 3, 4, 5], draw.MainNumbers);
        Assert.Equal([9], draw.BonusNumbers);
    }

    private static LotteryDbContext CreateDbContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<LotteryDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        return new LotteryDbContext(options);
    }

    private sealed class FakeArchiveClient(
        ArchiveDiscoveryResult discoveryResult,
        IReadOnlyDictionary<string, byte[]> payloadByUrl) : IFdjArchiveClient
    {
        public Task<ArchiveDiscoveryResult> DiscoverArchivesAsync(
            LotteryGame game,
            ArchiveDiscoveryCache? cache,
            CancellationToken cancellationToken) =>
            Task.FromResult(discoveryResult);

        public Task<byte[]> DownloadArchiveAsync(Uri archiveUrl, CancellationToken cancellationToken)
        {
            if (!payloadByUrl.TryGetValue(archiveUrl.AbsoluteUri, out var payload))
            {
                throw new InvalidOperationException($"Payload introuvable pour {archiveUrl.AbsoluteUri}");
            }

            return Task.FromResult(payload);
        }
    }
}
