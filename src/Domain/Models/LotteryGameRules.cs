using Domain.Enums;

namespace Domain.Models;

public sealed record LotteryGameRules(
    LotteryGame Game,
    int MainPoolSize,
    int MainNumbersToPick,
    int BonusPoolSize,
    int BonusNumbersToPick,
    IReadOnlySet<DayOfWeek> DrawDays)
{
    public long TotalCombinations =>
        BinomialCoefficient(MainPoolSize, MainNumbersToPick) * BinomialCoefficient(BonusPoolSize, BonusNumbersToPick);

    private static long BinomialCoefficient(int n, int k)
    {
        if (k < 0 || k > n)
        {
            return 0;
        }

        if (k == 0 || k == n)
        {
            return 1;
        }

        var effectiveK = Math.Min(k, n - k);
        long result = 1;

        for (var index = 1; index <= effectiveK; index++)
        {
            result = checked(result * (n - effectiveK + index) / index);
        }

        return result;
    }
}
