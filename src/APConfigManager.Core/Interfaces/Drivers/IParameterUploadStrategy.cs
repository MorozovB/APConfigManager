using APConfigManager.Core.Interfaces.Transport;
using APConfigManager.Core.Models;
using APConfigManager.Core.Results;

namespace APConfigManager.Core.Interfaces.Drivers;

public sealed record ParameterUploadContext(
    ITelemetryProtocol Telemetry,
    ISerialPortAdapter Port,
    Func<CancellationToken, Task> ReconnectAfterReboot);

public interface IParameterUploadStrategy
{
    Task<ParameterUploadResult> UploadAsync(
        ParameterUploadContext context,
        IReadOnlyList<Parameter> parameters,
        IProgress<(int current, int total)> progress,
        CancellationToken ct);
}
