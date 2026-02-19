using System.Globalization;
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
        ["fevrier"] = 2,
        ["mars"] = 3,
        ["avril"] = 4,
        ["mai"] = 5,
        ["juin"] = 6,
        ["juillet"] = 7,
        ["aout"] = 8,
        ["septembre"] = 9,
        ["octobre"] = 10,
        ["novembre"] = 11,
        ["decembre"] = 12
    };

    public async Task<IReadOnlyCollection<FdjArchiveDescriptor>> DiscoverArchivesAsync(LotteryGame game, CancellationToken cancellationToken)
    {
        var gameOptions = options.Value.GetGameOptions(game);
        if (!Uri.TryCreate(gameOptions.HistoryUrl, UriKind.Absolute, out var sourcePageUrl))
        {
            throw new InvalidOperationException($"URL historique invalide pour {game}: {gameOptions.HistoryUrl}");
        }

        var client = httpClientFactory.CreateClient(HttpClientNames.FdjArchive);
        var html = await client.GetStringAsync(sourcePageUrl, cancellationToken);
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

            if (!LooksLikeArchiveLabel(combinedLabel, game, download))
            {
                continue;
            }

            _ = TryExtractPeriod(combinedLabel, out var periodStart, out var periodEnd);

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

        logger.LogInformation(
            "Archives FDJ decouvertes pour {Game}: total={TotalCount}, retenues={SelectedCount}, ruleStartDate={RuleStartDate}",
            game,
            uniqueArchives.Length,
            archivesToUse.Length,
            gameOptions.RuleStartDate);

        return archivesToUse;
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
        var uri = url.AbsoluteUri;
        return uri.Contains("service-draw-info", StringComparison.OrdinalIgnoreCase)
               && (uri.Contains("/documentations/", StringComparison.OrdinalIgnoreCase)
                   || uri.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeArchiveLabel(string label, LotteryGame game, string downloadAttribute)
    {
        var normalizedLabel = NormalizeForMatching(label);
        var normalizedDownload = NormalizeForMatching(downloadAttribute);

        var hintsArchive = normalizedLabel.Contains("historique", StringComparison.Ordinal)
                           || normalizedLabel.Contains("telecharger", StringComparison.Ordinal)
                           || normalizedDownload.Length > 0;

        if (!hintsArchive)
        {
            return false;
        }

        return game switch
        {
            LotteryGame.Loto =>
                (normalizedLabel.Contains("historique loto", StringComparison.Ordinal)
                 || normalizedDownload.StartsWith("loto", StringComparison.Ordinal))
                && !normalizedLabel.Contains("grand loto", StringComparison.Ordinal)
                && !normalizedLabel.Contains("super loto", StringComparison.Ordinal)
                && !normalizedDownload.StartsWith("grandloto", StringComparison.Ordinal)
                && !normalizedDownload.StartsWith("superloto", StringComparison.Ordinal),
            LotteryGame.EuroMillions =>
                normalizedLabel.Contains("historique euromillions", StringComparison.Ordinal)
                || normalizedDownload.StartsWith("euromillions", StringComparison.Ordinal),
            _ => false
        };
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
