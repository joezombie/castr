#!/usr/bin/env python3
"""
Match playlist episode names to MP3 files using fuzzy string matching.
Preserves the order of the playlist while finding the best matching MP3 file for each entry.
"""

import json
import re
from difflib import SequenceMatcher
from pathlib import Path


def extract_filename_from_ls_line(line):
    """Extract the filename from an ls -l output line."""
    # Match the path after the timestamp
    match = re.search(r'/mnt/user/.*\.mp3', line)
    if match:
        full_path = match.group(0)
        # Remove escaped spaces
        full_path = full_path.replace('\\', '')
        # Extract just the filename
        return Path(full_path).name
    return None


def extract_part_number(title):
    """Extract part number from title."""
    # Try to find "Part One", "Part Two", etc.
    part_words = {
        'one': 1, 'two': 2, 'three': 3, 'four': 4, 'five': 5, 'six': 6,
        'seven': 7, 'eight': 8, 'nine': 9, 'ten': 10
    }

    # Check for "Part X:" format
    match = re.search(r'^Part\s+(One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|\d+)[:：]', title, flags=re.IGNORECASE)
    if match:
        part = match.group(1).lower()
        return part_words.get(part, int(part) if part.isdigit() else None)

    # Check for "Pt X:" format
    match = re.search(r'^Pt\s+(\d+)[:：]', title, flags=re.IGNORECASE)
    if match:
        return int(match.group(1))

    return None


def normalize_title(title):
    """Normalize title for better matching."""
    # Remove " | BEHIND THE BASTARDS" suffix
    title = re.sub(r'\s*[｜|]\s*BEHIND THE BASTARDS\s*$', '', title, flags=re.IGNORECASE)

    # Normalize unicode characters
    title = title.replace('：', ':').replace('？', '?').replace('｜', '|')

    # Remove extra whitespace
    title = ' '.join(title.split())

    return title.strip()


def normalize_for_comparison(title):
    """Normalize title for comparison - removes part numbers."""
    # Remove common prefixes
    title = re.sub(r'^Part (One|Two|Three|Four|Five|Six|Seven|Eight|Nine|Ten|\d+):\s*', '', title, flags=re.IGNORECASE)
    title = re.sub(r'^Pt\s+\d+[:：]\s*', '', title, flags=re.IGNORECASE)

    return normalize_title(title)


def similarity_score(a, b):
    """Calculate similarity score between two strings."""
    # First check if part numbers match (if both have them)
    part_a = extract_part_number(a)
    part_b = extract_part_number(b)

    # If both have part numbers and they don't match, heavily penalize
    if part_a is not None and part_b is not None and part_a != part_b:
        # Still calculate base similarity for the episode name
        base_score = SequenceMatcher(None, normalize_for_comparison(a).lower(),
                                    normalize_for_comparison(b).lower()).ratio()
        # But heavily penalize mismatched part numbers
        return base_score * 0.3

    # Calculate similarity on full normalized titles (keeping part numbers)
    a_norm = normalize_title(a).lower()
    b_norm = normalize_title(b).lower()
    full_score = SequenceMatcher(None, a_norm, b_norm).ratio()

    # Also calculate similarity without part numbers (for the episode name itself)
    a_base = normalize_for_comparison(a).lower()
    b_base = normalize_for_comparison(b).lower()
    base_score = SequenceMatcher(None, a_base, b_base).ratio()

    # If part numbers match (or one/both don't have part numbers), use weighted average
    # Give more weight to full title match
    return 0.7 * full_score + 0.3 * base_score


def find_best_match(playlist_name, mp3_files, used_indices):
    """Find the best matching MP3 file for a playlist entry."""
    best_score = 0
    best_match = None
    best_idx = None

    for idx, mp3_file in enumerate(mp3_files):
        if idx in used_indices:
            continue

        # Remove .mp3 extension for comparison
        mp3_title = mp3_file.replace('.mp3', '')
        score = similarity_score(playlist_name, mp3_title)

        if score > best_score:
            best_score = score
            best_match = mp3_file
            best_idx = idx

    return best_match, best_score, best_idx


def main():
    # Read the files list
    print("Reading files1.txt...")
    with open('files1.txt', 'r', encoding='utf-8') as f:
        lines = f.readlines()

    mp3_files = []
    mp3_paths = []
    for line in lines:
        filename = extract_filename_from_ls_line(line)
        if filename:
            mp3_files.append(filename)
            mp3_paths.append(line.strip())

    print(f"Found {len(mp3_files)} MP3 files")

    # Read the playlist
    print("Reading playlist-bulletized1.txt...")
    with open('playlist-bulletized1.txt', 'r', encoding='utf-8') as f:
        lines = f.readlines()

    # Parse the playlist - skip first '[' and last ']' lines, and clean up entries
    playlist = []
    for line in lines[1:-1]:  # Skip first and last line
        # Remove leading/trailing whitespace and trailing comma
        entry = line.strip().rstrip(',')
        if entry and entry != "Private video":
            playlist.append(entry)

    print(f"Found {len(playlist)} playlist entries (excluding private videos)")

    # Match playlist entries to MP3 files
    print("\nMatching playlist entries to MP3 files...")
    print("=" * 80)

    matches = []
    used_indices = set()

    for i, playlist_entry in enumerate(playlist, 1):
        best_match, score, match_idx = find_best_match(playlist_entry, mp3_files, used_indices)

        if best_match:
            used_indices.add(match_idx)
            matches.append({
                'order': i,
                'playlist_name': playlist_entry,
                'mp3_file': best_match,
                'match_score': score,
                'file_index': match_idx
            })

            print(f"\n{i}. {playlist_entry}")
            print(f"   → {best_match}")
            print(f"   (match score: {score:.2%})")
        else:
            print(f"\n{i}. {playlist_entry}")
            print(f"   → NO MATCH FOUND")

    # Save results to JSON
    print("\n" + "=" * 80)
    print(f"\nSaving results to matched_episodes.json...")
    with open('matched_episodes.json', 'w', encoding='utf-8') as f:
        json.dump(matches, f, indent=2, ensure_ascii=False)

    # Create a simple mapping file
    print("Saving simple mapping to episode_mapping.txt...")
    with open('episode_mapping.txt', 'w', encoding='utf-8') as f:
        for match in matches:
            f.write(f"{match['order']}. {match['playlist_name']}\n")
            f.write(f"   → {match['mp3_file']}\n\n")

    # Statistics
    print("\n" + "=" * 80)
    print("\nStatistics:")
    print(f"  Total playlist entries: {len(playlist)}")
    print(f"  Total MP3 files: {len(mp3_files)}")
    print(f"  Successful matches: {len(matches)}")
    print(f"  Unmatched MP3 files: {len(mp3_files) - len(used_indices)}")

    avg_score = sum(m['match_score'] for m in matches) / len(matches) if matches else 0
    print(f"  Average match score: {avg_score:.2%}")

    low_confidence = [m for m in matches if m['match_score'] < 0.7]
    if low_confidence:
        print(f"\n  ⚠️  {len(low_confidence)} matches have low confidence (< 70%):")
        for m in low_confidence[:5]:  # Show first 5
            print(f"     - {m['playlist_name']} → {m['mp3_file']} ({m['match_score']:.2%})")
        if len(low_confidence) > 5:
            print(f"     ... and {len(low_confidence) - 5} more")

    print("\nDone! Results saved to:")
    print("  - matched_episodes.json (detailed JSON)")
    print("  - episode_mapping.txt (human-readable)")


if __name__ == '__main__':
    main()
