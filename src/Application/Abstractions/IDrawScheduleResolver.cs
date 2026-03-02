using Domain.Enums;

namespace Application.Abstractions;

public interface IDrawScheduleResolver
{
    bool IsDrawDay(LotteryGame game, DateOnly localDate);

    DateOnly GetNextDrawDay(LotteryGame game, DateOnly localDate);
}
