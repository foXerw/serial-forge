using SerialForge.Core.Engine;
using SerialForge.Core.Exceptions;
using SerialForge.Core.Models;
using SerialForge.Core;

namespace SerialForge.Tests.Core;

public class ValidateTest
{
    private static ProtocolDefinition Def() =>
        ProtocolLoader.Load(File.ReadAllText("Fixtures/demo-mcu.json"));

    [Fact]
    public void Valid_definition_passes()
    {
        var ex = Record.Exception(() => ProtocolLoader.Validate(Def()));
        Assert.Null(ex);
    }

    [Fact]
    public void Missing_name_throws()
    {
        var d = Def() with { Name = "" };
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(d));
    }

    [Fact]
    public void Missing_layout_throws()
    {
        var d = Def() with { Layout = Array.Empty<FieldDef>() };
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(d));
    }

    [Fact]
    public void Compute_over_unknown_field_throws()
    {
        var d = Def();
        var computed = Array.Find(d.Layout, f => f.Compute is not null);
        Assert.NotNull(computed);
        var bad = computed! with { Compute = computed!.Compute! with { To = "no_such_field" } };
        var layout = d.Layout.Select(f => f == computed ? bad : f).ToArray();
        Assert.Throws<ProtocolException>(() => ProtocolLoader.Validate(d with { Layout = layout }));
    }
}
