using SerialForge.Core.Engine;
using SerialForge.Core.Models;

namespace SerialForge.App.Services;

public sealed class ProtocolCatalog
{
    public string Directory { get; set; } = AppContext.BaseDirectory; // protocols copied next to exe

    public ProtocolDefinition? LoadFirst()
    {
        var file = System.IO.Path.Combine(Directory, "demo-mcu.json");
        return System.IO.File.Exists(file) ? ProtocolLoader.LoadFile(file) : null;
    }
}
