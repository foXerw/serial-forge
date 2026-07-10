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
}
