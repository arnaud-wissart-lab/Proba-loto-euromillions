using Domain.Enums;
using Domain.Services;

namespace UnitTests;

public sealed class LotteryGameRulesCatalogTests
{
    [Fact]
    public void LotoTotalCombinationsShouldMatchReferenceValue()
    {
        var rules = LotteryGameRulesCatalog.GetRules(LotteryGame.Loto);

        Assert.Equal(19_068_840, rules.TotalCombinations);
    }

    [Fact]
    public void EuroMillionsTotalCombinationsShouldMatchReferenceValue()
    {
        var rules = LotteryGameRulesCatalog.GetRules(LotteryGame.EuroMillions);

        Assert.Equal(139_838_160, rules.TotalCombinations);
    }

    [Fact]
    public void GetNextDrawDateShouldFollowCalendarRules()
    {
        var thursday = new DateOnly(2026, 2, 19);

        var nextLoto = LotteryGameRulesCatalog.GetNextDrawDate(LotteryGame.Loto, thursday);
        var nextEuroMillions = LotteryGameRulesCatalog.GetNextDrawDate(LotteryGame.EuroMillions, thursday);

        Assert.Equal(new DateOnly(2026, 2, 21), nextLoto);
        Assert.Equal(new DateOnly(2026, 2, 20), nextEuroMillions);
    }

    [Theory]
    [InlineData("Loto", LotteryGame.Loto)]
    [InlineData("loto", LotteryGame.Loto)]
    [InlineData("EuroMillions", LotteryGame.EuroMillions)]
    [InlineData("euro-millions", LotteryGame.EuroMillions)]
    public void TryParseGameShouldParseKnownValues(string rawValue, LotteryGame expected)
    {
        var parsed = LotteryGameRulesCatalog.TryParseGame(rawValue, out var game);

        Assert.True(parsed);
        Assert.Equal(expected, game);
    }
}
