using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ProjectMonitoring.Application.Features.Reports;

/// <summary>Writes RFC 4180 CSV with a UTF-8 BOM, so Excel shows Thai text correctly.</summary>
public static class CsvWriter
{
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] QuotedCharacters = [',', '"', '\r', '\n'];

    public static byte[] Write<T>(IEnumerable<T> rows)
    {
        var properties = typeof(T).GetProperties();
        var csv = new StringBuilder();
        AppendLine(csv, properties.Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)));
        foreach (var row in rows)
        {
            AppendLine(csv, properties.Select(property => FormatCell(property.GetValue(row))));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static void AppendLine(StringBuilder csv, IEnumerable<string> cells) =>
        csv.Append(string.Join(',', cells)).Append("\r\n");

    private static string FormatCell(object? value) => value switch
    {
        null => "",
        string text => Quote(Neutralize(text)),
        bool flag => flag ? "true" : "false",
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(Neutralize(value.ToString() ?? "")),
    };

    // Stops spreadsheets from running user-entered text as a formula.
    private static string Neutralize(string text) =>
        text.Length > 0 && FormulaPrefixes.Contains(text[0]) ? "'" + text : text;

    private static string Quote(string text) =>
        text.IndexOfAny(QuotedCharacters) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
}
