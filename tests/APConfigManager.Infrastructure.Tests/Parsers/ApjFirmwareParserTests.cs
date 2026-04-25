using System.IO.Compression;
using System.Text;
using System.Text.Json;
using APConfigManager.Core.Exceptions;
using APConfigManager.Infrastructure.Parsers;
using FluentAssertions;

namespace APConfigManager.Infrastructure.Tests.Parsers;

public class ApjFirmwareParserTests
{
    private static byte[] CompressZlib(byte[] data)
    {
        using var output = new MemoryStream();

        output.WriteByte(0x78);
        output.WriteByte(0x9C);

        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    private static MemoryStream CreateApjStream(
    uint? boardId = 140,
    string? magic = "APJFWv1",
    byte[]? imageData = null,
    byte[]? extfImageData = null,
    string? version = "4.5.1",
    string? gitIdentity = "abc123def456",
    int? imageSize = null,
    int? boardRevision = 0,
    string? description = "Test firmware")
    {
        imageData ??= new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 };

        var json = new Dictionary<string, object>();

        if (magic is not null)
            json["magic"] = magic;

        if (boardId.HasValue)
            json["board_id"] = boardId.Value;

        if (imageData is not null)
        {
            var compressed = CompressZlib(imageData);
            json["image"] = Convert.ToBase64String(compressed);
            json["image_size"] = imageSize ?? imageData.Length;
        }

        if (extfImageData is not null)
        {
            var compressed = CompressZlib(extfImageData);
            json["extf_image"] = Convert.ToBase64String(compressed);
        }

        if (version is not null)
            json["version"] = version;

        if (gitIdentity is not null)
            json["git_identity"] = gitIdentity;

        if (boardRevision.HasValue)
            json["board_revision"] = boardRevision.Value;

        if (description is not null)
            json["description"] = description;

        var jsonString = JsonSerializer.Serialize(json);
        return new MemoryStream(Encoding.UTF8.GetBytes(jsonString));
    }

    private static MemoryStream CreateRawJsonStream(string json)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(json));
    }

    [Fact]
    public void Parse_ValidApj_ReturnsFirmwarePackage()
    {
        var originalImage = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };
        using var stream = CreateApjStream(
            boardId: 140,
            imageData: originalImage,
            version: "4.5.1",
            gitIdentity: "abc123");

        var result = ApjFirmwareParser.Parse(stream);

        result.BoardId.Should().Be(140);
        result.Version.Should().Be("4.5.1");
        result.GitIdentity.Should().Be("abc123");
        result.ImageBytes.Should().Equal(originalImage);
    }

    [Fact]
    public void Parse_WithExtfImage_DecodesExtfImageBytes()
    {
        var mainImage = new byte[] { 0x01, 0x02, 0x03 };
        var extfImage = new byte[] { 0xF1, 0xF2, 0xF3, 0xF4 };
        using var stream = CreateApjStream(
            imageData: mainImage,
            extfImageData: extfImage);

        var result = ApjFirmwareParser.Parse(stream);

        result.ImageBytes.Should().Equal(mainImage);
        result.ExtfImageBytes.Should().Equal(extfImage);
    }

    [Fact]
    public void Parse_OptionalFieldsMissing_ReturnsPackageWithNulls()
    {
        using var stream = CreateApjStream(
            version: null,
            gitIdentity: null,
            extfImageData: null,
            description: null);

        var result = ApjFirmwareParser.Parse(stream);

        result.BoardId.Should().Be(140);
        result.ImageBytes.Should().NotBeNull();
        result.Version.Should().BeEmpty();
        result.GitIdentity.Should().BeEmpty();
        result.ExtfImageBytes.Should().BeNull();
    }

    [Fact]
    public void Parse_LargeImage_DecompressesCorrectly()
    {
        var largeImage = new byte[10_000];
        new Random(42).NextBytes(largeImage);
        using var stream = CreateApjStream(imageData: largeImage);

        var result = ApjFirmwareParser.Parse(stream);

        result.ImageBytes.Should().Equal(largeImage);
    }

    [Fact]
    public void Parse_MissingBoardId_ThrowsApjParseException()
    {
        using var stream = CreateApjStream(boardId: null);

        var act = () => ApjFirmwareParser.Parse(stream);

        act.Should().Throw<ApjParseException>();
    }

    [Fact]
    public void Parse_MissingImage_ThrowsApjParseException()
    {
        var json = JsonSerializer.Serialize(new
        {
            magic = "APJFWv1",
            board_id = 140,
            image_size = 100
        });
        using var stream = CreateRawJsonStream(json);

        var act = () => ApjFirmwareParser.Parse(stream);

        act.Should().Throw<ApjParseException>();
    }

    [Fact]
    public void Parse_InvalidJson_ThrowsApjParseException()
    {
        using var stream = CreateRawJsonStream("not a json {{{");

        var act = () => ApjFirmwareParser.Parse(stream);

        act.Should().Throw<ApjParseException>();
    }

    [Fact]
    public void Parse_CorruptedBase64_ThrowsApjParseException()
    {
        var json = JsonSerializer.Serialize(new
        {
            magic = "APJFWv1",
            board_id = 140,
            image_size = 100,
            image = "!!!not_base64!!!"
        });
        using var stream = CreateRawJsonStream(json);

        var act = () => ApjFirmwareParser.Parse(stream);

        act.Should().Throw<ApjParseException>();
    }

    [Fact]
    public void Parse_EmptyStream_ThrowsApjParseException()
    {
        using var stream = new MemoryStream();

        var act = () => ApjFirmwareParser.Parse(stream);

        act.Should().Throw<ApjParseException>();
    }
}
