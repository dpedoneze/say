using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Say.Models;

namespace Say.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public SettingsStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Say");
        SettingsPath = Path.Combine(_directory, "settings.json");
    }

    public string SettingsPath { get; }

    public string DataDirectory => _directory;

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Current = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                Migrate(Current);
            }
        }
        catch
        {
            Current = new AppSettings();
        }
    }

    private static void Migrate(AppSettings settings)
    {
        settings.Languages ??= [];

        // Pre-multi-language versions stored a single code in Language.
        if (settings.Languages.Count == 0
            && !string.IsNullOrEmpty(settings.Language)
            && !string.Equals(settings.Language, "auto", StringComparison.OrdinalIgnoreCase))
        {
            settings.Languages.Add(settings.Language);
        }

        settings.Languages.RemoveAll(string.IsNullOrWhiteSpace);
        settings.Languages = settings.Languages.Distinct().ToList();

        settings.Vocabulary = VocabularyCorrector.Parse(string.Join('\n', settings.Vocabulary ?? []));
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_directory);
            var tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, JsonOptions));
            File.Move(tmp, SettingsPath, overwrite: true);
        }
        catch
        {
            // Settings persistence must never take the app down.
        }
    }
}
