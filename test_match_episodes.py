#!/usr/bin/env python3
"""
Unit tests for match_episodes.py fuzzy matching functionality.

Tests cover:
- Title normalization
- Part number extraction
- Similarity scoring
- File path handling
- Edge cases and error handling
"""

import unittest
from match_episodes import (
    normalize_title,
    normalize_for_comparison,
    extract_part_number,
    similarity_score,
    extract_filename_from_ls_line,
    find_best_match
)


class TestNormalizeTitle(unittest.TestCase):
    """Test the normalize_title function."""

    def test_normalize_basic_suffix(self):
        """Test removal of standard suffix."""
        title = "Episode Name | BEHIND THE BASTARDS"
        expected = "Episode Name"
        self.assertEqual(normalize_title(title), expected)

    def test_normalize_unicode_separator(self):
        """Test removal of suffix with unicode separator."""
        title = "Episode Name ｜ BEHIND THE BASTARDS"
        expected = "Episode Name"
        self.assertEqual(normalize_title(title), expected)

    def test_normalize_case_insensitive(self):
        """Test case insensitive suffix removal."""
        title = "Episode Name | behind the bastards"
        expected = "Episode Name"
        self.assertEqual(normalize_title(title), expected)

    def test_normalize_unicode_characters(self):
        """Test unicode character normalization."""
        title = "Episode： Name？ Test"
        expected = "Episode: Name? Test"
        self.assertEqual(normalize_title(title), expected)

    def test_normalize_extra_whitespace(self):
        """Test extra whitespace removal."""
        title = "Episode   Name    Test"
        expected = "Episode Name Test"
        self.assertEqual(normalize_title(title), expected)

    def test_normalize_empty_string(self):
        """Test empty string handling."""
        self.assertEqual(normalize_title(""), "")

    def test_normalize_whitespace_only(self):
        """Test whitespace-only string handling."""
        self.assertEqual(normalize_title("   "), "")

    def test_normalize_no_suffix(self):
        """Test title without suffix."""
        title = "Regular Episode Name"
        expected = "Regular Episode Name"
        self.assertEqual(normalize_title(title), expected)


class TestExtractPartNumber(unittest.TestCase):
    """Test the extract_part_number function."""

    def test_extract_part_one(self):
        """Test extraction of Part One."""
        self.assertEqual(extract_part_number("Part One: Title"), 1)

    def test_extract_part_two(self):
        """Test extraction of Part Two."""
        self.assertEqual(extract_part_number("Part Two: Title"), 2)

    def test_extract_part_three(self):
        """Test extraction of Part Three."""
        self.assertEqual(extract_part_number("Part Three: Title"), 3)

    def test_extract_part_numeric(self):
        """Test extraction of numeric part."""
        self.assertEqual(extract_part_number("Part 5: Title"), 5)

    def test_extract_pt_abbreviation(self):
        """Test extraction with Pt abbreviation."""
        self.assertEqual(extract_part_number("Pt 3: Title"), 3)

    def test_extract_pt_abbreviation_large(self):
        """Test extraction with Pt abbreviation for large numbers."""
        self.assertEqual(extract_part_number("Pt 42: Title"), 42)

    def test_extract_part_case_insensitive(self):
        """Test case insensitivity."""
        self.assertEqual(extract_part_number("part one: title"), 1)
        self.assertEqual(extract_part_number("PART TWO: TITLE"), 2)

    def test_extract_part_unicode_colon(self):
        """Test with unicode colon."""
        self.assertEqual(extract_part_number("Part One： Title"), 1)

    def test_extract_no_part(self):
        """Test when no part number exists."""
        self.assertIsNone(extract_part_number("No Part"))
        self.assertIsNone(extract_part_number("Episode Title"))

    def test_extract_part_not_at_start(self):
        """Test that part number not at start is ignored."""
        self.assertIsNone(extract_part_number("Title Part One: Something"))

    def test_extract_part_all_words(self):
        """Test all word-based part numbers."""
        self.assertEqual(extract_part_number("Part Four: Title"), 4)
        self.assertEqual(extract_part_number("Part Five: Title"), 5)
        self.assertEqual(extract_part_number("Part Six: Title"), 6)
        self.assertEqual(extract_part_number("Part Seven: Title"), 7)
        self.assertEqual(extract_part_number("Part Eight: Title"), 8)
        self.assertEqual(extract_part_number("Part Nine: Title"), 9)
        self.assertEqual(extract_part_number("Part Ten: Title"), 10)


