namespace Application.Models;

public sealed record SubscriptionDispatchSummaryDto(
    DateOnly ReferenceDate,
    int ActiveSubscriptions,
    int SentCount,
    int FailedCount,
    int SkippedCount);
