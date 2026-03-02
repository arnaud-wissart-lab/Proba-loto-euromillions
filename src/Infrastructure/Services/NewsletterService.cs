using System.Net.Mail;
using System.Security.Cryptography;
using Application.Abstractions;
using Application.Models;
using Infrastructure.Email;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class NewsletterService(
    LotteryDbContext dbContext,
    IEmailSender emailSender,
    IOptions<MailOptions> mailOptions,
    ILogger<NewsletterService> logger) : INewsletterService
{
    private const int MaxGridCount = 100;

    public async Task RequestSubscriptionAsync(NewsletterSubscribeRequestDto request, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeAndValidateEmail(request.Email);
        ValidateGridCounts(request.LotoGridsCount, request.EuroMillionsGridsCount, requireAtLeastOneGrid: true);

        var confirmToken = GenerateToken();
        var unsubscribeToken = GenerateToken();
        var subscriber = await dbContext.NewsletterSubscribers
            .FirstOrDefaultAsync(entity => entity.Email == normalizedEmail, cancellationToken);

        if (subscriber is null)
        {
            subscriber = new NewsletterSubscriberEntity
            {
                Email = normalizedEmail
            };

            dbContext.NewsletterSubscribers.Add(subscriber);
        }

        subscriber.LotoGridsCount = request.LotoGridsCount;
        subscriber.EuroMillionsGridsCount = request.EuroMillionsGridsCount;
        subscriber.ConfirmToken = confirmToken;
        subscriber.UnsubscribeToken = unsubscribeToken;
        subscriber.ConfirmedAtUtc = null;
        subscriber.IsActive = false;

        await dbContext.SaveChangesAsync(cancellationToken);

        var publicBaseUrl = mailOptions.Value.BaseUrl.TrimEnd('/');
        var confirmLink = BuildTokenizedLink(publicBaseUrl, "/api/v1/newsletter/confirm", confirmToken);
        var unsubscribeLink = BuildTokenizedLink(publicBaseUrl, "/api/v1/newsletter/unsubscribe", unsubscribeToken);
        var preferencesLink = BuildTokenizedLink(publicBaseUrl, "/abonnement/preferences", unsubscribeToken);

        var message = NewsletterEmailTemplates.BuildConfirmationEmail(
            normalizedEmail,
            request.LotoGridsCount,
            request.EuroMillionsGridsCount,
            confirmLink,
            unsubscribeLink,
            preferencesLink);

        await emailSender.SendAsync(message, cancellationToken);

        logger.LogInformation("Demande newsletter enregistree pour {Email}.", normalizedEmail);
    }

    public async Task<NewsletterActionResultDto> ConfirmAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new NewsletterActionResultDto(false, "Lien de confirmation invalide.");
        }

        var subscriber = await dbContext.NewsletterSubscribers
            .FirstOrDefaultAsync(entity => entity.ConfirmToken == token, cancellationToken);

        if (subscriber is null)
        {
            return new NewsletterActionResultDto(false, "Lien de confirmation invalide ou expiré.");
        }

        if (subscriber.ConfirmedAtUtc.HasValue && subscriber.IsActive)
        {
            return new NewsletterActionResultDto(true, "Abonnement déjà confirmé.");
        }

        subscriber.ConfirmedAtUtc = DateTimeOffset.UtcNow;
        subscriber.IsActive = true;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NewsletterActionResultDto(true, "Abonnement confirmé.");
    }

    public async Task<NewsletterActionResultDto> UnsubscribeAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new NewsletterActionResultDto(false, "Lien de désinscription invalide.");
        }

        var subscriber = await dbContext.NewsletterSubscribers
            .FirstOrDefaultAsync(entity => entity.UnsubscribeToken == token, cancellationToken);

        if (subscriber is null)
        {
            return new NewsletterActionResultDto(false, "Lien de désinscription invalide ou expiré.");
        }

        if (!subscriber.IsActive)
        {
            return new NewsletterActionResultDto(true, "Désinscription déjà effective.");
        }

        subscriber.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new NewsletterActionResultDto(true, "Désinscription effectuée.");
    }

    public async Task<NewsletterPreferencesDto?> GetPreferencesAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var subscriber = await dbContext.NewsletterSubscribers
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.UnsubscribeToken == token, cancellationToken);

        return subscriber is null
            ? null
            : new NewsletterPreferencesDto(
                subscriber.Email,
                subscriber.LotoGridsCount,
                subscriber.EuroMillionsGridsCount,
                subscriber.IsActive,
                subscriber.ConfirmedAtUtc);
    }

    public async Task<NewsletterActionResultDto> UpdatePreferencesAsync(
        NewsletterPreferencesUpdateRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return new NewsletterActionResultDto(false, "Token manquant.");
        }

        ValidateGridCounts(request.LotoGridsCount, request.EuroMillionsGridsCount, requireAtLeastOneGrid: true);

        var subscriber = await dbContext.NewsletterSubscribers
            .FirstOrDefaultAsync(entity => entity.UnsubscribeToken == request.Token, cancellationToken);

        if (subscriber is null)
        {
            return new NewsletterActionResultDto(false, "Token invalide.");
        }

        subscriber.LotoGridsCount = request.LotoGridsCount;
        subscriber.EuroMillionsGridsCount = request.EuroMillionsGridsCount;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new NewsletterActionResultDto(true, "Préférences mises à jour.");
    }

    private static void ValidateGridCounts(int lotoGridsCount, int euroMillionsGridsCount, bool requireAtLeastOneGrid)
    {
        if (lotoGridsCount is < 0 or > MaxGridCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lotoGridsCount), "Le nombre de grilles Loto doit être compris entre 0 et 100.");
        }

        if (euroMillionsGridsCount is < 0 or > MaxGridCount)
        {
            throw new ArgumentOutOfRangeException(nameof(euroMillionsGridsCount), "Le nombre de grilles EuroMillions doit être compris entre 0 et 100.");
        }

        if (requireAtLeastOneGrid && lotoGridsCount == 0 && euroMillionsGridsCount == 0)
        {
            throw new ArgumentException("Au moins une grille doit être demandée.");
        }
    }

    private static string NormalizeAndValidateEmail(string rawEmail)
    {
        var normalized = rawEmail.Trim().ToLowerInvariant();
        if (!MailAddress.TryCreate(normalized, out _))
        {
            throw new ArgumentException("Adresse e-mail invalide.", nameof(rawEmail));
        }

        return normalized;
    }

    private static string GenerateToken() =>
        ToBase64Url(RandomNumberGenerator.GetBytes(32));

    private static string BuildTokenizedLink(string publicBaseUrl, string path, string token)
    {
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{publicBaseUrl}{normalizedPath}?token={Uri.EscapeDataString(token)}";
    }

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
