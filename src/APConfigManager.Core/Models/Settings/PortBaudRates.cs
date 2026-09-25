namespace APConfigManager.Core.Models.Settings
{
    /// <summary>
    /// Allowed serial connection speeds for the user-selected global port baud rate,
    /// plus safe coercion. The list is capped to guard against unusably high values.
    /// Bootloader and flash use their own fixed rates and are unaffected by this.
    /// </summary>
    public class PortBaudRates
    {
        public const int Default = 115200;

        public static readonly IReadOnlyList<int> Allowed = new[]
        {
            57600,
            115200,
            230400,
            460800,
            921600,
        };

        /// <summary>Returns the value if it is an allowed speed, otherwise the safe default.</summary>
        public static int Coerce(int value) => Allowed.Contains(value) ? value : Default;
    }
}
