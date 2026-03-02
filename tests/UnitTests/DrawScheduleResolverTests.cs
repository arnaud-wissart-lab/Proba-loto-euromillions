using Domain.Enums;

namespace UnitTests;

public sealed class DrawScheduleResolverTests
{
    private readonly Infrastructure.Services.DrawScheduleResolver _resolver = new();

    [Theory]
    [InlineData(LotteryGame.EuroMillions, 2026, 3, 3, true)]   // mardi
    [InlineData(LotteryGame.EuroMillions, 2026, 3, 6, true)]   // vendredi
    [InlineData(LotteryGame.EuroMillions, 2026, 3, 4, false)]  // mercredi
    [InlineData(LotteryGame.Loto, 2026, 3, 2, true)]           // lundi
    [InlineData(LotteryGame.Loto, 2026, 3, 4, true)]           // mercredi
    [InlineData(LotteryGame.Loto, 2026, 3, 7, true)]           // samedi
    [InlineData(LotteryGame.Loto, 2026, 3, 6, false)]          // vendredi
    public void IsDrawDayShouldMatchExpectedRules(LotteryGame game, int year, int month, int day, bool expected)
    {
        var date = new DateOnly(year, month, day);

        var result = _resolver.IsDrawDay(game, date);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNextDrawDayShouldReturnSameDayWhenAlreadyDrawDay()
    {
        var drawDay = new DateOnly(2026, 3, 3); // mardi

        var result = _resolver.GetNextDrawDay(LotteryGame.EuroMillions, drawDay);

        Assert.Equal(drawDay, result);
    }

    [Fact]
    public void GetNextDrawDayShouldReturnFollowingDrawDate()
    {
        var nonDrawDay = new DateOnly(2026, 3, 5); // jeudi

        var euroMillionsResult = _resolver.GetNextDrawDay(LotteryGame.EuroMillions, nonDrawDay);
        var lotoResult = _resolver.GetNextDrawDay(LotteryGame.Loto, nonDrawDay);

        Assert.Equal(new DateOnly(2026, 3, 6), euroMillionsResult); // vendredi
        Assert.Equal(new DateOnly(2026, 3, 7), lotoResult); // samedi
    }
}
