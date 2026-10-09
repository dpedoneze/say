using Say.Models;
using Say.Services;

namespace Say.Tests;

public class LanguageSelectionTests
{
    private static AppSettings WithLanguages(params string[] languages) => new()
    {
        Languages = languages.ToList(),
    };

    [Fact]
    public void Candidates_Empty_MeansDetectAll()
    {
        Assert.Empty(LanguageSelection.Candidates(WithLanguages()));
        Assert.Null(LanguageSelection.FixedLanguage(WithLanguages()));
        Assert.False(LanguageSelection.NeedsRestrictedDetection(WithLanguages()));
    }

    [Fact]
    public void Candidates_Single_IsFixedLanguage()
    {
        var settings = WithLanguages("pt");
        Assert.Equal(new[] { "pt" }, LanguageSelection.Candidates(settings));
        Assert.Equal("pt", LanguageSelection.FixedLanguage(settings));
        Assert.False(LanguageSelection.NeedsRestrictedDetection(settings));
    }

    [Fact]
    public void Candidates_Multiple_NeedsRestrictedDetection()
    {
        var settings = WithLanguages("en", "pt");
        Assert.Equal(new[] { "en", "pt" }, LanguageSelection.Candidates(settings));
        Assert.Null(LanguageSelection.FixedLanguage(settings));
        Assert.True(LanguageSelection.NeedsRestrictedDetection(settings));
    }

    [Fact]
    public void Candidates_FiltersAutoDuplicatesAndBlanks()
    {
        var settings = WithLanguages("auto", "en", "EN", "", "pt");
        Assert.Equal(new[] { "en", "pt" }, LanguageSelection.Candidates(settings));
    }
}
