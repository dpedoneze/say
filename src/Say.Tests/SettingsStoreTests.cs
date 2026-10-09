using System.IO;
using Say.Models;
using Say.Services;

namespace Say.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir;

    public SettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "say-tests-" + Guid.NewGuid().ToString("N"));
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

    [Fact]
    public void SaveAndLoad_RoundTripsAllFields()
    {
        var store = new SettingsStore(_dir);
        store.Current.SelectedModelId = "small";
        store.Current.PreviewModelId = "tiny";
        store.Current.Languages = ["en", "pt"];
        store.Current.Language = "pt";
        store.Current.Threads = 4;
        store.Current.HotkeyVk = 0x77;
        store.Current.AppendTrailingSpace = false;
        store.Current.PasteMethod = PasteMethod.Type;
        store.Current.RestoreClipboard = false;
        store.Current.FocusRestoreDelayMs = 200;
        store.Current.AudioDeviceNumber = 2;
        store.Current.RunAtStartup = true;
        store.Save();

        var reloaded = new SettingsStore(_dir);
        reloaded.Load();

        Assert.Equal("small", reloaded.Current.SelectedModelId);
        Assert.Equal("tiny", reloaded.Current.PreviewModelId);
        Assert.Equal(new[] { "en", "pt" }, reloaded.Current.Languages);
        Assert.Equal("pt", reloaded.Current.Language);
        Assert.Equal(4, reloaded.Current.Threads);
        Assert.Equal(0x77, reloaded.Current.HotkeyVk);
        Assert.False(reloaded.Current.AppendTrailingSpace);
        Assert.Equal(PasteMethod.Type, reloaded.Current.PasteMethod);
        Assert.False(reloaded.Current.RestoreClipboard);
        Assert.Equal(200, reloaded.Current.FocusRestoreDelayMs);
        Assert.Equal(2, reloaded.Current.AudioDeviceNumber);
        Assert.True(reloaded.Current.RunAtStartup);
    }

    [Fact]
    public void Load_WithMissingFile_UsesDefaults()
    {
        var store = new SettingsStore(_dir);
        store.Load();

        Assert.Null(store.Current.SelectedModelId);
        Assert.Equal("auto", store.Current.Language);
        Assert.Equal(PasteMethod.Clipboard, store.Current.PasteMethod);
    }

    [Fact]
    public void Load_WithCorruptFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ this is not json");

        var store = new SettingsStore(_dir);
        store.Load();

        Assert.Null(store.Current.SelectedModelId);
        Assert.Equal("auto", store.Current.Language);
        Assert.Empty(store.Current.Languages);
    }

    [Fact]
    public void Load_MigratesLegacySingleLanguage_IntoLanguagesList()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "settings.json"),
            """{ "Language": "pt", "SelectedModelId": "tiny" }""");

        var store = new SettingsStore(_dir);
        store.Load();

        Assert.Equal(new[] { "pt" }, store.Current.Languages);
    }

    [Fact]
    public void Load_LegacyAutoLanguage_MigratesToEmptyList()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "settings.json"),
            """{ "Language": "auto" }""");

        var store = new SettingsStore(_dir);
        store.Load();

        Assert.Empty(store.Current.Languages);
    }

    [Fact]
    public void Load_ExistingLanguages_AreNotOverwrittenByLegacyField()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "settings.json"),
            """{ "Language": "pt", "Languages": ["en"] }""");

        var store = new SettingsStore(_dir);
        store.Load();

        Assert.Equal(new[] { "en" }, store.Current.Languages);
    }
}
