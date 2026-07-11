using SerialForge.App.ViewModels;

namespace SerialForge.App.Services;

// Abstraction over WPF windows/dialogs so view models stay testable. The WPF
// implementation (DialogService) lives in the App project; tests inject a fake.
public interface IDialogService
{
    void ShowHelp();
    void ShowEditor(ProtocolEditorViewModel vm);
    string? PickOpenJsonPath();
    string? PickSaveJsonPath();
}
