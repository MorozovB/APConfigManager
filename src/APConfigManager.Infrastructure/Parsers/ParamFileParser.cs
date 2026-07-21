using APConfigManager.Core.Interfaces.Parsers;
using APConfigManager.Core.Exceptions;
using APConfigManager.Core.Models;

namespace APConfigManager.Infrastructure.Parsers
{
    /// <summary>
    /// Parses .param files (CSV-like format with parameter name and float value).
    /// </summary>
    public class ParamFileParser : IParamFileParser
    {
        // Delegation to static methods.
        List<Parameter> IParamFileParser.Parse(string filePath) => Parse(filePath);
        List<Parameter> IParamFileParser.Parse(Stream stream) => Parse(stream);


        /// <summary>
        /// Parses a .param file from disk.
        /// </summary>
        public static List<Parameter> Parse(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new ParamParseException($"File {filePath} doesn't exist");
            }

            try
            {
                using var stream = File.OpenRead(filePath);
                return Parse(stream);
            }
            catch (Exception ex) when (ex is not ParamParseException)
            {
                throw new ParamParseException($"File read error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Parses parameters from a stream.
        /// </summary>
        public static List<Parameter> Parse(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var result = new List<Parameter>();

            try
            {
                using var reader = new StreamReader(stream, leaveOpen: true);
                string? line;
                int lineNumber = 0;

                while ((line = reader.ReadLine()) is not null)
                {
                    lineNumber++;
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                    {
                        continue;
                    }

                    string[] parts = trimmedLine.Split(',', ' ', '\t');

                    if (parts.Length < 2)
                    {
                        throw new ParamParseException
                            ($"Line {lineNumber}: invalid format at param '{line}'");
                    }

                    var name = parts[0].Trim();

                    if (name.Length > 16)
                    {
                        throw new ParamParseException
                            ($"Line {lineNumber}: Parameters name can't be more than 16 symbols");
                    }

                    if (!float.TryParse(parts[1].Trim(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float value))
                    {
                        throw new ParamParseException($"Line {lineNumber}: cannot read the value '{parts[1].Trim()}'");
                    }

                    result.Add(new Parameter
                    {
                        Name = name,
                        Value = value
                    });
                }

                if (result.Count == 0)
                {
                    throw new ParamParseException("File doesn't have any parameters!");
                }
            }
            catch (Exception ex) when (ex is not ParamParseException)
            {
                throw new ParamParseException($"Stream parse error: {ex.Message}", ex);
            }

            return result;
        }
    }
}
