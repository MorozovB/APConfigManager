namespace APConfigManager.Infrastructure.Drivers.Ardupilot
{
    /// <summary>
    /// Protocol constants for STM32 bootloader and MAVLink communication.
    /// </summary>
    public static class ArduPilotConstants
    {
        /// <summary>Bootloader SYNC request command.</summary>
        public const byte GET_SYNC = 0x21;

        /// <summary>Request device information (board_id, flash_size).</summary>
        public const byte GET_DEVICE = 0x22;

        /// <summary>Full chip erase command.</summary>
        public const byte CHIP_ERASE = 0x23;

        /// <summary>Program multiple bytes command.</summary>
        public const byte PROG_MULTI = 0x27;

        /// <summary>Get CRC of programmed data command.</summary>
        public const byte GET_CRC = 0x29;

        /// <summary>Set bootloader baud rate.</summary>
        public const byte SET_BAUD = 0x33;

        /// <summary>Bootloader start command.</summary>
        public const byte BOOT = 0x30;

        /// <summary>End of command marker.</summary>
        public const byte EOC = 0x20;

        /// <summary>Bootloader response: in sync.</summary>
        public const byte INSYNC = 0x12;

        /// <summary>Bootloader response: operation successful.</summary>
        public const byte OK = 0x10;

        /// <summary>Bootloader response: invalid operation.</summary>
        public const byte FAILED = 0x11;

        /// <summary>Bootloader response: sync failed.</summary>
        public const byte INVALID = 0x13;

        /// <summary>Maximum bytes per PROG_MULTI write (64 bytes).</summary>
        public const int ProgMultiMaxSize = 64;

        /// <summary>Timeout for bootloader sync in milliseconds.</summary>
        public const int SyncTimeoutMs = 1000;

        /// <summary>Timeout for chip erase in milliseconds.</summary>
        public const int EraseTimeoutMs = 30000;

        /// <summary>Default baud rate for bootloader communication.</summary>
        public const int BootloaderBaudRate = 115200;

        /// <summary>MAVLink system ID for the ground station (GCS).</summary>
        public const byte MavSysId = 255;

        /// <summary>MAVLink component ID (MAV_COMP_ID_MISSIONPLANNER).</summary>
        public const byte MavCompId = 190;

        /// <summary>MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN — reboot into bootloader.</summary>
        public const ushort CmdRebootToBootloader = 246;

        /// <summary>MAV_CMD_PREFLIGHT_STORAGE — reset parameters to defaults.</summary>
        public const ushort CmdResetParameters = 245;

        /// <summary>Default baud rate for MAVLink communication.</summary>
        public const int MavlinkBaudRate = 115200;
    }
}
