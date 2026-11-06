using System.Globalization;
using System.Text;
using ProjectMonitoring.Application.Features.Reports;

namespace ProjectMonitoring.UnitTests;

public class CsvWriterTests
{
    private sealed record Row(string? Name, decimal? Amount, DateOnly? Day, bool Flag, long Count);

    private static string Csv(params Row[] rows)
    {
        var bytes = CsvWriter.Write(rows);
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    [Fact]
    public void Starts_with_bom_and_camel_case_header()
    {
        var bytes = CsvWriter.Write(Array.Empty<Row>());
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("name,amount,day,flag,count\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public void Writes_thai_text_and_empty_nulls()
    {
        var line = Csv(new Row("ภาษาไทย", null, null, true, 3)).Split("\r\n")[1];
        Assert.Equal("ภาษาไทย,,,true,3", line);
    }

    [Theory]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    public void Quotes_special_characters(string input, string expected) =>
        Assert.StartsWith(expected + ",", Csv(new Row(input, null, null, false, 0)).Split("\r\n")[1], StringComparison.Ordinal);

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@SUM(A1)")]
    [InlineData("\tx")]
    public void Neutralizes_formulas(string input) =>
        Assert.StartsWith("'" + input, Csv(new Row(input, null, null, false, 0)).Split("\r\n")[1], StringComparison.Ordinal);

    [Fact]
    public void Numbers_and_dates_ignore_the_machine_locale()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // th-TH defaults to the Buddhist calendar; de-DE uses a decimal comma.
            foreach (var culture in new[] { "th-TH", "de-DE" })
            {
                CultureInfo.CurrentCulture = new CultureInfo(culture);
                var line = Csv(new Row("x", 1234.50m, new DateOnly(2026, 11, 6), false, 1)).Split("\r\n")[1];
                Assert.Equal("x,1234.50,2026-11-06,false,1", line);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
