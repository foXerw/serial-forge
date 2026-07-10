using System.Collections.ObjectModel;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

public sealed class LogEntry
{
    public string Direction { get; init; } = "";
    public string Hex { get; init; } = "";
    public string? Detail { get; init; }      // field breakdown or error
    public bool IsError { get; init; }
}

public partial class LogViewModel : ViewModelBase
{
    private readonly int _maxEntries;
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public LogViewModel() : this(5000) { }
    public LogViewModel(int maxEntries) => _maxEntries = maxEntries;

    public void AddTx(byte[] frame) => Append("TX", frame, detail: null, error: false);

    public void AddRx(DecodedFrame decoded)
    {
        var detail = !string.IsNullOrEmpty(decoded.Error)
            ? "ERROR: " + decoded.Error
            : string.Join(" | ", decoded.Fields.Select(f => $"{f.Name}={f.Value}"));
        Append("RX", decoded.Raw, detail, !string.IsNullOrEmpty(decoded.Error));
    }

    private void Append(string dir, byte[] frame, string? detail, bool error)
    {
        // caller marshals to UI thread via dispatcher
        Entries.Add(new LogEntry
        {
            Direction = dir,
            Hex = string.Join(' ', frame.Select(b => b.ToString("X2"))),
            Detail = detail,
            IsError = error
        });
        while (Entries.Count > _maxEntries) Entries.RemoveAt(0);
    }
}
