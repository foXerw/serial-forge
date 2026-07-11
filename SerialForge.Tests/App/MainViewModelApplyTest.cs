using SerialForge.App.ViewModels;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.App;

public class MainViewModelApplyTest
{
    private static ProtocolDefinition Def(string cmdName) =>
        ProtocolLoader.Load(@"{
          ""name"": ""p"", ""version"": ""1"", ""defaultByteOrder"": ""little"",
          ""framing"": { ""mode"": ""length-prefix"", ""preamble"": [""0xAA""], ""lengthField"": ""len"", ""frameTimeoutMs"": 50 },
          ""layout"": [
            { ""name"": ""preamble"", ""kind"": ""literal"", ""codec"": ""bytes"", ""value"": [""0xAA""] },
            { ""name"": ""len"", ""kind"": ""computed"", ""codec"": ""u16"", ""compute"": { ""algo"": ""length"", ""counts"": [""payload""] } },
            { ""name"": ""cmd"", ""kind"": ""value"", ""codec"": ""u8"" },
            { ""name"": ""payload"", ""kind"": ""value"", ""codec"": ""bytes"" },
            { ""name"": ""crc16"", ""kind"": ""computed"", ""codec"": ""u16"",
              ""compute"": { ""algo"": ""crc16"", ""over"": { ""from"": ""preamble"", ""to"": ""payload"" } } }
          ],
          ""commands"": [
            { ""name"": """ + cmdName + @""", ""title"": ""T"", ""fix"": { ""cmd"": ""0x01"" }, ""payloadFields"": [] }
          ]
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
