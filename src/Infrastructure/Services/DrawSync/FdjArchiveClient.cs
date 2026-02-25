using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Domain.Enums;
using HtmlAgilityPack;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.DrawSync;

public sealed partial class FdjArchiveClient(
    IHttpClientFactory httpClientFactory,
    IOptions<DrawSyncOptions> options,
    ILogger<FdjArchiveClient> logger) : IFdjArchiveClient
{
    private static readonly IReadOnlyDictionary<string, int> FrenchMonthByName = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["janvier"] = 1,
        ["janv"] = 1,
        ["fevrier"] = 2,
        ["fev"] = 2,
        ["mars"] = 3,
        ["avril"] = 4,
        ["avr"] = 4,
        ["mai"] = 5,
        ["juin"] = 6,
        ["juillet"] = 7,
        ["juil"] = 7,
        ["aout"] = 8,
        ["septembre"] = 9,
        ["sept"] = 9,
        ["octobre"] = 10,
        ["oct"] = 10,
        ["novembre"] = 11,
        ["nov"] = 11,
        ["decembre"] = 12,
        ["dec"] = 12
    };

    public async Task<ArchiveDiscoveryResult> DiscoverArchivesAsync(
        LotteryGame game,
        ArchiveDiscoveryCache? cache,
        CancellationToken cancellationToken)
    {
        var gameOptions = options.Value.GetGameOptions(game);
        if (!Uri.TryCreate(gameOptions.HistoryUrl, UriKind.Absolute, out var sourcePageUrl))
        {
            throw new InvalidOperationException($"URL historique invalide pour {game}: {gameOptions.HistoryUrl}");
        }

        var client = httpClientFactory.CreateClient(HttpClientNames.FdjArchive);

        using var response = await RequestHistoryPageAsync(client, sourcePageUrl, cache, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            var cachedArchives = cache?.CachedArchives?.ToArray() ?? [];
            if (cachedArchives.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Reponse 304 pour {game} sans cache d'archives disponible.");
            }

            logger.LogInformation(
                "Page historique non modifiee pour {Game} (cache reutilise, count={ArchiveCount}).",
                game,
                cachedArchives.Length);

            var etag = response.Headers.ETag?.Tag ?? cache?.ETag;
            var lastModified = ExtractLastModified(response) ?? cache?.LastModifiedUtc;
            return new ArchiveDiscoveryResult(cachedArchives, etag, lastModified, IsFromCache: true);
        }

        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var anchors = document.DocumentNode.SelectNodes("//a[@href]")?.ToArray() ?? [];
        var discovered = new List<FdjArchiveDescriptor>();

        foreach (var anchor in anchors)
        {
            var href = anchor.GetAttributeValue("href", string.Empty)?.Trim();
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            if (!Uri.TryCreate(sourcePageUrl, href, out var absoluteUrl))
            {
                continue;
            }

            if (!LooksLikeArchiveUrl(absoluteUrl))
            {
                continue;
            }

            var title = anchor.GetAttributeValue("title", string.Empty);
            var ariaLabel = anchor.GetAttributeValue("aria-label", string.Empty);
            var download = anchor.GetAttributeValue("download", string.Empty);
            var innerText = HtmlEntity.DeEntitize(anchor.InnerText);
            var combinedLabel = NormalizeWhitespace($"{title} {ariaLabel} {innerText}".Trim());

            if (!LooksLikeArchiveLabel(combinedLabel, absoluteUrl, game, download))
            {
                continue;
            }

            _ = TryExtractPeriod($"{combinedLabel} {absoluteUrl.AbsoluteUri}", out var periodStart, out var periodEnd);

            discovered.Add(new FdjArchiveDescriptor(
                absoluteUrl,
                sourcePageUrl,
                combinedLabel,
                periodStart,
                periodEnd));
        }

        var uniqueArchives = discovered
            .GroupBy(item => item.DownloadUrl.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.PeriodStart ?? DateOnly.MinValue)
            .ToArray();

        var filteredArchives = uniqueArchives
            .Where(item => item.PeriodEnd is null || item.PeriodEnd >= gameOptions.RuleStartDate)
            .ToArray();

        var archivesToUse = filteredArchives.Length > 0 ? filteredArchives : uniqueArchives;

        var discoveredEtag = response.Headers.ETag?.Tag;
        var discoveredLastModified = ExtractLastModified(response);

        logger.LogInformation(
            "Archives FDJ decouvertes pour {Game}: total={TotalCount}, retenues={SelectedCount}, ruleStartDate={RuleStartDate}, etag={ETag}, lastModified={LastModified}",
            game,
            uniqueArchives.Length,
            archivesToUse.Length,
            gameOptions.RuleStartDate,
            discoveredEtag,
            discoveredLastModified);

        return new ArchiveDiscoveryResult(archivesToUse, discoveredEtag, discoveredLastModified, IsFromCache: false);
    }

    public async Task<byte[]> DownloadArchiveAsync(Uri archiveUrl, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientNames.FdjArchive);
        using var response = await client.GetAsync(archiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static bool LooksLikeArchiveUrl(Uri url)
    {
        var normalized = NormalizeForMatching(url.AbsoluteUri);
        var hasArchiveHint = normalized.Contains(".zip", StringComparison.Ordinal)
                             || normalized.Contains("documentations", StringComparison.Ordinal)
                             || normalized.Contains("historique", StringComparison.Ordinal)
                             || normalized.Contains("archive", StringComparison.Ordinal);

        var hasDrawHint = normalized.Contains("service-draw-info", StringComparison.Ordinal)
                          || normalized.Contains("tirage", StringComparison.Ordinal)
                          || normalized.Contains("draw", StringComparison.Ordinal);

        return hasArchiveHint && hasDrawHint;
    }

    private static bool LooksLikeArchiveLabel(string label, Uri url, LotteryGame game, string downloadAttribute)
    {
        var normalizedLabel = NormalizeForMatching(label);
        var normalizedDownload = NormalizeForMatching(downloadAttribute);
        var normalizedUrl = NormalizeForMatching(url.AbsolutePath);

        var hintsArchive = normalizedLabel.Contains("historique", StringComparison.Ordinal)
                           || normalizedLabel.Contains("telecharger", StringComparison.Ordinal)
                           || normalizedLabel.Contains("archive", StringComparison.Ordinal)
                           || normalizedDownload.Length > 0
                           || normalizedUrl.Contains(".zip", StringComparison.Ordinal)
                           || normalizedUrl.Contains("historique", StringComparison.Ordinal)
                           || normalizedUrl.Contains("archive", StringComparison.Ordinal);

        if (!hintsArchive)
        {
            return false;
        }

        return game switch
        {
            LotteryGame.Loto =>
                (normalizedLabel.Contains("loto", StringComparison.Ordinal)
                 || normalizedDownload.Contains("loto", StringComparison.Ordinal)
                 || normalizedUrl.Contains("loto", StringComparison.Ordinal))
                && !normalizedLabel.Contains("grand loto", StringComparison.Ordinal)
                && !normalizedLabel.Contains("super loto", StringComparison.Ordinal)
                && !normalizedDownload.Contains("grandloto", StringComparison.Ordinal)
                && !normalizedDownload.Contains("superloto", StringComparison.Ordinal)
                && !normalizedUrl.Contains("grandloto", StringComparison.Ordinal)
                && !normalizedUrl.Contains("superloto", StringComparison.Ordinal),
            LotteryGame.EuroMillions =>
                normalizedLabel.Contains("euromillion", StringComparison.Ordinal)
                || normalizedDownload.Contains("euromillion", StringComparison.Ordinal)
                || normalizedUrl.Contains("euromillion", StringComparison.Ordinal),
            _ => false
        };
    }

    private async Task<HttpResponseMessage> RequestHistoryPageAsync(
        HttpClient client,
        Uri sourcePageUrl,
        ArchiveDiscoveryCache? cache,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, sourcePageUrl);
        ApplyConditionalHeaders(request, cache);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotModified)
        {
            return response;
        }

        if (cache?.CachedArchives.Count > 0)
        {
            return response;
        }

        response.Dispose();
        logger.LogWarning(
            "Page historique renvoie 304 pour {SourcePageUrl} sans cache local. Nouvelle requête sans condition.",
            sourcePageUrl);
        return await client.GetAsync(sourcePageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static void ApplyConditionalHeaders(HttpRequestMessage request, ArchiveDiscoveryCache? cache)
    {
        if (cache is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(cache.ETag)
            && EntityTagHeaderValue.TryParse(cache.ETag, out var entityTag))
        {
            request.Headers.IfNoneMatch.Add(entityTag);
        }

        if (cache.LastModifiedUtc is not null)
        {
            request.Headers.IfModifiedSince = cache.LastModifiedUtc;
        }
    }

    private static DateTimeOffset? ExtractLastModified(HttpResponseMessage response)
    {
        if (response.Content.Headers.LastModified is { } contentLastModified)
        {
            return contentLastModified;
        }

        if (!response.Headers.TryGetValues("Last-Modified", out var values))
        {
            return null;
        }

        var rawValue = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return DateTimeOffset.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryExtractPeriod(string label, out DateOnly? periodStart, out DateOnly? periodEnd)
    {
        periodStart = null;
        periodEnd = null;

        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        var dateMatches = FullDateRegex().Matches(label);
        if (dateMatches.Count >= 2
            && TryParseFrenchDate(dateMatches[0].Value, out var startDate)
            && TryParseFrenchDate(dateMatches[^1].Value, out var endDate))
        {
            periodStart = DateOnly.FromDateTime(startDate);
            periodEnd = DateOnly.FromDateTime(endDate);
            return true;
        }

        var normalized = NormalizeForMatching(label);
        var monthMatch = MonthRangeRegex().Match(normalized);
        if (!monthMatch.Success)
        {
            return false;
        }

        var startMonthName = monthMatch.Groups["startMonth"].Value;
        var endMonthName = monthMatch.Groups["endMonth"].Value;
        if (!FrenchMonthByName.TryGetValue(startMonthName, out var startMonth)
            || !FrenchMonthByName.TryGetValue(endMonthName, out var endMonth))
        {
            return false;
        }

        if (!int.TryParse(monthMatch.Groups["startYear"].Value, out var startYear)
            || !int.TryParse(monthMatch.Groups["endYear"].Value, out var endYear))
        {
            return false;
        }

        var start = new DateOnly(startYear, startMonth, 1);
        var end = new DateOnly(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));
        periodStart = start;
        periodEnd = end;

        return true;
    }

    private static bool TryParseFrenchDate(string value, out DateTime dateTime) =>
        DateTime.TryParseExact(
            value,
            ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy"],
            CultureInfo.GetCultureInfo("fr-FR"),
            DateTimeStyles.AssumeLocal,
            out dateTime);

    private static string NormalizeWhitespace(string value) =>
        MultipleWhitespaceRegex().Replace(value, " ").Trim();

    private static string NormalizeForMatching(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return NormalizeWhitespace(builder.ToString().Normalize(NormalizationForm.FormC));
    }

    [GeneratedRegex(@"\b\d{1,2}[/-]\d{1,2}[/-]\d{4}\b", RegexOptions.CultureInvariant)]
    private static partial Regex FullDateRegex();

    [GeneratedRegex(@"(?<startMonth>[a-z]+)\s+(?<startYear>\d{4})\s+a\s+(?<endMonth>[a-z]+)\s+(?<endYear>\d{4})", RegexOptions.CultureInvariant)]
    private static partial Regex MonthRangeRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleWhitespaceRegex();
}
