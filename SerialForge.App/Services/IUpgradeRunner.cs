using SerialForge.Transport;

namespace SerialForge.App.Services;

// Decouples UpgradeViewModel from the real flow (FlowRunner/UpgradeFlow), so the
// VM is testable with a fake. MainViewModel wires the real implementation.
public interface IUpgradeRunner
{
    Task<UpgradeStatus> RunAsync(string firmwarePath, int chunkSize, IProgress<UpgradeProgress> progress, CancellationToken ct);
}