class TestNormalizeForComparison(unittest.TestCase):
    """Test the normalize_for_comparison function."""

    def test_normalize_removes_part_number(self):
        """Test that part numbers are removed."""
        title = "Part One: Episode Name"
        expected = "Episode Name"
        self.assertEqual(normalize_for_comparison(title), expected)

    def test_normalize_removes_numeric_part(self):
        """Test that numeric part numbers are removed."""
        title = "Part 5: Episode Name"
        expected = "Episode Name"
        self.assertEqual(normalize_for_comparison(title), expected)

    def test_normalize_removes_pt_abbreviation(self):
        """Test that Pt abbreviation is removed."""
        title = "Pt 3: Episode Name"
        expected = "Episode Name"
        self.assertEqual(normalize_for_comparison(title), expected)

    def test_normalize_removes_suffix(self):
        """Test that BTB suffix is also removed."""
        title = "Part One: Episode Name | BEHIND THE BASTARDS"
        expected = "Episode Name"
        self.assertEqual(normalize_for_comparison(title), expected)

    def test_normalize_no_part_number(self):
        """Test normalization without part number."""
        title = "Episode Name | BEHIND THE BASTARDS"
        expected = "Episode Name"
        self.assertEqual(normalize_for_comparison(title), expected)


class TestSimilarityScore(unittest.TestCase):
    """Test the similarity_score function."""

    def test_identical_strings(self):
        """Test identical strings have high similarity."""
        score = similarity_score("Episode Name", "Episode Name")
        self.assertGreater(score, 0.9)

    def test_very_different_strings(self):
        """Test very different strings have low similarity."""
        score = similarity_score("Episode Name", "Completely Different")
        self.assertLess(score, 0.5)

    def test_matching_part_numbers(self):
        """Test that matching part numbers increase similarity."""
        score1 = similarity_score("Part One: Episode", "Part One: Episode")
        score2 = similarity_score("Episode", "Episode")
        # Both should be high, but Part One matching adds to confidence
        self.assertGreater(score1, 0.9)
        self.assertGreater(score2, 0.9)

    def test_mismatched_part_numbers_penalty(self):
        """Test that mismatched part numbers heavily penalize score."""
        score = similarity_score("Part One: Episode Name", "Part Two: Episode Name")
        # Should be heavily penalized despite same episode name
        self.assertLess(score, 0.7)

    def test_one_has_part_other_doesnt(self):
        """Test when only one title has a part number."""
        score = similarity_score("Part One: Episode Name", "Episode Name")
        # Should still match reasonably well
        self.assertGreater(score, 0.5)

    def test_case_insensitive_matching(self):
        """Test that matching is case insensitive."""
        score = similarity_score("Episode Name", "episode name")
        self.assertGreater(score, 0.9)

    def test_partial_match(self):
        """Test partial string matching."""
        score = similarity_score("Episode Name Test", "Episode Name")
        self.assertGreater(score, 0.6)

    def test_empty_strings(self):
        """Test empty string handling."""
        score = similarity_score("", "")
        # Empty strings should match perfectly
        self.assertEqual(score, 1.0)

    def test_unicode_normalization(self):
        """Test unicode characters are normalized before comparison."""
        score = similarity_score("Episode： Name", "Episode: Name")
        self.assertGreater(score, 0.9)


class TestExtractFilenameFromLsLine(unittest.TestCase):
    """Test the extract_filename_from_ls_line function."""
    
    # Standard ls -l output format template for creating test lines
    # Format: permissions user group size date time
    LS_FORMAT = "-rw-r--r-- 1 user group 12345 Jan 1 12:00"

    def _make_ls_line(self, path):
        """Helper method to create ls -l output line."""
        return f"{self.LS_FORMAT} {path}"

    def test_extract_simple_path(self):
        """Test extraction from simple ls -l line."""
        line = self._make_ls_line("/mnt/user/media/Episode.mp3")
        expected = "Episode.mp3"
        self.assertEqual(extract_filename_from_ls_line(line), expected)

    def test_extract_with_escaped_spaces(self):
        """Test extraction with escaped spaces in path."""
        line = self._make_ls_line(r"/mnt/user/media/Episode\ Name.mp3")
        expected = "Episode Name.mp3"
        self.assertEqual(extract_filename_from_ls_line(line), expected)

    def test_extract_long_path(self):
        """Test extraction from long path."""
        line = self._make_ls_line("/mnt/user/media/podcasts/btb/Episode.mp3")
        expected = "Episode.mp3"
        self.assertEqual(extract_filename_from_ls_line(line), expected)

    def test_extract_no_mp3(self):
        """Test when line doesn't contain .mp3."""
        line = self._make_ls_line("/mnt/user/media/file.txt")
        self.assertIsNone(extract_filename_from_ls_line(line))

    def test_extract_no_path(self):
        """Test when line doesn't contain expected path."""
        line = "some random text"
        self.assertIsNone(extract_filename_from_ls_line(line))

    def test_extract_multiple_backslashes(self):
        """Test extraction with multiple escaped spaces."""
        line = self._make_ls_line(r"/mnt/user/media/Episode\ Name\ Part\ One.mp3")
        expected = "Episode Name Part One.mp3"
        self.assertEqual(extract_filename_from_ls_line(line), expected)


