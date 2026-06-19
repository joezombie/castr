using Xunit;
using Castr.Services;

namespace Castr.Tests;

/// <summary>
/// Tests for YouTubeFilterEvaluator's title keyword filters and the date cutoff. The date cutoff
/// originates from a date-only picker (Kind=Unspecified midnight), so comparisons are by calendar
/// date in UTC and the cutoff day itself is inclusive.
/// </summary>
public class YouTubeFilterEvaluatorTests
{
    #region Title filter tests

    [Fact]
    public void PassesTitleFilters_NoFilters_PassesEverything()
    {
        var evaluator = new YouTubeFilterEvaluator(null, null, null);
        Assert.True(evaluator.PassesTitleFilters("Anything goes here"));
    }

    [Fact]
    public void PassesTitleFilters_IncludeMatch_Passes()
    {
        var evaluator = new YouTubeFilterEvaluator("podcast", null, null);
        Assert.True(evaluator.PassesTitleFilters("Weekly Podcast Episode"));
        Assert.False(evaluator.PassesTitleFilters("Some other video"));
    }

    [Fact]
    public void PassesTitleFilters_ExcludeWinsOverInclude()
    {
        var evaluator = new YouTubeFilterEvaluator("podcast", "trailer", null);
        Assert.False(evaluator.PassesTitleFilters("Podcast Trailer"));
    }

    #endregion

    #region Date filter tests

    [Fact]
    public void PassesDateFilter_NoCutoff_AllPass()
    {
        var evaluator = new YouTubeFilterEvaluator(null, null, null);
        Assert.True(evaluator.PassesDateFilter(new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void PassesDateFilter_NullUploadDate_Passes()
    {
        var evaluator = new YouTubeFilterEvaluator(null, null, new DateTime(2026, 6, 1));
        Assert.True(evaluator.PassesDateFilter(null));
    }

    [Fact]
    public void PassesDateFilter_SameCalendarDay_InclusivePass()
    {
        // Cutoff comes from the picker as Kind=Unspecified midnight.
        var cutoff = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var evaluator = new YouTubeFilterEvaluator(null, null, cutoff);

        // A video uploaded any time on the cutoff calendar day (UTC) passes.
        var sameDayMorning = new DateTime(2026, 6, 15, 8, 30, 0, DateTimeKind.Utc);
        var sameDayLate = new DateTime(2026, 6, 15, 23, 59, 0, DateTimeKind.Utc);

        Assert.True(evaluator.PassesDateFilter(sameDayMorning));
        Assert.True(evaluator.PassesDateFilter(sameDayLate));
    }

    [Fact]
    public void PassesDateFilter_DayBeforeCutoff_Fails()
    {
        var cutoff = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var evaluator = new YouTubeFilterEvaluator(null, null, cutoff);

        var dayBefore = new DateTime(2026, 6, 14, 23, 59, 0, DateTimeKind.Utc);
        Assert.False(evaluator.PassesDateFilter(dayBefore));
    }

    [Fact]
    public void PassesDateFilter_DayAfterCutoff_Passes()
    {
        var cutoff = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
        var evaluator = new YouTubeFilterEvaluator(null, null, cutoff);

        var dayAfter = new DateTime(2026, 6, 16, 0, 1, 0, DateTimeKind.Utc);
        Assert.True(evaluator.PassesDateFilter(dayAfter));
    }

    #endregion

    #region Filter hash tests

    [Fact]
    public void ComputeFilterHash_IsDateOnly_TimeComponentDoesNotChangeHash()
    {
        // Two cutoffs on the same calendar day but different times produce the same hash, matching
        // the date-only comparison semantics.
        var a = YouTubeFilterEvaluator.ComputeFilterHash(null, null, new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Unspecified));
        var b = YouTubeFilterEvaluator.ComputeFilterHash(null, null, new DateTime(2026, 6, 15, 13, 45, 0, DateTimeKind.Unspecified));
        Assert.Equal(a, b);
    }

    [Fact]
    public void ComputeFilterHash_DifferentDate_DifferentHash()
    {
        var a = YouTubeFilterEvaluator.ComputeFilterHash(null, null, new DateTime(2026, 6, 15));
        var b = YouTubeFilterEvaluator.ComputeFilterHash(null, null, new DateTime(2026, 6, 16));
        Assert.NotEqual(a, b);
    }

    #endregion
}
