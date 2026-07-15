using SerialForge.App.ViewModels;

namespace SerialForge.Tests.App;

public class FieldEditorViewModelTest
{
    [Fact]
    public void Numeric_field_validates_range_and_format()
    {
        var u8 = new FieldEditorViewModel("id", "0", false, maxValue: 0xFF);
        Assert.Null(u8.ValidationMessage);                       // 0 valid
        u8.Value = "255"; Assert.Null(u8.ValidationMessage);     // max valid
        u8.Value = "256"; Assert.NotNull(u8.ValidationMessage);  // out of range
        u8.Value = "0xFF"; Assert.Null(u8.ValidationMessage);    // hex valid
        u8.Value = "xyz"; Assert.NotNull(u8.ValidationMessage);  // bad format
    }

    [Fact]
    public void Free_form_payload_is_not_range_validated()
    {
        var bytes = new FieldEditorViewModel("data", "", false);   // no maxValue => free-form hex
        Assert.Null(bytes.ValidationMessage);
        bytes.Value = "anything"; Assert.Null(bytes.ValidationMessage);
    }

    [Fact]
    public void Read_only_field_is_not_validated()
    {
        var ro = new FieldEditorViewModel("len", "0", true, maxValue: 0xFFFF);
        Assert.Null(ro.ValidationMessage);
        ro.Value = "999999"; Assert.Null(ro.ValidationMessage);   // read-only skips validation
    }

    [Fact]
    public void Field_validates_against_width_range()
    {
        var fe = new FieldEditorViewModel("flags.type", "0", false, maxValue: 15); // 4-bit
        fe.Value = "0x10";   // 16 > 15
        Assert.False(string.IsNullOrEmpty(fe.ValidationMessage));
        fe.Value = "0xF";    // 15 ok
        Assert.True(string.IsNullOrEmpty(fe.ValidationMessage));
    }
}
