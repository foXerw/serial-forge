using System.Text.Json;
using SerialForge.App.ViewModels;
using SerialForge.Core;
using SerialForge.Core.Models;
using SerialForge.Core.SegmentModel;
using SegLoader = SerialForge.Core.SegmentModel.ProtocolLoader;
using SegSaver = SerialForge.Core.SegmentModel.ProtocolSaver;

namespace SerialForge.Tests.App;

public class EditorViewModelsTest
{
    private static ProtocolDefinition DemoDef() =>
        SegLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    private static Segment Seg(string name, SegmentRole role, int? width, params string[] counts) =>
        new(name, role, width, null, null, null, null, counts.Length == 0 ? null : counts, 0, null, null, null, null);

    [Fact]
    public void Segment_view_model_round_trips_role_specific_fields()
    {
        var crc = new Segment("crc", SegmentRole.Checksum, 16, ByteOrder.Little, null, null, null, null, 0,
            "crc16", "preamble", "payload", new()
            {
                ["poly"] = JsonSerializer.SerializeToElement("0x1021"),
                ["init"] = JsonSerializer.SerializeToElement("0xFFFF"),
                ["refIn"] = JsonSerializer.SerializeToElement(false),
                ["refOut"] = JsonSerializer.SerializeToElement(false),
                ["xorOut"] = JsonSerializer.SerializeToElement("0x0000"),
            });
        var vm = new SegmentViewModel(crc);
        Assert.Equal(SegmentRole.Checksum, vm.Role);
        Assert.Equal("crc16", vm.Algo);
        Assert.Equal("0x1021", vm.Poly);
        Assert.False(vm.RefIn);
        var back = vm.ToSegment();
        Assert.Equal("crc16", back.Algo);
        Assert.Equal("0x1021", back.Params!["poly"].GetString());
    }

    [Fact]
    public void Editor_populate_then_build_round_trips_key_fields()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        Assert.Equal("demo-mcu", vm.Name);
        Assert.Equal(5, vm.Segments.Count);
        Assert.Equal(2, vm.Commands.Count);
        var built = vm.Build();              // 不抛即合法
        Assert.Equal("demo-mcu", built.Name);
        Assert.Equal(SegmentRole.Length, built.Frame.First(s => s.Name == "len").Role);
    }

    [Fact]
    public void Apply_invokes_callback_only_when_valid()
    {
        ProtocolDefinition? applied = null;
        var vm = new ProtocolEditorViewModel(DemoDef(), d => applied = d, null!);
        vm.Apply.Execute(null);              // 草稿来自合法 demo → 应用成功
        Assert.NotNull(applied);
    }

    [Fact]
    public void Apply_with_invalid_draft_surfaces_error_and_does_not_apply()
    {
        ProtocolDefinition? applied = null;
        var vm = new ProtocolEditorViewModel(DemoDef(), d => applied = d, null!);
        vm.Segments.First(s => s.Role == SegmentRole.Checksum).OverTo = "no_such";
        vm.Apply.Execute(null);
        Assert.Null(applied);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void AddSegment_and_RemoveSegment_mutate_collection()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        int n = vm.Segments.Count;
        vm.AddSegment.Execute(null);
        Assert.Equal(n + 1, vm.Segments.Count);
        var last = vm.Segments[^1];
        vm.RemoveSegment.Execute(last);
        Assert.Equal(n, vm.Segments.Count);
    }

    [Fact]
    public void RefreshRaw_serializes_draft_to_parseable_json()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        vm.RefreshRaw.Execute(null);
        Assert.False(string.IsNullOrWhiteSpace(vm.RawJson));
        SegLoader.Load(vm.RawJson);   // round-trips
    }

    [Fact]
    public void Command_editor_preserves_payload_template_through_round_trip()
    {
        var def = DemoDef();
        var json = SegSaver.ToJson(def);
        var again = SegLoader.Load(json);
        // writeConfig carries its payload sub-template through save/load.
        var write = again.Commands.First(c => c.Name == "writeConfig");
        Assert.NotNull(write.Payload);
        Assert.Equal(2, write.Payload!.Length);
    }
}
