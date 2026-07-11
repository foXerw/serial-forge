using SerialForge.Core.Models;

namespace SerialForge.Transport;

public enum UpgradeStatus { Running, Done, Failed, Cancelled }

public sealed record UpgradeProgress(int SentBlocks, int TotalBlocks, string Phase, UpgradeStatus Status, string? Detail = null);

// Built-in firmware-upgrade flow on a FlowRunner: start -> per-chunk transfer -> end.
// Progress reports after each ACKed transfer block; cancellation propagated cleanly.
public sealed class UpgradeFlow
{
    private readonly FlowRunner _runner;
    private readonly ProtocolDefinition _def;
    private readonly int _timeoutMs;
    private readonly int _retries;

    public UpgradeFlow(FlowRunner runner, ProtocolDefinition def, int timeoutMs, int retries)
    { _runner = runner; _def = def; _timeoutMs = timeoutMs; _retries = retries; }

    public async Task<UpgradeStatus> RunAsync(FirmwareImage image, IProgress<UpgradeProgress>? progress, CancellationToken ct)
    {
        try
        {
            await Step("startUpgrade",
                new() { ["totalSize"] = (ulong)image.TotalSize, ["totalCrc32"] = ToUlong(image.TotalCrc32) },
                IsAck, ct);

            int seq = 0;
            foreach (var (s, offset, block) in image.Chunks())
            {
                seq = s;
                await Step("transferBlock",
                    new() { ["seq"] = (ulong)seq, ["offset"] = (ulong)offset, ["data"] = block },
                    f => IsAck(f) && AckSeq(f) == seq, ct);
                progress?.Report(new UpgradeProgress(seq + 1, image.TotalBlocks, "Transfer", UpgradeStatus.Running));
            }

            await Step("endUpgrade",
                new() { ["totalCrc32"] = ToUlong(image.TotalCrc32) },
                IsAck, ct);
            progress?.Report(new UpgradeProgress(image.TotalBlocks, image.TotalBlocks, "Done", UpgradeStatus.Done));
            return UpgradeStatus.Done;
        }
        catch (OperationCanceledException)
        {
            progress?.Report(new UpgradeProgress(0, image.TotalBlocks, "Cancelled", UpgradeStatus.Cancelled));
            return UpgradeStatus.Cancelled;
        }
        catch (Exception ex)
        {
            progress?.Report(new UpgradeProgress(0, image.TotalBlocks, "Failed", UpgradeStatus.Failed, ex.Message));
            return UpgradeStatus.Failed;
        }
    }

    private async Task Step(string cmdName, Dictionary<string, object> payload, Func<DecodedFrame, bool> expect, CancellationToken ct)
    {
        var inst = new CommandInstance { Command = _def.Commands.First(c => c.Name == cmdName), PayloadValues = payload };
        await _runner.SendExpect(inst, expect, _timeoutMs, _retries, ct);
    }

    // ACK = cmd==0x06. For transfer the caller additionally requires seq match.
    private static bool IsAck(DecodedFrame f) =>
        f.Fields.FirstOrDefault(x => x.Name == "cmd") is { } cmd && (ulong)cmd.Value! == 0x06;

    // ACK payload layout: tag:u8 at [0], seq:u16le at [1..3].
    private static int AckSeq(DecodedFrame f)
    {
        var payload = (byte[])f.Fields.First(x => x.Name == "payload").Value!;
        return payload.Length >= 3 ? payload[1] | (payload[2] << 8) : 0;
    }

    private static ulong ToUlong(byte[] le)
    {
        ulong v = 0;
        for (int i = 0; i < le.Length && i < 4; i++) v |= ((ulong)le[i]) << (8 * i);
        return v;
    }
}
