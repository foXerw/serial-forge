using System.Text;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerialForge.Core.Codecs;

namespace SerialForge.App.ViewModels;

public enum RawSendMode { Hex, Text }
public enum TextEncoding { Ascii, Utf8, Gbk }

// Send arbitrary bytes straight down the wire, bypassing the protocol command
// form. Hex mode parses a hex string; Text mode encodes the string with the
// chosen encoding. Invalid/empty input is surfaced via onError and sends nothing.
public sealed partial class RawSendViewModel : ViewModelBase
{
    private readonly Action<byte[]> _send;
    private readonly Action<string>? _onError;

    [ObservableProperty] private string _input = "";
    [ObservableProperty] private RawSendMode _mode = RawSendMode.Hex;
    [ObservableProperty] private TextEncoding _encoding = TextEncoding.Utf8;

    public bool IsTextMode => Mode == RawSendMode.Text;
    partial void OnModeChanged(RawSendMode value) => OnPropertyChanged(nameof(IsTextMode));

    public ICommand Send { get; }

    public RawSendViewModel(Action<byte[]> send, Action<string>? onError = null)
    {
        _send = send;
        _onError = onError;
        Send = new RelayCommand(DoSend);
    }

    private void DoSend()
    {
        if (string.IsNullOrWhiteSpace(Input)) return;
        byte[] bytes;
        try { bytes = Mode == RawSendMode.Hex ? BytesCodec.ParseHex(Input) : EncodeText(Input, Encoding); }
        catch (Exception ex) { _onError?.Invoke("原始发送解析失败：" + ex.Message); return; }
        _send(bytes);
    }

    private static byte[] EncodeText(string text, TextEncoding enc) => enc switch
    {
        TextEncoding.Ascii => System.Text.Encoding.ASCII.GetBytes(text),
        TextEncoding.Utf8 => System.Text.Encoding.UTF8.GetBytes(text),
        TextEncoding.Gbk => System.Text.Encoding.GetEncoding("GBK").GetBytes(text),
        _ => throw new ArgumentOutOfRangeException(nameof(enc))
    };
}
