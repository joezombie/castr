using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Castr.Data.Entities;

namespace Castr.Services;

/// <summary>
/// Pure, stateless-per-feed evaluator for a feed's YouTube sync filters. Built from a feed's three
/// filter fields; parses comma-separated keyword lists once and precompiles whole-word, case-insensitive
/// regexes for matching titles.
/// </summary>
public sealed class YouTubeFilterEvaluator
{
    private readonly IReadOnlyList<Regex> _includePatterns;
    private readonly IReadOnlyList<Regex> _excludePatterns;
    private readonly DateTime? _downloadAfterDate;

    public YouTubeFilterEvaluator(Feed feed)
        : this(feed.YouTubeIncludeKeywords, feed.YouTubeExcludeKeywords, feed.YouTubeDownloadAfterDate)
    {
    }

    public YouTubeFilterEvaluator(string? includeKeywords, string? excludeKeywords, DateTime? downloadAfterDate)
    {
        _includePatterns = BuildPatterns(includeKeywords);
        _excludePatterns = BuildPatterns(excludeKeywords);
        _downloadAfterDate = downloadAfterDate;
    }

    /// <summary>
    /// Applies include + exclude keyword rules to a title. Exclude wins: a title matching any exclude
    /// term fails regardless of include matches. If no include terms are configured, the include check
    /// passes for all titles; otherwise the title must match at least one include term.
    /// </summary>
    public bool PassesTitleFilters(string title)
    {
        title ??= string.Empty;

        if (_excludePatterns.Any(p => p.IsMatch(title)))
            return false;

        if (_includePatterns.Count == 0)
            return true;

        return _includePatterns.Any(p => p.IsMatch(title));
    }

    /// <summary>
    /// Applies the upload-date cutoff (inclusive). A null cutoff means no date filter (all pass). A null
    /// upload date passes (we don't skip on missing data). The cutoff originates from a date-only picker
    /// (Kind=Unspecified midnight), so we compare by calendar date in UTC. YoutubeExplode yields the upload
    /// date as a Kind=Unspecified value that already represents UTC, so we treat it as UTC (rather than
    /// reinterpreting it as host-local) and compare its date component against the cutoff's date. A video
    /// uploaded on the cutoff calendar day passes; behavior is independent of the host time zone.
    /// </summary>
    public bool PassesDateFilter(DateTime? uploadDate)
    {
        if (_downloadAfterDate is null)
            return true;

        if (uploadDate is null)
            return true;

        return DateTime.SpecifyKind(uploadDate.Value, DateTimeKind.Utc).Date >= _downloadAfterDate.Value.Date;
    }

    /// <summary>
    /// Computes a stable hash (SHA-256 hex) of a feed's three filter fields, so a change to any of them
    /// produces a different hash. Used to invalidate stale skip rows when filters change.
    /// </summary>
    public static string ComputeFilterHash(Feed feed)
        => ComputeFilterHash(feed.YouTubeIncludeKeywords, feed.YouTubeExcludeKeywords, feed.YouTubeDownloadAfterDate);

    public static string ComputeFilterHash(string? includeKeywords, string? excludeKeywords, DateTime? downloadAfterDate)
    {
        // Canonical representation: normalized term lists + the cutoff's date-only form. The cutoff is
        // date-only (see PassesDateFilter), so canonicalizing by date keeps the hash consistent with the
        // comparison. Normalizing terms means cosmetic edits (case, spacing, ordering) that don't change
        // the effective filter also don't churn the hash and needlessly re-admit skipped videos.
        var include = string.Join(",", NormalizeTerms(includeKeywords).OrderBy(t => t, StringComparer.Ordinal));
        var exclude = string.Join(",", NormalizeTerms(excludeKeywords).OrderBy(t => t, StringComparer.Ordinal));
        var date = downloadAfterDate?.Date.ToString("yyyy-MM-dd") ?? string.Empty;

        var canonical = $"include={include}|exclude={exclude}|after={date}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(bytes);
    }

    private static List<Regex> BuildPatterns(string? keywords)
    {
        return NormalizeTerms(keywords)
            .Select(term => new Regex($@"\b{Regex.Escape(term)}\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled))
            .ToList();
    }

    private static List<string> NormalizeTerms(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return [];

        return keywords
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant())
            .Distinct()
            .ToList();
    }
}
