using SerialForge.App.ViewModels;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.App;

public class CommandPanelViewModelTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Selecting_command_exposes_only_editable_payload_fields()
    {
        var engine = new ProtocolEngine(Def());
        var vm = new CommandPanelViewModel(engine, _ => { });
        vm.Load(Def());
        vm.SelectedCommand = vm.Commands[1]; // writeConfig

        Assert.Equal(2, vm.Fields.Count); // id, value
        Assert.Equal("id", vm.Fields[0].Name);
    }

    [Fact]
    public void Send_encodes_and_invokes_sender_with_bytes()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        byte[]? sent = null;
        var vm = new CommandPanelViewModel(engine, b => sent = b);
        vm.Load(def);
        vm.SelectedCommand = vm.Commands[1];
        vm.Fields[0].Value = "0x10";        // id
        vm.Fields[1].Value = "0x1234";      // value

        vm.Send.Execute(null);
        Assert.NotNull(sent);
        Assert.Equal(0xAA, sent![0]);
        Assert.Equal(0x05, sent[4]); // cmd fixed
    }

    [Fact]
    public void Send_surfaces_encode_error_and_does_not_invoke_sender()
    {
        var def = Def();
        var engine = new ProtocolEngine(def);
        byte[]? sent = null;
        string? error = null;
        var vm = new CommandPanelViewModel(engine, b => sent = b, msg => error = msg);
        vm.Load(def);
        vm.SelectedCommand = vm.Commands[1]; // writeConfig: id(u8), value(u16)
        vm.Fields[0].Value = "0x1FF";        // overflows u8 -> encode throws

        vm.Send.Execute(null);

        Assert.Null(sent);
        Assert.NotNull(error);
        Assert.Contains("编码失败", error);
    }

    [Fact]
    public void Send_can_execute_only_when_command_selected()
    {
        var vm = new CommandPanelViewModel(new ProtocolEngine(Def()), _ => { });
        vm.Load(Def());
        vm.SelectedCommand = null;
        Assert.False(vm.Send.CanExecute(null));
        vm.SelectedCommand = vm.Commands[0];
        Assert.True(vm.Send.CanExecute(null));
    }
}
