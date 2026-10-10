using System.IO;
using Say.Services;
using Say.ViewModels;

namespace Say.Tests;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _dir;

    public SettingsViewModelTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "say-vm-tests-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // Best effort.
        }
    }

    private SettingsViewModel CreateViewModel(out SettingsStore store)
    {
        store = new SettingsStore(_dir);
        store.Load();
        return new SettingsViewModel(
            store,
            new ModelManager(_dir),
            new TranscriptionService(),
            new TranscriptionService());
    }

    [Fact]
    public void VocabularyText_KeepsRawTextWhileParsingCommas()
    {
        var vm = CreateViewModel(out var store);

        vm.VocabularyText = "Daiane, Helena";

        Assert.Equal("Daiane, Helena", vm.VocabularyText);
        Assert.Equal(new[] { "Daiane", "Helena" }, store.Current.Vocabulary);
    }

    [Fact]
    public void VocabularyText_KeepsRawTextWhileParsingNewlines()
    {
        var vm = CreateViewModel(out var store);

        vm.VocabularyText = "Daiane\r\nHelena";

        Assert.Equal("Daiane\r\nHelena", vm.VocabularyText);
        Assert.Equal(new[] { "Daiane", "Helena" }, store.Current.Vocabulary);
    }

    [Fact]
    public void VocabularyText_KeepsTrailingCommaWhileTyping()
    {
        var vm = CreateViewModel(out var store);

        vm.VocabularyText = "Daiane,";

        Assert.Equal("Daiane,", vm.VocabularyText);
        Assert.Equal(new[] { "Daiane" }, store.Current.Vocabulary);
    }

    [Fact]
    public void VocabularyText_InitializesFromStoredVocabulary()
    {
        var store = new SettingsStore(_dir);
        store.Current.Vocabulary = ["Daiane", "Helena"];
        store.Save();

        var vm = new SettingsViewModel(
            store,
            new ModelManager(_dir),
            new TranscriptionService(),
            new TranscriptionService());

        Assert.Equal("Daiane" + Environment.NewLine + "Helena", vm.VocabularyText);
    }
}
