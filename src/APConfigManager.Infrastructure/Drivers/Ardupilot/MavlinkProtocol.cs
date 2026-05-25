using APConfigManager.Core.Interfaces.Drivers;
using APConfigManager.Core.Models;
using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Infrastructure.Drivers.Ardupilot;
using static MAVLink;

namespace APConfigManager.Infrastructure.Drivers.ArduPilot;

/// <summary>
/// MAVLink v1 protocol implementation for ArduPilot telemetry and commands.
/// </summary>
public class MavLinkProtocol : ITelemetryProtocol
{
    private readonly ISerialPortAdapter port;
    private readonly MavlinkParse parser;

    /// <summary>
    /// Initializes the MAVLink protocol with a serial port adapter.
    /// </summary>
    public MavLinkProtocol(ISerialPortAdapter port)
    {
        this.port = port;
        parser = new MavlinkParse();
    }

    /// <summary>
    /// Sends a MAVLink HEARTBEAT message to maintain connection with the autopilot.
    /// </summary>
    public async Task SendHeartbeatAsync(CancellationToken ct)
    {
        var heartbeat = new mavlink_heartbeat_t
        {
            type = (byte)MAV_TYPE.GCS,
            autopilot = (byte)MAV_AUTOPILOT.INVALID,
            base_mode = 0,
            system_status = (byte)MAV_STATE.ACTIVE,
            mavlink_version = 3
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.HEARTBEAT,
            heartbeat,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);
    }

    /// <summary>
    /// Sends MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246) to reboot into bootloader.
    /// </summary>
    public async Task RebootToBootloaderAsync(CancellationToken ct)
    {
        var command = new mavlink_command_long_t
        {
            target_system = 1,
            target_component = 1,
            command = (ushort)MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN,
            confirmation = 0,
            param1 = 3
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.COMMAND_LONG,
            command,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);
    }

    /// <summary>
    /// Requests all parameters from the autopilot via PARAM_REQUEST_LIST.
    /// </summary>
    public async Task<List<Parameter>> RequestAllParamsAsync(CancellationToken ct)
    {
        // Establish GCS presence — autopilot ignores commands without heartbeat stream
        for (var i = 0; i < 3; i++)
        {
            await SendHeartbeatAsync(ct);
            await Task.Delay(500, ct);
        }

        var request = new mavlink_param_request_list_t
        {
            target_system = 1,
            target_component = 1
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.PARAM_REQUEST_LIST,
            request,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        // Send request, retry up to 3 times if no response
        var parameters = new List<Parameter>();
        var totalExpected = -1;

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            await port.WriteAsync(packet, 0, packet.Length, ct);
            Console.WriteLine($"RequestAllParams: attempt {attempt}, sent PARAM_REQUEST_LIST");

            var consecutiveNulls = 0;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var msg = await ReadMessageAsync(ct);

                if (msg is null)
                {
                    consecutiveNulls++;
                    var threshold = parameters.Count > 0 ? 3 : 5;

                    if (consecutiveNulls >= threshold)
                        break;

                    continue;
                }

                consecutiveNulls = 0;

                if (msg.msgid != (uint)MAVLINK_MSG_ID.PARAM_VALUE)
                    continue;

                var paramValue = (mavlink_param_value_t)msg.data;
                var name = System.Text.Encoding.ASCII.GetString(paramValue.param_id).TrimEnd('\0');

                parameters.Add(new Parameter
                {
                    Name = name,
                    Value = paramValue.param_value,
                    ParamType = paramValue.param_type
                });

                totalExpected = paramValue.param_count;

                if (parameters.Count >= totalExpected)
                    break;
            }

            if (parameters.Count > 0)
                break;

            Console.WriteLine($"RequestAllParams: attempt {attempt} got 0 params, retrying...");
            await Task.Delay(1000, ct);
        }

        Console.WriteLine($"RequestAllParams: received {parameters.Count}/{totalExpected} parameters");

        var deduplicated = parameters
            .GroupBy(p => p.Name)
            .Select(g => g.Last())
            .ToList();

