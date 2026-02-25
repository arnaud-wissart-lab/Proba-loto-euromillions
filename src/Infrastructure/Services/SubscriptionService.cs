using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Email;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class SubscriptionService(
    LotteryDbContext dbContext,
    IGridGenerationService gridGenerationService,
    IEmailSender emailSender,
    IOptions<SubscriptionOptions> options,
    ILogger<SubscriptionService> logger) : ISubscriptionService, ISubscriptionDispatchService
{
    private static readonly TimeZoneInfo ParisTimeZone = ResolveParisTimeZone();

    public async Task RequestSubscriptionAsync(CreateSubscriptionRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeAndValidateEmail(request.Email);
        var entries = request.Entries?.ToArray() ?? [];
        if (entries.Length == 0)
        {
            throw new ArgumentException("Au moins un abonnement doit être sélectionné.", nameof(request.Entries));
        }

        var subscriptionsToProcess = new Dictionary<LotteryGame, (int GridCount, GridGenerationStrategy Strategy)>();

        foreach (var entry in entries)
        {
            if (!LotteryGameRulesCatalog.TryParseGame(entry.Game, out var game))
            {
                throw new ArgumentException("Jeu invalide.", nameof(request.Entries));
            }

            if (!GridGenerationStrategyExtensions.TryParseStrategy(entry.Strategy, out var strategy))
            {
                throw new ArgumentException("Stratégie invalide.", nameof(request.Entries));
            }

            if (entry.GridCount is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.Entries),
                    "Le nombre de grilles doit être compris entre 1 et 100.");
            }

            subscriptionsToProcess[game] = (entry.GridCount, strategy);
        }

        if (subscriptionsToProcess.Count == 0)
        {
            throw new ArgumentException("Au moins un abonnement valide doit être sélectionné.", nameof(request.Entries));
        }

        var emailPayloads = new List<(SubscriptionEntity Subscription, string ConfirmToken, string UnsubscribeToken)>(subscriptionsToProcess.Count);

        foreach (var (game, config) in subscriptionsToProcess)
        {
            var subscription = await dbContext.Subscriptions
                .Where(entity => entity.Email == normalizedEmail
                                 && entity.Game == game
                                 && entity.Status != SubscriptionStatus.Unsubscribed)
                .OrderByDescending(entity => entity.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (subscription is null)
            {
                subscription = new SubscriptionEntity
                {
                    Email = normalizedEmail,
                    Game = game
                };
                dbContext.Subscriptions.Add(subscription);
            }

            subscription.GridCount = config.GridCount;
            subscription.Strategy = config.Strategy;
            subscription.Status = SubscriptionStatus.Pending;
            subscription.ConfirmedAt = null;
            subscription.UnsubscribedAt = null;
            subscription.LastSentForDrawDate = null;

            var confirmToken = BuildToken(subscription, "confirm");
            var unsubscribeToken = BuildToken(subscription, "unsubscribe");
            subscription.ConfirmTokenHash = HashToken(confirmToken);
            subscription.UnsubTokenHash = HashToken(unsubscribeToken);

            emailPayloads.Add((subscription, confirmToken, unsubscribeToken));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var payload in emailPayloads)
        {
            var message = SubscriptionEmailTemplates.BuildConfirmationEmail(
                payload.Subscription,
                BuildPublicLink("abonnement/confirmation", payload.ConfirmToken),
                BuildPublicLink("abonnement/desinscription", payload.UnsubscribeToken));

            await emailSender.SendAsync(message, cancellationToken);
        }
    }

    public async Task<SubscriptionActionResultDto> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new SubscriptionActionResultDto(false, "Lien de confirmation invalide.");
        }

        var tokenHash = HashToken(token);
        var subscription = await dbContext.Subscriptions
            .FirstOrDefaultAsync(entity => entity.ConfirmTokenHash == tokenHash, cancellationToken);

        if (subscription is null)
        {
            subscription = await FindByDeterministicTokenAsync(token, "confirm", cancellationToken);
            if (subscription is not null)
            {
                subscription.ConfirmTokenHash = tokenHash;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (subscription is null || subscription.Status == SubscriptionStatus.Unsubscribed)
        {
            return new SubscriptionActionResultDto(false, "Lien de confirmation invalide ou expiré.");
        }

        if (subscription.Status == SubscriptionStatus.Active)
        {
            return new SubscriptionActionResultDto(true, "Abonnement déjà confirmé.");
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.ConfirmedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscriptionActionResultDto(true, "Abonnement confirmé avec succès.");
    }

    public async Task<SubscriptionActionResultDto> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new SubscriptionActionResultDto(false, "Lien de désinscription invalide.");
        }

        var tokenHash = HashToken(token);
        var subscription = await dbContext.Subscriptions
            .FirstOrDefaultAsync(entity => entity.UnsubTokenHash == tokenHash, cancellationToken);

        if (subscription is null)
        {
            subscription = await FindByDeterministicTokenAsync(token, "unsubscribe", cancellationToken);
            if (subscription is not null)
            {
                subscription.UnsubTokenHash = tokenHash;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (subscription is null)
        {
            return new SubscriptionActionResultDto(false, "Lien de désinscription invalide ou expiré.");
        }

        if (subscription.Status == SubscriptionStatus.Unsubscribed)
        {
            return new SubscriptionActionResultDto(true, "Désinscription déjà effective.");
        }

        subscription.Status = SubscriptionStatus.Unsubscribed;
        subscription.UnsubscribedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SubscriptionActionResultDto(true, "Désinscription effectuée.");
    }

    public async Task<SubscriptionStatusDto> GetStatusByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeAndValidateEmail(email);

        var subscriptions = await dbContext.Subscriptions
            .AsNoTracking()
            .Where(entity => entity.Email == normalizedEmail)
            .OrderByDescending(entity => entity.CreatedAt)
            .Select(entity => new SubscriptionStatusItemDto(
                entity.Game.ToString(),
                entity.GridCount,
                entity.Strategy.ToApiValue(),
                entity.Status.ToString(),
                entity.CreatedAt,
                entity.ConfirmedAt,
                entity.UnsubscribedAt))
            .ToArrayAsync(cancellationToken);

        return new SubscriptionStatusDto(normalizedEmail, subscriptions);
    }

    public async Task DeleteDataByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeAndValidateEmail(email);
        var subscriptions = await dbContext.Subscriptions
            .Where(entity => entity.Email == normalizedEmail)
            .ToListAsync(cancellationToken);

        if (subscriptions.Count == 0)
        {
            return;
        }

        dbContext.Subscriptions.RemoveRange(subscriptions);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubscriptionDispatchSummaryDto> SendForUpcomingDrawsAsync(
        DateTimeOffset executionTimeUtc,
        CancellationToken cancellationToken)
    {
        var parisNow = TimeZoneInfo.ConvertTime(executionTimeUtc, ParisTimeZone);
        var referenceDate = DateOnly.FromDateTime(parisNow.DateTime);
        var tomorrow = referenceDate.AddDays(1);

        var activeSubscriptions = await dbContext.Subscriptions
            .Where(entity => entity.Status == SubscriptionStatus.Active)
            .ToListAsync(cancellationToken);

        var sentCount = 0;
        var failedCount = 0;
        var skippedCount = 0;

        foreach (var subscription in activeSubscriptions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextDrawDate = LotteryGameRulesCatalog.GetNextDrawDate(subscription.Game, referenceDate);
            if (nextDrawDate != tomorrow || subscription.LastSentForDrawDate == nextDrawDate)
            {
                skippedCount++;
                continue;
            }

            try
            {
                var generated = await gridGenerationService.GenerateAsync(
                    subscription.Game,
                    subscription.GridCount,
                    subscription.Strategy,
                    cancellationToken);

                var unsubscribeToken = BuildToken(subscription, "unsubscribe");
                var message = SubscriptionEmailTemplates.BuildNotificationEmail(
                    subscription,
                    generated,
                    nextDrawDate,
                    BuildPublicLink("abonnement/desinscription", unsubscribeToken));

                await emailSender.SendAsync(message, cancellationToken);

                subscription.LastSentForDrawDate = nextDrawDate;
                dbContext.EmailSendLogs.Add(new EmailSendLogEntity
                {
                    SubscriptionId = subscription.Id,
                    IntendedDrawDate = nextDrawDate,
                    SentAt = DateTimeOffset.UtcNow,
                    Status = EmailSendLogStatus.Sent
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                sentCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                dbContext.EmailSendLogs.Add(new EmailSendLogEntity
                {
                    SubscriptionId = subscription.Id,
                    IntendedDrawDate = nextDrawDate,
                    SentAt = DateTimeOffset.UtcNow,
                    Status = EmailSendLogStatus.Failed,
                    Error = exception.GetType().Name
                });

                await dbContext.SaveChangesAsync(cancellationToken);
                failedCount++;

                logger.LogError(
                    exception,
                    "Échec d'envoi subscriptionId={SubscriptionId} drawDate={DrawDate}",
                    subscription.Id,
                    nextDrawDate);
            }
        }

        return new SubscriptionDispatchSummaryDto(
            referenceDate,
            activeSubscriptions.Count,
            sentCount,
            failedCount,
            skippedCount);
    }

    private string BuildPublicLink(string relativePath, string token)
    {
        var baseUrl = options.Value.PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{relativePath}?token={Uri.EscapeDataString(token)}";
    }

    private string BuildToken(SubscriptionEntity subscription, string purpose)
    {
        var identifier = subscription.Id.ToString("N");
        var payload = $"{identifier}:{purpose}:{subscription.Email}:{subscription.CreatedAt.ToUnixTimeMilliseconds()}";
        var secretBytes = Encoding.UTF8.GetBytes(options.Value.TokenSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(secretBytes);
        var signatureBytes = hmac.ComputeHash(payloadBytes);
        var signature = ToBase64Url(signatureBytes);
        return $"{identifier}.{purpose}.{signature}";
    }

    private async Task<SubscriptionEntity?> FindByDeterministicTokenAsync(
        string token,
        string purpose,
        CancellationToken cancellationToken)
    {
        var parts = token.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3 || !string.Equals(parts[1], purpose, StringComparison.Ordinal))
        {
            return null;
        }

        if (!Guid.TryParseExact(parts[0], "N", out var subscriptionId))
        {
            return null;
        }

        var subscription = await dbContext.Subscriptions
            .FirstOrDefaultAsync(entity => entity.Id == subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return null;
        }

        var expectedToken = BuildToken(subscription, purpose);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var providedBytes = Encoding.UTF8.GetBytes(token);

        return expectedBytes.Length == providedBytes.Length
               && CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes)
            ? subscription
            : null;
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string NormalizeAndValidateEmail(string rawEmail)
    {
        var normalized = rawEmail.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(normalized, out _))
        {
            throw new ArgumentException("E-mail invalide.", nameof(rawEmail));
        }

        return normalized;
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static TimeZoneInfo ResolveParisTimeZone()
    {
        var candidates = new[]
        {
            "Europe/Paris",
            "Romance Standard Time"
        };

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
