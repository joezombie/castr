using Castr.Data.Entities;

namespace Castr.Tests.Services;

public class YouTubeFilterEvaluatorTests
{
    private static YouTubeFilterEvaluator Build(
        string? include = null, string? exclude = null, DateTime? after = null)
        => new(new Feed
        {
            YouTubeIncludeKeywords = include,
            YouTubeExcludeKeywords = exclude,
            YouTubeDownloadAfterDate = after
        });

    #region Whole-word matching

    [Theory]
    [InlineData("This is an ad", true)]
    [InlineData("Visit our address", false)]
    [InlineData("Live broadcast tonight", false)]
    [InlineData("AD spot", true)]
    public void PassesTitleFilters_IncludeWholeWord_RespectsBoundaries(string title, bool expected)
    {
        var evaluator = Build(include: "ad");
        Assert.Equal(expected, evaluator.PassesTitleFilters(title));
    }

    [Theory]
    [InlineData("This is an ad", false)]      // excluded
    [InlineData("Our address here", true)]    // not the whole word "ad"
    [InlineData("A broadcast", true)]         // not the whole word "ad"
    public void PassesTitleFilters_ExcludeWholeWord_RespectsBoundaries(string title, bool expected)
    {
        var evaluator = Build(exclude: "ad");
        Assert.Equal(expected, evaluator.PassesTitleFilters(title));
    }

    #endregion

    #region Case-insensitivity

    [Theory]
    [InlineData("Episode SPECIAL edition", true)]
    [InlineData("episode special edition", true)]
    [InlineData("Episode Normal edition", false)]
    public void PassesTitleFilters_Include_IsCaseInsensitive(string title, bool expected)
    {
        var evaluator = Build(include: "Special");
        Assert.Equal(expected, evaluator.PassesTitleFilters(title));
    }

    [Fact]
    public void PassesTitleFilters_Exclude_IsCaseInsensitive()
    {
        var evaluator = Build(exclude: "TRAILER");
        Assert.False(evaluator.PassesTitleFilters("Movie trailer"));
    }

    #endregion

    #region Exclude wins over include

    [Fact]
    public void PassesTitleFilters_ExcludeWinsOverInclude()
    {
        var evaluator = Build(include: "interview", exclude: "trailer");
        // Title matches both include and exclude -> excluded wins.
        Assert.False(evaluator.PassesTitleFilters("Interview trailer"));
    }

    [Fact]
    public void PassesTitleFilters_IncludeMatch_NoExcludeMatch_Passes()
    {
        var evaluator = Build(include: "interview", exclude: "trailer");
        Assert.True(evaluator.PassesTitleFilters("Long interview"));
    }

    #endregion

    #region Empty / null keyword sets

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",, ,")]
    public void PassesTitleFilters_EmptyInclude_AllPass(string? include)
    {
        var evaluator = Build(include: include);
        Assert.True(evaluator.PassesTitleFilters("anything goes here"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void PassesTitleFilters_EmptyExclude_NoneExcluded(string? exclude)
    {
        var evaluator = Build(exclude: exclude);
        Assert.True(evaluator.PassesTitleFilters("anything goes here"));
    }

    #endregion

    #region Multiple keywords

    [Fact]
    public void PassesTitleFilters_Include_MatchesAnyTerm()
    {
        var evaluator = Build(include: "news, weather, sports");
        Assert.True(evaluator.PassesTitleFilters("Morning weather report"));
        Assert.False(evaluator.PassesTitleFilters("Cooking show"));
    }

    [Fact]
    public void PassesTitleFilters_Exclude_MatchesAnyTerm()
    {
        var evaluator = Build(exclude: "teaser, trailer");
        Assert.False(evaluator.PassesTitleFilters("Official teaser"));
        Assert.True(evaluator.PassesTitleFilters("Full episode"));
    }

    #endregion

    #region Date filter

    [Fact]
    public void PassesDateFilter_NullCutoff_AllPass()
    {
        var evaluator = Build(after: null);
        Assert.True(evaluator.PassesDateFilter(new DateTime(2000, 1, 1)));
        Assert.True(evaluator.PassesDateFilter(null));
    }

    [Fact]
    public void PassesDateFilter_NullUploadDate_Passes()
    {
        var evaluator = Build(after: new DateTime(2026, 1, 1));
        Assert.True(evaluator.PassesDateFilter(null));
    }

    [Fact]
    public void PassesDateFilter_BoundaryIsInclusive()
    {
        var cutoff = new DateTime(2026, 6, 1);
        var evaluator = Build(after: cutoff);
        Assert.True(evaluator.PassesDateFilter(cutoff));                      // equal -> passes
        Assert.True(evaluator.PassesDateFilter(cutoff.AddDays(1)));           // after -> passes
        Assert.False(evaluator.PassesDateFilter(cutoff.AddDays(-1)));         // before -> fails
    }

    #endregion
}