        return deduplicated;
    }

    /// <summary>
    /// Sets a single parameter on the autopilot via PARAM_SET.
    /// </summary>
    public async Task<bool> SetParamAsync(Parameter param, CancellationToken ct)
    {
        var paramId = new byte[16];
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(param.Name);
        Array.Copy(nameBytes, paramId, Math.Min(nameBytes.Length, 16));

        var paramSet = new mavlink_param_set_t
        {
            target_system = 1,
            target_component = 1,
            param_id = paramId,
            param_value = param.Value,
            param_type = param.ParamType
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.PARAM_SET,
            paramSet,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);

        var response = await WaitForMessageAsync(
            MAVLINK_MSG_ID.PARAM_VALUE,
            3000,
            ct);

        if (response is null)
            return false;

        var confirmed = (mavlink_param_value_t)response.data;
        var confirmedName = System.Text.Encoding.ASCII.GetString(confirmed.param_id).TrimEnd('\0');

        if (!confirmedName.Equals(param.Name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (Math.Abs(confirmed.param_value - param.Value) > 0.001f)
        {
            Console.WriteLine($"  Note: {param.Name} sent={param.Value}, device stored={confirmed.param_value}");
        }

        return true;
    }

    /// <summary>
    /// Retrieves the firmware git hash from the autopilot.
    /// </summary>
    public async Task<string> GetFirmwareVersionAsync(CancellationToken ct)
    {
        var command = new mavlink_command_long_t
        {
            target_system = 1,
            target_component = 1,
            command = (ushort)MAV_CMD.REQUEST_MESSAGE,
            confirmation = 0,
            param1 = (uint)MAVLINK_MSG_ID.AUTOPILOT_VERSION
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.COMMAND_LONG,
            command,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);

        var response = await WaitForMessageAsync(
            MAVLINK_MSG_ID.AUTOPILOT_VERSION,
            3000,
            ct);

        if (response is null)
            return string.Empty;

        var version = (mavlink_autopilot_version_t)response.data;
        return BitConverter.ToString(version.flight_custom_version)
            .Replace("-", "")
            .ToLower();
    }

    /// <summary>
    /// Waiting for a heartbeat message from the device, with a specified timeout.
    /// </summary>
    public async Task<bool> WaitForHeartbeatAsync(int timeoutMs, CancellationToken ct)
    {
        var msg = await WaitForMessageAsync(MAVLINK_MSG_ID.HEARTBEAT, timeoutMs, ct);
        return msg is not null;
    }

    /// <summary>
    /// Sends MAV_CMD_PREFLIGHT_STORAGE (245) to reset parameters to defaults.
    /// </summary>
    public async Task ResetParamsAsync(CancellationToken ct)
    {
        var command = new mavlink_command_long_t
        {
            target_system = 1,
            target_component = 1,
            command = (ushort)MAV_CMD.PREFLIGHT_STORAGE,
            confirmation = 0,
            param1 = 2
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.COMMAND_LONG,
            command,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);

        await WaitForMessageAsync(MAVLINK_MSG_ID.COMMAND_ACK, 5000, ct);
    }

    public async Task RebootNormalAsync(CancellationToken ct)
    {
        var command = new mavlink_command_long_t
        {
            target_system = 1,
            target_component = 1,
            command = (ushort)MAV_CMD.PREFLIGHT_REBOOT_SHUTDOWN,
            param1 = 1  // 1 = normal reboot, NOT bootloader
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.COMMAND_LONG,
            command,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        await port.WriteAsync(packet, 0, packet.Length, ct);
    }

    /// <summary>
    /// Sends MAV_CMD_FLASH_BOOTLOADER command and waits for ACK.
    /// Returns true if bootloader was updated successfully.
    /// </summary>
    public async Task<bool> FlashBootloaderAsync(CancellationToken ct)
    {
        // Establish GCS presence
        for (var i = 0; i < 3; i++)
        {
            await SendHeartbeatAsync(ct);
            await Task.Delay(500, ct);
        }

        // Build COMMAND_LONG packet
        var command = new mavlink_command_long_t
        {
            target_system = 1,
            target_component = 1,
            command = ArduPilotConstants.MavCmdFlashBootloader,
            confirmation = 0,
            param1 = 0,
            param2 = 0,
            param3 = 0,
            param4 = 0,
            param5 = ArduPilotConstants.BootloaderMagicNumber,
            param6 = 0,
            param7 = 0
        };

        var packet = parser.GenerateMAVLinkPacket10(
            MAVLINK_MSG_ID.COMMAND_LONG,
            command,
            ArduPilotConstants.MavSysId,
            ArduPilotConstants.MavCompId);

        Console.WriteLine($"FlashBootloader: sending MAV_CMD_FLASH_BOOTLOADER ({ArduPilotConstants.MavCmdFlashBootloader}), param5={ArduPilotConstants.BootloaderMagicNumber}");

        await port.WriteAsync(packet, 0, packet.Length, ct);

        // Wait for COMMAND_ACK — bootloader write takes 5-15 seconds
        var ackMsg = await WaitForMessageAsync(MAVLINK_MSG_ID.COMMAND_ACK, 30000, ct);

        if (ackMsg is null)
        {
            Console.WriteLine("FlashBootloader: no ACK received (timeout 30s)");
            return false;
        }

        var ack = (mavlink_command_ack_t)ackMsg.data;

        Console.WriteLine($"FlashBootloader: ACK received, command={ack.command}, result={ack.result}");

        // Check that ACK is for our command
        if (ack.command != ArduPilotConstants.MavCmdFlashBootloader)
        {
            Console.WriteLine($"FlashBootloader: ACK for wrong command ({ack.command}), ignoring");
            return false;
        }

        // MAV_RESULT.ACCEPTED = 0
        if (ack.result == (byte)MAV_RESULT.ACCEPTED)
        {
            Console.WriteLine("FlashBootloader: bootloader updated successfully");
            return true;
        }

        var resultName = ack.result switch
        {
            1 => "TEMPORARILY_REJECTED",
            2 => "DENIED",
            3 => "UNSUPPORTED",
            4 => "FAILED",
            _ => $"UNKNOWN ({ack.result})"
        };

        Console.WriteLine($"FlashBootloader: rejected with result={resultName}");
        return false;
    }


    /// <summary>
    /// Reads and parses a single MAVLink message from the port.
    /// </summary>
    private async Task<MAVLinkMessage?> ReadMessageAsync(CancellationToken ct)
    {
        try
        {
            var msg = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    return parser.ReadPacket(port.BaseStream);
                }
                catch (TimeoutException)
                {
                    return null;
                }
            }, ct);

            if (msg == null || msg.data == null)
                return null;

            return msg;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Waits for a specific MAVLink message by ID within the given timeout.
    /// </summary>
    private async Task<MAVLinkMessage?> WaitForMessageAsync(
        MAVLINK_MSG_ID messageId,
        int timeoutMs,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeoutMs);

        try
        {
            while (true)
            {
                var msg = await ReadMessageAsync(cts.Token);

                if (msg is null)
                    continue;

                if (msg.msgid == (uint)messageId)
                    return msg;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
