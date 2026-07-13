using System.Text;
using System.Windows;

namespace SerialForge.App;

public partial class App : Application
{
    static App() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
}
