using SerialForge.App.ViewModels;
using SerialForge.Core.Engine;
using SerialForge.Core.SegmentModel;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;

namespace SerialForge.Tests.App;

public class CommandPanelViewModelTest
{
    private static ProtocolDefinition Def() =>
        SegLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    private static FrameEngine Engine(ProtocolDefinition def) => new(def.Frame, def.DefaultByteOrder);

    [Fact]
    public void Selecting_command_exposes_editable_payload_template_fields()
    {
        var def = Def();
        var vm = new CommandPanelViewModel(Engine(def), def, _ => { });
        vm.SelectedCommand = vm.Commands[1]; // writeConfig: payload template [id, value]
        Assert.Equal(2, vm.Fields.Count);    // cmd is preset by the command, not shown
        Assert.Equal("id", vm.Fields[0].Name);
    }

    [Fact]
    public void Send_encodes_and_invokes_sender_with_bytes()
    {
        var def = Def();
        byte[]? sent = null;
        var vm = new CommandPanelViewModel(Engine(def), def, b => sent = b);
        vm.SelectedCommand = vm.Commands[1];
        vm.Fields[0].Value = "0x10";        // id
        vm.Fields[1].Value = "0x1234";      // value

        vm.Send.Execute(null);
        Assert.NotNull(sent);
        Assert.Equal(0xAA, sent![0]);
        Assert.Equal(0x05, sent[4]);        // cmd preset
    }

    [Fact]
    public void Send_surfaces_encode_error_and_does_not_invoke_sender()
    {
        var def = Def();
        byte[]? sent = null;
        string? error = null;
        var vm = new CommandPanelViewModel(Engine(def), def, b => sent = b, msg => error = msg);
        vm.SelectedCommand = vm.Commands[1];       // writeConfig: id(u8)
        vm.Fields[0].Value = "999";                // overflows u8 -> encode throws

        vm.Send.Execute(null);
        Assert.Null(sent);
        Assert.NotNull(error);
        Assert.Contains("编码失败", error);
    }

    [Fact]
    public void Session_snapshot_and_restore_round_trips_field_values()
    {
        var def = Def();
        var vm = new CommandPanelViewModel(Engine(def), def, _ => { });
        vm.SelectedCommand = vm.Commands[1];
        vm.Fields[0].Value = "0x10";
        vm.Fields[1].Value = "0x1234";
        var snap = vm.SnapshotSession();
        var selected = vm.SelectedCommandName;

        var vm2 = new CommandPanelViewModel(Engine(Def()), Def(), _ => { });
        vm2.RestoreSession(snap, selected);

        Assert.Equal("writeConfig", vm2.SelectedCommand!.Name);
        Assert.Equal("0x10", vm2.Fields[0].Value);
        Assert.Equal("0x1234", vm2.Fields[1].Value);
    }

    [Fact]
    public void Field_values_persist_across_command_selection()
    {
        var def = Def();
        var vm = new CommandPanelViewModel(Engine(def), def, _ => { });
        vm.SelectedCommand = vm.Commands[1];
        vm.Fields[0].Value = "0x10";                 // id
        vm.SelectedCommand = vm.Commands[0];         // readVersion (away)
        vm.SelectedCommand = vm.Commands[1];         // back to writeConfig
        Assert.Equal("0x10", vm.Fields[0].Value);    // restored from cache
    }

    [Fact]
    public void Send_can_execute_only_when_command_selected()
    {
        var def = Def();
        var vm = new CommandPanelViewModel(Engine(def), def, _ => { });
        vm.SelectedCommand = null;
        Assert.False(vm.Send.CanExecute(null));
        vm.SelectedCommand = vm.Commands[0];
        Assert.True(vm.Send.CanExecute(null));
    }
}
