using SerialForge.App.Services;
using SerialForge.App.ViewModels;
using SerialForge.Transport;

namespace SerialForge.Tests.App;

public class UpgradeViewModelTest
{
    private sealed class FakeRunner : IUpgradeRunner
    {
        public bool WasCalled;
        public Func<IProgress<UpgradeProgress>, UpgradeStatus> Behave = _ => UpgradeStatus.Done;
        public Task<UpgradeStatus> RunAsync(string path, int chunkSize, IProgress<UpgradeProgress> progress, CancellationToken ct)
        { WasCalled = true; return Task.FromResult(Behave(progress)); }
    }

    private sealed class FakeDialogs : IDialogService
    {
        private readonly string _path;
        public FakeDialogs(string path) { _path = path; }
        public void ShowHelp() { }
        public void ShowEditor(ProtocolEditorViewModel vm) { }
        public void ShowUpgrade(UpgradeViewModel vm) { }
        public string? PickOpenJsonPath() => null;
        public string? PickSaveJsonPath() => null;
        public string? PickFirmwarePath() => string.IsNullOrEmpty(_path) ? null : _path;
    }

    [Fact]
    public async Task Start_runs_and_reports_done()
    {
        var runner = new FakeRunner();
        runner.Behave = p => { p.Report(new UpgradeProgress(1, 1, "Done", UpgradeStatus.Done)); return UpgradeStatus.Done; };
        var vm = new UpgradeViewModel(new FakeDialogs("fw.bin"), runner);
        vm.Start.Execute(null);
        await Task.Yield();
        Assert.False(vm.IsRunning);
        Assert.Contains("完成", vm.StatusText);
    }

    [Fact]
    public async Task Start_without_file_is_noop()
    {
        var runner = new FakeRunner();
        var vm = new UpgradeViewModel(new FakeDialogs(""), runner);   // PickFirmwarePath -> null
        vm.Start.Execute(null);
        await Task.Yield();
        Assert.False(runner.WasCalled);
    }
}
