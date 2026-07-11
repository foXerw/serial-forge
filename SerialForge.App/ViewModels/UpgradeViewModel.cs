using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.App.Services;
using SerialForge.Transport;

namespace SerialForge.App.ViewModels;

public sealed partial class UpgradeViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IUpgradeRunner _runner;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private int _chunkSize = 64;
    [ObservableProperty] private int _sentBlocks;
    [ObservableProperty] private int _totalBlocks;
    [ObservableProperty] private string _statusText = "待开始";
    [ObservableProperty] private bool _isRunning;

    public ICommand Start { get; }
    public ICommand Cancel { get; }

    public UpgradeViewModel(IDialogService dialogs, IUpgradeRunner runner)
    {
        _dialogs = dialogs;
        _runner = runner;
        Start = new RelayCommand(DoStart);
        Cancel = new RelayCommand(() => _cts?.Cancel(), () => IsRunning);
    }

    private async void DoStart()
    {
        var path = string.IsNullOrEmpty(FilePath) ? _dialogs.PickFirmwarePath() : FilePath;
        if (string.IsNullOrEmpty(path)) return;
        FilePath = path;
        IsRunning = true;
        StatusText = "升级中…";
        SentBlocks = 0;
        _cts = new CancellationTokenSource();
        var progress = new Progress<UpgradeProgress>(OnProgress);
        try
        {
            var status = await _runner.RunAsync(path, ChunkSize, progress, _cts.Token);
            StatusText = status switch
            {
                Transport.UpgradeStatus.Done => $"完成（{TotalBlocks} 块）",
                Transport.UpgradeStatus.Cancelled => "已取消",
                Transport.UpgradeStatus.Failed => "失败",
                _ => StatusText
            };
        }
        catch (Exception ex)
        {
            StatusText = "失败：" + ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void OnProgress(UpgradeProgress p)
    {
        SentBlocks = p.SentBlocks;
        TotalBlocks = p.TotalBlocks;
        // Only the running phase owns the live status text; terminal status
        // (Done/Cancelled/Failed) is set by DoStart's switch. This avoids a race
        // where a deferred Progress callback overwrites the final status.
        if (p.Status == Transport.UpgradeStatus.Running)
            StatusText = $"{p.Phase}：{p.SentBlocks}/{p.TotalBlocks}";
    }
}
