using System.Text.Json;
using SerialForge.App.ViewModels;
using SerialForge.Core;
using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.Tests.App;

public class EditorViewModelsTest
{
    [Fact]
    public void Crc_params_round_trip_through_compute_spec()
    {
        var spec = new ComputeSpec("crc16", null, 0, "preamble", "payload", new()
        {
            ["poly"] = JsonSerializer.SerializeToElement("0x1021"),
            ["init"] = JsonSerializer.SerializeToElement("0xFFFF"),
            ["xorOut"] = JsonSerializer.SerializeToElement("0x0000"),
            ["refIn"] = JsonSerializer.SerializeToElement(false),
            ["refOut"] = JsonSerializer.SerializeToElement(false),
        });

        var vm = new ComputeEditorViewModel(spec);
        Assert.Equal("0x1021", vm.Poly);
        Assert.False(vm.RefIn);

        var back = vm.ToComputeSpec();
        Assert.Equal("0x1021", back.Params!["poly"].GetString());
        Assert.False(back.Params!["refIn"].GetBoolean());
    }

    [Fact]
    public void Blank_crc_hex_params_are_omitted_so_algo_defaults_apply()
    {
        // refIn/refOut are always emitted (defaults false); only blank HEX params
        // (poly/init/xorOut) are omitted so the algorithm falls back to its defaults.
        var vm = new ComputeEditorViewModel { Algo = "crc16", From = "preamble", To = "payload" };
        var back = vm.ToComputeSpec();
        Assert.NotNull(back.Params);
        Assert.False(back.Params!.ContainsKey("poly"));
        Assert.False(back.Params.ContainsKey("init"));
        Assert.False(back.Params.ContainsKey("xorOut"));
        Assert.False(back.Params["refIn"].GetBoolean());
    }

    [Fact]
    public void Hex_is_normalized_to_lowercase_0x_prefix()
    {
        var vm = new ComputeEditorViewModel { Algo = "crc16", From = "a", To = "b", Poly = "ABCD" };
        Assert.Equal("0xabcd", vm.ToComputeSpec().Params!["poly"].GetString());
    }

    [Fact]
    public void Length_compute_reads_counts_and_offset()
    {
        var spec = new ComputeSpec("length", new[] { "payload" }, 0, null, null, null);
        var vm = new ComputeEditorViewModel(spec);
        Assert.Equal("payload", vm.Counts);
        var back = vm.ToComputeSpec();
        Assert.Equal(new[] { "payload" }, back.Counts);
    }

    [Fact]
    public void Literal_value_round_trips_to_0x_hex_array()
    {
        var vm = new LayoutFieldViewModel
        { Kind = FieldKind.Literal, Codec = CodecType.Bytes, LiteralValue = "AA 55" };
        var def = vm.ToFieldDef();
        Assert.Equal(new[] { "0xaa", "0x55" }, def.LiteralValue);
    }

    [Fact]
    public void Length_compute_field_derives_width_from_codec()
    {
        var vm = new LayoutFieldViewModel
        { Name = "len", Kind = FieldKind.Computed, Codec = CodecType.U16 };
        vm.Compute.Algo = "length";
        vm.Compute.Counts = "payload";
        var def = vm.ToFieldDef();
        Assert.Equal(2, def.Compute!.Params!["width"].GetInt32());
    }

    [Fact]
    public void Command_round_trips_fix_and_payload_fields()
    {
        var src = new CommandDef("writeConfig", "Write Config",
            new() { ["cmd"] = "0x05" },
            new[] { new PayloadFieldDef("id", CodecType.U8, null, null, "0") });
        var vm = new CommandEditorViewModel(src);
        Assert.Equal("writeConfig", vm.Name);
        Assert.Single(vm.Fix);
        Assert.Single(vm.PayloadFields);
        var back = vm.ToDef();
        Assert.Equal("0x05", back.Fix["cmd"]);
        Assert.Equal("id", back.PayloadFields[0].Name);
    }

