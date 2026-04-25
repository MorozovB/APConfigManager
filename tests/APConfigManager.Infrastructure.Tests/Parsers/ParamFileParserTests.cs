using System.Text;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Models;
using APConfigManager.Infrastructure.Parsers;
using FluentAssertions;

namespace APConfigManager.Infrastructure.Tests.Parsers;

public class ParamFileParserTests
{
    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }


    [Fact]
    public void Parse_ValidFile_ReturnsParameters()
    {
        using var stream = CreateStream(
            "ARMING_CHECK,1\n" +
            "BATT_MONITOR,4\n" +
            "SERVO1_MIN,1100\n" +
            "SERVO1_MAX,1900\n" +
            "SERVO1_TRIM,1500.5\n");

        var result = ParamFileParser.Parse(stream);

        result.Should().HaveCount(5);
        result[0].Name.Should().Be("ARMING_CHECK");
        result[0].Value.Should().Be(1f);
        result[4].Name.Should().Be("SERVO1_TRIM");
        result[4].Value.Should().Be(1500.5f);
    }

    [Fact]
    public void Parse_FloatWithDot_ParsesCorrectly()
    {
        using var stream = CreateStream("TRIM_VALUE,1500.75");

        var result = ParamFileParser.Parse(stream);

        result.Should().HaveCount(1);
        result[0].Value.Should().Be(1500.75f);
    }

    [Fact]
    public void Parse_NegativeValue_ParsesCorrectly()
    {
        using var stream = CreateStream("TRIM_VALUE,-150.5");

        var result = ParamFileParser.Parse(stream);

        result.Should().HaveCount(1);
        result[0].Value.Should().Be(-150.5f);
    }

    [Fact]
    public void Parse_FileWithComments_SkipsCommentLines()
    {
        using var stream = CreateStream(
            "# comment\n" +
            "ARMING_CHECK,1\n" +
            "# another comment\n" +
            "BATT_MONITOR,4\n");

        var result = ParamFileParser.Parse(stream);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_FileWithEmptyLines_SkipsEmptyLines()
    {
        using var stream = CreateStream(
            "ARMING_CHECK,1\n" +
            "\n" +
            "\n" +
            "BATT_MONITOR,4\n");

        var result = ParamFileParser.Parse(stream);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Parse_OnlyComments_ReturnsEmptyList()
    {
        using var stream = CreateStream(
            "# comment1\n" +
            "# comment2\n" +
            "# comment3\n");

        var result = ParamFileParser.Parse(stream);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsEmptyList()
    {
        using var stream = new MemoryStream();

        var result = ParamFileParser.Parse(stream);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Parse_ParamNameTooLong_ThrowsParamParseException()
    {
        using var stream = CreateStream("THIS_NAME_IS_WAY_TOO_LONG_FOR_MAVLINK,1");

        var act = () => ParamFileParser.Parse(stream);

        act.Should().Throw<ParamParseException>();
    }

    [Fact]
    public void Parse_InvalidValue_ThrowsParamParseException()
    {
        using var stream = CreateStream("ARMING_CHECK,abc");

        var act = () => ParamFileParser.Parse(stream);

        act.Should().Throw<ParamParseException>();
    }

    [Fact]
    public void Parse_LineWithoutSeparator_ThrowsParamParseException()
    {
        using var stream = CreateStream("ARMING_CHECK");

        var act = () => ParamFileParser.Parse(stream);

        act.Should().Throw<ParamParseException>();
    }
}
