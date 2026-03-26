using APConfigManager.Core.Models;

namespace APConfigManager.Core.Interfaces.Parsers
{
    /// <summary>
    /// Parsing the parameter file (.param) into a list of Parameter objects.
    /// </summary>
    public interface IParamFileParse
    {
        /// <summary>
        /// Parsing the .param file.
        /// </summary>
        List<Parameter> Parse(string filePath);

        /// <summary>
        ///  Parsing from a stream.
        /// </summary>
        List<Parameter> Parse(Stream stream);
    }
}
