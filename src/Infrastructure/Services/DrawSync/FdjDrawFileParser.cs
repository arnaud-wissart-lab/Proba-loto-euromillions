using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Domain.Enums;
using ExcelDataReader;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace Infrastructure.Services.DrawSync;

public sealed partial class FdjDrawFileParser(ILogger<FdjDrawFileParser> logger) : IFdjDrawFileParser
{
    private static readonly Lazy<bool> EncodingProviderRegistration = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return true;
    });

    public IReadOnlyCollection<ParsedDraw> ParseArchiveEntry(
        LotteryGame game,
        string archiveName,
        string entryName,
        byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _ = EncodingProviderRegistration.Value;

        if (TryParseCsv(game, entryName, content, out var csvDraws))
        {
            logger.LogInformation(
                "Parsing CSV reussi pour {Game} ({ArchiveName}/{EntryName}): {DrawCount} tirages.",
                game,
                archiveName,
                entryName,
                csvDraws.Count);

            return csvDraws;
        }

        if (TryParseExcel(game, entryName, content, out var excelDraws))
        {
            logger.LogInformation(
                "Parsing Excel reussi pour {Game} ({ArchiveName}/{EntryName}): {DrawCount} tirages.",
                game,
                archiveName,
                entryName,
                excelDraws.Count);

            return excelDraws;
        }

        logger.LogWarning(
            "Fichier ignore pour {Game} ({ArchiveName}/{EntryName}): format non interpretable (CSV puis Excel).",
            game,
            archiveName,
            entryName);

        return [];
    }

    private bool TryParseCsv(LotteryGame game, string entryName, byte[] content, out List<ParsedDraw> draws)
    {
        draws = [];
        foreach (var encoding in CsvEncodings())
        {
            string text;
            try
            {
                text = encoding.GetString(content);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            foreach (var delimiter in CsvDelimiters())
            {
                if (TryParseCsvWithDelimiter(game, entryName, text, delimiter, out draws) && draws.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryParseCsvWithDelimiter(
        LotteryGame game,
        string entryName,
        string text,
        string delimiter,
        out List<ParsedDraw> draws)
    {
        draws = [];

        using var reader = new StringReader(text);
        using var parser = new TextFieldParser(reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true
        };
        parser.SetDelimiters(delimiter);

        if (parser.EndOfData)
        {
            return false;
        }

        var headers = parser.ReadFields();
        if (headers is null || headers.Length == 0)
        {
            return false;
        }

        var columns = BuildColumnIndex(headers);
        if (!HasMinimumColumns(game, columns))
        {
            return false;
        }

        var rowIndex = 1;
        while (!parser.EndOfData)
        {
            string[]? fields;
            try
            {
                fields = parser.ReadFields();
            }
            catch (MalformedLineException exception)
            {
                logger.LogWarning(
                    exception,
                    "Ligne CSV invalide ignoree ({EntryName}, ligne={RowIndex}).",
                    entryName,
                    rowIndex);
                rowIndex++;
                continue;
            }

            rowIndex++;
            if (fields is null || fields.Length == 0)
            {
                continue;
            }

            if (TryMapDraw(game, columns, fields, out var draw, out var error))
            {
                draws.Add(draw);
            }
            else
            {
                logger.LogWarning(
                    "Ligne CSV ignoree ({EntryName}, ligne={RowIndex}): {Reason}",
                    entryName,
                    rowIndex,
                    error);
            }
        }

        return true;
    }

    private bool TryParseExcel(LotteryGame game, string entryName, byte[] content, out List<ParsedDraw> draws)
    {
        _ = EncodingProviderRegistration.Value;
        draws = [];

        try
        {
            using var stream = new MemoryStream(content, writable: false);
            using var excelReader = ExcelReaderFactory.CreateReader(stream);
            var dataSet = excelReader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            foreach (DataTable table in dataSet.Tables)
            {
                var headers = table.Columns
                    .Cast<DataColumn>()
                    .Select(column => column.ColumnName)
                    .ToArray();

                if (headers.Length == 0)
                {
                    continue;
                }

                var columns = BuildColumnIndex(headers);
                if (!HasMinimumColumns(game, columns))
                {
                    continue;
                }

                var rowIndex = 1;
                foreach (DataRow row in table.Rows)
                {
                    rowIndex++;
                    var fields = row.ItemArray
                        .Select(value => value?.ToString() ?? string.Empty)
                        .ToArray();

                    if (TryMapDraw(game, columns, fields, out var draw, out var error))
                    {
                        draws.Add(draw);
                    }
                    else
                    {
                        logger.LogWarning(
                            "Ligne Excel ignoree ({EntryName}, table={TableName}, ligne={RowIndex}): {Reason}",
                            entryName,
                            table.TableName,
                            rowIndex,
                            error);
                    }
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or NotSupportedException)
        {
            logger.LogWarning(
                exception,
                "Echec parsing Excel pour {EntryName}.",
                entryName);
            return false;
        }

        return draws.Count > 0;
    }

    private static bool HasMinimumColumns(LotteryGame game, IReadOnlyDictionary<string, int> columns) =>
        game switch
        {
            LotteryGame.Loto => columns.ContainsKey("date_de_tirage")
                                && (columns.ContainsKey("boule_1")
                                    || columns.ContainsKey("combinaison_gagnante_en_ordre_croissant")),
            LotteryGame.EuroMillions => columns.ContainsKey("date_de_tirage")
                                        && (columns.ContainsKey("boule_1")
                                            || columns.ContainsKey("boules_gagnantes_en_ordre_croissant")),
            _ => false
        };

    private static Dictionary<string, int> BuildColumnIndex(IReadOnlyList<string> headers)
    {
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < headers.Count; index++)
        {
            var normalized = NormalizeHeader(headers[index]);
            if (!string.IsNullOrWhiteSpace(normalized) && !columns.ContainsKey(normalized))
            {
                columns.Add(normalized, index);
            }
        }

        return columns;
    }

    private static string NormalizeHeader(string value)
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

            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append('_');
            }
        }

        var compact = MultipleUnderscoreRegex().Replace(builder.ToString(), "_").Trim('_');
        return compact;
    }

    private static IEnumerable<Encoding> CsvEncodings()
    {
        yield return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        yield return Encoding.GetEncoding("windows-1252");
        yield return Encoding.Latin1;
    }

    private static IEnumerable<string> CsvDelimiters()
    {
        yield return ";";
        yield return ",";
        yield return "\t";
    }

    private static bool TryMapDraw(
        LotteryGame game,
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<string> fields,
        out ParsedDraw draw,
        out string error)
    {
        draw = default!;
        error = string.Empty;

        if (!TryGetDate(columns, fields, out var drawDate))
        {
            error = "date_de_tirage invalide ou absente";
            return false;
        }

        if (!TryGetMainNumbers(columns, fields, out var mainNumbers))
        {
            error = "boules principales invalides";
            return false;
        }

        if (!TryGetBonusNumbers(game, columns, fields, out var bonusNumbers))
        {
            error = "boules bonus invalides";
            return false;
        }

        mainNumbers = mainNumbers.OrderBy(number => number).ToArray();
        bonusNumbers = bonusNumbers.OrderBy(number => number).ToArray();

        if (!IsValid(game, mainNumbers, bonusNumbers, out var validationError))
        {
            error = validationError;
            return false;
        }

        draw = new ParsedDraw(drawDate, mainNumbers, bonusNumbers);
        return true;
    }

    private static bool TryGetDate(
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<string> fields,
        out DateOnly drawDate)
    {
        drawDate = default;
        if (!TryGetColumnValue(columns, fields, "date_de_tirage", out var rawDate)
            || string.IsNullOrWhiteSpace(rawDate))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            rawDate.Trim(),
            ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy"],
            CultureInfo.GetCultureInfo("fr-FR"),
            DateTimeStyles.None,
            out drawDate);
    }

    private static bool TryGetMainNumbers(
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<string> fields,
        out int[] mainNumbers)
    {
        mainNumbers = [];
        var values = new List<int>(5);

        for (var i = 1; i <= 5; i++)
        {
            if (TryGetColumnValue(columns, fields, $"boule_{i}", out var rawValue)
                && TryParseInt(rawValue, out var number))
            {
                values.Add(number);
            }
        }

        if (values.Count == 5)
        {
            mainNumbers = values.ToArray();
            return true;
        }

        var combinedColumns = new[]
        {
            "combinaison_gagnante_en_ordre_croissant",
            "boules_gagnantes_en_ordre_croissant"
        };

        foreach (var column in combinedColumns)
        {
            if (TryGetColumnValue(columns, fields, column, out var combinedValue))
            {
                var extracted = NumberRegex()
                    .Matches(combinedValue)
                    .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
                    .Take(5)
                    .ToArray();

                if (extracted.Length == 5)
                {
                    mainNumbers = extracted;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetBonusNumbers(
        LotteryGame game,
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<string> fields,
        out int[] bonusNumbers)
    {
        bonusNumbers = [];

        if (game == LotteryGame.Loto)
        {
            if (TryGetColumnValue(columns, fields, "numero_chance", out var numeroChance)
                && TryParseInt(numeroChance, out var bonus))
            {
                bonusNumbers = [bonus];
                return true;
            }

            if (TryGetColumnValue(columns, fields, "combinaison_gagnante_en_ordre_croissant", out var combined))
            {
                var extracted = NumberRegex()
                    .Matches(combined)
                    .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
                    .ToArray();
                if (extracted.Length >= 6)
                {
                    bonusNumbers = [extracted[^1]];
                    return true;
                }
            }

            return false;
        }

        if (TryGetColumnValue(columns, fields, "etoile_1", out var etoile1)
            && TryGetColumnValue(columns, fields, "etoile_2", out var etoile2)
            && TryParseInt(etoile1, out var bonus1)
            && TryParseInt(etoile2, out var bonus2))
        {
            bonusNumbers = [bonus1, bonus2];
            return true;
        }

        if (TryGetColumnValue(columns, fields, "etoiles_gagnantes_en_ordre_croissant", out var starsCombined))
        {
            var extracted = NumberRegex()
                .Matches(starsCombined)
                .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
                .Take(2)
                .ToArray();

            if (extracted.Length == 2)
            {
                bonusNumbers = extracted;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetColumnValue(
        IReadOnlyDictionary<string, int> columns,
        IReadOnlyList<string> fields,
        string column,
        out string value)
    {
        value = string.Empty;
        if (!columns.TryGetValue(column, out var index))
        {
            return false;
        }

        if (index < 0 || index >= fields.Count)
        {
            return false;
        }

        value = fields[index].Trim();
        return true;
    }

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
        || int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.GetCultureInfo("fr-FR"), out result);

    private static bool IsValid(
        LotteryGame game,
        int[] mainNumbers,
        int[] bonusNumbers,
        out string validationError)
    {
        validationError = string.Empty;
        if (mainNumbers.Length != 5 || mainNumbers.Distinct().Count() != 5)
        {
            validationError = "les 5 boules principales doivent etre presentes et distinctes";
            return false;
        }

        if (game == LotteryGame.Loto)
        {
            if (mainNumbers.Any(number => number is < 1 or > 49))
            {
                validationError = "boules loto hors plage [1..49]";
                return false;
            }

            if (bonusNumbers.Length != 1 || bonusNumbers[0] is < 1 or > 10)
            {
                validationError = "numero chance hors plage [1..10]";
                return false;
            }

            return true;
        }

        if (mainNumbers.Any(number => number is < 1 or > 50))
        {
            validationError = "boules euro millions hors plage [1..50]";
            return false;
        }

        if (bonusNumbers.Length != 2 || bonusNumbers.Distinct().Count() != 2 || bonusNumbers.Any(number => number is < 1 or > 12))
        {
            validationError = "etoiles hors plage [1..12]";
            return false;
        }

        return true;
    }

    [GeneratedRegex(@"_+", RegexOptions.CultureInvariant)]
    private static partial Regex MultipleUnderscoreRegex();

    [GeneratedRegex(@"\d+", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();
}
