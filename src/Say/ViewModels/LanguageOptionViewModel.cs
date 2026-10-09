using CommunityToolkit.Mvvm.ComponentModel;

namespace Say.ViewModels;

public partial class LanguageOptionViewModel : ObservableObject
{
    private readonly Action _onChanged;

    public LanguageOptionViewModel(string code, string name, bool isSelected, Action onChanged)
    {
        Code = code;
        Name = name;
        _isSelected = isSelected;
        _onChanged = onChanged;
    }

    public string Code { get; }

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value) => _onChanged();
}
