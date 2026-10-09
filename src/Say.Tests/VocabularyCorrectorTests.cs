using Say.Services;

namespace Say.Tests;

public class VocabularyCorrectorTests
{
    [Fact]
    public void Apply_ExactWord_GetsCanonicalCasing()
    {
        Assert.Equal("hello Daiane", VocabularyCorrector.Apply("hello daiane", ["Daiane"]));
    }

    [Fact]
    public void Apply_NearMiss_IsCorrected()
    {
        Assert.Equal("hello Daiane", VocabularyCorrector.Apply("hello Diane", ["Daiane"]));
        Assert.Equal("David said hi", VocabularyCorrector.Apply("davi said hi", ["David"]));
    }

    [Fact]
    public void Apply_Accents_AreCorrected()
    {
        Assert.Equal("João arrived", VocabularyCorrector.Apply("joao arrived", ["João"]));
    }

    [Fact]
    public void Apply_UnrelatedWord_IsNotTouched()
    {
        Assert.Equal("the dado rolled", VocabularyCorrector.Apply("the dado rolled", ["Davi"]));
    }

    [Fact]
    public void Apply_TwoLetterTokens_AreNotFuzzyMatched()
    {
        Assert.Equal("go now", VocabularyCorrector.Apply("go now", ["Jo"]));
    }

    [Fact]
    public void Apply_Punctuation_IsPreserved()
    {
        Assert.Equal("hi Daiane, bye.", VocabularyCorrector.Apply("hi Daiane, bye.", ["Daiane"]));
        Assert.Equal("hi Daiane.", VocabularyCorrector.Apply("hi diane.", ["Daiane"]));
    }

    [Fact]
    public void Apply_MultiWordEntry_IsReplacedWithCanonicalCasing()
    {
        Assert.Equal("see Hello World now", VocabularyCorrector.Apply("see HELLO WORLD now", ["Hello World"]));
    }

    [Fact]
    public void Apply_MultiWordEntry_IsNotFuzzyMatched()
    {
        Assert.Equal("see helo world now", VocabularyCorrector.Apply("see helo world now", ["hello world"]));
    }

    [Fact]
    public void Apply_EmptyVocabulary_ReturnsInput()
    {
        Assert.Equal("anything goes", VocabularyCorrector.Apply("anything goes", []));
    }

    [Fact]
    public void BuildPrompt_FormatsGlossary()
    {
        Assert.Equal("Glossary: Daiane, Helena.", VocabularyCorrector.BuildPrompt(["Daiane", "Helena"]));
    }

    [Fact]
    public void BuildPrompt_Empty_ReturnsNull()
    {
        Assert.Null(VocabularyCorrector.BuildPrompt([]));
    }

    [Fact]
    public void BuildPrompt_TruncatesLongLists()
    {
        var vocabulary = Enumerable.Range(0, 100)
            .Select(i => "word" + i.ToString("D16"))
            .ToList();

        var prompt = VocabularyCorrector.BuildPrompt(vocabulary);

        Assert.NotNull(prompt);
        Assert.True(prompt!.Length <= VocabularyCorrector.MaxPromptChars);
        Assert.EndsWith(".", prompt);
    }

    [Fact]
    public void Parse_SplitsTrimsAndDeduplicates()
    {
        var parsed = VocabularyCorrector.Parse("Daiane\nHelena, david\n\nDaiane\ndavid\n");

        Assert.Equal(new[] { "Daiane", "Helena", "david" }, parsed);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(VocabularyCorrector.Parse(null));
        Assert.Empty(VocabularyCorrector.Parse("   \n , "));
    }
}
