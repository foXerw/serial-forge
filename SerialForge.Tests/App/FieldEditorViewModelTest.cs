using SerialForge.App.ViewModels;

namespace SerialForge.Tests.App;

public class FieldEditorViewModelTest
{
    [Fact]
    public void Numeric_field_validates_range_and_format()
    {
        var u8 = new FieldEditorViewModel("id", "U8", "0", false);
        Assert.Null(u8.ValidationMessage);              // 0 valid
        u8.Value = "255"; Assert.Null(u8.ValidationMessage);     // max valid
        u8.Value = "256"; Assert.NotNull(u8.ValidationMessage);  // out of range
        u8.Value = "0xFF"; Assert.Null(u8.ValidationMessage);    // hex valid
        u8.Value = "xyz"; Assert.NotNull(u8.ValidationMessage);  // bad format
    }

    [Fact]
    public void Non_numeric_codec_is_not_validated()
    {
        var bytes = new FieldEditorViewModel("data", "Bytes", "", false);
        Assert.Null(bytes.ValidationMessage);
        bytes.Value = "anything"; Assert.Null(bytes.ValidationMessage);
    }

    [Fact]
    public void Read_only_field_is_not_validated()
    {
        var ro = new FieldEditorViewModel("len", "U16", "0", true);
        Assert.Null(ro.ValidationMessage);
        ro.Value = "999999"; Assert.Null(ro.ValidationMessage);   // read-only skips validation
    }
}
