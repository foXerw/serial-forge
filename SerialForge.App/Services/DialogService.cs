using System.Windows;
using Microsoft.Win32;
using SerialForge.App.ViewModels;
using SerialForge.App.Views;

namespace SerialForge.App.Services;

public sealed class DialogService : IDialogService
{
    public void ShowHelp() => new HelpView().Show();

    public void ShowEditor(ProtocolEditorViewModel vm)
        => new ProtocolEditorView { DataContext = vm }.Show();

    public string? PickOpenJsonPath()
    {
        var dlg = new OpenFileDialog { Filter = "协议 JSON (*.json)|*.json", Title = "打开协议定义" };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickSaveJsonPath()
    {
        var dlg = new SaveFileDialog { Filter = "协议 JSON (*.json)|*.json", Title = "另存为协议定义", AddExtension = true };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public void ShowUpgrade(UpgradeViewModel vm) => new UpgradeView { DataContext = vm }.Show();

    public string? PickFirmwarePath()
    {
        var dlg = new OpenFileDialog { Filter = "固件镜像 (*.bin;*.hex)|*.bin;*.hex|所有文件 (*.*)|*.*", Title = "选择固件文件" };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
