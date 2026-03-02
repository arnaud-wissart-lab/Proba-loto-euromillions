using Application.Abstractions;
using Domain.Enums;

namespace Infrastructure.Services;

public sealed class DrawScheduleResolver : IDrawScheduleResolver
{
    public bool IsDrawDay(LotteryGame game, DateOnly localDate)
    {
        var day = localDate.DayOfWeek;

        return game switch
        {
            LotteryGame.EuroMillions => day is DayOfWeek.Tuesday or DayOfWeek.Friday,
            LotteryGame.Loto => day is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Saturday,
            _ => false
        };
    }

    public DateOnly GetNextDrawDay(LotteryGame game, DateOnly localDate)
    {
        var date = localDate;

        while (!IsDrawDay(game, date))
        {
            date = date.AddDays(1);
        }

        return date;
    }
}
