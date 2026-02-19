using Domain.Enums;

namespace Infrastructure.Services.DrawSync;

public interface IFdjDrawFileParser
{
    IReadOnlyCollection<ParsedDraw> ParseArchiveEntry(
        LotteryGame game,
        string archiveName,
        string entryName,
        byte[] content);
}
