namespace Application.Models;

public sealed record NewsletterDispatchSummaryDto(
    DateOnly LocalDate,
    string TimeZone,
    bool Force,
    bool IsScheduleWindowOpen,
    int TotalSubscribersConsidered,
    int SentCount,
    int SkippedCount,
    int ErrorCount,
    IReadOnlyCollection<string> DispatchedGames);
