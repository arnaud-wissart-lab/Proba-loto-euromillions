using System.Diagnostics;
using System.IO.Compression;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.DrawSync;

public sealed class DrawSyncService(
    LotteryDbContext dbContext,
    IFdjArchiveClient archiveClient,
    IFdjDrawFileParser drawFileParser,
    IOptions<DrawSyncOptions> options,
    ILogger<DrawSyncService> logger) : IDrawSyncService
{
    private static readonly ActivitySource ActivitySource = new("ProbabilitesLotoEuroMillions.DrawSync");

    public async Task<SyncExecutionSummaryDto> SyncAllAsync(string trigger, CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        var gameResults = new List<GameSyncResultDto>(2);

        foreach (var game in new[] { LotteryGame.Loto, LotteryGame.EuroMillions })
        {
            gameResults.Add(await SyncGameAsync(game, trigger, cancellationToken));
        }

        return new SyncExecutionSummaryDto(startedAtUtc, DateTimeOffset.UtcNow, gameResults);
    }

    public async Task<GameSyncResultDto> SyncGameAsync(LotteryGame game, string trigger, CancellationToken cancellationToken)
    {
        using var activity = ActivitySource.StartActivity("draws.sync.game", ActivityKind.Internal);
        activity?.SetTag("draw.game", game.ToString());
        activity?.SetTag("draw.trigger", trigger);

        var startedAtUtc = DateTimeOffset.UtcNow;
        var run = new SyncRunEntity
        {
            Game = game,
            StartedAtUtc = startedAtUtc,
            Status = SyncRunStatus.Fail,
            DrawsUpsertedCount = 0
        };

        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var gameOptions = options.Value.GetGameOptions(game);
            var archives = await archiveClient.DiscoverArchivesAsync(game, cancellationToken);
            if (archives.Count == 0)
            {
                throw new InvalidOperationException($"Aucune archive FDJ trouvee pour {game}.");
            }

            var drawByDate = new Dictionary<DateOnly, (ParsedDraw Draw, string Source)>();

            foreach (var archive in archives)
            {
                var archiveBytes = await archiveClient.DownloadArchiveAsync(archive.DownloadUrl, cancellationToken);
                using var archiveStream = new MemoryStream(archiveBytes, writable: false);
                using var zip = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: false);

                foreach (var entry in zip.Entries.Where(item => item.Length > 0))
                {
                    using var entryStream = entry.Open();
                    using var entryBuffer = new MemoryStream();
                    await entryStream.CopyToAsync(entryBuffer, cancellationToken);
                    var entryBytes = entryBuffer.ToArray();

                    var parsedDraws = drawFileParser.ParseArchiveEntry(
                        game,
                        archive.DownloadUrl.AbsoluteUri,
                        entry.FullName,
                        entryBytes);

                    foreach (var parsedDraw in parsedDraws)
                    {
                        if (parsedDraw.DrawDate < gameOptions.RuleStartDate)
                        {
                            continue;
                        }

                        drawByDate[parsedDraw.DrawDate] = (parsedDraw, archive.DownloadUrl.AbsoluteUri);
                    }
                }
            }

            if (drawByDate.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Aucun tirage valide pour {game} apres la date de regle {gameOptions.RuleStartDate:yyyy-MM-dd}.");
            }

            var upsertedCount = await UpsertDrawsAsync(game, drawByDate, cancellationToken);
            var lastKnownDrawDate = drawByDate.Keys.Max();
            var finishedAtUtc = DateTimeOffset.UtcNow;

            await UpdateSyncStateAsync(game, finishedAtUtc, lastKnownDrawDate, cancellationToken);

            run.Status = SyncRunStatus.Success;
            run.FinishedAtUtc = finishedAtUtc;
            run.DrawsUpsertedCount = upsertedCount;
            run.Error = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Sync {Game} terminee (trigger={Trigger}, archives={ArchiveCount}, upserts={UpsertCount}, lastDrawDate={LastDrawDate}).",
                game,
                trigger,
                archives.Count,
                upsertedCount,
                lastKnownDrawDate);

            return new GameSyncResultDto(
                game,
                SyncRunStatus.Success,
                upsertedCount,
                startedAtUtc,
                finishedAtUtc,
                lastKnownDrawDate,
                null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var finishedAtUtc = DateTimeOffset.UtcNow;
            run.Status = SyncRunStatus.Fail;
            run.FinishedAtUtc = finishedAtUtc;
            run.DrawsUpsertedCount = 0;
            run.Error = exception.ToString();
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogError(exception, "Sync {Game} en echec (trigger={Trigger}).", game, trigger);

            return new GameSyncResultDto(
                game,
                SyncRunStatus.Fail,
                0,
                startedAtUtc,
                finishedAtUtc,
                null,
                exception.Message);
        }
    }

    private async Task<int> UpsertDrawsAsync(
        LotteryGame game,
        IReadOnlyDictionary<DateOnly, (ParsedDraw Draw, string Source)> parsedDraws,
        CancellationToken cancellationToken)
    {
        var targetDates = parsedDraws.Keys.ToArray();
        var existingByDate = await dbContext.Draws
            .Where(entity => entity.Game == game && targetDates.Contains(entity.DrawDate))
            .ToDictionaryAsync(entity => entity.DrawDate, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var upserted = 0;

        foreach (var (drawDate, value) in parsedDraws)
        {
            if (existingByDate.TryGetValue(drawDate, out var existing))
            {
                if (!existing.MainNumbers.SequenceEqual(value.Draw.MainNumbers)
                    || !existing.BonusNumbers.SequenceEqual(value.Draw.BonusNumbers)
                    || !string.Equals(existing.Source, value.Source, StringComparison.Ordinal))
                {
                    existing.MainNumbers = value.Draw.MainNumbers;
                    existing.BonusNumbers = value.Draw.BonusNumbers;
                    existing.Source = value.Source;
                    existing.UpdatedAtUtc = now;
                    upserted++;
                }

                continue;
            }

            dbContext.Draws.Add(new DrawEntity
            {
                Game = game,
                DrawDate = drawDate,
                MainNumbers = value.Draw.MainNumbers,
                BonusNumbers = value.Draw.BonusNumbers,
                Source = value.Source,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            upserted++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return upserted;
    }

    private async Task UpdateSyncStateAsync(
        LotteryGame game,
        DateTimeOffset finishedAtUtc,
        DateOnly lastKnownDrawDate,
        CancellationToken cancellationToken)
    {
        var state = await dbContext.SyncStates.FindAsync([game], cancellationToken);
        if (state is null)
        {
            state = new SyncStateEntity
            {
                Game = game
            };
            dbContext.SyncStates.Add(state);
        }

        state.LastSuccessfulSyncAtUtc = finishedAtUtc;
        state.LastKnownDrawDate = state.LastKnownDrawDate is { } existingLastDate && existingLastDate > lastKnownDrawDate
            ? existingLastDate
            : lastKnownDrawDate;
    }
}
