using System.Text;
using Domain.Enums;
using Infrastructure.Services.DrawSync;
using Microsoft.Extensions.Logging.Abstractions;

namespace UnitTests;

public sealed class FdjDrawFileParserTests
{
    [Fact]
    public void ParseArchiveEntryShouldHandleAlternativeLotoHeaders()
    {
        const string csv = """
date_tirage;numero1;numero2;numero3;numero4;numero5;num_chance
19/02/2026;1;2;3;4;5;9
""";

        var parser = CreateParser();

        var draws = parser.ParseArchiveEntry(
            LotteryGame.Loto,
            "archive.zip",
            "loto.csv",
            Encoding.UTF8.GetBytes(csv));

        var draw = Assert.Single(draws);
        Assert.Equal(new DateOnly(2026, 2, 19), draw.DrawDate);
        Assert.Equal([1, 2, 3, 4, 5], draw.MainNumbers);
        Assert.Equal([9], draw.BonusNumbers);
    }

    [Fact]
    public void ParseArchiveEntryShouldHandleCombinedEuroMillionsColumns()
    {
        const string csv = """
Date du tirage,numeros gagnants,stars gagnantes
20/02/2026,1 2 3 4 5,1 7
""";

        var parser = CreateParser();

        var draws = parser.ParseArchiveEntry(
            LotteryGame.EuroMillions,
            "archive.zip",
            "euromillions.csv",
            Encoding.UTF8.GetBytes(csv));

        var draw = Assert.Single(draws);
        Assert.Equal(new DateOnly(2026, 2, 20), draw.DrawDate);
        Assert.Equal([1, 2, 3, 4, 5], draw.MainNumbers);
        Assert.Equal([1, 7], draw.BonusNumbers);
    }

    [Fact]
    public void ParseArchiveEntryShouldSkipInvalidRowsAndKeepValidRows()
    {
        const string csv = """
date_de_tirage;boule_1;boule_2;boule_3;boule_4;boule_5;numero_chance
20/02/2026;1;2;3;4;5;3
21/02/2026;1;2;2;4;5;3
""";

        var parser = CreateParser();

        var draws = parser.ParseArchiveEntry(
            LotteryGame.Loto,
            "archive.zip",
            "loto.csv",
            Encoding.UTF8.GetBytes(csv));

        var draw = Assert.Single(draws);
        Assert.Equal(new DateOnly(2026, 2, 20), draw.DrawDate);
    }

    private static FdjDrawFileParser CreateParser() =>
        new(NullLogger<FdjDrawFileParser>.Instance);
}
