using System.Text.Json;
using SerialForge.App.ViewModels;
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
}
