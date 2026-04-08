using System;
using System.IO.Compression;
using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Models;
using static MAVLink;

namespace APConfigManager.Infrastructure.Parsers
{
    /// <summary>
    /// Parses APJ firmware files.
    /// </summary>
    public class ApjFirmwareParser : IFirmwareParser
    {
        private const string ExpectedMagic = "APJFWv1";


        public static FirmwarePackage Parse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new ApjParseException($"File {filePath} doesn't exists");
            }

            string json;

            try
            {
                json = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                throw new ApjParseException($"File read error: {ex.Message}", ex);
            }

            JsonNode root;
            try
            {
                root = JsonNode.Parse(json) ?? throw new ApjParseException("File is empty or not JSON");
            }
            catch (Exception ex)
            {
                throw new ApjParseException($"JSON isn't valid: {ex.Message}", ex);
            }

            // Magic checking
            var magic = root["magic"]?.GetValue<string>()
                ?? throw new ApjParseException($"The field 'magic' is missing");

            if ( magic != ExpectedMagic )
            {
                throw new ApjParseException($"Unexpected 'magic': '{magic}'. Expected: {ExpectedMagic}");
            }

            // Required fields
            uint boardId = root["board_id"]?.GetValue<uint>()
                ?? throw new ApjParseException("The field 'board_id' is missing");

            int imageSize = root["image_size"]?.GetValue<int>()
                ?? throw new ApjParseException("The field 'image_size' is missing");

            string imageByte64 = root["image"]?.GetValue<string>()
                ?? throw new ApjParseException("The field 'image' is missing");

            // Optional fields
            int boardRevision = root["board_revision"]?.GetValue<int>() ?? 0;
            string description = root["description"]?.GetValue<string>() ?? string.Empty;
            string version = root["version"]?.GetValue<string>() ?? string.Empty;

            // Image decoding.
            byte[] imageBytes = DecodeBase64(imageByte64, "image");

            string gitIdentity = root["git_identity"]?.GetValue<string>() ?? string.Empty;

            // External flash (optional).
            byte[]? extfBytes = null;
            var extfNode = root["extf_image"];
            if (extfNode is not null && extfNode.GetValueKind() == JsonValueKind.String)
            {
                string extfBase64 = extfNode.GetValue<string>();
                if (!string.IsNullOrEmpty(extfBase64))
                    extfBytes = DecodeBase64(extfBase64, "extf_image");
            }

            return new FirmwarePackage
            {
                Magic = magic,
                BoardId = boardId,
                FlashSize = imageSize,
                ImageBytes = imageBytes,
                BoardRevision = boardRevision,
                Version = version,
                Description = description,
                ExtfImageBytes = extfBytes,
                GitIdentity = gitIdentity
            };
        }

        /// <summary>
        /// Decodes base64 string and decompresses zlib data.
        /// </summary>
        private static byte[] DecodeBase64(string base64, string fileName)
        {
            byte[] compressed;

            try
            {
                compressed = Convert.FromBase64String(base64);
            }
            catch (FormatException ex)
            {
                throw new ApjParseException($"Field '{fileName}' doesn't include valid base64: {ex.Message}, ex");

            }

            try
            {
                using var input = new MemoryStream(compressed, 2, compressed.Length - 2); // skip the 2-byte header before deflate.
                using var deflate = new DeflateStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                deflate.CopyTo(output);
                return output.ToArray();
            }
            catch (Exception ex)
            {
                throw new ApjParseException($"Error during unzip '{fileName}': {ex.Message}", ex);
            }
        }
    }
}