    private static ProtocolDefinition DemoDef() =>
        SerialForge.Core.Engine.ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Editor_populate_then_build_round_trips_key_fields()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        Assert.Equal("demo-mcu", vm.Name);
        Assert.Equal(5, vm.LayoutFields.Count);
        Assert.Equal(2, vm.Commands.Count);
        var built = vm.Build();              // 不抛即合法
        Assert.Equal("demo-mcu", built.Name);
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
        // 破坏：把 crc 的 to 指向不存在的字段
        vm.LayoutFields.First(f => f.Compute.IsCrc).Compute.To = "no_such";
        vm.Apply.Execute(null);
        Assert.Null(applied);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void AddLayoutField_and_RemoveLayoutField_mutate_collection()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        int n = vm.LayoutFields.Count;
        vm.AddLayoutField.Execute(null);
        Assert.Equal(n + 1, vm.LayoutFields.Count);
        var last = vm.LayoutFields[^1];
        vm.RemoveLayoutField.Execute(last);
        Assert.Equal(n, vm.LayoutFields.Count);
    }

    [Fact]
    public void MoveLayoutFieldUp_swaps_with_previous()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        var first = vm.LayoutFields[0]; var second = vm.LayoutFields[1];
        vm.MoveLayoutFieldUp.Execute(second);          // moves second up
        Assert.Same(second, vm.LayoutFields[0]);
        Assert.Same(first, vm.LayoutFields[1]);
        vm.MoveLayoutFieldUp.Execute(first);           // first already at top -> no-op
        Assert.Same(first, vm.LayoutFields[0]);
    }

    [Fact]
    public void AddCommand_and_RemoveCommand_mutate_collection()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        int n = vm.Commands.Count;
        vm.AddCommand.Execute(null);
        Assert.Equal(n + 1, vm.Commands.Count);
        var added = vm.Commands[^1];
        vm.RemoveCommand.Execute(added);
        Assert.Equal(n, vm.Commands.Count);
    }

    [Fact]
    public void RefreshRaw_serializes_draft_to_parseable_json()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        vm.RefreshRaw.Execute(null);
        Assert.False(string.IsNullOrWhiteSpace(vm.RawJson));
        ProtocolLoader.Load(vm.RawJson);   // round-trips
    }

    [Fact]
    public void ApplyRaw_populates_from_json_and_surfaces_parse_errors()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        // valid JSON -> repopulates
        vm.RawJson = File.ReadAllText("Fixtures/demo-mcu.json");
        vm.ApplyRaw.Execute(null);
        Assert.Equal("demo-mcu", vm.Name);
        Assert.True(string.IsNullOrEmpty(vm.ErrorMessage));
        // garbage -> error, draft unchanged
        vm.RawJson = "{ not valid json";
        string nameBefore = vm.Name;
        vm.ApplyRaw.Execute(null);
        Assert.Equal(nameBefore, vm.Name);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public void BuildDraft_serializes_invalid_draft_without_throwing()
    {
        var vm = new ProtocolEditorViewModel(DemoDef(), _ => { }, null!);
        vm.LayoutFields.First(f => f.Compute.IsCrc).Compute.To = "no_such"; // invalid
        var ex = Record.Exception(() => ProtocolSaver.ToJson(vm.BuildDraft()));
        Assert.Null(ex);   // BuildDraft does not validate; only Build() does
    }

    [Fact]
    public void AddPayloadField_and_RemovePayloadField_mutate_collection()
    {
        var cmd = new CommandEditorViewModel();
        int n = cmd.PayloadFields.Count;
        cmd.AddPayloadField.Execute(null);
        Assert.Equal(n + 1, cmd.PayloadFields.Count);
        var last = cmd.PayloadFields[^1];
        cmd.RemovePayloadField.Execute(last);
        Assert.Equal(n, cmd.PayloadFields.Count);
    }

    [Fact]
    public void AddFix_and_RemoveFix_mutate_collection()
    {
        var cmd = new CommandEditorViewModel();
        int n = cmd.Fix.Count;
        cmd.AddFix.Execute(null);
        Assert.Equal(n + 1, cmd.Fix.Count);
        var last = cmd.Fix[^1];
        cmd.RemoveFix.Execute(last);
        Assert.Equal(n, cmd.Fix.Count);
    }
}
