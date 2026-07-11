using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Core.Codecs;

namespace SerialForge.App.ViewModels;

// Send arbitrary hex bytes straight down the wire, bypassing the protocol command
// form — the standard "poke the device" aid. Invalid/empty input is surfaced via
// onError (wired to the log) and sends nothing.
public sealed partial class RawSendViewModel : ViewModelBase
{
    private readonly Action<byte[]> _send;
    private readonly Action<string>? _onError;

    [ObservableProperty] private string _hexText = "";

    public ICommand Send { get; }

    public RawSendViewModel(Action<byte[]> send, Action<string>? onError = null)
    {
        _send = send;
        _onError = onError;
        Send = new RelayCommand(DoSend);
    }

    private void DoSend()
    {
        if (string.IsNullOrWhiteSpace(HexText)) return;
        byte[] bytes;
        try { bytes = BytesCodec.ParseHex(HexText); }
        catch (Exception ex) { _onError?.Invoke("原始发送解析失败：" + ex.Message); return; }
        _send(bytes);
    }
}