class TestFindBestMatch(unittest.TestCase):
    """Test the find_best_match function."""

    def test_find_exact_match(self):
        """Test finding exact match."""
        playlist_name = "Episode Name"
        mp3_files = ["Episode Name.mp3", "Other Episode.mp3", "Third Episode.mp3"]
        used_indices = set()

        match, score, idx = find_best_match(playlist_name, mp3_files, used_indices)

        self.assertEqual(match, "Episode Name.mp3")
        self.assertGreater(score, 0.9)
        self.assertEqual(idx, 0)

    def test_find_close_match(self):
        """Test finding close but not exact match."""
        playlist_name = "Episode Name"
        mp3_files = ["Episode_Name.mp3", "Other Episode.mp3"]
        used_indices = set()

        match, score, idx = find_best_match(playlist_name, mp3_files, used_indices)

        self.assertEqual(match, "Episode_Name.mp3")
        self.assertGreater(score, 0.6)

    def test_skip_used_indices(self):
        """Test that used indices are skipped."""
        playlist_name = "Episode Name"
        mp3_files = ["Episode Name.mp3", "Episode Name 2.mp3"]
        used_indices = {0}  # First file already used

        match, score, idx = find_best_match(playlist_name, mp3_files, used_indices)

        self.assertEqual(match, "Episode Name 2.mp3")
        self.assertEqual(idx, 1)

    def test_no_match_available(self):
        """Test when all files are used."""
        playlist_name = "Episode Name"
        mp3_files = ["Episode 1.mp3", "Episode 2.mp3"]
        used_indices = {0, 1}  # All files used

        match, score, idx = find_best_match(playlist_name, mp3_files, used_indices)

        self.assertIsNone(match)
        self.assertEqual(score, 0)
        self.assertIsNone(idx)

    def test_empty_mp3_list(self):
        """Test with empty MP3 file list."""
        playlist_name = "Episode Name"
        mp3_files = []
        used_indices = set()

        match, score, idx = find_best_match(playlist_name, mp3_files, used_indices)

        self.assertIsNone(match)
        self.assertEqual(score, 0)
        self.assertIsNone(idx)

    def test_part_number_matching(self):
        """Test that part numbers affect matching."""
        playlist_name = "Part Two: Episode Name"
        mp3_files = ["Part One Episode Name.mp3", "Part Two Episode Name.mp3"]
        used_indices = set()

        match, score, idx = find_best_match(playlist_name, mp3_files, used_indices)

        self.assertEqual(match, "Part Two Episode Name.mp3")
        self.assertEqual(idx, 1)


class TestEdgeCases(unittest.TestCase):
    """Test edge cases and error scenarios."""

    def test_normalize_title_with_only_suffix(self):
        """Test title that is only the suffix."""
        title = "| BEHIND THE BASTARDS"
        result = normalize_title(title)
        self.assertEqual(result, "")

    def test_extract_part_number_invalid_word(self):
        """Test invalid part word (numbers beyond 'ten' are not supported as words).
        
        The extract_part_number function only supports word-based part numbers
        from 'one' through 'ten'. For larger numbers, numeric format (e.g., 'Part 11')
        should be used instead.
        """
        self.assertIsNone(extract_part_number("Part Eleven: Title"))

    def test_similarity_score_special_characters(self):
        """Test similarity with special characters."""
        score = similarity_score("Episode: Name!", "Episode: Name?")
        self.assertGreater(score, 0.7)

    def test_extract_filename_edge_case_double_extension(self):
        """Test filename with double extension."""
        line = "-rw-r--r-- 1 user group 12345 Jan 1 12:00 /mnt/user/media/file.tar.mp3"
        expected = "file.tar.mp3"
        self.assertEqual(extract_filename_from_ls_line(line), expected)

    def test_normalize_preserves_meaningful_punctuation(self):
        """Test that meaningful punctuation is preserved."""
        title = "Episode: The Test - Part One"
        result = normalize_title(title)
        self.assertIn(":", result)
        self.assertIn("-", result)

    def test_similarity_with_numbers(self):
        """Test similarity matching with numbers in title."""
        score = similarity_score("Episode 123: Name", "Episode 123: Name")
        self.assertGreater(score, 0.9)

    def test_part_number_requires_colon_separator(self):
        """Test that part number detection requires a colon separator.
        
        The function specifically looks for 'Part X:' or 'Pt X:' format,
        requiring the colon to separate the part number from the title.
        Without the colon, the part number is not detected.
        """
        # Should not match without colon separator
        self.assertIsNone(extract_part_number("Part One Title"))
        self.assertIsNone(extract_part_number("Pt 1 Title"))

    def test_unicode_in_all_functions(self):
        """Test unicode handling across functions."""
        title = "Episode： Name ｜ BEHIND THE BASTARDS"
        normalized = normalize_title(title)
        self.assertEqual(normalized, "Episode: Name")

        title_with_part = "Part One： Episode"
        part = extract_part_number(title_with_part)
        self.assertEqual(part, 1)


if __name__ == '__main__':
    # Run tests with verbose output
    unittest.main(verbosity=2)
