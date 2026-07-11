using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Core.Models;

namespace SerialForge.App.ViewModels;

public sealed class LogEntry
{
    public DateTime Timestamp { get; init; }
    public string Direction { get; init; } = "";
    public string Hex { get; init; } = "";
    public string? Detail { get; init; }      // field breakdown or error
    public bool IsError { get; init; }
}

public partial class LogViewModel : ViewModelBase
{
    private readonly int _maxEntries;
    private readonly Func<DateTime> _clock;
    public ObservableCollection<LogEntry> Entries { get; } = new();

    public LogViewModel() : this(5000) { }
    public LogViewModel(int maxEntries) : this(maxEntries, () => DateTime.Now) { }
    public LogViewModel(int maxEntries, Func<DateTime> clock) => (_maxEntries, _clock) = (maxEntries, clock);

    [RelayCommand]
    private void Clear() => Entries.Clear();

    // When true (default), the view auto-scrolls to the newest entry. Toggle off
    // to scroll up and inspect history without being yanked back.
    [ObservableProperty] private bool _autoScroll = true;

    public void AddTx(byte[] frame) => Append("TX", frame, detail: null, error: false);

    // Tool-side error (e.g. encode failure) — not a real TX/RX frame, but shown
    // in the same log so the user sees why nothing went out the port.
    public void AddError(string message) => Append("错误", Array.Empty<byte>(), detail: message, error: true);

    // TX with field breakdown for display (acceptance #3). Mirrors AddRx's
    // formatting so RX/TX share one rendering rule.
    public void AddTx(byte[] frame, DecodedFrame decoded)
    {
        var (detail, error) = FormatDetail(decoded);
        Append("TX", frame, detail, error);
    }

    public void AddRx(DecodedFrame decoded)
    {
        var (detail, error) = FormatDetail(decoded);
        Append("RX", decoded.Raw, detail, error);
    }

    private static (string Detail, bool IsError) FormatDetail(Core.Models.DecodedFrame d) =>
        string.IsNullOrEmpty(d.Error)
            ? (string.Join(" | ", d.Fields.Select(f => $"{f.Name}={f.Value}")), false)
            : ($"错误：{d.Error}", true);

    private void Append(string dir, byte[] frame, string? detail, bool error)
    {
        // caller marshals to UI thread via dispatcher
        Entries.Add(new LogEntry
        {
            Timestamp = _clock(),
            Direction = dir,
            Hex = string.Join(' ', frame.Select(b => b.ToString("X2"))),
            Detail = detail,
            IsError = error
        });
        while (Entries.Count > _maxEntries) Entries.RemoveAt(0);
    }

    // Write the whole log (timestamp | dir | hex | detail per line) to a file.
    public void Export(string path)
    {
        using var writer = new StreamWriter(path);
        foreach (var e in Entries)
            writer.WriteLine($"{e.Timestamp:HH:mm:ss.fff} | {e.Direction} | {e.Hex}" + (e.Detail is null ? "" : " | " + e.Detail));
    }
}
