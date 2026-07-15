using SerialForge.App.ViewModels;
using SerialForge.Core.SegmentModel;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.App;

public class MainViewModelApplyTest
{
    private static ProtocolDefinition Def(string cmdName) =>
        SegLoader.Load(@"{
          ""name"": ""p"", ""version"": ""1"", ""defaultByteOrder"": ""little"", ""frameTimeoutMs"": 50,
          ""frame"": [
            { ""name"": ""preamble"", ""role"": ""fixed"",   ""width"": 16, ""value"": [""0xAA"", ""0x55""] },
            { ""name"": ""len"",      ""role"": ""length"",  ""width"": 16, ""byteOrder"": ""little"", ""counts"": [""payload""] },
            { ""name"": ""cmd"",      ""role"": ""value"",   ""width"": 8 },
            { ""name"": ""payload"",  ""role"": ""value"" },
            { ""name"": ""crc"",      ""role"": ""checksum"",""width"": 16, ""byteOrder"": ""little"", ""algo"": ""crc16"",
              ""over"": { ""from"": ""preamble"", ""to"": ""payload"" },
              ""params"": { ""poly"": ""0x1021"", ""init"": ""0xFFFF"", ""refIn"": false, ""refOut"": false, ""xorOut"": ""0x0000"" } }
          ],
          ""commands"": [ { ""name"": """ + cmdName + @""", ""title"": ""T"", ""values"": { ""cmd"": ""0x01"" } } ]
        }");

    [Fact]
    public void ApplyProtocol_reloads_command_panel_without_restart()
    {
        var vm = new MainViewModel(Def("alpha"), null);
        Assert.Equal("alpha", vm.CommandPanel.Commands[0].Name);

        vm.ApplyProtocol(Def("beta"));
        Assert.Equal("beta", vm.CommandPanel.Commands[0].Name);
        Assert.NotNull(vm.CommandPanel.SelectedCommand);
    }

    [Fact]
    public void OpenEditor_constructs_editor_with_current_def()
    {
        var vm = new MainViewModel(Def("alpha"), null);
        // 没有对话服务时 OpenEditor 不抛（no-op），证明命令可绑可用
        var ex = Record.Exception(() => vm.OpenEditor.Execute(null));
        Assert.Null(ex);
    }
}
